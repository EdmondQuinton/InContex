using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using InContex.Runtime.Serialization;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("InContex.Collections.Persisted, PublicKey=0024000004800000940000000602000000240000525341310004000001000100ed8c2deb489a2d" +
                              "6f8dfc5904a518da3141c3563361b1cf0fb1759e260cdc80a77c1cf73c1d11c124b9574587128a" +
                              "660979908ee27ef61ae18b90a9aa27ffdbfcf35e5baf8aefa882c7b24094de7f32b31550582552" +
                              "a2025b0bf3dbbdd75b198a87b17c470ae1d653bb628740a57963547e546f4c5a33e4a123b0e988" +
                              "4139c2bd")]
namespace InContex.DataLoading.Prototyping01
{
    /// <summary>
    /// Class provides high speed serialization of AnalogueSignal struct intended for use with the IPPQueue. 
    /// This class does not support all generic struct types and as such is not visible to external 
    /// libraries or code.
    /// </summary>
    /// <typeparam name="T">AnalogueSignal type to serialize.</typeparam>
    internal class SignalSerializer<T> : ISerializer<T> where T : struct
    {
        private int _size;
        private int _longSize;
        private int _guidSize;

        internal SignalSerializer()
        {
            Type type = typeof(T);

            if (!TypeSupported(type))
            {
                string message = string.Format("The specified type '{0}' is not supported by this Serializer.", type.ToString());
                throw new NotSupportedException(message);
            }

            _size = Marshal.SizeOf(typeof(AnalogueSignal));
        }

        private bool TypeSupported(Type type)
        {
            if (type == typeof(AnalogueSignal))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private byte[] ToBytes(AnalogueSignal item)
        {
            unsafe
            {
                int size = _size;

                byte[] byteArray = new byte[size];

                fixed (byte* pByteArray = byteArray)
                {
                    *((AnalogueSignal*)pByteArray) = item;
                }

                return byteArray;
            }
        }

        private AnalogueSignal BytesToAnalogueSignal(byte[] buffer)
        {
            AnalogueSignal item;

            unsafe
            {
                fixed (byte* pBuffer = buffer)
                {
                    item = *((AnalogueSignal*)pBuffer);
                }
            }

            return item;
        }


        private byte[] Serialize(object value, Type type)
        {
            if (type == typeof(AnalogueSignal))
            {
                return this.ToBytes((AnalogueSignal)value);
            }

            throw new NotSupportedException("The specified type is not supported.");
        }


        public byte[] Serialize(T value)
        {
            Type type = typeof(T);

            return Serialize(value, type);
        }


        private object DeSerialize(byte[] buffer, Type type)
        {
            if (type == typeof(AnalogueSignal))
            {
                return BytesToAnalogueSignal(buffer);
            }

            throw new NotSupportedException("The specified type is not supported.");
        }


        public T DeSerialize(byte[] buffer)
        {
            Type type = typeof(T);

            return (T)DeSerialize(buffer, type);
        }


        private int SerializedByteSize(Type type)
        {
            if (type == typeof(AnalogueSignal))
            {
                return _size;
            }

            throw new NotSupportedException("The specified type is not supported.");
        }

        public int SerializedByteSize()
        {
            Type type = typeof(T);

            return SerializedByteSize(type);
        }
    }
}
