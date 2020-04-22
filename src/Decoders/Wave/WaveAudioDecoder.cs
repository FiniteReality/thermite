using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Thermite.Decoders.Wave
{
    /// <summary>
    /// A decoder for decoding RIFF WAVE audio files.
    /// </summary>
    public sealed class WaveAudioDecoder : IAudioDecoder
    {
        private readonly PipeReader _input;
        private readonly Pipe _outputPipe;

        /// <inheritdoc/>
        public PipeReader Output => _outputPipe.Reader;

        internal WaveAudioDecoder(PipeReader input)
        {
            _input = input;
            _outputPipe = new Pipe();
        }

        /// <inheritdoc/>
        public Task RunAsync(CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        /// <inheritdoc/>
        public ValueTask<IAudioCodec?> IdentifyCodecAsync(CancellationToken cancellationToken = default)
            => throw new System.NotImplementedException();

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
            => default;
    }
}
