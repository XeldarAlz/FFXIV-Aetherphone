using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Shell;

internal enum MinimizedShape : byte
{
    Phone,
    Minimap,
}

internal enum MinimizedMapSize : byte
{
    Small,
    Medium,
    Large,
}

internal static class MinimizedShapes
{
    public const int ShapeCount = 2;
    public const int MapSizeCount = 3;
    public const int MapZoomCount = 7;
    public const int DefaultMapZoom = 3;
    public const float MinScale = 0.75f;
    public const float MaxScale = 2.5f;
    private const float ScaleSnapTolerance = 0.04f;

    private static readonly float[] MapSides = { 118f, 148f, 184f };

    private static readonly float[] MapSpans = { 140f, 108f, 82f, 62f, 46f, 34f, 24f };

    private static readonly LocString[] ShapeLabels =
    {
        L.Minimized.ShapePhone, L.Minimized.ShapeMinimap,
    };

    private static readonly LocString[] MapSizeLabels =
    {
        L.Minimized.SizeSmall, L.Minimized.SizeMedium, L.Minimized.SizeLarge,
    };

    public static float MapSide(MinimizedMapSize size) => MapSides[(int)size];

    public static float MapSpan(int zoom) => MapSpans[ClampZoom(zoom)];

    public static int ClampZoom(int zoom) => Math.Clamp(zoom, 0, MapZoomCount - 1);

    public static float SnapScale(float scale)
    {
        if (MathF.Abs(scale - 1f) <= ScaleSnapTolerance)
        {
            return 1f;
        }

        return MathF.Round(scale, 2);
    }

    public static LocString Label(MinimizedShape shape) => ShapeLabels[(int)shape];

    public static LocString Label(MinimizedMapSize size) => MapSizeLabels[(int)size];
}
