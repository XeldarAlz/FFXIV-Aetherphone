using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Skywatcher;

internal sealed partial class SkywatcherApp
{
    private const int ZoneQueryMaxLength = 48;
    private const int MaxZoneMatches = 30;
    private const int ZoneLookaheadWindows = 8;
    private const float ZoneCardUnits = 108f;
    private const float ZoneRowUnits = 52f;
    private const float ZoneSaveHitUnits = 16f;
    private static readonly Vector4 RemoveFill = new(0.96f, 0.27f, 0.25f, 1f);
    private readonly List<WeatherZone> allZones = new();
    private readonly List<WeatherEntry> allZoneWeather = new();
    private readonly List<int> allZoneIndices = new();
    private readonly List<int> zoneMatches = new();
    private readonly List<ZoneCard> zoneCards = new();
    private readonly List<WeatherWindow> zoneScratch = new();
    private string zoneQuery = string.Empty;
    private string? matchedQuery;
    private CultureInfo? zonesCulture;
    private bool editingZones;
    private uint pendingRemoval;

    private readonly record struct ZoneCard(uint Territory, string Name, WeatherEntry Weather, bool Current,
        string Status);

    private void RefreshZoneCards()
    {
        zoneCards.Clear();
        var current = weather.CurrentTerritory;
        weather.Forecast(zoneScratch, ZoneLookaheadWindows);
        if (zoneScratch.Count > 0)
        {
            zoneCards.Add(new ZoneCard(current, weather.CurrentZone(), zoneScratch[0].Weather, true,
                ZoneStatus(zoneScratch)));
        }

        var saved = configuration.SkywatcherZones;
        for (var index = 0; index < saved.Count; index++)
        {
            var territory = saved[index];
            if (territory == current)
            {
                continue;
            }

            weather.Forecast(territory, zoneScratch, ZoneLookaheadWindows);
            if (zoneScratch.Count == 0)
            {
                continue;
            }

            zoneCards.Add(new ZoneCard(territory, weather.ZoneName(territory), zoneScratch[0].Weather, false,
                ZoneStatus(zoneScratch)));
        }
    }

    private static string ZoneStatus(List<WeatherWindow> windows)
    {
        var current = windows[0].Weather.Id;
        for (var index = 1; index < windows.Count; index++)
        {
            if (windows[index].Weather.Id != current)
            {
                return $"{windows[index].Weather.Name} {LongWhen(windows[index])}";
            }
        }

        return Loc.T(L.Skywatcher.ForNextHours, windows[0].Weather.Name);
    }

    private void DrawZones(in SkyPalette palette, float daylight, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        DrawZonesTitle(width, palette, scale);
        DrawZoneSearch(width, palette, scale);
        if (zoneQuery.Length > 0)
        {
            DrawZoneMatches(width, palette, daylight, scale);
            return;
        }

        for (var index = 0; index < zoneCards.Count; index++)
        {
            DrawZoneCard(width, zoneCards[index], daylight, scale);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }

        ApplyPendingRemoval();

        if (configuration.SkywatcherZones.Count == 0)
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            var origin = ImGui.GetCursorScreenPos();
            var height = Typography.DrawWrappedCentered(origin + new Vector2(width * 0.5f, 0f),
                Loc.T(L.Skywatcher.ZonesEmpty), palette.InkSoft, TextStyles.Subheadline, width - 24f * scale);
            ImGui.Dummy(new Vector2(width, height));
        }

        EnsureZones();
        DrawZoneGroups(allZoneIndices, width, palette, daylight, scale);
    }

    private void EnsureZones()
    {
        if (allZones.Count > 0 && ReferenceEquals(zonesCulture, Loc.Culture))
        {
            return;
        }

        weather.WeatherZones(allZones);
        zonesCulture = Loc.Culture;
        matchedQuery = null;
        allZoneIndices.Clear();
        for (var index = 0; index < allZones.Count; index++)
        {
            allZoneIndices.Add(index);
        }

        RefreshZoneWeather();
    }

    private void RefreshZoneWeather()
    {
        if (activeTab != SkywatcherTab.Zones || allZones.Count == 0)
        {
            return;
        }

        allZoneWeather.Clear();
        for (var index = 0; index < allZones.Count; index++)
        {
            weather.Forecast(allZones[index].TerritoryId, zoneScratch, 1);
            allZoneWeather.Add(zoneScratch.Count > 0 ? zoneScratch[0].Weather : default);
        }
    }

    private static bool SameRegion(in WeatherZone left, in WeatherZone right) =>
        left.FieldOperation == right.FieldOperation &&
        string.Equals(left.Region, right.Region, StringComparison.Ordinal);

    private static string RegionLabel(in WeatherZone zone)
    {
        if (zone.FieldOperation)
        {
            return Loc.T(L.Skywatcher.FieldOperations);
        }

        return zone.Region.Length > 0 ? zone.Region : Loc.T(L.Skywatcher.OtherZones);
    }

    private void ApplyPendingRemoval()
    {
        if (pendingRemoval == 0)
        {
            return;
        }

        configuration.SkywatcherZones.Remove(pendingRemoval);
        configuration.Save();
        pendingRemoval = 0;
        if (configuration.SkywatcherZones.Count == 0)
        {
            editingZones = false;
        }

        RefreshZoneCards();
    }

    private void DrawZonesTitle(float width, in SkyPalette palette, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var style = TextStyles.LargeTitle;
        var height = Typography.LineHeight(style) + 6f * scale;
        ShadowText(drawList, origin + new Vector2(2f * scale, 0f), Loc.T(L.Skywatcher.Zones), palette.Ink, style,
            palette, scale);
        if (configuration.SkywatcherZones.Count > 0 && zoneQuery.Length == 0)
        {
            var label = Loc.T(editingZones ? L.Skywatcher.Done : L.Skywatcher.Edit);
            var labelStyle = editingZones ? TextStyles.BodyEmphasized : TextStyles.Body;
            var labelSize = Typography.Measure(label, labelStyle);
            var labelMin = new Vector2(origin.X + width - labelSize.X - 4f * scale,
                origin.Y + (height - labelSize.Y) * 0.5f);
            ShadowText(drawList, labelMin, label, palette.Ink, labelStyle, palette, scale);
            var hit = new Rect(labelMin - new Vector2(8f * scale, 6f * scale),
                labelMin + labelSize + new Vector2(8f * scale, 6f * scale));
            if (UiInteract.HoverClick(hit.Min, hit.Max))
            {
                editingZones = !editingZones;
            }
        }

        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawZoneSearch(float width, in SkyPalette palette, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var field = new Rect(origin, origin + new Vector2(width, GlassField.HeightUnits * scale));
        WeatherCard.Panel(drawList, field, palette, sky.Density, scale, GlassField.Radius(field));
        GlassField.Search(drawList, field, "##skywatcherZoneSearch", Loc.T(L.Skywatcher.SearchZones), ref zoneQuery,
            PhoneTheme.Default, scale, ZoneQueryMaxLength, false);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, field.Height + 12f * scale));
    }

    private void DrawZoneMatches(float width, in SkyPalette palette, float daylight, float scale)
    {
        UpdateZoneMatches();
        if (zoneMatches.Count > 0)
        {
            DrawZoneGroups(zoneMatches, width, palette, daylight, scale);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.DrawWrappedCentered(origin + new Vector2(width * 0.5f, 8f * scale),
            Loc.T(L.Skywatcher.NoZoneMatch), palette.InkSoft, TextStyles.Subheadline, width - 24f * scale);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }

    private void DrawZoneGroups(List<int> indices, float width, in SkyPalette palette, float daylight, float scale)
    {
        var first = 0;
        while (first < indices.Count)
        {
            var last = first + 1;
            while (last < indices.Count && SameRegion(allZones[indices[first]], allZones[indices[last]]))
            {
                last++;
            }

            SectionLabel(RegionLabel(allZones[indices[first]]), palette, scale);
            DrawZoneGroup(indices, first, last, width, palette, daylight, scale);
            first = last;
        }
    }

    private void DrawZoneGroup(List<int> indices, int first, int last, float width, in SkyPalette palette,
        float daylight, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cardOrigin = ImGui.GetCursorScreenPos();
        var padding = WeatherCard.PaddingUnits * scale;
        var rowHeight = ZoneRowUnits * scale;
        var card = new Rect(cardOrigin, cardOrigin + new Vector2(width, (last - first) * rowHeight));
        WeatherCard.Panel(drawList, card, palette, sky.Density, scale);
        var nameHeight = Typography.LineHeight(TextStyles.Body);
        var weatherHeight = Typography.LineHeight(TextStyles.Footnote);
        var isDay = daylight >= 0.5f;
        for (var row = first; row < last; row++)
        {
            var rowTop = card.Min.Y + (row - first) * rowHeight;
            var rowRect = new Rect(new Vector2(card.Min.X, rowTop), new Vector2(card.Max.X, rowTop + rowHeight));
            if (!ImGui.IsRectVisible(rowRect.Min, rowRect.Max))
            {
                continue;
            }

            if (row > first)
            {
                WeatherCard.Divider(drawList, card, rowTop, palette, scale);
            }

            DrawZoneRow(drawList, rowRect, indices[row], palette, isDay, nameHeight, weatherHeight, padding, scale);
        }

        ImGui.SetCursorScreenPos(cardOrigin);
        ImGui.Dummy(new Vector2(width, card.Height));
    }

    private void DrawZoneRow(ImDrawListPtr drawList, Rect rowRect, int zoneIndex, in SkyPalette palette, bool isDay,
        float nameHeight, float weatherHeight, float padding, float scale)
    {
        var zone = allZones[zoneIndex];
        var saved = configuration.SkywatcherZones;
        var isSaved = saved.Contains(zone.TerritoryId) || zone.TerritoryId == weather.CurrentTerritory;
        var saveCenter = new Vector2(rowRect.Max.X - padding - 8f * scale, rowRect.Center.Y);
        var glyphCenter = new Vector2(saveCenter.X - 30f * scale, rowRect.Center.Y);
        var textLeft = rowRect.Min.X + padding;
        var textWidth = glyphCenter.X - 16f * scale - textLeft;
        var textTop = rowRect.Center.Y - (nameHeight + weatherHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(zone.Name, textWidth, TextStyles.Body), palette.Ink, TextStyles.Body);
        var zoneWeather = zoneIndex < allZoneWeather.Count ? allZoneWeather[zoneIndex] : default;
        if (zoneWeather.Name is { Length: > 0 })
        {
            Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight),
                Typography.FitText(zoneWeather.Name, textWidth, TextStyles.Footnote), palette.InkSoft,
                TextStyles.Footnote);
            WeatherGlyph.Draw(drawList, WeatherSky.Classify(zoneWeather.EnglishKey), glyphCenter, 11f * scale,
                palette, isDay, palette.Horizon);
        }

        ProgressRing.CenterIcon(drawList, saveCenter, isSaved ? FontAwesomeIcon.Check : FontAwesomeIcon.Plus,
            isSaved ? palette.InkSoft : palette.Ink, 13f * scale);
        var opened = UiInteract.HoverClick(rowRect.Min, rowRect.Max);
        if (!isSaved && UiInteract.HoverClickCircle(saveCenter, ZoneSaveHitUnits * scale))
        {
            saved.Add(zone.TerritoryId);
            configuration.Save();
            RefreshZoneCards();
            return;
        }

        if (!opened)
        {
            return;
        }

        zoneQuery = string.Empty;
        View(zone.TerritoryId);
    }

    private void UpdateZoneMatches()
    {
        EnsureZones();
        if (string.Equals(matchedQuery, zoneQuery, StringComparison.Ordinal))
        {
            return;
        }

        matchedQuery = zoneQuery;
        zoneMatches.Clear();
        var compare = Loc.Culture.CompareInfo;
        for (var index = 0; index < allZones.Count && zoneMatches.Count < MaxZoneMatches; index++)
        {
            var zone = allZones[index];
            if (compare.IndexOf(zone.Name, zoneQuery, CompareOptions.IgnoreCase) >= 0 ||
                compare.IndexOf(RegionLabel(zone), zoneQuery, CompareOptions.IgnoreCase) >= 0)
            {
                zoneMatches.Add(index);
            }
        }
    }

    private void DrawZoneCard(float width, in ZoneCard zoneCard, float daylight, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var card = new Rect(origin, origin + new Vector2(width, ZoneCardUnits * scale));
        var radius = Metrics.Radius.Grouped * scale;
        var kind = WeatherSky.Classify(zoneCard.Weather.EnglishKey);
        var zonePalette = WeatherSky.Blend(kind, daylight);
        WeatherSky.Paint(drawList, card, radius, zonePalette);
        WeatherAmbience.Draw(drawList, card, radius, kind, daylight, zonePalette, scale, 1f);
        var hovered = UiInteract.Hover(card.Min, card.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(White with { W = 0.06f }));
        }

        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(White with { W = 0.12f }),
            1f * scale);
        var padding = WeatherCard.PaddingUnits * scale;
        var glyphReserve = 52f * scale;
        var titleStyle = TextStyles.Title3;
        var nameWidth = card.Width - padding * 2f - glyphReserve;
        ShadowText(drawList, new Vector2(card.Min.X + padding, card.Min.Y + padding),
            Typography.FitText(zoneCard.Name, nameWidth, titleStyle), zonePalette.Ink, titleStyle, zonePalette, scale);
        var subtitleTop = card.Min.Y + padding + Typography.LineHeight(titleStyle);
        if (zoneCard.Current)
        {
            var captionStyle = TextStyles.FootnoteEmphasized;
            var iconCenterY = subtitleTop + Typography.LineHeight(captionStyle) * 0.5f;
            ProgressRing.CenterIcon(drawList, new Vector2(card.Min.X + padding + 5f * scale, iconCenterY),
                FontAwesomeIcon.LocationArrow, zonePalette.InkSoft, 9f * scale);
            ShadowText(drawList, new Vector2(card.Min.X + padding + 15f * scale, subtitleTop),
                Typography.FitText(Loc.T(L.Skywatcher.CurrentZone), nameWidth - 15f * scale, captionStyle),
                zonePalette.InkSoft, captionStyle, zonePalette, scale);
        }

        var statusStyle = TextStyles.Footnote;
        var statusTop = card.Max.Y - padding - Typography.LineHeight(statusStyle);
        var weatherStyle = TextStyles.SubheadlineEmphasized;
        var weatherName = Typography.FitText(zoneCard.Weather.Name, card.Width * 0.45f, weatherStyle);
        var weatherWidth = Typography.Measure(weatherName, weatherStyle).X;
        ShadowText(drawList, new Vector2(card.Max.X - padding - weatherWidth, statusTop), weatherName, zonePalette.Ink,
            weatherStyle, zonePalette, scale);
        ShadowText(drawList, new Vector2(card.Min.X + padding, statusTop),
            Typography.FitText(zoneCard.Status, card.Width - padding * 3f - weatherWidth, statusStyle),
            zonePalette.InkSoft, statusStyle, zonePalette, scale);
        var glyphCenter = new Vector2(card.Max.X - padding - 18f * scale, card.Min.Y + padding + 16f * scale);
        if (editingZones && !zoneCard.Current)
        {
            drawList.AddCircleFilled(glyphCenter, 13f * scale, ImGui.GetColorU32(RemoveFill), 32);
            drawList.AddLine(glyphCenter - new Vector2(6f * scale, 0f), glyphCenter + new Vector2(6f * scale, 0f),
                ImGui.GetColorU32(White), 2.2f * scale);
            if (UiInteract.HoverClickCircle(glyphCenter, 16f * scale))
            {
                pendingRemoval = zoneCard.Territory;
            }
        }
        else
        {
            WeatherGlyph.Draw(drawList, kind, glyphCenter, 17f * scale, zonePalette, daylight >= 0.5f, zonePalette.Top);
            if (!editingZones && UiInteract.HoverClick(card.Min, card.Max))
            {
                View(zoneCard.Current ? 0 : zoneCard.Territory);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, card.Height));
    }
}
