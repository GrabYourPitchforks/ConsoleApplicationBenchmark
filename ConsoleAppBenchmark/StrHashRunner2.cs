using BenchmarkDotNet.Attributes;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ConsoleAppBenchmark
{
    public class StrHashRunner2
    {
        private byte[] _input;

        [Params(64)]
        public int InputLengthInBytes { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _input = new byte[InputLengthInBytes];
        }

        //[Benchmark]
        //public int StrHashCode()
        //{
        //    return String.GetHashCode();
        //}

        [Benchmark]
        [SkipLocalsInit]
        public unsafe int DoSha256ComputeHash()
        {
            const int DataByteCount = 256 / 8;
            byte* bytes = stackalloc byte[DataByteCount];
            SHA256.HashData(_input, new Span<byte>(bytes, DataByteCount));
            return Unsafe.ReadUnaligned<int>(bytes);
        }

        [Benchmark]
        [SkipLocalsInit]
        public unsafe int DoSha512ComputeHash()
        {
            const int DataByteCount = 512 / 8;
            byte* bytes = stackalloc byte[DataByteCount];
            SHA512.HashData(_input, new Span<byte>(bytes, DataByteCount));
            return Unsafe.ReadUnaligned<int>(bytes);
        }
    }
}
