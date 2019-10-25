using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    public class EncodingRunner
    {
        private const string SampleTextsFolder = @"C:\Users\levib\source\repos\fast-utf8\FastUtf8Tester\SampleTexts\";

        private const int ITER_COUNT = 100;
        private byte[] _utf8Data;
        private string _utf16Data;

        // [Params("11.txt", "11-0.txt", "25249-0.txt", "30774-0.txt", "39251-0.txt")]
        // [Params("11-0.txt", "30774-0.txt", "39251-0.txt")]
        // [Params("11.txt")]
        // [Params("39251-0.txt")]
        // [Params("25249-0.txt")]
        public string Corpus;

        [Params("", "Hello", "Hello world!", "Γεια σου κόσμε", "Nǐhǎo 你好", "Lorem ipsum dolor sit amet, consectetur adipiscing elit.")]
        public string Text;

        //[Benchmark]
        //public int GetByteCount()
        //{
        //    string utf16Data = _utf16Data;
        //    _ = utf16Data.Length; // JIT null check

        //    int byteCount = 0;
        //    for (int i = ITER_COUNT; i > 0; i--)
        //    {
        //        byteCount = Encoding.UTF8.GetByteCount(utf16Data.AsSpan());
        //    }

        //    return byteCount;
        //}

        //[Benchmark]
        //public int GetBytes()
        //{
        //    string utf16Data = _utf16Data;
        //    _ = utf16Data.Length; // JIT null check

        //    int written = 0;
        //    for (int i = ITER_COUNT; i > 0; i--)
        //    {
        //        int length = Encoding.UTF8.GetByteCount(utf16Data.AsSpan());

        //        // Rent an array instead of allocating an array
        //        byte[] rented = ArrayPool<byte>.Shared.Rent(length);
        //        written = Encoding.UTF8.GetBytes(utf16Data, rented);
        //        ArrayPool<byte>.Shared.Return(rented);
        //    }

        //    return written;
        //}

        //[Benchmark]
        //public int GetCharCount()
        //{
        //    byte[] utf8Data = _utf8Data;
        //    _ = utf8Data.Length; // JIT null check

        //    int charCount = 0;
        //    for (int i = ITER_COUNT; i > 0; i--)
        //    {
        //        charCount = Encoding.UTF8.GetCharCount(utf8Data.AsSpan());
        //    }

        //    return charCount;
        //}

        //[Benchmark]
        //public int GetChars()
        //{
        //    byte[] utf8Data = _utf8Data;
        //    _ = utf8Data.Length; // JIT null check

        //    int written = 0;
        //    for (int i = ITER_COUNT; i > 0; i--)
        //    {
        //        int length = Encoding.UTF8.GetCharCount(utf8Data.AsSpan());

        //        // Rent an array instead of allocating an array
        //        char[] rented = ArrayPool<char>.Shared.Rent(length);
        //        written = Encoding.UTF8.GetChars(utf8Data, rented);
        //        ArrayPool<char>.Shared.Return(rented);
        //    }

        //    return written;
        //}

        //[Benchmark(Baseline = true)]
        //public int GetCharCount_Old()
        //{
        //    byte[] utf8Data = _utf8Data;
        //    var unused = utf8Data.Length;

        //    int retVal = 0;
        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        retVal = Encoding.UTF8.GetCharCount(utf8Data);
        //    }

        //    return retVal;
        //}

        //[Benchmark(Baseline = true, Description = "GetBytes (old)")]
        //public int ToBytes_Old()
        //{
        //    string utf16Data = _utf16Data;
        //    var utf16Length = utf16Data.Length;

        //    byte[] utf8Data = ArrayPool<byte>.Shared.Rent(utf16Length * 3);
        //    _ = utf8Data.Length;

        //    int byteCount = 0;
        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        byteCount = Encoding.UTF8.GetBytes(utf16Data, utf8Data);
        //    }

        //    ArrayPool<byte>.Shared.Return(utf8Data);

        //    return byteCount;
        //}

        //[Benchmark(Description = "GetBytes (new)")]
        //public int ToBytes_New()
        //{
        //    string utf16Data = _utf16Data;
        //    var utf16Length = utf16Data.Length;

        //    byte[] utf8Data = ArrayPool<byte>.Shared.Rent(utf16Length * 3);
        //    _ = utf8Data.Length;

        //    int byteCount = 0;
        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        Utf8.ToBytes(utf16Data, utf8Data, false, false, out _, out byteCount);
        //    }

        //    ArrayPool<byte>.Shared.Return(utf8Data);

        //    return byteCount;
        //}

        //[Benchmark(Baseline = true, Description = "GetChars (old)")]
        //public int ToChars_Old()
        //{
        //    byte[] utf8Data = _utf8Data;
        //    var utf8Length = utf8Data.Length;

        //    char[] utf16Data = ArrayPool<char>.Shared.Rent(utf8Length );
        //    _ = utf16Data.Length;

        //    int charCount = 0;
        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        charCount = Encoding.UTF8.GetChars(utf8Data, utf16Data);
        //    }

        //    ArrayPool<char>.Shared.Return(utf16Data);

        //    return charCount;
        //}

        //[Benchmark(Description = "GetChars (new)")]
        //public int ToChars_New()
        //{
        //    byte[] utf8Data = _utf8Data;
        //    var utf8Length = utf8Data.Length;

        //    char[] utf16Data = ArrayPool<char>.Shared.Rent(utf8Length );
        //    _ = utf16Data.Length;

        //    int charCount = 0;
        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        Utf8.ToChars(utf8Data, utf16Data, false, false, out _, out charCount);
        //    }

        //    ArrayPool<char>.Shared.Return(utf16Data);

        //    return charCount;
        //}

        [Benchmark]
        public string GetString_FromByteArray()
        {
            return Encoding.UTF8.GetString(_utf8Data);
        }

        [Benchmark]
        public byte[] GetByteArray_FromString()
        {
            return Encoding.UTF8.GetBytes(Text);
        }

        [GlobalSetup]
        public void Setup()
        {
            //_utf8Data = File.ReadAllBytes(SampleTextsFolder + @"\" + Corpus);

            //// strip off UTF-8 BOM if it exists
            //if (_utf8Data.Length > 3 && _utf8Data.AsSpan(0, 3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }))
            //{
            //    _utf8Data = _utf8Data.AsSpan(3).ToArray();
            //}

            //_utf16Data = Encoding.UTF8.GetString(_utf8Data);

            // _utf8Data = new byte[32];
            _utf8Data = Encoding.UTF8.GetBytes(Text);
        }
    }
}
