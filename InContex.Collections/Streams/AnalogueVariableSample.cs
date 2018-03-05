using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InContex.Data.Streams
{
    /// <summary>
    /// 
    /// </summary>
    public struct AnalogueVariableSample
    {
        private DateTime _timestamp;
        private DateTime __timestampPrevious; // Timestamp for the previous samples value for this variable.
        private int _quality;
        private double _value;
        private double _valueDelta; // Delta value of previous samples value for this variable.
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
            get => _timestamp;
            set => _timestamp = value;
        }
        public int Quality
        {
            get => _quality;
            set => _quality = value;
        }
        public double Value
        {
            get => _value;
            set => _value = value;
        }

    }
}