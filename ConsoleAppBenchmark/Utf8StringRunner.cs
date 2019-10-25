using BenchmarkDotNet.Attributes;
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

using nint = System.Int64;
using nuint = System.UInt64;

namespace ConsoleAppBenchmark
{
    // [MemoryDiagnoser]
    public unsafe class Utf8StringRunner
    {
        private const string SampleTextsFolder = @"C:\Users\levib\source\repos\fast-utf8\FastUtf8Tester\SampleTexts\";

        // [Params("11.txt", "11-0.txt", "25249-0.txt", "30774-0.txt", "39251-0.txt")]
        [Params("25249-0.txt")]
        public string Corpus;

        private byte[] _utf8Bytes;
        private char[] _utf16Chars;

        [GlobalSetup]
        public void Setup()
        {
            // _utf8Bytes = Encoding.UTF8.GetBytes("αδαμάντινης");
            _utf8Bytes = File.ReadAllBytes(SampleTextsFolder + Corpus);

            if (_utf8Bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
            {
                _utf8Bytes = _utf8Bytes[Encoding.UTF8.Preamble.Length..];
            }

            _utf16Chars = new char[Encoding.UTF8.GetCharCount(_utf8Bytes)];
            // _utf8Bytes = Encoding.UTF8.GetBytes(s[..StringLength]);
            //string s = new string('é', 17170);
            //_utf8Bytes = Encoding.UTF8.GetBytes(s);
            // _utf8Bytes = File.ReadAllBytes(@"C:\Users\levib\source\repos\fast-utf8\FastUtf8Tester\SampleTexts\30774-0.txt");
        }

        //[Benchmark(Baseline = true)]
        //public int GetCharCount_Base() => Encoding.UTF8.GetCharCount(_utf8Bytes);

        //[Benchmark(Baseline = false)]
        //public int GetCharCount_Exp1() => GetUtf16CharCountFromKnownWellFormedUtf8(_utf8Bytes);

        //[Benchmark(Baseline = true)]
        //public void Transcode_Base() => Encoding.UTF8.GetChars(_utf8Bytes, _utf16Chars);

        //[Benchmark(Baseline = false)]
        //public void Transcode_Exp1() => TranscodeWellFormedUtf8ToUtf16(
        //    utf8SourceLength: (uint)_utf8Bytes.Length,
        //    utf8Source: ref MemoryMarshal.GetReference<byte>(_utf8Bytes),
        //    utf16Destination: ref MemoryMarshal.GetReference<char>(_utf16Chars));

        [Benchmark(Baseline = true)]
        //[Benchmark]
        public bool Utf8_Is_Valid() => Encoding.UTF8.GetCharCount(_utf8Bytes) >= 0;

        [Benchmark]
        public bool Validate_Exp1() => IsWellFormedUtf8(_utf8Bytes);

        //[Benchmark(Baseline = false)]
        //public int GetCharCount_Exp2() => GetUtf16CharCountFromKnownWellFormedUtf8_2(_utf8Bytes);

        private static readonly Vector128<sbyte> VectorOfElementIndices = Vector128.Create(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);

        public static unsafe int GetUtf16CharCountFromKnownWellFormedUtf8(ReadOnlySpan<byte> utf8Data)
        {
            // Remember: the number of resulting UTF-16 chars will never be greater than the number
            // of UTF-8 bytes given well-formed input, so we can get away with casting the final
            // result to an 'int'.

            fixed (byte* pPinnedUtf8Data = &MemoryMarshal.GetReference(utf8Data))
            {
                if (Sse2.IsSupported && Popcnt.IsSupported)
                {
                    // Optimizations via SSE2 & POPCNT are available - use them.

                    Debug.Assert(BitConverter.IsLittleEndian, "SSE2 only supported on little-endian platforms.");
                    Debug.Assert(sizeof(nint) == IntPtr.Size, "nint defined incorrectly.");
                    Debug.Assert(sizeof(nuint) == IntPtr.Size, "nuint defined incorrectly.");

                    byte* pBuffer = pPinnedUtf8Data;
                    nuint bufferLength = (uint)utf8Data.Length;

                    // Optimization: Can we stay in the all-ASCII code paths?

                    nuint utf16CharCount = GetIndexOfFirstNonAsciiByte_Sse2(pBuffer, bufferLength);

                    if (utf16CharCount != bufferLength)
                    {
                        // Found at least one non-ASCII byte, so fall down the slower (but still vectorized) code paths.
                        // Given well-formed UTF-8 input, we can compute the number of resulting UTF-16 code units
                        // using the following formula:
                        //
                        // utf16CharCount = utf8ByteCount - numUtf8ContinuationBytes + numUtf8FourByteHeaders

                        utf16CharCount = bufferLength;

                        Vector128<sbyte> vecAllC0 = Vector128.Create(unchecked((sbyte)0xC0));
                        Vector128<sbyte> vecAll80 = Vector128.Create(unchecked((sbyte)0x80));
                        Vector128<sbyte> vecAll6F = Vector128.Create(unchecked((sbyte)0x6F));

                        {
                            // Perform an aligned read of the first part of the buffer.
                            // We'll mask out any data at the start of the buffer we don't care about.
                            //
                            // For example, if (pBuffer MOD 16) = 2:
                            // [ AA BB CC DD ... ] <-- original vector
                            // [ 00 00 CC DD ... ] <-- after PANDN operation

                            nint offset = -((nint)pBuffer & (sizeof(Vector128<sbyte>) - 1));
                            Vector128<sbyte> shouldBeMaskedOut = Sse2.CompareGreaterThan(Vector128.Create((byte)((int)offset + sizeof(Vector128<sbyte>) - 1)).AsSByte(), VectorOfElementIndices);
                            Vector128<sbyte> thisVector = Sse2.AndNot(shouldBeMaskedOut, Unsafe.Read<Vector128<sbyte>>(pBuffer + offset));

                            // If there's any data at the end of the buffer we don't care about, mask it out now.
                            // If this happens the 'bufferLength' value will be a lie, but it'll cause all of the
                            // branches later in the method to be skipped, so it's not a huge problem.

                            if (bufferLength < (nuint)offset + (uint)sizeof(Vector128<sbyte>))
                            {
                                Vector128<sbyte> shouldBeAllowed = Sse2.CompareLessThan(VectorOfElementIndices, Vector128.Create((byte)((int)bufferLength - (int)offset)).AsSByte());
                                thisVector = Sse2.And(shouldBeAllowed, thisVector);
                                bufferLength = (nuint)offset + (uint)sizeof(Vector128<sbyte>);
                            }

                            uint maskOfContinuationBytes = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(vecAllC0, thisVector));
                            uint countOfContinuationBytes = Popcnt.PopCount(maskOfContinuationBytes);
                            utf16CharCount -= countOfContinuationBytes;

                            uint maskOfFourByteHeaders = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(thisVector, vecAll80), vecAll6F));
                            uint countOfFourByteHeaders = Popcnt.PopCount(maskOfFourByteHeaders);
                            utf16CharCount += countOfFourByteHeaders;

                            bufferLength -= (nuint)offset;
                            bufferLength -= (uint)sizeof(Vector128<sbyte>);

                            pBuffer += offset;
                            pBuffer += (uint)sizeof(Vector128<sbyte>);
                        }

                        // At this point, pBuffer is guaranteed aligned.

                        Debug.Assert((nuint)pBuffer % (uint)sizeof(Vector128<sbyte>) == 0, "pBuffer should have been aligned.");

                        while (bufferLength >= (uint)sizeof(Vector128<sbyte>))
                        {
                            Vector128<sbyte> thisVector = Sse2.LoadAlignedVector128((sbyte*)pBuffer);

                            uint maskOfContinuationBytes = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(vecAllC0, thisVector));
                            uint countOfContinuationBytes = Popcnt.PopCount(maskOfContinuationBytes);
                            utf16CharCount -= countOfContinuationBytes;

                            uint maskOfFourByteHeaders = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(thisVector, vecAll80), vecAll6F));
                            uint countOfFourByteHeaders = Popcnt.PopCount(maskOfFourByteHeaders);
                            utf16CharCount += countOfFourByteHeaders;

                            pBuffer += sizeof(Vector128<sbyte>);
                            bufferLength -= (uint)sizeof(Vector128<sbyte>);
                        }

                        if ((uint)bufferLength > 0)
                        {
                            // There's still more data to be read.
                            // We need to mask out elements of the vector we don't care about.
                            // These elements will occur at the end of the vector.
                            //
                            // For example, if 14 bytes remain in the input stream:
                            // [ ... CC DD EE FF ] <-- original vector
                            // [ ... CC DD 00 00 ] <-- after PANDN operation

                            Vector128<sbyte> shouldBeMaskedOut = Sse2.CompareGreaterThan(VectorOfElementIndices, Vector128.Create((byte)((int)bufferLength - 1)).AsSByte());
                            Vector128<sbyte> thisVector = Sse2.AndNot(shouldBeMaskedOut, *(Vector128<sbyte>*)pBuffer);

                            uint maskOfContinuationBytes = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(vecAllC0, thisVector));
                            uint countOfContinuationBytes = Popcnt.PopCount(maskOfContinuationBytes);
                            utf16CharCount -= countOfContinuationBytes;

                            uint maskOfFourByteHeaders = (uint)Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(thisVector, vecAll80), vecAll6F));
                            uint countOfFourByteHeaders = Popcnt.PopCount(maskOfFourByteHeaders);
                            utf16CharCount += countOfFourByteHeaders;
                        }
                    }

                    return (int)utf16CharCount;
                }
                else
                {
                    // Cannot use SSE2 & POPCNT. Fall back to slower code paths.

                    throw new NotImplementedException();
                }
            }
        }

        public static void TranscodeWellFormedUtf8ToUtf16(ref byte utf8Source, nuint utf8SourceLength, ref char utf16Destination)
        {
            while (utf8SourceLength > 0)
            {
            DoItAgain:

                uint thisCodePoint = utf8Source;
                if ((thisCodePoint & 0x80) == 0)
                {
                    utf16Destination = (char)thisCodePoint;

                    if (utf8SourceLength == 1)
                    {
                        break;
                    }

                    utf8Source = ref Unsafe.Add(ref utf8Source, 1);
                    utf8SourceLength--;
                    utf16Destination = ref Unsafe.Add(ref utf16Destination, 1);
                    goto DoItAgain;
                }
                else if ((thisCodePoint & 0x20) == 0) // 110xxxxx
                {




                    thisCodePoint <<= 6;
                    thisCodePoint += Unsafe.Add(ref utf8Source, 1);
                    thisCodePoint -= (0xC0 << 6) + 0x80;
                TightLoop:
                    utf16Destination = (char)thisCodePoint;

                    if (utf8SourceLength == 2)
                    {
                        break;
                    }

                    utf8Source = ref Unsafe.Add(ref utf8Source, 2);
                    utf8SourceLength -= 2;
                    utf16Destination = ref Unsafe.Add(ref utf16Destination, 1);

                    if ((sbyte)utf8Source < unchecked((sbyte)0xE0))
                    {
                        uint re = BinaryPrimitives.ReverseEndianness((uint)Unsafe.ReadUnaligned<ushort>(ref utf8Source));
                        thisCodePoint = Bmi2.ParallelBitExtract(re, 0b00011111_00111111_00000000_00000000);
                        goto TightLoop;
                    }

                    goto DoItAgain;
                }
                else if ((thisCodePoint & 0x10) == 0) // 1110xxxx
                {
                    uint re = BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<uint>(ref utf8Source));
                    thisCodePoint = Bmi2.ParallelBitExtract(re, 0b00001111_00111111_00111111_00000000);

                    //thisCodePoint <<= 12;
                    //thisCodePoint += (uint)Unsafe.Add(ref utf8Source, 1) << 6;
                    //thisCodePoint += Unsafe.Add(ref utf8Source, 2);
                    //thisCodePoint -= (0xE0 << 12) + (0x80 << 6) + 0x80;

                    utf16Destination = (char)thisCodePoint;

                    if (utf8SourceLength == 3)
                    {
                        break;
                    }

                    utf8Source = ref Unsafe.Add(ref utf8Source, 3);
                    utf8SourceLength -= 3;
                    utf16Destination = ref Unsafe.Add(ref utf16Destination, 1);
                    goto DoItAgain;
                }
                else // 11110xxx
                {
                    thisCodePoint <<= 18;
                    thisCodePoint += (uint)Unsafe.Add(ref utf8Source, 1) << 12;
                    thisCodePoint += (uint)Unsafe.Add(ref utf8Source, 2) << 6;
                    thisCodePoint += Unsafe.Add(ref utf8Source, 3);
                    thisCodePoint -= (0xF0 << 18) + (0x80 << 12) + (0x80 << 6) + 0x80;

                    utf16Destination = (char)((thisCodePoint >> 10) + 0xD800);
                    Unsafe.Add(ref utf16Destination, 1) = (char)((thisCodePoint & 0x03FF) + 0xDC00);

                    if (utf8SourceLength == 4)
                    {
                        break;
                    }

                    utf8Source = ref Unsafe.Add(ref utf8Source, 4);
                    utf8SourceLength -= 4;
                    utf16Destination = ref Unsafe.Add(ref utf16Destination, 2);
                    goto DoItAgain;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool IsWellFormedUtf8(ReadOnlySpan<byte> buffer)
        {
            fixed (byte* pBuffer = &MemoryMarshal.GetReference(buffer))
            {
                return IsWellFormedUtf8(pBuffer, (uint)buffer.Length);
            }
        }

        private static readonly Vector128<sbyte> Shuf3MinInclusive = Vector128.Create(0xA0, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80).AsSByte();
        private static readonly Vector128<sbyte> Shuf3MaxExclusive = Vector128.Create(0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xA0, 0xC0, 0xC0).AsSByte();

        private static readonly Vector128<sbyte> Shuf4MinInclusive = Vector128.Create(0x90, 0x80, 0x80, 0x80, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00).AsSByte();
        private static readonly Vector128<sbyte> Shuf4MaxExclusive = Vector128.Create(0xC0, 0xC0, 0xC0, 0xC0, 0x90, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00).AsSByte();

        private static bool IsWellFormedUtf8(byte* pBuffer, nuint bufferLength)
        {
            // Optimization: Can we stay in the all-ASCII code paths?

            nuint asciiByteCount = GetIndexOfFirstNonAsciiByte_Sse2(pBuffer, bufferLength);
            if (asciiByteCount == bufferLength)
            {
                return true;
            }

            // Found some non-ASCII data. Must perform manual checks.

            pBuffer += asciiByteCount;
            bufferLength -= asciiByteCount;

            Vector128<sbyte> vecAll0F = Vector128.Create(unchecked((sbyte)0x0F));
            Vector128<sbyte> vecAll61 = Vector128.Create(unchecked((sbyte)0x61));
            Vector128<sbyte> vecAll6F = Vector128.Create(unchecked((sbyte)0x6F));
            Vector128<sbyte> vecAll90 = Vector128.Create(unchecked((sbyte)0x90));
            Vector128<sbyte> vecAllA0 = Vector128.Create(unchecked((sbyte)0xA0));
            Vector128<sbyte> vecAllC0 = Vector128.Create(unchecked((sbyte)0xC0));
            Vector128<sbyte> vecAllE0 = Vector128.Create(unchecked((sbyte)0xE0));
            Vector128<sbyte> vecAllF0 = Vector128.Create(unchecked((sbyte)0xF0));

            while ((nint)bufferLength >= sizeof(Vector128<sbyte>))
            {
                Vector128<sbyte> thisVector = Unsafe.ReadUnaligned<Vector128<sbyte>>(pBuffer);

                // Try a quick all-ASCII check?

                uint combinedMask = (uint)Sse2.MoveMask(thisVector);
                if (combinedMask == 0)
                {
                    pBuffer += sizeof(Vector128<sbyte>);
                    bufferLength -= (uint)sizeof(Vector128<sbyte>);
                    continue;
                }

                combinedMask = (ushort)~combinedMask; // set 1 for all ASCII bytes, 0 for all non-ASCII bytes

                // Get a vector of all the continuation bytes [ 80 .. BF ] we have.

                Vector128<sbyte> continuationBytes = Sse2.CompareGreaterThan(vecAllC0, thisVector);

                // Now get a vector of all the two-byte headers [ C2 .. DF ] we have.

                Vector128<sbyte> twoByteHeaderBytes = Sse2.CompareGreaterThan(Sse2.Add(thisVector, vecAllA0), vecAll61);
                Vector128<sbyte> validTwoByteSequences = Sse2.And(Sse2.ShiftRightLogical128BitLane(continuationBytes, 1), twoByteHeaderBytes);

                uint twoByteMask = (uint)Sse2.MoveMask(validTwoByteSequences);
                combinedMask += twoByteMask * 4; // set 1 for all ASCII and valid 2-byte sequences, 0 otherwise
                combinedMask -= twoByteMask;

                if (((combinedMask + 1) & 0x3FFF) == 0)
                {
                    nuint stride = Bmi1.TrailingZeroCount(~combinedMask);
                    pBuffer += stride;
                    bufferLength -= stride;
                    continue;
                }

                {
                    // Now get a vector of all the three-byte headers [ E0 .. EF ] we have.

                    // Vector128<sbyte> threeByteHeaderBytes = 

                    Vector128<sbyte> shifted = Sse2.ShiftRightLogical128BitLane(thisVector, 1);
                    Vector128<sbyte> shiftedX = Sse2.ShiftRightLogical128BitLane(continuationBytes, 2);

                    Vector128<sbyte> minInclusive = Sse41.BlendVariable(
                        Vector128.Create(unchecked((sbyte)0x80)),
                        Vector128.Create(unchecked((sbyte)0xA0)),
                        Sse2.CompareEqual(thisVector, Vector128.Create(unchecked((sbyte)0xE0))));

                    Vector128<sbyte> maxExclusive = Sse41.BlendVariable(
                        Vector128.Create(unchecked((sbyte)0xC0)),
                        Vector128.Create(unchecked((sbyte)0xA0)),
                        Sse2.CompareEqual(thisVector, Vector128.Create(unchecked((sbyte)0xED))));

                    Vector128<sbyte> xyz =
                        Sse2.AndNot(
                            Sse2.CompareGreaterThan(minInclusive, shifted),
                            Sse2.CompareGreaterThan(maxExclusive, shifted));

                    xyz = Sse2.And(xyz, shiftedX);

                    Vector128<sbyte> validThreeByteSequences = Sse2.CompareGreaterThan(Sse2.Add(thisVector, vecAll90), vecAll6F);
                    validThreeByteSequences = Sse2.And(validThreeByteSequences, xyz);


                    //Vector128<sbyte> mask = Sse2.Subtract(thisVector, vecAllE0);
                    //Vector128<sbyte> bytesToMaskOut = Sse2.CompareGreaterThan(mask, vecAll0F);
                    //mask = Sse2.Or(mask, bytesToMaskOut);

                    //Vector128<sbyte> shufMinIncl = Ssse3.Shuffle(Shuf3MinInclusive, mask);
                    //Vector128<sbyte> shufMaxExcl = Ssse3.Shuffle(Shuf3MaxExclusive, mask);
                    //Vector128<sbyte> shifted = Sse2.ShiftRightLogical128BitLane(thisVector, 1);

                    //Vector128<sbyte> validThreeByteSequences =
                    //    Sse2.And(
                    //        Sse2.ShiftRightLogical128BitLane(continuationBytes, 2),
                    //        );

                    uint threeByteMask = (uint)Sse2.MoveMask(validThreeByteSequences);
                    combinedMask += threeByteMask * 8; // set 1 for all ASCII and valid 2 or 3-byte sequences, 0 otherwise
                    combinedMask -= threeByteMask;

                    if (((combinedMask + 1) & 0x1FFF) == 0)
                    {
                        nuint stride = Bmi1.TrailingZeroCount(~combinedMask);
                        pBuffer += stride;
                        bufferLength -= stride;
                        continue;
                    }
                }

                {
                    // Now get a vector of all the four-byte headers [ F0 .. F4 ] we have.

                    Vector128<sbyte> mask = Sse2.Subtract(thisVector, vecAllF0);
                    Vector128<sbyte> bytesToMaskOut = Sse2.CompareGreaterThan(mask, vecAll0F);
                    mask = Sse2.Or(mask, bytesToMaskOut);

                    Vector128<sbyte> shufMinIncl = Ssse3.Shuffle(Shuf4MinInclusive, mask);
                    Vector128<sbyte> shufMaxExcl = Ssse3.Shuffle(Shuf4MaxExclusive, mask);
                    Vector128<sbyte> shifted = Sse2.ShiftRightLogical128BitLane(thisVector, 1);

                    Vector128<sbyte> validFourByteSequences =
                        Sse2.And(
                            Sse2.And(
                                Sse2.ShiftRightLogical128BitLane(continuationBytes, 2),
                                Sse2.ShiftRightLogical128BitLane(continuationBytes, 3)),
                            Sse2.AndNot(
                                Sse2.CompareGreaterThan(shufMinIncl, shifted),
                                Sse2.CompareGreaterThan(shufMaxExcl, shifted)));

                    uint fourByteMask = (uint)Sse2.MoveMask(validFourByteSequences);
                    combinedMask += fourByteMask * 16; // set 1 for all ASCII and valid 2, 3, or 4-byte sequences, 0 otherwise
                    combinedMask -= fourByteMask;

                    if ((combinedMask & 1) == 0)
                    {
                        return false; // we didn't even make a single byte of progress - must have seen bad data
                    }

                    nuint stride = Bmi1.TrailingZeroCount(~combinedMask);
                    pBuffer += stride;
                    bufferLength -= stride;
                    continue;
                }
            }

            return true;
        }

        private static unsafe nuint GetIndexOfFirstNonAsciiByte_Sse2(byte* pBuffer, nuint bufferLength)
        {
            // JIT turns the below into constants

            uint SizeOfVector128 = (uint)Unsafe.SizeOf<Vector128<byte>>();
            nuint MaskOfAllBitsInVector128 = (nuint)(SizeOfVector128 - 1);

            Debug.Assert(Sse2.IsSupported, "Should've been checked by caller.");
            Debug.Assert(BitConverter.IsLittleEndian, "SSE2 assumes little-endian.");

            uint currentMask, secondMask;
            byte* pOriginalBuffer = pBuffer;

            // This method is written such that control generally flows top-to-bottom, avoiding
            // jumps as much as possible in the optimistic case of a large enough buffer and
            // "all ASCII". If we see non-ASCII data, we jump out of the hot paths to targets
            // after all the main logic.

            if (bufferLength < SizeOfVector128)
            {
                goto InputBufferLessThanOneVectorInLength; // can't vectorize; drain primitives instead
            }

            // Read the first vector unaligned.

            currentMask = (uint)Sse2.MoveMask(Sse2.LoadVector128(pBuffer)); // unaligned load

            if (currentMask != 0)
            {
                goto FoundNonAsciiDataInCurrentMask;
            }

            // If we have less than 32 bytes to process, just go straight to the final unaligned
            // read. There's no need to mess with the loop logic in the middle of this method.

            if (bufferLength < 2 * SizeOfVector128)
            {
                goto IncrementCurrentOffsetBeforeFinalUnalignedVectorRead;
            }

            // Now adjust the read pointer so that future reads are aligned.

            pBuffer = (byte*)(((nuint)pBuffer + SizeOfVector128) & ~(nuint)MaskOfAllBitsInVector128);

#if DEBUG
            long numBytesRead = pBuffer - pOriginalBuffer;
            Debug.Assert(0 < numBytesRead && numBytesRead <= SizeOfVector128, "We should've made forward progress of at least one byte.");
            Debug.Assert((nuint)numBytesRead <= bufferLength, "We shouldn't have read past the end of the input buffer.");
#endif

            // Adjust the remaining length to account for what we just read.

            bufferLength += (nuint)pOriginalBuffer;
            bufferLength -= (nuint)pBuffer;

            // The buffer is now properly aligned.
            // Read 2 vectors at a time if possible.

            if (bufferLength >= 2 * SizeOfVector128)
            {
                byte* pFinalVectorReadPos = (byte*)((nuint)pBuffer + bufferLength - 2 * SizeOfVector128);

                // After this point, we no longer need to update the bufferLength value.

                do
                {
                    Vector128<byte> firstVector = Sse2.LoadAlignedVector128(pBuffer);
                    Vector128<byte> secondVector = Sse2.LoadAlignedVector128(pBuffer + SizeOfVector128);

                    currentMask = (uint)Sse2.MoveMask(firstVector);
                    secondMask = (uint)Sse2.MoveMask(secondVector);

                    if ((currentMask | secondMask) != 0)
                    {
                        goto FoundNonAsciiDataInInnerLoop;
                    }

                    pBuffer += 2 * SizeOfVector128;
                } while (pBuffer <= pFinalVectorReadPos);
            }

            // We have somewhere between 0 and (2 * vector length) - 1 bytes remaining to read from.
            // Since the above loop doesn't update bufferLength, we can't rely on its absolute value.
            // But we _can_ rely on it to tell us how much remaining data must be drained by looking
            // at what bits of it are set. This works because had we updated it within the loop above,
            // we would've been adding 2 * SizeOfVector128 on each iteration, but we only care about
            // bits which are less significant than those that the addition would've acted on.

            // If there is fewer than one vector length remaining, skip the next aligned read.

            if ((bufferLength & SizeOfVector128) == 0)
            {
                goto DoFinalUnalignedVectorRead;
            }

            // At least one full vector's worth of data remains, so we can safely read it.
            // Remember, at this point pBuffer is still aligned.

            currentMask = (uint)Sse2.MoveMask(Sse2.LoadAlignedVector128(pBuffer));
            if (currentMask != 0)
            {
                goto FoundNonAsciiDataInCurrentMask;
            }

        IncrementCurrentOffsetBeforeFinalUnalignedVectorRead:

            pBuffer += SizeOfVector128;

        DoFinalUnalignedVectorRead:

            if (((byte)bufferLength & MaskOfAllBitsInVector128) != 0)
            {
                // Perform an unaligned read of the last vector.
                // We need to adjust the pointer because we're re-reading data.

                pBuffer += (bufferLength & MaskOfAllBitsInVector128) - SizeOfVector128;

                currentMask = (uint)Sse2.MoveMask(Sse2.LoadVector128(pBuffer)); // unaligned load
                if (currentMask != 0)
                {
                    goto FoundNonAsciiDataInCurrentMask;
                }

                pBuffer += SizeOfVector128;
            }

        Finish:

            return (nuint)pBuffer - (nuint)pOriginalBuffer; // and we're done!

        FoundNonAsciiDataInInnerLoop:

            // If the current (first) mask isn't the mask that contains non-ASCII data, then it must
            // instead be the second mask. If so, skip the entire first mask and drain ASCII bytes
            // from the second mask.

            if (currentMask == 0)
            {
                pBuffer += SizeOfVector128;
                currentMask = secondMask;
            }

        FoundNonAsciiDataInCurrentMask:

            // The mask contains - from the LSB - a 0 for each ASCII byte we saw, and a 1 for each non-ASCII byte.
            // Tzcnt is the correct operation to count the number of zero bits quickly. If this instruction isn't
            // available, we'll fall back to a normal loop.

            Debug.Assert(currentMask != 0, "Shouldn't be here unless we see non-ASCII data.");
            pBuffer += (uint)BitOperations.TrailingZeroCount(currentMask);

            goto Finish;

        FoundNonAsciiDataInCurrentDWord:

            uint currentDWord;
            Debug.Assert(!AllBytesInUInt32AreAscii(currentDWord), "Shouldn't be here unless we see non-ASCII data.");
            pBuffer += CountNumberOfLeadingAsciiBytesFromUInt32WithSomeNonAsciiData(currentDWord);

            goto Finish;

        InputBufferLessThanOneVectorInLength:

            // These code paths get hit if the original input length was less than one vector in size.
            // We can't perform vectorized reads at this point, so we'll fall back to reading primitives
            // directly. Note that all of these reads are unaligned.

            Debug.Assert(bufferLength < SizeOfVector128);

            // QWORD drain

            if ((bufferLength & 8) != 0)
            {
                if (Bmi1.X64.IsSupported)
                {
                    // If we can use 64-bit tzcnt to count the number of leading ASCII bytes, prefer it.

                    ulong candidateUInt64 = Unsafe.ReadUnaligned<ulong>(pBuffer);
                    if (!AllBytesInUInt64AreAscii(candidateUInt64))
                    {
                        // Clear everything but the high bit of each byte, then tzcnt.
                        // Remember the / 8 at the end to convert bit count to byte count.

                        candidateUInt64 &= UInt64HighBitsOnlyMask;
                        pBuffer += (nuint)(Bmi1.X64.TrailingZeroCount(candidateUInt64) / 8);
                        goto Finish;
                    }
                }
                else
                {
                    // If we can't use 64-bit tzcnt, no worries. We'll just do 2x 32-bit reads instead.

                    currentDWord = Unsafe.ReadUnaligned<uint>(pBuffer);
                    uint nextDWord = Unsafe.ReadUnaligned<uint>(pBuffer + 4);

                    if (!AllBytesInUInt32AreAscii(currentDWord | nextDWord))
                    {
                        // At least one of the values wasn't all-ASCII.
                        // We need to figure out which one it was and stick it in the currentMask local.

                        if (AllBytesInUInt32AreAscii(currentDWord))
                        {
                            currentDWord = nextDWord; // this one is the culprit
                            pBuffer += 4;
                        }

                        goto FoundNonAsciiDataInCurrentDWord;
                    }
                }

                pBuffer += 8; // successfully consumed 8 ASCII bytes
            }

            // DWORD drain

            if ((bufferLength & 4) != 0)
            {
                currentDWord = Unsafe.ReadUnaligned<uint>(pBuffer);

                if (!AllBytesInUInt32AreAscii(currentDWord))
                {
                    goto FoundNonAsciiDataInCurrentDWord;
                }

                pBuffer += 4; // successfully consumed 4 ASCII bytes
            }

            // WORD drain
            // (We movzx to a DWORD for ease of manipulation.)

            if ((bufferLength & 2) != 0)
            {
                currentDWord = Unsafe.ReadUnaligned<ushort>(pBuffer);

                if (!AllBytesInUInt32AreAscii(currentDWord))
                {
                    // We only care about the 0x0080 bit of the value. If it's not set, then we
                    // increment currentOffset by 1. If it's set, we don't increment it at all.

                    pBuffer += (nuint)((nint)(sbyte)currentDWord >> 7) + 1;
                    goto Finish;
                }

                pBuffer += 2; // successfully consumed 2 ASCII bytes
            }

            // BYTE drain

            if ((bufferLength & 1) != 0)
            {
                // sbyte has non-negative value if byte is ASCII.

                if (*(sbyte*)(pBuffer) >= 0)
                {
                    pBuffer++; // successfully consumed a single byte
                }
            }

            goto Finish;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool AllBytesInUInt32AreAscii(uint value)
        {
            // If the high bit of any byte is set, that byte is non-ASCII.

            return (value & UInt32HighBitsOnlyMask) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllBytesInUInt64AreAscii(ulong value)
        {
            // If the high bit of any byte is set, that byte is non-ASCII.

            return (value & UInt64HighBitsOnlyMask) == 0;
        }

        private const uint UInt32HighBitsOnlyMask = 0x80808080u;
        private const ulong UInt64HighBitsOnlyMask = 0x80808080_80808080ul;

        /// <summary>
        /// Given a DWORD which represents a four-byte buffer read in machine endianness, and which
        /// the caller has asserted contains a non-ASCII byte *somewhere* in the data, counts the
        /// number of consecutive ASCII bytes starting from the beginning of the buffer. Returns
        /// a value 0 - 3, inclusive. (The caller is responsible for ensuring that the buffer doesn't
        /// contain all-ASCII data.)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint CountNumberOfLeadingAsciiBytesFromUInt32WithSomeNonAsciiData(uint value)
        {
            Debug.Assert(!AllBytesInUInt32AreAscii(value), "Caller shouldn't provide an all-ASCII value.");

            // Use BMI1 directly rather than going through BitOperations. We only see a perf gain here
            // if we're able to emit a real tzcnt instruction; the software fallback used by BitOperations
            // is too slow for our purposes since we can provide our own faster, specialized software fallback.

            if (Bmi1.IsSupported)
            {
                Debug.Assert(BitConverter.IsLittleEndian);
                return Bmi1.TrailingZeroCount(value & UInt32HighBitsOnlyMask) >> 3;
            }

            // Couldn't emit tzcnt, use specialized software fallback.
            // The 'allBytesUpToNowAreAscii' DWORD uses bit twiddling to hold a 1 or a 0 depending
            // on whether all processed bytes were ASCII. Then we accumulate all of the
            // results to calculate how many consecutive ASCII bytes are present.

            value = ~value;

            if (BitConverter.IsLittleEndian)
            {
                // Read first byte
                value >>= 7;
                uint allBytesUpToNowAreAscii = value & 1;
                uint numAsciiBytes = allBytesUpToNowAreAscii;

                // Read second byte
                value >>= 8;
                allBytesUpToNowAreAscii &= value;
                numAsciiBytes += allBytesUpToNowAreAscii;

                // Read third byte
                value >>= 8;
                allBytesUpToNowAreAscii &= value;
                numAsciiBytes += allBytesUpToNowAreAscii;

                return numAsciiBytes;
            }
            else
            {
                // BinaryPrimitives.ReverseEndianness is only implemented as an intrinsic on
                // little-endian platforms, so using it in this big-endian path would be too
                // expensive. Instead we'll just change how we perform the shifts.

                // Read first byte
                value = BitOperations.RotateLeft(value, 1);
                uint allBytesUpToNowAreAscii = value & 1;
                uint numAsciiBytes = allBytesUpToNowAreAscii;

                // Read second byte
                value = BitOperations.RotateLeft(value, 8);
                allBytesUpToNowAreAscii &= value;
                numAsciiBytes += allBytesUpToNowAreAscii;

                // Read third byte
                value = BitOperations.RotateLeft(value, 8);
                allBytesUpToNowAreAscii &= value;
                numAsciiBytes += allBytesUpToNowAreAscii;

                return numAsciiBytes;
            }
        }

    }

    //[MemoryDiagnoser]
    //public class Utf8StringRunner
    //{
    //    private string _str;
    //    private Utf8String _u8str;
    //    private byte[] _utf8Bytes;

    //    [Params(0, 8, 32, 128)]
    //    public int StringLength;

    //    [GlobalSetup]
    //    public void Setup()
    //    {
    //        _str = new string('x', StringLength);
    //        _u8str = new Utf8String(_str);
    //        _utf8Bytes = _u8str.ToByteArray();
    //    }

    //    [Benchmark(Baseline = true)]
    //    public int StringGetHashCode()
    //    {
    //        return _str.GetHashCode();
    //    }

    //    [Benchmark]
    //    public unsafe int Utf8StringGetHashCode()
    //    {
    //        return _u8str.GetHashCode();
    //    }
    //}
}
