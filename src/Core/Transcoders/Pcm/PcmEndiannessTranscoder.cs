using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;

using static Thermite.Internal.FrameParsingUtilities;
using static Thermite.Internal.ThrowHelpers;

namespace Thermite.Transcoders.Pcm
{
    internal sealed class PcmEndiannessTranscoder<T> : IAudioTranscoder
        where T : unmanaged
    {
        private static readonly Vector128<byte> ReverseUInt16Mask
            = Vector128.Create(
                (byte)1, 0,
                3, 2,
                5, 4,
                7, 6,
                9, 8,
                11, 10,
                13, 12,
                15, 14
            );

        private static readonly Vector128<byte> ReverseUInt32Mask
            = Vector128.Create(
                (byte)3, 2, 1, 0,
                7, 6, 5, 4,
                11, 10, 9, 8,
                15, 14, 13, 12
            );

        private readonly PipeReader _input;
        private readonly PcmAudioCodec _codec;
        private readonly Pipe _pipe;

        public PipeReader Output => _pipe.Reader;

        public PcmEndiannessTranscoder(PipeReader input,
            PcmAudioCodec codec)
        {
            Debug.Assert(
                typeof(T) == typeof(ushort) ||
                typeof(T) == typeof(uint));

            Debug.Assert(codec.Endianness == SampleEndianness.BigEndian);

            if (!Avx2.IsSupported &&
                !Ssse3.IsSupported &&
                !Sse2.IsSupported)
                ThrowPlatformNotSupportedException(
                    "Could not find support for AVX2, SSSE3 or SSE2");

            _input = input;
            _codec = codec;
            _pipe = new Pipe();
        }

        public async Task RunAsync(
            CancellationToken cancellationToken = default)
        {
            var writer = _pipe.Writer;
            try
            {
                ReadResult readResult = default;
                FlushResult flushResult = default;

                while (!flushResult.IsCompleted)
                {
                    readResult = await _input.ReadAsync(cancellationToken);
                    var sequence = readResult.Buffer;

                    if (sequence.IsEmpty && readResult.IsCompleted)
                        return;

                    while (TryReadFrame(ref sequence, out var frame))
                    {
                        if (!TryTranscodeFrame(frame, writer))
                            return;
                    }

                    _input.AdvanceTo(sequence.Start, sequence.End);
                    flushResult = await writer.FlushAsync(cancellationToken);
                }
            }
            finally
            {
                await writer.CompleteAsync();
                await _input.CompleteAsync();
            }

            static unsafe bool TryTranscodeFrame(ReadOnlySequence<byte> frame,
                PipeWriter writer)
            {
                var elements = (int)(frame.Length / sizeof(Vector<T>));

                // We're not changing the size of the data, so there'll be
                // exactly the same amount of data out as there was in.
                // (Excluding headers, of course.)
                var destination = writer.GetSpan(
                    sizeof(short) + (int)frame.Length);
                var pcmDestination = destination.Slice(sizeof(short));

                frame.CopyTo(pcmDestination);

                for (var x = 0; x < elements; x++)
                {
                    var block = pcmDestination.Slice(
                            x * Vector<T>.Count * sizeof(T));
                    var vector = new Vector<T>(block);

                    if (sizeof(Vector<T>) >= sizeof(Vector256<T>))
                    {
                        vector = ReverseEndianness256(vector);
                    }
                    else if (sizeof(Vector<T>) >= sizeof(Vector128<T>))
                    {
                        vector = ReverseEndianness128(vector);
                    }

                    if (!vector.TryCopyTo(block))
                        return false;
                }

                var remainingSamples = MemoryMarshal.Cast<byte, T>(
                    pcmDestination.Slice(elements * sizeof(Vector<T>)));
                for (int x = 0; x < remainingSamples.Length; x++)
                {
                    ref var sample = ref remainingSamples[x];

                    if (typeof(T) == typeof(ushort))
                    {
                        ref ushort realSample =
                            ref Unsafe.As<T, ushort>(ref sample);

                        realSample = BinaryPrimitives.ReverseEndianness(
                            realSample);
                    }
                    else if (typeof(T) == typeof(uint))
                    {
                        ref uint realSample =
                            ref Unsafe.As<T, uint>(ref sample);

                        realSample = BinaryPrimitives.ReverseEndianness(
                            realSample);
                    }
                }

                if (!BinaryPrimitives.TryWriteInt16LittleEndian(
                    destination, (short)frame.Length))
                    return false;

                writer.Advance((int)frame.Length + sizeof(short));
                return true;
            }

            static Vector<T> ReverseEndianness256(Vector<T> input)
            {
                var vector = input.AsVector256().AsByte();

                Unsafe.SkipInit(out Vector128<byte> mask);

                if (Avx2.IsSupported || Ssse3.IsSupported)
                {
                    if (typeof(T) == typeof(ushort))
                    {
                        mask = ReverseUInt16Mask;
                    }
                    else if (typeof(T) == typeof(uint))
                    {
                        mask = ReverseUInt32Mask;
                    }
                }

                if (Avx2.IsSupported)
                {
                    var result = Avx2.Shuffle(vector,
                        Vector256.Create(mask, mask));

                    return result.As<byte, T>().AsVector();
                }
                else if (Ssse3.IsSupported)
                {
                    var resultLower = Ssse3.Shuffle(
                        vector.GetLower(), mask);
                    var resultUpper = Ssse3.Shuffle(
                        vector.GetUpper(), mask);

                    var result = Vector256.Create(
                        resultLower, resultUpper);

                    return result.As<byte, T>().AsVector();
                }
                else if (Sse2.IsSupported)
                {
                    var result = Vector256.Create(
                        ReverseEndiannessSse2(vector.GetLower()).AsByte(),
                        ReverseEndiannessSse2(vector.GetUpper()).AsByte()
                    );

                    return result.As<byte, T>().AsVector();
                }
                else
                {
                    Debug.Fail("SSE2 not supported");

                    return default;
                }
            }

            static Vector<T> ReverseEndianness128(Vector<T> input)
            {
                var vector = input.AsVector128().AsByte();

                if (Ssse3.IsSupported)
                {
                    Vector128<byte> mask;

                    if (typeof(T) == typeof(ushort))
                    {
                        mask = ReverseUInt16Mask;
                    }
                    else if (typeof(T) == typeof(uint))
                    {
                        mask = ReverseUInt32Mask;
                    }
                    else
                    {
                        Debug.Fail($"{typeof(T)} wasn't ushort or uint");

                        return default;
                    }

                    var result = Ssse3.Shuffle(vector, mask);
                    return result.As<byte, T>().AsVector();
                }
                else if (Sse2.IsSupported)
                {
                    return ReverseEndiannessSse2(vector).AsVector();
                }
                else
                {
                    Debug.Fail("SSE2 not supported");

                    return default;
                }
            }

            static Vector128<T> ReverseEndiannessSse2(Vector128<byte> input)
            {
                if (typeof(T) == typeof(ushort))
                {
                    var mask = Vector128.Create((ushort)0x00FF);
                    var val1 = input.AsUInt16();
                    var val2 = val1;

                    val1 = Sse2.And(val1, mask);
                    val1 = Sse2.ShiftLeftLogical(val1, 8);
                    val2 = Sse2.ShiftRightLogical(val2, 8);
                    val2 = Sse2.And(val2, mask);

                    return Sse2.Or(val1, val2).As<ushort, T>();
                }
                else if (typeof(T) == typeof(uint))
                {
                    var maskLower = Vector128.Create((uint)0x00FF00FF);
                    var maskUpper = Vector128.Create((uint)0xFF00FF00);
                    var val1 = input.AsUInt32();
                    var val2 = val1;

                    val1 = Sse2.And(val1, maskLower);
                    val1 = Sse2.Or(Sse2.ShiftRightLogical(val1, 8),
                        Sse2.ShiftLeftLogical(val1, 24));

                    val2 = Sse2.And(val2, maskUpper);
                    val2 = Sse2.Or(Sse2.ShiftLeftLogical(val2, 8),
                        Sse2.ShiftRightLogical(val2, 24));

                    return Sse2.Or(val1, val2).As<uint, T>();
                }
                else
                {
                    Debug.Fail($"{typeof(T)} wasn't ushort or uint");

                    return default;
                }
            }
        }

        public ValueTask<IAudioCodec> GetOutputCodecAsync(
            CancellationToken cancellationToken = default)
            => new ValueTask<IAudioCodec>(
                new PcmAudioCodec(
                    _codec.BitDepth,
                    _codec.ChannelCount,
                    SampleEndianness.LittleEndian,
                    _codec.Format,
                    _codec.SamplingRate));

        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
