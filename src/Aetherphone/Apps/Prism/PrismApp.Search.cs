using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int SearchLimit = 12;
    private const int SearchScan = 400;
    private const int QueryMaxLength = 60;
    private const int TagTiles = 3;
    private const int SearchScrollId = 1;
    private const float CancelShare = 0.4f;
    private const double SearchFetchSeconds = 0.35;
    private const double TagRetrySeconds = 5;
    private const int PeopleLimit = 3;

    private readonly List<MarketItemRef> searchHits = new();
    private readonly List<MarketItemRef> searchFound = new();
    private string discoverQuery = string.Empty;
    private string searchedFor = string.Empty;
    private string searchTag = string.Empty;
    private string searchPeople = string.Empty;
    private string searchTagKey = string.Empty;
    private string searchTagLabel = string.Empty;
    private string searchTagTitle = string.Empty;
    private string searchLanguage = string.Empty;
    private string tagGlamsFor = string.Empty;
    private GlamDto[] tagGlams = Array.Empty<GlamDto>();
    private double searchChangedAt;
    private double tagRetryAt;
    private bool searchItems;
    private bool searchIndexed;
    private bool searchOpen;
    private bool searchFocus;

    private void OpenSearch()
    {
        searchOpen = true;
        searchFocus = true;
    }

    private void DrawSearch(ImDrawListPtr drawList, Rect content, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var top = content.Min.Y + Metrics.Space.Xs * scale;
        var cancel = Loc.T(L.Common.Cancel);
        var right = content.Max.X - Metrics.Space.Sm * scale;
        var cancelRect = new Rect(new Vector2(right - ButtonWidth([cancel], content.Width * CancelShare, scale), top),
            new Vector2(right, top + tap));
        if (DrawButton(drawList, cancelRect, cancel, ButtonStyle.Plain, scale))
        {
            discoverQuery = string.Empty;
            searchOpen = false;
            return;
        }

        var field = new Rect(new Vector2(content.Min.X + Metrics.Space.Lg * scale, top),
            new Vector2(cancelRect.Min.X - Metrics.Space.Sm * scale, top + tap));
        SearchBar.Surface(drawList, field, controls);
        var inset = Metrics.Space.Md * scale;
        var glyph = PrismArt.RowIconUnits * scale;
        PhoneIcon.Draw(drawList, new Vector2(field.Min.X + inset + glyph * 0.5f, field.Center.Y), PhoneIcons.Search,
            PrismArt.Muted, glyph);
        if (searchFocus)
        {
            ImGui.SetKeyboardFocusHere();
            searchFocus = false;
        }

        GlassField.Text(field, "##glamSearch", Loc.T(L.Prism.SearchHint), ref discoverQuery, PrismArt.Fore,
            PrismArt.Muted, inset + glyph + Metrics.Space.Sm * scale, QueryMaxLength);
        UpdateSearch();
        UpdateTagGlams();
        UpdatePeople();
        using var id = UiScope.PushId(SearchScrollId);
        DrawSearchBody(new Rect(new Vector2(content.Min.X, field.Max.Y + Metrics.Space.Xs * scale), content.Max),
            scale);
    }

    private void UpdateSearch()
    {
        if (discoverQuery == searchedFor && searchIndexed == marketIndex.Ready &&
            ReferenceEquals(searchLanguage, Loc.Current.Code))
        {
            return;
        }

        if (discoverQuery != searchedFor)
        {
            searchChangedAt = ImGui.GetTime();
        }

        searchedFor = discoverQuery;
        searchIndexed = marketIndex.Ready;
        searchLanguage = Loc.Current.Code;
        searchFound.Clear();
        var query = discoverQuery.Trim();
        var person = query.Length > 0 && query[0] == '@';
        searchItems = query.Length > 0 && query[0] != '#' && !person;
        searchPeople = query.Length > 0 && query[0] != '#' ? query.TrimStart('@').Trim() : string.Empty;
        var word = query.TrimStart('#').Trim().ToLowerInvariant();
        searchTag = !person && word.Length > 0 && (query[0] == '#' || word.IndexOf(' ') < 0) ? word : string.Empty;
        searchTagKey = searchTag.Length > 0 ? GlamStore.TagKey(searchTag) : string.Empty;
        searchTagLabel = searchTag.Length > 0 ? "#" + searchTag : string.Empty;
        searchTagTitle = searchTag.Length > 0 ? Loc.T(L.Prism.GlamsTagged, searchTagLabel) : string.Empty;
        if (!searchItems)
        {
            return;
        }

        marketIndex.EnsureBuilt();
        marketIndex.Search(query, searchHits, SearchScan);
        for (var index = 0; index < searchHits.Count && searchFound.Count < SearchLimit; index++)
        {
            var hit = searchHits[index];
            if (gameData.EquipSlot(hit.Id) == GearSlots.None)
            {
                continue;
            }

            searchFound.Add(hit);
        }
    }

    private void UpdateTagGlams()
    {
        if (searchTag.Length == 0 || tagGlamsFor == searchTag)
        {
            return;
        }

        var now = ImGui.GetTime();
        if (glams.ListKey == searchTagKey)
        {
            if (glams.List.Loading)
            {
                return;
            }

            if (!glams.List.Failed)
            {
                tagGlams = glams.List.Items;
                tagGlamsFor = searchTag;
                return;
            }

            if (now < tagRetryAt)
            {
                return;
            }

            tagRetryAt = now + TagRetrySeconds;
            glams.OpenTag(searchTag);
            return;
        }

        if (now - searchChangedAt >= SearchFetchSeconds)
        {
            tagRetryAt = now + TagRetrySeconds;
            glams.OpenTag(searchTag);
        }
    }

    private void UpdatePeople()
    {
        if (searchPeople.Length > 0 && glams.PeopleAsked != searchPeople &&
            ImGui.GetTime() - searchChangedAt >= SearchFetchSeconds)
        {
            glams.SearchPeople(searchPeople);
        }
    }

    private void DrawSearchBody(Rect body, float scale)
    {
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = Metrics.Space.Lg * scale;
        var icon = PieceTileUnits * scale;
        var textLeft = origin.X + inset + icon + Metrics.Space.Md * scale;
        var top = origin.Y;
        if (searchPeople.Length > 0)
        {
            top = DrawPeople(list, origin.X, top, width, textLeft, scale);
        }

        if (searchTag.Length > 0)
        {
            if (searchPeople.Length > 0)
            {
                top = DrawSearchCaption(list, origin.X + inset, top + Metrics.Space.Md * scale, Loc.T(L.Prism.TagsLabel));
            }

            var row = SearchRow(origin.X, top, width, scale);
            if (DrawSearchRow(list, row, textLeft, searchTagLabel, Loc.T(L.Prism.SeeTagGlams), scale))
            {
                OpenTagGlams(searchTag);
            }

            var tile = new Rect(new Vector2(origin.X + inset, row.Center.Y - icon * 0.5f),
                new Vector2(origin.X + inset + icon, row.Center.Y + icon * 0.5f));
            Squircle.Fill(list, tile.Min, tile.Max, Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(Surfaces.Fill(PrismArt.Fore, FillLevel.Tertiary)));
            PhoneIcon.Draw(list, tile.Center, PhoneIcons.Hash, PrismArt.Fore, PrismArt.RowIconUnits * scale);
            top = DrawTagStrip(list, origin.X, row.Max.Y, width, scale);
        }

        if (searchFound.Count > 0)
        {
            top = DrawSearchCaption(list, origin.X + inset, top + Metrics.Space.Md * scale, Loc.T(L.Prism.GearHeading));
        }

        for (var index = 0; index < searchFound.Count; index++)
        {
            var item = searchFound[index];
            var row = SearchRow(origin.X, top, width, scale);
            top = row.Max.Y;
            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            if (DrawSearchRow(list, row, textLeft, item.Name, item.Category, scale))
            {
                OpenItemGlams(item.Id);
            }

            var iconTop = row.Center.Y - icon * 0.5f;
            GameIconTile.Draw(list, textures, item.IconId, new Vector2(origin.X + inset, iconTop),
                new Vector2(origin.X + inset + icon, iconTop + icon), 0f, scale);
        }

        if (searchItems && searchFound.Count == 0 && (searchTag.Length == 0 || !marketIndex.Ready))
        {
            top = DrawSearchCaption(list, origin.X + inset, top + Metrics.Space.Md * scale,
                Loc.T(marketIndex.Ready ? L.Prism.NoMatches : L.Common.Loading));
        }

        ImGui.Dummy(new Vector2(width, top - origin.Y + PrismArt.BottomMargin(scale)));
    }

    private float DrawPeople(ImDrawListPtr drawList, float left, float top, float width, float textLeft, float scale)
    {
        var inset = Metrics.Space.Lg * scale;
        top = DrawSearchCaption(drawList, left + inset, top + Metrics.Space.Md * scale, Loc.T(L.Prism.PeopleHeading));
        var current = glams.PeopleFor == searchPeople;
        var failed = glams.PeopleFailed && glams.PeopleAsked == searchPeople;
        var people = current || (!failed && glams.PeopleFor.Length > 0) ? glams.People : Array.Empty<GlamAuthorDto>();
        if (people.Length == 0)
        {
            return DrawPeopleState(drawList, left + inset, top, failed, current, scale);
        }

        var radius = PieceTileUnits * scale * 0.5f;
        var shown = searchItems ? Math.Min(people.Length, PeopleLimit) : people.Length;
        for (var index = 0; index < shown; index++)
        {
            var person = people[index];
            var row = SearchRow(left, top, width, scale);
            top = row.Max.Y;
            if (!ImGui.IsRectVisible(row.Min, row.Max))
            {
                continue;
            }

            var name = AuthorName(person);
            if (DrawSearchRow(drawList, row, textLeft, name, AuthorLine(person), scale))
            {
                OpenProfile(person);
            }

            DrawAuthorAvatar(drawList, new Vector2(left + inset + radius, row.Center.Y), radius, name, person.AvatarUrl,
                TextStyles.Callout.Scale);
        }

        return top;
    }

    private float DrawPeopleState(ImDrawListPtr drawList, float left, float top, bool failed, bool current,
        float scale)
    {
        var line = Typography.LineHeight(PrismType.Caption);
        var tap = Metrics.Size.TapTarget * scale;
        var textTop = top + (tap - line) * 0.5f;
        var text = Loc.T(failed ? L.Common.LoadFailed : current ? L.Prism.NoPeople : L.Common.Loading);
        Typography.Draw(drawList, new Vector2(left, textTop), text, PrismArt.Muted, PrismType.Caption);
        if (failed && PrismArt.Link(drawList,
                new Vector2(left + Typography.Measure(text, PrismType.Caption).X + Metrics.Space.Sm * scale, textTop),
                Loc.T(L.Common.Retry), PrismArt.Fore, scale))
        {
            glams.SearchPeople(searchPeople, true);
        }

        return top + tap;
    }

    private static Rect SearchRow(float left, float top, float width, float scale) =>
        new(new Vector2(left, top), new Vector2(left + width, top + GlamRowUnits * scale));

    private static float DrawSearchCaption(ImDrawListPtr drawList, float left, float top, string text)
    {
        Typography.Draw(drawList, new Vector2(left, top), text, PrismArt.Muted, PrismType.Caption);
        return top + Typography.LineHeight(PrismType.Caption);
    }

    private bool DrawSearchRow(ImDrawListPtr drawList, Rect row, float textLeft, string title, string caption,
        float scale)
    {
        var hovered = HoverWash(drawList, row);
        DrawRowText(drawList, textLeft, row.Max.X - Metrics.Space.Lg * scale, row.Center.Y, title, null, caption, scale);
        FeedCell.Hairline(drawList, textLeft, row.Max.X, row.Max.Y, PrismArt.Faint);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private float DrawTagStrip(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        if (tagGlamsFor != searchTag || tagGlams.Length == 0)
        {
            return top;
        }

        top = DrawSearchCaption(drawList, left + Metrics.Space.Lg * scale, top + Metrics.Space.Md * scale,
            searchTagTitle) + Metrics.Space.Xs * scale;
        var gap = Metrics.Space.Xxs * scale;
        var tile = (width - gap * (TagTiles + 1)) / TagTiles;
        var height = tile / GlamStore.PhotoRatio;
        for (var index = 0; index < TagTiles && index < tagGlams.Length; index++)
        {
            var x = left + gap + index * (tile + gap);
            var photo = new Rect(new Vector2(x, top), new Vector2(x + tile, top + height));
            DrawLook(drawList, photo, Cover(tagGlams[index]), false);
            if (UiInteract.Click(photo.Min, photo.Max, HoverWash(drawList, photo)))
            {
                OpenGlam(tagGlams[index]);
            }
        }

        return top + height;
    }
}
