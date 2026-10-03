namespace Aetherphone.Core.Shell.Home;

internal readonly struct HomeMotion
{
    private const float RecessionRate = 1.6f;
    public const float WallpaperZoomDepth = 0.06f;

    public readonly float Zoom;
    public readonly Vector2 Pivot;
    public readonly float Progress;
    public readonly bool Interactive;
    public readonly string? RevealAppId;
    public readonly float StageZoom;
    public readonly Vector2 StagePivot;

    public HomeMotion(float zoom, Vector2 pivot, float progress, bool interactive, string? revealAppId = null)
        : this(zoom, pivot, progress, interactive, revealAppId, 1f, default)
    {
    }

    private HomeMotion(float zoom, Vector2 pivot, float progress, bool interactive, string? revealAppId,
        float stageZoom, Vector2 stagePivot)
    {
        Zoom = zoom;
        Pivot = pivot;
        Progress = progress;
        Interactive = interactive;
        RevealAppId = revealAppId;
        StageZoom = stageZoom;
        StagePivot = stagePivot;
    }

    public static HomeMotion Rest => new(1f, default, 0f, true);

    public static HomeMotion Still => new(1f, default, 0f, false);

    public static HomeMotion Recede(float progress, string? revealAppId) => new(1f, default, progress, false, revealAppId);

    public static HomeMotion Recede(float progress, string? revealAppId, float stageZoom, Vector2 stagePivot) =>
        new(1f, default, progress, false, revealAppId, stageZoom, stagePivot);

    public float Recession => Math.Clamp(Progress * RecessionRate, 0f, 1f);

    public float WallpaperZoom => 1f + WallpaperZoomDepth * Math.Clamp(Progress, 0f, 1f);

    public Vector2 Warp(Vector2 point) => Pivot + (point - Pivot) * Zoom;

    public Rect Warp(Rect rect) => new(Warp(rect.Min), Warp(rect.Max));

    public Rect WallpaperQuad(Rect screen)
    {
        var zoomed = ScaleAbout(screen, screen.Center, WallpaperZoom);
        if (StageZoom <= 0f || StageZoom == 1f)
        {
            return zoomed;
        }

        return ScaleAbout(zoomed, StagePivot, 1f / StageZoom);
    }

    public bool Reveals(string appId) => RevealAppId is not null && string.Equals(RevealAppId, appId, StringComparison.Ordinal);

    private static Rect ScaleAbout(Rect rect, Vector2 pivot, float factor) =>
        new(pivot + (rect.Min - pivot) * factor, pivot + (rect.Max - pivot) * factor);
}
