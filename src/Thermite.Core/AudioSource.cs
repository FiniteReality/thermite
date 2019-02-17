using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Thermite.Core.Providers;
using Thermite.Core.Streams;

namespace Thermite.Core
{
    public class AudioSource : IAudioSource
    {
        private readonly IReadOnlyCollection<IProvider> _providers;
        private readonly HttpClient _client;

        public AudioSource(IReadOnlyCollection<IProvider> providers,
            HttpClient client)
        {
            _providers = providers;
            _client = client;
        }

        public async Task<IReadOnlyCollection<IAudioFile>> GetTracksAsync(Uri url)
        {
            foreach (var provider in _providers)
            {
                if (provider.IsSupported(url))
                {
                    var tracks = await provider.GetTracksAsync(url)
                        .ConfigureAwait(false);

                    var result = new List<IAudioFile>(tracks.Count);

                    foreach (var track in tracks)
                        result.Add(new AudioFile(track.BrowseUrl,
                            track.Metadata, GetStreamProvider(track)));

                    return result;
                }
            }

            // TODO: better exception type here
            throw new Exception("Url is not supported");
        }

        protected virtual Func<Task<ThermiteStream>> GetStreamProvider(
            TrackInfo info)
        {
            // TODO: check whether stream is compatible or not first

            if (info.StreamUrl.IsFile)
            {
                return () =>
                {
                    var file = File.OpenRead(info.StreamUrl.LocalPath);

                    return Task.FromResult<ThermiteStream>(
                        new OpusEncodeStream(file));
                };
            }
            else
            {
                return async () =>
                {
                    var stream = await _client.GetStreamAsync(info.StreamUrl)
                        .ConfigureAwait(false);

                    return new DiscordCompatibleOpusStream(stream);
                };
            }
        }
    }
}