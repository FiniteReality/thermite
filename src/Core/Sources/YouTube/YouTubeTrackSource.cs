using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Core.Sources.YouTube;
using Thermite.Internal;

using static Thermite.Utilities.ThrowHelpers;

namespace Thermite.Core.Sources
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
        private readonly SemaphoreSlim _rateLimiter;
        private readonly Random _rateLimiterRandomness;
        private readonly Timer _rateLimiterTimer;


        /// <summary>
        /// Creates a new instance of the <see cref="YouTubeTrackSource"/>
        /// type.
        /// </summary>
        /// <param name="clientFactory">
        /// The <see cref="IHttpClientFactory"/> used for creating instances of
        /// <see cref="HttpClient"/>.
        /// </param>
        public YouTubeTrackSource(IHttpClientFactory clientFactory)
        {
            _client = clientFactory.CreateClient(
                "urn:thermite:http_client/youtube");

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

            if (playlistId != null)
                return GetPlaylistTracks(playlistId, videoId,
                    cancellationToken);

            if (videoId != null)
                return GetVideoTracksAsync(videoId, cancellationToken);

            throw new InvalidUriException(nameof(location), location);
        }

        private async IAsyncEnumerable<TrackInfo> GetVideoTracksAsync(
            string videoId,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            yield return await GetTrackInfoAsync(videoId, cancellationToken);
        }

        private async IAsyncEnumerable<TrackInfo> GetPlaylistTracks(
            string playlistId, string? startVideoId,
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            bool foundStartVideo = startVideoId == null;
            string? continuation = null;
            for (int pages = 0; pages < 6; pages++)
            {
                ReadOnlySequenceBuilder<byte> data;
                if (continuation == null)
                    data = await GetPlaylistContentsAsync(playlistId,
                        cancellationToken);
                else
                    data = await GetPlaylistContinuationAsync(continuation,
                        cancellationToken);

                using (data)
                {
                    JsonReaderState state = default;
                    var sequence = data.AsSequence();
                    if (!YouTubePlaylistParser.TryReadPreamble(ref sequence,
                        ref state, out var alert))
                        throw new InvalidOperationException(alert);

                    while (YouTubePlaylistParser.TryReadPlaylistItem(
                        ref sequence, ref state, out var videoId))
                    {
                        if (!foundStartVideo && videoId == startVideoId)
                            foundStartVideo = true;

                        if (foundStartVideo)
                            yield return await GetTrackInfoAsync(
                                videoId);
                    }

                    if (!YouTubePlaylistParser.TryGetContinuation(ref sequence,
                        ref state, out continuation))
                        break;
                }
            }
        }

        private async ValueTask<TrackInfo> GetTrackInfoAsync(string videoId,
            CancellationToken cancellationToken = default)
        {
            using var info = await GetVideoInfoAsync(videoId,
                cancellationToken);
            var sequence = info.AsSequence();

            if (!YoutubeStreamParser.GetBestStream(sequence, out var track))
                throw new ArgumentException("Invalid video ID",
                    nameof(videoId));

            track.OriginalLocation = new Uri(
                $"https://youtube.com/watch?v={videoId}");

            return track;
        }

        private async Task<ReadOnlySequenceBuilder<byte>>
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

            return builder;
        }

        private async Task<ReadOnlySequenceBuilder<byte>>
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

            return builder;
        }

        private async Task<ReadOnlySequenceBuilder<byte>> GetVideoInfoAsync(
            string videoId, CancellationToken cancellationToken = default)
        {
            await _rateLimiter.WaitAsync(cancellationToken);
            using var response = await _client.GetAsync(
                $"https://youtube.com/get_video_info?video_id={videoId}",
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

            return builder;
        }
    }
}