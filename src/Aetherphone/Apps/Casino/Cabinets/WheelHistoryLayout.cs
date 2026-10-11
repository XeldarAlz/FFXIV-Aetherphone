using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Cabinets;

internal readonly struct WheelHistoryLayout
{
    public const float Height = 28f;
    public const float BelowGap = 6f;
    public const float CaptionGap = 10f;
    public const float CaptionShare = 0.4f;
    public const float ChipHeight = 22f;
    public const float ChipPadX = 7f;
    public const float ChipMinWidth = 32f;
    public const float ChipGap = 4f;
    public const float LatestGap = 8f;
    public const float LatestGrow = 4f;
    public const float NewerFade = 0.85f;
    public const float OldestFade = 0.4f;

    public readonly Rect Strip;
    public readonly Rect Caption;
    public readonly int Visible;

    private readonly float chipsLeft;
    private readonly float chipWidth;
    private readonly float latestWidth;
    private readonly float chipHeight;
    private readonly float chipGap;
    private readonly float latestGap;

    private WheelHistoryLayout(Rect strip, Rect caption, int visible, float chipsLeft, float chipWidth,
        float latestWidth, float chipHeight, float chipGap, float latestGap)
    {
        Strip = strip;
        Caption = caption;
        Visible = visible;
        this.chipsLeft = chipsLeft;
        this.chipWidth = chipWidth;
        this.latestWidth = latestWidth;
        this.chipHeight = chipHeight;
        this.chipGap = chipGap;
        this.latestGap = latestGap;
    }

    public static Rect StripOf(Rect safe, float scale) =>
        new(safe.Min, new Vector2(safe.Max.X, MathF.Min(safe.Max.Y, safe.Min.Y + Height * scale)));

    public static WheelHistoryLayout Compute(Rect strip, float captionWidth, float chipLabelWidth,
        float latestLabelWidth, int count, float scale)
    {
        var captionRight = strip.Min.X + MathF.Min(MathF.Max(0f, captionWidth), strip.Width * CaptionShare);
        var caption = new Rect(strip.Min, new Vector2(captionRight, strip.Max.Y));
        var pad = ChipPadX * scale;
        var chip = MathF.Max(ChipMinWidth * scale, chipLabelWidth + pad * 2f);
        var latest = MathF.Max(chip + LatestGrow * scale, latestLabelWidth + pad * 2f);
        var gap = ChipGap * scale;
        var leading = LatestGap * scale;
        var left = captionRight + CaptionGap * scale;
        var room = strip.Max.X - left;
        var visible = 0;
        if (count > 0 && room >= latest)
        {
            visible = 1 + Math.Max(0, (int)MathF.Floor((room - latest - leading + gap) / (chip + gap)));
            visible = Math.Min(visible, count);
        }

        var height = MathF.Min(ChipHeight * scale, strip.Height);
        return new WheelHistoryLayout(strip, caption, visible, left, chip, latest, height, gap, leading);
    }

    public Rect Chip(int index)
    {
        if (index == 0)
        {
            return new Rect(new Vector2(chipsLeft, Strip.Min.Y), new Vector2(chipsLeft + latestWidth, Strip.Max.Y));
        }

        var left = chipsLeft + latestWidth + latestGap + (index - 1) * (chipWidth + chipGap);
        var top = Strip.Center.Y - chipHeight * 0.5f;
        return new Rect(new Vector2(left, top), new Vector2(left + chipWidth, top + chipHeight));
    }

    public float Fade(int index)
    {
        if (index == 0)
        {
            return 1f;
        }

        if (Visible <= 2)
        {
            return NewerFade;
        }

        var age = (index - 1) / (float)(Visible - 2);
        return NewerFade + (OldestFade - NewerFade) * age;
    }
}
