using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Runtime.InteropServices;

namespace InContex.Data.Streams
{
    /// <summary>
    /// 
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 1)]
    public unsafe struct StringVariableSample
    {
        private DateTime _timeStamp;
        private int _quality;
        public fixed char _value[20];
        private int _handle;
        private int _namespaceID;

        public StringVariableSample(string value)
        {
            _timeStamp = DateTime.MinValue;
            _quality = 0;
            _handle = -1;
            _namespaceID = -1;

            unsafe
            {
                fixed (char* pSampleValue = _value)
                {
                    fixed (char* pValue = value)
                    {
                        int length = value.Length;
                        if (length > 20) length = 20;

                        for (int i = 0; i < length; i++)
                        {
                            *(pSampleValue + i) = *(pValue + i);
                        }
                    }
                }
            }
        }

        public int NamespaceID
        {
            get => _namespaceID;
            set => _namespaceID = value;
        }

        public int Handle
        {
            get => _handle;
            set => _handle = value;
        }

        public DateTime Timestamp
        {
            get => _timeStamp;
            set => _timeStamp = value;
        }
        public int Quality
        {
            get => _quality;
            set => _quality = value;
        }

        public string GetValue()
        {
            unsafe
            {
                fixed (char* pValue = this._value)
                {
                    return new string(pValue);
                }
            }
        }

        public void SetValue(string value)
        {
            if (value == null)
            {
                return;
            }

            unsafe
            {
                fixed (char* pSampleValue = this._value)
                {
                    fixed (char* pValue = value)
                    {
                        int length = value.Length;
                        if (length > 20) length = 20;

                        for (int i = 0; i < length; i++)
                        {
                            *(pSampleValue + i) = *(pValue + i);
                        }
                    }
                }
            }
        }
    }
}