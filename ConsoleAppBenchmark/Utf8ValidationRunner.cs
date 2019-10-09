using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    public class Utf8ValidationRunner
    {
        private const string SampleTextsFolder = @"C:\Users\levib\source\repos\fast-utf8\FastUtf8Tester\SampleTexts\";

        private byte[] _utf8Data;

        [Params("11.txt", "11-0.txt", "25249-0.txt", "30774-0.txt", "39251-0.txt")]
        public string Corpus;

        [GlobalSetup]
        public void Setup()
        {
            _utf8Data = File.ReadAllBytes(SampleTextsFolder + Corpus);
        }

        //[Benchmark(Baseline = true)]
        //public int WithoutSimd()
        //{
        //    return Encoding.UTF8.GetCharCount(_utf8Data);
        //}

        [Benchmark(Baseline = true)]
        public int WithSimd1()
        {
            return Utf8Logic.GetIndexOfFirstInvalidUtf8Byte(ref _utf8Data[0], _utf8Data.Length);
        }

        //[Benchmark(Baseline = false)]
        //public int WithSimd2()
        //{
        //    return Utf8Logic2.GetIndexOfFirstInvalidUtf8Byte(ref _utf8Data[0], _utf8Data.Length);
        //}
    }
}
