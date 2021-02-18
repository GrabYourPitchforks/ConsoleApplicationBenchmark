using BenchmarkDotNet.Attributes;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace ConsoleAppBenchmark
{
    // [MemoryDiagnoser]
    public class GuidRunner
    {
        private Guid _a;
        private Guid _b;
        private Guid _c;

        [GlobalSetup]
        public void Setup()
        {
            _a = Guid.NewGuid();
            _b = _a;
            _c = Guid.NewGuid();
        }

        [Benchmark(Baseline = true)]
        public int GetHashCode_Old()
        {
            return OldGetHashCode(_a);
        }

        [Benchmark(Baseline = false)]
        public int GetHashCode_New()
        {
            return FastHashCode(_a);
        }

        //[Benchmark]
        //public bool Equal_32Bit()
        //{
        //    return AreEqual32(_a, _b);
        //}

        //[Benchmark]
        //public bool Equal_64Bit()
        //{
        //    return AreEqual64(_a, _b);
        //}

        //[Benchmark]
        //public bool Equal_64BitBranchless()
        //{
        //    return AreEqual64Branchless(_a, _b);
        //}

        //[Benchmark]
        //public bool Equal_Sse2()
        //{
        //    return AreEqualSimd(_a, _b);
        //}

        //[Benchmark]
        //public bool NotEqual_32Bit()
        //{
        //    return AreEqual32(_c, _b);
        //}

        //[Benchmark]
        //public bool NotEqual_64Bit()
        //{
        //    return AreEqual64(_c, _b);
        //}

        //[Benchmark]
        //public bool NotEqual_64BitBranchless()
        //{
        //    return AreEqual64Branchless(_c, _b);
        //}

        //[Benchmark]
        //public bool NotEqual_Sse2()
        //{
        //    return AreEqualSimd(_c, _b);
        //}

        private static bool AreEqual32(in Guid a, in Guid b)
        {
            ref int rA = ref Unsafe.As<Guid, int>(ref Unsafe.AsRef(in a));
            ref int rB = ref Unsafe.As<Guid, int>(ref Unsafe.AsRef(in b));

            return rA == rB
                && Unsafe.Add(ref rA, 1) == Unsafe.Add(ref rB, 1)
                && Unsafe.Add(ref rA, 2) == Unsafe.Add(ref rB, 2)
                && Unsafe.Add(ref rA, 3) == Unsafe.Add(ref rB, 3);
        }

        private static bool AreEqual64(in Guid a, in Guid b)
        {
            ref byte rA = ref Unsafe.As<Guid, byte>(ref Unsafe.AsRef(in a));
            ref byte rB = ref Unsafe.As<Guid, byte>(ref Unsafe.AsRef(in b));

            return Unsafe.ReadUnaligned<ulong>(ref rA) == Unsafe.ReadUnaligned<ulong>(ref rB)
                && Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref rA, 8)) == Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref rB, 8));
        }

        private static bool AreEqual64Branchless(in Guid a, in Guid b)
        {
            ref byte rA = ref Unsafe.As<Guid, byte>(ref Unsafe.AsRef(in a));
            ref byte rB = ref Unsafe.As<Guid, byte>(ref Unsafe.AsRef(in b));

            ulong ulla = Unsafe.ReadUnaligned<ulong>(ref rA) ^ Unsafe.ReadUnaligned<ulong>(ref rB);
            ulong ullb = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref rA, 8)) ^ Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref rB, 8));
            return (ulla | ullb) == 0;
        }

        private static bool AreEqualSimd(in Guid a, in Guid b)
        {
            ref byte rA = ref Unsafe.As<Guid, byte>(ref Unsafe.AsRef(in a));
            ref byte rB = ref Unsafe.As<Guid, byte>(ref Unsafe.AsRef(in b));

            var eq = Sse2.CompareEqual(Unsafe.ReadUnaligned<Vector128<byte>>(ref rA), Unsafe.ReadUnaligned<Vector128<byte>>(ref rB));
            return Sse2.MoveMask(eq) == 0xFFFF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int OldGetHashCode(in Guid g)
        {
            ref int rA = ref Unsafe.As<Guid, int>(ref Unsafe.AsRef(in g));

            return rA
                ^ Unsafe.Add(ref rA, 1)
                ^ Unsafe.Add(ref rA, 2)
                ^ Unsafe.Add(ref rA, 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FastHashCode(in Guid g)
        {
            var enc = Aes.Encrypt(Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.As<Guid, byte>(ref Unsafe.AsRef(in g))), default);
            return enc.AsInt32().ToScalar();
        }
    }
}
