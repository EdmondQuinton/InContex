using System;
using System.Runtime.InteropServices;

using System.Reflection;
using System.Reflection.Emit;


namespace InContex.Runtime.Serialization
{
    public class GenericSerializer<T> : ISerializer<T> where T : struct
    {
        private int _structSize;

        private delegate void StructToPtrDelegate(ref T value, IntPtr pointer);
        private delegate T PtrToStructDelegate(IntPtr pointer);

        private readonly StructToPtrDelegate GetStructPtr;
        private readonly PtrToStructDelegate PtrToStruct;

        public GenericSerializer()
        {
            this._structSize = Marshal.SizeOf(typeof(T));
            this.GetStructPtr = CreateGetStructPtr();
            this.PtrToStruct = CreatePtrToStruct();
        }


        public byte[] Serialize(T value)
        {
            byte[] buffer = new byte[this._structSize];

            IntPtr ptr = Marshal.AllocHGlobal(this._structSize);
            GetStructPtr(ref value, (IntPtr)ptr);
            Marshal.Copy(ptr, buffer, 0, this._structSize);
            Marshal.FreeHGlobal(ptr);

            return buffer;
        }

        /// <summary>
        ///  Copy content of struct to buffer referenced by pointer.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="bufferPtr"></param>
        public void Serialize(T value, IntPtr bufferPtr)
        {
            GetStructPtr(ref value, bufferPtr);
        }


        public T DeSerialize(byte[] buffer) 
        {
            unsafe
            {
                fixed (byte* pBuffer = buffer)
                {
                    return PtrToStruct((IntPtr)pBuffer);
                }
            }
        }

        public T DeSerialize(IntPtr bufferPtr)
        {
            return PtrToStruct(bufferPtr);
        }

        public int SerializedByteSize() 
        {
            return this._structSize;
        }

        private unsafe StructToPtrDelegate CreateGetStructPtr()
        {
            string methodName = "StructToPtr<" + typeof(T).FullName + ">";

            MethodInfo methodInfo = typeof(GenericSerializer<T>).GetMethod(methodName);

            if (methodInfo != null)
            {
                return (StructToPtrDelegate)methodInfo.CreateDelegate(typeof(StructToPtrDelegate));
            }
            else
            {
                var method = new DynamicMethod(methodName,
                    null, new Type[2] { typeof(T).MakeByRefType(), typeof(IntPtr) }, typeof(GenericSerializer<T>).Module);

                ILGenerator generator = method.GetILGenerator();
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldobj, typeof(T));
                generator.Emit(OpCodes.Stobj, typeof(T));
                generator.Emit(OpCodes.Ret);

                return (StructToPtrDelegate)method.CreateDelegate(typeof(StructToPtrDelegate));
            }
        }

        private static unsafe PtrToStructDelegate CreatePtrToStruct()
        {
            string methodName = "PtrToStructure<" + typeof(T).FullName + ">";

            MethodInfo methodInfo = typeof(GenericSerializer<T>).GetMethod(methodName);

            if (methodInfo != null)
            {
                return (PtrToStructDelegate)methodInfo.CreateDelegate(typeof(PtrToStructDelegate));
            }
            else
            {

                var method = new DynamicMethod(methodName,
                    typeof(T), new Type[1] { typeof(IntPtr) }, typeof(GenericSerializer<T>).Module);

                ILGenerator generator = method.GetILGenerator();
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldobj, typeof(T));
                generator.Emit(OpCodes.Ret);

                return (PtrToStructDelegate)method.CreateDelegate(typeof(PtrToStructDelegate));
            }
        }
    }
}
