using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;

using static Thermite.Internal.FrameParsingUtilities;

namespace Thermite.Transcoders.Pcm
{
    internal sealed class PcmFloatTranscoder<T> : IAudioTranscoder
        where T : unmanaged
    {
        private static float MaxSignedValue { get; } = GetMaxSignedValue();

        private static float GetMaxSignedValue()
        {
            if (typeof(T) == typeof(sbyte))
                return sbyte.MaxValue;
            else if (typeof(T) == typeof(byte))
                return sbyte.MaxValue;
            else if (typeof(T) == typeof(short))
                return short.MaxValue;
            else if (typeof(T) == typeof(ushort))
                return short.MaxValue;
            else if (typeof(T) == typeof(int))
                return int.MaxValue;
            else if (typeof(T) == typeof(uint))
                return int.MaxValue;
            else
            {
                Debug.Fail(
                    $"Invalid {nameof(T)} passed to " +
                    $"{nameof(PcmFloatTranscoder<T>)}");

                return 0;
            }
        }

        private readonly PipeReader _input;
        private readonly PcmAudioCodec _codec;
        private readonly Pipe _pipe;

        public PipeReader Output => _pipe.Reader;

        internal PcmFloatTranscoder(PipeReader input,
            PcmAudioCodec codec)
        {
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

                var destination = writer.GetSpan(sizeof(short) +
                    (int)frame.Length * (sizeof(float) / sizeof(T)));
                var pcmDestination = destination.Slice(sizeof(short));
                var bytesWritten = 0;

                Unsafe.SkipInit(out Vector<T> vectorInput);
                for (var x = 0; x < elements; x++)
                {
                    ReadOnlySequence<byte> sampleBlock = frame.Slice(
                        x * sizeof(Vector<T>), sizeof(Vector<T>));

                    var block = Unsafe.AsPointer(ref vectorInput);
                    var writableBuffer = new Span<byte>(
                        block, sizeof(Vector<T>));

                    sampleBlock.CopyTo(writableBuffer);

                    if (!TryWriteAsFloat(vectorInput, pcmDestination,
                        ref bytesWritten))
                        return false;

                    pcmDestination = pcmDestination.Slice(bytesWritten);
                }

                Span<float> remainingDestination =
                    MemoryMarshal.Cast<byte, float>(pcmDestination);
                SequenceReader<byte> remaining = new SequenceReader<byte>(
                    frame.Slice(elements * sizeof(Vector<T>)));

                for (int x = 0; !remaining.End; x++)
                {
                    if (typeof(T) == typeof(sbyte))
                    {
                        if (!remaining.TryRead(out byte value))
                            return false;

                        remainingDestination[x] =
                            (sbyte)value / MaxSignedValue;
                    }
                    else if (typeof(T) == typeof(byte))
                    {
                        if (!remaining.TryRead(out byte value))
                            return false;

                        remainingDestination[x] =
                            (value - MaxSignedValue) / MaxSignedValue;
                    }
                    else if (typeof(T) == typeof(short))
                    {
                        if (!remaining.TryReadLittleEndian(out short value))
                            return false;

                        remainingDestination[x] = value / MaxSignedValue;
                    }
                    else if (typeof(T) == typeof(ushort))
                    {
                        if (!remaining.TryReadLittleEndian(out short value))
                            return false;

                        remainingDestination[x] =
                            ((ushort)value - MaxSignedValue) / MaxSignedValue;
                    }
                    else if (typeof(T) == typeof(int))
                    {
                        if (!remaining.TryReadLittleEndian(out int value))
                            return false;

                        remainingDestination[x] = value / MaxSignedValue;
                    }
                    else if (typeof(T) == typeof(uint))
                    {
                        if (!remaining.TryReadLittleEndian(out int value))
                            return false;

                        remainingDestination[x] =
                            ((uint)value - MaxSignedValue) / MaxSignedValue;
                    }
                }

                if (!BinaryPrimitives.TryWriteInt16LittleEndian(
                    destination, (short)bytesWritten))
                    return false;

                writer.Advance(bytesWritten + sizeof(short));
                return true;
            }

            static bool TryWriteAsFloat<U>(Vector<U> input,
                Span<byte> destination, ref int bytesWritten)
                where U : unmanaged
            {
                if (typeof(U) == typeof(byte))
                {
                    Vector<byte> byteInput = (Vector<byte>)input;

                    Vector.Widen(byteInput, out var lower, out var higher);

                    return TryWriteAsFloat(lower,
                            destination, ref bytesWritten)
                        && TryWriteAsFloat(higher,
                            destination, ref bytesWritten);
                }
                else if (typeof(U) == typeof(sbyte))
                {
                    Vector<sbyte> sbyteInput = (Vector<sbyte>)input;

                    Vector.Widen(sbyteInput, out var lower, out var higher);

                    return TryWriteAsFloat(lower,
                            destination, ref bytesWritten)
                        && TryWriteAsFloat(higher,
                            destination, ref bytesWritten);
                }
                else if (typeof(U) == typeof(short))
                {
                    Vector<short> shortInput = (Vector<short>)input;

                    Vector.Widen(shortInput, out var lower, out var higher);

                    return TryWriteAsFloat(lower,
                            destination, ref bytesWritten)
                        && TryWriteAsFloat(higher,
                            destination, ref bytesWritten);
                }
                else if (typeof(U) == typeof(ushort))
                {
                    Vector<ushort> ushortInput = (Vector<ushort>)input;

                    Vector.Widen(ushortInput, out var lower, out var higher);

                    return TryWriteAsFloat(lower,
                            destination, ref bytesWritten)
                        && TryWriteAsFloat(higher,
                            destination, ref bytesWritten);
                }
                else if (typeof(U) == typeof(int))
                {
                    // input in range [T.MinValue, T.MaxValue) (T is signed)
                    Vector<int> intInput = (Vector<int>)input;
                    Vector<float> maxValue = new Vector<float>(MaxSignedValue);

                    var desired = Vector.ConvertToSingle(intInput);
                    desired /= maxValue; // take to range [-1.0, 1.0)

                    var success = desired.TryCopyTo(
                        destination.Slice(bytesWritten));
                    bytesWritten += Vector<float>.Count * sizeof(float);
                    return success;
                }
                else if (typeof(U) == typeof(uint))
                {
                    // input in range [0, T.MaxValue) (T is unsigned)
                    Vector<uint> intInput = (Vector<uint>)input;
                    Vector<float> maxValue = new Vector<float>(MaxSignedValue);

                    var desired = Vector.ConvertToSingle(intInput);
                    // take to range [T.MinValue, T.MaxValue)
                    desired -= maxValue;
                    // take to range [-1.0, 1.0)
                    desired /= maxValue;

                    var success = desired.TryCopyTo(
                        destination.Slice(bytesWritten));
                    bytesWritten += Vector<float>.Count * sizeof(float);
                    return success;
                }
                else
                    return false;
            }
        }

        public ValueTask<IAudioCodec> GetOutputCodecAsync(
            CancellationToken cancellationToken = default)
            => new ValueTask<IAudioCodec>(
                new PcmAudioCodec(
                    bitDepth: sizeof(float) * 8,
                    _codec.ChannelCount,
                    SampleEndianness.Indeterminate,
                    SampleFormat.FloatingPoint,
                    _codec.SamplingRate));

        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
