using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace ConsoleAppBenchmark
{
    static unsafe class Utf8Logic
    {
        public static int GetIndexOfFirstInvalidUtf8Byte(ref byte data, int length)
        {
            int totalNumBytesValidated = 0;

            while (length >= sizeof(Vector128<byte>))
            {
                int bytesValidatedJustNow = ValidateVector(ref data);

                data = ref Unsafe.Add(ref data, bytesValidatedJustNow);
                totalNumBytesValidated += bytesValidatedJustNow;
                length -= bytesValidatedJustNow;

                if (bytesValidatedJustNow < 13)
                {
                    return totalNumBytesValidated;
                }
            }

            if (length > 0)
            {
                // Still have pending data, but not enough to populate an entire vector.
                // Let's create a partial vector and perform the SIMD checks over that.

                Vector128<byte> tempVector = default;

                fixed (byte* pbData = &data)
                {
                    Buffer.MemoryCopy(pbData, &tempVector, sizeof(Vector128<byte>), length);
                }

                int bytesValidatedJustNow = ValidateVector(ref Unsafe.As<Vector128<byte>, byte>(ref tempVector));
                if (bytesValidatedJustNow > length)
                {
                    bytesValidatedJustNow = length;
                }

                totalNumBytesValidated += bytesValidatedJustNow;
            }

            return totalNumBytesValidated;
        }

        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ValidateVector(ref byte buffer)
        {
            // Get the vector of the data we're going to test.

            Vector128<sbyte> data = Unsafe.ReadUnaligned<Vector128<sbyte>>(ref buffer);

            // First, check for all-ASCII.
            // (Or, rather, if the first 15 bytes are ASCII.)

            uint asciiMask = (uint)Sse2.MoveMask(data);
            if ((asciiMask & 0x7FFF) == 0)
            {
                return 16 - (int)(asciiMask >> 15);
            }

            uint combinedMask = (ushort)(~asciiMask);

            // Saw some non-ASCII data, *and* there's potentially
            // room left over for a 2-byte sequence. Get the mask
            // of all the bytes which are continuation bytes ([ 80 .. BF ]).

            ref Vector128<sbyte> VectorConstants = ref Unsafe.As<byte, Vector128<sbyte>>(ref MemoryMarshal.GetReference(GetVectors()));

            uint continuationByteMask = (ushort)~Sse2.MoveMask(Sse2.CompareGreaterThan(data, Unsafe.Add(ref VectorConstants, OffsetTo0xBF)));

            // Do we see any 2-byte sequence markers?
            // Those would be [ C2 .. DF ].

            Vector128<sbyte> tempData = Sse2.Add(data, Unsafe.Add(ref VectorConstants, OffsetTo0xBE));
            uint maskOf2ByteSequenceMarkers = ~(uint)Sse2.MoveMask(Sse2.CompareGreaterThan(tempData, Unsafe.Add(ref VectorConstants, OffsetTo0x9D)));
            combinedMask += ((continuationByteMask >> 1) & maskOf2ByteSequenceMarkers) * 3;

            if (((combinedMask + 1) & 0x3FFF) == 0)
            {
                return BitOperations.TrailingZeroCount((combinedMask + 1) | (1u << 16));
            }

            // Do we see any 3-byte sequence markers?
            // Those would be [ E0 .. EF ].

            // Vector128<sbyte> dataShiftedRightByOne = Ssse3.Shuffle(data, VecShufRightOneByte());
            Vector128<sbyte> dataShiftedRightByOne = Ssse3.AlignRight(data, data, 1);
            Vector128<sbyte> maskForShuffle3 = Sse2.And(data, Vector0x0F);

            Vector128<sbyte> maskOf3ByteSequenceMarkers = Sse2.CompareGreaterThan(Sse2.Add(data, Unsafe.Add(ref VectorConstants, OffsetTo0xA0)), Unsafe.Add(ref VectorConstants, OffsetTo0x8F));

            // This is a little more complicated than the 2-byte case,
            // as certain ranges (overlongs and surrogates) are disallowed.
            // We can use the low nibble of the sequence markers as an index
            // into a shuffle vector, and that vector can be used to determine
            // the high & low bounds of the byte which immediately follows.
            // The third byte is a normal continuation byte.

            Vector128<sbyte> lowerBoundsInclusive = Ssse3.Shuffle(Unsafe.Add(ref VectorConstants, OffsetToVecLowerBoundInclusive3ByteSeq), maskForShuffle3);
            Vector128<sbyte> upperBoundsExclusive = Ssse3.Shuffle(Unsafe.Add(ref VectorConstants, OffsetToVecUpperBoundInclusive3ByteSeq), maskForShuffle3);

            Vector128<sbyte> maskOfSuccessfulTrailingByteBoundsCheck = Sse2.AndNot(
                Sse2.CompareLessThan(dataShiftedRightByOne, lowerBoundsInclusive),
                Sse2.CompareLessThan(dataShiftedRightByOne, upperBoundsExclusive));

            Vector128<sbyte> maskOfSequenceMarkerAndSuccessfulTrailingByteBoundsCheck = Sse2.AndNot(maskOf3ByteSequenceMarkers, maskOfSuccessfulTrailingByteBoundsCheck);

            uint maskOfSuccessful3ByteSequences = (uint)Sse2.MoveMask(maskOfSequenceMarkerAndSuccessfulTrailingByteBoundsCheck) & (continuationByteMask >> 2);
            combinedMask += (maskOfSuccessful3ByteSequences << 3) - maskOfSuccessful3ByteSequences;

            if (((combinedMask + 1) & 0x1FFF) == 0)
            {
                return BitOperations.TrailingZeroCount((combinedMask + 1) | (1u << 16));
            }

            // Do we see any 4-byte sequence markers?
            // Those would be [ F0 .. F4 ].
            // (For simplicity, we'll check [ F0 .. FF ].)

            Vector128<sbyte> maskOf4ByteSequenceMarkers = Sse2.CompareGreaterThan(Sse2.Add(data, Unsafe.Add(ref VectorConstants, OffsetTo0x90)), Unsafe.Add(ref VectorConstants, OffsetTo0x8F));
            Vector128<sbyte> maskForShuffle4 = Sse2.And(data, Vector0x0F);

            // Use a shuffle just like in the 3-byte case.

            lowerBoundsInclusive = Ssse3.Shuffle(Unsafe.Add(ref VectorConstants, OffsetToVecLowerBoundInclusive4ByteSeq), maskForShuffle4);
            upperBoundsExclusive = Ssse3.Shuffle(Unsafe.Add(ref VectorConstants, OffsetToVecUpperBoundInclusive4ByteSeq), maskForShuffle4);

            maskOfSuccessfulTrailingByteBoundsCheck = Sse2.AndNot(
                Sse2.CompareLessThan(dataShiftedRightByOne, lowerBoundsInclusive),
                Sse2.CompareLessThan(dataShiftedRightByOne, upperBoundsExclusive));

            maskOfSequenceMarkerAndSuccessfulTrailingByteBoundsCheck = Sse2.AndNot(maskOf4ByteSequenceMarkers, maskOfSuccessfulTrailingByteBoundsCheck);

            uint maskOfSuccessful4ByteSequences = (uint)Sse2.MoveMask(maskOfSequenceMarkerAndSuccessfulTrailingByteBoundsCheck) & (continuationByteMask >> 2) & (continuationByteMask >> 3);
            combinedMask += (maskOfSuccessful4ByteSequences << 4) - maskOfSuccessful4ByteSequences;
            return BitOperations.TrailingZeroCount(combinedMask + 1);
        }

        private static readonly Vector128<sbyte> Vector0x0F = Vector128.Create((sbyte)0x0F);

        const int OffsetToVecLowerBoundInclusive3ByteSeq = 0;
        const int OffsetToVecUpperBoundInclusive3ByteSeq = 1;
        const int OffsetToVecLowerBoundInclusive4ByteSeq = 2;
        const int OffsetToVecUpperBoundInclusive4ByteSeq = 3;
        const int OffsetTo0x0F = 4;
        const int OffsetTo0x8F = 5;
        const int OffsetTo0x90 = 6;
        const int OffsetTo0x9D = 7;
        const int OffsetTo0xA0 = 8;
        const int OffsetTo0xBE = 9;
        const int OffsetTo0xBF = 10;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<byte> GetVectors()
        {
            return new byte[]
            {
                0xA0, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
                0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,

                0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0,
                0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xA0, 0xC0, 0xC0,

                0x90, 0x80, 0x80, 0x80, 0x80, 0x7F, 0x7F, 0x7F,
                0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F,

                0xC0, 0xC0, 0xC0, 0xC0, 0x90, 0x80, 0x80, 0x80,
                0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,

                0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F,
                0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F,

                0x8F, 0x8F, 0x8F, 0x8F, 0x8F, 0x8F, 0x8F, 0x8F,
                0x8F, 0x8F, 0x8F, 0x8F, 0x8F, 0x8F, 0x8F, 0x8F,

                0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
                0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,

                0x9D, 0x9D, 0x9D, 0x9D, 0x9D, 0x9D, 0x9D, 0x9D,
                0x9D, 0x9D, 0x9D, 0x9D, 0x9D, 0x9D, 0x9D, 0x9D,

                0xA0, 0xA0, 0xA0, 0xA0, 0xA0, 0xA0, 0xA0, 0xA0,
                0xA0, 0xA0, 0xA0, 0xA0, 0xA0, 0xA0, 0xA0, 0xA0,

                0xBE, 0xBE, 0xBE, 0xBE, 0xBE, 0xBE, 0xBE, 0xBE,
                0xBE, 0xBE, 0xBE, 0xBE, 0xBE, 0xBE, 0xBE, 0xBE,

                0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF,
                0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF,
            };
        }
    }
}
