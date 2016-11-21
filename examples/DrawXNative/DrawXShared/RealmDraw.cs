﻿////////////////////////////////////////////////////////////////////////////
//
// Copyright 2014 Realm Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using SkiaSharp;
using Realms;
using Realms.Sync;


namespace DrawXShared
{
    /***
     Class that does almost everything for the demo that can be shared.
     It combines drawing with logging in and connecting to the server.

     DrawXSettingsManager provides a singleton to wrap settings and their local Realm.

    Login
    -----
    Credentials come from DrawXSettings.
    It is the responsibility of external GUI classes to get credentials entered and delay
    starting RealmDraw until connection is made.

    Drawing
    -------
    There are three components to drawing:
    - the background images for "controls"
    - previously drawn completed, paths which will no longer grow,
    - the currently growing paths (one per app updating the shared Realm).

    Caching and Responsiveness
    --------------------------
    Ideally, we want to have almost all the drawn content cached in a bitmap for redisplay, 
    and only draw new line segments for each added point. It is relatively easy to optimise for
    local draw updates because we know when touched that we are drawing a single added segment.

    There are two distinct patterns which can cause a poor demo with synchronised Draw.
    - A long, winding path being updated - there may be enough lag that we have to add more than 
      one point to it locally but we don't want to redraw the whole thing from scratch.      
    - Many small, single "dab" strokes being drawn, say from someone tapping a display, 
      which mean we have, at least, a TouchesBegan and TouchesEnded and probably AddPoint in between.
    
    We make use of some non-persistent properties to help a given Draw differentiate which 
    points it has processed from those 
    
    */
    public class RealmDraw
    {
        private Realm _realm;
        internal Realm.RealmChangedEventHandler RefreshOnRealmUpdate { get; set; }
        internal Action CredentialsEditor { get; set; }

        #region DrawingState
        private bool _isDrawing = false;
        private bool _ignoringTouches = false;
        private DrawPath _drawPath;
        private float _canvasWidth, _canvasHeight;
        private const float NORMALISE_TO = 4000.0f;
        private const float PENCIL_MARGIN = 4.0f;
        private const float INVALID_LAST_COORD = -1.0f;
        private float _lastX = INVALID_LAST_COORD;
        private float _lastY = INVALID_LAST_COORD;

        #endregion

        #region CachedCanvas
        private Int32 _canvasSaveCount;  // from SaveLayer
        private bool _hasSavedBitmap = false;  // separate flag so we don't rely on any given value in _canvasSaveCount
        private bool _redrawPathsAtNextDraw = true;
        #endregion

        #region Touch Areas
        private SKRect _loginIconRect;
        private SKRect _loginIconTouchRect;
        // setup in DrawBackground
        private float _pencilWidth;
        private float _pencilsTop;
        private int _numPencils;
        private List<SKBitmap> _pencilBitmaps;
        private SKBitmap _loginIconBitmap;
        #endregion

        #region LoginState
        private bool _waitingForLogin = false;
        #endregion

        #region Settings
        private DrawXSettings Settings => DrawXSettingsManager.Settings;
        private int _currentColorIndex;  // for quick check if pencil we draw is current color
        private SwatchColor _currentColor;
        private SwatchColor currentColor
        {
            get
            {
                if (String.IsNullOrEmpty(_currentColor.Name))
                {
                    _currentColor = SwatchColor.ColorsByName[Settings.LastColorUsed];
                    _currentColorIndex = SwatchColor.Colors.IndexOf(_currentColor);
                }
                return _currentColor;
            }
            set
            {
                if (!_currentColor.Name.Equals(value.Name))
                {
                    _currentColor = value;
                    DrawXSettingsManager.Write(() => Settings.LastColorUsed = _currentColor.Name);
                    _currentColorIndex = SwatchColor.Colors.IndexOf(_currentColor);
                }

            }
        }
        #endregion Settings

        public RealmDraw(float inWidth, float inHeight)
        {
            // TODO close the Realm
            _canvasWidth = inWidth;
            _canvasHeight = inHeight;

            // simple local open            
            //_realm = Realm.GetInstance("DrawX.realm");

            _pencilBitmaps = new List<SKBitmap>(SwatchColor.Colors.Count);
            foreach (var swatch in SwatchColor.Colors)
            {
                _pencilBitmaps.Add(EmbeddedMedia.BitmapNamed(swatch.Name + ".png"));
            }
            _loginIconBitmap = EmbeddedMedia.BitmapNamed("CloudIcon.png");
        }


        internal void InvalidateCachedPaths()
        {
            _redrawPathsAtNextDraw = true;
            _hasSavedBitmap = false;
        }

        internal async void LoginToServerAsync()
        {
            if (_realm != null)
            {
                // TODO more logout?
                _realm.RealmChanged -= RefreshOnRealmUpdate;  // don't want old event notifications from unused Realm
            }
            _waitingForLogin = true;
            var s = Settings;
            // TODO allow entering Create User flag on credentials to pass in here instead of false
            var credentials = Credentials.UsernamePassword(s.Username, s.Password, false);
            var user = await User.LoginAsync(credentials, new Uri($"http://{s.ServerIP}"));
            Debug.WriteLine($"Got user logged in with refresh token {user.RefreshToken}");

            var loginConf = new SyncConfiguration(user, new Uri($"realm://{s.ServerIP}/~/Draw"));
            _realm = Realm.GetInstance(loginConf);
            _realm.RealmChanged += RefreshOnRealmUpdate;
            RefreshOnRealmUpdate(_realm, null);  // force initial draw on login
            _waitingForLogin = false;
        }

        private void ScalePointsToStore(ref float w, ref float h)
        {
            w *= NORMALISE_TO / _canvasWidth;
            h *= NORMALISE_TO / _canvasHeight;
        }

        private void ScalePointsToDraw(ref float w, ref float h)
        {
            w *= _canvasWidth / NORMALISE_TO;
            h *= _canvasHeight / NORMALISE_TO;
        }


        private bool TouchInControlArea(float inX, float inY)
        {
            if (_loginIconTouchRect.Contains(inX, inY))
            {
                InvalidateCachedPaths();
                CredentialsEditor();  // TODO only invalidate if changed server??
                return true;
            }
            if (inY < _pencilsTop)
                return false;
            int pencilIndex = (int)(inX / (_pencilWidth + PENCIL_MARGIN));
            // see opposite calc in DrawBackground
            var selectecColor = SwatchColor.Colors[pencilIndex];
            if (!selectecColor.Name.Equals(currentColor.Name))
            {
                currentColor = selectecColor;  // will update saved settings
            }
            InvalidateCachedPaths();
            return true;  // if in this area even if didn't actually change
        }


        private void DrawPencils(SKCanvas canvas, SKPaint paint)
        {
            // draw pencils, assigning the fields used for touch detection
            _numPencils = SwatchColor.ColorsByName.Count;
            var marginAlloc = (_numPencils + 1) * PENCIL_MARGIN;
            _pencilWidth = (canvas.ClipBounds.Width - marginAlloc) / _numPencils;  // see opposite calc in TouchInControlArea
            var pencilHeight = _pencilWidth * 334.0f / 112.0f;  // scale as per originals
            float runningLeft = PENCIL_MARGIN;
            float pencilsBottom = canvas.ClipBounds.Height;
            _pencilsTop = pencilsBottom - pencilHeight;
            int _pencilIndex = 0;
            foreach (var swatchBM in _pencilBitmaps)
            {
                var pencilRect = new SKRect(runningLeft, _pencilsTop, runningLeft + _pencilWidth, pencilsBottom);
                if (_pencilIndex++ == _currentColorIndex)
                {
                    var offsetY = -Math.Max(20.0f, pencilHeight / 4.0f);
                    pencilRect.Offset(0.0f, offsetY);  // show selected color
                }
                canvas.DrawBitmap(swatchBM, pencilRect, paint);
                runningLeft += PENCIL_MARGIN + _pencilWidth;
            }
        }


        private void DrawLoginIcon(SKCanvas canvas, SKPaint paint)
        {
            if (_loginIconRect.Width <= 0.1f)
            {
                const float ICON_WIDTH = 84.0f;
                const float ICON_HEIGHT = 54.0f;
#if __IOS__
                const float TOP_BAR_OFFSET = 48.0f;
#else
                const float TOP_BAR_OFFSET = 8.0f;
#endif
                _loginIconRect = new SKRect(8.0f, TOP_BAR_OFFSET, 8.0f + ICON_WIDTH, TOP_BAR_OFFSET + ICON_HEIGHT);
                _loginIconTouchRect = new SKRect(0.0f, 0.0f,
                                                 Math.Max(_loginIconRect.Right + 4.0f, 44.0f),
                                                 Math.Max(_loginIconRect.Bottom + 4.0f, 44.0f)
                                                );
            }
            canvas.DrawBitmap(_loginIconBitmap, _loginIconRect, paint);
        }


        private void DrawAPath(SKCanvas canvas, SKPaint paint, DrawPath drawPath)
        {
            using (SKPath path = new SKPath())
            {
                var pathColor = SwatchColor.ColorsByName[drawPath.color].Color;
                paint.Color = pathColor;
                bool isFirst = true;
                int numDrawn = 0;
                foreach (var point in drawPath.points)
                {
                    // for compatibility with iOS Realm, stores floats, normalised to NORMALISE_TO
                    float fx = (float)point.x;
                    float fy = (float)point.y;
                    ScalePointsToDraw(ref fx, ref fy);
                    if (isFirst)
                    {
                        isFirst = false;
                        path.MoveTo(fx, fy);
                    }
                    else
                    {
                        path.LineTo(fx, fy);
                    }
                    numDrawn++;
                }
                drawPath.NumPointsDrawnLocally = numDrawn;
                canvas.DrawPath(path, paint);
            }
        }


        private void DrawAPathUndrawnBits(SKCanvas canvas, SKPaint paint, DrawPath drawPath)
        {
            int numPoints = drawPath.points.Count;
            int numToDraw = numPoints - drawPath.NumPointsDrawnLocally;
            if (numToDraw <= 0)
            {
                Debug.WriteLine($"Skipping a partial path because all {numPoints} drawn before");
                return;  // have drawn all of this partial path in the last refresh
            }
            // we know we have at least one preceding point to iterate to
            Debug.Assert(drawPath.NumPointsDrawnLocally > 0);  // guarded by caller
            using (SKPath path = new SKPath())
            {
                var pathColor = SwatchColor.ColorsByName[drawPath.color].Color;
                paint.Color = pathColor;
                int pointIndex = drawPath.NumPointsDrawnLocally - 1;
                var firstPoint = drawPath.points[pointIndex];
                float fx = (float)firstPoint.x;
                float fy = (float)firstPoint.y;
                ScalePointsToDraw(ref fx, ref fy);
                Debug.WriteLine($"Drawing a partial path remaining {numToDraw} points");
                path.MoveTo(fx, fy);
                while (++pointIndex < numPoints)
                {
                    var point = drawPath.points[pointIndex];
                    fx = (float)point.x;
                    fy = (float)point.y;
                    ScalePointsToDraw(ref fx, ref fy);
                    path.LineTo(fx, fy);
                }
                drawPath.NumPointsDrawnLocally = numPoints;
                canvas.DrawPath(path, paint);
            }
        }


        // replaces the CanvasView.drawRect of the original
        public void DrawTouches(SKCanvas canvas)
        {
            if (_realm == null)
                return;  // too early to have finished login
            if (_hasSavedBitmap)
            {
                Debug.WriteLine($"DrawTouches - blitting saved canvas");
                canvas.RestoreToCount(_canvasSaveCount);  // use up the offscreen bitmap regardless
            }

            using (SKPaint paint = new SKPaint())
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = 10;
                paint.IsAntialias = true;
                paint.StrokeCap = SKStrokeCap.Round;
                paint.StrokeJoin = SKStrokeJoin.Round;
                if (_redrawPathsAtNextDraw)
                {
                    Debug.WriteLine($"DrawTouches - Redrawing all paths");
                    canvas.Clear(SKColors.White);
                    DrawPencils(canvas, paint);
                    DrawLoginIcon(canvas, paint);
                    foreach (var drawPath in _realm.All<DrawPath>())
                    {
                        DrawAPath(canvas, paint, drawPath);
                    }
                }
                else
                {
                    Debug.WriteLine($"DrawTouches - just redrawing paths in progress");
                    // current paths being drawn, here or by other devices
                    foreach (var drawPath in _realm.All<DrawPath>().Where(path => path.drawerID == null))
                    {
                        if (drawPath.NumPointsDrawnLocally == 0)
                            DrawAPath(canvas, paint, drawPath);
                        else
                            DrawAPathUndrawnBits(canvas, paint, drawPath);
                    }
                }
                _canvasSaveCount = canvas.SaveLayer(paint);  // cache everything to-date
                _hasSavedBitmap = true;
            } // SKPaint
            _redrawPathsAtNextDraw = false;
        }


        public void StartDrawing(float inX, float inY, ref bool needsRefresh)
        {
            if (TouchInControlArea(inX, inY))
            {
                _ignoringTouches = true;
                needsRefresh = true;
                return;
            }
            _ignoringTouches = false;
            if (_realm == null)
            {
                if (!_waitingForLogin)
                    LoginToServerAsync();
                return;  // not yet logged into server, let next touch invoke us
            }
            _lastX = inX;
            _lastY = inY;
            Debug.WriteLine($"Writing a new path starting at {inX}, {inY}");
            ScalePointsToStore(ref inX, ref inY);
            _isDrawing = true;
            // TODO smarter guard against _realm null
            _realm.Write(() =>
            {
                _drawPath = new DrawPath() { color = currentColor.Name, NumPointsDrawnLocally = 0 };  // Realm saves name of color
                _drawPath.points.Add(new DrawPoint() { x = inX, y = inY });
                _realm.Add(_drawPath);
            });
        }

        public void AddPoint(float inX, float inY)
        {
            if (_ignoringTouches)
                return;  // probably touched in pencil area
            if (_realm == null)
                return;  // not yet logged into server
            if (!_isDrawing)
            {
                // has finished connecting to Realm so this is actually a start
                bool ignored = false;
                StartDrawing(inX, inY, ref ignored);
                return;
            }
            _lastX = inX;
            _lastY = inY;
            Debug.WriteLine($"Adding a point at {inX}, {inY}");
            ScalePointsToStore(ref inX, ref inY);
            //TODO add check if _drawPath.IsInvalidated
            _realm.Write(() =>
            {
                _drawPath.points.Add(new DrawPoint() { x = inX, y = inY });
            });
            Debug.WriteLine("AddPoint - Just after adding a point to the Realm");
        }


        public void StopDrawing(float inX, float inY)
        {
            if (_ignoringTouches)
                return;  // probably touched in pencil area
            _ignoringTouches = false;
            _isDrawing = false;
            if (_realm == null)
                return;  // not yet logged into server

            bool stoppedWithoutMoving = (_lastX == inX) && (_lastY == inY);
            _lastX = INVALID_LAST_COORD;
            _lastY = INVALID_LAST_COORD;

            Debug.WriteLine($"Ending a path at {inX}, {inY}");
            ScalePointsToStore(ref inX, ref inY);
            _realm.Write(() =>
            {
                if (!stoppedWithoutMoving) 
                {
                    _drawPath.points.Add(new DrawPoint() { x = inX, y = inY });
                }
                _drawPath.drawerID = "";  // TODO work out what the intent is here in original Draw sample!
            });
        }

        public void CancelDrawing()
        {
            _isDrawing = false;
            _ignoringTouches = false;
            _lastX = INVALID_LAST_COORD;
            _lastY = INVALID_LAST_COORD;
            InvalidateCachedPaths();
            // TODO wipe current path
        }

        public void ErasePaths()
        {
            InvalidateCachedPaths();
            _realm.Write(() => _realm.RemoveAll<DrawPath>());
        }
    }
}
