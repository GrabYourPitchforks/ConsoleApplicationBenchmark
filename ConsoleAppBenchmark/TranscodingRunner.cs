using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Unicode;

namespace ConsoleAppBenchmark
{
    public class TranscodingRunner
    {
        private const string SampleTextsFolder = @"C:\Users\levib\source\repos\fast-utf8\FastUtf8Tester\SampleTexts\";

        private byte[] _dataAsUtf8;
        private char[] _dataAsUtf16;

        [Params("11.txt", "11-0.txt", "25249-0.txt", "30774-0.txt", "39251-0.txt")]
        public string Corpus;

        [GlobalSetup]
        public void Setup()
        {
            _dataAsUtf8 = File.ReadAllBytes(SampleTextsFolder + Corpus);

            if (_dataAsUtf8.AsSpan().StartsWith(Encoding.UTF8.Preamble))
            {
                _dataAsUtf8 = _dataAsUtf8[Encoding.UTF8.Preamble.Length..];
            }

            _dataAsUtf16 = Encoding.UTF8.GetChars(_dataAsUtf8);

            //byte[] utf8Bytes = Encoding.UTF8.GetBytes(_dataAsUtf16);
            //if (_dataAsUtf8.Length != utf8Bytes.Length)
            //{
            //    throw new Exception("Length difference!");
            //}

            //for (int i = 0; i < utf8Bytes.Length; i++)
            //{
            //    if (utf8Bytes[i] != _dataAsUtf8[i])
            //    {
            //        throw new Exception($"Different in position {i} of {utf8Bytes.Length}: {GetDiff(_dataAsUtf8, i)} || {GetDiff(utf8Bytes, i)}");
            //    }
            //}
        }

        private static string GetDiff(byte[] bytes, int index)
        {
            return BitConverter.ToString(bytes[(index - 20)..(index + 20)]);
        }

        [Benchmark]
        public void TranscodeUtf16ToUtf8()
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(_dataAsUtf8.Length);
            Encoding.UTF8.GetBytes(_dataAsUtf16, rented);
            ArrayPool<byte>.Shared.Return(rented);
        }

        [Benchmark]
        public void TranscodeUtf8ToUtf16()
        {
            char[] rented = ArrayPool<char>.Shared.Rent(_dataAsUtf16.Length);
            Encoding.UTF8.GetChars(_dataAsUtf8, rented);
            ArrayPool<char>.Shared.Return(rented);
        }
    }
}
