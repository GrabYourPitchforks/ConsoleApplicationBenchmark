using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    public class MemoryRunner
    {
        private ReadOnlyMemory<byte> _romBytesFromArray = new byte[1024];
        private ReadOnlyMemory<byte> _romBytesFromMM = new MyMemoryManager<byte>().Memory;
        private ReadOnlyMemory<byte> _romBytesEmpty = ReadOnlyMemory<byte>.Empty;

        private ReadOnlyMemory<char> _romCharsFromString = new string('x', 1024).AsMemory();
        private ReadOnlyMemory<char> _romCharsFromArray = new char[1024];
        private ReadOnlyMemory<char> _romCharsFromMM = new MyMemoryManager<char>().Memory;
        private ReadOnlyMemory<char> _romCharsEmpty = ReadOnlyMemory<char>.Empty;

        private const int NUM_ITERS = 1_000_000;

        [Benchmark]
        public int GetSpan_MemOfBytesFromArray()
        {
            return DoTest(_romBytesFromArray);
        }

        //[Benchmark]
        //public int GetSpan_MemOfBytesFromMemMgr()
        //{
        //    return DoTest(_romBytesFromMM);
        //}

        //[Benchmark]
        //public int GetSpan_MemOfBytesEmpty()
        //{
        //    return DoTest(_romBytesEmpty);
        //}

        //[Benchmark]
        //public int GetSpan_MemOfCharsFromArray()
        //{
        //    return DoTest(_romCharsFromArray);
        //}

        //[Benchmark]
        //public int GetSpan_MemOfCharsFromString()
        //{
        //    return DoTest(_romCharsFromString);
        //}

        //[Benchmark]
        //public int GetSpan_MemOfCharsFromMemMgr()
        //{
        //    return DoTest(_romCharsFromMM);
        //}

        //[Benchmark]
        //public int GetSpan_MemOfCharsEmpty()
        //{
        //    return DoTest(_romCharsEmpty);
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DoTest<T>(in ReadOnlyMemory<T> rom)
        {
            int result = default;

            for (int i = NUM_ITERS; i > 0; i--)
            {
                var span = rom.Span;
                result += span.Length;

                if (!span.IsEmpty)
                {
                    result += Unsafe.As<T, int>(ref MemoryMarshal.GetReference(span));
                }
            }

            return result;
        }

        private sealed class MyMemoryManager<T> : MemoryManager<T>
        {
            private readonly T[] _arr = new T[1024];

            public override Span<T> GetSpan()
            {
                _ = _arr.Length;
                return _arr;
            }

            public override MemoryHandle Pin(int elementIndex = 0)
            {
                throw new NotImplementedException();
            }

            public override void Unpin()
            {
                throw new NotImplementedException();
            }

            protected override void Dispose(bool disposing)
            {
                throw new NotImplementedException();
            }
        }
    }
}
