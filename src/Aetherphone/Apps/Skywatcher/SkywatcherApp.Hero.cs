using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Skywatcher;

internal sealed partial class SkywatcherApp
{
    private const float HeroExpandedUnits = 232f;
    private const float HeroCollapsedUnits = 70f;
    private const float HeroTopInsetUnits = 6f;
    private const float CompactLineGapUnits = 10f;
    private const float CardFadeUnits = 18f;
    private const float CardFadeRampUnits = 8f;
    private const float CaptionFadeEnd = 0.2f;
    private const float BlockFadeEnd = 0.42f;
    private const float BlockParallax = 0.6f;
    private const float CompactFadeStart = 0.5f;
    private const float CompactFadeEnd = 0.8f;
    private const float ZoneMinimumFit = 0.62f;
    private const float CompactZoneScale = 1.20f;
    private static readonly TextStyle ZoneStyle = new(1.65f, FontWeight.Medium);
    private static readonly TextStyle ClockStyle = new(2.65f, FontWeight.Regular);
    private static readonly TextStyle ConditionStyle = new(1.20f, FontWeight.Medium);
    private CachedText heroClock;
    private CachedText compactLine;
    private CardFade cardFade;
    private int cardFadeFrom;

    private float HeroVisibleHeight(float scale) =>
        MathF.Max(HeroCollapsedUnits * scale, HeroExpandedUnits * scale - scrollY);

    private float HeroCollapse(float scale) =>
        Math.Clamp(scrollY / ((HeroExpandedUnits - HeroCollapsedUnits) * scale), 0f, 1f);

    private void BeginCardFade(float edge, float scale)
    {
        cardFade = CardFade.Under(edge, ImGui.GetCursorScreenPos().Y, CardFadeUnits * scale,
            CardFadeRampUnits * scale);
        cardFadeFrom = ImGui.GetWindowDrawList().VtxBuffer.Size;
    }

    private void DrawCardPanel(ImDrawListPtr drawList, Rect card, in SkyPalette palette, float scale)
    {
        FlushCardFade(drawList);
        cardFade.Panel(drawList, card, palette, sky.Density, scale);
        cardFadeFrom = drawList.VtxBuffer.Size;
    }

    private void FlushCardFade(ImDrawListPtr drawList)
    {
        cardFade.Apply(drawList, cardFadeFrom);
        cardFadeFrom = drawList.VtxBuffer.Size;
    }

    private void EndCardFade(ImDrawListPtr drawList)
    {
        FlushCardFade(drawList);
        cardFade = default;
    }

    private void DrawHero(Rect body, in SkyPalette palette, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var top = body.Min.Y;
        var centerX = body.Center.X;
        var maxWidth = body.Width - 2f * SidePaddingUnits * scale;
        var collapse = HeroCollapse(scale);
        var visible = HeroVisibleHeight(scale);
        if (UiAnchors.Recording)
        {
            UiAnchors.Report("skywatcher.current", new Rect(body.Min, new Vector2(body.Max.X, top + visible)));
        }

        var inset = HeroTopInsetUnits * scale;
        var cursor = top + inset - scrollY;
        if (ViewingCurrentZone)
        {
            var captionHeight = Typography.LineHeight(TextStyles.Caption1);
            var captionAlpha = 1f - Math.Clamp(collapse / CaptionFadeEnd, 0f, 1f);
            if (captionAlpha > 0.01f)
            {
                DrawCaption(drawList, new Vector2(centerX, cursor + captionHeight * 0.5f), captionAlpha, palette,
                    scale);
            }

            cursor += captionHeight;
        }

        var zoneScale = ZoneStyle.Scale + (CompactZoneScale - ZoneStyle.Scale) * collapse;
        var fittedScale = FitZoneScale(zone, maxWidth, zoneScale);
        var zoneText = fittedScale > zoneScale * ZoneMinimumFit
            ? zone
            : Typography.FitText(zone, maxWidth * Plugin.Fonts.NearestScale(fittedScale) / fittedScale, fittedScale,
                ZoneStyle.Weight);
        var zoneHeight = ExactLineHeight(fittedScale, ZoneStyle.Weight);
        var zoneCenterY = MathF.Max(top + inset + zoneHeight * 0.5f, cursor + zoneHeight * 0.5f);
        cursor += zoneHeight;

        var blockAlpha = 1f - Math.Clamp(collapse / BlockFadeEnd, 0f, 1f);
        if (blockAlpha > 0.01f)
        {
            DrawExpandedLines(drawList, centerX, cursor + scrollY * (1f - BlockParallax), maxWidth, blockAlpha,
                palette, scale);
        }

        HeroTitle(drawList, new Vector2(centerX, zoneCenterY), zoneText, palette.Ink, fittedScale, palette, scale);

        var compactAlpha = Math.Clamp((collapse - CompactFadeStart) / (CompactFadeEnd - CompactFadeStart), 0f, 1f);
        if (compactAlpha > 0.01f)
        {
            var compactCenterY = zoneCenterY + zoneHeight * 0.5f + CompactLineGapUnits * scale;
            HeroText(drawList, new Vector2(centerX, compactCenterY),
                Typography.FitText(CompactLine(), maxWidth, TextStyles.Subheadline), palette.InkSoft,
                TextStyles.Subheadline, compactAlpha, palette, scale);
        }
    }

    private void DrawExpandedLines(ImDrawListPtr drawList, float centerX, float top, float maxWidth, float alpha,
        in SkyPalette palette, float scale)
    {
        var clockHeight = Typography.LineHeight(ClockStyle);
        DrawClock(drawList, new Vector2(centerX, top + clockHeight * 0.5f), palette, alpha, scale);
        top += clockHeight;
        var conditionHeight = Typography.LineHeight(ConditionStyle);
        HeroText(drawList, new Vector2(centerX, top + conditionHeight * 0.5f),
            Typography.FitText(forecast[0].Weather.Name, maxWidth, ConditionStyle), palette.Ink, ConditionStyle,
            alpha, palette, scale);
        top += conditionHeight + 2f * scale;
        var summaryHeight = Typography.LineHeight(TextStyles.Subheadline);
        HeroText(drawList, new Vector2(centerX, top + summaryHeight * 0.5f),
            Typography.FitText(summaryText, maxWidth, TextStyles.Subheadline), palette.InkSoft,
            TextStyles.Subheadline, alpha, palette, scale);
    }

    private static float FitZoneScale(string text, float maxWidth, float textScale)
    {
        var width = Typography.MeasureExact(text, textScale, ZoneStyle.Weight).X;
        if (width <= maxWidth)
        {
            return textScale;
        }

        return MathF.Max(textScale * ZoneMinimumFit, textScale * maxWidth / width);
    }

    private static float ExactLineHeight(float textScale, FontWeight weight) =>
        Typography.LineHeight(new TextStyle(textScale, weight)) * textScale / Plugin.Fonts.NearestScale(textScale);

    private static Vector4 ShadowInk(in SkyPalette palette, float alpha) =>
        new(0f, 0f, 0f, (palette.LightSky ? 0.18f : 0.36f) * alpha);

    private static void HeroTitle(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        float textScale, in SkyPalette palette, float scale)
    {
        Typography.DrawCenteredExact(drawList, center + new Vector2(0f, 1.2f * scale), text, ShadowInk(palette, 1f),
            textScale, ZoneStyle.Weight);
        Typography.DrawCenteredExact(drawList, center, text, color, textScale, ZoneStyle.Weight);
    }

    private static void HeroText(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        in TextStyle style, float alpha, in SkyPalette palette, float scale)
    {
        Typography.DrawCentered(drawList, center + new Vector2(0f, 1.2f * scale), text, ShadowInk(palette, alpha),
            style.Scale, style.Weight);
        Typography.DrawCentered(drawList, center, text, color with { W = color.W * alpha }, style.Scale,
            style.Weight);
    }

    private static void DrawCaption(ImDrawListPtr drawList, Vector2 center, float alpha, in SkyPalette palette,
        float scale)
    {
        var caption = Loc.Upper(Loc.T(L.Skywatcher.CurrentZone));
        var style = TextStyles.Caption1 with { Weight = FontWeight.SemiBold };
        var color = palette.InkSoft with { W = palette.InkSoft.W * alpha };
        var textWidth = Typography.Measure(caption, style).X;
        var iconSize = 9f * scale;
        var gap = 5f * scale;
        var startX = center.X - (textWidth + iconSize + gap) * 0.5f;
        ProgressRing.CenterIcon(drawList, new Vector2(startX + iconSize * 0.5f, center.Y),
            FontAwesomeIcon.LocationArrow, color, iconSize);
        HeroText(drawList, new Vector2(startX + iconSize + gap + textWidth * 0.5f, center.Y), caption,
            palette.InkSoft, style, alpha, palette, scale);
    }

    private void DrawClock(ImDrawListPtr drawList, Vector2 center, in SkyPalette palette, float alpha, float scale)
    {
        var now = EorzeaTime.Now();
        var minuteKey = now.Hour * 60L + now.Minute;
        var clock = heroClock.IsCurrent(minuteKey)
            ? heroClock.Value
            : heroClock.Store(minuteKey, TimeText.Clock(new DateTime(1, 1, 1, now.Hour, now.Minute, 0)));
        var suffix = Loc.T(L.WidgetsLife.EorzeaShort);
        var suffixStyle = TextStyles.SubheadlineEmphasized;
        var clockSize = Typography.Measure(clock, ClockStyle);
        var suffixSize = Typography.Measure(suffix, suffixStyle);
        var gap = 4f * scale;
        var left = center.X - (clockSize.X + gap + suffixSize.X) * 0.5f;
        HeroText(drawList, new Vector2(left + clockSize.X * 0.5f, center.Y), clock, palette.Ink, ClockStyle, alpha,
            palette, scale);
        var suffixCenter = new Vector2(left + clockSize.X + gap + suffixSize.X * 0.5f,
            center.Y + clockSize.Y * 0.5f - suffixSize.Y * 0.5f - clockSize.Y * 0.16f);
        HeroText(drawList, suffixCenter, suffix, palette.InkSoft, suffixStyle, alpha, palette, scale);
    }

    private string CompactLine()
    {
        var now = EorzeaTime.Now();
        var key = (now.Hour * 60L + now.Minute) * 1024L + forecast[0].Weather.Id;
        if (compactLine.IsCurrent(key))
        {
            return compactLine.Value;
        }

        var clock = TimeText.Clock(new DateTime(1, 1, 1, now.Hour, now.Minute, 0));
        return compactLine.Store(key, $"{clock} {Loc.T(L.WidgetsLife.EorzeaShort)}  |  {forecast[0].Weather.Name}");
    }
}
