using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ConsoleAppBenchmark
{
    public static class MyMac
    {
        private static readonly AesGcm _aesGcm = new AesGcm(new byte[128 / 8]);
        private static ReadOnlySpan<byte> nonce => new byte[96 / 8];

        public static void HashData(ReadOnlySpan<byte> data, Span<byte> digest)
        {
            _aesGcm.Encrypt(nonce, ReadOnlySpan<byte>.Empty, Span<byte>.Empty, digest, data);
        }
    }
}
