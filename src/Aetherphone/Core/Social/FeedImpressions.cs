namespace Aetherphone.Core.Social;

internal sealed class FeedImpressions
{
    public const float VisibleFraction = 0.5f;

    public const float ViewportFraction = 0.6f;

    public const float DwellSeconds = 1f;

    private const float MaxFrameSeconds = 0.25f;

    private const int WatchingCap = 64;

    private const int ReportedCap = 600;

    private readonly Dictionary<string, float> watching = new(StringComparer.Ordinal);
    private readonly HashSet<string> reported = new(StringComparer.Ordinal);
    private float windowTop;
    private float windowBottom;
    private float frameSeconds;

    public void BeginFrame(float top, float bottom, float deltaSeconds)
    {
        windowTop = top;
        windowBottom = bottom;
        frameSeconds = deltaSeconds < 0f ? 0f : MathF.Min(deltaSeconds, MaxFrameSeconds);
    }

    public bool Observe(string postId, float rowTop, float rowBottom)
    {
        if (reported.Contains(postId))
        {
            return false;
        }

        var height = rowBottom - rowTop;
        if (height <= 0f)
        {
            return false;
        }

        var visible = MathF.Min(rowBottom, windowBottom) - MathF.Max(rowTop, windowTop);
        var viewport = windowBottom - windowTop;
        var required = MathF.Min(height * VisibleFraction, viewport * ViewportFraction);
        if (visible <= 0f || visible < required)
        {
            watching.Remove(postId);
            return false;
        }

        var dwell = watching.GetValueOrDefault(postId) + frameSeconds;
        if (dwell < DwellSeconds)
        {
            if (watching.Count < WatchingCap || watching.ContainsKey(postId))
            {
                watching[postId] = dwell;
            }

            return false;
        }

        watching.Remove(postId);
        if (reported.Count >= ReportedCap)
        {
            reported.Clear();
        }

        reported.Add(postId);
        return true;
    }

    public void Reset()
    {
        watching.Clear();
        reported.Clear();
    }
}
