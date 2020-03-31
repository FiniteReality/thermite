using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Thermite.Sources.YouTube;
using Thermite.Internal;

using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Sources
{
    /// <summary>
    /// A source for retrieving Youtube tracks from the internet
    /// </summary>
    public sealed class YouTubeTrackSource : ITrackSource, IAsyncDisposable
    {
        private delegate (string? videoId, string? playlistId) VideoIdGetter(
            Uri location);

        private static readonly VideoIdGetter UnsupportedUrl =
            x => (null, null);

        private static readonly Dictionary<Uri, VideoIdGetter> VideoIdProviders
            = new Dictionary<Uri, VideoIdGetter>()
            {
                [new Uri("http://youtube.com/watch")] = GetWatchVideoId,
                [new Uri("https://youtube.com/watch")] = GetWatchVideoId,
                [new Uri("http://www.youtube.com/watch")] = GetWatchVideoId,
                [new Uri("https://www.youtube.com/watch")] = GetWatchVideoId,

                [new Uri("http://youtu.be/")] = GetShortLinkVideoId,
                [new Uri("https://youtu.be/")] = GetShortLinkVideoId,
                [new Uri("http://www.youtu.be/")] = GetShortLinkVideoId,
                [new Uri("https://www.youtu.be/")] = GetShortLinkVideoId,
            };

        private static readonly VideoIdGetter GetShortLinkVideoId =
            x => (x.AbsolutePath.Substring(1), null);
        private static (string?, string?) GetWatchVideoId(Uri videoLocation)
        {
            if (videoLocation.Query.Length == 0)
                return (null, null);

            var videoIdStart = videoLocation.Query.IndexOf("v=");
            var playlistIdStart = videoLocation.Query.IndexOf("list=");
            var ampersand = videoLocation.Query.IndexOf("&");
            var videoIdEnd = videoLocation.Query.Length;
            var playlistIdEnd = videoLocation.Query.Length;

            string? videoId = null;
            string? playlistId = null;

            if (ampersand > videoIdStart)
                videoIdEnd = ampersand;
            if (ampersand > playlistIdStart)
                playlistIdEnd = ampersand;

            if (videoIdStart > 0)
                videoId = videoLocation.Query.Substring(
                    videoIdStart + 2, videoIdEnd - videoIdStart - 2);
            if (playlistIdStart > 0)
                playlistId = videoLocation.Query.Substring(
                    playlistIdStart + 5, playlistIdEnd - playlistIdStart - 5);

            return (videoId, playlistId);
        }

        private readonly HttpClient _client;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly Random _rateLimiterRandomness;
        private readonly Timer _rateLimiterTimer;


        /// <summary>
        /// Creates a new instance of the <see cref="YouTubeTrackSource"/>
        /// type.
        /// </summary>
        /// <param name="loggerFactory">
        /// The <see cref="ILoggerFactory"/> used for creating instances of
        /// <see cref="ILogger"/>.
        /// </param>
        /// <param name="clientFactory">
        /// The <see cref="IHttpClientFactory"/> used for creating instances of
        /// <see cref="HttpClient"/>.
        /// </param>
        public YouTubeTrackSource(ILoggerFactory loggerFactory,
            IHttpClientFactory clientFactory)
        {
            _client = clientFactory.CreateClient(nameof(YouTubeTrackSource));
            _logger = loggerFactory.CreateLogger<YouTubeTrackSource>();

            _client.DefaultRequestHeaders.Add("x-youtube-client-name", "1");
            _client.DefaultRequestHeaders.Add("x-youtube-client-version",
                "2.20191008.04.1");

            _rateLimiter = new SemaphoreSlim(initialCount: 0, maxCount: 1);
            _rateLimiterRandomness = new Random();
            _rateLimiterTimer = new Timer(ResetRateLimit, this, 0,
                Timeout.Infinite);
        }

        private static void ResetRateLimit(object? state)
        {
            var source = state as YouTubeTrackSource;
            Debug.Assert(source != null,
                $"{nameof(state)} wasn't {nameof(YouTubeTrackSource)}");

            if (source._rateLimiter.CurrentCount < 1)
                source._rateLimiter.Release();

            // Schedule the timer for 10s +/- random(-1.5s, 5.5s)
            // This heuristic should help prevent getting nodes banned at the
            // cost of slightly slower video access times.
            var random = source._rateLimiterRandomness;
            var nextTime = 10000 + random.Next(-3500, 5500);

            source._logger.LogTrace("Ratelimit resetting in {timeout}ms",
                nextTime);

            source._rateLimiterTimer.Change(nextTime, Timeout.Infinite);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            _rateLimiter.Dispose();
            await _rateLimiterTimer.DisposeAsync();
        }

        /// <inheritdoc/>
        public bool IsSupported(Uri location)
        {
            foreach (var url in VideoIdProviders.Keys)
                if (url.IsBaseOf(location))
                    return true;

            return false;
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<TrackInfo> GetTracksAsync(Uri location,
            CancellationToken cancellationToken = default)
        {
            VideoIdGetter getter = UnsupportedUrl;
            foreach (var pair in VideoIdProviders)
            {
                if (pair.Key.IsBaseOf(location))
                {
                    getter = pair.Value;
                    break;
                }
            }

            var (videoId, playlistId) = getter(location);

            if (videoId == null && playlistId == null)
                ThrowInvalidUriException(nameof(location), location);

            _logger.LogDebug("Video ID: {videoId}\nPlaylist ID: {playlistId}",
                videoId ?? "(null)", playlistId ?? "(null)");

            if (playlistId != null)
                return GetPlaylistTracksAsync(playlistId, videoId,
                    cancellationToken);

            if (videoId != null)
                return GetVideoTracksAsync(videoId, cancellationToken);

            ThrowInvalidUriException(nameof(location), location);

            return null; // not hit, above does not return
        }

        private async IAsyncEnumerable<TrackInfo> GetVideoTracksAsync(
            string videoId,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            var info = await GetTrackInfoAsync(videoId, cancellationToken);

            if (info == null)
                ThrowArgumentException(nameof(videoId), "Invalid video ID");

            yield return info.Value;
        }

        private async IAsyncEnumerable<TrackInfo> GetPlaylistTracksAsync(
            string playlistId, string? startVideoId,
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            bool foundStartVideo = startVideoId == null;
            string? continuation = null;

            while (true)
            {
                bool success;
                ReadOnlySequenceBuilder<byte> data;
                if (continuation == null)
                    (success, data) = await GetPlaylistContentsAsync(playlistId,
                        cancellationToken);
                else
                    (success, data) = await GetPlaylistContinuationAsync(
                        continuation, cancellationToken);

                if (!success)
                    ThrowArgumentException(nameof(playlistId),
                        "Invalid playlist ID");

                using (_logger.BeginScope("Enumerating playlist: {playlistId}",
                    playlistId))
                using (data)
                {
                    JsonReaderState state = default;
                    var sequence = data.AsSequence();
                    if (!YouTubePlaylistParser.TryReadPreamble(ref sequence,
                        ref state, out var alert))
                        ThrowInvalidOperationException(alert);

                    while (YouTubePlaylistParser.TryReadPlaylistItem(
                        ref sequence, ref state, out var videoId))
                    {
                        _logger.LogInformation("Found video ID {videoId}",
                            videoId);
                        if (!foundStartVideo && videoId == startVideoId)
                            foundStartVideo = true;

                        if (foundStartVideo)
                        {
                            var info = await GetTrackInfoAsync(videoId);

                            if (info != null)
                                yield return info.Value;
                        }
                    }

                    if (!YouTubePlaylistParser.TryGetContinuation(ref sequence,
                        ref state, out continuation))
                        break;

                    if (continuation != null)
                        _logger.LogDebug("Got continuation {continuation}",
                            continuation);
                }
            }
        }

        private async ValueTask<TrackInfo?> GetTrackInfoAsync(string videoId,
            CancellationToken cancellationToken = default)
        {
            var (success, info) = await GetVideoInfoAsync(videoId,
                cancellationToken);

            if (!success)
                return default;

            using (info)
            {
                var sequence = info.AsSequence();

                if (!YoutubeStreamParser.GetBestStream(sequence, out var track))
                    return default;

                _logger.LogTrace("Got best stream: {stream}",
                    track.AudioLocation);

                track.OriginalLocation = new Uri(
                    $"https://youtube.com/watch?v={videoId}");

                return track;
            }
        }

        private async Task<(bool, ReadOnlySequenceBuilder<byte>)>
            GetPlaylistContentsAsync(string playlistId,
                CancellationToken cancellationToken = default)
        {
            await _rateLimiter.WaitAsync(cancellationToken);
            using var response = await _client.GetAsync(
                $"https://youtube.com/playlist?list={playlistId}&pbj=1&hl=en",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            using var content = response.Content;
            using var stream = await content.ReadAsStreamAsync();

            var builder = new ReadOnlySequenceBuilder<byte>();

            while (true)
            {
                var memory = builder.GetMemory(1024);
                var bytesRead = await stream.ReadAsync(memory,
                    cancellationToken);

                builder.Advance(bytesRead);

                if (bytesRead == 0)
                    break;
            }

            return (response.IsSuccessStatusCode, builder);
        }

        private async Task<(bool, ReadOnlySequenceBuilder<byte>)>
            GetPlaylistContinuationAsync(string continuation,
                CancellationToken cancellationToken = default)
        {
            await _rateLimiter.WaitAsync(cancellationToken);
            using var response = await _client.GetAsync(
                $"https://youtube.com/browse_ajax?continuation=" +
                $"{continuation}&ctoken={continuation}&hl=en",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            using var content = response.Content;
            using var stream = await content.ReadAsStreamAsync();

            var builder = new ReadOnlySequenceBuilder<byte>();

            while (true)
            {
                var memory = builder.GetMemory(1024);
                var bytesRead = await stream.ReadAsync(memory,
                    cancellationToken);

                builder.Advance(bytesRead);

                if (bytesRead == 0)
                    break;
            }

            return (response.IsSuccessStatusCode, builder);
        }

        private async Task<(bool, ReadOnlySequenceBuilder<byte>)>
            GetVideoInfoAsync(string videoId,
                CancellationToken cancellationToken = default)
        {
            await _rateLimiter.WaitAsync(cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://youtube.com/get_video_info?video_id={videoId}");

            request.Headers.Referrer = new Uri(
                $"https://youtube.com/watch?v={videoId}");

            using var response = await _client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            using var content = response.Content;
            using var stream = await content.ReadAsStreamAsync();

            var builder = new ReadOnlySequenceBuilder<byte>();

            while (true)
            {
                var memory = builder.GetMemory(1024);
                var bytesRead = await stream.ReadAsync(memory,
                    cancellationToken);

                builder.Advance(bytesRead);

                if (bytesRead == 0)
                    break;
            }

            return (response.IsSuccessStatusCode, builder);
        }
    }
}
