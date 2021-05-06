// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Attributes;
using System.Buffers.Text;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace ConsoleAppBenchmark
{
    // [DisassemblyDiagnoser(maxDepth: 5)]
    public class Utf8ParserTests
    {
        public IEnumerable<object> Int64Values
            => Values.Select(v => v.ToString()).Select(formatted => new Utf8TestCase(formatted));

        public static IEnumerable<object> Values => new object[]
        {
            (long)0,
            (long)1,
            (long)-1,
            (long)12345,
            (long)-12345,
            (long)12345678901234567,
            (long)-12345678901234567,
            long.MaxValue,
            long.MinValue
        };

        [Benchmark]
        [ArgumentsSource(nameof(Int64Values))]
        public bool TryParseInt64(Utf8TestCase value) => Utf8Parser.TryParse(value.Utf8Bytes, out long _, out int _);

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
    }
}
