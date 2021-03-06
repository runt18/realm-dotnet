////////////////////////////////////////////////////////////////////////////
//
// Copyright 2016 Realm Inc.
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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if __IOS__
using ObjCRuntime;
#endif

namespace Realms
{
    /// <summary>
    /// Base for any object that can be persisted in a Realm.
    /// </summary>
    /// <remarks>
    /// Has a Preserve attribute to attempt to preserve all subtypes without having to weave.
    /// </remarks>
    [Preserve(AllMembers = true, Conditional = false)]
    public class RealmObject : IReflectableType, INotifyPropertyChanged
    {
        #region static

        #if __IOS__
        [MonoPInvokeCallback(typeof(NativeCommon.NotifyRealmCallback))]
        #endif
        internal static void NotifyRealmObjectPropertyChanged(IntPtr realmObjectHandle, IntPtr propertyIndex)
        {
            var gch = GCHandle.FromIntPtr(realmObjectHandle);
            var realmObject = (RealmObject)gch.Target;
            var property = realmObject.ObjectSchema.ElementAtOrDefault((int)propertyIndex);
            realmObject.RaisePropertyChanged(property.PropertyInfo?.Name ?? property.Name);
        }

        #endregion

        private Realm _realm;
        private ObjectHandle _objectHandle;
        private Metadata _metadata;
        private GCHandle? _notificationsHandle;

        private event PropertyChangedEventHandler _propertyChanged;

        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                if (IsManaged && _propertyChanged == null)
                {
                    SubscribeForNotifications();
                }

                _propertyChanged += value;
            }

            remove
            {
                _propertyChanged -= value;

                if (IsManaged &&
                    _propertyChanged == null)
                {
                    UnsubscribeFromNotifications();
                }
            }
        }

        internal ObjectHandle ObjectHandle => _objectHandle;

        internal Metadata ObjectMetadata => _metadata;

        /// <summary>
        /// Allows you to check if the object has been associated with a Realm, either at creation or via Realm.Add.
        /// </summary>
        public bool IsManaged => _realm != null;

        /// <summary>
        /// Returns true if this object is managed and represents a row in the database.
        /// If a managed object has been removed from the Realm, it is no longer valid and accessing properties on it
        /// will throw an exception.
        /// Unmanaged objects are always considered valid.
        /// </summary>
        public bool IsValid => _objectHandle?.IsValid != false;

        /// <summary>
        /// The <see cref="Realm"/> instance this object belongs to, or <code>null</code> if it is unmanaged.
        /// </summary>
        public Realm Realm => _realm;

        /// <summary>
        /// The <see cref="Schema.ObjectSchema"/> instance that describes how the <see cref="Realm"/> this object belongs to sees it.
        /// </summary>
        public Schema.ObjectSchema ObjectSchema => _metadata?.Schema;

        internal void _SetOwner(Realm realm, ObjectHandle objectHandle, Metadata metadata)
        {
            _realm = realm;
            _objectHandle = objectHandle;
            _metadata = metadata;

            if (_propertyChanged != null)
            {
                SubscribeForNotifications();
            }
        }

        internal class Metadata
        {
            internal TableHandle Table;

            internal Weaving.IRealmObjectHelper Helper;

            internal Dictionary<string, IntPtr> PropertyIndices;

            internal Schema.ObjectSchema Schema;
        }

        #region Getters

        protected string GetStringValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetString(_metadata.PropertyIndices[propertyName]);
        }

        protected char GetCharValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return (char)_objectHandle.GetInt64(_metadata.PropertyIndices[propertyName]);
        }

        protected char? GetNullableCharValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return (char?)_objectHandle.GetNullableInt64(_metadata.PropertyIndices[propertyName]);
        }

        protected byte GetByteValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return (byte)_objectHandle.GetInt64(_metadata.PropertyIndices[propertyName]);
        }

        protected byte? GetNullableByteValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return (byte?)_objectHandle.GetNullableInt64(_metadata.PropertyIndices[propertyName]);
        }

        protected short GetInt16Value(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return (short)_objectHandle.GetInt64(_metadata.PropertyIndices[propertyName]);
        }

        protected short? GetNullableInt16Value(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return (short?)_objectHandle.GetNullableInt64(_metadata.PropertyIndices[propertyName]);
        }

        protected int GetInt32Value(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return (int)_objectHandle.GetInt64(_metadata.PropertyIndices[propertyName]);
        }

        protected int? GetNullableInt32Value(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return (int?)_objectHandle.GetNullableInt64(_metadata.PropertyIndices[propertyName]);
        }

        protected long GetInt64Value(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetInt64(_metadata.PropertyIndices[propertyName]);
        }

        protected long? GetNullableInt64Value(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetNullableInt64(_metadata.PropertyIndices[propertyName]);
        }

        protected float GetSingleValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetSingle(_metadata.PropertyIndices[propertyName]);
        }

        protected float? GetNullableSingleValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetNullableSingle(_metadata.PropertyIndices[propertyName]);
        }

        protected double GetDoubleValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetDouble(_metadata.PropertyIndices[propertyName]);
        }

        protected double? GetNullableDoubleValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetNullableDouble(_metadata.PropertyIndices[propertyName]);
        }

        protected bool GetBooleanValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetBoolean(_metadata.PropertyIndices[propertyName]);
        }

        protected bool? GetNullableBooleanValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetNullableBoolean(_metadata.PropertyIndices[propertyName]);
        }

        protected DateTimeOffset GetDateTimeOffsetValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetDateTimeOffset(_metadata.PropertyIndices[propertyName]);
        }

        protected DateTimeOffset? GetNullableDateTimeOffsetValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetNullableDateTimeOffset(_metadata.PropertyIndices[propertyName]);
        }

        protected IList<T> GetListValue<T>(string propertyName) where T : RealmObject
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            Schema.Property property;
            _metadata.Schema.TryFindProperty(propertyName, out property);
            var relatedMeta = _realm.Metadata[property.ObjectType];

            var listHandle = _objectHandle.TableLinkList(_metadata.PropertyIndices[propertyName]);
            return new RealmList<T>(_realm, listHandle, relatedMeta);
        }

        protected T GetObjectValue<T>(string propertyName) where T : RealmObject
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            var linkedObjectPtr = _objectHandle.GetLink(_metadata.PropertyIndices[propertyName]);
            if (linkedObjectPtr == IntPtr.Zero)
            {
                return null;
            }

            Schema.Property property;
            _metadata.Schema.TryFindProperty(propertyName, out property);
            var objectType = property.ObjectType;
            return (T)_realm.MakeObject(objectType, linkedObjectPtr);
        }

        protected byte[] GetByteArrayValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetByteArray(_metadata.PropertyIndices[propertyName]);
        }

        #endregion

        #region Setters

        protected void SetStringValue(string propertyName, string value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetString(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetStringValueUnique(string propertyName, string value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetStringUnique(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetCharValue(string propertyName, char value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetCharValueUnique(string propertyName, char value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetInt64Unique(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableCharValue(string propertyName, char? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableCharValueUnique(string propertyName, char? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64Unique(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetByteValue(string propertyName, byte value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetByteValueUnique(string propertyName, byte value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetInt64Unique(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableByteValue(string propertyName, byte? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableByteValueUnique(string propertyName, byte? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64Unique(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetInt16Value(string propertyName, short value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetInt16ValueUnique(string propertyName, short value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetInt64Unique(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableInt16Value(string propertyName, short? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableInt16ValueUnique(string propertyName, short? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64Unique(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetInt32Value(string propertyName, int value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetInt32ValueUnique(string propertyName, int value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetInt64Unique(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableInt32Value(string propertyName, int? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableInt32ValueUnique(string propertyName, int? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64Unique(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetInt64Value(string propertyName, long value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetInt64ValueUnique(string propertyName, long value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetInt64Unique(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableInt64Value(string propertyName, long? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableInt64ValueUnique(string propertyName, long? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableInt64Unique(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetSingleValue(string propertyName, float value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetSingle(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableSingleValue(string propertyName, float? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableSingle(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetDoubleValue(string propertyName, double value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetDouble(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableDoubleValue(string propertyName, double? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableDouble(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetBooleanValue(string propertyName, bool value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetBoolean(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableBooleanValue(string propertyName, bool? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableBoolean(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetDateTimeOffsetValue(string propertyName, DateTimeOffset value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetDateTimeOffset(_metadata.PropertyIndices[propertyName], value);
        }

        protected void SetNullableDateTimeOffsetValue(string propertyName, DateTimeOffset? value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetNullableDateTimeOffset(_metadata.PropertyIndices[propertyName], value);
        }

        // Originally a generic fallback, now used only for RealmObject To-One relationship properties
        // most other properties handled with woven type-specific setters above
        protected void SetObjectValue<T>(string propertyName, T value) where T : RealmObject
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            if (value == null)
            {
                _objectHandle.ClearLink(_metadata.PropertyIndices[propertyName]);
            }
            else
            {
                if (!value.IsManaged)
                {
                    _realm.Add(value);
                }

                _objectHandle.SetLink(_metadata.PropertyIndices[propertyName], value.ObjectHandle);
            }
        }

        protected void SetByteArrayValue(string propertyName, byte[] value)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetByteArray(_metadata.PropertyIndices[propertyName], value);
        }

        #endregion

        /// <summary>
        /// Compare objects with identity query for persistent objects.
        /// </summary>
        /// <remarks>Persisted RealmObjects map their properties directly to the realm with no caching so multiple instances of a given object always refer to the same store.</remarks>
        /// <param name="obj">Object being compared against to see if is the same C# object or maps to the same managed object in Realm.</param>
        /// <returns>True when objects are the same memory object or refer to the same persisted object.</returns>
        public override bool Equals(object obj)
        {
            // If parameter is null, return false. 
            if (ReferenceEquals(obj, null))
            {
                return false;
            }

            // Optimization for a common success case. 
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false. 
            if (GetType() != obj.GetType())
            {
                return false;
            }

            // standalone objects cannot participate in the same store check
            if (!IsManaged)
            {
                return false;
            }

            // Return true if the fields match. 
            // Note that the base class is not invoked because it is 
            // System.Object, which defines Equals as reference equality. 
            return ObjectHandle.Equals(((RealmObject)obj).ObjectHandle);
        }

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        TypeInfo IReflectableType.GetTypeInfo()
        {
            return RealmObjectTypeInfo.FromType(this.GetType());
        }

        private void SubscribeForNotifications()
        {
            Debug.Assert(!_notificationsHandle.HasValue, "Notification handle must be null before subscribing");

            var managedRealmHandle = GCHandle.Alloc(_realm, GCHandleType.Weak);
            _notificationsHandle = GCHandle.Alloc(this, GCHandleType.Weak);
            _realm.SharedRealmHandle.AddObservedObject(GCHandle.ToIntPtr(managedRealmHandle), this.ObjectHandle, GCHandle.ToIntPtr(_notificationsHandle.Value));
        }

        private void UnsubscribeFromNotifications()
        {
            Debug.Assert(_notificationsHandle.HasValue, "Notification handle must not be null to unsubscribe");

            if (_notificationsHandle.HasValue)
            {
                _realm.SharedRealmHandle.RemoveObservedObject(GCHandle.ToIntPtr(_notificationsHandle.Value));
                _notificationsHandle.Value.Free();
                _notificationsHandle = null;
            }
        }
    }
}