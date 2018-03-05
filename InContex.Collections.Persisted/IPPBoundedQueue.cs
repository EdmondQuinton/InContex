using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections;

using InContex.Collections.Persisted.Core;
using InContex.Runtime.Serialization;

namespace InContex.Collections.Persisted
{
    /// <summary>
    /// Represents a fixed length first-in, first-out collection of objects.
    /// </summary>
    /// <remarks>
    /// This class implements a generic fixed length inter process persited queue (IPPBoundedQueue) 
    /// as a circular array. The queue utilizes the IPPArray as the circular array,  this ensures 
    /// that the queue is persited and accessible across process boundaries on the same machine.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class IPPBoundedQueue<T> : IEnumerable<T>, IEnumerable, ICollection, IReadOnlyCollection<T>, IDisposable where T: struct
    {
        #region Field Members
        /// <summary>
        /// Circular array buffer used to store queue entries.
        /// </summary>
        internal IPPArray<T> _array;
        /// <summary>
        /// Index of the control header block entry in the IPPArray header control block used to store the current position of the queue’s head.
        /// </summary>
        private const int ControlBlockIndexHead = 0;
        /// <summary>
        /// Index of the control header block entry in the IPPArray header control block used to store the current position of the queue’s tail.
        /// </summary>
        private const int ControlBlockIndexTail = 1;
        /// <summary>
        /// Index of the control header block entry in the IPPArray header control block used to store the current item count of the queue.
        /// </summary>
        private const int ControlBlockIndexCount = 2;
        /// <summary>
        /// Since the first control block entries of the array is used by the queue, we add an offset that will allow 3rd party applications to utilize the remaining control block entries. 
        /// </summary>
        private const int ControlBlockCustomEntriesOffset = 3; // If user wish to define custom control block entries then entries starting from 3 to 5 can be used.


        [NonSerialized]
        private object _syncRoot;

        /// <summary>
        /// Enumerator defines the behavior of the queue when it is full.
        /// </summary>
        private QueueFullBehaviorEnum _fullBehavior;

        #endregion

        #region Constructors
        /// <summary>
        /// IPPBoundedQueue constructor
        /// </summary>
        /// <param name="name">The name of the queue, required to access queue across process boundaries.</param>
        /// <param name="path">The location / path where the queue will be persited to.</param>
        /// <param name="capacity">The maximum number of items that can be stored within the queue.</param>
        /// <param name="fullBehavior">
        /// The behavior of the queue once the queue is full. The queue will either throw an exception ‘QueueFullBehaviorEnum.ThrowException’ 
        /// or queue can overwrite old entries ‘QueueFullBehaviorEnum.OverwriteOldEntries’
        /// </param>
        /// <param name="serializer">User defined object serializer that implements the ISerializer<T> interface.</param>
        public IPPBoundedQueue(string name, string path, int capacity, QueueFullBehaviorEnum fullBehavior, ISerializer<T> serializer)
        {
            if(capacity <= 0)
            {
                string message = string.Format("{0} is an invalid capacity. Queue capacity can only be set to positive non-zero values.", capacity);
                throw new ArgumentOutOfRangeException("capacity", message);
            }

            if(fullBehavior == QueueFullBehaviorEnum.AutoGrow)
            {
                string message = "Bounded queues are fixed in size and cannot be dynamically grown.";
                throw new ApplicationException(message);
            }

            _array = IPPArray<T>.Open(path, name, capacity, serializer);
            _fullBehavior = fullBehavior;
        }

        /// <summary>
        /// IPPBoundedQueue constructor
        /// </summary>
        /// <param name="name">The name of the queue, required to access queue across process boundaries.</param>
        /// <param name="path">The location / path where the queue will be persited to.</param>
        /// <param name="capacity">The maximum number of items that can be stored within the queue.</param>
        /// <param name="fullBehavior">
        /// The behavior of the queue once the queue is full. The queue will either throw an exception ‘QueueFullBehaviorEnum.ThrowException’ 
        /// or queue can overwrite old entries ‘QueueFullBehaviorEnum.OverwriteOldEntries’
        /// </param>
        public IPPBoundedQueue(string name, string path, int capacity, QueueFullBehaviorEnum fullBehavior)
            :this(name, path, capacity, fullBehavior, new GenericSerializer<T>())
        { }

        /// <summary>
        /// IPPBoundedQueue constructor
        /// </summary>
        /// <param name="name">The name of the queue, required to access queue across process boundaries.</param>
        /// <param name="path">The location / path where the queue will be persited to.</param>
        /// <param name="capacity">The maximum number of items that can be stored within the queue.</param>
        public IPPBoundedQueue(string name, string path, int capacity)
            :this(name, path, capacity, QueueFullBehaviorEnum.ThrowException, new GenericSerializer<T>())
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Internal property gets or sets the head position of queue in the circular array buffer.
        /// </summary>
        internal long QueueHead
        {
            get => _array.GetControlBlockEntry(ControlBlockIndexHead);
            set => _array.SetControlBlockEntry(ControlBlockIndexHead, value);
        }

        /// <summary>
        /// Private property gets or sets the tail position of queue in the circular array buffer.
        /// </summary>
        private long QueueTail
        {
            get => _array.GetControlBlockEntry(ControlBlockIndexTail);
            set => _array.SetControlBlockEntry(ControlBlockIndexTail, value);
        }

        /// <summary>
        /// Private property gets or sets the queue element count.
        /// </summary>
        private long QueueCount
        {
            get => _array.GetControlBlockEntry(ControlBlockIndexCount);
            set => _array.SetControlBlockEntry(ControlBlockIndexCount, value);
        }

        /// <summary>
        /// Property gets the queue element count.
        /// </summary>
        public int Count
        {
            get => (int)_array.GetControlBlockEntry(ControlBlockIndexCount);
        }

        /// <summary>
        /// Property gets the queue capacity.
        /// </summary>
        public int Capacity
        {
            get => (int)_array.Length;
        }

        /// <summary>
        /// Property gets the queue’s initialization count.
        /// </summary>
        public long InitializationCount
        {
            get => _array.InitializationCount;
        }

        /// <summary>
        /// Property gets the behavior of the queue when the queue is full.
        /// </summary>
        public QueueFullBehaviorEnum FullBehavior
        {
            get
            {
                return _fullBehavior;
            }
            set
            {
                _fullBehavior = value;
            }
        }

        /// <summary>
        /// Property returns whether the queue is a synchronized collection.
        /// </summary>
        /// <remarks>
        /// The IsSynchronized property will always return false. Even though the Enqueue and Dequeue methods are 
        /// thread safe, the rest queue’s properties and methods are not.
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
                if (_syncRoot == null)
                {
                    Interlocked.CompareExchange<object>(ref _syncRoot, new object(), null);
                }
                return _syncRoot;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Read custom control information from the array header control block.
        /// </summary>
        /// <param name="index">Index of the control block entry to read.</param>
        /// <returns>Returns the value of the control block entry.</returns>
        internal long GetCustomControlBlockEntry(int index)
        {
            int indexCustom = index + ControlBlockCustomEntriesOffset;
            return _array.GetControlBlockEntry(indexCustom);
        }

        /// <summary>
        /// Write custom control information to the array header control block.
        /// </summary>
        /// <param name="index">Index of the control block entry to write to.</param>
        /// <param name="value">The value to write to the control block.</param>
        internal void SetCustomControlBlockEntry(int index, long value)
        {
            int indexCustom = index + ControlBlockCustomEntriesOffset;
            _array.SetControlBlockEntry(indexCustom, value);
        }

        /// <summary>
        /// Empty out the queue.
        /// </summary>
        /// <param name="zeroBuffer">Parameter indicates whether or not internal circular buffer will be zeroed or not.</param>
        public void Clear(bool zeroBuffer = true)
        {
            _array.AcquireSpinLock();

            try
            {
                if (zeroBuffer)
                {
                    long head = QueueHead;
                    long tail = QueueTail;
                    long count = QueueCount;

                    if (head < tail)
                    {
                        _array.Clear(head, count);
                    }
                    else
                    {
                        _array.Clear(head, _array.Length - head);
                        _array.Clear(0, tail);
                    }
                }

                QueueHead = 0;
                QueueTail = 0;
                QueueCount = 0;
            }
            finally
            {
                _array.ReleaseSpinLock();
            }
        }

        /// <summary>
        /// Determines whether the queue contains a specified element by using the default equality comparer.
        /// </summary>
        /// <param name="item">The value to locate in the queue.</param>
        /// <returns>true if the queue contains the element that has the specified value; otherwise, false</returns>
        public bool Contains(T item)
        {
            _array.AcquireSpinLock();

            bool found = false;

            try
            {
                long index = QueueHead;
                long itemCount = QueueCount;
                long capacity = _array.Length;
                EqualityComparer<T> comparer = EqualityComparer<T>.Default;

                while (itemCount-- > 0)
                {
                    if (comparer.Equals(_array[index], item))
                    {
                        found = true;
                        break;
                    }
                    index = (index + 1) % capacity;
                }
            }
            finally
            {
                _array.ReleaseSpinLock();
            }

            return found;
        }

        /// <summary>
        /// Get element at specific index. The index is relative to the position of the queue’s 
        /// head element, where an index of 0 will return the head element.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public T GetElement(int i)
        {
            // TODO: Decide lock behavior
            long capacity = _array.Length;
            long head = QueueHead;

            return _array[(head + i) % capacity];
        }

        /// <summary>
        /// Adds an object to the end of the queue without any synchronization locks.
        /// </summary>
        /// <param name="item"></param>
        internal void EnqueueNoLock(T item)
        {
            long capacity = _array.Length;
            long count = QueueCount;
            long tail = QueueTail;
            long itemSize = _array.ItemSize;

            if (count == capacity)
            {
                if (_fullBehavior == QueueFullBehaviorEnum.ThrowException)
                {
                    string message = string.Format("Number of queue entries exceeds the queue capacity.");
                    throw new IndexOutOfRangeException(message);
                }

                if (_fullBehavior == QueueFullBehaviorEnum.OverwriteOldEntries)
                {
                    DequeueNoLock();
                }
            }

            _array[tail] = item;
            tail = (tail + 1) % capacity;
            count++;
            QueueCount = count;
            QueueTail = tail;
        }

        /// <summary>
        /// Adds an object to the end of the queue.
        /// </summary>
        /// <param name="item">The object to add to the queue<T></param>
        public virtual void Enqueue(T item)
        {
            _array.AcquireSpinLock();

            try
            {
                EnqueueNoLock(item);
            }
            finally
            {
                _array.ReleaseSpinLock();
            }
        }


        /// <summary>
        /// Removes and returns the object at the beginning of the queue without any synchronization locks.
        /// </summary>
        /// <returns>The object that is removed from the beginning of the queue.</returns>
        internal T DequeueNoLock()
        {
            long count = QueueCount;
            long head = QueueHead;
            long capacity = _array.Length;
            long itemSize = _array.ItemSize;

            if (count == 0)
            {
                string message = "Invalid operation queue is empty.";
                throw new InvalidOperationException(message);
            }

            T item = _array[head];
            //_array[head] = default(T);
            head = (head + 1) % capacity;
            count--;

            QueueHead = head;
            QueueCount = count;

            return item;
        }

        /// <summary>
        /// Removes and returns the object at the beginning of the queue .
        /// </summary>
        /// <returns>The object that is removed from the beginning of the queue.</returns>
        public T Dequeue()
        {
            T item;

            _array.AcquireSpinLock();

            try
            {
                item = DequeueNoLock();
            }
            finally
            {
                _array.ReleaseSpinLock();
            }

            return item;
        }

        /// <summary>
        /// Remove all items from queue and return removed items as array.
        /// </summary>
        /// <returns>Array containing dequeued elements.</returns>
        public T[] DequeueAll()
        {
            T[] items;

            _array.AcquireSpinLock();

            try
            {
                items = ToArrayNoLock();
                QueueHead = 0;
                QueueTail = 0;
                QueueCount = 0;
                //_array[0] = default(T); // Write  default value to head. This will force a version increase.
            }
            finally
            {
                _array.ReleaseSpinLock();
            }

            return items;
        }

        /// <summary>
        /// Returns the element at the beginning of the queue without removing it. The method does not apply
        /// any synchronization locks.
        /// </summary>
        /// <returns>The element at the beginning of the queue.</returns>
        internal T PeekNoLock()
        {
            T item;
            long head = QueueHead;
            long count = QueueCount;


            if (count == 0)
            {
                string message = "Invalid operation queue is empty.";
                throw new InvalidOperationException(message);
            }

            item = _array[head];

            return item;
        }

        /// <summary>
        /// Returns the element at the beginning of the queue without removing it. 
        /// </summary>
        /// <returns>The element at the beginning of the queue.</returns>
        public T Peek()
        {
            _array.AcquireSpinLock();

            T item;

            try
            {

                item = PeekNoLock();
            }
            finally
            {
                _array.ReleaseSpinLock();
            }

            return item;
        }

        /// <summary>
        /// Returns the element at the end of the queue without removing it. 
        /// </summary>
        /// <returns>The element at the end of the queue.</returns>
        public T PeekTail()
        {
            _array.AcquireSpinLock();
            T item;

            try
            {
                item = PeekTailNoLock();
            }
            finally
            {
                _array.ReleaseSpinLock();
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
            ToArray().CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Copies the queue elements to an existing one-dimensional Array, starting at the specified array index.
        /// </summary>
        /// <param name="array">The zero-based one-dimensional Array that is the destination of the elements copied from the queue.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        void ICollection.CopyTo(Array array, int index)
        {
            ToArray().CopyTo(array, index);
        }

        /// <summary>
        /// Return queue elements as an array without any synchronization locks.
        /// </summary>
        /// <returns>Array containing queue elements.</returns>
        internal T[] ToArrayNoLock()
        {
            long capacity = _array.Length;
            long count = QueueCount;
            long head = QueueHead;
            long tail = QueueTail;

            T[] destinationArray = new T[count];

            if (count != 0)
            {
                if (head < tail)
                {
                    _array.CopyTo(head, ref destinationArray, 0, count);
                    return destinationArray;
                }

                _array.CopyTo(head, ref destinationArray, 0, capacity - head);
                _array.CopyTo(0, ref destinationArray, capacity - head, tail);
            }

            return destinationArray;
        }

        /// <summary>
        /// Return queue elements as an array.
        /// </summary>
        /// <returns>Array containing queue elements.</returns>
        public T[] ToArray()
        {
            T[] items;
            _array.AcquireSpinLock();

            try
            {
                items = ToArrayNoLock();
            }
            finally
            {
                _array.ReleaseSpinLock();
            }

            return items;
        }

        /// <summary>
        /// Returns the element at the end of the queue without removing it. The method does not apply
        /// any synchronization locks.
        /// </summary>
        /// <returns>The element at the end of the queue.</returns>
        internal T PeekTailNoLock()
        {
            T item;
            long tail = QueueTail - 1;
            long count = QueueCount;

            if (count == 0)
            {
                string message = "Invalid operation queue is empty.";
                throw new InvalidOperationException(message);
            }

            item = _array[tail];

            return item;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the IPPBoundedQueue.
        /// </summary>
        /// <returns>A IPPBoundedQueue<T>.Enumerator structure for the IPPBoundedQueue.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator((IPPBoundedQueue<T>)this);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the IPPBoundedQueue.
        /// </summary>
        /// <returns>A IPPBoundedQueue<T>.Enumerator structure for the IPPBoundedQueue.</returns>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator((IPPBoundedQueue<T>)this);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the IPPBoundedQueue.
        /// </summary>
        /// <returns>A IPPBoundedQueue<T>.Enumerator structure for the IPPBoundedQueue.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator((IPPBoundedQueue<T>)this);
        }

        #endregion
        
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
                if (_array != null)
                {
                    _array.Dispose();
                    _array = null;
                }

            }
            // free native resources if there are any. 
        }

        ~IPPBoundedQueue()
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
            private IPPBoundedQueue<T> _q;
            private int _index;
            private long _version;
            private T _currentElement;


            internal Enumerator(IPPBoundedQueue<T> q)
            {
                _q = q;
                _version = _q._array.Version;
                _index = -1;
                _currentElement = default(T);
            }

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose()
            {
                _index = -2;
                _currentElement = default(T);
            }

            /// <summary>
            /// Advances the enumerator to the next element of the queue.
            /// </summary>
            /// <returns>true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the queue.</returns>
            public bool MoveNext()
            {
                if (_version != _q._array.Version)
                {
                    string message = "Invalid operation the underlying queue has been modified.";
                    throw new InvalidOperationException(message);
                }
                if (_index == -2)
                {
                    return false;
                }
                _index++;
                if (_index == _q.QueueCount)
                {
                    _index = -2;
                    _currentElement = default(T);
                    return false;
                }
                _currentElement = _q.GetElement(_index);
                return true;
            }

            /// <summary>
            /// Gets the element in the queue at the current position of the enumerator.
            /// </summary>
            public T Current
            {
                get
                {
                    if (_index < 0)
                    {
                        if (_index == -1)
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
                    return _currentElement;
                }
            }

            /// <summary>
            /// Gets the element in the queue at the current position of the enumerator.
            /// </summary>
            object IEnumerator.Current
            {

                get
                {
                    if (_index < 0)
                    {
                        if (_index == -1)
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
                    return _currentElement;
                }
            }

            /// <summary>
            /// Sets the enumerator to its initial position, which is before the first element in the queue.
            /// </summary>
            void IEnumerator.Reset()
            {
                if (_version != _q._array.Version)
                {
                    string message = "Invalid operation the underlying queue has been modified.";
                    throw new InvalidOperationException(message);
                }
                _index = -1;
                _currentElement = default(T);
            }
        }

        #endregion
    }
}
