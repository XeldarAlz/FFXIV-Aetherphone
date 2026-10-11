namespace Aetherphone.Windows.Components;

internal readonly struct ScrollThumb
{
    public const float InsetUnits = 2f;
    public const float MinHeightUnits = 24f;

    private readonly float maxY;

    private ScrollThumb(float height, float travel, float top, float maxY)
    {
        Height = height;
        Travel = travel;
        Top = top;
        this.maxY = maxY;
    }

    public float Height { get; }

    public float Travel { get; }

    public float Top { get; }

    public static ScrollThumb Measure(float viewTop, float viewHeight, float scrollY, float maxY, float scale)
    {
        var range = MathF.Max(0f, maxY);
        var inset = InsetUnits * scale;
        var height = MathF.Max(MinHeightUnits * scale, viewHeight * viewHeight / (viewHeight + range));
        var travel = MathF.Max(0f, viewHeight - height - inset * 2f);
        var progress = range > 0f ? Math.Clamp(scrollY / range, 0f, 1f) : 0f;
        return new ScrollThumb(height, travel, viewTop + inset + travel * progress, range);
    }

    public float ScrollDelta(float pointerDelta) => Travel > 0f ? pointerDelta * maxY / Travel : 0f;
}
