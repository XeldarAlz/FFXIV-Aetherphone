using System.Globalization;

namespace Aetherphone.Core.Collections;

internal static class CollectionRarity
{
    public const float Unknown = -1f;
    private const float DecimalBelow = 10f;

    public static float Parse(string? owned)
    {
        if (string.IsNullOrWhiteSpace(owned))
        {
            return Unknown;
        }

        var span = owned.AsSpan().Trim();
        if (span.Length > 0 && span[^1] == '%')
        {
            span = span[..^1].TrimEnd();
        }

        if (!float.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !float.IsFinite(value))
        {
            return Unknown;
        }

        return Math.Clamp(value, 0f, 100f);
    }

    public static double Display(float rarity) =>
        Math.Round(rarity, rarity < DecimalBelow ? 1 : 0, MidpointRounding.AwayFromZero);
}
