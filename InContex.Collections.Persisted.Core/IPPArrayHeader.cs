using System;
using System.Runtime.InteropServices;

namespace InContex.Collections.Persisted.Core
{
    /// <summary>
    /// Header struct for memory mapped array.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct IPPArrayHeader
    {
        /// <summary>
        /// Padding used to ensure that the LockState does not share the CPU cache line 
        /// </summary>
        private fixed byte Pad0[64];    
        /// <summary>
        /// Shared lock used to synchronize reads and writes between processes.
        /// </summary>
        public long LockState;
        /// <summary>
        /// Padding used to ensure that the LockState does not share the CPU cache line 
        /// </summary>
        private fixed byte Pad1[64];
        /// <summary>
        /// The number of times the array has been initialized. 
        /// If 0 then array has never been persisted and still needs to be created.
        /// </summary>
        public long InitializationCount;
        /// <summary>
        /// The size of each of the enqueued entries in bytes.
        /// </summary>
        public long ItemSize;
        /// <summary>
        /// The length of the array (maximum number of entries).
        /// </summary>
        public long Length;
        /// <summary>
        /// The queue version number. Each time an entry is enqueued or dequeued the version number will increase.
        /// </summary>
        public long Version;
        /// <summary>
        /// Data block that allows user to read and write custom control information.
        /// </summary>
        public fixed long UserControlBlock[5];
        /// <summary>
        /// Padding used to ensure cache lines does not overlap
        /// </summary>
        private fixed byte Pad2[64];
    }
}
