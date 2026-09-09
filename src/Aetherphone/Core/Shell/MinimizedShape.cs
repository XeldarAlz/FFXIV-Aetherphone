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

    private static readonly float[] MapSides = { 118f, 148f, 184f };

    private static readonly LocString[] ShapeLabels =
    {
        L.Minimized.ShapePhone, L.Minimized.ShapeMinimap,
    };

    private static readonly LocString[] MapSizeLabels =
    {
        L.Minimized.SizeSmall, L.Minimized.SizeMedium, L.Minimized.SizeLarge,
    };

    public static float MapSide(MinimizedMapSize size) => MapSides[(int)size];

    public static LocString Label(MinimizedShape shape) => ShapeLabels[(int)shape];

    public static LocString Label(MinimizedMapSize size) => MapSizeLabels[(int)size];
}
