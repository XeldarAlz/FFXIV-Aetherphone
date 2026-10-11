using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Stage;

internal readonly struct PhaseRibbonLayout
{
    public const float SideInset = 12f;
    public const float VerticalInset = 4f;
    public const float MaximumHeight = 40f;
    public const float RingRadius = 12f;
    public const float RingTextPad = 3f;
    public const float CrowdGlyph = 13f;
    public const float Gap = 8f;
    public const float CrowdGap = 4f;

    public readonly Rect Capsule;
    public readonly Vector2 RingCenter;
    public readonly float RingRadiusPixels;
    public readonly Rect Label;
    public readonly Rect Crowd;
    public readonly bool HasRing;
    public readonly bool HasCrowd;

    private PhaseRibbonLayout(Rect capsule, Vector2 ringCenter, float ringRadius, Rect label, Rect crowd, bool hasRing,
        bool hasCrowd)
    {
        Capsule = capsule;
        RingCenter = ringCenter;
        RingRadiusPixels = ringRadius;
        Label = label;
        Crowd = crowd;
        HasRing = hasRing;
        HasCrowd = hasCrowd;
    }

    public static PhaseRibbonLayout Compute(Rect rect, float countdownTextWidth, bool ring, float crowdTextWidth,
        bool crowd, float scale)
    {
        var height = MathF.Min(rect.Height, MaximumHeight * scale);
        var top = rect.Min.Y + MathF.Min(VerticalInset * scale, height * 0.5f);
        var bottom = rect.Min.Y + height - MathF.Min(VerticalInset * scale, height * 0.5f);
        var capsule = new Rect(new Vector2(rect.Min.X + SideInset * scale, top),
            new Vector2(MathF.Max(rect.Min.X + SideInset * scale, rect.Max.X - SideInset * scale), bottom));
        var half = capsule.Height * 0.5f;
        var centerY = capsule.Center.Y;
        var gap = Gap * scale;
        var left = capsule.Min.X + half * 0.6f;
        var right = capsule.Max.X - half * 0.6f;
        var ringRadius = 0f;
        var ringCenter = new Vector2(left, centerY);
        if (ring)
        {
            ringRadius = Math.Clamp(countdownTextWidth * 0.5f + RingTextPad * scale, RingRadius * scale,
                MathF.Max(RingRadius * scale, half - scale));
            ringCenter = new Vector2(capsule.Min.X + half, centerY);
            left = ringCenter.X + ringRadius + gap;
        }

        var crowdRect = new Rect(new Vector2(right, centerY), new Vector2(right, centerY));
        if (crowd)
        {
            var crowdWidth = CrowdGlyph * scale + CrowdGap * scale + crowdTextWidth;
            crowdRect = new Rect(new Vector2(right - crowdWidth, capsule.Min.Y), new Vector2(right, capsule.Max.Y));
            right = crowdRect.Min.X - gap;
        }

        var label = new Rect(new Vector2(left, capsule.Min.Y), new Vector2(MathF.Max(left, right), capsule.Max.Y));
        return new PhaseRibbonLayout(capsule, ringCenter, ringRadius, label, crowdRect, ring, crowd);
    }
}

internal static class PhaseRibbon
{
    private const int SecondsOnlyBelow = 60;
    private static readonly MarqueeId LabelMarquee = new("casino.phaseRibbon", string.Empty);

    public static string CountdownText(long remainingMs)
    {
        var seconds = (int)((Math.Max(0, remainingMs) + 999) / 1000);
        return seconds < SecondsOnlyBelow ? GameNumber.Label(seconds) : TimeText.Duration(seconds);
    }

    public static void Draw(ImDrawListPtr drawList, Rect rect, string label, long remainingMs, int windowSeconds,
        int crowd, Vector4 accent, float scale)
    {
        if (rect.Height <= 0f)
        {
            return;
        }

        var style = TextStyles.FootnoteEmphasized;
        var ring = windowSeconds > 0;
        var countdown = ring ? CountdownText(remainingMs) : string.Empty;
        var count = crowd > 0 ? GameNumber.Label(crowd) : string.Empty;
        var countdownWidth = ring ? Typography.Measure(countdown, style).X : 0f;
        var countWidth = crowd > 0 ? Typography.Measure(count, style).X : 0f;
        var layout = PhaseRibbonLayout.Compute(rect, countdownWidth, ring, countWidth, crowd > 0, scale);
        var capsule = layout.Capsule;
        Material.LiquidGlass(drawList, capsule.Min, capsule.Max, capsule.Height * 0.5f, scale, GlassTone.Dark, 0f);
        var lineHeight = Typography.LineHeight(style);
        var centerY = capsule.Center.Y;
        if (layout.HasRing)
        {
            TurnTimerRing.Draw(drawList, layout.RingCenter, layout.RingRadiusPixels, remainingMs, windowSeconds, accent,
                scale);
            Typography.DrawCentered(drawList, layout.RingCenter, countdown, CasinoColors.InkTitle, style);
        }

        if (layout.HasCrowd)
        {
            var glyph = PhaseRibbonLayout.CrowdGlyph * scale;
            ProgressRing.CenterIcon(drawList, new Vector2(layout.Crowd.Min.X + glyph * 0.5f, centerY),
                FontAwesomeIcon.Users, CasinoColors.InkBody, glyph);
            Typography.Draw(drawList, new Vector2(layout.Crowd.Max.X - countWidth, centerY - lineHeight * 0.5f), count,
                CasinoColors.InkTitle, style);
        }

        if (label.Length == 0 || layout.Label.Width <= 0f)
        {
            return;
        }

        Marquee.DrawLeftAuto(drawList, LabelMarquee, label, layout.Label.Min.X, centerY - lineHeight * 0.5f,
            layout.Label.Width, style, CasinoColors.InkTitle);
    }
}
