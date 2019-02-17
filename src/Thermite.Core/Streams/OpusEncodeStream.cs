using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Core.Natives;
using Voltaic;

namespace Thermite.Core.Streams
{
    public class OpusEncodeStream : ThermiteStream
    {
        public const int SampleRate = 48000;
        public const int Channels = 2;
        public const int FrameMillis = 20;

        public const int FrameSamplesPerChannel = SampleRate / 1000 * FrameMillis;
        public const int FrameSamples = FrameSamplesPerChannel * Channels;
        public const int FrameBytes = FrameSamplesPerChannel * sizeof(short) * Channels;


        private readonly Stream _source;
        private readonly OpusEncoder _encoder;

        public OpusEncodeStream(Stream source)
        {
            _source = source;
            _encoder = Opus.CreateEncoder(SampleRate, Channels,
                Opus.Application.MusicOrMixed);
        }

        public override async Task<ReadResult> ReadAsync(
            ArraySegment<byte> array, CancellationToken cancelToken)
        {
            var memory = new ResizableMemory<byte>(FrameBytes * 2);

            try
            {
                int read = 0;
                while (read < FrameBytes)
                {
                    var block = memory.RequestSegment(FrameBytes - read);
                    var thisRead = await _source.ReadAsync(block.Array,
                        block.Offset, block.Count, cancelToken)
                        .ConfigureAwait(false);

                    if (thisRead <= 0)
                        break;

                    memory.Advance(thisRead);
                    read += thisRead;
                }

                if (read <= 0)
                    return new ReadResult();

                var encodedBytes = _encoder.Encode(memory.AsSpan(),
                    FrameSamplesPerChannel, array.AsSpan());

                if (encodedBytes <= 0)
                    return new ReadResult();

                var samples = (uint)Opus.GetSamplesPerFrame(
                    array.AsSpan().Slice(0, encodedBytes), SampleRate);
                var time = TimeSpan.FromMilliseconds(
                    (1000d / SampleRate) * samples);

                return new ReadResult(encodedBytes, time, samples);
            }
            finally
            {
                memory.Return();
            }
        }
    }
}