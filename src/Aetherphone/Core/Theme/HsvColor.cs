namespace Aetherphone.Core.Theme;

internal readonly record struct HsvColor(float Hue, float Saturation, float Value)
{
    public static HsvColor FromRgb(Vector4 color)
    {
        var largest = MathF.Max(color.X, MathF.Max(color.Y, color.Z));
        var smallest = MathF.Min(color.X, MathF.Min(color.Y, color.Z));
        var span = largest - smallest;
        if (span <= 0f)
        {
            return new HsvColor(0f, 0f, largest);
        }

        var sector = largest <= color.X
            ? (color.Y - color.Z) / span
            : largest <= color.Y
                ? 2f + (color.Z - color.X) / span
                : 4f + (color.X - color.Y) / span;
        var hue = sector / 6f;
        return new HsvColor(hue - MathF.Floor(hue), largest > 0f ? span / largest : 0f, largest);
    }

    public Vector4 ToRgb()
    {
        var sector = (Hue - MathF.Floor(Hue)) * 6f;
        var index = (int)sector;
        var fraction = sector - index;
        var dimmest = Value * (1f - Saturation);
        var falling = Value * (1f - Saturation * fraction);
        var rising = Value * (1f - Saturation * (1f - fraction));
        return index switch
        {
            0 => new Vector4(Value, rising, dimmest, 1f),
            1 => new Vector4(falling, Value, dimmest, 1f),
            2 => new Vector4(dimmest, Value, rising, 1f),
            3 => new Vector4(dimmest, falling, Value, 1f),
            4 => new Vector4(rising, dimmest, Value, 1f),
            _ => new Vector4(Value, dimmest, falling, 1f),
        };
    }
}
