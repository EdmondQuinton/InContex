using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace InContex.DataLoading.Prototyping01
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AnalogueSignal
    {
        public int SignalID;

        public long SampleDateTimeUTC;

        public long PreviousSampleDateTimeUTC;

        public double Value;

        public double DeltaValue; // Current minus previous value.

        public int StatusGood;

        public int StatusCode;

    }
}
