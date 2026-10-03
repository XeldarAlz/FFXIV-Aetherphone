using Aetherphone.Apps.Timers;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Dailies;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Dailies;

internal sealed partial class DailiesApp
{
    private const float TileHeight = 112f;
    private const float TileGap = 12f;
    private const float TilePad = 14f;
    private const float TileRingDiameter = 40f;
    private const float TileRingThickness = 4f;
    private const float TileGlyph = 15f;
    private const float TileCheckGlyph = 26f;
    private const float TileGlassOpacity = 0.92f;
    private const float TileTrackAlpha = 0.18f;
    private const float TileSubAlpha = 0.82f;
    private const float TileHoverAlpha = 0.04f;

    private static readonly Vector4 TileInk = new(1f, 1f, 1f, 1f);

    private readonly Spring[] tileSelection = new Spring[2];
    private readonly Spring[] tileFill = new Spring[2];
    private readonly CachedText[] tileCounts = new CachedText[2];
    private readonly CachedText[] tileResets = new CachedText[2];
    private bool tilesPrimed;

    private void PrimeTiles() => tilesPrimed = false;

    private void DrawTiles(DateTime utcNow, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = TileHeight * scale;
        var gap = TileGap * scale;
        var tileWidth = MathF.Max(1f, (width - gap) * 0.5f);
        var dailyRect = new Rect(origin, origin + new Vector2(tileWidth, height));
        var weeklyMin = new Vector2(origin.X + tileWidth + gap, origin.Y);
        var weeklyRect = new Rect(weeklyMin, weeklyMin + new Vector2(tileWidth, height));
        UiAnchors.Report("dailies.hero", new Rect(origin, origin + new Vector2(width, height)));
        UiAnchors.Report(WeeklyTab, weeklyRect);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        if (!tilesPrimed)
        {
            tilesPrimed = true;
            for (var index = 0; index < tileSelection.Length; index++)
            {
                var tileCadence = (DailyCadence)index;
                tileSelection[index].SnapTo(tileCadence == cadence ? 1f : 0f);
                tileFill[index].SnapTo(FractionOf(tileCadence));
            }
        }

        if (ImGui.IsRectVisible(origin, origin + new Vector2(width, height)))
        {
            DrawTile(dailyRect, DailyCadence.Daily, FontAwesomeIcon.Sun, L.Dailies.Daily, utcNow, delta, scale);
            DrawTile(weeklyRect, DailyCadence.Weekly, FontAwesomeIcon.CalendarAlt, L.Dailies.Weekly, utcNow, delta,
                scale);
        }

        Advance(origin, width, height, TimersArt.SectionGap, scale);
    }

    private void DrawTile(Rect rect, DailyCadence tileCadence, FontAwesomeIcon icon, LocString title, DateTime utcNow,
        float delta, float scale)
    {
        var index = (int)tileCadence;
        var selected = Math.Clamp(tileSelection[index].Step(tileCadence == cadence ? 1f : 0f, Motion.Release, delta),
            0f, 1f);
        var fill = Math.Clamp(tileFill[index].Step(FractionOf(tileCadence), Motion.Sheet, delta), 0f, 1f);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(tileCadence == DailyCadence.Weekly ? "dailies.tile.weekly" : "dailies.tile.daily",
            pressed, PressFx.CardPressedScale);
        var center = rect.Center;
        var half = rect.Size * 0.5f * press;
        var min = center - half;
        var max = center + half;
        var drawList = ImGui.GetWindowDrawList();
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, min, max, radius, true);
        Material.AccentGlass(drawList, min, max, radius, scale, ui.Accent, TileGlassOpacity * selected);
        if (hovered && selected < 1f)
        {
            Squircle.Fill(drawList, min, max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, TileHoverAlpha * (1f - selected))));
        }

        var ink = Vector4.Lerp(ui.TitleInk, TileInk, selected);
        var subInk = Vector4.Lerp(ui.MutedInk, Palette.WithAlpha(TileInk, TileSubAlpha), selected);
        var ringInk = Vector4.Lerp(ui.Accent, TileInk, selected);
        var pad = TilePad * scale;
        var ringRadius = TileRingDiameter * 0.5f * scale;
        var ringCenter = new Vector2(min.X + pad + ringRadius, min.Y + pad + ringRadius);
        var thickness = TileRingThickness * scale;
        ProgressRing.Track(drawList, ringCenter, ringRadius - thickness * 0.5f, thickness,
            Palette.WithAlpha(ringInk, TileTrackAlpha));
        if (fill > 0f)
        {
            ProgressRing.Fill(drawList, ringCenter, ringRadius - thickness * 0.5f, thickness, fill, ringInk);
        }

        ProgressRing.CenterIcon(drawList, ringCenter, icon, ringInk, TileGlyph * scale);

        var trackedCount = tracker.Tracked(tileCadence);
        var remaining = tracker.Remaining(tileCadence);
        var right = max.X - pad;
        if (trackedCount > 0 && remaining <= 0)
        {
            ProgressRing.CenterIcon(drawList, new Vector2(right - TileCheckGlyph * 0.5f * scale, ringCenter.Y),
                FontAwesomeIcon.CheckCircle, ringInk, TileCheckGlyph * scale);
        }
        else if (trackedCount > 0)
        {
            var count = WidgetText.Integer(ref tileCounts[index], remaining);
            var size = Typography.Measure(count, TextStyles.WidgetDisplayCompact);
            Typography.Draw(drawList, new Vector2(right - size.X, ringCenter.Y - size.Y * 0.5f), count, ink,
                TextStyles.WidgetDisplayCompact);
        }

        var textWidth = MathF.Max(1f, right - (min.X + pad));
        var footHeight = Typography.LineHeight(TextStyles.Footnote);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var footTop = max.Y - pad - footHeight;
        var titleTop = footTop - titleHeight;
        Typography.Draw(drawList, new Vector2(min.X + pad, titleTop),
            Typography.FitText(Loc.T(title), textWidth, TextStyles.Headline), ink, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(min.X + pad, footTop),
            Typography.FitText(ResetLine(tileCadence, utcNow), textWidth, TextStyles.Footnote), subInk,
            TextStyles.Footnote);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered, false))
        {
            SelectCadence(tileCadence, true);
        }
    }

    private float FractionOf(DailyCadence tileCadence)
    {
        var trackedCount = tracker.Tracked(tileCadence);
        return trackedCount == 0 ? 0f : tracker.Done(tileCadence) / (float)trackedCount;
    }

    private string ResetLine(DailyCadence tileCadence, DateTime utcNow)
    {
        var remaining = DailyLedger.NextReset(tileCadence, utcNow) - utcNow;
        var key = (long)remaining.TotalMinutes;
        ref var cache = ref tileResets[(int)tileCadence];
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        return cache.Store(key, Loc.T(L.Dailies.Resets, TimeText.Until(remaining)));
    }
}
