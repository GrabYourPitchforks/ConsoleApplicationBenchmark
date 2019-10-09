using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Linq;
using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;

namespace ConsoleAppBenchmark
{
    public class AsciiRunner
    {
        private const int ITER_COUNT = 100;

        private byte[] _bytesAllAscii;
        private string _strAllAscii;
        private string _strWithNonAsciiData;
        private Encoder _encoder;
        private readonly ASCIIEncoding _asciiEncodingErrorFallback = (ASCIIEncoding)Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

        private byte[][] _bytesOfBytesAllAscii;

        // [Params(0, 1, 2, 3, 4, 8, 12, 16, 32, 64, 128)]
        // [Params(0, 4, 14, 29)]
        // [Params(256)]
        // public int StringLength;

        [Params("1 - 6", "6 - 16", "12 - 24", "20 - 48", "32 - 256")]
        public string StringLength;

        [GlobalSetup]
        public void Setup()
        {
            //_strAllAscii = new string('x', StringLength);
            //_bytesAllAscii = Enumerable.Repeat((byte)'x', StringLength).ToArray();

            //_strWithNonAsciiData = _strAllAscii + "é" + _strAllAscii + "\U0010FFFF" + _strAllAscii;
            //_encoder = Encoding.ASCII.GetEncoder();

            string[] split = StringLength.Split('-');
            int minInclusive = int.Parse(split[0]);
            int maxInclusive = int.Parse(split[1]);

            Random r = new Random(0x12345);

            _bytesOfBytesAllAscii = new byte[32][];

            for (int i = 0; i < _bytesOfBytesAllAscii.Length; i++)
            {
                _bytesOfBytesAllAscii[i] = Encoding.ASCII.GetBytes(new string('x', r.Next(minInclusive, maxInclusive + 1)));
            }
        }

        //[Benchmark]
        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        //public int GetBytes()
        //{
        //    ReadOnlySpan<char> chars = _strAllAscii;
        //    Span<byte> bytes = stackalloc byte[256];

        //    int byteCount = 0;
        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        byteCount = Encoding.ASCII.GetBytes(chars, bytes);
        //    }

        //    return byteCount;
        //}

        //[Benchmark]
        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        //public int GetChars()
        //{
        //    //ReadOnlySpan<byte> bytes = _bytesAllAscii;
        //    //Span<char> chars = stackalloc char[256];

        //    //int byteCount = 0;
        //    //for (int i = 0; i < ITER_COUNT; i++)
        //    //{
        //    //    byteCount = Encoding.ASCII.GetChars(bytes, chars);
        //    //}

        //    //return byteCount;

        //    byte[] bytes = new byte[1024];
        //    MethodA(bytes);
        //    MethodB(bytes);

        //    return bytes[0];
        //}

        [Benchmark]
        public int GetCharCount()
        {
            int retVal = 0;
            foreach (byte[] bytes in _bytesOfBytesAllAscii)
            {
                retVal += _asciiEncodingErrorFallback.GetCharCount(bytes);
            }

            return retVal;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        public static unsafe int MethodA(ReadOnlySpan<byte> span)
        {
            fixed (byte* ptr = &MemoryMarshal.GetReference(span))
            {
                return DoSomething(ptr, span.Length);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        public static unsafe int MethodB(ReadOnlySpan<byte> span)
        {
            fixed (byte* ptr = &MemoryMarshal.GetReference(span))
            {
                return DoSomething((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span)), span.Length);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static unsafe int DoSomething(byte* pBytes, int theInt)
        {
            return theInt;
        }

        //[Benchmark]
        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        //public int GetBytes_WithFallback()
        //{
        //    ReadOnlySpan<char> chars = _strWithNonAsciiData;
        //    Span<byte> bytes = stackalloc byte[512];

        //    int byteCount = 0;
        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        byteCount = Encoding.ASCII.GetBytes(chars, bytes);
        //    }

        //    return byteCount;
        //}

        //[Benchmark]
        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        //public int GetBytes_EncoderNLS()
        //{
        //    ReadOnlySpan<char> chars = _strAllAscii;
        //    Span<byte> bytes = stackalloc byte[256];

        //    int byteCount = 0;
        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        byteCount = _encoder.GetBytes(chars, bytes, flush: true);
        //    }

        //    return byteCount;
        //}

        //[Benchmark]
        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        //public int GetBytes_EncoderNLS_WithFallback()
        //{
        //    ReadOnlySpan<char> chars = _strWithNonAsciiData;
        //    Span<byte> bytes = stackalloc byte[512];

        //    int byteCount = 0;
        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        byteCount = _encoder.GetBytes(chars, bytes, flush: true);
        //    }

        //    return byteCount;
        //}

        //[Benchmark]
        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        //public int GetByteCount()
        //{
        //    string str = _strAllAscii;
        //    _ = str.Length; // null check

        //    var encoding = _asciiEncodingErrorFallback;


        //    int byteCount = 0;
        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        byteCount = encoding.GetByteCount(str);
        //    }

        //    return byteCount;
        //}

        //[Benchmark]
        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        //public int GetCharCount()
        //{
        //    byte[] bytes = _bytesAllAscii;
        //    _ = bytes.Length; // null check

        //    var encoding = _asciiEncodingErrorFallback;

        //    int charCount = 0;
        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        charCount = encoding.GetCharCount(bytes);
        //    }

        //    return charCount;
        //}
    }
}
