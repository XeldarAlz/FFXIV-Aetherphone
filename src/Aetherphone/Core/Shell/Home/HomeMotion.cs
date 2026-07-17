namespace Aetherphone.Core.Shell.Home;

internal readonly struct HomeMotion
{
    public readonly float Zoom;
    public readonly Vector2 Pivot;
    public readonly float Progress;
    public readonly bool Interactive;

    public HomeMotion(float zoom, Vector2 pivot, float progress, bool interactive)
    {
        Zoom = zoom;
        Pivot = pivot;
        Progress = progress;
        Interactive = interactive;
    }

    public static HomeMotion Rest => new(1f, default, 0f, true);

    public Vector2 Warp(Vector2 point) => Pivot + (point - Pivot) * Zoom;

    public Rect Warp(Rect rect) => new(Warp(rect.Min), Warp(rect.Max));
}
