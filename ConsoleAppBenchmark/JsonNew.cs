using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace ConsoleAppBenchmark
{
    public static class VectorExtensions
    {
        public static ref Vector128<byte> MyAsByte<T>(this in Vector128<T> vector) where T : unmanaged
        {
            return ref Unsafe.As<Vector128<T>, Vector128<byte>>(ref Unsafe.AsRef(in vector));
        }

        public static ref Vector128<short> MyAsInt16<T>(this in Vector128<T> vector) where T : unmanaged
        {
            return ref Unsafe.As<Vector128<T>, Vector128<short>>(ref Unsafe.AsRef(in vector));
        }

        public static ref Vector128<uint> MyAsUInt32<T>(this in Vector128<T> vector) where T : unmanaged
        {
            return ref Unsafe.As<Vector128<T>, Vector128<uint>>(ref Unsafe.AsRef(in vector));
        }
    }

    internal static class JsonNew
    {
        private const ulong Allowed00To3F = 0x1F1F3F3F_3F1F3F3Dul;
        private const ulong Allowed40To7F = 0x3E2F3F2B_1F3F3F3Ful;

        private unsafe static int GetIndexOfFirstByteToEncode_Vectorized(ReadOnlySpan<byte> buffer)
        {
            //Debugger.Launch();
            //Debugger.Break();

            Debug.Assert(buffer.Length >= 256 / 8, "Must be at least 256 bits in length to benefit from vectorization.");

            Vector128<byte> bitmaskOfAllowedBytes = Vector128.Create(Allowed00To3F, Allowed40To7F).MyAsByte();
            Vector128<uint> vectorDWord7 = Vector128.Create((uint)7);
            Vector128<byte> blendMask1 = Vector128.Create((short)0x80).MyAsByte();
            Vector128<byte> blendMask2 = Vector128.Create((uint)0xFFFF).MyAsByte();

            ref byte rBuffer = ref MemoryMarshal.GetReference(buffer);
            fixed (byte* pBuffer = &rBuffer)
            {
                byte* pReadPos;

                // Perform an unaligned read of the first part of the buffer.

                {
                    Vector128<byte> firstData = Sse2.LoadVector128((byte*)Unsafe.AsPointer(ref rBuffer));

                    // The low nibble of each byte is used as an index into 'allowedBytesMask'.
                    // If the byte has the 0x80 bit set, we move 0x00 into the element, which is what we want.

                    Vector128<short> maskedData = Avx.Shuffle(bitmaskOfAllowedBytes, firstData).MyAsInt16();

                    // Now we'll treat the high nibble of each byte as a shift factor.

                    Vector128<uint> firstDataAsUInt = firstData.MyAsUInt32();

                    Vector128<uint> dataA = Avx.And(Avx.ShiftRightLogical(firstDataAsUInt, 4), vectorDWord7);
                    Vector128<uint> dataB = Avx.And(Avx.ShiftRightLogical(firstDataAsUInt, 12), vectorDWord7);
                    Vector128<uint> dataC = Avx.And(Avx.ShiftRightLogical(firstDataAsUInt, 20), vectorDWord7);
                    Vector128<uint> dataD = Avx.ShiftRightLogical(firstDataAsUInt, 28);

                    dataA = Avx2.ShiftLeftLogicalVariable(maskedData.MyAsUInt32(), dataA);
                    dataB = Avx2.ShiftLeftLogicalVariable(maskedData.MyAsUInt32(), dataB);
                    dataC = Avx2.ShiftLeftLogicalVariable(maskedData.MyAsUInt32(), dataC);
                    dataD = Avx2.ShiftLeftLogicalVariable(maskedData.MyAsUInt32(), dataD);

                    Vector128<byte> combined = Avx.BlendVariable(
                        Avx.BlendVariable(dataA.MyAsByte(), dataB.MyAsByte(), blendMask1),
                        Avx.BlendVariable(dataC.MyAsByte(), dataD.MyAsByte(), blendMask1),
                        blendMask2);

                    uint mask = (uint)Sse2.MoveMask(combined);
                    if (mask != ushort.MaxValue)
                    {
                        return (int)Bmi1.TrailingZeroCount(~mask);
                    }

                    if (IntPtr.Size == 8)
                    {
                        pReadPos = (byte*)(((ulong)Unsafe.AsPointer(ref rBuffer) + 16) & ~15ul);
                    }
                    else
                    {
                        pReadPos = (byte*)(((uint)Unsafe.AsPointer(ref rBuffer) + 16) & ~15u);
                    }
                }

                // Now perform aligned reads of the middle parts of the buffer.

                byte* pFinalReadPos = (byte*)Unsafe.AsPointer(ref rBuffer) + (uint)buffer.Length - 16;
                do
                {
                    Vector128<byte> firstData = Sse2.LoadAlignedVector128(pReadPos);

                    // The low nibble of each byte is used as an index into 'allowedBytesMask'.
                    // If the byte has the 0x80 bit set, we move 0x00 into the element, which is what we want.

                    Vector128<short> maskedData = Avx.Shuffle(bitmaskOfAllowedBytes, firstData).MyAsInt16();

                    // Now we'll treat the high nibble of each byte as a shift factor.

                    Vector128<uint> firstDataAsUInt = firstData.MyAsUInt32();

                    Vector128<uint> dataA = Avx.And(Avx.ShiftRightLogical(firstDataAsUInt, 4), vectorDWord7);
                    Vector128<uint> dataB = Avx.And(Avx.ShiftRightLogical(firstDataAsUInt, 12), vectorDWord7);
                    Vector128<uint> dataC = Avx.And(Avx.ShiftRightLogical(firstDataAsUInt, 20), vectorDWord7);
                    Vector128<uint> dataD = Avx.ShiftRightLogical(firstDataAsUInt, 28);

                    dataA = Avx2.ShiftLeftLogicalVariable(maskedData.MyAsUInt32(), dataA);
                    dataB = Avx2.ShiftLeftLogicalVariable(maskedData.MyAsUInt32(), dataB);
                    dataC = Avx2.ShiftLeftLogicalVariable(maskedData.MyAsUInt32(), dataC);
                    dataD = Avx2.ShiftLeftLogicalVariable(maskedData.MyAsUInt32(), dataD);

                    Vector128<byte> combined = Avx.BlendVariable(
                        Avx.BlendVariable(dataA.MyAsByte(), dataB.MyAsByte(), blendMask1),
                        Avx.BlendVariable(dataC.MyAsByte(), dataD.MyAsByte(), blendMask1),
                        blendMask2);

                    uint mask = (uint)Sse2.MoveMask(combined);
                    if (mask != ushort.MaxValue)
                    {
                        return (int)Bmi1.TrailingZeroCount(~mask) + (int)Unsafe.ByteOffset(ref rBuffer, ref *pReadPos);
                    }

                    pReadPos += 16;
                } while (pReadPos <= pFinalReadPos);

                // Finally, if we need to, perform an unaligned read of the end of the buffer.

                if ((uint)Unsafe.ByteOffset(ref *pFinalReadPos, ref *pReadPos) < 16)
                {
                    Vector128<byte> firstData = Sse2.LoadVector128(pFinalReadPos);

                    // The low nibble of each byte is used as an index into 'allowedBytesMask'.
                    // If the byte has the 0x80 bit set, we move 0x00 into the element, which is what we want.

                    Vector128<short> maskedData = Avx.Shuffle(bitmaskOfAllowedBytes, firstData).MyAsInt16();

                    // Now we'll treat the high nibble of each byte as a shift factor.

                    Vector128<uint> firstDataAsUInt = firstData.MyAsUInt32();

                    Vector128<uint> dataA = Avx.And(Avx.ShiftRightLogical(firstDataAsUInt, 4), vectorDWord7);
                    Vector128<uint> dataB = Avx.And(Avx.ShiftRightLogical(firstDataAsUInt, 12), vectorDWord7);
                    Vector128<uint> dataC = Avx.And(Avx.ShiftRightLogical(firstDataAsUInt, 20), vectorDWord7);
                    Vector128<uint> dataD = Avx.ShiftRightLogical(firstDataAsUInt, 28);

                    dataA = Avx2.ShiftLeftLogicalVariable(maskedData.MyAsUInt32(), dataA);
                    dataB = Avx2.ShiftLeftLogicalVariable(maskedData.MyAsUInt32(), dataB);
                    dataC = Avx2.ShiftLeftLogicalVariable(maskedData.MyAsUInt32(), dataC);
                    dataD = Avx2.ShiftLeftLogicalVariable(maskedData.MyAsUInt32(), dataD);

                    Vector128<byte> combined = Avx.BlendVariable(
                        Avx.BlendVariable(dataA.MyAsByte(), dataB.MyAsByte(), blendMask1),
                        Avx.BlendVariable(dataC.MyAsByte(), dataD.MyAsByte(), blendMask1),
                        blendMask2);

                    uint mask = (uint)Sse2.MoveMask(combined);
                    if (mask != ushort.MaxValue)
                    {
                        return (int)Bmi1.TrailingZeroCount(~mask) + (int)Unsafe.ByteOffset(ref rBuffer, ref *pFinalReadPos);
                    }
                }

                // We got through the entire buffer!

                return -1;
            }
        }

        private static ReadOnlySpan<byte> Mask => new byte[256]
        {
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,0,1,1,1,0,0,1,1,1,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,1,0,1,
            1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,1,1,1,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int GetIndexOfFirstByteToEncode(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length >= 256 / 8)
            {
                return GetIndexOfFirstByteToEncode_Vectorized(buffer);
            }

            ReadOnlySpan<byte> mask = Mask;

            int idx;
            for (idx = 0; idx < buffer.Length; idx++)
            {
                if (mask[buffer[idx]] == 0)
                {
                    goto Return;
                }
            }

            idx = -1;

        Return:

            return idx;
        }
    }
}
