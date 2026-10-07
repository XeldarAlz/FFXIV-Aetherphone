using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Games.Hub;

internal static class HubMetrics
{
    public const float SectionGap = Metrics.Space.Xl;
    public const float HeaderGap = Metrics.Space.Xxs;
    public const float CardRadius = Metrics.Radius.Grouped;
    public const float SmallCardRadius = Metrics.Radius.Lg;
    public const float IconRadiusFactor = Metrics.Radius.HomeTileFactor;
    public const float RailBleed = AppSurface.SidePadding;
    public const float TiltDepth = HomeTileView.TiltDepth;
    public const float CaptionGap = Metrics.Space.Xs;
    public const float GridMinTile = 70f;
    public const float GridGapX = 14f;
    public const float GridGapY = 18f;

    public static readonly Vector4 Ember = new(0.98f, 0.72f, 0.34f, 1f);

    public static int GridColumns(float width, float minTile, float gap) =>
        Math.Max(1, (int)((width + gap) / (minTile + gap)));

    public static float GridTile(float width, int columns, float gap) =>
        columns <= 1 ? width : (width - gap * (columns - 1)) / columns;
}
