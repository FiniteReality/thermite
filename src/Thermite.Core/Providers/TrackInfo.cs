using System;
using System.Collections.Generic;

namespace Thermite.Core.Providers
{
    public class TrackInfo
    {
        public TrackInfo(Dictionary<string, string> metadata,
            Uri browseUrl, Uri streamUrl, string contentType)
        {
            Metadata = metadata;
            BrowseUrl = browseUrl;
            StreamUrl = streamUrl;
            StreamContentType = contentType;
        }

        public IReadOnlyDictionary<string, string> Metadata { get; }

        public Uri BrowseUrl { get; }
        public Uri StreamUrl { get; }
        public string StreamContentType { get; }
    }
}