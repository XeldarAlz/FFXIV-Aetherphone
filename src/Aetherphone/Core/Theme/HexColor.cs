using System.Globalization;

namespace Aetherphone.Core.Theme;

internal static class HexColor
{
    public static bool TryParse(ReadOnlySpan<char> text, out Vector4 color)
    {
        color = default;
        var trimmed = text.Trim();
        if (trimmed.Length > 0 && trimmed[0] == '#')
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length != 6 ||
            !uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        color = new Vector4(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f, 1f);
        return true;
    }

    public static string ToDigits(Vector4 color)
    {
        var r = (int)Math.Clamp(MathF.Round(color.X * 255f), 0f, 255f);
        var g = (int)Math.Clamp(MathF.Round(color.Y * 255f), 0f, 255f);
        var b = (int)Math.Clamp(MathF.Round(color.Z * 255f), 0f, 255f);
        return $"{r:X2}{g:X2}{b:X2}";
    }
}
