using System;
using System.IO.Pipelines;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Thermite.Providers.Http
{
    /// <summary>
    /// A provider for retrieving audio files from HTTP(S) locations.
    /// </summary>
    public sealed class HttpAudioProvider : IAudioProvider
    {
        private static readonly PipeOptions PipeOptions = new PipeOptions(
            pauseWriterThreshold: 4096 * 2048, // 8MB
            resumeWriterThreshold: 4096 * 1024 // 4MB
        );

        private readonly HttpClient _client;
        private readonly Pipe _dataPipe;
        private readonly Uri _location;

        private HttpResponseMessage? _response;

        internal HttpAudioProvider(IHttpClientFactory clientFactory,
            Uri location)
        {
            _client = clientFactory.CreateClient(nameof(HttpAudioProvider));
            _dataPipe = new Pipe(PipeOptions);
            _location = location;

            _client.DefaultRequestHeaders.UserAgent.Clear();
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (X11; Linux x86_64; rv:69.0) Gecko/20100101 " +
                "Firefox/69.0");
        }

        /// <inheritdoc/>
        public PipeReader Output => _dataPipe.Reader;

        /// <inheritdoc/>
        public async Task RunAsync(
            CancellationToken cancellationToken = default)
        {
            var response = await GetHttpResponseMessageAsync(
                cancellationToken);
            var body = response.Content;
            var stream = await body.ReadAsStreamAsync();

            try
            {
                FlushResult result = default;
                while (!result.IsCompleted)
                {
                    var memory = _dataPipe.Writer.GetMemory(1024);
                    var bytesRead = await stream.ReadAsync(memory,
                        cancellationToken);

                    _dataPipe.Writer.Advance(bytesRead);
                    result = await _dataPipe.Writer.FlushAsync(
                        cancellationToken);

                    if (bytesRead == 0)
                        break;
                }
            }
            finally
            {
                await _dataPipe.Writer.CompleteAsync();
            }
        }

        /// <inheritdoc/>
        public async ValueTask<string> IdentifyMediaTypeAsync(
            CancellationToken cancellationToken = default)
        {
            var response = await GetHttpResponseMessageAsync(cancellationToken);

            // Reading the response body for identification is not possible
            // as we may permanently lose data
            return response.Content.Headers.ContentType.ToString();
        }

        private async Task<HttpResponseMessage> GetHttpResponseMessageAsync(
            CancellationToken cancellationToken = default)
        {
            return _response ??= await _client.GetAsync(_location,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            _response?.Dispose();
            _client.Dispose();

            return default;
        }
    }
}
