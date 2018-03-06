using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("InContex.Collections.Persisted.Core, PublicKey=0024000004800000940000000602000000240000525341310004000001000100ed8c2deb489a2d" +
                              "6f8dfc5904a518da3141c3563361b1cf0fb1759e260cdc80a77c1cf73c1d11c124b9574587128a" +
                              "660979908ee27ef61ae18b90a9aa27ffdbfcf35e5baf8aefa882c7b24094de7f32b31550582552" +
                              "a2025b0bf3dbbdd75b198a87b17c470ae1d653bb628740a57963547e546f4c5a33e4a123b0e988" +
                              "4139c2bd")]
namespace InContex.Runtime.Interop
{
    /// <summary>
    /// Class exposes native memory management functions to allow high performance memory accesses. 
    /// </summary>
    internal class NativeMethods
    {
        [DllImport("msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        internal static extern IntPtr MemSet(IntPtr dest, int c, int byteCount);
    }
}
