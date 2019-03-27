using System;
using System.Buffers;
using System.Runtime.CompilerServices;
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

        [Benchmark]
        public int GetSpan_MemOfBytesFromMemMgr()
        {
            return DoTest(_romBytesFromMM);
        }

        [Benchmark]
        public int GetSpan_MemOfBytesEmpty()
        {
            return DoTest(_romBytesEmpty);
        }

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
        public static T DoTest<T>(in ReadOnlyMemory<T> rom) => DoTest<T>(in rom, out _);

        public static T DoTest<T>(in ReadOnlyMemory<T> rom, out int unused)
        {
            int totalLength = default;
            T retVal = default;
            for (int i = 0; i < NUM_ITERS; i++)
            {
                var span = rom.Span;
                totalLength += span.Length;

                if (!span.IsEmpty)
                {
                    retVal = span[0];
                }
            }
            unused = totalLength;
            return retVal;
        }

        private sealed class MyMemoryManager<T> : MemoryManager<T>
        {
            private readonly T[] _arr = new T[1024];

            public override Span<T> GetSpan()
            {
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
