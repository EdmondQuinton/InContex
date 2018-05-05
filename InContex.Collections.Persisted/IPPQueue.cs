using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections;

using InContex.Collections.Persisted.Core;
using InContex.Runtime.Serialization;



namespace InContex.Collections.Persisted
{
    /// <summary>
    /// Inter process persisted queue. Represents a first-in, first-out collection of objects.
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the queue.</typeparam>
    public sealed class IPPQueue<T> : IEnumerable<T>, IEnumerable, ICollection, IReadOnlyCollection<T>, IDisposable where T : struct
    {
        private static T[] _emptyBuffer = new T[0];
        /// <summary>
        /// 
        /// </summary>
        private IPPBoundedQueue<int> _segementList;
        /// <summary>
        /// Bounded queue segment that contains the head element.
        /// </summary>
        private IPPBoundedQueue<T> _headSegment;
        /// <summary>
        /// Bounded queue segment that contains the tail element.
        /// </summary>
        private IPPBoundedQueue<T> _tailSegment;
        /// <summary>
        /// Bounded queue used to cache a queue segment that is not part of head or tail. This cache is use to 
        /// for operation that needs to access random queue entries not part of head or tail segments.
        /// </summary>
        private IPPBoundedQueue<T> _cachedSegment;
        /// <summary>
        /// User defined object serializer that implements the ISerializer<T> interface.
        /// </summary>
        private ISerializer<T> _serializer;


        /// <summary>
        /// The name of the queue.
        /// </summary>
        private string _name;

        /// <summary>
        /// The path where queue will be persited.
        /// </summary>
        private string _path;
        /// <summary>
        /// The number of queue entries that can be stored per segment.
        /// </summary>
        private int _segmentSize = 16384;

        private const int ControlBlockIndexVersion = 3;
        private const int ControlBlockIndexCount = 2;
        private const int ControlBlockIndexSegmentID = 1;

        /// <summary>
        /// The maximum number of segments that can be created for a queue.
        /// </summary>
        private const int _MaximumSegmentCount = 1048576;


        [NonSerialized]
        private object _syncRoot;

        /// <summary>
        /// IPPQueue constructor
        /// </summary>
        /// <param name="name">The name of the queue, required to access queue across process boundaries.</param>
        /// <param name="path">The location / path where the queue will be persited to.</param>
        /// <param name="serializer">User defined object serializer that implements the ISerializer<T> interface.</param>
        /// <param name="segmentSize">The number of entries that can be stored in a segment.</param>
        public IPPQueue(string name, string path, ISerializer<T> serializer, int segmentSize)
        {
            this._path = path;
            this._name = name;
            this._segmentSize = segmentSize;
            this._segementList = new IPPBoundedQueue<int>(name, path, _MaximumSegmentCount, QueueFullBehaviorEnum.ThrowException, new PrimitiveTypeSerializer<int>());
            this._serializer = serializer;
        }

        /// <summary>
        /// IPPQueue constructor
        /// </summary>
        /// <param name="name">The name of the queue, required to access queue across process boundaries.</param>
        /// <param name="path">The location / path where the queue will be persited to.</param>
        /// <param name="serializer">User defined object serializer that implements the ISerializer<T> interface.</param>
        public IPPQueue(string name, string path, ISerializer<T> serializer)
            :this(name, path, serializer, 16384)
        { }

        /// <summary>
        /// IPPQueue constructor
        /// </summary>
        /// <param name="name">The name of the queue, required to access queue across process boundaries.</param>
        /// <param name="path">The location / path where the queue will be persited to.</param>
        public IPPQueue(string name, string path)
            : this(name, path, new GenericSerializer<T>(), 16384)
        { }

        #region Properties

        /// <summary>
        /// Get / set the queue element count.
        /// </summary>
        private long QueueItemCount
        {
            get => this._segementList.GetCustomControlBlockEntry(ControlBlockIndexCount);
            set => _segementList.SetCustomControlBlockEntry(ControlBlockIndexCount, value);
        }

        /// <summary>
        /// Get the queue version number. Each time an entry is enqueued or dequeued the version number will increase.
        /// </summary>
        /// <remarks>
        /// Each time a change is made to the queue the version number associated with the queue will increase. 
        /// This version number is used to help detect changes to the queue.
        /// </remarks>
        public long Version
        {
            get => this._segementList.GetCustomControlBlockEntry(ControlBlockIndexVersion);
        }

        /// <summary>
        /// Get the element count in the queue.
        /// </summary>
        public int Count
        {
            get => (int)this._segementList.GetCustomControlBlockEntry(ControlBlockIndexCount);
        }


        /// <summary>
        /// Property returns whether the queue is a synchronized collection.
        /// </summary>
        /// <remarks>
        /// The IsSynchronized property will always return false. Even though the Enqueue and Dequeue methods are 
        /// thread safe, the rest of the queue’s properties and methods are not.
        /// </remarks>
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


        /// <summary>
        /// Empty out the queue.
        /// </summary>
        /// <param name="zeroBuffer">Parameter indicates whether or not internal circular buffer will be zeroed or not.</param>
        public void Clear(bool zeroBuffer = true)
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
                        segment.Clear(zeroBuffer);
                    }
                }

                _segementList.Clear(zeroBuffer);
            }
            finally
            {
                ReleaseSpinLock();
            }

        }

        /// <summary>
        /// Determines whether the queue contains a specified element by using the default equality comparer.
        /// </summary>
        /// <param name="item">The value to locate in the queue.</param>
        /// <returns>true if the queue contains the element that has the specified value; otherwise, false</returns>
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

        /// <summary>
        /// Get element at specific index. The index is relative to the position of the queue’s 
        /// head element, where an index of 0 will return the head element.
        /// </summary>
        /// <param name="i"></param>
        /// <returns>The element located at index</returns>
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

        /// <summary>
        /// Adds an object to the end of the queue without any synchronization locks.
        /// </summary>
        /// <param name="item">The element to enqueue.</param>
        internal void EnqueueNoLock(T item)
        {
            IPPBoundedQueue<T> tailTailSegment = GetTailSegmentNoLock();
            tailTailSegment.EnqueueNoLock(item);
            QueueItemCount++;
            VersionIncrement();
        }

        /// <summary>
        /// Adds an object to the end of the queue.
        /// </summary>
        /// <param name="item">The element to enqueue.</param>
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

        /// <summary>
        /// Removes and returns the object at the beginning of the queue without any synchronization locks.
        /// </summary>
        /// <returns>The object that is removed from the beginning of the queue.</returns>
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

        /// <summary>
        /// Removes and returns the object at the beginning of the queue.
        /// </summary>
        /// <returns>The object that is removed from the beginning of the queue.</returns>
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

        /// <summary>
        /// Method tries to remove and return the element at the beginning of the queue via reference parameter.
        /// </summary>
        /// <param name="item">The queue item to return.</param>
        /// <returns>'true' if element was successfully removed, 'false' if queue was empty.</returns>
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

        /// <summary>
        /// Remove all items from queue and return removed items as array.
        /// </summary>
        /// <returns>Array containing dequeued elements.</returns>
        /// <remarks>
        /// It is not recommended to use this method without verifying the size of the queue first. This is 
        /// due to the potential for the queue to grow extremely large on disk.
        /// </remarks>
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

        public T[] DequeueSegment()
        {
            AcquireSpinLock();

            try
            {
                IPPBoundedQueue<T> headSegment = GetHeadSegmentNoLock();

                if (headSegment == null)
                {
                    return _emptyBuffer;
                }

                int count = headSegment.Count;

                if (count > 0)
                {
                    T[] items = headSegment.DequeueAllNoLock();
                    QueueItemCount -= count;
                    VersionIncrement();
                    return items;
                }
            }
            finally
            {
                ReleaseSpinLock();
            }

            return _emptyBuffer;
        }

        /// <summary>
        /// Returns the element at the beginning of the queue without removing it. 
        /// </summary>
        /// <returns>The element at the beginning of the queue.</returns>
        public T Peek()
        {
            AcquireSpinLock();

            T item;

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

        /// <summary>
        /// Returns the element at the end of the queue without removing it. 
        /// </summary>
        /// <returns>The element at the end of the queue.</returns>
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

        /// <summary>
        /// Copies the queue elements to an existing one-dimensional Array, starting at the specified array index.
        /// </summary>
        /// <param name="array">The zero-based one-dimensional Array that is the destination of the elements copied from the queue.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            this.ToArray().CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Copies the queue elements to an existing one-dimensional Array, starting at the specified array index.
        /// </summary>
        /// <param name="array">The zero-based one-dimensional Array that is the destination of the elements copied from the queue.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        void ICollection.CopyTo(Array array, int index)
        {
            this.ToArray().CopyTo(array, index);
        }

        /// <summary>
        /// Return queue elements as an array without any synchronization locks.
        /// </summary>
        /// <returns>Array containing queue elements.</returns>
        private T[] ToArrayNoLock()
        {
            int count = this.Count;
            int offset = 0;
            int itemSize = _serializer.SerializedByteSize();

            T[] finalArray = new T[count];

            foreach (int segmentID in _segementList)
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

        /// <summary>
        /// Return queue elements as an array.
        /// </summary>
        /// <returns>Array containing queue elements.</returns>
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

        /// <summary>
        /// Returns an enumerator that iterates through the IPPBoundedQueue.
        /// </summary>
        /// <returns>A IPPQueue<T>.Enumerator structure for the IPPBoundedQueue.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator((IPPQueue<T>)this);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the IPPQueue.
        /// </summary>
        /// <returns>A IPPQueue<T>.Enumerator structure for the IPPQueue.</returns>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator((IPPQueue<T>)this);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the IPPQueue.
        /// </summary>
        /// <returns>A IPPQueue<T>.Enumerator structure for the IPPQueue.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator((IPPQueue<T>)this);
        }




        #region Dispose

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
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

        #region Enumerator Implementation

        /// <summary>
        /// Supports a simple iteration over a queue.
        /// </summary>
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

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose()
            {
                this._index = -2;
                this._currentElement = default(T);
            }

            /// <summary>
            /// Advances the enumerator to the next element of the queue.
            /// </summary>
            /// <returns>true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the queue.</returns>
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

            /// <summary>
            /// Gets the element in the queue at the current position of the enumerator.
            /// </summary>
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

            /// <summary>
            /// Gets the element in the queue at the current position of the enumerator.
            /// </summary>
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

            /// <summary>
            /// Sets the enumerator to its initial position, which is before the first element in the queue.
            /// </summary>
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

        #endregion
    }
}
