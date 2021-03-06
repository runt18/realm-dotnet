﻿////////////////////////////////////////////////////////////////////////////
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
using System.Runtime.InteropServices;

namespace Realms
{
    internal class LinkListHandle : RealmHandle
    {
        private static class NativeMethods
        {
            [DllImport(InteropConfig.DLL_NAME, EntryPoint = "linklist_add", CallingConvention = CallingConvention.Cdecl)]
            public static extern void add(LinkListHandle linklistHandle, ObjectHandle objectHandle, out NativeException ex);

            [DllImport(InteropConfig.DLL_NAME, EntryPoint = "linklist_insert", CallingConvention = CallingConvention.Cdecl)]
            public static extern void insert(LinkListHandle linklistHandle, IntPtr targetIndex, ObjectHandle objectHandle, out NativeException ex);

            [DllImport(InteropConfig.DLL_NAME, EntryPoint = "linklist_erase", CallingConvention = CallingConvention.Cdecl)]
            public static extern void erase(LinkListHandle linklistHandle, IntPtr rowIndex, out NativeException ex);

            [DllImport(InteropConfig.DLL_NAME, EntryPoint = "linklist_clear", CallingConvention = CallingConvention.Cdecl)]
            public static extern void clear(LinkListHandle linklistHandle, out NativeException ex);

            [DllImport(InteropConfig.DLL_NAME, EntryPoint = "linklist_get", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr get(LinkListHandle linklistHandle, SharedRealmHandle realmHandle, IntPtr link_ndx, out NativeException ex);

            [DllImport(InteropConfig.DLL_NAME, EntryPoint = "linklist_find", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr find(LinkListHandle linklistHandle, ObjectHandle objectHandle, IntPtr start_from, out NativeException ex);

            [DllImport(InteropConfig.DLL_NAME, EntryPoint = "linklist_size", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr size(LinkListHandle linklistHandle, out NativeException ex);

            [DllImport(InteropConfig.DLL_NAME, EntryPoint = "linklist_destroy", CallingConvention = CallingConvention.Cdecl)]
            public static extern void destroy(IntPtr linklistInternalHandle);
        }

        internal LinkListHandle(RealmHandle root) : base(root)
        {
        }

        protected override void Unbind()
        {
            NativeMethods.destroy(handle);
        }

        public void Add(ObjectHandle objectHandle)
        {
            NativeException nativeException;
            NativeMethods.add(this, objectHandle, out nativeException);
            nativeException.ThrowIfNecessary();
        }

        public void Insert(IntPtr targetIndex, ObjectHandle objectHandle)
        {
            NativeException nativeException;
            NativeMethods.insert(this, targetIndex, objectHandle, out nativeException);
            nativeException.ThrowIfNecessary();
        }

        public void Erase(IntPtr rowIndex)
        {
            NativeException nativeException;
            NativeMethods.erase(this, rowIndex, out nativeException);
            nativeException.ThrowIfNecessary();
        }

        public void Clear()
        {
            NativeException nativeException;
            NativeMethods.clear(this, out nativeException);
            nativeException.ThrowIfNecessary();
        }

        public IntPtr Get(IntPtr linkIndex, SharedRealmHandle realmHandle)
        {
            NativeException nativeException;
            var result = NativeMethods.get(this, realmHandle, linkIndex, out nativeException);
            nativeException.ThrowIfNecessary();
            return result;
        }

        public IntPtr Find(ObjectHandle objectHandle, IntPtr startFrom)
        {
            NativeException nativeException;
            var result = NativeMethods.find(this, objectHandle, startFrom, out nativeException);
            nativeException.ThrowIfNecessary();
            return result;
        }

        public int Size()
        {
            NativeException nativeException;
            var result = NativeMethods.size(this, out nativeException);
            nativeException.ThrowIfNecessary();
            return (int)result;
        }
    }
}