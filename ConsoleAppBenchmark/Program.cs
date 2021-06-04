using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
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
                AddCustom50Toolchain(
                    coreRunDirectory: @"C:\Users\levib\Desktop\experiments\main",
                    displayName: "main",
                    isBaseline: true);

                AddCustom50Toolchain(
                    coreRunDirectory: @"C:\Users\levib\Desktop\experiments\idxofany",
                    displayName: "idxofany",
                    isBaseline: false);

                AddExporter(DefaultConfig.Instance.GetExporters().ToArray());
                AddLogger(DefaultConfig.Instance.GetLoggers().ToArray());
                AddColumnProvider(DefaultConfig.Instance.GetColumnProviders().ToArray());

                // Add(DisassemblyDiagnoser.Create(new DisassemblyDiagnoserConfig(printAsm: true, recursiveDepth: 2)));
            }

            private void AddCustom50Toolchain(string coreRunDirectory, string displayName, bool enableTieredCompilation = true, bool isBaseline = false, Dictionary<string, string> envVars = default)
            {
                var toolchain = new CoreRunToolchain(
                    coreRun: new DirectoryInfo(coreRunDirectory).GetFiles("CoreRun.exe").Single(),
                    targetFrameworkMoniker: "netcoreapp5.0",
                    displayName: displayName);

                var job = Job.ShortRun.With(toolchain);
                // var job = Job.Default.WithToolchain(toolchain);

                if (isBaseline)
                {
                    job = job.AsBaseline();
                }

                if (!enableTieredCompilation)
                {
                    envVars ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    envVars["COMPLUS_TieredCompilation"] = "0";
                }

                if (envVars != null)
                {
                    foreach (var (key, value) in envVars)
                    {
                        job = job.WithEnvironmentVariable(key, value);
                    }
                }

                // job = job.With(new[] { new EnvironmentVariable("COMPLUS_TieredCompilation", "0") });

                // job = job.WithWarmupCount(10);

                AddJob(job);
            }

            private void AddCustom60Toolchain(string coreRunDirectory, string displayName, bool enableTieredCompilation = true, bool isBaseline = false, Dictionary<string, string> envVars = default)
            {
                var toolchain = new CoreRunToolchain(
                    coreRun: new DirectoryInfo(coreRunDirectory).GetFiles("CoreRun.exe").Single(),
                    targetFrameworkMoniker: "net6.0",
                    displayName: displayName);

                // var job = Job.ShortRun.With(toolchain);
                var job = Job.Default.WithToolchain(toolchain);
                // var job = Job.LongRun.With(toolchain);

                if (isBaseline)
                {
                    job = job.AsBaseline();
                }

                if (!enableTieredCompilation)
                {
                    envVars ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    envVars["COMPLUS_TieredCompilation"] = "0";
                }

                if (envVars != null)
                {
                    foreach (var (key, value) in envVars)
                    {
                        job = job.WithEnvironmentVariable(key, value);
                    }
                }

                // job = job.With(new[] { new EnvironmentVariable("COMPLUS_TieredCompilation", "0") });

                // job = job.WithWarmupCount(10);

                AddJob(job);
            }

            private void AddCustom50Toolchain(string coreRunDirectory, string displayName, bool isBaseline = false, params string[] isasToSuppress)
            {
                Dictionary<string, string> envVars = new Dictionary<string, string>();
                foreach (string isa in isasToSuppress)
                {
                    envVars["COMPLUS_ENABLE" + isa.ToUpperInvariant()] = "0";
                }

                AddCustom50Toolchain(coreRunDirectory, displayName, isBaseline: isBaseline, envVars: envVars);
            }
        }

        private static void Run<T>()
        {
            BenchmarkRunner.Run<T>(new LocalCoreClrConfig());
        }

        static void Main(string[] args)
        {
            // Run<IndexOfAnyRunner<byte>>();
            Run<IndexOfAnyRunner<char>>();
            // Run<IndexOfAnyRunner<int>>();

            // Run<SpanClearRunner>();
            // BenchmarkRunner.Run<GuidRunner>();

            //Utf8StringRunner runner = new Utf8StringRunner();
            //runner.Corpus = "25249-0.txt";
            //runner.Setup();
            //while (true)
            //{
            //    runner.Validate_Exp1();
            //}

            //EncodingRunner runner = new EncodingRunner();
            //runner.Setup();
            //while (true)
            //{
            //    runner.GetString_FromByteArray();
            //}

            // BenchmarkRunner.Run<Utf8ParserTests>(new LocalCoreClrConfig());
            // BenchmarkRunner.Run<ArrayRunner>(new LocalCoreClrConfig());
            // BenchmarkRunner.Run<SpanClearRunner>(new LocalCoreClrConfig());
            // BenchmarkRunner.Run<TextEncoderRunner>(new LocalCoreClrConfig());
            // BenchmarkRunner.Run<TwitterJsonRunner>(new LocalCoreClrConfig());
            // BenchmarkRunner.Run<SpanFwdRunner>(new LocalCoreClrConfig());
            // BenchmarkRunner.Run<Utf8Scenarios>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<ActivatorRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<Sha1Runner>(new LocalCoreClrConfig());
            // BenchmarkRunner.Run<BinaryWriterRunner>(new LocalCoreClrConfig());
            // BenchmarkRunner.Run<BinaryWriterRunner_Extended>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<MethodInfoRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<TypeCmpRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<StrCpyRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<ArrayRunner>(new LocalCoreClrConfig());
            // BenchmarkSwitcher.FromTypes(new[] { typeof(SpanFillRunner<>) }).RunAll(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run(typeof(SpanFillRunner<>), new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<SpanTrimRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<Utf8ValidationRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<SliceRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<CharRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<ArrayRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<ActivatorRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<DictionaryRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<HexRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<RegexRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<CharUnicodeInfoRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<Utf8StringRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<BitManipulaitonRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<EncodingRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<FieldInfoLookupRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<StrHashRunner2>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<StringHashCodeRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<StringCtorRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<TranscodingRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<StrCpyRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<ObjectFactoryRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<CopyToRunner<string>>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<Latin1GetCharsRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<Latin1GetBytesRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<Utf8ParserRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<BinaryReaderRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<StringRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<SpanRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<Runner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<AsciiRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<Runner>();
            // var summary = BenchmarkRunner.Run<CharRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<HashRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<GetByteCountRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<ListRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<MemoryRunner>(new LocalCoreClrConfig());

            //var runner = new JsonRunner();
            //runner.Setup();
            //runner.WithVector();

            // var summary = BenchmarkRunner.Run<JsonRunner>(new LocalCoreClrConfig());
        }
    }
}
