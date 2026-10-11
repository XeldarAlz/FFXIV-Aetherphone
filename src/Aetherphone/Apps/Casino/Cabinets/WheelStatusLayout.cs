using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Cabinets;

internal readonly struct WheelStatusLayout
{
    public const float PillHeight = 30f;
    public const float PadX = 12f;
    public const float DotRadius = 4f;
    public const float DotGap = 8f;
    public const float ClockGap = 10f;
    public const float GlyphBox = 14f;
    public const float GlyphGap = 5f;
    public const float CrowdPadX = 10f;
    public const float PillGap = 8f;

    public readonly Rect Row;
    public readonly Rect Phase;
    public readonly Vector2 Dot;
    public readonly Rect Label;
    public readonly Rect Clock;
    public readonly Rect Crowd;
    public readonly Vector2 Glyph;
    public readonly Rect Count;

    private WheelStatusLayout(Rect row, Rect phase, Vector2 dot, Rect label, Rect clock, Rect crowd, Vector2 glyph,
        Rect count)
    {
        Row = row;
        Phase = phase;
        Dot = dot;
        Label = label;
        Clock = clock;
        Crowd = crowd;
        Glyph = glyph;
        Count = count;
    }

    public bool HasClock => Clock.Width > 0f;

    public bool HasCrowd => Crowd.Width > 0f;

    public static Rect RowOf(Rect ribbon, float scale)
    {
        var inset = CasinoStageLayout.SafeInset * scale;
        return new Rect(new Vector2(ribbon.Min.X + inset, ribbon.Min.Y),
            new Vector2(MathF.Max(ribbon.Min.X + inset, ribbon.Max.X - inset), ribbon.Max.Y));
    }

    public static WheelStatusLayout Compute(Rect row, float labelWidth, float clockWidth, float countWidth,
        float scale)
    {
        var height = MathF.Min(PillHeight * scale, row.Height);
        var centerY = row.Center.Y;
        var top = centerY - height * 0.5f;
        var bottom = top + height;
        var right = row.Max.X;
        var crowd = new Rect(new Vector2(right, top), new Vector2(right, bottom));
        var glyph = new Vector2(right, centerY);
        var count = crowd;
        if (countWidth > 0f)
        {
            var crowdPad = CrowdPadX * scale;
            var glyphBox = GlyphBox * scale;
            var crowdWidth = crowdPad * 2f + glyphBox + GlyphGap * scale + countWidth;
            crowd = new Rect(new Vector2(right - crowdWidth, top), new Vector2(right, bottom));
            glyph = new Vector2(crowd.Min.X + crowdPad + glyphBox * 0.5f, centerY);
            var countLeft = crowd.Min.X + crowdPad + glyphBox + GlyphGap * scale;
            count = new Rect(new Vector2(countLeft, top), new Vector2(countLeft + countWidth, bottom));
            right = crowd.Min.X - PillGap * scale;
        }

        var pad = PadX * scale;
        var dotRadius = DotRadius * scale;
        var clockSpan = clockWidth > 0f ? ClockGap * scale + clockWidth : 0f;
        var fixedWidth = pad * 2f + dotRadius * 2f + DotGap * scale + clockSpan;
        var labelRoom = MathF.Max(0f, right - row.Min.X - fixedWidth);
        var granted = MathF.Min(MathF.Max(0f, labelWidth), labelRoom);
        var phase = new Rect(new Vector2(row.Min.X, top), new Vector2(row.Min.X + fixedWidth + granted, bottom));
        var dot = new Vector2(phase.Min.X + pad + dotRadius, centerY);
        var labelLeft = dot.X + dotRadius + DotGap * scale;
        var label = new Rect(new Vector2(labelLeft, top), new Vector2(labelLeft + granted, bottom));
        var clockLeft = clockWidth > 0f ? label.Max.X + ClockGap * scale : label.Max.X;
        var clock = new Rect(new Vector2(clockLeft, top), new Vector2(clockLeft + MathF.Max(0f, clockWidth), bottom));
        return new WheelStatusLayout(row, phase, dot, label, clock, crowd, glyph, count);
    }
}
