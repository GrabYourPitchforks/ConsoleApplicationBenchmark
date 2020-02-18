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
    [DisassemblyDiagnoser]
    public class StringRunner
    {
        //[Benchmark]
        //public int TestNullableInt() => Array.Empty<int?>().AsSpan().Length;

[Benchmark]
[Arguments("hello")]
[Arguments("XYZ")]
public string ToUpperInvariant(string s) => s.ToUpperInvariant();
    }
}
