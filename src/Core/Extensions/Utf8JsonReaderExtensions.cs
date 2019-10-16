using System.Text.Json;

namespace Thermite.Core
{
    internal static class Utf8JsonReaderExtensions
    {
        public static bool TryReadToken(ref this Utf8JsonReader reader,
            JsonTokenType tokenType)
        {
            var copy = reader;
            var status = copy.Read() && copy.TokenType == tokenType;

            if (status)
                _ = reader.Read();

            return status;
        }
    }
}