using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Thermite.Core.Caching;
using Thermite.Core.Streams;
using Voltaic;
using Voltaic.Serialization.Utf8;

namespace Thermite.Core.Providers
{
    public class YoutubeProvider : IProvider
    {
        private static readonly Dictionary<Uri, Func<Uri, string>> Extractors
            = new Dictionary<Uri, Func<Uri, string>>()
            {
                [new Uri("https://youtube.com/watch")] =
                    x => IdFromQuery(x),
                [new Uri("https://www.youtube.com/watch")] =
                    x => IdFromQuery(x),
                [new Uri("https://m.youtube.com/watch")] =
                    x => IdFromQuery(x),

                [new Uri("https://youtube.com/embed")] =
                    x => new Uri("https://youtube.com/embed")
                        .MakeRelativeUri(x).ToString(),
                [new Uri("https://www.youtube.com/embed")] =
                    x => new Uri("https://www.youtube.com/embed")
                        .MakeRelativeUri(x).ToString(),
                [new Uri("https://m.youtube.com/embed")] =
                    x => new Uri("https://m.youtube.com/embed")
                        .MakeRelativeUri(x).ToString(),

                [new Uri("https://youtu.be")] =
                    x => x.AbsolutePath.Trim('/'),
            };

        private readonly YoutubeConfiguration _config;
        private readonly HttpClient _client;
        private readonly ICache _cache;

        public YoutubeProvider(YoutubeConfiguration config, HttpClient client,
            ICache cache)
        {
            _config = config;
            _client = client;
            _cache = cache;
        }

        public async Task<IReadOnlyCollection<TrackInfo>> GetTracksAsync(Uri url)
        {
            if (TryGetPlaylistId(url, out var playlistId))
                return await GetPlaylistAsync(playlistId)
                    .ConfigureAwait(false);
            else if (TryGetVideoId(url, out var videoId))
                return new TrackInfo[]
                {
                    await GetSingleVideoAsync(videoId)
                        .ConfigureAwait(false)
                };
            else
                throw new ArgumentException(
                    "Cannot retrieve the tracks from this URL", nameof(url));
        }


        private Task<IReadOnlyCollection<TrackInfo>> GetPlaylistAsync(
            string playlistId)
        {
            throw new NotImplementedException();
        }

        private async Task<TrackInfo> GetSingleVideoAsync(
            string videoId)
        {
            var response = await _client.GetAsync(
                $"https://youtube.com/get_video_info?video_id={videoId}")
                .ConfigureAwait(false);
            using (response)
            {
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync()
                    .ConfigureAwait(false);

                var metadata = new Dictionary<string, string>();

                if (!YoutubeStreamParser.TryGetVideoMetadata(bytes, metadata,
                    out var bestStream, out var bestContentType))
                    throw new YoutubeException(
                        "Couldn't get the metadata from this video");

                return new TrackInfo(metadata,
                    new Uri($"https://youtube.com/watch?v={videoId}"),
                    new Uri(bestStream),
                    bestContentType);
            }
        }

        public bool IsSupported(Uri url)
        {
            foreach (var extractor in Extractors)
            {
                if (extractor.Key.IsBaseOf(url))
                    return true;
            }

            return false;
        }

        private bool TryGetVideoId(Uri url, out string id)
        {
            foreach (var extractor in Extractors)
            {
                if (extractor.Key.IsBaseOf(url))
                {
                    var foundId = extractor.Value(url);
                    if (!string.IsNullOrEmpty(foundId))
                    {
                        id = foundId;
                        return true;
                    }
                }
            }

            id = null;
            return false;
        }

        private bool TryGetPlaylistId(Uri url, out string playlistId)
        {
            playlistId = null;
            return false;
        }

        private static string IdFromQuery(Uri uri)
        {
            var query = uri.Query.AsSpan();

            var start = query.IndexOf("v=".AsSpan(),
                StringComparison.OrdinalIgnoreCase) + 2;
            var idLength = 0;

            foreach (var c in query.Slice(start))
            {
                if (c == '&')
                    break;

                idLength++;
            }

            return query.Slice(start, idLength).ToString();
        }
    }
}