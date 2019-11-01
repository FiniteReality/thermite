using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Thermite.Core.Sources.YouTube
{
    internal static class YouTubePlaylistParser
    {
        private static readonly byte[] PlaylistVideoListRendererProperty =
            Encoding.UTF8.GetBytes("playlistVideoListRenderer");
        private static readonly byte[] PlaylistVideoListContinuationProperty =
            Encoding.UTF8.GetBytes("playlistVideoListContinuation");
        private static readonly byte[] AlertsProperty =
            Encoding.UTF8.GetBytes("alerts");
        private static readonly byte[] AlertRendererProperty =
            Encoding.UTF8.GetBytes("alertRenderer");
        private static readonly byte[] TextProperty =
            Encoding.UTF8.GetBytes("text");
        private static readonly byte[] SimpleTextProperty =
            Encoding.UTF8.GetBytes("simpleText");

        public static bool TryReadPreamble(
            ref ReadOnlySequence<byte> sequence,
            ref JsonReaderState state,
            [NotNullWhen(false)] out string? alert)
        {
            alert = default;
            var reader = new Utf8JsonReader(sequence, true, state);

            int alertDepth = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (reader.ValueTextEquals(
                        PlaylistVideoListRendererProperty)
                        || reader.ValueTextEquals(
                        PlaylistVideoListContinuationProperty))
                    {
                        sequence = sequence.Slice(reader.Position);
                        state = reader.CurrentState;
                        return true;
                    }
                    else
                    {
                        switch (alertDepth)
                        {
                            case 0
                                when reader.ValueTextEquals(AlertsProperty):
                            case 1
                                when reader.ValueTextEquals(
                                    AlertRendererProperty):
                            case 2
                                when reader.ValueTextEquals(TextProperty):
                                alertDepth++;
                                break;
                            case 3
                                when reader.ValueTextEquals(
                                    SimpleTextProperty):
                            {
                                if (!TryGetString(ref reader, out alert))
                                    alert = "An error occured";
                                return false;
                            }
                            default:
                                if (alertDepth > 0)
                                    alertDepth--;
                                break;
                        }
                    }
                }
            }

            return false;
        }

        private static readonly byte[] PlaylistVideoRendererProperty =
            Encoding.UTF8.GetBytes("playlistVideoRenderer");
        private static readonly byte[] VideoIdPropertyName =
            Encoding.UTF8.GetBytes("videoId");
        private static readonly byte[] SidebarPropertyName =
            Encoding.UTF8.GetBytes("sidebar");

        public static bool TryReadPlaylistItem(
            ref ReadOnlySequence<byte> sequence,
            ref JsonReaderState state,
            [NotNullWhen(true)] out string? videoId)
        {
            videoId = default;
            var reader = new Utf8JsonReader(sequence, true, state);

            int savedDepth = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (reader.ValueTextEquals(PlaylistVideoRendererProperty))
                    {
                        savedDepth = reader.CurrentDepth;
                    }
                    else if (reader.ValueTextEquals(VideoIdPropertyName))
                    {
                        if (!TryGetString(ref reader, out videoId))
                            return false;

                        while (reader.CurrentDepth > savedDepth)
                            if (!reader.Read())
                                return false;

                        sequence = sequence.Slice(reader.Position);
                        state = reader.CurrentState;
                        return true;
                    }
                    else if (reader.ValueTextEquals(SidebarPropertyName))
                    {
                        if (!reader.TrySkip())
                            return false;
                    }
                }
            }

            return false;
        }

        private static readonly byte[] ContinuationPropertyName =
            Encoding.UTF8.GetBytes("continuation");
        public static bool TryGetContinuation(
            ref ReadOnlySequence<byte> sequence,
            ref JsonReaderState state,
            [NotNullWhen(true)] out string? continuation)
        {
            continuation = default;
            var reader = new Utf8JsonReader(sequence, true, state);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName
                    && reader.ValueTextEquals(ContinuationPropertyName))
                {
                    if (!TryGetString(ref reader, out continuation))
                        return false;

                    sequence = sequence.Slice(reader.Position);
                    state = reader.CurrentState;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetString(ref Utf8JsonReader reader,
            [NotNullWhen(true)] out string? value)
        {
            value = default;

            if (!reader.TryReadToken(JsonTokenType.String))
                return false;

            if (!reader.HasValueSequence)
            {
                value = Encoding.UTF8.GetString(reader.ValueSpan);
                return true;
            }

            var length = (int)reader.ValueSequence.Length;
            var buffer = ArrayPool<byte>.Shared.Rent(length);
            reader.ValueSequence.CopyTo(buffer);

            value = Encoding.UTF8.GetString(buffer, 0, length);
            ArrayPool<byte>.Shared.Return(buffer);

            return true;
        }
    }
}
