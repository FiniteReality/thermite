using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;
using Thermite.Natives;
using static Thermite.Natives.MiniMp3;

namespace Thermite.Transcoders.Mpeg
{
    internal sealed class MpegAudioTranscoder : IAudioTranscoder
    {
        private MpegAudioCodec _codec;
        private readonly PipeReader _input;
        private readonly Pipe _outputPipe;

        public PipeReader Output => _outputPipe.Reader;

        internal MpegAudioTranscoder(PipeReader input, MpegAudioCodec codec)
        {
            _codec = codec;
            _input = input;
            _outputPipe = new Pipe();
        }

        public async Task RunAsync(
            CancellationToken cancellationToken = default)
        {
            mp3dec_t decoder;

            unsafe
            {
                mp3dec_init(&decoder);
            }

            try
            {
                FlushResult flushResult = default;
                ReadResult readResult = default;
                while (!flushResult.IsCompleted)
                {
                    readResult = await _input.ReadAsync();
                    var buffer = readResult.Buffer;

                    if (buffer.IsEmpty && readResult.IsCompleted)
                        return;

                    while (TryReadFrame(ref buffer, out var frame))
                    {
                        var block = _outputPipe.Writer.GetMemory(
                            MINIMP3_MAX_SAMPLES_PER_FRAME * sizeof(short)
                            + sizeof(short));

                        var success = TryProcessFrame(frame, ref decoder,
                            block.Span, out var bytesWritten);

                        _outputPipe.Writer.Advance(bytesWritten);

                        // invalid data was passed, so bail out
                        if (!success)
                            return;
                    }

                    flushResult = await _outputPipe.Writer.FlushAsync(
                        cancellationToken);
                    _input.AdvanceTo(buffer.Start, buffer.End);
                }
            }
            finally
            {
                _ = await _outputPipe.Writer.FlushAsync(cancellationToken);
                await _input.CompleteAsync();
                await _outputPipe.Writer.CompleteAsync();
            }

            static unsafe bool TryProcessFrame(
                ReadOnlySequence<byte> buffer, ref mp3dec_t decoder,
                Span<byte> destination, out int bytesWritten)
            {
                bytesWritten = default;

                mp3dec_frame_info_t frameInfo;
                int samples;

                fixed (mp3dec_t* decoderPtr = &decoder)
                fixed (byte* input = buffer.FirstSpan)
                fixed (short* output = MemoryMarshal.Cast<byte, short>(
                    destination))
                    samples = mp3dec_decode_frame(decoderPtr, input,
                    buffer.FirstSpan.Length, output + 1, &frameInfo);

                if (samples == 0 && frameInfo.frame_bytes == 0)
                    return false;

                bytesWritten = samples * sizeof(short) * frameInfo.channels;

                if (!BinaryPrimitives.TryWriteInt16LittleEndian(
                    destination, (short)bytesWritten))
                    return false;

                bytesWritten += sizeof(short);

                return true;
            }
        }

        public ValueTask<IAudioCodec> GetOutputCodecAsync(
            CancellationToken cancellationToken = default)
            => new ValueTask<IAudioCodec>(
                new PcmAudioCodec(
                    bitDepth: sizeof(short) * 8,
                    _codec.ChannelCount,
                    SampleEndianness.LittleEndian,
                    SampleFormat.SignedInteger,
                    _codec.SamplingRate));

        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
