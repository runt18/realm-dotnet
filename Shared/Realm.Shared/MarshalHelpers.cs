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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Realms
{
    internal class MarshalHelpers
    {
        public static IntPtr BoolToIntPtr(bool value)
        {
            return value ? (IntPtr)1 : (IntPtr)0;
        }

        public static bool IntPtrToBool(IntPtr value)
        {
            return (IntPtr)1 == value;
        }

        public static IntPtr RealmColType(Type columnType)
        {
            // values correspond to core/data_type.hpp enum DataType
            // ordered in decreasing likelihood of type
            if (columnType == typeof(string))
            {
                return (IntPtr)2;  // type_String
            }

            if (columnType == typeof(char) || columnType == typeof(byte) || columnType == typeof(short) || columnType == typeof(int) || columnType == typeof(long) ||
                columnType == typeof(char?) || columnType == typeof(byte?) || columnType == typeof(short?) || columnType == typeof(int?) || columnType == typeof(long?))
            {
                return (IntPtr)0;  // type_Int
            }

            if (columnType == typeof(float) || columnType == typeof(float?))
            {
                return (IntPtr)9;  // type_Float
            }

            if (columnType == typeof(double) || columnType == typeof(double?))
            {
                return (IntPtr)10; // type_Double
            }

            if (columnType == typeof(DateTimeOffset) || columnType == typeof(DateTimeOffset?))
            {
                return (IntPtr)8;  // type_Timestamp
            }

            if (columnType == typeof(bool) || columnType == typeof(bool?))
            {
                return (IntPtr)1;  // type_Bool
            }

            if (columnType == typeof(byte[]))
            {
                return (IntPtr)4; // type_Data
            }

            if (columnType.BaseType == typeof(RealmObject))
            {
                return (IntPtr)12;  // type_Link
            }

            if (columnType.IsGenericType)
            {
                var type = columnType.GetGenericTypeDefinition();
                if (type == typeof(RealmList<>) || type == typeof(IList<>))
                {
                    return (IntPtr)13;  // type_LinkList 
                }
            }

            /*
            TODO
                    Table = 5, // type_Table for sub-tables, not relatinoships????
                    Mixed = 6, // type_Mixed

            */
            throw new NotImplementedException("Type " + columnType.FullName + " not supported");
        }

        public delegate IntPtr NativeStringGetter(IntPtr buffer, IntPtr bufferLength, out bool isNull, out NativeException ex);

        public static string GetString(NativeStringGetter getter)
        {
            var bufferSizeNeededChars = 128;

            // First alloc this thread
            var stringGetBuffer = Marshal.AllocHGlobal((IntPtr)(bufferSizeNeededChars * sizeof(char)));
            var stringGetBufferLen = bufferSizeNeededChars;

            bool isNull;
            NativeException nativeException;

            // try to read
            var bytesRead = (int)getter(stringGetBuffer, (IntPtr)stringGetBufferLen, out isNull, out nativeException);
            nativeException.ThrowIfNecessary();
            if (bytesRead == -1)
            {
                throw new RealmInvalidDatabaseException("Corrupted string data");
            }

            if (bytesRead > stringGetBufferLen) // need a bigger buffer
            {
                Marshal.FreeHGlobal(stringGetBuffer);
                stringGetBuffer = Marshal.AllocHGlobal((IntPtr)(bytesRead * sizeof(char)));
                stringGetBufferLen = bytesRead;

                // try to read with big buffer
                bytesRead = (int)getter(stringGetBuffer, (IntPtr)stringGetBufferLen, out isNull, out nativeException);
                nativeException.ThrowIfNecessary();
                if (bytesRead == -1) // bad UTF-8 in full string
                {
                    throw new RealmInvalidDatabaseException("Corrupted string data");
                }

                Debug.Assert(bytesRead <= stringGetBufferLen, "Buffer must have overflowed.");
            } // needed re-read with expanded buffer

            return bytesRead != 0 ? Marshal.PtrToStringUni(stringGetBuffer, bytesRead) : (isNull ? null : string.Empty);
        }
    }
}
