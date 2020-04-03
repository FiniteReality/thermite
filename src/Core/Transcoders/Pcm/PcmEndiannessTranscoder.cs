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

namespace Thermite.Transcoders.Pcm
{
    internal sealed class PcmEndiannessTranscoder<T> : IAudioTranscoder
        where T : unmanaged
    {
        private static ReadOnlySpan<byte> ReverseUInt16Mask256
            => new byte[]
            {
                1, 0,
                3, 2,
                5, 4,
                7, 6,
                9, 8,
                11, 10,
                13, 12,
                15, 14,
                17, 16,
                19, 18,
                21, 20,
                23, 22,
                25, 24,
                27, 26,
                29, 28,
                31, 30
            };
        private static ReadOnlySpan<byte> ReverseUInt16Mask128
            => ReverseUInt16Mask256.Slice(0, 16);

        private static ReadOnlySpan<byte> ReverseUInt32Mask256
            => new byte[]
            {
                3, 2, 1, 0,
                7, 6, 5, 4,
                11, 10, 9, 8,
                15, 14, 13, 12,
                19, 18, 17, 16,
                23, 22, 21, 20,
                27, 26, 25, 24,
                31, 30, 29, 28
            };
        private static ReadOnlySpan<byte> ReverseUInt32Mask128
            => ReverseUInt32Mask256.Slice(0, 16);

        private readonly PipeReader _input;
        private readonly PcmAudioCodec _codec;
        private readonly Pipe _pipe;

        public PipeReader Output => _pipe.Reader;

        internal PcmEndiannessTranscoder(PipeReader input,
            PcmAudioCodec codec)
        {
            Debug.Assert(
                typeof(T) == typeof(ushort) ||
                typeof(T) == typeof(uint));

            Debug.Assert(Ssse3.IsSupported && Avx2.IsSupported);

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
                        if (!TryWriteReversedBytes256(ref vector))
                            return false;
                    else if (sizeof(Vector<T>) >= sizeof(Vector128<T>))
                        if (!TryWriteReversedBytes128(ref vector))
                            return false;

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

            static bool TryWriteReversedBytes256(ref Vector<T> input)
            {
                var vector = input.AsVector256();

                if (typeof(T) == typeof(ushort))
                {
                    var mask = new Vector<byte>(ReverseUInt16Mask256)
                        .AsVector256();
                    var result = Avx2.Shuffle(
                        vector.AsByte(), mask);

                    input = result.As<byte, T>().AsVector();
                    return true;
                }
                else if (typeof(T) == typeof(uint))
                {
                    var mask = new Vector<byte>(ReverseUInt32Mask256)
                        .AsVector256();
                    var result = Avx2.Shuffle(
                        vector.AsByte(), mask);

                    input = result.As<byte, T>().AsVector();
                    return true;
                }

                return false;
            }

            static bool TryWriteReversedBytes128(ref Vector<T> input)
            {
                var vector = input.AsVector128();

                if (typeof(T) == typeof(ushort))
                {
                    var mask = new Vector<byte>(ReverseUInt16Mask256)
                        .AsVector128();
                    var result = Ssse3.Shuffle(
                        vector.AsByte(), mask);

                    input = result.As<byte, T>().AsVector();
                    return true;
                }
                else if (typeof(T) == typeof(uint))
                {
                    var mask = new Vector<byte>(ReverseUInt32Mask256)
                        .AsVector128();
                    var result = Ssse3.Shuffle(
                        vector.AsByte(), mask);

                    input = result.As<byte, T>().AsVector();
                    return true;
                }

                return false;
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
