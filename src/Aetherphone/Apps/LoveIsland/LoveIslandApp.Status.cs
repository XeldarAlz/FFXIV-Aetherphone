using Aetherphone.Core;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal sealed partial class LoveIslandApp
{
    private const float TileHeight = 66f;
    private const float TileGap = 8f;
    private const float TileRadius = 14f;
    private const float TileRowGap = 12f;
    private const float RollWindow = 0.30f;
    private const float ProgressGap = 12f;
    private const float ProgressHeight = 5f;
    private const float ProgressHeadRadius = 4f;
    private const long SecondsPerMinute = 60L;
    private const long SecondsPerHour = 3600L;
    private const long SecondsPerDay = 86400L;
    private const string CountdownCardId = "loveisland.tonight.countdown";

    private static readonly string[] TwoDigits = BuildTwoDigits();
    private static readonly TextStyle DigitStyle = TextStyles.Title1;
    private static readonly TextStyle UnitStyle = TextStyles.Caption2;

    private readonly CountdownUnit[] countdownUnits = new CountdownUnit[4];
    private CachedText dayDigits;

    private readonly record struct CountdownUnit(long Value, long Previous, bool Rolling, string Label);

    private float DrawCountdownCard(ImDrawListPtr drawList, float left, float top, float width, long remaining,
        string when, out bool tapped)
    {
        tapped = false;
        var scale = UiScale.Current;
        var firstVertex = CardEntrance.Mark(drawList);
        var pad = LoveIslandKit.CardPad * scale;
        var inner = MathF.Max(1f, width - pad * 2f);
        var eyebrowHeight = Typography.LineHeight(LoveIslandKit.EyebrowStyle);
        var whenHeight = when.Length > 0
            ? Metrics.Space.Sm * scale + Typography.MeasureWrappedBlock(when, LoveIslandKit.CardBodyStyle, inner).Y
            : 0f;
        var height = pad * 2f + eyebrowHeight + TileRowGap * scale + TileHeight * scale + whenHeight;
        var rest = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        if (!ImGui.IsRectVisible(rest.Min, rest.Max))
        {
            entrance.Settle(drawList, firstVertex, scale);
            return rest.Max.Y;
        }

        tapped = LoveIslandKit.PressableCard(drawList, ui, rest, CountdownCardId, scale, true, out _, true);
        var cursorY = top + pad;
        LoveIslandKit.Eyebrow(drawList, new Vector2(left + pad, cursorY), Texts.T(CampaignTextKeys.DoorsOpenIn),
            EclipseFx.Rose(ui.Accent, 0.35f), inner);
        cursorY += eyebrowHeight + TileRowGap * scale;
        var count = FillCountdownUnits(remaining);
        var gap = TileGap * scale;
        var tileWidth = (inner - gap * (count - 1)) / count;
        var subSecond = (float)(nowPrecise - Math.Floor(nowPrecise));
        for (var index = 0; index < count; index++)
        {
            var tileLeft = left + pad + index * (tileWidth + gap);
            var tile = new Rect(new Vector2(tileLeft, cursorY),
                new Vector2(tileLeft + tileWidth, cursorY + TileHeight * scale));
            DrawCountdownTile(drawList, tile, countdownUnits[index], subSecond, scale);
        }

        cursorY += TileHeight * scale;
        if (when.Length > 0)
        {
            cursorY += Metrics.Space.Sm * scale;
            Typography.DrawWrappedLeft(new Vector2(left + pad, cursorY), when, ui.MutedInk,
                LoveIslandKit.CardBodyStyle, inner);
        }

        entrance.Settle(drawList, firstVertex, scale);
        return rest.Max.Y;
    }

    private int FillCountdownUnits(long remainingSeconds)
    {
        var remaining = Math.Max(0L, remainingSeconds);
        var days = remaining / SecondsPerDay;
        var hours = remaining % SecondsPerDay / SecondsPerHour;
        var minutes = remaining % SecondsPerHour / SecondsPerMinute;
        var seconds = remaining % SecondsPerMinute;
        var next = remaining + 1;
        var previousDays = next / SecondsPerDay;
        var previousHours = next % SecondsPerDay / SecondsPerHour;
        var previousMinutes = next % SecondsPerHour / SecondsPerMinute;
        var previousSeconds = next % SecondsPerMinute;
        var count = 0;
        if (days > 0)
        {
            countdownUnits[count++] = new CountdownUnit(days, previousDays, previousDays != days,
                Loc.T(L.LoveIsland.UnitDays));
        }

        countdownUnits[count++] = new CountdownUnit(hours, previousHours, previousHours != hours,
            Loc.T(L.LoveIsland.UnitHours));
        countdownUnits[count++] = new CountdownUnit(minutes, previousMinutes, previousMinutes != minutes,
            Loc.T(L.LoveIsland.UnitMinutes));
        countdownUnits[count++] = new CountdownUnit(seconds, previousSeconds, previousSeconds != seconds,
            Loc.T(L.LoveIsland.UnitSeconds));
        return count;
    }

    private void DrawCountdownTile(ImDrawListPtr drawList, Rect tile, in CountdownUnit unit, float subSecond,
        float scale)
    {
        var radius = TileRadius * scale;
        Squircle.FillVerticalGradient(drawList, tile.Min, tile.Max, radius,
            ImGui.GetColorU32(LoveIslandKit.Night with { W = 0.55f }),
            ImGui.GetColorU32(Palette.Mix(ui.Accent, LoveIslandKit.Night, 0.82f) with { W = 0.75f }));
        Squircle.Stroke(drawList, tile.Min, tile.Max, radius,
            ImGui.GetColorU32(EclipseFx.Rose(ui.Accent, 0.4f) with { W = 0.22f }), 1f);
        var unitHeight = Typography.LineHeight(UnitStyle);
        var digitHeight = Typography.LineHeight(DigitStyle);
        var block = digitHeight + unitHeight;
        var digitTop = tile.Center.Y - block * 0.5f;
        var unitLabel = Typography.FitText(unit.Label, tile.Width - Metrics.Space.Sm * scale, UnitStyle);
        var unitWidth = Typography.Measure(unitLabel, UnitStyle).X;
        Typography.Draw(drawList, new Vector2(tile.Center.X - unitWidth * 0.5f, digitTop + digitHeight), unitLabel,
            ui.MutedInk, UnitStyle);
        var current = Digits(unit.Value);
        if (!unit.Rolling || subSecond >= RollWindow)
        {
            DrawDigits(drawList, tile.Center.X, digitTop, current, 1f);
            return;
        }

        var progress = subSecond / RollWindow;
        var eased = 1f - (1f - progress) * (1f - progress) * (1f - progress);
        var travel = digitHeight * 0.7f;
        drawList.PushClipRect(new Vector2(tile.Min.X, digitTop), new Vector2(tile.Max.X, digitTop + digitHeight), true);
        DrawDigits(drawList, tile.Center.X, digitTop - travel * eased, Digits(unit.Previous), 1f - eased);
        DrawDigits(drawList, tile.Center.X, digitTop + travel * (1f - eased), current, eased);
        drawList.PopClipRect();
    }

    private void DrawDigits(ImDrawListPtr drawList, float centerX, float top, string digits, float alpha)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var size = Typography.Measure(digits, DigitStyle);
        Typography.Draw(drawList, new Vector2(centerX - size.X * 0.5f, top), digits, ui.TitleInk with { W = alpha },
            DigitStyle);
    }

    private string Digits(long value)
    {
        if (value < TwoDigits.Length)
        {
            return TwoDigits[value];
        }

        var count = (int)Math.Min(value, int.MaxValue);
        return dayDigits.IsCurrent(count) ? dayDigits.Value : dayDigits.Store(count, NumberText.Group(count));
    }

    private void DrawProgressBar(ImDrawListPtr drawList, float left, float top, float width, float progress,
        float scale)
    {
        var height = ProgressHeight * scale;
        var radius = height * 0.5f;
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + height);
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(LoveIslandKit.White with { W = 0.10f }));
        var clamped = Math.Clamp(progress, 0f, 1f);
        var fillRight = left + MathF.Max(height, width * clamped);
        Squircle.FillHorizontalGradient(drawList, min, new Vector2(fillRight, max.Y), radius,
            ImGui.GetColorU32(Palette.Darken(ui.Accent, 0.1f)), ImGui.GetColorU32(EclipseFx.Rose(ui.Accent, 0.45f)));
        var head = new Vector2(fillRight - radius, top + radius);
        SoftGlow.Draw(drawList, head, ProgressHeadRadius * 4f * scale,
            EclipseFx.Rose(ui.Accent, 0.3f) with { W = 0.8f });
        drawList.AddCircleFilled(head, ProgressHeadRadius * scale, ImGui.GetColorU32(LoveIslandKit.White), 16);
    }

    private float LiveProgress(int experienceIndex)
    {
        var experience = frameCampaign!.Experiences[experienceIndex];
        var span = experience.EndsAtUnix - experience.StartsAtUnix;
        return span <= 0 ? 1f : (float)((nowPrecise - experience.StartsAtUnix) / span);
    }

    private static string[] BuildTwoDigits()
    {
        var digits = new string[100];
        for (var index = 0; index < digits.Length; index++)
        {
            digits[index] = index.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }

        return digits;
    }
}
