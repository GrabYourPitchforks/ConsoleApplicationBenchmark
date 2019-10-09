//using System;
//using System.Diagnostics;
//using System.Numerics;
//using System.Runtime.CompilerServices;
//using System.Runtime.InteropServices;
//using System.Runtime.Intrinsics;
//using System.Runtime.Intrinsics.X86;

//using nint = System.Int64;
//using nuint = System.UInt64;

//namespace ConsoleAppBenchmark
//{
//    static unsafe class Utf8Logic2
//    {
//        private static unsafe nuint GetIndexOfFirstNonAsciiByte_Sse2(byte* pBuffer, nuint bufferLength)
//        {
//            // JIT turns the below into constants

//            uint SizeOfVector128 = (uint)Unsafe.SizeOf<Vector128<byte>>();
//            nuint MaskOfAllBitsInVector128 = (nuint)(SizeOfVector128 - 1);

//            Debug.Assert(Sse2.IsSupported, "Should've been checked by caller.");
//            Debug.Assert(BitConverter.IsLittleEndian, "SSE2 assumes little-endian.");

//            uint currentMask, secondMask;
//            byte* pOriginalBuffer = pBuffer;

//            // This method is written such that control generally flows top-to-bottom, avoiding
//            // jumps as much as possible in the optimistic case of a large enough buffer and
//            // "all ASCII". If we see non-ASCII data, we jump out of the hot paths to targets
//            // after all the main logic.

//            if (bufferLength < SizeOfVector128)
//            {
//                goto InputBufferLessThanOneVectorInLength; // can't vectorize; drain primitives instead
//            }

//            // Read the first vector unaligned.

//            currentMask = (uint)Sse2.MoveMask(Sse2.LoadVector128(pBuffer)); // unaligned load

//            if (currentMask != 0)
//            {
//                goto FoundNonAsciiDataInCurrentMask;
//            }

//            // If we have less than 32 bytes to process, just go straight to the final unaligned
//            // read. There's no need to mess with the loop logic in the middle of this method.

//            if (bufferLength < 2 * SizeOfVector128)
//            {
//                goto IncrementCurrentOffsetBeforeFinalUnalignedVectorRead;
//            }

//            // Now adjust the read pointer so that future reads are aligned.

//            pBuffer = (byte*)(((nuint)pBuffer + SizeOfVector128) & ~(nuint)MaskOfAllBitsInVector128);

//#if DEBUG
//            long numBytesRead = pBuffer - pOriginalBuffer;
//            Debug.Assert(0 < numBytesRead && numBytesRead <= SizeOfVector128, "We should've made forward progress of at least one byte.");
//            Debug.Assert((nuint)numBytesRead <= bufferLength, "We shouldn't have read past the end of the input buffer.");
//#endif

//            // Adjust the remaining length to account for what we just read.

//            bufferLength += (nuint)pOriginalBuffer;
//            bufferLength -= (nuint)pBuffer;

//            // The buffer is now properly aligned.
//            // Read 2 vectors at a time if possible.

//            if (bufferLength >= 2 * SizeOfVector128)
//            {
//                byte* pFinalVectorReadPos = (byte*)((nuint)pBuffer + bufferLength - 2 * SizeOfVector128);

//                // After this point, we no longer need to update the bufferLength value.

//                do
//                {
//                    Vector128<byte> firstVector = Sse2.LoadAlignedVector128(pBuffer);
//                    Vector128<byte> secondVector = Sse2.LoadAlignedVector128(pBuffer + SizeOfVector128);

//                    currentMask = (uint)Sse2.MoveMask(firstVector);
//                    secondMask = (uint)Sse2.MoveMask(secondVector);

//                    if ((currentMask | secondMask) != 0)
//                    {
//                        goto FoundNonAsciiDataInInnerLoop;
//                    }

//                    pBuffer += 2 * SizeOfVector128;
//                } while (pBuffer <= pFinalVectorReadPos);
//            }

//            // We have somewhere between 0 and (2 * vector length) - 1 bytes remaining to read from.
//            // Since the above loop doesn't update bufferLength, we can't rely on its absolute value.
//            // But we _can_ rely on it to tell us how much remaining data must be drained by looking
//            // at what bits of it are set. This works because had we updated it within the loop above,
//            // we would've been adding 2 * SizeOfVector128 on each iteration, but we only care about
//            // bits which are less significant than those that the addition would've acted on.

//            // If there is fewer than one vector length remaining, skip the next aligned read.

//            if ((bufferLength & SizeOfVector128) == 0)
//            {
//                goto DoFinalUnalignedVectorRead;
//            }

//            // At least one full vector's worth of data remains, so we can safely read it.
//            // Remember, at this point pBuffer is still aligned.

//            currentMask = (uint)Sse2.MoveMask(Sse2.LoadAlignedVector128(pBuffer));
//            if (currentMask != 0)
//            {
//                goto FoundNonAsciiDataInCurrentMask;
//            }

//        IncrementCurrentOffsetBeforeFinalUnalignedVectorRead:

//            pBuffer += SizeOfVector128;

//        DoFinalUnalignedVectorRead:

//            if (((byte)bufferLength & MaskOfAllBitsInVector128) != 0)
//            {
//                // Perform an unaligned read of the last vector.
//                // We need to adjust the pointer because we're re-reading data.

//                pBuffer += (bufferLength & MaskOfAllBitsInVector128) - SizeOfVector128;

//                currentMask = (uint)Sse2.MoveMask(Sse2.LoadVector128(pBuffer)); // unaligned load
//                if (currentMask != 0)
//                {
//                    goto FoundNonAsciiDataInCurrentMask;
//                }

//                pBuffer += SizeOfVector128;
//            }

//        Finish:

//            return (nuint)pBuffer - (nuint)pOriginalBuffer; // and we're done!

//        FoundNonAsciiDataInInnerLoop:

//            // If the current (first) mask isn't the mask that contains non-ASCII data, then it must
//            // instead be the second mask. If so, skip the entire first mask and drain ASCII bytes
//            // from the second mask.

//            if (currentMask == 0)
//            {
//                pBuffer += SizeOfVector128;
//                currentMask = secondMask;
//            }

//        FoundNonAsciiDataInCurrentMask:

//            // The mask contains - from the LSB - a 0 for each ASCII byte we saw, and a 1 for each non-ASCII byte.
//            // Tzcnt is the correct operation to count the number of zero bits quickly. If this instruction isn't
//            // available, we'll fall back to a normal loop.

//            Debug.Assert(currentMask != 0, "Shouldn't be here unless we see non-ASCII data.");
//            pBuffer += (uint)BitOperations.TrailingZeroCount(currentMask);

//            goto Finish;

//        FoundNonAsciiDataInCurrentDWord:

//            uint currentDWord;
//            Debug.Assert(!AllBytesInUInt32AreAscii(currentDWord), "Shouldn't be here unless we see non-ASCII data.");
//            pBuffer += CountNumberOfLeadingAsciiBytesFromUInt32WithSomeNonAsciiData(currentDWord);

//            goto Finish;

//        InputBufferLessThanOneVectorInLength:

//            // These code paths get hit if the original input length was less than one vector in size.
//            // We can't perform vectorized reads at this point, so we'll fall back to reading primitives
//            // directly. Note that all of these reads are unaligned.

//            Debug.Assert(bufferLength < SizeOfVector128);

//            // QWORD drain

//            if ((bufferLength & 8) != 0)
//            {
//                if (Bmi1.X64.IsSupported)
//                {
//                    // If we can use 64-bit tzcnt to count the number of leading ASCII bytes, prefer it.

//                    ulong candidateUInt64 = Unsafe.ReadUnaligned<ulong>(pBuffer);
//                    if (!AllBytesInUInt64AreAscii(candidateUInt64))
//                    {
//                        // Clear everything but the high bit of each byte, then tzcnt.
//                        // Remember the / 8 at the end to convert bit count to byte count.

//                        candidateUInt64 &= UInt64HighBitsOnlyMask;
//                        pBuffer += (nuint)(Bmi1.X64.TrailingZeroCount(candidateUInt64) / 8);
//                        goto Finish;
//                    }
//                }
//                else
//                {
//                    // If we can't use 64-bit tzcnt, no worries. We'll just do 2x 32-bit reads instead.

//                    currentDWord = Unsafe.ReadUnaligned<uint>(pBuffer);
//                    uint nextDWord = Unsafe.ReadUnaligned<uint>(pBuffer + 4);

//                    if (!AllBytesInUInt32AreAscii(currentDWord | nextDWord))
//                    {
//                        // At least one of the values wasn't all-ASCII.
//                        // We need to figure out which one it was and stick it in the currentMask local.

//                        if (AllBytesInUInt32AreAscii(currentDWord))
//                        {
//                            currentDWord = nextDWord; // this one is the culprit
//                            pBuffer += 4;
//                        }

//                        goto FoundNonAsciiDataInCurrentDWord;
//                    }
//                }

//                pBuffer += 8; // successfully consumed 8 ASCII bytes
//            }

//            // DWORD drain

//            if ((bufferLength & 4) != 0)
//            {
//                currentDWord = Unsafe.ReadUnaligned<uint>(pBuffer);

//                if (!AllBytesInUInt32AreAscii(currentDWord))
//                {
//                    goto FoundNonAsciiDataInCurrentDWord;
//                }

//                pBuffer += 4; // successfully consumed 4 ASCII bytes
//            }

//            // WORD drain
//            // (We movzx to a DWORD for ease of manipulation.)

//            if ((bufferLength & 2) != 0)
//            {
//                currentDWord = Unsafe.ReadUnaligned<ushort>(pBuffer);

//                if (!AllBytesInUInt32AreAscii(currentDWord))
//                {
//                    // We only care about the 0x0080 bit of the value. If it's not set, then we
//                    // increment currentOffset by 1. If it's set, we don't increment it at all.

//                    pBuffer += (nuint)((nint)(sbyte)currentDWord >> 7) + 1;
//                    goto Finish;
//                }

//                pBuffer += 2; // successfully consumed 2 ASCII bytes
//            }

//            // BYTE drain

//            if ((bufferLength & 1) != 0)
//            {
//                // sbyte has non-negative value if byte is ASCII.

//                if (*(sbyte*)(pBuffer) >= 0)
//                {
//                    pBuffer++; // successfully consumed a single byte
//                }
//            }

//            goto Finish;
//        }

//        public static int GetIndexOfFirstInvalidUtf8Byte(ref byte data, int length)
//        {
//            int retVal;

//            fixed (byte* pData = &data)
//            {
//                retVal = (int)GetIndexOfFirstNonAsciiByte_Sse2(pData, (uint)length);
//            }

//            if (retVal < length)
//            {
//                retVal += GetIndexOfFirstInvalidUtf8Byte_Worker(ref Unsafe.Add(ref data, (IntPtr)(void*)retVal), length - retVal);
//            }

//            return retVal;
//        }

//        private static int GetIndexOfFirstInvalidUtf8Byte_Worker(ref byte data, int length)
//        {
//            int totalNumBytesValidated = 0;

//            if (length < sizeof(Vector128<sbyte>))
//            {
//                goto SmallInput;
//            }

//            do
//            {
//                uint numBytesConsumedThisIteration;

//                // Get the vector of the data we're going to test.

//                Vector128<sbyte> dataAsVector = Unsafe.ReadUnaligned<Vector128<sbyte>>(ref data);

//                // First, check for all-ASCII.
//                // (Or, rather, if the first 15 bytes are ASCII.)

//                uint asciiMask = (uint)Sse2.MoveMask(dataAsVector);
//                if ((asciiMask & 0x7FFF) == 0)
//                {
//                    numBytesConsumedThisIteration = 16 - (asciiMask >> 15);
//                    goto EndOfLoop;
//                }

//                uint combinedMask = (ushort)(~asciiMask);

//                // Saw some non-ASCII data, *and* there's potentially
//                // room left over for a 2-byte sequence. Get the mask
//                // of all the bytes which are continuation bytes ([ 80 .. BF ]).

//                ref Vector128<sbyte> VectorConstants = ref Unsafe.As<byte, Vector128<sbyte>>(ref MemoryMarshal.GetReference(GetVectors()));

//                uint continuationByteMask = (ushort)~Sse2.MoveMask(Sse2.CompareGreaterThan(dataAsVector, Vector128.Create(unchecked((sbyte)0xBF))));

//                // Do we see any 2-byte sequence markers?
//                // Those would be [ C2 .. DF ].

//                Vector128<sbyte> tempData = Sse2.Add(dataAsVector, Vector128.Create(unchecked((sbyte)0xBE)));
//                uint maskOf2ByteSequenceMarkers = ~(uint)Sse2.MoveMask(Sse2.CompareGreaterThan(tempData, Vector128.Create(unchecked((sbyte)0x9D))));
//                combinedMask += ((continuationByteMask >> 1) & maskOf2ByteSequenceMarkers) * 3;

//                if (((combinedMask + 1) & 0x3FFF) == 0)
//                {
//                    numBytesConsumedThisIteration = (uint)BitOperations.TrailingZeroCount((combinedMask + 1) | (1u << 16));
//                    goto EndOfLoop;
//                }

//                // Do we see any 3-byte sequence markers?
//                // Those would be [ E0 .. EF ].

//                // Vector128<sbyte> dataShiftedRightByOne = Ssse3.Shuffle(data, VecShufRightOneByte());
//                Vector128<sbyte> dataShiftedRightByOne = Ssse3.AlignRight(dataAsVector, dataAsVector, 1);
//                Vector128<sbyte> maskForShuffle3 = Sse2.And(dataAsVector, Vector128.Create((sbyte)0x0F));

//                Vector128<sbyte> maskOf3ByteSequenceMarkers = Sse2.CompareGreaterThan(Sse2.Add(dataAsVector, Vector128.Create(unchecked((sbyte)0xA0))), Vector128.Create(unchecked((sbyte)0x8F)));

//                // This is a little more complicated than the 2-byte case,
//                // as certain ranges (overlongs and surrogates) are disallowed.
//                // We can use the low nibble of the sequence markers as an index
//                // into a shuffle vector, and that vector can be used to determine
//                // the high & low bounds of the byte which immediately follows.
//                // The third byte is a normal continuation byte.

//                Vector128<sbyte> lowerBoundsInclusive = Ssse3.Shuffle(Unsafe.Add(ref VectorConstants, OffsetToVecLowerBoundInclusive3ByteSeq), maskForShuffle3);
//                Vector128<sbyte> upperBoundsExclusive = Ssse3.Shuffle(Unsafe.Add(ref VectorConstants, OffsetToVecUpperBoundInclusive3ByteSeq), maskForShuffle3);

//                Vector128<sbyte> maskOfSuccessfulTrailingByteBoundsCheck = Sse2.AndNot(
//                    Sse2.CompareLessThan(dataShiftedRightByOne, lowerBoundsInclusive),
//                    Sse2.CompareLessThan(dataShiftedRightByOne, upperBoundsExclusive));

//                Vector128<sbyte> maskOfSequenceMarkerAndSuccessfulTrailingByteBoundsCheck = Sse2.AndNot(maskOf3ByteSequenceMarkers, maskOfSuccessfulTrailingByteBoundsCheck);

//                uint maskOfSuccessful3ByteSequences = (uint)Sse2.MoveMask(maskOfSequenceMarkerAndSuccessfulTrailingByteBoundsCheck) & (continuationByteMask >> 2);
//                combinedMask += (maskOfSuccessful3ByteSequences << 3) - maskOfSuccessful3ByteSequences;

//                if (((combinedMask + 1) & 0x1FFF) == 0)
//                {
//                    numBytesConsumedThisIteration = (uint)BitOperations.TrailingZeroCount((combinedMask + 1) | (1u << 16));
//                    goto EndOfLoop;
//                }

//                // Do we see any 4-byte sequence markers?
//                // Those would be [ F0 .. F4 ].
//                // (For simplicity, we'll check [ F0 .. FF ].)

//                Vector128<sbyte> maskOf4ByteSequenceMarkers = Sse2.CompareGreaterThan(Sse2.Add(dataAsVector, Vector128.Create(unchecked((sbyte)0x90))), Vector128.Create(unchecked((sbyte)0x8F)));
//                Vector128<sbyte> maskForShuffle4 = Sse2.And(dataAsVector, Vector128.Create((sbyte)0x0F));

//                // Use a shuffle just like in the 3-byte case.

//                lowerBoundsInclusive = Ssse3.Shuffle(Unsafe.Add(ref VectorConstants, OffsetToVecLowerBoundInclusive4ByteSeq), maskForShuffle4);
//                upperBoundsExclusive = Ssse3.Shuffle(Unsafe.Add(ref VectorConstants, OffsetToVecUpperBoundInclusive4ByteSeq), maskForShuffle4);

//                maskOfSuccessfulTrailingByteBoundsCheck = Sse2.AndNot(
//                    Sse2.CompareLessThan(dataShiftedRightByOne, lowerBoundsInclusive),
//                    Sse2.CompareLessThan(dataShiftedRightByOne, upperBoundsExclusive));

//                maskOfSequenceMarkerAndSuccessfulTrailingByteBoundsCheck = Sse2.AndNot(maskOf4ByteSequenceMarkers, maskOfSuccessfulTrailingByteBoundsCheck);

//                uint maskOfSuccessful4ByteSequences = (uint)Sse2.MoveMask(maskOfSequenceMarkerAndSuccessfulTrailingByteBoundsCheck) & (continuationByteMask >> 2) & (continuationByteMask >> 3);
//                combinedMask += (maskOfSuccessful4ByteSequences << 4) - maskOfSuccessful4ByteSequences;
//                numBytesConsumedThisIteration = (uint)BitOperations.TrailingZeroCount(combinedMask + 1);

//            EndOfLoop:

//            } while (length >= sizeof(Vector128<sbyte>));

//        SmallInput:

//            while (length >= sizeof(Vector128<byte>))
//            {
//                int bytesValidatedJustNow = ValidateVector(ref data);

//                data = ref Unsafe.Add(ref data, bytesValidatedJustNow);
//                totalNumBytesValidated += bytesValidatedJustNow;
//                length -= bytesValidatedJustNow;

//                if (bytesValidatedJustNow < 13)
//                {
//                    return totalNumBytesValidated;
//                }
//            }

//            if (length > 0)
//            {
//                // Still have pending data, but not enough to populate an entire vector.
//                // Let's create a partial vector and perform the SIMD checks over that.

//                Vector128<byte> tempVector = default;

//                fixed (byte* pbData = &data)
//                {
//                    Buffer.MemoryCopy(pbData, &tempVector, sizeof(Vector128<byte>), length);
//                }

//                int bytesValidatedJustNow = ValidateVector(ref Unsafe.As<Vector128<byte>, byte>(ref tempVector));
//                if (bytesValidatedJustNow > length)
//                {
//                    bytesValidatedJustNow = length;
//                }

//                totalNumBytesValidated += bytesValidatedJustNow;
//            }

//            return totalNumBytesValidated;
//        }

//        /// <summary>
//        /// A mask which selects only the high bit of each byte of the given <see cref="uint"/>.
//        /// </summary>
//        private const uint UInt32HighBitsOnlyMask = 0x80808080u;

//        /// <summary>
//        /// A mask which selects only the high bit of each byte of the given <see cref="ulong"/>.
//        /// </summary>
//        private const ulong UInt64HighBitsOnlyMask = 0x80808080_80808080ul;

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        private static bool AllBytesInUInt64AreAscii(ulong value)
//        {
//            // If the high bit of any byte is set, that byte is non-ASCII.

//            return (value & UInt64HighBitsOnlyMask) == 0;
//        }

//        /// <summary>
//        /// Returns <see langword="true"/> iff all bytes in <paramref name="value"/> are ASCII.
//        /// </summary>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        internal static bool AllBytesInUInt32AreAscii(uint value)
//        {
//            // If the high bit of any byte is set, that byte is non-ASCII.

//            return (value & UInt32HighBitsOnlyMask) == 0;
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        internal static uint CountNumberOfLeadingAsciiBytesFromUInt32WithSomeNonAsciiData(uint value)
//        {
//            Debug.Assert(!AllBytesInUInt32AreAscii(value), "Caller shouldn't provide an all-ASCII value.");

//            // Use BMI1 directly rather than going through BitOperations. We only see a perf gain here
//            // if we're able to emit a real tzcnt instruction; the software fallback used by BitOperations
//            // is too slow for our purposes since we can provide our own faster, specialized software fallback.

//            if (Bmi1.IsSupported)
//            {
//                Debug.Assert(BitConverter.IsLittleEndian);
//                return Bmi1.TrailingZeroCount(value & UInt32HighBitsOnlyMask) >> 3;
//            }

//            // Couldn't emit tzcnt, use specialized software fallback.
//            // The 'allBytesUpToNowAreAscii' DWORD uses bit twiddling to hold a 1 or a 0 depending
//            // on whether all processed bytes were ASCII. Then we accumulate all of the
//            // results to calculate how many consecutive ASCII bytes are present.

//            value = ~value;

//            if (BitConverter.IsLittleEndian)
//            {
//                // Read first byte
//                value >>= 7;
//                uint allBytesUpToNowAreAscii = value & 1;
//                uint numAsciiBytes = allBytesUpToNowAreAscii;

//                // Read second byte
//                value >>= 8;
//                allBytesUpToNowAreAscii &= value;
//                numAsciiBytes += allBytesUpToNowAreAscii;

//                // Read third byte
//                value >>= 8;
//                allBytesUpToNowAreAscii &= value;
//                numAsciiBytes += allBytesUpToNowAreAscii;

//                return numAsciiBytes;
//            }
//            else
//            {
//                // BinaryPrimitives.ReverseEndianness is only implemented as an intrinsic on
//                // little-endian platforms, so using it in this big-endian path would be too
//                // expensive. Instead we'll just change how we perform the shifts.

//                // Read first byte
//                value = BitOperations.RotateLeft(value, 1);
//                uint allBytesUpToNowAreAscii = value & 1;
//                uint numAsciiBytes = allBytesUpToNowAreAscii;

//                // Read second byte
//                value = BitOperations.RotateLeft(value, 8);
//                allBytesUpToNowAreAscii &= value;
//                numAsciiBytes += allBytesUpToNowAreAscii;

//                // Read third byte
//                value = BitOperations.RotateLeft(value, 8);
//                allBytesUpToNowAreAscii &= value;
//                numAsciiBytes += allBytesUpToNowAreAscii;

//                return numAsciiBytes;
//            }
//        }

//        private static readonly Vector128<sbyte> Vector0x0F = Vector128.Create((sbyte)0x0F);

//        const int OffsetToVecLowerBoundInclusive3ByteSeq = 0;
//        const int OffsetToVecUpperBoundInclusive3ByteSeq = 1;
//        const int OffsetToVecLowerBoundInclusive4ByteSeq = 2;
//        const int OffsetToVecUpperBoundInclusive4ByteSeq = 3;

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        private static ReadOnlySpan<byte> GetVectors()
//        {
//            return new byte[]
//            {
//                0xA0, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
//                0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,

//                0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0,
//                0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xA0, 0xC0, 0xC0,

//                0x90, 0x80, 0x80, 0x80, 0x80, 0x7F, 0x7F, 0x7F,
//                0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F, 0x7F,

//                0xC0, 0xC0, 0xC0, 0xC0, 0x90, 0x80, 0x80, 0x80,
//                0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,

//                0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F,
//                0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F,

//                0x8F, 0x8F, 0x8F, 0x8F, 0x8F, 0x8F, 0x8F, 0x8F,
//                0x8F, 0x8F, 0x8F, 0x8F, 0x8F, 0x8F, 0x8F, 0x8F,

//                0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
//                0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,

//                0x9D, 0x9D, 0x9D, 0x9D, 0x9D, 0x9D, 0x9D, 0x9D,
//                0x9D, 0x9D, 0x9D, 0x9D, 0x9D, 0x9D, 0x9D, 0x9D,

//                0xA0, 0xA0, 0xA0, 0xA0, 0xA0, 0xA0, 0xA0, 0xA0,
//                0xA0, 0xA0, 0xA0, 0xA0, 0xA0, 0xA0, 0xA0, 0xA0,

//                0xBE, 0xBE, 0xBE, 0xBE, 0xBE, 0xBE, 0xBE, 0xBE,
//                0xBE, 0xBE, 0xBE, 0xBE, 0xBE, 0xBE, 0xBE, 0xBE,

//                0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF,
//                0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF, 0xBF,
//            };
//        }
//    }
//}
