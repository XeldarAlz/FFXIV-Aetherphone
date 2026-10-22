using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal enum GlamListKind : byte
{
    Item,
    Mod,
    Tag,
}

internal sealed partial class PrismApp
{
    private const float ProfileNameUnits = PrismType.TitleUnits;
    private const float ProfileAvatarUnits = 64f;
    private const float PlacingMedalShare = 0.75f;
    private const long CompactFrom = 10000;
    private const string MarketAppId = "market";
    private const double UserRetrySeconds = 5d;

    private double nextUserRequest;
    private bool showSaved;
    private GlamListKind listKind;
    private uint listItemId;
    private GlamModDto? listMod;
    private string listTag = string.Empty;
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
        if (!glams.IsSignedIn)
        {
            return;
        }

        if (store.CurrentUser is not { } me)
        {
            RequestUser();
            return;
        }

        glams.OpenProfile(new GlamAuthorDto(me.Id, me.DisplayName, me.Handle, me.AvatarUrl ?? string.Empty), showSaved);
    }

    private void RequestUser()
    {
        var now = ImGui.GetTime();
        if (now < nextUserRequest)
        {
            return;
        }

        nextUserRequest = now + UserRetrySeconds;
        account.EnsureCurrentUser();
    }

    private bool ShowsOwnProfile() => glams.MyId.Length > 0 && glams.Profile?.User?.Id == glams.MyId;

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

    private void OpenTagGlams(string tag)
    {
        Push(PrismScreen.GlamList);
        listKind = GlamListKind.Tag;
        listItemId = 0;
        listMod = null;
        listTag = tag;
        listTitle = TagLabel(tag);
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
        else if (listKind == GlamListKind.Tag)
        {
            glams.OpenTag(listTag);
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
        if (mine && !ShowsOwnProfile())
        {
            if (glams.MyId.Length == 0)
            {
                RequestUser();
                DrawMasthead(drawList, content, Loc.T(L.Prism.TabMe), string.Empty, string.Empty, scale, out _);
                DrawCenteredLabel(drawList, content, Loc.T(L.Common.Loading));
                return;
            }

            OpenOwnProfile();
        }

        var profile = glams.Profile;
        var author = profile?.User;
        var gutter = PrismArt.Gutter(scale);
        var left = content.Min.X + gutter;
        var right = content.Max.X - gutter;
        var top = mine
            ? DrawMasthead(drawList, content, Loc.T(L.Prism.TabMe), string.Empty, string.Empty, scale, out _)
            : DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var avatar = ProfileAvatarUnits * scale;
        var textLeft = left + avatar + Metrics.Space.Md * scale;
        var nameWidth = right - textLeft;
        var name = AuthorName(author);
        var pixels = PrismType.Fit(name, PrismFace.Headline, nameWidth, ProfileNameUnits * scale,
            ProfileNameUnits * scale * 0.7f, 1, out var lines);
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var block = lines.Length * PrismType.LineHeight(PrismFace.Headline, pixels) + labelLine;
        var rowHeight = MathF.Max(avatar, block);
        var middle = top + rowHeight * 0.5f;
        DrawAuthorAvatar(drawList, new Vector2(left + avatar * 0.5f, middle), avatar * 0.5f, name, author?.AvatarUrl,
            TextStyles.Title3.Scale);
        var textTop = middle - block * 0.5f;
        textTop += PrismType.DrawLines(drawList, new Vector2(textLeft, textTop), lines, PrismArt.Fore, PrismFace.Headline,
            pixels);
        PrismType.Draw(drawList, new Vector2(textLeft, textTop),
            PrismType.FitLabel(AuthorLine(author), labelPixels, nameWidth), PrismArt.Muted, PrismFace.Label, labelPixels);
        top += rowHeight + Metrics.Space.Xl * scale;
        top = DrawProfileStats(drawList, left, top, right - left, profile, scale);
        if (!mine && author is not null)
        {
            var following = profile is { Following: true };
            var strip = new Rect(new Vector2(left, top), new Vector2(right, top + PrismArt.StripUnits * scale));
            if (PrismArt.Strip(drawList, strip, Loc.T(following ? L.Prism.Following : L.Prism.Follow), string.Empty,
                    !following, true, following ? UiSound.ToggleOff : UiSound.ToggleOn))
            {
                glams.SetFollow(author, !following);
            }

            top = strip.Max.Y + Metrics.Space.Xl * scale;
        }

        if (mine)
        {
            top = DrawSeasonLine(drawList, left, top, right - left, scale);
        }

        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var cursor = DrawPlacings(list, origin.X + gutter, origin.Y, width, profile, scale);
        if (mine)
        {
            cursor = DrawProfileToggle(list, origin.X + gutter, cursor, scale);
        }
        else if (cursor > origin.Y)
        {
            cursor = DrawSectionLabel(list, origin.X + gutter, cursor, width, 1, Loc.T(L.Prism.StatGlams), scale);
        }

        cursor = DrawLaneGrid(list, origin.X + gutter, cursor, width, glams.List,
            showSaved ? L.Prism.SavedEmptyHint : L.Prism.ProfileEmptyHint, scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y));
        if (InfiniteScroll.ReachedBottom())
        {
            glams.LoadMoreList();
        }
    }

    private float DrawProfileStats(ImDrawListPtr drawList, float left, float top, float width, GlamProfileDto? profile,
        float scale)
    {
        var column = (width - Metrics.Space.Lg * scale * 2f) / 3f;
        var step = column + Metrics.Space.Lg * scale;
        DrawStat(drawList, new Vector2(left, top), column, Loc.T(L.Prism.StatGlams),
            Count(ref glamCountText, profile?.GlamCount ?? 0), scale);
        DrawStat(drawList, new Vector2(left + step, top), column, Loc.T(L.Prism.StatTriedOn),
            Count(ref triedCountText, profile?.TryOnsReceived ?? 0), scale);
        DrawStat(drawList, new Vector2(left + step * 2f, top), column, Loc.T(L.Prism.StatLikes),
            Count(ref likeCountText, profile?.LikesReceived ?? 0), scale);

        var labelLine = PrismType.LineHeight(PrismFace.Label, PrismArt.LabelPixels());
        return top + Metrics.Space.Md * scale + labelLine + PrismType.LineHeight(PrismFace.Headline, StatUnits * scale) +
               Metrics.Space.Xl * scale;
    }

    private static string Count(ref CachedText cache, long value)
    {
        var key = HashCode.Combine(value, Loc.Current.Code);
        return cache.IsCurrent(key)
            ? cache.Value
            : cache.Store(key, value >= CompactFrom ? NumberText.Compact(value) : value.ToString("N0", Loc.Culture));
    }

    private float DrawSeasonLine(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        if (glams.Season is not { Me: { Rank: > 0 } standing } season)
        {
            return top;
        }

        var key = HashCode.Combine(season.Name, standing.Rank, standing.Points, Loc.Current.Code);
        var line = seasonText.IsCurrent(key)
            ? seasonText.Value
            : seasonText.Store(key, Loc.Plural(L.Prism.SeasonStanding, (int)Math.Min(standing.Points, int.MaxValue),
                season.Name, standing.Rank));
        var labelPixels = PrismArt.LabelPixels();
        return top + PrismType.DrawLines(drawList, new Vector2(left, top),
            PrismType.Wrap(line, PrismFace.Label, labelPixels, width), PrismArt.Fore, PrismFace.Label, labelPixels) +
               Metrics.Space.Lg * scale;
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
        if (profile?.Placings is not { Length: > 0 } placings)
        {
            return top;
        }

        top = DrawSectionLabel(drawList, left, top, width, 0, Loc.T(L.Prism.PlacingsHeading), scale);
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var radius = labelLine * PlacingMedalShare;
        var line = MathF.Max(radius * 2f, labelLine);
        var column = MathF.Max(RankColumnUnits * scale, radius * 2f + Metrics.Space.Sm * scale);
        for (var index = 0; index < placings.Length; index++)
        {
            var placing = placings[index];
            var middle = top + line * 0.5f;
            var center = new Vector2(left + radius, middle);
            var numeral = Plain[Math.Clamp(placing.Rank, 0, Plain.Length - 1)];
            var podium = placing.Rank is >= 1 and <= 3;
            var face = podium ? PrismFace.Numerals : PrismFace.Label;
            var numeralPixels = podium ? radius / Plugin.Fonts.Zoom : labelPixels;
            if (podium)
            {
                PrismArt.Medal(drawList, center, radius, placing.Rank, 0f, scale);
            }

            PrismType.Draw(drawList, center - PrismType.Measure(numeral, face, numeralPixels) * 0.5f, numeral,
                PrismArt.Fore, face, numeralPixels);
            var textTop = middle - labelLine * 0.5f;
            var trackWidth = placing.Track.Length > 0
                ? PrismType.DrawRight(drawList, new Vector2(left + width, textTop), PrismTracks.Name(placing.Track),
                    PrismArt.Muted, PrismFace.Label, labelPixels)
                : 0f;
            PrismType.Draw(drawList, new Vector2(left + column, textTop),
                PrismType.FitLabel(placing.Title, labelPixels,
                    width - column - trackWidth - (trackWidth > 0f ? Metrics.Space.Lg * scale : 0f)), PrismArt.Fore,
                PrismFace.Label, labelPixels);
            top += line + Metrics.Space.Sm * scale;
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
        var labelPixels = PrismArt.LabelPixels();
        var tryLabel = Loc.T(L.Prism.TryOnGlam);
        var tryWidth = listKind == GlamListKind.Item ? PrismType.Measure(tryLabel, PrismFace.Label, labelPixels).X : 0f;
        if (listKind == GlamListKind.Item && glams.Item is { } item && item.ItemId == listItemId)
        {
            var key = HashCode.Combine(item.GlamCount, Loc.Current.Code);
            var count = listCountText.IsCurrent(key)
                ? listCountText.Value
                : listCountText.Store(key, Loc.Plural(L.Prism.WornInGlams, item.GlamCount));
            PrismType.Draw(drawList, new Vector2(left, top),
                PrismType.FitLabel(count, labelPixels, width - tryWidth - Metrics.Space.Lg * scale), PrismArt.Muted,
                PrismFace.Label, labelPixels);
        }

        if (listKind == GlamListKind.Item &&
            PrismArt.Link(drawList, new Vector2(left + width - tryWidth, top), tryLabel, PrismArt.Muted, scale))
        {
            glamour.TryOnPiece(new GlamGearDto(gameData.EquipSlot(listItemId), listItemId));
            StartWearing(string.Empty, listTitle);
        }

        top += PrismType.LineHeight(PrismFace.Label, labelPixels) + Metrics.Space.Lg * scale;
        if (listKind == GlamListKind.Item)
        {
            top = DrawItemSources(drawList, left, top, scale);
        }
        var body = new Rect(new Vector2(content.Min.X, top), content.Max);
        using var surface = BeginBody(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var scrollWidth = ScrollLayout.StableContentWidth() - gutter * 2f;
        var cursor = DrawLaneGrid(list, origin.X + gutter, origin.Y, scrollWidth, glams.List,
            L.Prism.DiscoverEmptyHint, scale);
        ImGui.Dummy(new Vector2(scrollWidth, cursor - origin.Y));
        if (InfiniteScroll.ReachedBottom())
        {
            glams.LoadMoreList();
        }
    }
}
