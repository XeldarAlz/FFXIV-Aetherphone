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
    private const int WeatherColumns = 3;
    private const float LegendUnits = 30f;
    private const float ExtraBadgeUnits = 15f;
    private static readonly Vector4 ExtraBadgeFill = new(0f, 0f, 0f, 0.45f);
    private static readonly Vector4 ExtraBadgeInk = new(1f, 0.86f, 0.44f, 1f);
    private CachedText controlClock;
    private static readonly Vector4 SunKnob = new(1.00f, 0.80f, 0.36f, 1f);
    private static readonly Vector4 MoonKnob = new(0.62f, 0.72f, 1.00f, 1f);

    private static readonly (float Bell, Vector4 Color)[] DayStops =
    {
        (0f, new Vector4(0.10f, 0.13f, 0.30f, 1f)),
        (5.25f, new Vector4(0.16f, 0.20f, 0.42f, 1f)),
        (6f, new Vector4(1.00f, 0.58f, 0.36f, 1f)),
        (7f, new Vector4(0.40f, 0.68f, 0.98f, 1f)),
        (18f, new Vector4(0.36f, 0.62f, 0.96f, 1f)),
        (19f, new Vector4(1.00f, 0.52f, 0.34f, 1f)),
        (19.75f, new Vector4(0.20f, 0.20f, 0.44f, 1f)),
        (24f, new Vector4(0.10f, 0.13f, 0.30f, 1f)),
    };

    private static readonly (int Minutes, LocString Label)[] TimePresets =
    {
        (6 * 60, L.Skywatcher.Dawn),
        (12 * 60, L.Skywatcher.Noon),
        (18 * 60, L.Skywatcher.Dusk),
        (0, L.Skywatcher.Midnight),
    };

    private void DrawControl(in SkyPalette palette, float scale)
    {
        var zoneWeathers = weather.ZoneWeathers();
        if (zoneWeathers.Count == 0)
        {
            DrawControlEmpty(palette, scale);
            return;
        }

        var ink = control.CanControl ? palette : palette with { Ink = palette.Ink with { W = palette.Ink.W * 0.4f } };
        var width = ImGui.GetContentRegionAvail().X;
        SectionLabel(Loc.T(L.Skywatcher.Time), ink, scale);
        DrawTimeCard(width, ink, scale);
        SectionLabel(Loc.T(L.Skywatcher.Weather), ink, scale);
        DrawWeatherGrid(width, ink, zoneWeathers, weather.ExtraWeathers(), scale);
        DrawControlFooter(width, palette, scale);
        ImGui.Dummy(new Vector2(0f, 8f * scale));
    }

    private void DrawTimeCard(float width, in SkyPalette palette, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = 132f * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
        UiAnchors.Report("skywatcher.control.time", card);
        WeatherCard.Panel(ImGui.GetWindowDrawList(), card, palette, sky.Density, scale);
        var inner = card.Inset(14f * scale);
        var custom = control.HasTimeOverride;
        var minutes = custom ? control.TimeOverrideMinutes : EorzeaTime.CurrentMinuteOfDay();
        var clock = controlClock.IsCurrent(minutes)
            ? controlClock.Value
            : controlClock.Store(minutes, TimeText.Clock(new DateTime(1, 1, 1, minutes / 60, minutes % 60, 0)));
        Typography.Draw(inner.Min, clock, palette.Ink, TextStyles.Title1);
        if (!custom)
        {
            var state = Loc.T(L.Skywatcher.Natural);
            var stateSize = Typography.Measure(state, TextStyles.Footnote);
            Typography.Draw(new Vector2(inner.Max.X - stateSize.X, inner.Min.Y + 6f * scale), state, palette.InkFaint,
                TextStyles.Footnote);
        }

        var track = new Rect(new Vector2(inner.Min.X, inner.Min.Y + 50f * scale),
            new Vector2(inner.Max.X, inner.Min.Y + 74f * scale));
        DrawScrubTrack(track, minutes, palette, scale);
        var presetTop = inner.Max.Y - 30f * scale;
        var presetWidth = inner.Width / TimePresets.Length;
        for (var index = 0; index < TimePresets.Length; index++)
        {
            var preset = TimePresets[index];
            var cell = new Rect(new Vector2(inner.Min.X + presetWidth * index + 3f * scale, presetTop),
                new Vector2(inner.Min.X + presetWidth * (index + 1) - 3f * scale, presetTop + 28f * scale));
            if (DrawPill(cell, Loc.T(preset.Label), custom && minutes == preset.Minutes, palette, scale))
            {
                control.SetTime(preset.Minutes);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawScrubTrack(Rect track, int minutes, in SkyPalette palette, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var barHeight = 8f * scale;
        var barMin = new Vector2(track.Min.X, track.Center.Y - barHeight * 0.5f);
        var barMax = new Vector2(track.Max.X, track.Center.Y + barHeight * 0.5f);
        DrawDayGradient(drawList, barMin, barMax);
        var fraction = minutes / (float)(EorzeaTime.MinutesPerDay - 1);
        var knobX = track.Min.X + track.Width * fraction;
        var knobCenter = new Vector2(knobX, track.Center.Y);
        var daylight = WeatherSky.Daylight(minutes / 60f);
        var core = Vector4.Lerp(MoonKnob, SunKnob, daylight);
        WeatherAmbience.Glow(drawList, knobCenter, 20f * scale, core, 0.55f);
        drawList.AddCircleFilled(knobCenter, 10f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 32);
        drawList.AddCircleFilled(knobCenter, 6f * scale, ImGui.GetColorU32(core), 32);
        if (UiInteract.Hover(track.Min, track.Max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                scrubbing = true;
            }
        }

        if (!scrubbing)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            scrubbing = false;
            return;
        }

        var position = Math.Clamp((ImGui.GetIO().MousePos.X - track.Min.X) / track.Width, 0f, 1f);
        control.SetTime((int)MathF.Round(position * (EorzeaTime.MinutesPerDay - 1)));
    }

    private static void DrawDayGradient(ImDrawListPtr drawList, Vector2 min, Vector2 max)
    {
        var radius = (max.Y - min.Y) * 0.5f;
        var innerMin = min.X + radius;
        var innerWidth = max.X - radius - innerMin;
        drawList.AddCircleFilled(new Vector2(min.X + radius, min.Y + radius), radius,
            ImGui.GetColorU32(DayStops[0].Color), 16);
        drawList.AddCircleFilled(new Vector2(max.X - radius, min.Y + radius), radius,
            ImGui.GetColorU32(DayStops[^1].Color), 16);
        for (var index = 1; index < DayStops.Length; index++)
        {
            var from = DayStops[index - 1];
            var to = DayStops[index];
            var left = innerMin + innerWidth * from.Bell / 24f;
            var right = innerMin + innerWidth * to.Bell / 24f;
            var leftColor = ImGui.GetColorU32(from.Color);
            var rightColor = ImGui.GetColorU32(to.Color);
            drawList.AddRectFilledMultiColor(new Vector2(left, min.Y), new Vector2(right + 0.5f, max.Y), leftColor,
                rightColor, rightColor, leftColor);
        }
    }

    private void DrawWeatherGrid(float width, in SkyPalette palette, IReadOnlyList<WeatherEntry> zoneWeathers,
        IReadOnlyList<WeatherEntry> extraWeathers, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var cellHeight = 82f * scale;
        var count = zoneWeathers.Count + extraWeathers.Count + 1;
        var rows = (count + WeatherColumns - 1) / WeatherColumns;
        var legendHeight = extraWeathers.Count > 0 ? LegendUnits * scale : 0f;
        var height = rows * cellHeight + 12f * scale + legendHeight;
        var card = new Rect(origin, origin + new Vector2(width, height));
        UiAnchors.Report("skywatcher.control.weather", card);
        WeatherCard.Panel(ImGui.GetWindowDrawList(), card, palette, sky.Density, scale);
        var inner = card.Inset(6f * scale);
        var cellWidth = inner.Width / WeatherColumns;
        for (var index = 0; index < count; index++)
        {
            var column = index % WeatherColumns;
            var row = index / WeatherColumns;
            var cell = new Rect(new Vector2(inner.Min.X + cellWidth * column, inner.Min.Y + cellHeight * row),
                new Vector2(inner.Min.X + cellWidth * (column + 1), inner.Min.Y + cellHeight * (row + 1)));
            if (index == 0)
            {
                DrawNaturalCell(cell, palette, scale);
                continue;
            }

            var natural = index - 1 < zoneWeathers.Count;
            var entry = natural ? zoneWeathers[index - 1] : extraWeathers[index - 1 - zoneWeathers.Count];
            DrawWeatherCell(cell, entry, !natural, palette, scale);
        }

        if (legendHeight > 0f)
        {
            DrawExtraLegend(new Vector2(inner.Min.X + 8f * scale, card.Max.Y - legendHeight * 0.5f - 4f * scale),
                inner.Width - 16f * scale, palette, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawNaturalCell(Rect cell, in SkyPalette palette, float scale)
    {
        var active = !control.HasWeatherOverride;
        var tile = CellTile(cell, scale);
        var drawList = ImGui.GetWindowDrawList();
        var radius = Metrics.Radius.Md * scale;
        Squircle.FillVerticalGradient(drawList, tile.Min, tile.Max, radius, ImGui.GetColorU32(palette.Top),
            ImGui.GetColorU32(palette.Bottom));
        Squircle.Stroke(drawList, tile.Min, tile.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)),
            1f * scale);
        ProgressRing.CenterIcon(tile.Center, FontAwesomeIcon.Redo, palette.Ink, 18f * scale);
        if (active)
        {
            RingActive(drawList, tile, palette, scale);
        }

        Typography.DrawCentered(new Vector2(cell.Center.X, tile.Max.Y + 12f * scale), Loc.T(L.Skywatcher.Natural),
            active ? palette.Ink : palette.InkSoft, TextStyles.Caption1.Scale,
            active ? FontWeight.SemiBold : FontWeight.Regular);
        if (UiInteract.HoverClick(cell.Min, cell.Max))
        {
            control.ClearWeather();
        }
    }

    private static void DrawExtraLegend(Vector2 leftCenter, float width, in SkyPalette palette, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var badgeCenter = new Vector2(leftCenter.X + ExtraBadgeUnits * 0.5f * scale, leftCenter.Y);
        DrawExtraBadge(drawList, badgeCenter, scale);
        var style = TextStyles.Footnote;
        var textX = badgeCenter.X + (ExtraBadgeUnits * 0.5f + 6f) * scale;
        var text = Typography.FitText(Loc.T(L.Skywatcher.ExtraWeather), leftCenter.X + width - textX, style);
        Typography.Draw(drawList, new Vector2(textX, leftCenter.Y - Typography.LineHeight(style) * 0.5f), text,
            palette.InkSoft, style);
    }

    private static void DrawExtraBadge(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        drawList.AddCircleFilled(center, ExtraBadgeUnits * 0.5f * scale, ImGui.GetColorU32(ExtraBadgeFill), 20);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Star, ExtraBadgeInk, 8f * scale);
    }

    private void DrawWeatherCell(Rect cell, WeatherEntry entry, bool extra, in SkyPalette palette, float scale)
    {
        var active = control.HasWeatherOverride && control.WeatherOverride == entry.Id;
        var tile = CellTile(cell, scale);
        var drawList = ImGui.GetWindowDrawList();
        WeatherCard.Chip(drawList, tile, WeatherSky.Classify(entry.EnglishKey), true, scale);
        if (extra)
        {
            DrawExtraBadge(drawList, new Vector2(tile.Max.X - 9f * scale, tile.Min.Y + 9f * scale), scale);
        }
        if (active)
        {
            RingActive(drawList, tile, palette, scale);
        }

        Typography.DrawCentered(new Vector2(cell.Center.X, tile.Max.Y + 12f * scale),
            Typography.FitText(entry.Name, cell.Width - 8f * scale, TextStyles.Caption1.Scale,
                TextStyles.Caption1.Weight), active ? palette.Ink : palette.InkSoft, TextStyles.Caption1.Scale,
            active ? FontWeight.SemiBold : FontWeight.Regular);
        if (UiInteract.HoverClick(cell.Min, cell.Max))
        {
            control.SetWeather(entry.Id);
        }
    }

    private static Rect CellTile(Rect cell, float scale)
    {
        var tileWidth = MathF.Min(cell.Width - 14f * scale, 74f * scale);
        var tileHeight = 44f * scale;
        var top = cell.Min.Y + 4f * scale;
        return new Rect(new Vector2(cell.Center.X - tileWidth * 0.5f, top),
            new Vector2(cell.Center.X + tileWidth * 0.5f, top + tileHeight));
    }

    private static void RingActive(ImDrawListPtr drawList, Rect tile, in SkyPalette palette, float scale)
    {
        var pad = 2.5f * scale;
        var min = new Vector2(tile.Min.X - pad, tile.Min.Y - pad);
        var max = new Vector2(tile.Max.X + pad, tile.Max.Y + pad);
        Squircle.Stroke(drawList, min, max, Metrics.Radius.Md * scale + pad, ImGui.GetColorU32(palette.Ink),
            2f * scale);
    }

    private static bool DrawPill(Rect pill, string label, bool active, in SkyPalette palette, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = pill.Height * 0.5f;
        drawList.AddRectFilled(pill.Min, pill.Max,
            ImGui.GetColorU32(palette.Ink with { W = active ? 0.22f : 0.08f }), rounding);
        if (active)
        {
            drawList.AddRect(pill.Min, pill.Max, ImGui.GetColorU32(palette.Ink with { W = 0.24f }), rounding,
                ImDrawFlags.RoundCornersAll, 1f * scale);
        }

        Typography.DrawCentered(pill.Center, label, active ? palette.Ink : palette.InkSoft, TextStyles.Caption1.Scale,
            active ? FontWeight.SemiBold : FontWeight.Regular);
        return UiInteract.HoverClick(pill.Min, pill.Max);
    }

    private void DrawControlFooter(float width, in SkyPalette palette, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var height = 34f * scale;
        var button = new Rect(origin, origin + new Vector2(width, height));
        var live = control.HasOverride;
        var drawList = ImGui.GetWindowDrawList();
        if (live)
        {
            WeatherCard.Panel(drawList, button, palette, sky.Density, scale, height * 0.5f);
        }
        else
        {
            drawList.AddRectFilled(button.Min, button.Max, ImGui.GetColorU32(palette.Ink with { W = 0.06f }),
                height * 0.5f);
        }

        Typography.DrawCentered(button.Center, Loc.T(L.Skywatcher.Reset), live ? palette.Ink : palette.InkFaint,
            TextStyles.Caption1.Scale, live ? FontWeight.SemiBold : FontWeight.Regular);
        if (live && UiInteract.HoverClick(button.Min, button.Max))
        {
            control.ClearAll();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        var notice = control.CanControl ? Loc.T(L.Skywatcher.LocalOnly) : Loc.T(L.Skywatcher.CombatPaused);
        var noticeOrigin = ImGui.GetCursorScreenPos() + new Vector2(4f * scale, 0f);
        var noticeShadow = new Vector4(0f, 0f, 0f, palette.LightSky ? 0.12f : 0.30f);
        Typography.DrawWrappedLeft(noticeOrigin + new Vector2(0f, 1f * scale), notice, noticeShadow,
            TextStyles.Footnote, width - 8f * scale);
        var noticeHeight = Typography.DrawWrappedLeft(noticeOrigin, notice, palette.Ink with { W = 0.58f },
            TextStyles.Footnote, width - 8f * scale);
        ImGui.Dummy(new Vector2(width, noticeHeight));
    }

    private static void DrawControlEmpty(in SkyPalette palette, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + ImGui.GetContentRegionAvail().Y * 0.35f);
        ProgressRing.CenterIcon(center, FontAwesomeIcon.CloudSun, palette.InkFaint, 40f * scale);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 44f * scale),
            Loc.T(L.Skywatcher.NothingToChange), palette.InkSoft, TextStyles.Subheadline);
    }
}
