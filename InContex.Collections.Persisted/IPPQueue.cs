using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections;

using InContex.Collections.Persisted.Core;
using InContex.Runtime.Serialization;



namespace InContex.Collections.Persisted
{
    /// <summary>
    /// Inter Process Persited Queue.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class IPPQueue<T> : IEnumerable<T>, IEnumerable, ICollection, IReadOnlyCollection<T>, IDisposable where T : struct
    {
        private IPPBoundedQueue<int> _segementList;

        private IPPBoundedQueue<T> _headSegment;

        private IPPBoundedQueue<T> _tailSegment;

        private IPPBoundedQueue<T> _cachedSegment;

        private ISerializer<T> _serializer;


        /// <summary>
        /// The name of the queue.
        /// </summary>
        private string _name;

        /// <summary>
        /// The path where queue will be persited.
        /// </summary>
        private string _path;

        private int _segmentSize = 16384;

        private const int ControlBlockIndexVersion = 3;
        private const int ControlBlockIndexCount = 2;
        private const int ControlBlockIndexSegmentID = 1;

        private const int _MaximumSegmentCount = 1048576;


        [NonSerialized]
        private object _syncRoot;

        private QueueFullBehaviorEnum _fullBehavior;

        public IPPQueue(string name, string path, QueueFullBehaviorEnum fullBehavior, ISerializer<T> serializer, int segmentSize)
        {
            this._path = path;
            this._name = name;
            this._segmentSize = segmentSize;
            this._segementList = new IPPBoundedQueue<int>(name, path, _MaximumSegmentCount, QueueFullBehaviorEnum.ThrowException, new PrimitiveTypeSerializer<int>());
            this._fullBehavior = fullBehavior;
            this._serializer = serializer;
        }

        public IPPQueue(string name, string path, QueueFullBehaviorEnum fullBehavior, ISerializer<T> serializer)
            :this(name, path, fullBehavior, serializer, 16384)
        { }

        public IPPQueue(string name, string path, QueueFullBehaviorEnum fullBehavior)
            : this(name, path, fullBehavior, new GenericSerializer<T>(), 16384)
        { }

        #region Properties

        private long QueueItemCount
        {
            get => this._segementList.GetCustomControlBlockEntry(ControlBlockIndexCount);
            set => _segementList.SetCustomControlBlockEntry(ControlBlockIndexCount, value);
        }

        public long Version
        {
            get => this._segementList.GetCustomControlBlockEntry(ControlBlockIndexVersion);
        }

        public int Count
        {
            get => (int)this._segementList.GetCustomControlBlockEntry(ControlBlockIndexCount);
        }

        public QueueFullBehaviorEnum FullBehavior
        {
            get
            {
                return this._fullBehavior;
            }
            set
            {
                this._fullBehavior = value;
            }
        }

        bool ICollection.IsSynchronized
        {
            get
            {
                return false;
            }
        }

        object ICollection.SyncRoot
        {
            get
            {
                if (this._syncRoot == null)
                {
                    Interlocked.CompareExchange<object>(ref this._syncRoot, new object(), null);
                }
                return this._syncRoot;
            }
        }

        #endregion

        private void VersionIncrement()
        {
            long version = this.Version;

            version++;

            if(version > long.MaxValue - 10)
            {
                version = 0;
            }

            this._segementList.SetCustomControlBlockEntry(ControlBlockIndexVersion, version);
        }
        
        private void AcquireSpinLock()
        {
            this._segementList._array.AcquireSpinLock();
        }

        private void ReleaseSpinLock()
        {
            this._segementList._array.ReleaseSpinLock();
        }

        public void Clear()
        {
            AcquireSpinLock();

            VersionIncrement();

            try
            {
                foreach (int segmentID in _segementList)
                {
                    string segmentName = GenerateSegmentName(segmentID);

                    using (IPPBoundedQueue<T> segment = new IPPBoundedQueue<T>(segmentName, _path, _segmentSize, QueueFullBehaviorEnum.ThrowException, _serializer))
                    {
                        segment.Clear(false);
                    }
                }

                _segementList.Clear(false);
            }
            finally
            {
                ReleaseSpinLock();
            }

        }

        public bool Contains(T item)
        {
            AcquireSpinLock();

            bool found = false;

            try
            {
                int index = 0;
                int itemCount = this.Count;
                EqualityComparer<T> comparer = EqualityComparer<T>.Default;

                while (itemCount-- > 0)
                {
                    T compareItem = GetElementNoLock(index);
                    if (comparer.Equals(compareItem, item))
                    {
                        found = true;
                        break;
                    }
                    index++;
                }
            }
            finally
            {
                ReleaseSpinLock();
            }

            return found;
        }

        private T GetElementNoLock(int i)
        {
            T item;

            IPPBoundedQueue<T> headSegment = GetHeadSegmentNoLock();

            int segmentSize = _segmentSize;
            int head = (int)headSegment.QueueHead;
            int segmentID = (head + i) / segmentSize;

            IPPBoundedQueue<T> segment = GetSegmentByIDNoLock(segmentID);
            int segmentOffset = (head + i) % _segmentSize;

            item = segment.GetElement(segmentOffset);

            return item;
        }

        public T GetElement(int i)
        {
            T item;

            AcquireSpinLock();

            try
            {
                item = GetElementNoLock(i);
            }
            finally
            {
                ReleaseSpinLock();
            }

            return item;
        }
        

        private string GenerateSegmentName(int segmentID)
        {
            string segmentName = string.Format("{0}.{1}", _name, segmentID);

            return segmentName;
        }

        private IPPBoundedQueue<T> GetSegmentByIDNoLock(int segmentID)
        {
            if (_headSegment != null)
            {
                int headSegmentID = (int)_headSegment.GetCustomControlBlockEntry(ControlBlockIndexSegmentID);
                // Request segment is part of head, simply return head segment.
                if (segmentID == headSegmentID)
                {
                    return _headSegment;
                }
            }

            if (_tailSegment != null)
            {
                int tailSegmentID = (int)_tailSegment.GetCustomControlBlockEntry(ControlBlockIndexSegmentID);
                // Request segment is part of tail, simply return head segment.
                if (segmentID == tailSegmentID)
                {
                    return _tailSegment;
                }
            }

            if (_cachedSegment != null)
            {
                int cachedSegmentID = (int)_cachedSegment.GetCustomControlBlockEntry(ControlBlockIndexSegmentID);
                // Request segment is part of cached segment, simply return cached segment.
                if (segmentID == cachedSegmentID)
                {
                    return _cachedSegment;
                }
            }

            // Requested segment is not part of head, tail or cached segment. Load segment in cache.
            if(_cachedSegment != null)
            {
                _cachedSegment.Dispose();
                _cachedSegment = null;
            }
            string segmentName = GenerateSegmentName(segmentID);

            _cachedSegment = new IPPBoundedQueue<T>(segmentName, _path, _segmentSize, QueueFullBehaviorEnum.ThrowException, _serializer);

            return _cachedSegment;
        }

        private IPPBoundedQueue<T> GetTailSegmentNoLock()
        {
            int segmentCount = this._segementList.Count;
            int segmentID = -1;

            IPPBoundedQueue<T> tailSegment = null;

            if (segmentCount <= 0)
            {
                // no segments yet so let's start at zero
                segmentID = 0;
                this._segementList.EnqueueNoLock(segmentID);
            }
            else
            {
                segmentID = this._segementList.PeekTailNoLock();
            }

            if (_tailSegment != null)
            {
                int cachedTailSegmentID = (int)_tailSegment.GetCustomControlBlockEntry(ControlBlockIndexSegmentID);

                if(cachedTailSegmentID != segmentID)
                {
                    // Expected segment id does not match segment ID of tail. Release cached tail segment and create new segment.
                    segmentID++;
                    string segementName = GenerateSegmentName(segmentID);

                    _tailSegment.Dispose();
                    _tailSegment = null;
                    _tailSegment = new IPPBoundedQueue<T>(segementName, _path, _segmentSize, QueueFullBehaviorEnum.ThrowException, _serializer);
                    _tailSegment.SetCustomControlBlockEntry(ControlBlockIndexSegmentID, segmentID);
                }
            }
            else
            {
                // Tail is not yet cached simply create new tail.
                string segmentName = GenerateSegmentName(segmentID);

                _tailSegment = new IPPBoundedQueue<T>(segmentName, _path, _segmentSize, QueueFullBehaviorEnum.ThrowException, _serializer);
                _tailSegment.SetCustomControlBlockEntry(ControlBlockIndexSegmentID, segmentID);
            }

            tailSegment = _tailSegment;
            int count = tailSegment.Count;
            int capacity = tailSegment.Capacity;

            // The segment is full so add next one
            if(count >= capacity)
            {
                segmentID++;
                this._segementList.EnqueueNoLock(segmentID);

                string segmentName = GenerateSegmentName(segmentID);

                _tailSegment.Dispose();
                _tailSegment = null;
                _tailSegment = new IPPBoundedQueue<T>(segmentName, _path, _segmentSize, QueueFullBehaviorEnum.ThrowException, _serializer);
                _tailSegment.SetCustomControlBlockEntry(ControlBlockIndexSegmentID, segmentID);
                tailSegment = _tailSegment;
            }

            return tailSegment;
        }

        private IPPBoundedQueue<T> GetHeadSegmentNoLock()
        {
            int segmentItemCount = this._segementList.Count;
            int segmentID = -1;

            IPPBoundedQueue<T> headSegment = null;

            if (segmentItemCount <= 0)
            {
                // no segments yet so simply return null.
                return null;
            }
            else
            {
                segmentID = this._segementList.PeekNoLock();
            }

            if (_headSegment != null)
            {
                int cachedHeadSegmentID = (int)_headSegment.GetCustomControlBlockEntry(ControlBlockIndexSegmentID);

                if (cachedHeadSegmentID != segmentID)
                {
                    // Expected segemnt id does not match segment ID of head. Release cached head and load correct segment.
                    string segmentName = GenerateSegmentName(segmentID);

                    _headSegment.Dispose();
                    _headSegment = null;
                    _headSegment = new IPPBoundedQueue<T>(segmentName, _path, _segmentSize, QueueFullBehaviorEnum.ThrowException, _serializer);
                    _headSegment.SetCustomControlBlockEntry(ControlBlockIndexSegmentID, segmentID);
                }
            }
            else
            {
                // Head is not yet cached simply create new head.
                string segmentName = GenerateSegmentName(segmentID);

                _headSegment = new IPPBoundedQueue<T>(segmentName, _path, _segmentSize, QueueFullBehaviorEnum.ThrowException, _serializer);
                _headSegment.SetCustomControlBlockEntry(ControlBlockIndexSegmentID, segmentID);
            }

            headSegment = _headSegment;
            int count = headSegment.Count;;

            // The segment is empty so dequeue and dispose of the segment and reset
            if (count <= 0)
            {
                //segmentID++;
                this._segementList.DequeueNoLock();
                segmentItemCount = this._segementList.Count;

                if (segmentItemCount > 0)
                {
                    // Get the next segment
                    segmentID = this._segementList.PeekNoLock(); 

                    string segmentName = GenerateSegmentName(segmentID);

                    _headSegment.Clear(false);    // It is possible that the file backing this segment can be re-used, so clear the segment first.
                    _headSegment.Dispose();
                    _headSegment = null;
                    _headSegment = new IPPBoundedQueue<T>(segmentName, _path, _segmentSize, QueueFullBehaviorEnum.ThrowException, _serializer);
                    _headSegment.SetCustomControlBlockEntry(ControlBlockIndexSegmentID, segmentID);
                    headSegment = _headSegment;
                }
                else
                {
                    return null;
                }
            }

            return headSegment;
        }

        internal void EnqueueNoLock(T item)
        {
            IPPBoundedQueue<T> tailTailSegment = GetTailSegmentNoLock();
            tailTailSegment.EnqueueNoLock(item);
            QueueItemCount++;
            VersionIncrement();
        }

        public void Enqueue(T item)
        {
            AcquireSpinLock();

            try
            {
                EnqueueNoLock(item);
            }
            finally
            {
                ReleaseSpinLock();
            }
        }

        internal object DequeueNoLock()
        {
            IPPBoundedQueue<T> headSegment = GetHeadSegmentNoLock();

            if(headSegment == null)
            {
                return null;
            }

            if (headSegment.Count > 0)
            {
                T item = headSegment.DequeueNoLock();
                QueueItemCount--;
                VersionIncrement();
                return item;
            }
            else
            {
                return null;
            }

            
        }

        public T Dequeue()
        {
            T item;

            AcquireSpinLock();

            try
            {
                object itemObj = DequeueNoLock();

                if(itemObj == null)
                {
                    string message = "Invalid operation queue is empty.";
                    throw new InvalidOperationException(message);
                }

                item = (T)itemObj;
            }
            finally
            {
                ReleaseSpinLock();
            }

            return item;
        }

        public bool TryDequeue(ref object item)
        {
            bool success = false;

            AcquireSpinLock();

            try
            {
                item = DequeueNoLock();
                if (item != null)
                {
                    success = true;
                }
            }
            finally
            {
                ReleaseSpinLock();
            }

            return success;
        }

        public T[] DequeueAll()
        {
            int count = this.Count;
            int offset = 0;
            int itemSize = _serializer.SerializedByteSize();

            T[] finalArray = new T[count];

            AcquireSpinLock();

            try
            {

                foreach (int segmentID in _segementList)
                {
                    string segmentName = GenerateSegmentName(segmentID);

                    using (IPPBoundedQueue<T> segment = new IPPBoundedQueue<T>(segmentName, _path, _segmentSize, QueueFullBehaviorEnum.ThrowException, _serializer))
                    {
                        T[] segmentArray = segment.ToArrayNoLock();
                        int length = segmentArray.Length * itemSize;

                        Buffer.BlockCopy(segmentArray, 0, finalArray, offset, length);

                        offset += (segmentArray.Length * itemSize);

                        segment.Clear(false);
                    }
                }

                _segementList.Clear(false);
            }
            finally
            {
                ReleaseSpinLock();
            }

            return finalArray;
        }

        public T Peek()
        {
            AcquireSpinLock();

            T item;
            //long head = this.QueueHead;
            //long count = this.QueueCount;

            try
            {
                IPPBoundedQueue<T> headSegment = GetHeadSegmentNoLock();

                if (headSegment == null)
                {
                    string message = "Invalid operation queue is empty.";
                    throw new InvalidOperationException(message);
                }

                item = headSegment.PeekNoLock();
            }
            finally
            {
                ReleaseSpinLock();
            }

            return item;
        }

        public T PeekTail()
        {
            AcquireSpinLock();

            T item;

            try
            {
                IPPBoundedQueue<T> tailSegment = GetTailSegmentNoLock();

                if (tailSegment == null)
                {
                    string message = "Invalid operation queue is empty.";
                    throw new InvalidOperationException(message);
                }

                item = tailSegment.PeekTailNoLock();
            }
            finally
            {
                ReleaseSpinLock();
            }

            return item;
        }


        public Enumerator GetEnumerator()
        {
            return new Enumerator((IPPQueue<T>)this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator((IPPQueue<T>)this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator((IPPQueue<T>)this);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            this.ToArray().CopyTo(array, arrayIndex);
        }


        void ICollection.CopyTo(Array array, int index)
        {
            this.ToArray().CopyTo(array, index);
        }

        private T[] ToArrayNoLock()
        {
            int count = this.Count;
            int offset = 0;
            int itemSize = _serializer.SerializedByteSize();

            T[] finalArray = new T[count];

            foreach(int segmentID in _segementList)
            {
                string segmentName = GenerateSegmentName(segmentID);

                using (IPPBoundedQueue<T> segment = new IPPBoundedQueue<T>(segmentName, _path, _segmentSize, QueueFullBehaviorEnum.ThrowException, _serializer))
                {
                    T[] segmentArray = segment.ToArrayNoLock();
                    int length = segmentArray.Length * itemSize;

                    Buffer.BlockCopy(segmentArray, 0, finalArray, offset, length);

                    offset += (segmentArray.Length * itemSize);
                }
            }

            return finalArray;
        }

        public T[] ToArray()
        {
            T[] items;
            AcquireSpinLock();

            try
            {
                items = ToArrayNoLock();
            }
            finally
            {
                ReleaseSpinLock();
            }

            return items;
        }

        // NOTE: Removed once memory is allocated in MMF view changing the size won’t actually release the memory.


        #region Dispose

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_segementList != null)
                {
                    _segementList.Dispose();
                    _segementList = null;
                }

                if(_headSegment != null)
                {
                    _headSegment.Dispose();
                    _headSegment = null;
                }

                if (_tailSegment != null)
                {
                    _tailSegment.Dispose();
                    _tailSegment = null;
                }

            }
            // free native resources if there are any. 
        }

        ~IPPQueue()
        {
            // Finalizer calls Dispose(false)  
            Dispose(false);
        }

        #endregion


        // Nested Types
        public struct Enumerator : IEnumerator<T>, IDisposable, IEnumerator
        {
            private IPPQueue<T> _q;
            private int _index;
            private long _version;
            private T _currentElement;
            internal Enumerator(IPPQueue<T> q)
            {
                this._q = q;
                this._version = this._q.Version;
                this._index = -1;
                this._currentElement = default(T);
            }

            public void Dispose()
            {
                this._index = -2;
                this._currentElement = default(T);
            }

            public bool MoveNext()
            {
                if (this._version != this._q.Version)
                {
                    string message = "Invalid operation the underlying queue has been modified.";
                    throw new InvalidOperationException(message);
                }
                if (this._index == -2)
                {
                    return false;
                }
                this._index++;
                if (this._index == this._q.Count)
                {
                    this._index = -2;
                    this._currentElement = default(T);
                    return false;
                }
                this._currentElement = this._q.GetElement(this._index);
                return true;
            }

            public T Current
            {
                get
                {
                    if (this._index < 0)
                    {
                        if (this._index == -1)
                        {
                            string message = "Invalid operation the enumeration has not yet been initialized.";
                            throw new InvalidOperationException(message);
                        }
                        else
                        {
                            string message = "Invalid operation the enumeration is completed.";
                            throw new InvalidOperationException(message);
                        }
                    }
                    return this._currentElement;
                }
            }

            object IEnumerator.Current
            {

                get
                {
                    if (this._index < 0)
                    {
                        if (this._index == -1)
                        {
                            string message = "Invalid operation the enumeration has not yet been initialized.";
                            throw new InvalidOperationException(message);
                        }
                        else
                        {
                            string message = "Invalid operation the enumeration is completed.";
                            throw new InvalidOperationException(message);
                        }
                    }
                    return this._currentElement;
                }
            }
            void IEnumerator.Reset()
            {
                if (this._version != this._q.Version)
                {
                    string message = "Invalid operation the underlying queue has been modified.";
                    throw new InvalidOperationException(message);
                }
                this._index = -1;
                this._currentElement = default(T);
            }
        }
    }
}
