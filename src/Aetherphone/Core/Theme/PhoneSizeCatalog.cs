namespace Aetherphone.Core.Theme;

internal static class PhoneSizeCatalog
{
    public const float DesignWidth = 360f;
    public const float DesignHeight = 780f;
    public const float AspectRatio = DesignHeight / DesignWidth;
    public const float MinimumWidth = 240f;
    public const float MaximumWidth = 900f;
    public const float DefaultWidth = 486f;
    public const float SnapTolerance = 6f;

    public static readonly IReadOnlyList<float> PresetWidths = new[] { 280f, 320f, 360f, 400f, 450f, 500f };

    public static Vector2 SizeFor(float width) => new(width, MathF.Round(width * AspectRatio));

    public static float ZoomFor(float width) => width / DesignWidth;

    public static float Clamp(float width) => Math.Clamp(width, MinimumWidth, MaximumWidth);

    public static float WidthForScale(float scale) => Clamp(MathF.Round(scale * DesignWidth));

    public static float Snap(float width)
    {
        for (var index = 0; index < PresetWidths.Count; index++)
        {
            if (MathF.Abs(PresetWidths[index] - width) <= SnapTolerance)
            {
                return PresetWidths[index];
            }
        }

        return MathF.Round(width);
    }
}
