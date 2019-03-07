using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CoreRun;

namespace ConsoleAppBenchmark
{
    class Program
    {
        public class LocalCoreClrConfig : ManualConfig
        {
            public LocalCoreClrConfig()
            {
                AddCustom30Toolchain(
                    coreRunDirectory: @"C:\Users\levib\Desktop\experiments\control_1382754a",
                    displayName: "baseline",
                    isBaseline: true);

                AddCustom30Toolchain(
                    coreRunDirectory: @"C:\Users\levib\Desktop\experiments\var_432173f1",
                    displayName: "exp-A");

                Add(DefaultConfig.Instance.GetExporters().ToArray());
                Add(DefaultConfig.Instance.GetLoggers().ToArray());
                Add(DefaultConfig.Instance.GetColumnProviders().ToArray());

                // Add(DisassemblyDiagnoser.Create(new DisassemblyDiagnoserConfig(printAsm: true, recursiveDepth: 2)));
            }

            private void AddCustom30Toolchain(string coreRunDirectory, string displayName, bool isBaseline = false, Dictionary<string, string> envVars = default)
            {
                var toolchain = new CoreRunToolchain(
                    coreRun: new DirectoryInfo(coreRunDirectory).GetFiles("CoreRun.exe").Single(),
                    targetFrameworkMoniker: "netcoreapp3.0",
                    displayName: displayName);

                var job = Job.Default.With(toolchain);

                if (isBaseline)
                {
                    job = job.AsBaseline();
                }

                if (envVars != null)
                {
                    foreach (var (key, value) in envVars)
                    {
                        job = job.With(new[] { new EnvironmentVariable(key, value) });
                    }
                }

                Add(job);
            }

            private void AddCustom30Toolchain(string coreRunDirectory, string displayName, params string[] isasToSuppress)
            {
                Dictionary<string, string> envVars = new Dictionary<string, string>();
                foreach (string isa in isasToSuppress)
                {
                    envVars["COMPLUS_ENABLE" + isa.ToUpperInvariant()] = "0";
                }

                AddCustom30Toolchain(coreRunDirectory, displayName, envVars: envVars);
            }
        }

        static void Main(string[] args)
        {
            // var summary = BenchmarkRunner.Run<Runner>(new LocalCoreClrConfig());
            var summary = BenchmarkRunner.Run<AsciiRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<Runner>();
            // var summary = BenchmarkRunner.Run<CharRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<HashRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<MemoryRunner>(new LocalCoreClrConfig());

            //var runner = new JsonRunner();
            //runner.Setup();
            //runner.WithVector();

            // var summary = BenchmarkRunner.Run<JsonRunner>(new LocalCoreClrConfig());
        }
    }

    public class HashRunner
    {
        const int ITER_COUNT = 100_000;

        //private string _strData;

        //[Params(0, 1, 3, 7, 16, 21, 32, 128)]
        //public int InputLength;

        //[GlobalSetup]
        //public void Setup()
        //{
        //    _strData = new string('\u927f', InputLength);
        //}

        //[Benchmark]
        //public int StringGetHashcode()
        //{
        //    int retVal = default;
        //    string data = _strData;
        //    _ = data.Length;

        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        retVal = data.GetHashCode();
        //    }

        //    return retVal;
        //}

        [Params("0 - 8", "6 - 24", "20 - 96", "64 - 256")]
        public string InputLength;

        private string[] _strings;

        [GlobalSetup]
        public void Setup()
        {
            Random r = new Random(0x12345);

            string[] inputLengths = InputLength.Split(" - ");
            int minInclusive = int.Parse(inputLengths[0]);
            int maxExclusive = int.Parse(inputLengths[1]) + 1;

            string[] strings = new string[50_000];
            for (int i = 0; i < strings.Length; i++)
            {
                strings[i] = new string('\u1234', r.Next(minInclusive, maxExclusive));
            }

            _strings = strings;
        }

        [Benchmark]
        public int StringGetHashcode()
        {
            int retVal = default;

            string[] strings = _strings;
            for (int i = 0; i < strings.Length; i++)
            {
                retVal ^= strings[i].GetHashCode();
            }

            return retVal;
        }
    }

    public class CharRunner
    {
        private const int ITER_COUNT = 1_000_000;

        private char _ca;
        private char _cb;

        [GlobalSetup]
        public void Setup()
        {
            _ca = '\ud800';
            _cb = '\udfff';
        }

        [Benchmark]
        public int ConvertToInt32()
        {
            int retVal = default;
            for (int i = 0; i < ITER_COUNT; i++)
            {
                retVal = char.ConvertToUtf32(_ca, _cb);
            }
            return retVal;
        }

        //[Benchmark]
        //public bool IsSurrogatePair()
        //{
        //    bool retVal = default;
        //    for (int i = 0; i < ITER_COUNT; i++)
        //    {
        //        retVal = char.IsSurrogatePair(_ca, _cb);
        //    }
        //    return retVal;
        //}
    }

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

        [Benchmark]
        public int GetSpan_MemOfCharsFromArray()
        {
            return DoTest(_romCharsFromArray);
        }

        [Benchmark]
        public int GetSpan_MemOfCharsFromString()
        {
            return DoTest(_romCharsFromString);
        }

        [Benchmark]
        public int GetSpan_MemOfCharsFromMemMgr()
        {
            return DoTest(_romCharsFromMM);
        }

        [Benchmark]
        public int GetSpan_MemOfCharsEmpty()
        {
            return DoTest(_romCharsEmpty);
        }

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

    //public class Runner
    //{
    //    private const string SampleTextsFolder = @"C:\Users\levib\source\repos\fast-utf8\FastUtf8Tester\SampleTexts\";

    //    private const int ITER_COUNT = 100;
    //    private byte[] _utf8Data;
    //    private string _utf16Data;

    //    //[Params("11.txt", "11-0.txt", "25249-0.txt", "30774-0.txt", "39251-0.txt")]
    //    [Params( "30774-0.txt", "39251-0.txt")]
    //    public string Corpus;

    //    //[Benchmark(Baseline = true)]
    //    //public int GetCharCount_Old()
    //    //{
    //    //    byte[] utf8Data = _utf8Data;
    //    //    var unused = utf8Data.Length;

    //    //    int retVal = 0;
    //    //    for (int i = 0; i < ITER_COUNT; i++)
    //    //    {
    //    //        retVal = Encoding.UTF8.GetCharCount(utf8Data);
    //    //    }

    //    //    return retVal;
    //    //}

    //    //[Benchmark]
    //    //public int GetCharCount_New()
    //    //{
    //    //    byte[] utf8Data = _utf8Data;
    //    //    var unused = utf8Data.Length;

    //    //    int retVal = 0;
    //    //    for (int i = 0; i < ITER_COUNT; i++)
    //    //    {
    //    //        Utf8.GetIndexOfFirstInvalidByte(utf8Data, out retVal, out _);
    //    //    }

    //    //    return retVal;
    //    //}

    //    [Benchmark(Baseline = true, Description = "GetBytes (old)")]
    //    public int ToBytes_Old()
    //    {
    //        string utf16Data = _utf16Data;
    //        var utf16Length = utf16Data.Length;

    //        byte[] utf8Data = ArrayPool<byte>.Shared.Rent(utf16Length * 3);
    //        _ = utf8Data.Length;

    //        int byteCount = 0;
    //        for (int i = 0; i < ITER_COUNT; i++)
    //        {
    //            byteCount = Encoding.UTF8.GetBytes(utf16Data, utf8Data);
    //        }

    //        ArrayPool<byte>.Shared.Return(utf8Data);

    //        return byteCount;
    //    }

    //    [Benchmark(Description = "GetBytes (new)")]
    //    public int ToBytes_New()
    //    {
    //        string utf16Data = _utf16Data;
    //        var utf16Length = utf16Data.Length;

    //        byte[] utf8Data = ArrayPool<byte>.Shared.Rent(utf16Length * 3);
    //        _ = utf8Data.Length;

    //        int byteCount = 0;
    //        for (int i = 0; i < ITER_COUNT; i++)
    //        {
    //            Utf8.ToBytes(utf16Data, utf8Data, false, false, out _, out byteCount);
    //        }

    //        ArrayPool<byte>.Shared.Return(utf8Data);

    //        return byteCount;
    //    }

    //    //[Benchmark(Baseline = true, Description = "GetChars (old)")]
    //    //public int ToChars_Old()
    //    //{
    //    //    byte[] utf8Data = _utf8Data;
    //    //    var utf8Length = utf8Data.Length;

    //    //    char[] utf16Data = ArrayPool<char>.Shared.Rent(utf8Length );
    //    //    _ = utf16Data.Length;

    //    //    int charCount = 0;
    //    //    for (int i = 0; i < ITER_COUNT; i++)
    //    //    {
    //    //        charCount = Encoding.UTF8.GetChars(utf8Data, utf16Data);
    //    //    }

    //    //    ArrayPool<char>.Shared.Return(utf16Data);

    //    //    return charCount;
    //    //}

    //    //[Benchmark(Description = "GetChars (new)")]
    //    //public int ToChars_New()
    //    //{
    //    //    byte[] utf8Data = _utf8Data;
    //    //    var utf8Length = utf8Data.Length;

    //    //    char[] utf16Data = ArrayPool<char>.Shared.Rent(utf8Length );
    //    //    _ = utf16Data.Length;

    //    //    int charCount = 0;
    //    //    for (int i = 0; i < ITER_COUNT; i++)
    //    //    {
    //    //        Utf8.ToChars(utf8Data, utf16Data, false, false, out _, out charCount);
    //    //    }

    //    //    ArrayPool<char>.Shared.Return(utf16Data);

    //    //    return charCount;
    //    //}

    //    [GlobalSetup]
    //    public void Setup()
    //    {
    //        _utf8Data = File.ReadAllBytes(SampleTextsFolder + @"\" + Corpus);

    //        // strip off UTF-8 BOM if it exists
    //        if (_utf8Data.Length > 3 && _utf8Data.AsSpan(0, 3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }))
    //        {
    //            _utf8Data = _utf8Data.AsSpan(3).ToArray();
    //        }

    //        _utf16Data = Encoding.UTF8.GetString(_utf8Data);
    //    }
    //}
}
