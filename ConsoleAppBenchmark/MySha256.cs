using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ConsoleAppBenchmark
{
    public unsafe static class MySha256
    {
        public static void HashData(ReadOnlySpan<byte> data, Span<byte> digest)
        {
            if (digest.Length != 32)
            {
                Throw();
            }

            fixed (byte* pbInput = data)
            fixed (byte* pbDigest = digest)
            {
                byte* pbActualInput = pbInput;
                if (pbActualInput == null) { pbActualInput = (byte*)1; } // cannot be null
                int retVal = BCryptHash((IntPtr)0x00000041, null, 0, pbActualInput, (uint)data.Length, pbDigest, (uint)digest.Length);

                if (retVal != 0)
                {
                    ThrowRetval(retVal);
                }
            }
        }

        private static void Throw()
        {
            throw new Exception();
        }

        private static void ThrowRetval(int retVal)
        {
            throw new Exception();
        }

        [DllImport("bcrypt.dll", CallingConvention = CallingConvention.Winapi, SetLastError = false)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int BCryptHash(
            [In] IntPtr hAlgorithm,
            [In] byte* pbSecret,
            [In] uint cbSecret,
            [In] byte* pbInput,
            [In] uint cbInput,
            [In] byte* pbOutput,
            [In] uint cbOutput);
    }
}
