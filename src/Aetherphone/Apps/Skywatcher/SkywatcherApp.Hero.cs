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
    private const float FadeRangeUnits = 30f;
    private static readonly TextStyle ZoneStyle = new(1.65f, FontWeight.Medium);
    private static readonly TextStyle CompactZoneStyle = new(1.20f, FontWeight.SemiBold);
    private static readonly TextStyle ClockStyle = new(2.65f, FontWeight.Regular);
    private static readonly TextStyle ConditionStyle = new(1.20f, FontWeight.Medium);
    private CachedText heroClock;
    private CachedText compactLine;

    private float HeroVisibleHeight(float scale) =>
        MathF.Max(HeroCollapsedUnits * scale, HeroExpandedUnits * scale - scrollY);

    private void DrawHero(Rect body, in SkyPalette palette, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var top = body.Min.Y;
        var centerX = body.Center.X;
        var maxWidth = body.Width - 2f * SidePaddingUnits * scale;
        var travel = (HeroExpandedUnits - HeroCollapsedUnits) * scale;
        var collapse = Math.Clamp(scrollY / travel, 0f, 1f);
        var visible = HeroVisibleHeight(scale);
        if (UiAnchors.Recording)
        {
            UiAnchors.Report("skywatcher.current", new Rect(body.Min, new Vector2(body.Max.X, top + visible)));
        }

        var fadeLine = top + HeroCollapsedUnits * scale;
        var cursor = top + 6f * scale - scrollY;
        if (ViewingCurrentZone)
        {
            var caption = Loc.Upper(Loc.T(L.Skywatcher.CurrentZone));
            var captionHeight = Typography.LineHeight(TextStyles.Caption1);
            var captionAlpha = FadeOut(cursor + scrollY, fadeLine, scale);
            if (captionAlpha > 0.01f)
            {
                DrawCaption(drawList, new Vector2(centerX, cursor + captionHeight * 0.5f), caption,
                    palette.InkSoft with { W = palette.InkSoft.W * captionAlpha }, palette, scale);
            }

            cursor += captionHeight;
        }

        var zoneScale =
            Plugin.Fonts.NearestScale(ZoneStyle.Scale + (CompactZoneStyle.Scale - ZoneStyle.Scale) * collapse);
        var zoneWeight = collapse > 0.5f ? CompactZoneStyle.Weight : ZoneStyle.Weight;
        var fittedScale = Typography.FitScale(zone, maxWidth, zoneScale, zoneScale * 0.62f, zoneWeight);
        var zoneText = Typography.FitText(zone, maxWidth, fittedScale, zoneWeight);
        var zoneHeight = Typography.LineHeight(new TextStyle(fittedScale, zoneWeight));
        var zoneCenterY = MathF.Max(top + 6f * scale + zoneHeight * 0.5f, cursor + zoneHeight * 0.5f);
        ShadowCentered(drawList, new Vector2(centerX, zoneCenterY), zoneText, palette.Ink,
            new TextStyle(fittedScale, zoneWeight), palette, scale);
        cursor += zoneHeight;

        var clockHeight = Typography.LineHeight(ClockStyle);
        var clockAlpha = FadeOut(cursor + scrollY, fadeLine, scale);
        if (clockAlpha > 0.01f)
        {
            DrawClock(drawList, new Vector2(centerX, cursor + clockHeight * 0.5f), palette, clockAlpha, scale);
        }

        cursor += clockHeight;
        var conditionHeight = Typography.LineHeight(ConditionStyle);
        var conditionAlpha = FadeOut(cursor + scrollY, fadeLine, scale);
        if (conditionAlpha > 0.01f)
        {
            ShadowCentered(drawList, new Vector2(centerX, cursor + conditionHeight * 0.5f),
                Typography.FitText(forecast[0].Weather.Name, maxWidth, ConditionStyle), palette.Ink with
                {
                    W = palette.Ink.W * conditionAlpha,
                }, ConditionStyle, palette, scale);
        }

        cursor += conditionHeight + 2f * scale;
        var summaryAlpha = FadeOut(cursor + scrollY, fadeLine, scale);
        if (summaryAlpha > 0.01f)
        {
            var summaryCenterY = cursor + Typography.LineHeight(TextStyles.Subheadline) * 0.5f;
            ShadowCentered(drawList, new Vector2(centerX, summaryCenterY),
                Typography.FitText(summaryText, maxWidth, TextStyles.Subheadline),
                palette.InkSoft with { W = palette.InkSoft.W * summaryAlpha }, TextStyles.Subheadline, palette, scale);
        }

        var compactAlpha = Math.Clamp((collapse - 0.72f) / 0.28f, 0f, 1f);
        if (compactAlpha > 0.01f)
        {
            var line = CompactLine();
            ShadowCentered(drawList, new Vector2(centerX, zoneCenterY + zoneHeight * 0.5f + 10f * scale),
                Typography.FitText(line, maxWidth, TextStyles.Subheadline),
                palette.InkSoft with { W = palette.InkSoft.W * compactAlpha }, TextStyles.Subheadline, palette, scale);
        }
    }

    private float FadeOut(float restY, float line, float scale)
    {
        var travelBeforeFade = MathF.Max(0f, restY - line);
        return Math.Clamp(1f - MathF.Max(0f, scrollY - travelBeforeFade) / (FadeRangeUnits * scale), 0f, 1f);
    }

    private static void DrawCaption(ImDrawListPtr drawList, Vector2 center, string caption, Vector4 color,
        in SkyPalette palette, float scale)
    {
        var style = TextStyles.Caption1 with { Weight = FontWeight.SemiBold };
        var textWidth = Typography.Measure(caption, style).X;
        var iconSize = 9f * scale;
        var gap = 5f * scale;
        var startX = center.X - (textWidth + iconSize + gap) * 0.5f;
        ProgressRing.CenterIcon(drawList, new Vector2(startX + iconSize * 0.5f, center.Y),
            FontAwesomeIcon.LocationArrow, color, iconSize);
        ShadowCentered(drawList, new Vector2(startX + iconSize + gap + textWidth * 0.5f, center.Y), caption, color,
            style, palette, scale);
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
        ShadowCentered(drawList, new Vector2(left + clockSize.X * 0.5f, center.Y), clock,
            palette.Ink with { W = palette.Ink.W * alpha }, ClockStyle, palette, scale);
        var suffixCenter = new Vector2(left + clockSize.X + gap + suffixSize.X * 0.5f,
            center.Y + clockSize.Y * 0.5f - suffixSize.Y * 0.5f - clockSize.Y * 0.16f);
        ShadowCentered(drawList, suffixCenter, suffix, palette.InkSoft with { W = palette.InkSoft.W * alpha },
            suffixStyle, palette, scale);
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
