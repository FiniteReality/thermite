using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Thermite.Core
{
    internal static class JsonElementExtensions
    {
        public static bool TryGetArrayElement(this JsonElement element,
            int index, out JsonElement value)
        {
            value = default;

            if (element.ValueKind != JsonValueKind.Array)
                return false;

            if (index > element.GetArrayLength())
                return false;

            value = element[index];
            return true;
        }

        public static bool TryGetString(this JsonElement element,
            string propertyName, [NotNullWhen(true)]out string? value)
        {
            value = default;

            if (!element.TryGetProperty(propertyName, out var elementValue))
                return false;

            if (elementValue.ValueKind != JsonValueKind.String)
                return false;

            value = elementValue.GetString();
            return true;
        }
    }
}