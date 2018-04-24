using System;
using System.Runtime.InteropServices;

using System.Reflection;
using System.Reflection.Emit;


namespace InContex.Runtime.Serialization
{
    /// <summary>
    /// Generic serializer, serializes and deserializes fixed length struct objects into and from byte arrays.
    /// </summary>
    /// <typeparam name="T">Object type to serialize.</typeparam>
    /// <remarks>
    /// The class uses IL emitted functions to quickly convert generic structs to and from pointers. This method
    /// method was developed by Justin Stenning as part of his FastStructure class as part of his SharedMemory project. 
    /// Please see http://spazzarama.com for more detail.
    /// </remarks>
    public class GenericSerializer<T> : ISerializer<T> where T : struct
    {
        private int _structSize;

        private delegate void StructToPtrDelegate(ref T value, IntPtr pointer);
        private delegate T PtrToStructDelegate(IntPtr pointer);

        private readonly StructToPtrDelegate GetStructPtr;
        private readonly PtrToStructDelegate PtrToStruct;

        /// <summary>
        /// Constructor
        /// </summary>
        public GenericSerializer()
        {
            this._structSize = Marshal.SizeOf(typeof(T));
            this.GetStructPtr = CreateGetStructPtr();
            this.PtrToStruct = CreatePtrToStruct();
        }


        /// <summary>
        /// Serializes the specified stuct to a byte array.
        /// </summary>
        /// <param name="item">The stuct item to serialize</param>
        /// <returns>Byte array of containing serialized item.</returns>
        public byte[] Serialize(T item)
        {
            byte[] buffer = new byte[this._structSize];

            IntPtr ptr = Marshal.AllocHGlobal(this._structSize);
            GetStructPtr(ref item, (IntPtr)ptr);
            Marshal.Copy(ptr, buffer, 0, this._structSize);
            Marshal.FreeHGlobal(ptr);

            return buffer;
        }

        /// <summary>
        /// Deserializes the stuct item contained by the specified byte array.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Return the expected byte size of any item serialized by this class.
        /// </summary>
        /// <returns></returns>
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
