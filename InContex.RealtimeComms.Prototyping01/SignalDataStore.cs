using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InContex.Collections.Persisted;
using System.IO;

namespace InContex.RealtimeComms.Prototyping01
{
    public class SignalStore
    {
        private IPPQueue<AnalogueSignal> _analogueSignalStore;
        private Dictionary<int, AnalogueSignal> _lastValueCache;

        public SignalStore()
            :this("SignalStore")
        { }

        public SignalStore(string storeName)
            :this(storeName, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"InContex\Data")) { }

        public SignalStore(string storeName, string storeLocation)
        {
            string analogueSignalStoreName = storeName + "_Analogue";

            _analogueSignalStore = new IPPQueue<AnalogueSignal>(storeName, storeLocation, new SignalSerializer<AnalogueSignal>());
            _lastValueCache = new Dictionary<int, AnalogueSignal>();
        }

        public void WriteAnalogue(AnalogueSignal signal)
        {
            int signalID = signal.SignalID;

            if (_lastValueCache.ContainsKey(signalID))
            {
                AnalogueSignal lastSignal = _lastValueCache[signalID];
                signal.PreviousSampleDateTimeUTC = lastSignal.SampleDateTimeUTC;
                signal.DeltaValue = signal.Value - lastSignal.Value;
                _lastValueCache[signalID] = signal;
            }
            else
            {
                _lastValueCache.Add(signalID, signal);
            }
            _analogueSignalStore.Enqueue(signal);
        }
    }
}
