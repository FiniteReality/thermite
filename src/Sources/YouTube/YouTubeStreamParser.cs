using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using Thermite.Internal;
using static Thermite.Utilities.TextParsingUtilities;
using static Thermite.Utilities.UrlParsingUtilities;

namespace Thermite.Sources.YouTube
{
    internal static class YoutubeStreamParser
    {
        private ref struct StreamInfo
        {
            public JsonElement Url;
            public JsonElement MimeType;
            public int Bitrate;
        }

        private static readonly byte[] PlayerResponseProperty =
            Encoding.UTF8.GetBytes("player_response");
        private static readonly byte[] StatusProperty =
            Encoding.UTF8.GetBytes("status");
        private static readonly byte[] StatusOkValue =
            Encoding.UTF8.GetBytes("ok");

        private static readonly byte[] VideoDetailsProperty =
            Encoding.UTF8.GetBytes("videoDetails");
        private static readonly byte[] TitleProperty =
            Encoding.UTF8.GetBytes("title");

        private static readonly byte[] StreamingDataProperty =
            Encoding.UTF8.GetBytes("streamingData");
        private static readonly byte[] FormatsProperty =
            Encoding.UTF8.GetBytes("formats");
        private static readonly byte[] AdaptiveFormatsProperty =
            Encoding.UTF8.GetBytes("adaptiveFormats");

        private static readonly byte[] UrlProperty =
            Encoding.UTF8.GetBytes("url");
        private static readonly byte[] MimeTypeProperty =
            Encoding.UTF8.GetBytes("mimeType");
        private static readonly byte[] BitrateProperty =
            Encoding.UTF8.GetBytes("bitrate");
        private static readonly byte[] QualityProperty =
            Encoding.UTF8.GetBytes("quality");

        // lower values are higher quality
        private static readonly byte[][] KnownQualityLevels =
        {
            Encoding.UTF8.GetBytes("hd1080"), // -0
            Encoding.UTF8.GetBytes("hd720"), // -1
            Encoding.UTF8.GetBytes("large"), // -2
            Encoding.UTF8.GetBytes("medium"), // -3
            Encoding.UTF8.GetBytes("small"), // -4
            Encoding.UTF8.GetBytes("tiny"), // -5
        };

        // content types containing audio streams
        // lower values are more preferred
        private static readonly byte[][] KnownContentTypes =
        {
            Encoding.UTF8.GetBytes("audio/webm; codecs=\"opus\""),
            Encoding.UTF8.GetBytes("audio/mp4; codecs=\"mp4a.40.2\""),

            Encoding.UTF8.GetBytes(
                "video/mp4; codecs=\"avc1.64001F, mp4a.40.2\""),
            Encoding.UTF8.GetBytes(
                "video/webm; codecs=\"vp8.0, vorbis\""),
            Encoding.UTF8.GetBytes(
                "video/mp4; codecs=\"avc1.42001E, mp4a.40.2\""),
        };

        public static bool GetBestStream(ReadOnlySequence<byte> sequence,
            out TrackInfo track)
        {
            track = default;
            if (!TryGetPlayerResponse(ref sequence, out var playerResponse))
                return false;

            using var decoded = new ReadOnlySequenceBuilder<byte>();
            while (playerResponse.Length > 0)
            {
                // 3 bytes minimum as longest percent sequence is '%XX'
                var buffer = decoded.GetMemory(3);

                if (playerResponse.Length >= buffer.Length)
                {
                    playerResponse.Slice(0, buffer.Length)
                        .CopyTo(buffer.Span);

                    playerResponse = playerResponse.Slice(buffer.Length);
                }
                else
                {
                    playerResponse.CopyTo(buffer.Span);
                    playerResponse = ReadOnlySequence<byte>.Empty;
                }

                if (!TryUrlDecode(buffer.Span, out var decodedBytes))
                    return false;

                decoded.Advance(decodedBytes);
            }

            var reader = new Utf8JsonReader(decoded.AsSequence());
            if (!JsonDocument.TryParseValue(ref reader, out var document))
                return false;

            using var _ = document;

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            if (!document.RootElement.TryGetProperty(StreamingDataProperty,
                out var streamingData))
                return false;

            if (!document.RootElement.TryGetProperty(VideoDetailsProperty,
                out var videoDetails))
                return false;

            StreamInfo bestFormatsStream = default;
            if (streamingData.TryGetProperty(
                FormatsProperty, out var formats)
                && !TryGetBestStream(formats, out bestFormatsStream))
                    return false;

            StreamInfo bestAdaptiveFormatsStream = default;
            if (streamingData.TryGetProperty(
                AdaptiveFormatsProperty, out var adaptiveFormats) &&
                !TryGetBestStream(adaptiveFormats,
                    out bestAdaptiveFormatsStream))
                return false;

            var bestStream = bestAdaptiveFormatsStream;

            if (IsBetterStream(bestStream, bestFormatsStream))
                bestStream = bestFormatsStream;

            if (!videoDetails.TryGetProperty(TitleProperty, out var title))
                return false;

            track.TrackName = title.GetString()!;
            track.AudioLocation = new Uri(bestStream.Url.GetString()!);
            track.MediaTypeOverride = bestStream.MimeType.GetString()!;

            return true;
        }

        private static bool TryGetBestStream(JsonElement streams,
            out StreamInfo bestStream)
        {
            bestStream = default;

            foreach (var stream in streams.EnumerateArray())
            {
                StreamInfo currentStream = default;

                if (!stream.TryGetProperty(UrlProperty, out currentStream.Url))
                    return false;
                if (!stream.TryGetProperty(MimeTypeProperty,
                    out currentStream.MimeType))
                    return false;

                var isKnownContentType = false;
                for (int x = 0; x < KnownContentTypes.Length; x++)
                    if (currentStream.MimeType.ValueEquals(
                        KnownContentTypes[x]))
                            isKnownContentType = true;

                if (!isKnownContentType)
                    continue;

                if (!stream.TryGetProperty(BitrateProperty, out var bitrate))
                {
                    if (!bitrate.TryGetInt32(out currentStream.Bitrate))
                        return false;
                }
                else if (stream.TryGetProperty(
                    QualityProperty, out var quality))
                {
                    for (int x = 0; x < KnownQualityLevels.Length; x++)
                    {
                        if (quality.ValueEquals(KnownQualityLevels[x]))
                        {
                            currentStream.Bitrate = -x;
                            break;
                        }
                    }
                }
                else
                {
                    return false;
                }

                if (IsBetterStream(bestStream, currentStream))
                    bestStream = currentStream;
            }

            return true;
        }

        private static bool TryGetPlayerResponse(
            ref ReadOnlySequence<byte> sequence,
            out ReadOnlySequence<byte> playerResponse)
        {
            playerResponse = default;

            while (TryGetKeyValuePair(ref sequence, out var key,
                out var value))
            {
                if (SequenceEqual(key, StatusProperty) &&
                    !SequenceEqual(value, StatusOkValue))
                    return false;
                else if (SequenceEqual(key, PlayerResponseProperty))
                    playerResponse = value;
            }

            return true;
        }

        private static bool IsBetterStream(StreamInfo currentBest,
            StreamInfo stream)
        {
            int currentBestMimeType = 0;
            int checkBestMimeType = 0;

            for (int x = 0; x < KnownContentTypes.Length; x++)
            {
                if (currentBest.MimeType.ValueKind == JsonValueKind.Undefined)
                    return true;
                else if (currentBest.MimeType.ValueEquals(KnownContentTypes[x]))
                    currentBestMimeType = x;
                else if (stream.MimeType.ValueEquals(KnownContentTypes[x]))
                    checkBestMimeType = x;
            }

            return checkBestMimeType <= currentBestMimeType
                && stream.Bitrate >= currentBest.Bitrate;
        }
    }
}
