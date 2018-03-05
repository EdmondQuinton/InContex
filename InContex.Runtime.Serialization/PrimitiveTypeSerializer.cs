using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using InContex.Runtime.Serialization;

namespace InContex.Runtime.Serialization
{
    // TODO: Make serilizer only visable to InfoHub.Collections.Persisted
    public class PrimitiveTypeSerializer<T> : ISerializer<T> where T : struct
    {
        private int _intSize;
        private int _longSize;
        private int _guidSize;

        public PrimitiveTypeSerializer()
        {
            Type type = typeof(T);

            if (!TypeSupported(type))
            {
                string message = string.Format("The specified type '{0}' is not supported by this Serializer.", type.ToString());
                throw new NotSupportedException(message);
            }

            _intSize = Marshal.SizeOf(typeof(int));
            _longSize = Marshal.SizeOf(typeof(long));
            _guidSize = Marshal.SizeOf(typeof(Guid));
        }

        private bool TypeSupported(Type type)
        {
            if ((type == typeof(int)) || (type == typeof(long)) || (type == typeof(Guid)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private byte[] IntToBytes(int item)
        {
            unsafe
            {
                int size = _intSize;

                byte[] byteArray = new byte[size];

                fixed (byte* pByteArray = byteArray)
                {
                    *((int*)pByteArray) = item;
                }

                return byteArray;
            }
        }


        private byte[] LongToBytes(long item)
        {
            unsafe
            {
                int size = _longSize;

                byte[] byteArray = new byte[size];

                fixed (byte* pByteArray = byteArray)
                {
                    *((long*)pByteArray) = item;
                }

                return byteArray;
            }
        }


        private byte[] GuidToBytes(Guid item)
        {
            return item.ToByteArray();
        }


        private int BytesToInt(byte[] buffer)
        {
            int item;

            unsafe
            {
                fixed (byte* pBuffer = buffer)
                {
                    item = *((int*)pBuffer);
                }
            }

            return item;
        }

        private long BytesToLong(byte[] buffer)
        {
            long item;

            unsafe
            {
                fixed (byte* pBuffer = buffer)
                {
                    item = *((long*)pBuffer);
                }
            }

            return item;
        }

        private Guid BytesToGuid(byte[] buffer)
        {
            return new Guid(buffer);
        }


        private byte[] Serialize(object value, Type type)
        {
            if (type == typeof(int))
            {
                return this.IntToBytes((int)value);
            }

            if (type == typeof(long))
            {
                return this.LongToBytes((long)value);
            }

            if (type == typeof(Guid))
            {
                return this.GuidToBytes((Guid)value);
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
            if (type == typeof(int))
            {
                return BytesToInt(buffer);
            }

            if (type == typeof(long))
            {
                return BytesToLong(buffer);
            }

            if (type == typeof(Guid))
            {
                return BytesToGuid(buffer);
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
            if (type == typeof(int))
            {
                return _intSize;
            }

            if (type == typeof(long))
            {
                return _longSize;
            }

            if (type == typeof(Guid))
            {
                return _guidSize;
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
