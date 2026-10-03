using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Maps;

internal sealed partial class MapsApp
{
    private const float PlaceRowHeight = 58f;
    private const float PlaceRowInset = 44f;
    private const float RowGlyphDisc = 32f;
    private const float RowGlyphSize = 16f;
    private const float RowStarSize = 14f;
    private const float RowChevronSize = 5f;
    private const float RowChevronStroke = 2f;
    private const float RowLineGap = 2f;
    private const float RowWashInset = 3f;

    private void DrawExpansionHeader(ImDrawListPtr drawList, Rect header, float scale)
    {
        var top = header.Min.Y + SheetMetrics.GrabberZone * scale;
        var rowHeight = GlassField.HeightUnits * scale;
        var inset = Metrics.Space.Lg * scale;
        var radius = MapChrome.CircleSize * 0.5f * scale;
        var backCenter = new Vector2(header.Min.X + inset + radius, top + rowHeight * 0.5f);
        if (MapChrome.Circle(drawList, "maps.back", backCenter, PhoneIcons.ChevronLeft, theme.TextStrong, theme, scale,
                Loc.T(L.Maps.Back), false))
        {
            PopPage();
        }

        var title = ExpansionTitle();
        var titleLeft = backCenter.X + radius + Metrics.Space.Md * scale;
        var titleSize = Typography.Measure(title, TextStyles.Title3);
        Marquee.DrawLeftAuto(drawList, "maps.expansion.title", title, titleLeft,
            top + (rowHeight - titleSize.Y) * 0.5f, MathF.Max(1f, header.Max.X - inset - titleLeft),
            TextStyles.Title3, theme.TextStrong);
    }

    private string ExpansionTitle()
    {
        var expansion = FindExpansion();
        return expansion is null ? string.Empty : expansion.Name;
    }

    private MapExpansion? FindExpansion()
    {
        var expansions = maps.Expansions;
        for (var index = 0; index < expansions.Count; index++)
        {
            if (expansions[index].Order == openExpansion)
            {
                return expansions[index];
            }
        }

        return null;
    }

    private void DrawExpansionPage(float scale)
    {
        var expansion = FindExpansion();
        if (expansion is null)
        {
            PopPage();
            return;
        }

        var anchorTaken = false;
        var regions = expansion.Regions;
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var region = regions[regionIndex];
            var destinations = region.Aetherytes;
            if (destinations.Count == 0)
            {
                continue;
            }

            ListSection.Header(region.Name, theme.TextMuted);
            var card = GroupCard.Begin(theme, destinations.Count, PlaceRowHeight);
            card.SeparatorInset = PlaceRowInset;
            for (var index = 0; index < destinations.Count; index++)
            {
                var row = card.NextRow();
                if (!anchorTaken)
                {
                    anchorTaken = true;
                    ReportVisible("maps.destination.first", RowBounds(row, card.Bounds));
                }

                DrawPlaceRow(row, destinations[index], PhoneIcons.MapPin, default, false, scale, card.Bounds);
            }

            card.End();
        }
    }

    private static Rect RowBounds(Rect row, Rect card) =>
        new(new Vector2(card.Min.X, row.Min.Y), new Vector2(card.Max.X, row.Max.Y));

    private void DrawPlaceRow(Rect row, MapAetheryte aetheryte, string glyph, in MapSearchHit hit, bool useHit,
        float scale, Rect card)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bounds = RowBounds(row, card);
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        if (hovered)
        {
            MapGlyphs.Highlight(drawList, row, theme.HoverWash, RowWashInset, scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var disc = RowGlyphDisc * scale;
        var discCenter = new Vector2(row.Min.X + disc * 0.5f, row.Center.Y);
        var surface = IconTile.Surface(accent);
        Squircle.FillCircleVerticalGradient(drawList, discCenter, disc * 0.5f,
            ImGui.GetColorU32(Palette.Lighten(surface, 0.10f) with { W = 1f }),
            ImGui.GetColorU32(Palette.Darken(surface, 0.14f) with { W = 1f }));
        PhoneIcon.Draw(drawList, discCenter, glyph, AccentRing.Ink, RowGlyphSize * scale);
        var chevronTip = new Vector2(row.Max.X, row.Center.Y);
        MapGlyphs.ChevronRight(drawList, chevronTip, RowChevronSize * scale, RowChevronStroke * scale,
            hovered ? theme.Accent : theme.TextMuted);
        var textRight = chevronTip.X - RowChevronSize * scale - Metrics.Space.Md * scale;
        if (favorites.Contains(aetheryte.RowId))
        {
            var starCenter = new Vector2(textRight - RowStarSize * 0.5f * scale, row.Center.Y);
            PhoneIcon.Draw(drawList, starCenter, PhoneIcons.StarFilled, MapGlyphs.FavoriteStar, RowStarSize * scale);
            textRight = starCenter.X - RowStarSize * 0.5f * scale - Metrics.Space.Sm * scale;
        }

        var textLeft = discCenter.X + disc * 0.5f + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var titleHeight = Typography.Measure(aetheryte.Name, TextStyles.Body).Y;
        var subtitleHeight = Typography.Measure(aetheryte.Subtitle, TextStyles.Footnote).Y;
        var textTop = row.Center.Y - (titleHeight + RowLineGap * scale + subtitleHeight) * 0.5f;
        MapGlyphs.Line(drawList, new Vector2(textLeft, textTop), aetheryte.Name, textWidth, TextStyles.Body,
            theme.TextStrong, theme.Accent, in hit, useHit && hit.InName);
        MapGlyphs.Line(drawList, new Vector2(textLeft, textTop + titleHeight + RowLineGap * scale),
            aetheryte.Subtitle, textWidth, TextStyles.Footnote, theme.TextMuted, theme.Accent, in hit,
            useHit && !hit.InName);
        if (!UiInteract.Click(bounds.Min, bounds.Max, hovered))
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        OpenPlace(aetheryte);
    }
}
