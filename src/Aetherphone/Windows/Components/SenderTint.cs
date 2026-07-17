namespace Aetherphone.Windows.Components;

internal static class SenderTint
{
    public static Vector4 Of(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return new Vector4(0.62f, 0.62f, 0.66f, 1f);
        }

        var hash = 2166136261u;
        for (var index = 0; index < name.Length; index++)
        {
            hash = (hash ^ name[index]) * 16777619u;
        }

        var hue = hash % 360u / 360f;
        return FromHsv(hue, 0.52f, 0.95f);
    }

    private static Vector4 FromHsv(float hue, float saturation, float value)
    {
        var sector = (int)MathF.Floor(hue * 6f);
        var fraction = hue * 6f - sector;
        var p = value * (1f - saturation);
        var q = value * (1f - fraction * saturation);
        var t = value * (1f - (1f - fraction) * saturation);
        return (sector % 6) switch
        {
            0 => new Vector4(value, t, p, 1f),
            1 => new Vector4(q, value, p, 1f),
            2 => new Vector4(p, value, t, 1f),
            3 => new Vector4(p, q, value, 1f),
            4 => new Vector4(t, p, value, 1f),
            _ => new Vector4(value, p, q, 1f),
        };
    }
}
