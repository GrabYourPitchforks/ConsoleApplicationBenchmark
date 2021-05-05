using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;

namespace ConsoleAppBenchmark
{
    [SkipLocalsInit]
    public class TextEncoderRunner
    {
        [Params(
            //"The quick brown fox jumps over the lazy dog.", // no escaping needed ever
            //"<div id=\"myDiv\">Escape &amp; me!</div>", // contains some HTML / URL / JSON-sensitive chars
            "Лорем ипсум долор сит амет, цоммуне малуиссет цонцлудатуряуе ад хис.", // Cyrillic lipsum; no escaping needed (when Cyrillic allowed)
            "選出相併整科試学上注改岡固報波材活益覚渡。販良属上二合属渡海際約保殺。集記一美春線報初費表化韓購予号。" // Chinese lipsum
            )] 
        public string Arg { get; set; }
        private byte[] _argUtf8;
        private char[] _scratchBuffer = new char[1024];
        private byte[] _scratchUtf8Buffer = new byte[1024];

        // [Params("HTML", "URL", "JSON-Default", "JSON-Relaxed")]
        [Params("JSON-Relaxed")]
        public string Encoder { get; set; }
        private TextEncoder _encoder;

        [GlobalSetup]
        public void Setup()
        {
            _argUtf8 = Encoding.UTF8.GetBytes(Arg);
            _encoder = Encoder switch
            {
                "HTML" => HtmlEncoder.Default,
                "URL" => UrlEncoder.Default,
                "JSON-Default" => JavaScriptEncoder.Default,
                "JSON-Relaxed" => JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                _ => throw new Exception("Unknown encoder."),
            };
        }

        //[Benchmark]
        //public unsafe int FindFirstCharToEncodeUtf16()
        //{
        //    string arg = Arg;
        //    _ = arg.Length; // deref; prove not null

        //    fixed (char* pArg = arg)
        //    {
        //        return _encoder.FindFirstCharacterToEncode(pArg, arg.Length);
        //    }
        //}

        [Benchmark]
        public int FindFirstCharToEncodeUtf8()
        {
            byte[] argUtf8 = _argUtf8;
            _ = argUtf8.Length; // deref; prove not null
            return _encoder.FindFirstCharacterToEncodeUtf8(argUtf8);
        }

        //[Benchmark]
        //public string EncodeToStringUtf16()
        //{
        //    return _encoder.Encode(Arg);
        //}

        //[Benchmark]
        //public OperationStatus EncodeToBufferUtf16()
        //{
        //    string arg = Arg;
        //    _ = arg.Length; // deref; prove not null

        //    char[] dest = _scratchBuffer;
        //    _ = dest.Length; // deref; prove not null

        //    return _encoder.Encode(arg, dest, out _, out _);
        //}

        [Benchmark]
        public OperationStatus EncodeToBufferUtf8()
        {
            byte[] argUtf8 = _argUtf8;
            _ = argUtf8.Length; // deref; prove not null

            byte[] dest = _scratchUtf8Buffer;
            _ = dest.Length; // deref; prove not null

            return _encoder.EncodeUtf8(argUtf8, dest, out _, out _);
        }
    }
}
