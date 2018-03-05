using System;
using System.Runtime.InteropServices;

namespace InContex.Data.Interop
{
    internal class NativeMem
    {
        [DllImport("msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        internal static extern IntPtr MemSet(IntPtr dest, int c, int byteCount);
    }
}
