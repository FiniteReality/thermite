using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Voltaic;

namespace Thermite.Core.Providers
{
    internal class YoutubeStreamParser
    {

        private static readonly byte[] TypeString =
            Encoding.UTF8.GetBytes("type");
        private static readonly byte[] BitrateString =
            Encoding.UTF8.GetBytes("bitrate");
        private static readonly byte[] UrlString =
            Encoding.UTF8.GetBytes("url");

        private static readonly byte[] QualityString =
            Encoding.UTF8.GetBytes("quality");

        // lower values are higher quality
        private static readonly byte[][] QualityLevels =
        {
            Encoding.UTF8.GetBytes("hd720"), // -0
            Encoding.UTF8.GetBytes("medium"), // -1
            Encoding.UTF8.GetBytes("small"), // -2
            Encoding.UTF8.GetBytes("tiny"), // -3
        };

        // higher values are higher quality
        private static readonly string[] KnownContentTypes =
        {
            //"some content type", // ?
        };

        private static readonly byte[] AdaptiveFmtsString =
            Encoding.UTF8.GetBytes("adaptive_fmts");
        private static readonly byte[] UrlEncodedStreamFmtMapString =
            Encoding.UTF8.GetBytes("url_encoded_fmt_stream_map");
        private static readonly byte[] TitleString =
            Encoding.UTF8.GetBytes("title");
        private static readonly byte[] AuthorString =
            Encoding.UTF8.GetBytes("author");
        private static readonly byte[] LengthSecondsString =
            Encoding.UTF8.GetBytes("length_seconds");
        private static readonly byte[] ThumbnailUrlString =
            Encoding.UTF8.GetBytes("thumbnail_url");

        public static bool TryGetVideoMetadata(ReadOnlySpan<byte> info,
            Dictionary<string, string> metadata,
            out string bestStreamUrl, out string bestStreamContentType)
        {
            bestStreamUrl = null;
            bestStreamContentType = null;

            // preallocate some space so that we don't immediately resize
            List<(int quality, string url, string contentType)> streams =
                new List<(int, string, string)>(16);

            while (StringUtilities.TryReadUrlEncodedKeyValuePair(ref info,
                out var key, out var encodedValue))
            {
                // technically we should be decoding the key here too but
                // they likely won't change any time soon
                if (!StringUtilities.TryUrlDecode(encodedValue, out var value))
                    return false;

                try
                {
                    if (key.SequenceEqual(AdaptiveFmtsString)
                        && !ParseAdaptiveFmtsMap(value.AsReadOnlySpan(),
                            streams))
                        return false;

                    else if (key.SequenceEqual(UrlEncodedStreamFmtMapString)
                        && !ParseUrlEncodedStreamFmtMap(value.AsReadOnlySpan(),
                            streams))
                        return false;

                    else if (key.SequenceEqual(TitleString))
                    {
                        if (!StringUtilities.TryUrlDecode(
                            value.AsReadOnlySpan(), out var title))
                            return false;

                        try
                        {
                            metadata.Add(MetadataTypes.SongName,
                                Encoding.UTF8.GetString(
                                    title.AsReadOnlySpan()));
                        }
                        finally
                        {
                            title.Return();
                        }
                    }

                    else if (key.SequenceEqual(AuthorString))
                    {
                        if (!StringUtilities.TryUrlDecode(
                            value.AsReadOnlySpan(), out var author))
                            return false;

                        try
                        {
                            metadata.Add(MetadataTypes.SongAuthor,
                                Encoding.UTF8.GetString(
                                    author.AsReadOnlySpan()));
                        }
                        finally
                        {
                            author.Return();
                        }
                    }

                    else if (key.SequenceEqual(LengthSecondsString))
                    {
                        if (!StringUtilities.TryUrlDecode(
                            value.AsReadOnlySpan(), out var length))
                            return false;

                        try
                        {
                            metadata.Add(MetadataTypes.SongLength,
                                Encoding.UTF8.GetString(
                                    length.AsReadOnlySpan()));
                        }
                        finally
                        {
                            length.Return();
                        }
                    }

                    else if (key.SequenceEqual(ThumbnailUrlString))
                    {
                        if (!StringUtilities.TryUrlDecode(
                            value.AsReadOnlySpan(), out var thumbnail))
                            return false;

                        try
                        {
                            metadata.Add(MetadataTypes.ThumbnailUrl,
                                Encoding.UTF8.GetString(
                                    thumbnail.AsReadOnlySpan()));
                        }
                        finally
                        {
                            thumbnail.Return();
                        }
                    }
                }
                finally
                {
                    value.Return();
                }
            }

            // Remove all video-only streams
            streams.RemoveAll(x => x.contentType.StartsWith("video"));

            if (streams.Count == 0)
                return false;

            streams.Sort(SortQuality);
            var bestQuality = streams.First();

            bestStreamUrl = bestQuality.url;
            bestStreamContentType = bestQuality.contentType;
            return true;

            int SortQuality((int quality, string, string contentType) a,
                (int quality, string, string contentType) b)
            {
                // -1 == a smaller than b
                // 0 == a equal to b
                // 1 = a greater than b

                var qualComparison = b.quality.CompareTo(a.quality);

                if (qualComparison != 0)
                    return qualComparison;

                return 0;

                /* int aContentType = -1,
                    bContentType = -1;

                for (int i = 0; i < KnownContentTypes.Length; i++)
                {
                    if (a.contentType == KnownContentTypes[i])
                        aContentType = i;
                    if (b.contentType == KnownContentTypes[i])
                        bContentType = i;
                }

                Debug.Assert(aContentType > 0, "Unknown content type",
                    "Content type: {0}", a.contentType);
                Debug.Assert(bContentType > 0, "Unknown content type",
                    "Content type: {0}", b.contentType);

                return bContentType.CompareTo(aContentType);*/
            }


            bool ParseAdaptiveFmtsMap(ReadOnlySpan<byte> map,
                List<(int quality, string url, string contentType)> streamUrls)
            {
                while (YoutubeStreamParser.TryGetUrlFromAdaptiveFmtsMap(
                    ref map, out var url, out var contentType,
                    out var quality))
                {
                    try
                    {
                        var streamUrl = Encoding.UTF8.GetString(
                            url.AsReadOnlySpan());
                        var streamContentType = Encoding.UTF8.GetString(
                            contentType.AsReadOnlySpan());
                        streamUrls.Add((quality, streamUrl, streamContentType));
                    }
                    finally
                    {
                        url.Return();
                        contentType.Return();
                    }
                }

                return map.Length == 0;
            }

            bool ParseUrlEncodedStreamFmtMap(ReadOnlySpan<byte> map,
                List<(int quality, string url, string contentType)> streamUrls)
            {
                while (YoutubeStreamParser.TryGetUrlFromUrlEncodedStreanFmtMap(
                    ref map, out var url, out var contentType,
                    out var quality))
                {
                    try
                    {
                        var streamUrl = Encoding.UTF8.GetString(
                            url.AsReadOnlySpan());
                        var streamContentType = Encoding.UTF8.GetString(
                            contentType.AsReadOnlySpan());
                        streamUrls.Add((quality, streamUrl, streamContentType));
                    }
                    finally
                    {
                        url.Return();
                        contentType.Return();
                    }
                }

                return map.Length == 0;
            }
        }

        public static bool TryGetUrlFromAdaptiveFmtsMap(
            ref ReadOnlySpan<byte> span, out ResizableMemory<byte> url,
            out ResizableMemory<byte> contentType, out int quality)
        {
            url = default;
            contentType = default;
            quality = 0;

            while (StringUtilities.TryReadCommaSeparatedValue(ref span,
                out var entry))
            {
                while (StringUtilities.TryReadUrlEncodedKeyValuePair(
                    ref entry, out var key, out var value))
                {
                    if (key.SequenceEqual(TypeString)
                        && !StringUtilities.TryUrlDecode(value, out contentType))
                    {
                        url.Return();
                        contentType.Return();

                        return false;
                    }

                    if (key.SequenceEqual(UrlString)
                        && !StringUtilities.TryUrlDecode(value,
                            out url))
                    {
                        url.Return();
                        contentType.Return();

                        return false;
                    }

                    if (key.SequenceEqual(BitrateString)
                        && !Utf8Parser.TryParse(value, out quality, out _))
                    {
                        url.Return();
                        contentType.Return();

                        return false;
                    }
                }

                if (url.Array != null && contentType.Array != null)
                    return true;
            }

            // if we got here, that means we either read the entire buffer or
            // failed to read somewhere
            return false;
        }

        public static bool TryGetUrlFromUrlEncodedStreanFmtMap(
            ref ReadOnlySpan<byte> span, out ResizableMemory<byte> url,
            out ResizableMemory<byte> contentType, out int quality)
        {
            url = default;
            contentType = default;
            quality = 0;

            while (StringUtilities.TryReadCommaSeparatedValue(ref span,
                out var entry))
            {
                while (StringUtilities.TryReadUrlEncodedKeyValuePair(
                    ref entry, out var key, out var value))
                {
                    if (key.SequenceEqual(UrlString)
                        && !StringUtilities.TryUrlDecode(value, out url))
                    {
                        url.Return();
                        contentType.Return();
                        return false;
                    }

                    if (key.SequenceEqual(TypeString)
                        && !StringUtilities.TryUrlDecode(value,
                            out contentType))
                    {
                        url.Return();
                        contentType.Return();
                        return false;
                    }

                    if (key.SequenceEqual(QualityString)
                        && !TryParseQuality(value, out quality))
                        return false;
                }

                if (url.Array != null && contentType.Array != null)
                    return true;
            }

            // if we got here, that means we either read the entire buffer or
            // failed to read somewhere
            return false;
        }

        private static bool TryParseQuality(ReadOnlySpan<byte> qualityString,
                out int quality)
        {
            for (int i = 0; i < QualityLevels.Length; i++)
            {
                if (qualityString.SequenceEqual(QualityLevels[i]))
                {
                    quality = -i;
                    return true;
                }
            }

            quality = default;
            return false;
        }
    }
}
