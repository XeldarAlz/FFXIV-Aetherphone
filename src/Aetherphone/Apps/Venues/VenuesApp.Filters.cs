using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private const float SourceRowHeight = 60f;
    private const float SourceTileSide = 34f;
    private const float ToggleWidth = Metrics.Size.ToggleWidth;
    private const float ToggleHeight = Metrics.Size.ToggleHeight;
    private const float TagRowHeight = 46f;
    private const float TagDot = 5f;
    private const float CheckGlyph = 22f;
    private const float CtaHeight = Button.LargeHeight;
    private const float CtaInset = 10f;
    private const int CollapsedTagCount = 10;
    private const int TagSearchMaxLength = 40;

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
    private readonly NavBarButton[] filterButtons = new NavBarButton[1];
    private readonly List<string> tagCountLabels = new();
    private bool[] selectedActive = Array.Empty<bool>();
    private string tagSearch = string.Empty;
    private string tagQuery = string.Empty;
    private bool tagsExpanded;
    private bool tagLabelsStale = true;
    private string showVenuesLabel = string.Empty;
    private string showAllTagsLabel = string.Empty;

    private void DrawFilters(in PhoneContext context, VenueRoute route)
    {
        var scale = UiScale.Current;
        RefreshTagIndex();
        var navBar = AppHeader.BeginLargeTitle(context);
        var ctaTop = navBar.Body.Max.Y - (CtaHeight + CtaInset * 2f) * scale;
        var body = new Rect(navBar.Body.Min, new Vector2(navBar.Body.Max.X, ctaTop));
        using (UiScope.PushId("venues.filters"))
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = Section(drawList, origin.X, origin.Y - VenuesArt.SectionGap * scale, width,
                Loc.T(L.Venues.Sources), string.Empty, out _, scale);
            cursorY = DrawSourceCard(drawList, origin.X, cursorY, width, scale);
            cursorY = Section(drawList, origin.X, cursorY, width, Loc.T(L.Venues.Content), string.Empty, out _, scale);
            var hideAdult = DrawToggleCard(drawList, "venues.hideAdult", Loc.T(L.Venues.HideAdult),
                Loc.T(L.Venues.HideAdultHint), configuration.VenueHideAdult, origin.X, ref cursorY, width, scale);
            if (hideAdult != configuration.VenueHideAdult)
            {
                configuration.VenueHideAdult = hideAdult;
                configuration.Save();
                visibleCards = PageSize;
            }

            cursorY = Section(drawList, origin.X, cursorY, width, Loc.T(L.Venues.Tags), string.Empty, out _, scale);
            cursorY = DrawSelectedTags(origin.X, cursorY, scale);
            cursorY = DrawTagSearch(drawList, origin.X, cursorY, width, scale);
            cursorY = DrawTagRows(drawList, origin.X, cursorY + Metrics.Space.Sm * scale, width, scale);
            cursorY = Section(drawList, origin.X, cursorY, width, Loc.T(L.Apps.Notifications), string.Empty, out _,
                scale);
            var notify = DrawToggleCard(drawList, "venues.notifyNew", Loc.T(L.Venues.NotifyNew),
                Loc.T(L.Venues.NotifyNewHelp), configuration.VenueNotifyNewEvents, origin.X, ref cursorY, width, scale);
            if (notify != configuration.VenueNotifyNewEvents)
            {
                configuration.VenueNotifyNewEvents = notify;
                configuration.Save();
            }

            VenuesArt.ReserveTo(origin, width, cursorY + VenuesArt.BottomPad * scale);
        }

        DrawFilterCta(new Rect(new Vector2(context.Content.Min.X, ctaTop), navBar.Body.Max), scale);
        var active = FiltersActive || configuration.VenueHideAdult;
        filterButtons[0] = new NavBarButton(PhoneIcons.Refresh, Loc.T(L.Venues.ResetFilters));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "venues.nav.filters", Loc.T(L.Venues.Filters),
            NavBarStyle.From(ui), filterButtons.AsSpan(0, active ? 1 : 0), route.BackTitle, back);
        if (pressed == 0)
        {
            ResetFilters();
        }
    }

    private void RefreshTagIndex()
    {
        var scope = ResolveScope();
        var key = new VenueTagIndexKey(venues.Version, configuration.VenueSourceFilter, scope.DataCenters, scope.World,
            configuration.VenueHideAdult, tagsStamp);
        CheckLanguage();
        if (!tagIndex.Update(key, venues.Events, selectedTags) && !tagLabelsStale)
        {
            return;
        }

        tagLabelsStale = false;

        var culture = Loc.Culture;
        showVenuesLabel = Loc.T(L.Venues.ShowVenues, tagIndex.MatchCount.ToString("N0", culture));
        showAllTagsLabel = Loc.T(L.Venues.ShowAllTags, tagIndex.Entries.Count.ToString("N0", culture));
        tagCountLabels.Clear();
        for (var index = 0; index < tagIndex.Entries.Count; index++)
        {
            tagCountLabels.Add(tagIndex.Entries[index].Count.ToString("N0", culture));
        }
    }

    private float DrawSourceCard(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var rowHeight = SourceRowHeight * scale;
        var card = new Rect(new Vector2(left, top),
            new Vector2(left + width, top + rowHeight * VenueFilter.SourceCount));
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        var inset = CardInset * scale;
        for (var source = 0; source < VenueFilter.SourceCount; source++)
        {
            var row = new Rect(new Vector2(card.Min.X, card.Min.Y + source * rowHeight),
                new Vector2(card.Max.X, card.Min.Y + (source + 1) * rowHeight));
            var hovered = VenuesArt.RowWash(drawList, ui, row, scale);
            var selected = configuration.VenueSourceFilter == source;
            var centerY = row.Center.Y;
            var tileSide = SourceTileSide * scale;
            var tileMin = new Vector2(row.Min.X + inset, centerY - tileSide * 0.5f);
            var tileMax = tileMin + new Vector2(tileSide, tileSide);
            IconTile.FillShaded(drawList, tileMin, tileMax, tileSide * Metrics.Radius.TileFactor,
                IconTile.Surface(SourceTints[source]));
            PhoneIcon.Draw(drawList, (tileMin + tileMax) * 0.5f, SourceGlyphs[source], AccentRing.Ink, 18f * scale);
            var checkCenter = new Vector2(row.Max.X - inset - CheckGlyph * scale * 0.5f, centerY);
            PhoneIcon.Draw(drawList, checkCenter, selected ? PhoneIcons.CircleCheckFilled : PhoneIcons.Circle,
                selected ? ui.Accent : ui.MutedInk, CheckGlyph * scale);
            var textLeft = tileMax.X + VenuesArt.TextGap * scale;
            var textWidth = MathF.Max(1f, checkCenter.X - 20f * scale - textLeft);
            var titleHeight = Typography.LineHeight(RowTitleStyle);
            var helpHeight = Typography.LineHeight(RowHelpStyle);
            var textTop = centerY - (titleHeight + helpHeight) * 0.5f;
            Typography.Draw(drawList, new Vector2(textLeft, textTop),
                Typography.FitText(Loc.T(SourceTitles[source]), textWidth, RowTitleStyle), ui.TitleInk, RowTitleStyle);
            Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
                Typography.FitText(Loc.T(SourceHints[source]), textWidth, RowHelpStyle), ui.MutedInk, RowHelpStyle);
            if (source < VenueFilter.SourceCount - 1)
            {
                FeedCell.Hairline(drawList, textLeft, row.Max.X, row.Max.Y, ui.Hairline);
            }

            if (UiInteract.Click(row.Min, row.Max, hovered) && !selected)
            {
                configuration.VenueSourceFilter = source;
                configuration.Save();
                visibleCards = PageSize;
                UiFeedback.Play(UiSound.Tap);
            }
        }

        return card.Max.Y;
    }

    private bool DrawToggleCard(ImDrawListPtr drawList, string id, string title, string help, bool value, float left,
        ref float cursorY, float width, float scale)
    {
        var inner = CardInset * scale;
        var right = left + width;
        var toggleWidth = ToggleWidth * scale;
        var textLeft = left + inner;
        var textWidth = MathF.Max(1f, right - inner - toggleWidth - VenuesArt.TextGap * scale - textLeft);
        var titleHeight = Typography.LineHeight(RowTitleStyle);
        var helpHeight = Typography.MeasureWrappedBlock(help, RowHelpStyle, textWidth).Y;
        var cardHeight = inner * 2f + titleHeight + VenuesArt.LineGap * scale + helpHeight;
        var top = cursorY;
        ui.Card(drawList, new Vector2(left, top), new Vector2(right, top + cardHeight), Metrics.Radius.Grouped * scale);
        Typography.Draw(drawList, new Vector2(textLeft, top + inner),
            Typography.FitText(title, textWidth, RowTitleStyle), ui.TitleInk, RowTitleStyle);
        Typography.DrawWrappedLeft(new Vector2(textLeft, top + inner + titleHeight + VenuesArt.LineGap * scale), help,
            ui.MutedInk, RowHelpStyle, textWidth);
        var toggleMin = new Vector2(right - inner - toggleWidth, top + (cardHeight - ToggleHeight * scale) * 0.5f);
        var next = Toggle.Draw(id, new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, ToggleHeight * scale)),
            value, theme);
        cursorY = top + cardHeight;
        if (next != value)
        {
            UiFeedback.Play(next ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        return next;
    }

    private float DrawSelectedTags(float left, float top, float scale)
    {
        if (selectedTags.Count == 0)
        {
            return top;
        }

        if (selectedActive.Length < selectedTags.Count)
        {
            selectedActive = new bool[selectedTags.Count * 2];
            Array.Fill(selectedActive, true);
        }

        ImGui.SetCursorScreenPos(new Vector2(left, top));
        var labels = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(selectedTags);
        var tapped = selectedRail.Draw(ui, labels, selectedActive.AsSpan(0, labels.Length));
        if (tapped >= 0)
        {
            ToggleTag(selectedTags[tapped]);
        }

        return top + ChipRail.RowHeight * scale + Metrics.Space.Sm * scale;
    }

    private float DrawTagSearch(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var field = new Rect(new Vector2(left, top), new Vector2(left + width, top + GlassField.HeightUnits * scale));
        SearchBar.Surface(drawList, field, ControlInk.From(theme));
        var before = tagSearch;
        GlassField.Search(drawList, field, "##venueTagSearch", Loc.T(L.Venues.SearchTags), ref tagSearch, theme, scale,
            TagSearchMaxLength, false);
        if (!ReferenceEquals(before, tagSearch))
        {
            tagQuery = tagSearch.Trim();
        }

        return field.Max.Y;
    }

    private float DrawTagRows(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var entries = tagIndex.Entries;
        var searching = tagQuery.Length > 0;
        var limit = searching || tagsExpanded ? entries.Count : Math.Min(entries.Count, CollapsedTagCount);
        var rowHeight = TagRowHeight * scale;
        var shown = 0;
        for (var index = 0; index < limit; index++)
        {
            if (!searching || entries[index].Tag.Contains(tagQuery, StringComparison.OrdinalIgnoreCase))
            {
                shown++;
            }
        }

        var showMore = !searching && !tagsExpanded && entries.Count > CollapsedTagCount;
        var rows = Math.Max(1, shown + (showMore ? 1 : 0));
        var card = new Rect(new Vector2(left, top), new Vector2(left + width, top + rows * rowHeight));
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        if (shown == 0)
        {
            var labelHeight = Typography.LineHeight(TagRowStyle);
            Typography.Draw(drawList, new Vector2(left + CardInset * scale, card.Center.Y - labelHeight * 0.5f),
                Loc.T(L.Venues.NoVenues), ui.MutedInk, TagRowStyle);
            return card.Max.Y;
        }

        var drawn = 0;
        for (var index = 0; index < limit; index++)
        {
            var entry = entries[index];
            if (searching && !entry.Tag.Contains(tagQuery, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var row = new Rect(new Vector2(left, top + drawn * rowHeight),
                new Vector2(left + width, top + (drawn + 1) * rowHeight));
            drawn++;
            if (ImGui.IsRectVisible(row.Min, row.Max))
            {
                DrawTagRow(drawList, row, entry.Tag, tagCountLabels[index], drawn < rows, scale);
            }
        }

        if (showMore)
        {
            var row = new Rect(new Vector2(left, top + drawn * rowHeight), card.Max);
            var hovered = VenuesArt.RowWash(drawList, ui, row, scale);
            var labelHeight = Typography.LineHeight(TextStyles.Body);
            Typography.Draw(drawList, new Vector2(row.Min.X + CardInset * scale, row.Center.Y - labelHeight * 0.5f),
                showAllTagsLabel, ui.Accent, TextStyles.Body);
            PhoneIcon.Draw(drawList, new Vector2(row.Max.X - CardInset * scale - 8f * scale, row.Center.Y),
                PhoneIcons.ChevronDown, ui.Accent, 16f * scale);
            if (UiInteract.Click(row.Min, row.Max, hovered))
            {
                tagsExpanded = true;
                UiFeedback.Play(UiSound.Tap);
            }
        }

        return card.Max.Y;
    }

    private void DrawTagRow(ImDrawListPtr drawList, Rect row, string tag, string count, bool divider, float scale)
    {
        var hovered = VenuesArt.RowWash(drawList, ui, row, scale);
        var inset = CardInset * scale;
        var centerY = row.Center.Y;
        var selected = IsTagSelected(tag);
        drawList.AddCircleFilled(new Vector2(row.Min.X + inset + TagDot * scale, centerY), TagDot * scale,
            ImGui.GetColorU32(VenueChips.Color(tag)), 16);
        var checkCenter = new Vector2(row.Max.X - inset - CheckGlyph * scale * 0.5f, centerY);
        PhoneIcon.Draw(drawList, checkCenter, selected ? PhoneIcons.CircleCheckFilled : PhoneIcons.Circle,
            selected ? ui.Accent : ui.MutedInk, (CheckGlyph - 2f) * scale);
        var countSize = Typography.Measure(count, TagCountStyle);
        var countLeft = checkCenter.X - 20f * scale - countSize.X;
        Typography.Draw(drawList, new Vector2(countLeft, centerY - countSize.Y * 0.5f), count, ui.MutedInk,
            TagCountStyle);
        var textLeft = row.Min.X + inset + (TagDot * 2f + 10f) * scale;
        var labelHeight = Typography.LineHeight(TagRowStyle);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - labelHeight * 0.5f),
            Typography.FitText(tag, MathF.Max(1f, countLeft - 10f * scale - textLeft), TagRowStyle),
            selected ? ui.TitleInk : ui.BodyInk, TagRowStyle);
        if (divider)
        {
            FeedCell.Hairline(drawList, textLeft, row.Max.X, row.Max.Y, ui.Hairline);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            ToggleTag(tag);
        }
    }

    private void DrawFilterCta(Rect bar, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var inset = CtaInset * scale;
        var button = new Rect(new Vector2(bar.Min.X, bar.Min.Y + inset),
            new Vector2(bar.Max.X, bar.Min.Y + inset + CtaHeight * scale));
        Elevation.Floating(drawList, button.Min, button.Max, button.Height * 0.5f, scale, 0.5f);
        if (VenuesArt.Action(drawList, ui, button, ImGui.GetID("venues.filters.show"), showVenuesLabel,
                string.Empty, false, ButtonStyle.Prominent))
        {
            tagSearch = string.Empty;
            tagQuery = string.Empty;
            router.Pop();
        }
    }
}
