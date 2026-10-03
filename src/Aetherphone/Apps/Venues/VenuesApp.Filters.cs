using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private const float CardInset = 14f;
    private const float SourceRowHeight = 60f;
    private const float SourceTileSide = 34f;
    private const float ToggleWidth = 46f;
    private const float ToggleHeight = 28f;
    private const float TagRowHeight = 46f;
    private const float TagSearchHeight = 38f;
    private const float FilterBarHeight = 74f;
    private const float FilterCtaHeight = 48f;
    private const int CollapsedTagCount = 10;

    private static readonly TextStyle RowTitleStyle = TextStyles.BodyEmphasized;
    private static readonly TextStyle RowHelpStyle = TextStyles.Footnote;
    private static readonly TextStyle TagRowStyle = TextStyles.Body;
    private static readonly TextStyle TagCountStyle = TextStyles.Footnote;

    private static readonly LocString[] SourceTitles =
    {
        L.Venues.AllSources, L.Venues.SourceFfxiv, L.Venues.SourcePartake, L.Venues.SourceRolladeck,
    };

    private static readonly LocString[] SourceHints =
    {
        L.Venues.SourceAllHint, L.Venues.SourceFfxivHint, L.Venues.SourcePartakeHint, L.Venues.SourceRolladeckHint,
    };

    private static readonly string[] SourceGlyphs =
    {
        PhoneIcons.World, PhoneIcons.MapPin, PhoneIcons.Calendar, PhoneIcons.Music,
    };

    private static readonly Vector4[] SourceTints =
    {
        new(0.55f, 0.52f, 0.95f, 1f), new(0.93f, 0.40f, 0.62f, 1f), new(0.95f, 0.60f, 0.24f, 1f),
        new(0.36f, 0.76f, 0.58f, 1f),
    };

    private readonly VenueTagIndex tagIndex = new();
    private readonly ChipRail selectedRail = new();
    private bool[] selectedActive = Array.Empty<bool>();
    private string tagSearch = string.Empty;
    private bool tagsExpanded;
    private string showVenuesLabel = string.Empty;
    private string showAllTagsLabel = string.Empty;
    private readonly List<string> tagCountLabels = new();

    private void DrawFilters(Rect area)
    {
        var scale = UiScale.Current;
        RefreshTagIndex();
        var reset = Loc.T(L.Venues.ResetFilters);
        var active = FiltersActive || configuration.VenueHideAdult;
        var reserve = active ? AppSkin.HeaderActionWidth(reset) / scale + 8f : 0f;
        SocialChrome.DrawScreenHeader(area, Loc.T(L.Venues.Filters), Ink, back, ScreenTitleStyle, reserve,
            string.Empty, true, true);
        if (active && ui.HeaderAction(area, reset, true))
        {
            ResetFilters();
            configuration.VenueHideAdult = false;
            configuration.Save();
        }

        var bar = new Rect(new Vector2(area.Min.X, area.Max.Y - FilterBarHeight * scale), area.Max);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale),
            new Vector2(area.Max.X, bar.Min.Y));
        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.Sources)), scale);
            DrawSourceCard(scale);
            DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.Content)), scale);
            var hideAdult = DrawToggleCard("venues.hideAdult", Loc.T(L.Venues.HideAdult), Loc.T(L.Venues.HideAdultHint),
                configuration.VenueHideAdult, scale);
            if (hideAdult != configuration.VenueHideAdult)
            {
                configuration.VenueHideAdult = hideAdult;
                configuration.Save();
                visibleCards = PageSize;
            }

            DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.Tags)), scale);
            DrawSelectedTags(scale);
            DrawTagSearch(scale);
            DrawTagRows(scale);
            DrawSectionHeading(Loc.Upper(Loc.T(L.Apps.Notifications)), scale);
            var notify = DrawToggleCard("venues.notifyNew", Loc.T(L.Venues.NotifyNew), Loc.T(L.Venues.NotifyNewHelp),
                configuration.VenueNotifyNewEvents, scale);
            if (notify != configuration.VenueNotifyNewEvents)
            {
                configuration.VenueNotifyNewEvents = notify;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }

        DrawFilterBar(bar, scale);
    }

    private void RefreshTagIndex()
    {
        var scope = ResolveScope();
        var key = new VenueTagIndexKey(venues.Version, configuration.VenueSourceFilter, scope.DataCenters, scope.World,
            configuration.VenueHideAdult, tagsStamp);
        if (!tagIndex.Update(key, venues.Events, selectedTags) && !CheckLanguage())
        {
            return;
        }

        var culture = Loc.Culture;
        showVenuesLabel = Loc.T(L.Venues.ShowVenues, tagIndex.MatchCount.ToString("N0", culture));
        showAllTagsLabel = Loc.T(L.Venues.ShowAllTags, tagIndex.Entries.Count.ToString("N0", culture));
        tagCountLabels.Clear();
        for (var index = 0; index < tagIndex.Entries.Count; index++)
        {
            tagCountLabels.Add(tagIndex.Entries[index].Count.ToString("N0", culture));
        }
    }

    private void DrawSourceCard(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var rowHeight = SourceRowHeight * scale;
        var card = new Rect(new Vector2(origin.X + pad, origin.Y),
            new Vector2(origin.X + width - pad, origin.Y + rowHeight * VenueFilter.SourceCount));
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var inset = CardInset * scale;
        for (var source = 0; source < VenueFilter.SourceCount; source++)
        {
            var rowMin = new Vector2(card.Min.X, card.Min.Y + source * rowHeight);
            var rowMax = new Vector2(card.Max.X, rowMin.Y + rowHeight);
            var hovered = UiInteract.Hover(rowMin, rowMax);
            var selected = configuration.VenueSourceFilter == source;
            if (hovered)
            {
                DrawRowHover(drawList, rowMin, rowMax, source, VenueFilter.SourceCount, rounding);
            }

            var centerY = rowMin.Y + rowHeight * 0.5f;
            var tileSide = SourceTileSide * scale;
            var tileMin = new Vector2(rowMin.X + inset, centerY - tileSide * 0.5f);
            var tint = SourceTints[source];
            Squircle.Fill(drawList, tileMin, tileMin + new Vector2(tileSide, tileSide), 10f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(tint, 0.9f)));
            PhoneIcon.Draw(drawList, tileMin + new Vector2(tileSide * 0.5f, tileSide * 0.5f), SourceGlyphs[source],
                MediaOverlay.White, 18f * scale);
            var checkCenter = new Vector2(rowMax.X - inset - 11f * scale, centerY);
            PhoneIcon.Draw(drawList, checkCenter, selected ? PhoneIcons.CircleCheckFilled : PhoneIcons.Circle,
                selected ? Ink.Accent : Ink.FaintInk, 22f * scale);
            var textLeft = tileMin.X + tileSide + 12f * scale;
            var textWidth = MathF.Max(1f, checkCenter.X - 20f * scale - textLeft);
            var titleHeight = Typography.LineHeight(RowTitleStyle);
            var helpHeight = Typography.LineHeight(RowHelpStyle);
            var top = centerY - (titleHeight + helpHeight) * 0.5f;
            Typography.Draw(drawList, new Vector2(textLeft, top),
                Typography.FitText(Loc.T(SourceTitles[source]), textWidth, RowTitleStyle), Ink.TitleInk,
                RowTitleStyle);
            Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
                Typography.FitText(Loc.T(SourceHints[source]), textWidth, RowHelpStyle), Ink.MutedInk, RowHelpStyle);
            if (source < VenueFilter.SourceCount - 1)
            {
                FeedCell.Hairline(drawList, textLeft, rowMax.X, rowMax.Y, Ink.Hairline);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(rowMin, rowMax, hovered) && !selected)
            {
                configuration.VenueSourceFilter = source;
                configuration.Save();
                visibleCards = PageSize;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, card.Height + Metrics.Space.Sm * scale));
    }

    private void DrawRowHover(ImDrawListPtr drawList, Vector2 min, Vector2 max, int index, int count, float rounding)
    {
        var flags = count == 1 ? ImDrawFlags.RoundCornersAll
            : index == 0 ? ImDrawFlags.RoundCornersTop
            : index == count - 1 ? ImDrawFlags.RoundCornersBottom : ImDrawFlags.RoundCornersNone;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Ink.HoverTint),
            flags == ImDrawFlags.RoundCornersNone ? 0f : rounding, flags);
    }

    private bool DrawToggleCard(string id, string title, string help, bool value, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var inner = CardInset * scale;
        var cardLeft = origin.X + pad;
        var cardRight = origin.X + width - pad;
        var toggleWidth = ToggleWidth * scale;
        var textLeft = cardLeft + inner;
        var textWidth = MathF.Max(1f, cardRight - inner - toggleWidth - 12f * scale - textLeft);
        var titleHeight = Typography.LineHeight(RowTitleStyle);
        var helpHeight = Typography.CountWrappedLines(help, RowHelpStyle, textWidth) *
                         Typography.LineHeight(RowHelpStyle);
        var cardHeight = inner * 2f + titleHeight + 3f * scale + helpHeight;
        ui.Card(drawList, new Vector2(cardLeft, origin.Y), new Vector2(cardRight, origin.Y + cardHeight),
            Metrics.Radius.Card * scale, elevated: true);
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + inner),
            Typography.FitText(title, textWidth, RowTitleStyle), Ink.TitleInk, RowTitleStyle);
        Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + inner + titleHeight + 3f * scale), help,
            Ink.MutedInk, RowHelpStyle, textWidth);
        var toggleMin = new Vector2(cardRight - inner - toggleWidth,
            origin.Y + (cardHeight - ToggleHeight * scale) * 0.5f);
        var next = Toggle.Draw(id, new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, ToggleHeight * scale)),
            value, theme);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + Metrics.Space.Sm * scale));
        return next;
    }

    private void DrawSelectedTags(float scale)
    {
        if (selectedTags.Count == 0)
        {
            return;
        }

        if (selectedActive.Length < selectedTags.Count)
        {
            selectedActive = new bool[selectedTags.Count * 2];
            Array.Fill(selectedActive, true);
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var height = ChipRail.RowHeight * scale;
        var row = new Rect(new Vector2(origin.X + pad, origin.Y), new Vector2(origin.X + width - pad, origin.Y + height));
        var labels = CollectionsMarshal.AsSpan(selectedTags);
        var tapped = selectedRail.Draw(row, ui, labels, selectedActive.AsSpan(0, labels.Length));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        if (tapped >= 0)
        {
            ToggleTag(selectedTags[tapped]);
        }
    }

    private void DrawTagSearch(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var rect = new Rect(new Vector2(origin.X + pad, origin.Y),
            new Vector2(origin.X + width - pad, origin.Y + TagSearchHeight * scale));
        SearchField.Draw(rect, "##venueTagSearch", Loc.T(L.Venues.SearchTags), ref tagSearch, AppPalettes.Venues, 40);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, TagSearchHeight * scale + Metrics.Space.Xs * scale));
    }

    private void DrawTagRows(float scale)
    {
        var entries = tagIndex.Entries;
        var query = tagSearch.Trim();
        var searching = query.Length > 0;
        var limit = searching || tagsExpanded ? entries.Count : Math.Min(entries.Count, CollapsedTagCount);
        var drawn = 0;
        for (var index = 0; index < limit; index++)
        {
            var entry = entries[index];
            if (searching && !entry.Tag.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DrawTagRow(entry.Tag, tagCountLabels[index], scale);
            drawn++;
        }

        if (drawn == 0)
        {
            var origin = ImGui.GetCursorScreenPos();
            Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + CellPadX * scale, origin.Y + 8f * scale),
                Loc.T(L.Venues.NoVenues), Ink.MutedInk, TagRowStyle);
            ImGui.Dummy(new Vector2(ScrollLayout.StableContentWidth(), TagRowHeight * scale));
            return;
        }

        if (!searching && !tagsExpanded && entries.Count > CollapsedTagCount)
        {
            DrawShowAllTags(scale);
        }
    }

    private void DrawTagRow(string tag, string count, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, TagRowHeight * scale, Ink.HoverTint);
        var bounds = cell.Bounds;
        var pad = CellPadX * scale;
        var centerY = bounds.Center.Y;
        var selected = IsTagSelected(tag);
        drawList.AddCircleFilled(new Vector2(bounds.Min.X + pad + 5f * scale, centerY), 5f * scale,
            ImGui.GetColorU32(VenueChips.Color(tag)), 16);
        var checkCenter = new Vector2(bounds.Max.X - pad - 10f * scale, centerY);
        PhoneIcon.Draw(drawList, checkCenter, selected ? PhoneIcons.CircleCheckFilled : PhoneIcons.Circle,
            selected ? Ink.Accent : Ink.FaintInk, 20f * scale);
        var countSize = Typography.Measure(count, TagCountStyle);
        var countLeft = checkCenter.X - 20f * scale - countSize.X;
        Typography.Draw(drawList, new Vector2(countLeft, centerY - countSize.Y * 0.5f), count, Ink.MutedInk,
            TagCountStyle);
        var textLeft = bounds.Min.X + pad + 20f * scale;
        var labelHeight = Typography.LineHeight(TagRowStyle);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - labelHeight * 0.5f),
            Typography.FitText(tag, MathF.Max(1f, countLeft - 10f * scale - textLeft), TagRowStyle),
            selected ? Ink.TitleInk : Ink.BodyInk, TagRowStyle);
        FeedCell.End(drawList, cell, Ink.Hairline);
        if (cell.Tapped)
        {
            ToggleTag(tag);
        }
    }

    private void DrawShowAllTags(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, TagRowHeight * scale, Ink.HoverTint);
        var labelHeight = Typography.LineHeight(SeeAllStyle);
        Typography.Draw(drawList,
            new Vector2(cell.Bounds.Min.X + CellPadX * scale, cell.Bounds.Center.Y - labelHeight * 0.5f),
            showAllTagsLabel, Ink.AccentLink, SeeAllStyle);
        PhoneIcon.Draw(drawList, new Vector2(cell.Bounds.Max.X - CellPadX * scale - 10f * scale, cell.Bounds.Center.Y),
            PhoneIcons.ChevronDown, Ink.AccentLink, 16f * scale);
        FeedCell.End(drawList, cell, Ink.Hairline, false);
        if (cell.Tapped)
        {
            tagsExpanded = true;
        }
    }

    private void DrawFilterBar(Rect bar, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        SocialChrome.PaintBarBackdrop(ui, drawList, bar, screenRect);
        FeedCell.Hairline(drawList, bar.Min.X, bar.Max.X, bar.Min.Y + 0.5f, Ink.Hairline);
        var pad = CellPadX * scale;
        var top = bar.Min.Y + (bar.Height - FilterCtaHeight * scale) * 0.5f;
        var button = new Rect(new Vector2(bar.Min.X + pad, top),
            new Vector2(bar.Max.X - pad, top + FilterCtaHeight * scale));
        if (SocialPill.Accent(drawList, button, showVenuesLabel, Ink, TextStyles.Headline, button.Height * 0.5f))
        {
            tagSearch = string.Empty;
            router.Pop();
        }
    }
}
