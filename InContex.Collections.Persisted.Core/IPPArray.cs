#define UseIntPtr

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using InContex.Runtime.Serialization;
using InContex.Runtime.Interop;

using NLog;


[assembly: InternalsVisibleTo("InContex.Collections.Persisted, PublicKey=0024000004800000940000000602000000240000525341310004000001000100ed8c2deb489a2d" +
                              "6f8dfc5904a518da3141c3563361b1cf0fb1759e260cdc80a77c1cf73c1d11c124b9574587128a" +
                              "660979908ee27ef61ae18b90a9aa27ffdbfcf35e5baf8aefa882c7b24094de7f32b31550582552" +
                              "a2025b0bf3dbbdd75b198a87b17c470ae1d653bb628740a57963547e546f4c5a33e4a123b0e988" +
                              "4139c2bd")]
namespace InContex.Collections.Persisted.Core
{

    /// <summary>
    /// Inter Process Persited Array, represents a fixed length disk backed array that can be 
    /// accessed across process boundaries.
    /// </summary>
    /// <typeparam name="T">The type of element stored in the array.</typeparam>
    public class IPPArray<T> : IEnumerable<T>, IEnumerable, ICollection, IDisposable where T : struct
    {
        #region Field Members
        /// <summary>
        /// Logger used to capture diagnostic information.  
        /// </summary>
        private static Logger __logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// Readonly variable used to determine if code is being executed within a multi-processor 
        /// environment. The behavior of the spin locking mechanism will change depending on whether 
        /// or not the code is run on a single processor or multi-processor environment.  
        /// </summary>
        private static readonly bool __multiProcessor;
        /// <summary>
        /// The number of bytes in the operating system's memory page. This is required 
        /// to ensure that when Memory map is created that we actual file size aligns with memory pages.
        /// </summary>
        private static readonly int __pageSize;
        /// <summary>
        /// Store current process ID.
        /// </summary>
        private static long __processID;
        /// <summary>
        /// Memory Mapped File that enable object persistence and inter process communication.
        /// </summary>
        private MemoryMappedFile _mmf;
        /// <summary>
        /// View accessor into the memory mapped file.
        /// </summary>
        private MemoryMappedViewAccessor _view;
        /// <summary>
        /// Serializer used to serialize struct data type.
        /// </summary>
        private ISerializer<T> _serializer;
        /// <summary>
        /// The size of items stored in the array.
        /// </summary>
        private int _itemSize;
        /// <summary>
        /// The persited array file header size.
        /// </summary>
        private int _headerSize;
        /// <summary>
        /// The array named instance name, used to reference array across process boundaries.
        /// </summary>
        private string _name;
        /// <summary>
        /// The path where the persited array will be stored.
        /// </summary>
        private string _path;
        /// <summary>
        /// Flag indicates if this is a server instance or client instance of the array.
        /// </summary>
        private bool _server;
        /// <summary>
        /// Pointer to the MemoryMappedViewAccessor buffer.
        /// </summary>
        private IntPtr _viewPtr = new IntPtr();
  

        [NonSerialized]
        private object _syncRoot;

        private bool _disposed = false;

        #endregion

        #region Constants

        const string InitializationLockMutexName = "Init-Lock-{0}";
        const string ServerInstanceMutexName = "Server-Instance-{0}";

        private const long Initialized = long.MinValue + 1;
        private const long NotInitialized = long.MinValue;

        #endregion

        #region Constructors

        static IPPArray()
        {
            __multiProcessor = (Environment.ProcessorCount > 1);
            __pageSize = Environment.SystemPageSize;
            __processID = ((long)Process.GetCurrentProcess().Id) << 32;
        }

        private IPPArray()
        {
            string message = "Default constructor is not supported for this class.";
            throw new NotSupportedException(message);
        }

        /// <summary>
        /// Determine if a persisted array already exists for the specified array name.
        /// </summary>
        /// <param name="path">Path where persisted array is saved.</param>
        /// <param name="name">Name of the persisted array</param>
        /// <returns></returns>
        private static bool PersistedArrayExists(string path, string name)
        {
            bool exists = false;
            string filename = name.Trim() + ".mma";
            string fileFullName = Path.Combine(path.Trim(), filename);

            exists = File.Exists(fileFullName);

            return exists;
        }

        private static IPPArrayHeader GetExistingArrayHeader(string path, string name)
        {
            bool exists = PersistedArrayExists(path, name);
            string filename = name.Trim() + ".mma";
            string fileFullName = Path.Combine(path.Trim(), filename);

            IPPArrayHeader header = new IPPArrayHeader();
            header.InitializationCount = 0;

            if (exists)
            {
                int headerSize = Marshal.SizeOf(typeof(IPPArrayHeader));

                using (MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(name))
                using (MemoryMappedViewAccessor view = mmf.CreateViewAccessor(0, headerSize, MemoryMappedFileAccess.Read))
                {
                    unsafe
                    {
                        byte* viewPtr = null;

                        view.SafeMemoryMappedViewHandle.AcquirePointer(ref viewPtr);

                        try
                        {
                            header = (IPPArrayHeader)(Marshal.PtrToStructure((IntPtr)viewPtr, typeof(IPPArrayHeader)));
                        }
                        finally
                        {
                            view.SafeMemoryMappedViewHandle.ReleasePointer();
                        }
                    }
                }
            }
            else
            {
                string message = string.Format("Could not find the memory map file for persisted array '{0}'. Expected memory map file '{1}'.", name, fileFullName);
                __logger.Error(message);
                throw new FileNotFoundException(message, fileFullName);
            }

            return header;
        }

        public static IPPArray<T> Open(string path, string name, int length, ISerializer<T> serializer)
        {
            int timeoutMS = 10000; // one second timeout
            string mutexName = string.Format(InitializationLockMutexName, name);

            if(length <= 0)
            {
                string message = string.Format("{0} is an invalid array length. Array length must be greater than zero.", length);
                __logger.Error(message);
                throw new ArgumentOutOfRangeException("length", message);
            }

            IPPArray<T> array = null;
            Mutex mutex = new Mutex(false, mutexName);

            if (mutex.WaitOne(timeoutMS))
            {
                try
                {
                    array = new IPPArray<T>(path, name, length, serializer);
                }
                catch (Exception exception)
                {
                    string message = string.Format("Exception occurred while attempting to open existing IPPArray '{0}' located in '{1}' directory.", name, path);
                    __logger.Error(exception, message);
                    // re-throw the error
                    throw new ApplicationException(message, exception);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
            else
            {
                string message = string.Format("Mutex timed out after {0} milliseconds. Failed to acquire exclusive lock while attempting to open IPPArray '{1}'.", timeoutMS, name);
                __logger.Error(message);
                throw new TimeoutException(message);
            }

            return array;
        }

        public static IPPArray<T> Open(string path, string name, int length)
        {
            return Open(path, name, length, new GenericSerializer<T>());
        }


        private IPPArray(string path, string name, int length, ISerializer<T> serializer)
        {
#if DEBUG
            __logger.Info("IPPArray({0}, {1}, {2}, ISerializer<{3}>) ...", path, name, length, typeof(T).Name);
#endif
            _path = path;
            _name = name;
            _serializer = serializer;
            _itemSize = serializer.SerializedByteSize();
            _headerSize = Marshal.SizeOf(typeof(IPPArrayHeader));
            _server = false;

            string filename = name.Trim() + ".mma";
            string fileFullName = Path.Combine(path.Trim(), filename);
            string mutexName = string.Format(ServerInstanceMutexName, name.Trim());

            bool createServerInstance = false;

            try
            {
#if DEBUG
                __logger.Info("Attempt to create client instance of '{0}'.", name);
#endif
                _mmf = MemoryMappedFile.OpenExisting(name);
                IPPArrayHeader header = GetExistingArrayHeader(path, name);

                int totalSize = _headerSize + (length * _itemSize);
                int totalMemoryPages = (totalSize / __pageSize) + 1;
                int memoryMapSize = totalMemoryPages * __pageSize;

                _view = _mmf.CreateViewAccessor(0, memoryMapSize, MemoryMappedFileAccess.ReadWrite);
                AcquireViewPointer();

                long persistedItemSize = GetItemSize();
                long persistedLength = GetLength();

                if (_itemSize != persistedItemSize)
                {
                    Close();
                    string message = "Expected memory map message size does not match persisted message size.";
                    __logger.Error(message);
                    throw new ArrayTypeMismatchException(message);
                }

                if (length != persistedLength)
                {
                    Close();
                    string message = "Persisted array length mismatches expected array length.";
                    __logger.Error(message);
                    throw new ApplicationException(message);
                }

#if DEBUG
                __logger.Info("Successfully created client instance of '{0}'", name);
#endif
            }
            catch
            {
                // Failed to open memory map as client
                createServerInstance = true;
#if DEBUG
                __logger.Info("Failed to create a client instance of { 0}. Attempting to create server instance.", name);
#endif
            }

            if (createServerInstance)
            {
                // Attempt to open array as a server.
                Directory.CreateDirectory(path);

                int expectedLength = length;
                int totalSize = _headerSize + (expectedLength * _itemSize);
                int totalMemoryPages = (totalSize / __pageSize) + 1;
                int memoryMapSize = totalMemoryPages * __pageSize;

                // We are the only instance attempting to open MMFArray as a server.
                _mmf = MemoryMappedFile.CreateFromFile(fileFullName, FileMode.OpenOrCreate, name, memoryMapSize);
                _view = _mmf.CreateViewAccessor(0, memoryMapSize, MemoryMappedFileAccess.ReadWrite);
                AcquireViewPointer();

                long initializationCount = GetInitializationCount();

                if(initializationCount <= 0)
                {
                    // This is the first time the array was created. Set information specific to this array persited array.
                    SetItemSize(_itemSize);
                    SetLength(expectedLength);
                    IncrementInitializationCount();
                    _server = true;
#if DEBUG
                    __logger.Info("Successfully created server instance of '{0}'", name);
#endif
                }
                else
                {
                    long persistedItemSize = GetItemSize();
                    long persistedLength = GetLength();

                    if (_itemSize != persistedItemSize)
                    {
                        Close();
                        string message = "Expected memory map message size does not match persisted message size.";
                        throw new ArrayTypeMismatchException(message);
                    }

                    if (expectedLength != persistedLength)
                    {
                        Close();
                        string message = "Persisted array length mismatches expected array length.";
                        throw new ApplicationException(message);
                    }

                    IncrementInitializationCount();
                    _server = true;
#if DEBUG
                    __logger.Info("Successfully created server instance of '{0}'", name);
#endif
                }
            }
        }


#endregion

        #region Properties

        /// <summary>
        /// The length of the array.
        /// </summary>
        public int Length => (int)GetLength();

        /// <summary>
        /// The length of the array.
        /// </summary>
        public long LongLength => GetLength();

        /// <summary>
        /// Gets a value indicating whether the Array has a fixed size.
        /// </summary>
        /// <remarks>
        /// This property is always true for all IPPArray objects.
        /// </remarks>
        public bool IsFixedSize => true;

        /// <summary>
        /// Get the number of dimensions of the array.
        /// </summary>
        /// <remarks>
        /// MMFArray only support one dimensional arrays, as a result this property will always return 1.
        /// </remarks>
        public int Rank => 1;

        /// <summary>
        /// The size of each of the enqueued entries in bytes.
        /// </summary>
        public long ItemSize => GetItemSize();

        /// <summary>
        /// Indicate the number of times the array has been initialized.
        /// </summary>
        public long InitializationCount => GetInitializationCount();

        /// <summary>
        /// Return the current lock state of the buffer.
        /// </summary>
        public long LockState
        {
            get => GetLockState();
        }
        public bool ServerInstance => _server;

        /// <summary>
        /// ICollection implementation of count. This will simply return length.
        /// </summary>
        int ICollection.Count => (int)Length;

        bool ICollection.IsSynchronized => false;

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

        private unsafe long GetInitializationCount()
        {
            
            long initialized = 0;

            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(_viewPtr);
            initialized = headerPtr->InitializationCount;

            return initialized;
        }

        private unsafe void IncrementInitializationCount()
        {
            int timeout = 1000; // one second timeout
            string mutexName = string.Format(InitializationLockMutexName, _name);

            Mutex mutex = new Mutex(false, mutexName);

            try
            {
                if (mutex.WaitOne(timeout))
                {
                    IPPArrayHeader* headerPtr = (IPPArrayHeader*)(_viewPtr);

                    if (headerPtr->InitializationCount >= int.MaxValue)
                    {
                        headerPtr->InitializationCount = 1;
                    }
                    else
                    {
                        headerPtr->InitializationCount++;
                    }
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }


        private unsafe long GetItemSize()
        {
            long itemSize = 0;

            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(_viewPtr);
            itemSize = headerPtr->ItemSize;
            return itemSize;
        }

        private unsafe void SetItemSize(long itemSize)
        {
            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(_viewPtr);
            headerPtr->ItemSize = itemSize;
        }


        private unsafe long GetLength()
        {
            long length = 0;

            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(_viewPtr);
            length = headerPtr->Length;

            return length;
        }

        private unsafe void SetLength(long length)
        {
            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(_viewPtr);
            headerPtr->Length = length;
        }

        /// <summary>
        /// Serves as the hash function for the array. 
        /// </summary>
        /// <returns>A hash code for the current array.</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        private unsafe long GetVersion()
        {

            long version = 0;

            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(_viewPtr);
            version = headerPtr->Version;

            return version;
        }

        private unsafe void VersionIncrement()
        {
            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(_viewPtr);
            // We don't care if value overflows, simply that it changes.
            unchecked
            {
                headerPtr->Version++;
            }
        }

        /// <summary>
        /// Get the array version number. Each time an entry is enqueued or dequeued the version number will increase.
        /// </summary>
        /// <remarks>
        /// Each time a write action is made to the array the version number associated with the array will increase. 
        /// This version number is used to help detect changes to the array.
        /// </remarks>
        public long Version
        {
            get
            {
                return GetVersion();
            }
        }

        private unsafe long GetLockState()
        {
            long lockstate = 0;

            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(_viewPtr);
            lockstate = headerPtr->LockState;

            return lockstate;
        }


        private unsafe void AcquireViewPointer()
        {
            byte* viewPtr = null;

            _view.SafeMemoryMappedViewHandle.AcquirePointer(ref viewPtr);

            try
            {
                _viewPtr = (IntPtr)viewPtr;
            }
            catch(Exception exp)
            {
                string message = "Failed to acquire pointer buffer header.";
                __logger.Error(message);
                throw new ApplicationException(message, exp);
            }
        }

        private unsafe void ReleaseViewPointer()
        {

            try
            {
                if (_viewPtr != IntPtr.Zero)
                {
                    _view.SafeMemoryMappedViewHandle.ReleasePointer();
                    _viewPtr = new IntPtr(); // Zero the pointer.
                }
            }
            catch (Exception exp)
            {
                string message = "Failed to release pointer buffer header.";
                __logger.Error(message);
                throw new ApplicationException(message, exp);
            }
        }


        private void CloseView()
        {
            if (_view != null)
            {
                ReleaseViewPointer();
                _view.Dispose();
            }

            _view = null;
        }

        private void Close()
        {
            string mutexName = string.Format(InitializationLockMutexName, _name);
            int timeoutMS = 10000;

            Mutex mutex = new Mutex(false, mutexName);

            if (mutex.WaitOne(timeoutMS))
            {
                try
                {
                    CloseView();

                    if (_mmf != null)
                    {
                        _mmf.Dispose();
                    }

                    _mmf = null;
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        /// <summary>
        /// Sets a range of elements in an array to the default value of each element type.
        /// </summary>
        /// <param name="index">The starting index of the range of elements to clear.</param>
        /// <param name="length">The number of elements to clear.</param>
        public unsafe void Clear(long index, long length)
        {
            byte* viewPtr = null;

            viewPtr = (byte*)(_viewPtr);
            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(viewPtr);

            int byteOffset = (int)index * _itemSize;
            int byteLength = (int)length * (int)_itemSize;

            byte* bufferPtr = viewPtr + _headerSize;
            NativeMem.MemSet((IntPtr)(bufferPtr + byteOffset), 0, byteLength);

            VersionIncrement();
        }


        /// <summary>
        /// Copies a specific number of elements of the current one-dimensional array to the specified 
        /// one-dimensional array from a specified source starting index to a specified destination index. 
        /// </summary>
        /// <param name="index">Index in current array at which copying will begin.</param>
        /// <param name="destinationArray">The one-dimensional array that is the destination of the elements copied from the current array.</param>
        /// <param name="destinationIndex">Index in destination array at which copying will begin.</param>
        /// <param name="length">The number of elements to copy.</param>
        public unsafe void CopyTo(long index, ref T[] destinationArray, long destinationIndex, long length)
        {
            long itemSize = _itemSize;

            long destinationByteLength = destinationArray.Length * itemSize;
            long byteIndex = index * itemSize;
            long destinationByteIndex = destinationIndex * itemSize;
            long byteLength = length * itemSize;

            byte* viewPtr = null;

            _view.SafeMemoryMappedViewHandle.AcquirePointer(ref viewPtr);

            try
            {
                IPPArrayHeader* headerPtr = (IPPArrayHeader*)(viewPtr);
                byte* bufferPtr = viewPtr + _headerSize;

                GCHandle handle = GCHandle.Alloc(destinationArray, GCHandleType.Pinned);
                IntPtr destinationPtr = handle.AddrOfPinnedObject();
                Buffer.MemoryCopy((bufferPtr + byteIndex), (((byte*)destinationPtr) + destinationByteIndex), destinationByteLength, byteLength);

                handle.Free();

            }
            finally
            {
                _view.SafeMemoryMappedViewHandle.ReleasePointer();
            }

        }

        /// <summary>
        /// Copies all the elements of the current one-dimensional array to the specified one-dimensional array 
        /// starting at the specified destination array index. The index is specified as a 32-bit integer.
        /// </summary>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from the current array.</param>
        /// <param name="index">A 32-bit integer that represents the index in array at which copying begins.</param>
        public void CopyTo(ref T[] array, int index)
        {
            CopyTo(ref array, ((long)index));
        }

        /// <summary>
        /// Copies all the elements of the current one-dimensional array to the specified one-dimensional array 
        /// starting at the specified destination array index. The index is specified as a 64-bit integer.
        /// </summary>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from the current array.</param>
        /// <param name="index">A 64-bit integer that represents the index in array at which copying begins.</param>
        public void CopyTo(ref T[] array, long index)
        {
            long length = Length - index;
            CopyTo(index, ref array, 0, length);
        }

        /// <summary>
        /// ICollection implementation of CopyTo method. Copies the elements of the ICollection to an 
        /// Array, starting at a particular Array index.
        /// </summary>
        /// <param name="array">The one-dimensional Array that is the destination of the elements copied from ICollection. The Array must have zero-based indexing.</param>
        /// <param name="index">The zero-based index in array at which copying begins.</param>
        void ICollection.CopyTo(Array array, int index)
        {
            long length = Length;
            T[] memoryArray = new T[length];

            CopyTo(ref memoryArray, length);

            memoryArray.CopyTo(array, index);
        }


        /// <summary>
        /// Copies data from current array to destination array as bytes.
        /// </summary>
        /// <param name="index">Byte index in current array from which copying will begin.</param>
        /// <param name="destinationArray">Destination byte array where data will be copied to.</param>
        /// <param name="destinationIndex">Byte index in destination byte array at which copying will begin.</param>
        /// <param name="length">The number of bytes to copy.</param>
        private unsafe void CopyBytesTo(long index, ref byte[] destinationArray, long destinationIndex, long length)
        {
            int destinationLength = destinationArray.Length;
            byte* viewPtr = null;

            viewPtr = (byte*)(_viewPtr);
            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(viewPtr);
            byte* bufferPtr = viewPtr + _headerSize;

            fixed (byte* pDestinationArray = destinationArray)
            {
                Buffer.MemoryCopy((bufferPtr + index), (pDestinationArray + destinationIndex), destinationLength, length);
            }
        }

        /// <summary>
        /// Copies data from a source array into the current array as bytes. 
        /// </summary>
        /// <param name="sourceArray">Source byte array to copy data from.</param>
        /// <param name="sourceIndex">A 64-bit integer that represents the index in source array (byte offset) at which copying of data should start from.</param>
        /// <param name="destinationIndex">A 64-bit integer that represents the index in current array array (byte offset) at which copying begins.</param>
        /// <param name="length">The number of bytes to copy.</param>
        private unsafe void CopyBytesFrom(ref byte[] sourceArray, long sourceIndex, long destinationIndex, long length)
        {
            byte* viewPtr = null;
            long destinationLength = Length * ItemSize;

            viewPtr = (byte*)(_viewPtr);
            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(viewPtr);
            byte* bufferPtr = viewPtr + _headerSize;

            fixed (byte* pSourceArray = sourceArray)
            {
                Buffer.MemoryCopy((pSourceArray + sourceIndex), (bufferPtr + destinationIndex), destinationLength, length);
            }
            VersionIncrement();
        }

        /// <summary>
        /// Copies elements of the source one-dimensional array to the current array.
        /// </summary>
        /// <param name="sourceArray">The source array from which the data will be copied.</param>
        /// <param name="sourceIndex">Index in source array from which copying will begin.</param>
        /// <param name="destinationIndex">Index in current array at which copying will begin.</param>
        /// <param name="length">The length of the data to copy.</param>
        public unsafe void CopyFrom(ref T[] sourceArray, long sourceIndex, long destinationIndex, long length)
        {
            byte* viewPtr = null;
            long destinationByteLength = Length * ItemSize;
            long sourceByteLength = length * ItemSize;
            long sourceByteIndex = sourceIndex * ItemSize;
            long destinationByteIndex = destinationIndex * ItemSize;
            long itemSize = _itemSize;

            viewPtr = (byte*)(_viewPtr);
            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(viewPtr);
            byte* bufferPtr = viewPtr + _headerSize;

            GCHandle handle = GCHandle.Alloc(sourceArray, GCHandleType.Pinned);
            IntPtr pSourceArray = handle.AddrOfPinnedObject();

            Buffer.MemoryCopy(((byte*)pSourceArray + sourceByteIndex), (bufferPtr + destinationByteIndex), destinationByteLength, sourceByteLength);

            handle.Free();

            VersionIncrement();
        }


        /// <summary>
        /// Read custom control information from the array header control block.
        /// </summary>
        /// <param name="index">Index of the control block entry to read.</param>
        /// <returns>Returns the value of the control block entry.</returns>
        internal unsafe long GetControlBlockEntry(int index)
        {
            byte* viewPtr = null;
            long controlBlockEntry = 0;

            viewPtr = (byte*)(_viewPtr);
            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(viewPtr);
            controlBlockEntry = headerPtr->UserControlBlock[index];

            return controlBlockEntry;
        }

        /// <summary>
        /// Write custom control information to the array header control block.
        /// </summary>
        /// <param name="index">Index of the control block entry to write to.</param>
        /// <param name="value">The value to write to the control block.</param>
        internal unsafe void SetControlBlockEntry(int index, long value)
        {
            byte* viewPtr = null;

            viewPtr = (byte*)(_viewPtr);
            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(viewPtr);
            headerPtr->UserControlBlock[index] = value;
        }


        /// <summary>
        /// Gets the value of the specified element in the current Array
        /// </summary>
        /// <param name="index">A 64-bit integer that represents the position of the Array element to get</param>
        /// <returns>The value at the specified position in the one-dimensional Array</returns>
        public T GetValue(long index)
        {
            long capacity = GetLength();

            if (index >= 0 && index < capacity)
            {
                //long offsetByte = (index * this._itemSize) + this._headerSize;
                //byte[] elementBytes = ReadBytes(offsetByte, _itemSize);

                long offsetByte = (index * _itemSize);
                byte[] elementBytes = new byte[_itemSize];

                CopyBytesTo(offsetByte, ref elementBytes, 0, _itemSize);

                return _serializer.DeSerialize(elementBytes);
            }
            else
            {
                string message = string.Format("Specified index {0} is out of range. Expected index range {1} - {2}", index, 0, capacity - 1);
                __logger.Error(message);
                throw new IndexOutOfRangeException(message);
            }
        }
        /// <summary>
        /// Gets the value of the specified element in the current Array.
        /// </summary>
        /// <param name="index">A 32-bit integer that represents the position of the Array element to get.</param>
        /// <returns>The value at the specified position in the one-dimensional Array.</returns>
        public T GetValue(int index)
        {
            return GetValue((long)index);
        }


        /// <summary>
        /// Sets a value to the element at the specified position in the one-dimensional Array.
        /// </summary>
        /// <param name="value">The new value for the specified element.</param>
        /// <param name="index">A 32-bit integer that represents the position of the Array element to set.</param>
        public void SetValue(T value, long index)
        {
            long capacity = GetLength();

            if (index >= 0 && index < capacity)
            {
                //long offsetByte = (index * this._itemSize) + this._headerSize;
                long offsetByte = (index * _itemSize);
                byte[] elementBytes = _serializer.Serialize(value);

                //WriteBytes(offsetByte, elementBytes);
                CopyBytesFrom(ref elementBytes, 0, offsetByte, _itemSize);

                VersionIncrement();
            }
            else
            {
                string message = string.Format("Specified index {0} is out of range. Expected index range {1} - {2}", index, 0, capacity - 1);
                __logger.Error(message);
                throw new IndexOutOfRangeException(message);
            }
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to get or set.</param>
        /// <returns>The element at the specified index.</returns>
        public T this[long index]
        {
            get => GetValue(index);
            set => SetValue(value, index);
        }

        // TODO: Investigating the code example of a re-entry safe Spin lock. Right now having my doubts about the correctness of this code.
        private long GetUniqueThreadID()
        {
            return __processID | (uint)Thread.CurrentThread.ManagedThreadId;
        }

        // TODO: Investigating the code example of a re-entry safe Spin lock. Right now having my doubts about the correctness of this code.
        public unsafe void ThreadExit()
        {
            long threadID = GetUniqueThreadID();
            byte* viewPtr = (byte*)(_viewPtr);
            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(viewPtr);
            
            // The following should be the same as the orginal code, except it will also include a memory barier.
            //Interlocked.CompareExchange(ref headerPtr->LockState, Initialized, threadID);

            
            if (headerPtr->LockState == Initialized)
            {
                return;
            }
            if (headerPtr->LockState == threadID)
            {
                headerPtr->LockState = Initialized;
            }
            
        }

        // TODO: Investigating the code example of a re-entry safe Spin lock. Right now having my doubts about the correctness of this code.
        public unsafe bool ThreadEnter()
        {
            long threadID = GetUniqueThreadID();
            int spinCount = 0;
            byte* viewPtr = null;

            viewPtr = (byte*)(_viewPtr);
            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(viewPtr);
            /*while (Interlocked.CompareExchange(ref headerPtr->LockState, 1, 0) != 0)
            {
                SpinWait(spinCount++);
            }*/

            // This state check seems unnecessary and not safe.  Not sure why original author included this.
            if (headerPtr->LockState == Initialized)
            {
                return false;
            }
            long inProgressByThisThread = Thread.CurrentThread.ManagedThreadId;
            long preexistingState = Interlocked.CompareExchange(ref headerPtr->LockState, inProgressByThisThread, NotInitialized);

            if (preexistingState == NotInitialized)
            {
                return true;
            }
            if (preexistingState == Initialized || preexistingState == inProgressByThisThread)
            {
                return false;
            }

            while (headerPtr->LockState != Initialized)
            {
                SpinWait(spinCount++);
            }

            return false;
        }

        /// <summary>
        /// Acquire a spin lock on the array for the calling thread / process. 
        /// </summary>
        /// <remarks>
        /// The spin lock is a non-reentrant lock, meaning that if a thread holds the lock, it is not 
        /// allowed to enter the lock again. If a thread attempts to enter a lock already held it will 
        /// result in a deadlock.
        /// </remarks>
        public unsafe void AcquireSpinLock()
        {
            int spinCount = 0;
            byte* viewPtr = null;

            viewPtr = (byte*)(_viewPtr);
            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(viewPtr);
            while (Interlocked.CompareExchange(ref headerPtr->LockState, 1, 0) != 0)
            {
                SpinWait(spinCount++);
            }
        }

        /// <summary>
        /// Release the spin lock held by the current thread / process.
        /// </summary>
        public unsafe void ReleaseSpinLock()
        {
            byte* viewPtr = null;

            viewPtr = (byte*)(_viewPtr);

            IPPArrayHeader* headerPtr = (IPPArrayHeader*)(viewPtr);

            long existingState = Interlocked.Exchange(ref headerPtr->LockState, 0);
            Debug.Assert(1 == existingState);
            }

        /// <summary>
        /// Causes a thread to wait the number of times defined by the iterations parameter.
        /// </summary>
        /// <param name="spinCount">A 32-bit signed integer that defines how long a thread is to wait.</param>
        static void SpinWait(int spinCount)
        {
            if (spinCount < 10 && __multiProcessor)
            {
                Thread.SpinWait(20 * (spinCount + 1));
            }
            else
            {
                // If SpinWait has been called more than 10 times and less than 15 then yield the thread
                if (spinCount < 15)
                {
                    Thread.Yield(); 
                }
                else
                {
                    // If SpinWait has been called 15 or more times then Sleep for on milli second.
                    Thread.Sleep(1);
                }
            }
        }

        #region Dispose

        /// <summary>
        /// Releases all resources used by the IPPArray.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources  
                Close();
                _disposed = true;
             }
            else
            {
                if(!_disposed)
                {
                    Close();
                    _disposed = true;
                }
            }
        }

        ~IPPArray()
        {
            Dispose(false);
        }

        #endregion

        /// <summary>
        /// Returns an enumerator that iterates through the IPPArray.
        /// </summary>
        /// <returns>A IPPArray<T>.Enumerator structure for the IPPArray.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator((IPPArray<T>)this);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the IPPArray.
        /// </summary>
        /// <returns>A IPPArray<T>.Enumerator structure for the IPPArray.</returns>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator((IPPArray<T>)this);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the IPPArray.
        /// </summary>
        /// <returns>A IPPArray<T>.Enumerator structure for the IPPArray.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator((IPPArray<T>)this);
        }

        /// <summary>
        /// Enumerates the elements of a IPPArray.
        /// </summary>
        public struct Enumerator : IEnumerator<T>, IDisposable, IEnumerator
        {
            private IPPArray<T> _array;
            private int _index;
            private long _version;
            private T _currentElement;

            /// <summary>
            /// IPPArray constructor.
            /// </summary>
            /// <param name="array"></param>
            internal Enumerator(IPPArray<T> array)
            {
                _array = array;
                _version = _array.Version;
                _index = -1;
                _currentElement = default(T);
            }

            /// <summary>
            /// Releases all resources used by the IPPArray.Enumerator.
            /// </summary>
            public void Dispose()
            {
                _index = -2;
                _currentElement = default(T);
            }

            /// <summary>
            /// Advances the enumerator to the next element of the IPPArray.
            /// </summary>
            /// <returns></returns>
            public bool MoveNext()
            {
                if (_version != _array.Version)
                {
                    string message = "Invalid operation the underlying array has been modified.";
                    __logger.Error(message);
                    throw new InvalidOperationException(message);
                }

                if (_index == -2)
                {
                    return false;
                }

                _index++;
                if (_index == _array.Length)
                {
                    _index = -2;
                    _currentElement = default(T);
                    return false;
                }

                _currentElement = _array.GetValue(_index);

                return true;
            }

            /// <summary>
            /// Gets the element at the current position of the enumerator.
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
                            __logger.Error(message);
                            throw new InvalidOperationException(message);
                        }
                        else
                        {
                            string message = "Invalid operation the enumeration is completed.";
                            __logger.Error(message);
                            throw new InvalidOperationException(message);
                        }
                    }
                    return _currentElement;
                }
            }

            /// <summary>
            /// Gets the element at the current position of the enumerator.
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
                            __logger.Error(message);
                            throw new InvalidOperationException(message);
                        }
                        else
                        {
                            string message = "Invalid operation the enumeration is completed.";
                            __logger.Error(message);
                            throw new InvalidOperationException(message);
                        }
                    }
                    return _currentElement;
                }
            }

            /// <summary>
            /// Sets the enumerator to its initial position, which is before the first element in the collection.
            /// </summary>
            void IEnumerator.Reset()
            {
                if (_version != _array.Version)
                {
                    string message = "Invalid operation the underlying queue has been modified.";
                    __logger.Error(message);
                    throw new InvalidOperationException(message);
                }
                _index = -1;
                _currentElement = default(T);
            }
        }
    }
}
