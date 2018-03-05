using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using InContex.Data.Streams;
using InContex.Runtime.Serialization;

namespace InContex.Data.Serialization
{
    public class VariableSampleSerializer<T> : ISerializer<T> where T : struct
    {
        private int _analogueSampleSize;
        private int _digitalSampleSize;
        private int _stringeSampleSize;

        public VariableSampleSerializer()
        {
            Type type = typeof(T);

            if(!TypeSupported(type))
            {
                string message = string.Format("The specified type '{0}' is not supported by this Serializer.", type.ToString());
                throw new NotSupportedException(message);
            }

            _analogueSampleSize = Marshal.SizeOf(typeof(AnalogueVariableSample));
            _digitalSampleSize = Marshal.SizeOf(typeof(DigitalVariableSample));
            _stringeSampleSize = Marshal.SizeOf(typeof(StringVariableSample));
        }

        private bool TypeSupported(Type type)
        {
            if ((type == typeof(AnalogueVariableSample)) || (type == typeof(DigitalVariableSample)) || (type == typeof(StringVariableSample)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private byte[] AnalogueSampleToBytes(AnalogueVariableSample item)
        {
            unsafe
            {
                int size = _analogueSampleSize;

                byte[] byteArray = new byte[size];

                fixed (byte* pByteArray = byteArray)
                {
                    *((AnalogueVariableSample*)pByteArray) = item;
                }

                return byteArray;
            }
        }

        private void AnalogueSampleToPtr(AnalogueVariableSample item, IntPtr bufferPtr)
        {
            unsafe
            {
                byte* pByteArray = (byte*)bufferPtr;
                *((AnalogueVariableSample*)pByteArray) = item;

            }
        }

        private byte[] DigitalSampleToBytes(DigitalVariableSample item)
        {
            unsafe
            {
                int size = _digitalSampleSize;

                byte[] byteArray = new byte[size];

                fixed (byte* pByteArray = byteArray)
                {
                    *((DigitalVariableSample*)pByteArray) = item;
                }

                return byteArray;
            }
        }

        private void DigitalSampleToPtr(DigitalVariableSample item, IntPtr bufferPtr)
        {
            unsafe
            {
                byte* pByteArray = (byte*)bufferPtr;
                *((DigitalVariableSample*)pByteArray) = item;
            }
        }

        private byte[] StringSampleToBytes(StringVariableSample item)
        {
            unsafe
            {
                int size = _stringeSampleSize;

                byte[] byteArray = new byte[size];

                fixed (byte* pByteArray = byteArray)
                {
                    *((StringVariableSample*)pByteArray) = item;
                }

                return byteArray;
            }
        }

        private void StringSampleToPtr(StringVariableSample item, IntPtr bufferPtr)
        {
            unsafe
            {
                byte* pByteArray = (byte *)bufferPtr;
                *((StringVariableSample*)pByteArray) = item;
            }
        }

        private AnalogueVariableSample BytesToAnalogueSample(byte[] buffer)
        {
            AnalogueVariableSample item;

            unsafe
            {
                fixed (byte* pBuffer = buffer)
                {
                    item = *((AnalogueVariableSample*)pBuffer);
                }
            }

            return item;
        }

        private DigitalVariableSample BytesToDigitalSample(byte[] buffer)
        {
            DigitalVariableSample item;

            unsafe
            {
                fixed (byte* pBuffer = buffer)
                {
                    item = *((DigitalVariableSample*)pBuffer);
                }
            }

            return item;
        }

        private StringVariableSample BytesToStringSample(byte[] buffer)
        {
            StringVariableSample item;

            unsafe
            {
                fixed (byte* pBuffer = buffer)
                {
                    item = *((StringVariableSample*)pBuffer);
                }
            }

            return item;
        }

        private AnalogueVariableSample BytesToAnalogueSample(IntPtr bufferPtr)
        {
            AnalogueVariableSample item;

            unsafe
            {
                byte* pBuffer = (byte*)bufferPtr;
                item = *((AnalogueVariableSample*)pBuffer);
            }

            return item;
        }

        private DigitalVariableSample BytesToDigitalSample(IntPtr bufferPtr)
        {
            DigitalVariableSample item;

            unsafe
            {
                byte* pBuffer = (byte*)bufferPtr;
                item = *((DigitalVariableSample*)pBuffer);
            }

            return item;
        }

        private StringVariableSample BytesToStringSample(IntPtr bufferPtr)
        {
            StringVariableSample item;

            unsafe
            {
                byte* pBuffer = (byte*)bufferPtr;
                item = *((StringVariableSample*)pBuffer);
            }

            return item;
        }


        private byte[] Serialize(object value, Type type)
        {
            if(type == typeof(AnalogueVariableSample))
            {
                return this.AnalogueSampleToBytes((AnalogueVariableSample)value);
            }

            if (type == typeof(DigitalVariableSample))
            {
                return this.DigitalSampleToBytes((DigitalVariableSample)value);
            }

            if (type == typeof(StringVariableSample))
            {
                return this.StringSampleToBytes((StringVariableSample)value);
            }

            throw new NotSupportedException("The specified type is not supported.");
        }

        private void SerializePtr(object value, Type type, IntPtr bufferPtr)
        {
            if (type == typeof(AnalogueVariableSample))
            {
                AnalogueSampleToPtr((AnalogueVariableSample)value, bufferPtr);
                return;
            }

            if (type == typeof(DigitalVariableSample))
            {
                DigitalSampleToPtr((DigitalVariableSample)value, bufferPtr);
                return;
            }

            if (type == typeof(StringVariableSample))
            {
                StringSampleToPtr((StringVariableSample)value, bufferPtr);
                return;
            }

            throw new NotSupportedException("The specified type is not supported.");
        }

        public byte[] Serialize(T value)
        {
            Type type = typeof(T);

            return Serialize(value, type);
        }

        /// <summary>
        ///  Copy content of struct to buffer referenced by pointer.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="bufferPtr"></param>
        public void Serialize(T value, IntPtr bufferPtr)
        {
            Type type = typeof(T);

            SerializePtr(value, type, bufferPtr);
        }

        private object DeSerialize(byte[] buffer, Type type)
        {
            if (type == typeof(AnalogueVariableSample))
            {
                return BytesToAnalogueSample(buffer);
            }

            if (type == typeof(DigitalVariableSample))
            {
                return BytesToDigitalSample(buffer);
            }

            if (type == typeof(StringVariableSample))
            {
                return BytesToStringSample(buffer);
            }

            throw new NotSupportedException("The specified type is not supported.");
        }

        private object DeSerialize(IntPtr bufferPtr, Type type)
        {
            if (type == typeof(AnalogueVariableSample))
            {
                return BytesToAnalogueSample(bufferPtr);
            }

            if (type == typeof(DigitalVariableSample))
            {
                return BytesToDigitalSample(bufferPtr);
            }

            if (type == typeof(StringVariableSample))
            {
                return BytesToStringSample(bufferPtr);
            }

            throw new NotSupportedException("The specified type is not supported.");
        }

        public T DeSerialize(byte[] buffer) 
        {
            Type type = typeof(T);

            return (T)DeSerialize(buffer, type);
        }

        public T DeSerialize(IntPtr bufferPtr)
        {
            Type type = typeof(T);

            return (T)DeSerialize(bufferPtr, type);
        }

        private int SerializedByteSize(Type type)
        {
            if (type == typeof(AnalogueVariableSample))
            {
                return _analogueSampleSize;
            }

            if (type == typeof(DigitalVariableSample))
            {
                return _digitalSampleSize;
            }

            if (type == typeof(StringVariableSample))
            {
                return _stringeSampleSize;
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
