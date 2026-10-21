using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal enum GlamListKind : byte
{
    Item,
    Saved,
    Mod,
}

internal sealed partial class PrismApp
{
    private const float ProfileNameUnits = 30f;

    private bool showSaved;
    private GlamListKind listKind;
    private uint listItemId;
    private string listTitle = string.Empty;
    private CachedText glamCountText;
    private CachedText triedCountText;
    private CachedText likeCountText;
    private CachedText listCountText;

    private void OpenOwnProfile()
    {
        if (!glams.IsSignedIn || store.CurrentUser is not { } me)
        {
            return;
        }

        showSaved = false;
        glams.OpenProfile(new GlamAuthorDto(me.Id, me.DisplayName, me.Handle, me.AvatarUrl ?? string.Empty));
    }

    private void OpenProfile(GlamAuthorDto author)
    {
        if (author.Id == glams.MyId)
        {
            OpenTab(PrismTab.Me);
            return;
        }

        glams.OpenProfile(author);
        Push(PrismScreen.Profile);
    }

    private void OpenItemGlams(uint itemId)
    {
        listKind = GlamListKind.Item;
        listItemId = itemId;
        listTitle = gameData.TryGetItem(itemId, out var name, out _, out _) ? name : string.Empty;
        glams.OpenItem(itemId);
        Push(PrismScreen.GlamList);
    }

    private void OpenModGlams(GlamModDto mod)
    {
        listKind = GlamListKind.Mod;
        listTitle = mod.Name;
        glams.OpenMod(mod.Name, mod.Author);
        Push(PrismScreen.GlamList);
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
                if (saved)
                {
                    glams.OpenCollection(GlamClient.SavedCollection);
                }
                else
                {
                    OpenOwnProfile();
                }
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
            if (PrismArt.Link(drawList, new Vector2(left + width - PrismType.Measure(Loc.T(L.Prism.TryOnGlam),
                    PrismFace.Label, PrismArt.LabelPixels()).X, top), Loc.T(L.Prism.TryOnGlam), PrismArt.Muted, scale))
            {
                glamour.TryOnPiece(new GlamGearDto(ItemId: listItemId));
            }
        }

        top += PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels()) + Metrics.Space.Lg * scale;
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
