namespace Aetherphone.Core.Shell;

internal readonly record struct CaptureWindow(Vector2 Uv0, Vector2 Uv1, Vector2 DestinationMin, Vector2 DestinationMax)
{
    public static readonly CaptureWindow Full = new(Vector2.Zero, Vector2.One, Vector2.Zero, Vector2.One);
    public static readonly CaptureWindow Empty = new(Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero);

    public bool Visible => Uv1.X > Uv0.X && Uv1.Y > Uv0.Y;
}

internal static class LiveBackdropPlan
{
    public const int Depth = 4;
    public const int SmoothLevel = 1;
    public const float CompositeFeedbackBlend = 0.7f;
    public const float WorldFeedbackBlend = 1f;
    public const float RegionPadding = 24f;
    public const float SampleReach = 8f;

    public static Rect Pad(Rect rect, float padding)
    {
        var grow = new Vector2(padding, padding);
        return new Rect(rect.Min - grow, rect.Max + grow);
    }

    public static Rect Union(Rect first, Rect second) =>
        new(Vector2.Min(first.Min, second.Min), Vector2.Max(first.Max, second.Max));

    public static bool Covers(Rect region, Rect rect) =>
        rect.Min.X >= region.Min.X && rect.Min.Y >= region.Min.Y &&
        rect.Max.X <= region.Max.X && rect.Max.Y <= region.Max.Y;

    public static (int Width, int Height) LevelSize(Vector2 regionPixels, int level)
    {
        var divisor = 1 << (level + 1);
        return (Shrink(regionPixels.X, divisor), Shrink(regionPixels.Y, divisor));
    }

    public static (int Width, int Height) SmoothSize(Vector2 regionPixels) => LevelSize(regionPixels, SmoothLevel);

    public static CaptureWindow Window(Rect screen, Rect viewport)
    {
        var viewportSize = viewport.Size;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return CaptureWindow.Empty;
        }

        var uvMin = (screen.Min - viewport.Min) / viewportSize;
        var uvMax = (screen.Max - viewport.Min) / viewportSize;
        var span = uvMax - uvMin;
        if (span.X <= 0f || span.Y <= 0f)
        {
            return CaptureWindow.Empty;
        }

        var clampedMin = Vector2.Clamp(uvMin, Vector2.Zero, Vector2.One);
        var clampedMax = Vector2.Clamp(uvMax, Vector2.Zero, Vector2.One);
        var destinationMin = (clampedMin - uvMin) / span;
        var destinationMax = (clampedMax - uvMin) / span;
        return new CaptureWindow(clampedMin, clampedMax, destinationMin, destinationMax);
    }

    public static float FeedbackBlend(LiveGlassSource source) =>
        source == LiveGlassSource.Composite ? CompositeFeedbackBlend : WorldFeedbackBlend;

    private static int Shrink(float pixels, int divisor) => Math.Max(1, (int)MathF.Ceiling(pixels / divisor));
}
