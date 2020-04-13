using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    [DisassemblyDiagnoser]
    public class StringHashCodeRunner
    {
        private delegate int HashCodeRoutine(string s);

        private EqualityComparer<string> _equalityComparer;
        private EqualityComparer<string> _newEqualityComparer;
        private HashCodeRoutine _del;
        private string _str;

        // [Params(0, 1, 2, 3, 7, 16, 64, 256)]
        [Params(0, 3, 7, 16, 256)]
        // [Params(0)]
        public int StringLength;

        [GlobalSetup]
        public void Setup()
        {
            Type equalityComparerType = typeof(string).Assembly.GetType("System.Collections.Generic.NonRandomizedStringEqualityComparer");
            _equalityComparer = (EqualityComparer<string>)equalityComparerType.GetProperty("Default", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            // _newEqualityComparer = new MyEqualityComparer();
            _str = new string('x', StringLength);

            var mi = typeof(string).GetMethod("GetNonRandomizedHashCode", BindingFlags.NonPublic | BindingFlags.Instance);
            _del = (HashCodeRoutine)mi.CreateDelegate(typeof(HashCodeRoutine), null);
        }

        [Benchmark]
        public int GetNonRandomizedHashCode()
        {
            return _equalityComparer.GetHashCode(_str);
        }

        //[Benchmark]
        //public int GetHashCode_Test2()
        //{
        //    return _del(_str);
        //}

        //[Benchmark(Baseline = true)]
        //public int GetHashCode_Base()
        //{
        //    return GetNonRandomizedHashCode_Baseline(_str);
        //}

        //[Benchmark(Baseline = false)]
        //public int GetHashCode_Ex1()
        //{
        //    return GetNonRandomizedHashCode_Ex1(_str);
        //}

        //[Benchmark(Baseline = false)]
        //public int GetHashCode_Ex2()
        //{
        //    return GetNonRandomizedHashCode_Ex2(_str);
        //}


        //private sealed class MyEqualityComparer : EqualityComparer<string>
        //{
        //    public override bool Equals([AllowNull] string x, [AllowNull] string y)
        //    {
        //        throw new NotImplementedException();
        //    }

        //    public override int GetHashCode([DisallowNull] string s)
        //    {
        //        IntPtr stringLength = (IntPtr)(uint)s.Length;

        //        uint hash1 = (5381 << 16) + 5381;
        //        uint hash2 = hash1;

        //        ref byte firstChar = ref Unsafe.As<char, Byte>(ref Unsafe.AsRef(in s.GetPinnableReference()));
        //        ref byte lastChar = ref Unsafe.As<char, Byte>(ref Unsafe.Add(ref Unsafe.As<Byte, Char>(ref firstChar), stringLength));

        //        while (true)
        //        {
        //            hash1 = (BitOperations.RotateLeft(hash1, 5) + hash1) ^ Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref lastChar, (IntPtr)(int)-4));
        //            hash2 = (BitOperations.RotateLeft(hash2, 5) + hash2) ^ Unsafe.ReadUnaligned<uint>(ref Unsafe.AddByteOffset(ref lastChar, (IntPtr)(int)-8));

        //            lastChar = ref Unsafe.AddByteOffset(ref lastChar, (IntPtr)(int)-8);
        //            if (!Unsafe.IsAddressGreaterThan(ref lastChar, ref firstChar))
        //            {
        //                break;
        //            }
        //        }

        //        return (int)(hash1 + (hash2 * 1566083941));
        //    }
        //}

        //private static unsafe int GetNonRandomizedHashCode_Baseline(string s)
        //{
        //    fixed (char* src = s)
        //    {
        //        Debug.Assert(src[s.Length] == '\0', "src[this.Length] == '\\0'");
        //        Debug.Assert(((int)src) % 4 == 0, "Managed string should start at 4 bytes boundary");

        //        uint hash1 = (5381 << 16) + 5381;
        //        uint hash2 = hash1;

        //        uint* ptr = (uint*)src;
        //        int length = s.Length;

        //        while (length > 2)
        //        {
        //            length -= 4;
        //            // Where length is 4n-1 (e.g. 3,7,11,15,19) this additionally consumes the null terminator
        //            hash1 = (BitOperations.RotateLeft(hash1, 5) + hash1) ^ ptr[0];
        //            hash2 = (BitOperations.RotateLeft(hash2, 5) + hash2) ^ ptr[1];
        //            ptr += 2;
        //        }

        //        if (length > 0)
        //        {
        //            // Where length is 4n-3 (e.g. 1,5,9,13,17) this additionally consumes the null terminator
        //            hash2 = (BitOperations.RotateLeft(hash2, 5) + hash2) ^ ptr[0];
        //        }

        //        return (int)(hash1 + (hash2 * 1566083941));
        //    }
        //}

        //private static unsafe int GetNonRandomizedHashCode_Ex1(string s)
        //{
        //    uint hash1 = (5381 << 16) + 5381;
        //    uint hash2 = hash1;

        //    int length = s.Length;
        //    ref uint ptr = ref Unsafe.As<char, uint>(ref Unsafe.AsRef(in s.GetPinnableReference()));

        //    while (length > 2)
        //    {
        //        length -= 4;
        //        // Where length is 4n-1 (e.g. 3,7,11,15,19) this additionally consumes the null terminator
        //        hash1 = (BitOperations.RotateLeft(hash1, 5) + hash1) ^ ptr;
        //        hash2 = (BitOperations.RotateLeft(hash2, 5) + hash2) ^ Unsafe.Add(ref ptr, 1);
        //        ptr = ref Unsafe.Add(ref ptr, 2);
        //    }

        //    if (length > 0)
        //    {
        //        // Where length is 4n-3 (e.g. 1,5,9,13,17) this additionally consumes the null terminator
        //        hash2 = (BitOperations.RotateLeft(hash2, 5) + hash2) ^ ptr;
        //    }

        //    return (int)(hash1 + (hash2 * 1566083941));
        //}

        //private static unsafe int GetNonRandomizedHashCode_Ex2(string s)
        //{
        //    Vector128<uint> vec = Vector128.CreateScalarUnsafe(0x1505_1505_1505_1505ul).AsUInt32();

        //    int length = s.Length;
        //    ref byte ptr = ref Unsafe.As<char, byte>(ref Unsafe.AsRef(in s.GetPinnableReference()));

        //    while (length > 2)
        //    {
        //        length -= 4;
        //        // Where length is 4n-1 (e.g. 3,7,11,15,19) this additionally consumes the null terminator

        //        var temp1 = Sse2.ShiftLeftLogical(vec, 5);
        //        var temp2 = Sse2.ShiftRightLogical(vec, 27);
        //        vec = Sse2.Add(vec, Sse2.Or(temp1, temp2));
        //        vec = Sse2.Xor(vec, Sse2.X64.ConvertScalarToVector128UInt64(Unsafe.ReadUnaligned<ulong>(ref ptr)).AsUInt32());
        //        ptr = ref Unsafe.Add(ref ptr, 8);
        //    }

        //    ulong hash2 = Sse2.X64.ConvertToUInt64(vec.AsUInt64());
        //    uint hash1 = (uint)(hash2 >> 32);

        //    if (length > 0)
        //    {
        //        // Where length is 4n-3 (e.g. 1,5,9,13,17) this additionally consumes the null terminator
        //        hash2 = (BitOperations.RotateLeft((uint)hash2, 5) + (uint)hash2) ^ Unsafe.As<byte, uint>(ref ptr);
        //    }

        //    return (int)(hash1 + ((uint)hash2 * 1566083941));
        //}
    }
}
