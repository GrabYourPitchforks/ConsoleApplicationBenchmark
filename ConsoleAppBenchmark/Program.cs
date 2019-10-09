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
                    coreRunDirectory: @"C:\Users\levib\Desktop\experiments\master",
                    displayName: "master",
                    isBaseline: true);

                //AddCustom30Toolchain(
                //    coreRunDirectory: @"C:\Users\levib\Desktop\experiments\proto",
                //    displayName: "proto",
                //    isBaseline: false);

                //AddCustom30Toolchain(
                //  coreRunDirectory: @"C:\Users\levib\Desktop\experiments\proto2",
                //  displayName: "proto2",
                //  isBaseline: false);

                //AddCustom30Toolchain(
                //   coreRunDirectory: @"C:\Users\levib\Desktop\experiments\ascii_test_no_hi",
                //   displayName: "without_intrin",
                //   isasToSuppress: "AVX2");

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

                // var job = Job.ShortRun.With(toolchain);
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

                // job = job.With(new[] { new EnvironmentVariable("COMPLUS_TieredCompilation", "0") });

                // job = job.WithWarmupCount(10);

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
            //Utf8StringRunner runner = new Utf8StringRunner();
            //runner.Corpus = "39251-0.txt";
            //runner.Setup();
            //runner.Validate_Exp1();

            // var summary = BenchmarkRunner.Run<Utf8ValidationRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<SliceRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<CharRunner>(new LocalCoreClrConfig());
            var summary = BenchmarkRunner.Run<Utf8StringRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<EncodingRunner>(new LocalCoreClrConfig());
            // var summary = BenchmarkRunner.Run<StringHashCodeRunner>(new LocalCoreClrConfig());
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
