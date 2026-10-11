using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Originals;

internal readonly struct OriginalsLayout
{
    public const float SignHeight = 30f;
    public const float SectionGap = 8f;

    public readonly Rect Sign;
    public readonly Rect Ladder;
    public readonly Rect World;
    public readonly Rect Status;

    private OriginalsLayout(Rect sign, Rect ladder, Rect world, Rect status)
    {
        Sign = sign;
        Ladder = ladder;
        World = world;
        Status = status;
    }

    public bool HasLadder => Ladder.Height > 0f;

    public static OriginalsLayout Compute(Rect safe, bool ladder, float statusHeight, float scale)
    {
        var signBottom = MathF.Min(safe.Max.Y, safe.Min.Y + SignHeight * scale);
        var sign = new Rect(safe.Min, new Vector2(safe.Max.X, signBottom));
        var gap = SectionGap * scale;
        var top = MathF.Min(safe.Max.Y, signBottom + gap);
        var ladderBottom = ladder ? MathF.Min(safe.Max.Y, top + OriginalsLadder.Height * scale) : top;
        var ladderRect = new Rect(new Vector2(safe.Min.X, top), new Vector2(safe.Max.X, ladderBottom));
        var worldTop = ladder ? MathF.Min(safe.Max.Y, ladderBottom + gap) : top;
        var statusTop = Math.Clamp(safe.Max.Y - MathF.Max(0f, statusHeight), worldTop, safe.Max.Y);
        var world = new Rect(new Vector2(safe.Min.X, worldTop), new Vector2(safe.Max.X, statusTop));
        var status = new Rect(new Vector2(safe.Min.X, statusTop), safe.Max);
        return new OriginalsLayout(sign, ladderRect, world, status);
    }
}
