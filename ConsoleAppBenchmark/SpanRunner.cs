using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    public class SpanRunner
    {
        const int NUM_ITERS = 100_000;

        private volatile int _index = 7;

        // private static byte[] _bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        [GlobalSetup]
        public void Setup()
        {
            //Random rnd = new Random(0x12345);

            //_ints = new int[SpanLength];

            //for (int i = 0; i < _ints.Length; i++)
            //{
            //    _ints[i] = rnd.Next();
            //}
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private unsafe static Span<byte> GetSpan()
        {
            return new Span<byte>((void*)0xdeadbeef, int.MaxValue);
        }

        //[MethodImpl(MethodImplOptions.NoInlining)]
        //private static Span<byte> GetSpan2()
        //{
        //    return _bytes;
        //}

        //[Benchmark]
        //public int Slice_Int()
        //{
        //    Span<byte> theSpan = GetSpan().Slice(0);

        //    for (int i = NUM_ITERS; i > 0; i--)
        //    {
        //        theSpan = theSpan.Slice(_index);
        //    }

        //    return theSpan.Length;
        //}

        //[Benchmark]
        //public int Slice_IntInt()
        //{
        //    Span<byte> theSpan = GetSpan().Slice(0);
        //    int desiredLength = theSpan.Length - 10;

        //    for (int i = NUM_ITERS; i > 0; i--)
        //    {
        //        theSpan = theSpan.Slice(_index, desiredLength);
        //        desiredLength -= 10;
        //    }

        //    return theSpan.Length;
        //}

        private readonly byte[] _bytes = Encoding.UTF8.GetBytes("1 22 333 4444 55555 666666 7777777 1 22 333 4444 55555 666666 7777777 1 22 333 4444 55555 666666 7777777 1 22 333 4444 55555 666666 7777777 ");

        [Benchmark]
        public int Utf8Parser_Sum()
        {
            byte[] bytes = _bytes;
            _ = bytes.Length; // allow JIT to determine non-null
            int sum = default;

            for (int i = NUM_ITERS; i > 0; i--)
            {
                ReadOnlySpan<byte> copy = bytes;
                while (!copy.IsEmpty)
                {
                    Utf8Parser.TryParse(copy, out int value, out int bytesConsumed, 'N');
                    copy = copy.Slice(bytesConsumed);
                    copy = copy.Slice(1);
                    sum += value;
                }
            }

            return sum;
        }

        [Benchmark]
        public int CountSpaces()
        {
            byte[] bytes = _bytes;
            _ = bytes.Length; // allow JIT to determine non-null
            int count = default;

            for (int i = NUM_ITERS; i > 0; i--)
            {
                ReadOnlySpan<byte> copy = bytes;
                while (!copy.IsEmpty)
                {
                    int index = copy.IndexOf((byte)' ');
                    if (index < 0) { break; }

                    count++;
                    copy = copy.Slice(index + 1);
                }
            }

            return count;
        }

        //[Benchmark]
        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        //public int CountSpaces2()
        //{
        //    byte[] bytes = _bytes;
        //    _ = bytes.Length; // allow JIT to determine non-null
        //    int count = default;

        //    for (int i = NUM_ITERS; i > 0; i--)
        //    {
        //        ReadOnlySpan<byte> copy = bytes;
        //        while (!copy.IsEmpty)
        //        {
        //            int index = copy.IndexOf((byte)' ');
        //            if (index < 0) { break; }

        //            count++;
        //            copy = copy.Slice(index);
        //            copy = copy.Slice(1);
        //        }
        //    }

        //    return count;
        //}
    }
}
