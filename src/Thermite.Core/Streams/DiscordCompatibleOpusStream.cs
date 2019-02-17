using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Core.Natives;

namespace Thermite.Core.Streams
{
    internal class DiscordCompatibleOpusStream : ThermiteStream
    {
        private const int SampleRate = 48 * 1000; // Discord audio is 48khz

        private readonly Stream _source;

        public DiscordCompatibleOpusStream(Stream source)
        {
            _source = source;
        }

        public override async Task<ReadResult> ReadAsync(
            ArraySegment<byte> array, CancellationToken cancelToken)
        {
            var read = await _source.ReadAsync(array.Array, array.Offset,
                array.Count, cancelToken)
                .ConfigureAwait(false);

            var samples = (uint)Opus.GetSamplesPerFrame(
                array.AsSpan(0, read), SampleRate);
            var time = TimeSpan.FromSeconds((1d / SampleRate) * samples);

            return new ReadResult(read, time, samples);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _source?.Dispose();
            }
        }
    }
}