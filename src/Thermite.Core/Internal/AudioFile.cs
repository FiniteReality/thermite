using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Thermite.Core.Streams;

namespace Thermite.Core
{
    internal class AudioFile : IAudioFile
    {
        public Uri Location { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }

        private readonly Func<Task<ThermiteStream>> _getStream;

        public AudioFile(Uri location,
            IReadOnlyDictionary<string, string> metadata,
            Func<Task<ThermiteStream>> getStream)
        {
            Location = location;
            Metadata = metadata;
            _getStream = getStream;
        }

        public Task<ThermiteStream> GetStreamAsync()
        {
            return _getStream();
        }
    }
}