using BenchmarkDotNet.Attributes;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ConsoleAppBenchmark
{
    public class Utf8ParserRunner
    {
        private byte[] _dateTimeBytes = Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N"));
        private byte[] _guidBytes = Encoding.ASCII.GetBytes("700bf1eb180e4ef49c2ba41b49276982");
        private byte[][] _strings;
        private byte[][] _intStrings;
        private string[] _intStringsUtf16;

        [GlobalSetup]
        public void Setup()
        {
            Random rnd = new Random(0x12345);

            byte[][] strings = new byte[200][];

            for (int i = 0; i < strings.Length; i++)
            {
                strings[i] = Encoding.ASCII.GetBytes((rnd.Next(0, 10)) switch
                {
                    0 => "true",
                    1 => "xtruex",
                    2 => "xtrue",
                    3 => "tru",
                    4 => "truex",
                    5 => "xtruex",
                    6 => "xfalsex",
                    7 => "fals",
                    8 => "false",
                    9 => "falsf",
                });
            }

            _strings = strings;

            byte[][] intStrings = new byte[200][];

            for (int i = 0; i < intStrings.Length; i++)
            {
                byte[] thisIntString = new byte[rnd.Next(1, 9)];

                for (int j = 0; j < thisIntString.Length; j++)
                {
                    thisIntString[j] = (byte)rnd.Next('0', '9' + 1);
                }

                intStrings[i] = thisIntString;
            }

            _intStrings = intStrings;

            _intStringsUtf16 = _intStrings.Select(Encoding.ASCII.GetString).ToArray();
        }

        //[Benchmark]
        //public void ParseBoolean()
        //{
        //    var strings = _strings;

        //    for (int i = 0; i < strings.Length; i++)
        //    {
        //        var s = strings[i];
        //        _ = s.Length; // assume not null

        //        Utf8Parser.TryParse(s, out bool value, out _);
        //    }
        //}

        private readonly byte[] _guidDefault = Encoding.ASCII.GetBytes("22628836-3fab-44cd-9ee2-067265a0c7e7");
        private readonly byte[] _guidBraces = Encoding.ASCII.GetBytes("{22628836-3fab-44cd-9ee2-067265a0c7e7}");

        //[Benchmark]
        //public bool ParseGuid_Default()
        //{
        //    byte[] bytes = _guidDefault;
        //    _ = bytes.Length; // suppress null check
        //    ReadOnlySpan<byte> span = bytes;

        //    bool retVal = default;
        //    for (int i = 0; i < 100; i++)
        //    {
        //        retVal = Utf8Parser.TryParse(bytes, out Guid value, out int bytesConsumed);
        //    }

        //    return retVal;
        //}

        //[Benchmark]
        //public bool ParseGuid_Braces()
        //{
        //    byte[] bytes = _guidBraces;
        //    _ = bytes.Length; // suppress null check
        //    ReadOnlySpan<byte> span = bytes;

        //    bool retVal = default;
        //    for (int i = 0; i < 100; i++)
        //    {
        //        retVal = Utf8Parser.TryParse(bytes, out Guid value, out int bytesConsumed, 'B');
        //    }

        //    return retVal;
        //}

        //[Benchmark]
        //public bool TryParseDateTime()
        //{
        //    byte[] bytes = _dateTimeBytes;
        //    _ = bytes.Length; // elide null check

        //    return Utf8Parser.TryParse(bytes, out DateTime _, out _);
        //}

        //[Benchmark]
        //public bool TryParseGuid()
        //{
        //    byte[] bytes = _guidBytes;
        //    _ = bytes.Length; // elide null check

        //    return Utf8Parser.TryParse(bytes, out Guid _, out _, 'N');
        //}

        //[Benchmark]
        //[ArgumentsSource(nameof(Int32Values))]
        //public bool TryParseInt32(Utf8TestCase value) => Utf8Parser.TryParse(value.Utf8Bytes, out int _, out int _);

        [Benchmark]
        public bool TryParseInt32()
        {
            bool retVal = false;

            byte[][] strs = _intStrings;
            for (int i = 0; i < strs.Length; i++)
            {
                byte[] str = strs[i];
                _ = str.Length;

                retVal = Utf8Parser.TryParse(str, out int _, out int _);
            }

            return retVal;
        }

        [Benchmark]
        public int Int32Parse()
        {
            int retVal = default;

            string[] strs = _intStringsUtf16;
            for (int i = 0; i < strs.Length; i++)
            {
                string str = strs[i];
                retVal = int.Parse(str, CultureInfo.InvariantCulture);
            }

            return retVal;
        }

        public IEnumerable<object> Int32Values
            => Perf_Int32.StringValuesDecimal.OfType<string>().Select(formatted => new Utf8TestCase(formatted));

        public class Utf8TestCase
        {
            public byte[] Utf8Bytes { get; }
            private string Text { get; }

            public Utf8TestCase(string text)
            {
                Text = text;
                Utf8Bytes = Encoding.UTF8.GetBytes(Text);
            }

            public override string ToString() => Text; // displayed by BDN
        }

        public class Perf_Int32
        {
            private char[] _destination = new char[int.MinValue.ToString().Length];

            public static IEnumerable<object> Values => new object[]
            {
            int.MinValue,
            4, // single digit
            (int)12345, // same value used by other tests to compare the perf
            int.MaxValue
            };

            public static IEnumerable<object> StringValuesDecimal => Values.Select(value => value.ToString()).ToArray();
            public static IEnumerable<object> StringValuesHex => Values.Select(value => ((int)value).ToString("X")).ToArray();

            [Benchmark]
            [ArgumentsSource(nameof(Values))]
            public string ToString(int value) => value.ToString();

            [Benchmark]
            [ArgumentsSource(nameof(Values))]
            public string ToStringHex(int value) => value.ToString("X");

            [Benchmark]
            [ArgumentsSource(nameof(StringValuesDecimal))]
            public int Parse(string value) => int.Parse(value);

            [Benchmark]
            [ArgumentsSource(nameof(StringValuesHex))]
            public int ParseHex(string value) => int.Parse(value, NumberStyles.HexNumber);

            [Benchmark]
            [ArgumentsSource(nameof(StringValuesDecimal))]
            public bool TryParse(string value) => int.TryParse(value, out _);

#if !NETFRAMEWORK // API added in .NET Core 2.1
            [Benchmark]
            [ArgumentsSource(nameof(StringValuesDecimal))]
            public int ParseSpan(string value) => int.Parse(value.AsSpan());

            [Benchmark]
            [ArgumentsSource(nameof(Values))]
            public bool TryFormat(int value) => value.TryFormat(new Span<char>(_destination), out _);

            [Benchmark]
            [ArgumentsSource(nameof(StringValuesDecimal))]
            public bool TryParseSpan(string value) => int.TryParse(value.AsSpan(), out _);
#endif
        }
    }
}
