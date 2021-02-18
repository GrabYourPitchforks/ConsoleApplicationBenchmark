using System;
using System.Buffers.Text;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace ConsoleAppBenchmark
{
    public class SpanClearRunner
    {
        private readonly object[] _objs = new object[1024];

        [Params(0, 1, 3, 7, 16, 32, 128)]
        // [Params(16)]
        public int LengthInNativeWords { get; set; }

        [Benchmark(Baseline = true)]
        public void Clear_Old()
        {
            Span<object> span = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(_objs), LengthInNativeWords);
            span.Clear();
        }

        [Benchmark(Baseline = false)]
        public void Clear_New()
        {
            Span<object> span = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(_objs), LengthInNativeWords);
            span.ClearNew();
        }
    }

    public static unsafe class MemoryExtensions2
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void ClearNew<T>(this Span<T> span)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                span.Clear();
            }
            else
            {
                ClearWithRefs(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span)), (uint)span.Length * (nuint)Unsafe.SizeOf<T>());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void ClearWithRefs(ref byte refToData, nuint lengthInBytes)
        {
            uint sizeOfVectorInBytes = (uint)Vector<byte>.Count;

            if (lengthInBytes > sizeOfVectorInBytes)
            {
                // Unaligned write

                Vector<byte> zeroVector = Vector<byte>.Zero;
                Unsafe.WriteUnaligned(ref refToData, zeroVector);

                // Now, compute alignment

                nuint bytesWritten = sizeOfVectorInBytes - ((nuint)Unsafe.AsPointer(ref refToData) & (sizeOfVectorInBytes - 1));
                refToData = ref Unsafe.Add(ref refToData, (nint)bytesWritten);
                lengthInBytes -= bytesWritten;

                // Enter main loop, assuming alignment.
                // If a GC kicks in, this could go unaligned - oh well. It'll still work, just slower.

                while (lengthInBytes >= 4 * sizeOfVectorInBytes)
                {
                    Unsafe.WriteUnaligned(ref refToData, zeroVector);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref refToData, 1 * (nint)sizeOfVectorInBytes), zeroVector);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref refToData, 2 * (nint)sizeOfVectorInBytes), zeroVector);
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref refToData, 3 * (nint)sizeOfVectorInBytes), zeroVector);

                    refToData = ref Unsafe.Add(ref refToData, 4 * (nint)sizeOfVectorInBytes);
                    lengthInBytes -= 4 * sizeOfVectorInBytes;
                }

                while (lengthInBytes > sizeOfVectorInBytes)
                {
                    Unsafe.WriteUnaligned(ref refToData, zeroVector);

                    refToData = ref Unsafe.Add(ref refToData, (nint)sizeOfVectorInBytes);
                    lengthInBytes -= sizeOfVectorInBytes;
                }
            }

            for (nuint i = 0; i < lengthInBytes; i++)
            {
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref refToData, (nint)i * sizeof(nuint)), (nuint)0);
            }
        }
    }
}
