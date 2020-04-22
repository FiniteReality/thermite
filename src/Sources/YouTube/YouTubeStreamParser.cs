using System;
using System.Buffers;
using System.Buffers.Text;
using System.Linq;
using System.Text;
using System.Text.Json;

using static Thermite.Utilities.TextParsingUtilities;
using static Thermite.Utilities.UrlParsingUtilities;

namespace Thermite.Sources.YouTube
{
    internal static class YoutubeStreamParser
    {
        private ref struct StreamInfo
        {
            public int Bitrate;
            public int MimeTypeRanking;
            public int SampleRate;
            public ReadOnlySpan<byte> Url;
            public ReadOnlySpan<byte> MimeType;
        }

        private static readonly byte[] PlayerResponseProperty =
            Encoding.UTF8.GetBytes("player_response");
        private static readonly byte[] AdaptiveFmtsProperty =
            Encoding.UTF8.GetBytes("adaptive_fmts");
        private static readonly byte[] UrlEncodedStreamFmtMapProperty =
            Encoding.UTF8.GetBytes("url_encoded_fmt_stream_map");
        private static readonly byte[] StatusProperty =
            Encoding.UTF8.GetBytes("status");

        private static readonly byte[] StatusOkValue =
            Encoding.UTF8.GetBytes("ok");

        private static readonly byte[] TitleProperty =
            Encoding.UTF8.GetBytes("title");

        private static readonly byte[] BitrateProperty =
            Encoding.UTF8.GetBytes("bitrate");
        private static readonly byte[] UrlProperty =
            Encoding.UTF8.GetBytes("url");
        private static readonly byte[] TypeProperty =
            Encoding.UTF8.GetBytes("type");
        private static readonly byte[] AudioSampleRateProperty =
            Encoding.UTF8.GetBytes("audio_sample_rate");

        private static readonly byte[] QualityProperty =
            Encoding.UTF8.GetBytes("quality");

        // lower values are higher quality
        private static readonly byte[][] KnownQualityLevels =
        {
            Encoding.UTF8.GetBytes("hd720"), // -0
            Encoding.UTF8.GetBytes("medium"), // -1
            Encoding.UTF8.GetBytes("small"), // -2
            Encoding.UTF8.GetBytes("tiny"), // -3
        };

        private static readonly byte[][] KnownContentTypes =
        {
            Encoding.UTF8.GetBytes("audio/webm; codecs=\"opus\""), // -0
            Encoding.UTF8.GetBytes("audio/mp4; codecs=\"mp4a.40.2\""), // -1

            // TODO: these are based on quality level for
            Encoding.UTF8.GetBytes(
                "video/mp4; codecs=\"avc1.64001F, mp4a.40.2\""), // -2
            Encoding.UTF8.GetBytes(
                "video/webm; codecs=\"vp8.0, vorbis\""), // -3
            Encoding.UTF8.GetBytes(
                "video/mp4; codecs=\"avc1.42001E, mp4a.40.2\""), // -4
            //"some content type", // ?
        };

        public static bool GetBestStream(ReadOnlySequence<byte> sequence,
            out TrackInfo track)
        {
            track = default;
            if (!TryGetStreamInfo(ref sequence, out var adaptiveStreams,
                out var streamMap, out var playerResponse))
                return false;

            if (adaptiveStreams.IsEmpty && streamMap.IsEmpty)
                return false;

            if (!TryParseAdaptiveStreams(adaptiveStreams,
                out var bestAdaptiveStream))
                return false;

            if (!TryParseStreamMap(streamMap,
                out var bestStreamMapStream))
                return false;

            var bestStream = bestStreamMapStream;
            if (IsBetterStream(bestStream, bestAdaptiveStream))
                bestStream = bestAdaptiveStream;

            if (!TryGetTitle(playerResponse, out var title))
                return false;
            if (!TryGetString(bestStream.Url, out var location))
                return false;
            if (!TryGetString(bestStream.MimeType, out var mediaType))
                return false;

            track.TrackName = title!;
            track.AudioLocation = new Uri(location!);
            track.MediaTypeOverride = mediaType;
            if (mediaType != null)
                track.CodecOverride = GetCodec(mediaType);
            return true;

            static IAudioCodec? GetCodec(string mediaType)
            {
                const string CodecsString = "codecs=\"";
                var codecsIndex = mediaType.IndexOf(CodecsString);

                if (codecsIndex < 0)
                    return default;

                var start = codecsIndex + CodecsString.Length;
                //return mediaType[start..^1];
                return null;
            }

            static bool TryGetString(ReadOnlySpan<byte> input,
                out string? value)
            {
                input = input.TrimEnd((byte)0);
                value = default;
                var buffer = ArrayPool<byte>.Shared.Rent(input.Length);

                if (!TryUrlDecode(input, buffer, out var decodedLength))
                    return false;

                value = Encoding.UTF8.GetString(
                    buffer.AsSpan().Slice(0, decodedLength));
                ArrayPool<byte>.Shared.Return(buffer);
                return true;
            }

            static bool TryGetTitle(ReadOnlySequence<byte> playerResponse,
                out string? title)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(
                    (int)playerResponse.Length);
                playerResponse.CopyTo(buffer);

                var status = ParseInternal(buffer.AsSpan()
                    .Slice(0, (int)playerResponse.Length),
                    out title);

                ArrayPool<byte>.Shared.Return(buffer);
                return status;

                static bool ParseInternal(Span<byte> buffer, out string? title)
                {
                    title = default;
                    if (!TryUrlDecode(buffer, out var decodedLength))
                        return false;

                    buffer = buffer.Slice(0, decodedLength);
                    ReadOnlySpan<byte> playerResponse = buffer;

                    var reader = new Utf8JsonReader(playerResponse);

                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName &&
                            reader.ValueTextEquals(TitleProperty))
                        {
                            if (reader.TryReadToken(JsonTokenType.String))
                            {
                                title = reader.GetString();
                                return true;
                            }
                        }
                    }

                    return false;
                }
            }
        }

        private static bool TryGetStreamInfo(
            ref ReadOnlySequence<byte> sequence,
            out ReadOnlySequence<byte> adaptiveStreams,
            out ReadOnlySequence<byte> streamMap,
            out ReadOnlySequence<byte> playerResponse)
        {
            adaptiveStreams = default;
            streamMap = default;
            playerResponse = default;

            while (TryGetKeyValuePair(ref sequence, out var key,
                out var value))
            {
                if (SequenceEqual(key, StatusProperty) &&
                    !SequenceEqual(value, StatusOkValue))
                    return false;
                else if (SequenceEqual(key, PlayerResponseProperty))
                    playerResponse = value;
                else if (SequenceEqual(key, AdaptiveFmtsProperty))
                    adaptiveStreams = value;
                else if (SequenceEqual(key, UrlEncodedStreamFmtMapProperty))
                    streamMap = value;
            }

            return true;
        }

        private static bool TryParseAdaptiveStreams(
            ReadOnlySequence<byte> adaptiveStreams,
            out StreamInfo bestStream)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(
                (int)adaptiveStreams.Length);
            adaptiveStreams.CopyTo(buffer);

            var status = ParseInternal(buffer.AsSpan()
                .Slice(0, (int)adaptiveStreams.Length),
                out bestStream);

            ArrayPool<byte>.Shared.Return(buffer);
            return status;

            static bool ParseInternal(Span<byte> buffer,
                out StreamInfo bestStream)
            {
                bestStream = default;
                if (!TryUrlDecode(buffer, out var decodedLength))
                    return false;

                buffer = buffer.Slice(0, decodedLength);
                ReadOnlySpan<byte> adaptiveStreams = buffer;

                while (TryReadTo(ref adaptiveStreams, (byte)',',
                    out var stream))
                {
                    if (!TryParseAdaptiveStream(stream, ref bestStream))
                        return false;
                }

                return true;
            }

            static bool TryParseAdaptiveStream(
                ReadOnlySpan<byte> adaptiveStream,
                ref StreamInfo bestStream)
            {
                StreamInfo stream = default;

                while (TryGetKeyValuePair(ref adaptiveStream, out var key,
                    out var value))
                {
                    if (key.SequenceEqual(BitrateProperty))
                    {
                        if (!Utf8Parser.TryParse(value, out int bitrate,
                            out int _))
                            return false;

                        stream.Bitrate = bitrate;
                    }
                    else if (key.SequenceEqual(UrlProperty))
                        stream.Url = value;
                    else if (key.SequenceEqual(TypeProperty))
                        stream.MimeType = value;
                    else if (key.SequenceEqual(AudioSampleRateProperty))
                    {
                        if (!Utf8Parser.TryParse(value, out int sampleRate,
                            out int _))
                            return false;

                        stream.SampleRate = sampleRate;
                    }
                }

                if (!IsKnownContentType(stream.MimeType, out var position))
                    position = int.MaxValue;

                stream.MimeTypeRanking = -position;

                if (IsBetterStream(bestStream, stream))
                    bestStream = stream;

                return true;
            }
        }

        private static bool TryParseStreamMap(ReadOnlySequence<byte> streamMap,
            out StreamInfo bestStream)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(
                (int)streamMap.Length);
            streamMap.CopyTo(buffer);

            var status = ParseInternal(buffer.AsSpan()
                .Slice(0, (int)streamMap.Length),
                out bestStream);

            ArrayPool<byte>.Shared.Return(buffer);
            return status;

            static bool ParseInternal(Span<byte> buffer,
                out StreamInfo bestStream)
            {
                bestStream = default;
                if (!TryUrlDecode(buffer, out var decodedLength))
                    return false;

                buffer = buffer.Slice(0, decodedLength);
                ReadOnlySpan<byte> streamMap = buffer;

                while (TryReadTo(ref streamMap, (byte)',', out var stream))
                {
                    if (!TryParseStream(stream, ref bestStream))
                        return false;
                }

                return true;
            }

            static bool TryParseStream(ReadOnlySpan<byte> streamMapStream,
                ref StreamInfo bestStream)
            {
                StreamInfo stream = default;

                while (TryGetKeyValuePair(ref streamMapStream, out var key,
                    out var value))
                {
                    if (key.SequenceEqual(UrlProperty))
                        stream.Url = value;
                    else if (key.SequenceEqual(QualityProperty))
                    {
                        if (!IsKnownQuality(value, out var qualityPosition))
                            return false;
                        stream.Bitrate = -qualityPosition;
                    }
                    else if (key.SequenceEqual(TypeProperty))
                        stream.MimeType = value;
                }

                if (!IsKnownContentType(stream.MimeType,
                    out var contentPosition))
                    contentPosition = int.MaxValue;

                stream.MimeTypeRanking = -contentPosition;

                if (IsBetterStream(bestStream, stream))
                    bestStream = stream;

                return true;
            }

            static bool IsKnownQuality(ReadOnlySpan<byte> quality,
                out int position)
            {
                for (int i = 0; i < KnownQualityLevels.Length; i++)
                {
                    if (quality.SequenceEqual(KnownQualityLevels[i]))
                    {
                        position = i;
                        return true;
                    }
                }

                position = default;
                return false;
            }
        }

        private static bool IsKnownContentType(ReadOnlySpan<byte> contentType,
            out int position)
        {
            position = default;
            Span<byte> decodedContentType =
                stackalloc byte[contentType.Length];

            if (!TryUrlDecode(contentType, decodedContentType,
                out int decodedLength))
                return false;

            decodedContentType = decodedContentType.Slice(0, decodedLength);

            for (int i = 0; i < KnownContentTypes.Length; i++)
            {
                if (contentType.SequenceEqual(KnownContentTypes[i]))
                {
                    position = i;
                    return true;
                }
            }

            position = default;
            return false;
        }

        private static bool IsBetterStream(StreamInfo currentBest,
            StreamInfo stream)
        {
            if (stream.MimeTypeRanking > currentBest.MimeTypeRanking)
                return true;

            if (stream.Bitrate > currentBest.Bitrate)
                return true;

            var streamDistance = 48000 - stream.SampleRate;
            var currentBestDistance = 48000 - currentBest.SampleRate;

            if (streamDistance < 0)
                streamDistance = -streamDistance;

            if (currentBestDistance < 0)
                currentBestDistance = -currentBestDistance;

            return streamDistance < currentBestDistance;
        }
    }
}
