using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InContex.DataLoading.Prototyping01
{
    public class InsertCache
    {
        private SortedDictionary<long, InsertCacheBucket> _cache;
        private int _timeBucketSeconds;

        public InsertCache()
        {
            _cache = new SortedDictionary<long, InsertCacheBucket>();
            _timeBucketSeconds = 10;
        }

        public void Add(AnalogueSignal item)
        {
            long timeBucketID = (int)(item.SampleDateTimeUTC / TimeSpan.TicksPerSecond / _timeBucketSeconds);

            if(_cache.ContainsKey(timeBucketID))
            {
                _cache[timeBucketID].AddAnalogue(item);
            }
            else
            {
                InsertCacheBucket bucket = new InsertCacheBucket(timeBucketID, 20000);
                bucket.AddAnalogue(item);
                _cache.Add(timeBucketID, bucket);
            }
        }


        public InsertCacheBucket GetNextCacheBucket()
        {
            if(_cache.Count > 3)
            {
                KeyValuePair<long, InsertCacheBucket> entry = _cache.First();
                long key = entry.Key;
                InsertCacheBucket bucket = entry.Value;

                long currentTimeBucketID = (int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond / _timeBucketSeconds);

                _cache.Remove(key);
                return bucket;
            }

            return null;
        }
    }
}
