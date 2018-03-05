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
    public struct DigitalVariableSample
    {
        private DateTime _timeStamp;
        private int _quality;
        private bool _value;
        private int _handle;
        private int _namespaceID;

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
        public bool Value
        {
            get => _value;
            set => _value = value;
        }

    }
}