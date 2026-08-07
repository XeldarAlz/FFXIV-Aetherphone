using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Skywatcher;

internal sealed partial class SkywatcherApp
{
    private const int WeatherColumns = 3;
    private static readonly (int Minutes, LocString Label)[] TimePresets =
    {
        (6 * 60, L.Skywatcher.Dawn),
        (12 * 60, L.Skywatcher.Noon),
        (18 * 60, L.Skywatcher.Dusk),
        (0, L.Skywatcher.Midnight),
    };

    private bool scrubbing;

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
        DrawWeatherGrid(width, ink, zoneWeathers, scale);
        DrawControlFooter(width, palette, scale);
        ImGui.Dummy(new Vector2(0f, 8f * scale));
    }

    private void DrawTimeCard(float width, in SkyPalette palette, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = 132f * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
        DrawGlass(card, palette, scale);
        var inner = card.Inset(14f * scale);
        var custom = control.HasTimeOverride;
        var minutes = custom ? control.TimeOverrideMinutes : EorzeaTime.CurrentMinuteOfDay();
        Typography.Draw(inner.Min, $"{minutes / 60:D2}:{minutes % 60:D2}", palette.Ink, TextStyles.Title1);
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
        var barHeight = 6f * scale;
        var barMin = new Vector2(track.Min.X, track.Center.Y - barHeight * 0.5f);
        var barMax = new Vector2(track.Max.X, track.Center.Y + barHeight * 0.5f);
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(palette.Ink with { W = 0.16f }), barHeight * 0.5f);
        var fraction = minutes / (float)(EorzeaTime.MinutesPerDay - 1);
        var knobX = track.Min.X + track.Width * fraction;
        drawList.AddRectFilled(barMin, new Vector2(knobX, barMax.Y),
            ImGui.GetColorU32(palette.Ink with { W = 0.42f }), barHeight * 0.5f);
        var knobCenter = new Vector2(knobX, track.Center.Y);
        drawList.AddCircleFilled(knobCenter, 9f * scale, ImGui.GetColorU32(palette.Ink), 24);
        drawList.AddCircleFilled(knobCenter, 5f * scale, ImGui.GetColorU32(palette.Horizon), 24);
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

    private void DrawWeatherGrid(float width, in SkyPalette palette, IReadOnlyList<WeatherEntry> zoneWeathers,
        float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var cellHeight = 82f * scale;
        var count = zoneWeathers.Count + 1;
        var rows = (count + WeatherColumns - 1) / WeatherColumns;
        var height = rows * cellHeight + 12f * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
        DrawGlass(card, palette, scale);
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

            DrawWeatherCell(cell, zoneWeathers[index - 1], palette, scale);
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

    private void DrawWeatherCell(Rect cell, WeatherEntry entry, in SkyPalette palette, float scale)
    {
        var active = control.HasWeatherOverride && control.WeatherOverride == entry.Id;
        var tile = CellTile(cell, scale);
        var drawList = ImGui.GetWindowDrawList();
        WeatherCard.Chip(drawList, tile, WeatherSky.Classify(entry.EnglishKey), true, scale);
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
            WeatherCard.Panel(drawList, button, palette, scale, height * 0.5f);
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
