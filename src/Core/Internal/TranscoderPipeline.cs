using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Codecs;

namespace Thermite.Internal
{
    internal sealed class TranscoderPipeline
    {
        private readonly List<IAudioTranscoder> _transcoders;

        public PipeReader Output { get; }

        private TranscoderPipeline(PipeReader outputPipe,
            List<IAudioTranscoder> transcoders)
        {
            Output = outputPipe;
            _transcoders = transcoders;
        }

        public Task RunAsync(
            CancellationToken cancellationToken = default)
        {
            if (_transcoders.Count > 1)
            {
                var tasks = new Task[_transcoders.Count];

                for (int x = 0; x < tasks.Length; x++)
                {
                    tasks[x] = _transcoders[x].RunAsync(cancellationToken);
                }

                return Task.WhenAll(tasks);
            }
            else
            {
                return _transcoders[0].RunAsync(cancellationToken);
            }
        }

        public static async ValueTask<TranscoderPipeline?> CreatePipelineAsync(
            PlayerManager manager, PipeReader input, IAudioCodec inputCodec,
            CancellationToken cancellationToken = default)
        {
            var transcoders = new List<IAudioTranscoder>();
            var currentCodec = inputCodec;

            // TODO: We could potentially bail out here if we're already the
            // desired codec.
            do
            {
                if (!manager.TryGetTranscoderFactory(currentCodec,
                    out var factory))
                    return null;

                var transcoder = factory.GetTranscoder(currentCodec, input);
                transcoders.Add(transcoder);

                currentCodec = await transcoder.GetOutputCodecAsync(
                    cancellationToken);
                input = transcoder.Output;
            }
            while (!IsDesiredCodec(currentCodec));

            return new TranscoderPipeline(input, transcoders);

            static bool IsDesiredCodec(IAudioCodec codec)
            {
                if (!(codec is OpusAudioCodec opusCodec))
                    return false;

                // 48khz Stereo Opus is what we desire
                return opusCodec.ChannelCount == 2
                    && opusCodec.SamplingRate == 48000;
            }
        }
    }
}
