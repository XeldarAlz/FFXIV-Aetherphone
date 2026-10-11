using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Race;

internal readonly struct RaceTrackLayout
{
    public const float Inset = 16f;
    public const float Gap = 8f;
    public const float ProgressHeight = 24f;
    public const float ProgressTop = 4f;
    public const float FooterHeight = 36f;
    public const float FooterBottom = 16f;
    public const float TopThreeWidth = 138f;
    public const float TopThreeShare = 0.45f;
    public const float TrackGap = 6f;

    public readonly Rect Stand;
    public readonly Rect Progress;
    public readonly Rect Track;
    public readonly Rect TopThree;
    public readonly Rect Caption;

    private RaceTrackLayout(Rect stand, Rect progress, Rect track, Rect topThree, Rect caption)
    {
        Stand = stand;
        Progress = progress;
        Track = track;
        TopThree = topThree;
        Caption = caption;
    }

    public static RaceTrackLayout Compute(Rect full, float bandBottom, float scale)
    {
        var inset = Inset * scale;
        var gap = Gap * scale;
        var left = full.Min.X + inset;
        var right = MathF.Max(left, full.Max.X - inset);
        var progressTop = MathF.Min(full.Max.Y, bandBottom + ProgressTop * scale);
        var progress = new Rect(new Vector2(left, progressTop),
            new Vector2(right, MathF.Min(full.Max.Y, progressTop + ProgressHeight * scale)));
        var footerBottom = MathF.Max(progress.Max.Y, full.Max.Y - FooterBottom * scale);
        var footerTop = MathF.Max(progress.Max.Y, footerBottom - FooterHeight * scale);
        var topThreeRight = MathF.Min(left + TopThreeWidth * scale, left + (right - left) * TopThreeShare);
        var topThree = new Rect(new Vector2(left, footerTop), new Vector2(topThreeRight, footerBottom));
        var caption = new Rect(new Vector2(MathF.Min(right, topThreeRight + gap), footerTop),
            new Vector2(right, footerBottom));
        var trackTop = MathF.Min(footerTop, progress.Max.Y + TrackGap * scale);
        var trackBottom = MathF.Max(trackTop, footerTop - TrackGap * scale);
        var track = new Rect(new Vector2(full.Min.X, trackTop), new Vector2(full.Max.X, trackBottom));
        var stand = new Rect(full.Min, new Vector2(full.Max.X, trackTop));
        return new RaceTrackLayout(stand, progress, track, topThree, caption);
    }
}
