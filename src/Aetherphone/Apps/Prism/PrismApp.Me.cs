using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal enum GlamListKind : byte
{
    Item,
    Mod,
}

internal sealed partial class PrismApp
{
    private const float ProfileNameUnits = 30f;
    private const string MarketAppId = "market";

    private bool showSaved;
    private GlamListKind listKind;
    private uint listItemId;
    private GlamModDto? listMod;
    private string listTitle = string.Empty;
    private CachedText glamCountText;
    private CachedText triedCountText;
    private CachedText likeCountText;
    private CachedText listCountText;
    private CachedText vendorText;
    private CachedText seasonText;
    private bool listOwned;

    private void OpenOwnProfile()
    {
        if (!glams.IsSignedIn || store.CurrentUser is not { } me)
        {
            return;
        }

        glams.OpenProfile(new GlamAuthorDto(me.Id, me.DisplayName, me.Handle, me.AvatarUrl ?? string.Empty), showSaved);
    }

    private void OpenProfile(GlamAuthorDto author)
    {
        if (author.Id == glams.MyId)
        {
            OpenTab(PrismTab.Me);
            return;
        }

        Push(PrismScreen.Profile);
        showSaved = false;
        glams.OpenProfile(author);
    }

    private void OpenItemGlams(uint itemId)
    {
        Push(PrismScreen.GlamList);
        listKind = GlamListKind.Item;
        listItemId = itemId;
        listMod = null;
        listTitle = GearItem(itemId, out var name, out _) ? name : string.Empty;
        OpenList();
    }

    private void OpenModGlams(GlamModDto mod)
    {
        Push(PrismScreen.GlamList);
        listKind = GlamListKind.Mod;
        listItemId = 0;
        listMod = mod;
        listTitle = mod.Name;
        OpenList();
    }

    private void OpenList()
    {
        if (listKind == GlamListKind.Item)
        {
            listOwned = OwnedItems.Owns(listItemId);
            marketIndex.EnsureBuilt();
            glams.OpenItem(listItemId);
        }
        else if (listMod is { } mod)
        {
            glams.OpenMod(mod.Name, mod.Author);
        }
    }

    private void DrawProfile(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (!glams.IsSignedIn)
        {
            DrawSignedOut(drawList, content, scale);
            return;
        }

        var mine = screen == PrismScreen.Me;
        var profile = glams.Profile;
        var author = profile?.User;
        var gutter = PrismArt.Gutter(scale);
        var left = content.Min.X + gutter;
        var right = content.Max.X - gutter;
        var top = mine
            ? content.Min.Y + HeaderGapUnits * scale
            : DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var name = author is null ? string.Empty : author.DisplayName.Length > 0 ? author.DisplayName : author.Handle;
        var pixels = PrismType.Fit(name, PrismFace.Headline, right - left, ProfileNameUnits * scale,
            ProfileNameUnits * scale * 0.7f, 1, out var lines);
        top += PrismType.DrawLines(drawList, new Vector2(left, top), lines, PrismArt.Fore, PrismFace.Headline, pixels);
        var labelPixels = PrismArt.LabelPixels();
        PrismType.Draw(drawList, new Vector2(left, top), AuthorLine(author), PrismArt.Muted, PrismFace.Label, labelPixels);
        var action = Loc.T(mine ? L.Prism.PostGlam : profile is { Following: true } ? L.Prism.Following : L.Prism.Follow);
        if (PrismArt.Button(drawList, new Vector2(right - PrismArt.ButtonWidth(action, scale), top), action, scale, out _))
        {
            if (mine)
            {
                StartCompose();
            }
            else if (author is not null)
            {
                glams.SetFollow(author, profile is not { Following: true });
            }
        }

        top += PrismArt.ButtonUnits * scale + Metrics.Space.Lg * scale;
        top = DrawProfileStats(drawList, left, top, right - left, profile, mine, scale);
        if (mine)
        {
            top = DrawSeasonLine(drawList, left, top, scale);
            top = DrawProfileToggle(drawList, left, top, scale);
        }

        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = AppSurface.BeginEdgeToEdge(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var cursor = DrawPlacings(list, origin.X + gutter, origin.Y, width, profile, scale);
        cursor = DrawLaneGrid(list, body, origin.X + gutter, cursor, width, glams.List,
            showSaved ? L.Prism.SavedEmptyHint : L.Prism.ProfileEmptyHint, scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y));
        if (InfiniteScroll.ReachedBottom())
        {
            glams.LoadMoreList();
        }
    }

    private float DrawProfileStats(ImDrawListPtr drawList, float left, float top, float width, GlamProfileDto? profile,
        bool mine, float scale)
    {
        var column = (width - Metrics.Space.Lg * scale * 2f) / 3f;
        var step = column + Metrics.Space.Lg * scale;
        DrawStat(drawList, new Vector2(left, top), column, Loc.T(L.Prism.StatGlams),
            Count(ref glamCountText, profile?.GlamCount ?? 0), scale);
        DrawStat(drawList, new Vector2(left + step, top), column, Loc.T(L.Prism.StatTriedOn),
            Count(ref triedCountText, profile?.TryOnsReceived ?? 0), scale);
        if (mine)
        {
            DrawStat(drawList, new Vector2(left + step * 2f, top), column, Loc.T(L.Prism.StatLikes),
                Count(ref likeCountText, profile?.LikesReceived ?? 0), scale);
        }

        var labelLine = PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels());
        return top + Metrics.Space.Md * scale + labelLine + PrismType.LineHeight(PrismFace.Headline, StatUnits * scale) +
               Metrics.Space.Xl * scale;
    }

    private static string Count(ref CachedText cache, long value)
    {
        var key = HashCode.Combine(value, Loc.Current.Code);
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, value.ToString("N0", Loc.Culture));
    }

    private float DrawSeasonLine(ImDrawListPtr drawList, float left, float top, float scale)
    {
        if (glams.Season is not { Me: { Rank: > 0 } standing } season)
        {
            return top;
        }

        var key = HashCode.Combine(season.Name, standing.Rank, standing.Points, Loc.Current.Code);
        var line = seasonText.IsCurrent(key)
            ? seasonText.Value
            : seasonText.Store(key, Loc.T(L.Prism.SeasonStanding, season.Name, standing.Rank,
                standing.Points.ToString("N0", Loc.Culture)));
        var labelPixels = PrismArt.LabelPixels();
        PrismType.Draw(drawList, new Vector2(left, top), line, PrismArt.Fore, PrismFace.Label, labelPixels);
        return top + PrismType.LineHeight(PrismFace.Label, labelPixels) + Metrics.Space.Lg * scale;
    }

    private float DrawProfileToggle(ImDrawListPtr drawList, float left, float top, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var x = left;
        for (var index = 0; index < 2; index++)
        {
            var saved = index == 1;
            var label = Loc.T(saved ? L.Prism.Saved : L.Prism.StatGlams);
            var width = PrismType.Measure(label, PrismFace.Label, labelPixels).X;
            var active = showSaved == saved;
            if (PrismArt.Link(drawList, new Vector2(x, top), label, active ? PrismArt.Fore : PrismArt.Muted, scale) &&
                !active)
            {
                showSaved = saved;
                OpenOwnProfile();
            }

            if (active)
            {
                PrismArt.Hairline(drawList, x, x + width, top + labelLine, PrismArt.Fore, scale);
            }

            x += width + Metrics.Space.Lg * scale;
        }

        return top + labelLine + Metrics.Space.Lg * scale;
    }

    private float DrawPlacings(ImDrawListPtr drawList, float left, float top, float width, GlamProfileDto? profile,
        float scale)
    {
        if (showSaved || profile?.Placings is not { Length: > 0 } placings)
        {
            return top;
        }

        top = DrawSectionLabel(drawList, left, top, width, 0, Loc.T(L.Prism.PlacingsHeading), scale);
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        for (var index = 0; index < placings.Length; index++)
        {
            var placing = placings[index];
            PrismType.Draw(drawList, new Vector2(left, top), Plain[Math.Clamp(placing.Rank, 0, Plain.Length - 1)],
                PrismArt.Fore, PrismFace.Label, labelPixels);
            PrismType.Draw(drawList, new Vector2(left + RankColumnUnits * scale, top),
                PrismType.FitLabel(placing.Title, labelPixels, width - RankColumnUnits * scale), PrismArt.Fore,
                PrismFace.Label, labelPixels);
            top += labelLine + Metrics.Space.Sm * scale;
        }

        return top + Metrics.Space.Lg * scale;
    }

    private float DrawItemSources(ImDrawListPtr drawList, float left, float top, float scale)
    {
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var start = top;
        if (listOwned)
        {
            PrismType.Draw(drawList, new Vector2(left, top), Loc.T(L.Prism.YouOwnThis), PrismArt.Fore, PrismFace.Label,
                labelPixels);
            top += labelLine;
        }

        if (marketIndex.TryGet(listItemId, out var market))
        {
            if (market.VendorPrice > 0)
            {
                var key = HashCode.Combine(market.VendorPrice, Loc.Current.Code);
                var price = vendorText.IsCurrent(key)
                    ? vendorText.Value
                    : vendorText.Store(key, Loc.T(L.Prism.VendorPrice, market.VendorPrice.ToString("N0", Loc.Culture)));
                PrismType.Draw(drawList, new Vector2(left, top), price, PrismArt.Muted, PrismFace.Label, labelPixels);
                top += labelLine;
            }

            if (navigation.IsAvailable(MarketAppId))
            {
                top += Metrics.Space.Sm * scale;
                if (PrismArt.Link(drawList, new Vector2(left, top), Loc.T(L.Prism.CheckMarket), PrismArt.Fore, scale))
                {
                    marketLauncher.RequestItem(listItemId);
                    navigation.Open(MarketAppId);
                }

                top += labelLine + Metrics.Space.Md * scale;
            }
        }

        return top > start ? top + Metrics.Space.Lg * scale : top;
    }

    private void DrawGlamList(ImDrawListPtr drawList, Rect content, float scale)
    {
        var top = DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var gutter = PrismArt.Gutter(scale);
        var left = content.Min.X + gutter;
        var width = content.Width - gutter * 2f;
        var pixels = PrismType.Fit(listTitle, PrismFace.Headline, width, DetailTitleUnits * scale,
            DetailTitleMinUnits * scale, 2, out var lines);
        top += PrismType.DrawLines(drawList, new Vector2(left, top), lines, PrismArt.Fore, PrismFace.Headline, pixels);
        if (listKind == GlamListKind.Item && glams.Item is { } item && item.ItemId == listItemId)
        {
            var key = HashCode.Combine(item.GlamCount, Loc.Current.Code);
            var count = listCountText.IsCurrent(key)
                ? listCountText.Value
                : listCountText.Store(key, Loc.Plural(L.Prism.WornInGlams, item.GlamCount));
            PrismType.Draw(drawList, new Vector2(left, top), count, PrismArt.Muted, PrismFace.Label, PrismArt.LabelPixels());
        }

        if (listKind == GlamListKind.Item && PrismArt.Link(drawList, new Vector2(left + width -
                PrismType.Measure(Loc.T(L.Prism.TryOnGlam), PrismFace.Label, PrismArt.LabelPixels()).X, top),
                Loc.T(L.Prism.TryOnGlam), PrismArt.Muted, scale))
        {
            glamour.TryOnPiece(new GlamGearDto(gameData.EquipSlot(listItemId), listItemId));
            StartWearing(string.Empty, listTitle);
        }

        top += PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels()) + Metrics.Space.Lg * scale;
        if (listKind == GlamListKind.Item)
        {
            top = DrawItemSources(drawList, left, top, scale);
        }
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = AppSurface.BeginEdgeToEdge(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var scrollWidth = ScrollLayout.StableContentWidth() - gutter * 2f;
        var cursor = DrawLaneGrid(list, body, origin.X + gutter, origin.Y, scrollWidth, glams.List,
            L.Prism.DiscoverEmptyHint, scale);
        ImGui.Dummy(new Vector2(scrollWidth, cursor - origin.Y));
        if (InfiniteScroll.ReachedBottom())
        {
            glams.LoadMoreList();
        }
    }
}
