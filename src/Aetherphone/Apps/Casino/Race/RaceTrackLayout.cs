using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Race;

internal readonly struct RaceTrackLayout
{
    public const float Inset = CasinoStageLayout.SafeInset;
    public const float Gap = 8f;
    public const float ProgressHeight = 24f;
    public const float ProgressTop = 4f;
    public const float FooterHeight = 36f;
    public const float FooterBottom = 16f;
    public const float TopThreeWidth = 138f;
    public const float TopThreeShare = 0.45f;
    public const float SideCaptionMin = 300f;
    public const float TrackGap = 6f;

    public readonly Rect Stand;
    public readonly Rect Progress;
    public readonly Rect Track;
    public readonly Rect TopThree;
    public readonly Rect Caption;
    public readonly bool Stacked;

    private RaceTrackLayout(Rect stand, Rect progress, Rect track, Rect topThree, Rect caption, bool stacked)
    {
        Stand = stand;
        Progress = progress;
        Track = track;
        TopThree = topThree;
        Caption = caption;
        Stacked = stacked;
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
        var rowTop = MathF.Max(progress.Max.Y, footerBottom - FooterHeight * scale);
        var topThreeWidth = MathF.Min(TopThreeWidth * scale, (right - left) * TopThreeShare);
        var sideLeft = MathF.Min(right, left + topThreeWidth + gap);
        var stacked = right - sideLeft < SideCaptionMin * scale;
        Rect topThree;
        Rect caption;
        float footerTop;
        if (stacked)
        {
            var center = (left + right) * 0.5f;
            topThree = new Rect(new Vector2(center - topThreeWidth * 0.5f, rowTop),
                new Vector2(center + topThreeWidth * 0.5f, footerBottom));
            var captionBottom = MathF.Max(progress.Max.Y, rowTop - gap);
            footerTop = MathF.Max(progress.Max.Y, captionBottom - FooterHeight * scale);
            caption = new Rect(new Vector2(left, footerTop), new Vector2(right, captionBottom));
        }
        else
        {
            topThree = new Rect(new Vector2(left, rowTop), new Vector2(left + topThreeWidth, footerBottom));
            caption = new Rect(new Vector2(sideLeft, rowTop), new Vector2(right, footerBottom));
            footerTop = rowTop;
        }

        var trackTop = MathF.Min(footerTop, progress.Max.Y + TrackGap * scale);
        var trackBottom = MathF.Max(trackTop, footerTop - TrackGap * scale);
        var track = new Rect(new Vector2(full.Min.X, trackTop), new Vector2(full.Max.X, trackBottom));
        var stand = new Rect(full.Min, new Vector2(full.Max.X, trackTop));
        return new RaceTrackLayout(stand, progress, track, topThree, caption, stacked);
    }
}
