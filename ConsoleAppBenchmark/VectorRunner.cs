using BenchmarkDotNet.Attributes;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConsoleAppBenchmark
{
    public unsafe class VectorRunner
    {
        private byte[] _bytes = new byte[Vector<byte>.Count];
        private uint[] _uints = new uint[Vector<uint>.Count];

        [Benchmark]
        public uint DoIt()
        {
            return SumAndGetFirstElement(_bytes, _uints);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static uint SumAndGetFirstElement(ReadOnlySpan<byte> bytes, ReadOnlySpan<uint> uints)
        {
            return (new Vector<uint>(bytes) + new Vector<uint>(uints))[0];
            // return (CreateVector<uint>(bytes) + CreateVector<uint>(uints))[0];
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private static Vector<T> CreateVector<T>(ReadOnlySpan<byte> bytes) where T : struct
        //{
        //    Vector<T> retVal;

        //    ThrowForUnsupportedVectorBaseType<T>();
        //    if (!MemoryMarshal.TryRead(bytes, out retVal))
        //    {
        //        ThrowSomething(Vector<byte>.Count);
        //    }

        //    return retVal;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private static Vector<T> CreateVector<T>(ReadOnlySpan<T> ts) where T : struct
        //{
        //    ThrowForUnsupportedVectorBaseType<T>();
        //    if (ts.Length < Vector<T>.Count)
        //    {
        //        ThrowSomething(Vector<T>.Count);
        //    }
        //    return Unsafe.ReadUnaligned<Vector<T>>(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(ts)));
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ThrowForUnsupportedVectorBaseType<T>() where T : struct
        {
            if (typeof(T) != typeof(byte) && typeof(T) != typeof(sbyte) &&
                typeof(T) != typeof(short) && typeof(T) != typeof(ushort) &&
                typeof(T) != typeof(int) && typeof(T) != typeof(uint) &&
                typeof(T) != typeof(long) && typeof(T) != typeof(ulong) &&
                typeof(T) != typeof(float) && typeof(T) != typeof(double))
            {
                throw new Exception();
            }
        }

        internal static void ThrowSomething(int i)
        {
            throw new Exception();
        }
    }
}
