namespace Aetherphone.Core.Theme;

internal static class TextZoomCatalog
{
    public const float MinimumZoom = 0.7f;
    public const float MaximumZoom = 1.5f;
    public const float SnapTolerance = 0.02f;

    public static readonly IReadOnlyList<float> PresetZooms = new[] { 0.8f, 0.9f, 1.0f, 1.15f, 1.3f, 1.5f };

    public static float Clamp(float zoom) => Math.Clamp(zoom, MinimumZoom, MaximumZoom);

    public static float Snap(float zoom)
    {
        for (var index = 0; index < PresetZooms.Count; index++)
        {
            if (MathF.Abs(PresetZooms[index] - zoom) <= SnapTolerance)
            {
                return PresetZooms[index];
            }
        }

        return MathF.Round(zoom * 100f) / 100f;
    }
}
