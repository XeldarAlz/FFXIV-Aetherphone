using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
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
    private const float ProfileAvatarUnits = 64f;
    private const int ProfileAvatarSegments = 48;
    private const float CoverShare = 0.75f;
    private const float CoverMedalUnits = 11f;
    private const float PlacingMedalUnits = 14f;
    private const float ListIconUnits = 56f;
    private const float EmptyStateUnits = 150f;
    private const long CompactFrom = 10000;
    private const string MarketAppId = "market";
    private const string SettingsAppId = "settings";
    private const double UserRetrySeconds = 5d;
    private const int GlamsTab = 0;
    private const int SavedTab = 1;
    private const int PlacingsTab = 2;

    private readonly string[] profileTabs = new string[3];
    private readonly int[] profileTabKinds = new int[3];
    private readonly bool[] profileTabActive = new bool[3];
    private readonly ChipRail profileTabRail = new();
    private readonly PullToRefresh profilePull = new();
    private Action? refreshProfileNow;
    private readonly Dictionary<string, string> placingLabels = new(StringComparer.Ordinal);
    private string? countLanguage;
    private Spring profileTabSlide;
    private double nextUserRequest;
    private bool showSaved;
    private bool showPlacings;
    private string coverUser = string.Empty;
    private GlamDto? profileCover;
    private GlamDto[]? coverItems;
    private GlamListKind listKind;
    private uint listItemId;
    private uint listIconFor;
    private uint listIconId;
    private GlamModDto? listMod;
    private string listTag = string.Empty;
    private string listTitle = string.Empty;
    private CachedText profileStatsText;
    private CachedText profileRankText;
    private CachedText listCountText;
    private CachedText wornCountText;
    private CachedText vendorText;
    private bool listOwned;

    private void OpenOwnProfile()
    {
        if (!glams.IsSignedIn)
        {
            return;
        }

        showPlacings = false;
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
        showPlacings = false;
        glams.OpenProfile(author);
    }

    private void OpenItemGlams(uint itemId)
    {
        Push(PrismScreen.GlamList);
        listKind = GlamListKind.Item;
        listItemId = itemId;
        listMod = null;
        listTitle = gameData.TryGetItem(itemId, out var name, out _, out _) ? name : string.Empty;
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
        listTitle = string.Concat("#", tag);
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

    private static float BodyTop() => ImGui.GetWindowPos().Y - ImGui.GetScrollY();

    private void DrawProfile(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (!glams.IsSignedIn)
        {
            DrawProfileSignedOut(drawList, content, scale);
            return;
        }

        var mine = screen == PrismScreen.Me;
        if (mine && !ShowsOwnProfile())
        {
            if (glams.MyId.Length == 0)
            {
                RequestUser();
                var loading = Loc.T(L.Common.Loading);
                Typography.Draw(drawList, content.Center - Typography.Measure(loading, PrismType.Caption) * 0.5f,
                    loading, PrismArt.Muted, PrismType.Caption);
                return;
            }

            OpenOwnProfile();
        }

        var profile = glams.Profile;
        var author = profile?.User;
        var lane = glams.List;
        SyncProfileCover(author?.Id ?? string.Empty, lane);
        var covered = profileCover is not null || profile is { GlamCount: > 0 } || !showSaved && lane.Loading;
        var full = FullFrom(content, scale);
        var top = covered ? PhotoTop(full, content) : content.Min.Y;
        if (!covered && !mine)
        {
            top = DrawBackHeader(drawList, content, string.Empty, scale);
        }

        var body = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, content.Max.Y));
        using var surface = BeginBody(body);
        profilePull.Draw(body, surface.Pull, surface.Dragging, lane.Loading, PrismArt.Muted,
            refreshProfileNow ??= RefreshProfile);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = Metrics.Space.Lg * scale;
        var left = origin.X + inset;
        var right = origin.X + width - inset;
        float cursor;
        if (covered)
        {
            var photoTop = new Vector2(origin.X, BodyTop());
            var photo = new Rect(photoTop, photoTop + new Vector2(width, width * CoverShare));
            DrawProfileCover(list, photo, PhotoInset(full, content), profile, mine, scale);
            cursor = DrawProfileStats(list, left, right, photo.Max.Y + Metrics.Space.Xs * scale, profile, mine, scale);
        }
        else
        {
            cursor = DrawProfileIdentity(list, left, right, origin.Y + Metrics.Space.Sm * scale, author, mine, scale);
        }

        cursor = DrawProfileTabs(left, right, cursor + Metrics.Space.Sm * scale, mine, scale);
        cursor = DrawProfileContent(list, origin.X, width, cursor + Metrics.Space.Xxs * scale, profile, mine, scale);
        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
        if (InfiniteScroll.ReachedBottom())
        {
            glams.LoadMoreList();
        }
    }

    private void RefreshProfile()
    {
        var placings = showPlacings;
        glams.List.Clear();
        if (screen == PrismScreen.GlamList)
        {
            OpenList();
            return;
        }

        if (screen == PrismScreen.Me)
        {
            OpenOwnProfile();
        }
        else if (glams.Profile?.User is { } author)
        {
            glams.OpenProfile(author);
        }

        showPlacings = placings;
    }

    private void SyncProfileCover(string userId, FeedLane<GlamDto> lane)
    {
        if (coverUser != userId)
        {
            coverUser = userId;
            profileCover = null;
            coverItems = null;
        }

        var items = lane.Items;
        if (profileCover is not null || showSaved || lane.Loading || ReferenceEquals(items, coverItems))
        {
            return;
        }

        coverItems = items;
        profileCover = MostWorn(items);
    }

    private static GlamDto? MostWorn(GlamDto[] items)
    {
        GlamDto? best = null;
        for (var index = 0; index < items.Length; index++)
        {
            var glam = items[index];
            if (glam.Photos is { Length: > 0 } && (best is null || WearsBetter(glam, best)))
            {
                best = glam;
            }
        }

        return best;
    }

    private static bool WearsBetter(GlamDto glam, GlamDto than)
    {
        var rank = glam.Placing > 0 ? glam.Placing : int.MaxValue;
        var otherRank = than.Placing > 0 ? than.Placing : int.MaxValue;
        if (rank != otherRank)
        {
            return rank < otherRank;
        }

        if (glam.TryOns != than.TryOns)
        {
            return glam.TryOns > than.TryOns;
        }

        return glam.Likes != than.Likes ? glam.Likes > than.Likes : glam.CreatedAtUnix > than.CreatedAtUnix;
    }

    private string ProfileHandle(GlamAuthorDto? author) =>
        author is { Handle.Length: > 0 } ? AuthorLine(author) : string.Empty;

    private void DrawProfileCover(ImDrawListPtr drawList, Rect photo, float topInset, GlamProfileDto? profile,
        bool mine, float scale)
    {
        if (profileCover is { } cover)
        {
            DrawLook(drawList, photo, Cover(cover), false, native: true, focusY: CoverFocus);
            if (UiInteract.Click(photo.Min, photo.Max))
            {
                OpenGlam(cover);
            }
        }
        else
        {
            PrismArt.Placeholder(drawList, photo, false, scale);
        }

        if (!PrismArt.Light)
        {
            PrismArt.TopFog(drawList, photo, scale);
        }

        var author = profile?.User;
        var pad = Metrics.Space.Lg * scale;
        var left = photo.Min.X + pad;
        var right = photo.Max.X - pad;
        var bottom = photo.Max.Y - pad;
        var handleHeight = Typography.LineHeight(PrismType.Caption);
        var following = profile is { Following: true };
        var followWidth = mine || author is null ? 0f : ButtonWidth([Loc.T(L.Prism.Follow), Loc.T(L.Prism.Following)],
            (right - left) * 0.5f, scale);
        var heroRight = followWidth > 0f ? right - followWidth - Metrics.Space.Sm * scale : right;
        var name = AuthorName(author);
        var heroHeight = PrismType.HeroHeight(name, heroRight - left);
        PrismArt.Scrim(drawList, photo, heroHeight + handleHeight, scale);
        PrismType.DrawHero(drawList, new Vector2(left, bottom - handleHeight - heroHeight), name, heroRight - left,
            PrismArt.PhotoFore);
        var handleRight = mine
            ? DrawCoverPlacing(drawList, right, bottom - handleHeight * 0.5f, (right - left) * 0.5f, profile, scale)
            : heroRight;
        Typography.Draw(drawList, new Vector2(left, bottom - handleHeight),
            Typography.FitText(ProfileHandle(author), MathF.Max(1f, handleRight - left), PrismType.Caption),
            PrismArt.PhotoMuted, PrismType.Caption);
        if (mine)
        {
            if (DrawPhotoButton(drawList, photo, topInset, 1, PhoneIcons.Pencil, Loc.T(L.Prism.EditProfile), scale))
            {
                navigation.Open(SettingsAppId);
            }

            return;
        }

        DrawPhotoBack(drawList, photo, topInset, scale);
        if (author is null)
        {
            return;
        }

        var height = Metrics.Size.TapTarget * scale;
        var pill = new Rect(new Vector2(right - followWidth, bottom - height), new Vector2(right, bottom));
        UiInteract.HoverOverlay(pill);
        if (DrawButton(drawList, pill, Loc.T(following ? L.Prism.Following : L.Prism.Follow),
                following ? ButtonStyle.Tinted : ButtonStyle.Solid, scale, overlay: true, ink: photoControls,
                id: "prism.follow"))
        {
            glams.SetFollow(author, !following);
        }
    }

    private float DrawCoverPlacing(ImDrawListPtr drawList, float right, float middle, float room,
        GlamProfileDto? profile, float scale)
    {
        GlamPlacingDto? best = null;
        var placings = profile?.Placings ?? Array.Empty<GlamPlacingDto>();
        for (var index = 0; index < placings.Length; index++)
        {
            if (placings[index].Rank is >= 1 and <= PrismArt.PodiumRanks && (best is null || placings[index].Rank < best.Rank))
            {
                best = placings[index];
            }
        }

        if (best is null)
        {
            return right;
        }

        var title = Typography.FitText(best.Title, room, PrismType.Caption);
        var titleSize = Typography.Measure(title, PrismType.Caption);
        Typography.Draw(drawList, new Vector2(right - titleSize.X, middle - titleSize.Y * 0.5f), title,
            PrismArt.PhotoFore, PrismType.Caption);
        var radius = CoverMedalUnits * scale;
        var center = new Vector2(right - titleSize.X - Metrics.Space.Xs * scale - radius, middle);
        DrawPlacingMedal(drawList, center, radius, best.Rank, true, scale);
        return center.X - radius - Metrics.Space.Sm * scale;
    }

    private void DrawPlacingMedal(ImDrawListPtr drawList, Vector2 center, float radius, int rank, bool onPhoto,
        float scale)
    {
        PrismArt.OverPhoto(onPhoto);
        PrismArt.Medal(drawList, center, radius, rank, 0f, scale);
        var digit = CountLabel(rank);
        Typography.Draw(drawList, center - Typography.Measure(digit, PrismType.Caption) * 0.5f, digit, PrismArt.Fore,
            PrismType.Caption);
        PrismArt.OverPhoto(false);
    }

    private float DrawProfileIdentity(ImDrawListPtr drawList, float left, float right, float top, GlamAuthorDto? author,
        bool mine, float scale)
    {
        var avatar = ProfileAvatarUnits * scale;
        var middle = top + avatar * 0.5f;
        if (mine)
        {
            var reach = Metrics.Size.TapTarget * scale;
            if (IconButton(drawList, new Vector2(right - reach * 0.5f, middle), PhoneIcons.Pencil,
                    Loc.T(L.Prism.EditProfile), scale))
            {
                navigation.Open(SettingsAppId);
            }

            right -= reach + Metrics.Space.Sm * scale;
        }

        var name = AuthorName(author);
        AvatarView.Draw(drawList, new Vector2(left + avatar * 0.5f, middle), avatar * 0.5f, PrismArt.Muted,
            Initials.Of(name), TextStyles.Title3.Scale, images.Avatar(author?.AvatarUrl, avatar), ProfileAvatarSegments);
        var textLeft = left + avatar + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, right - textLeft);
        var heroHeight = PrismType.HeroHeight(name, textWidth);
        var handleHeight = Typography.LineHeight(PrismType.Caption);
        var textTop = top + MathF.Max(0f, (avatar - heroHeight - handleHeight) * 0.5f);
        PrismType.DrawHero(drawList, new Vector2(textLeft, textTop), name, textWidth, PrismArt.Fore);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + heroHeight),
            Typography.FitText(ProfileHandle(author), textWidth, PrismType.Caption), PrismArt.Muted, PrismType.Caption);
        return top + MathF.Max(avatar, heroHeight + handleHeight);
    }

    private float DrawProfileStats(ImDrawListPtr drawList, float left, float right, float top, GlamProfileDto? profile,
        bool mine, float scale)
    {
        var height = MathF.Max(Metrics.Size.TapTarget * scale, Typography.LineHeight(PrismType.Body));
        if (profile is not { GlamCount: > 0 } stats)
        {
            return top + height;
        }

        var key = HashCode.Combine(stats.GlamCount, stats.TryOnsReceived, stats.LikesReceived, Loc.Current.Code);
        var line = profileStatsText.IsCurrent(key)
            ? profileStatsText.Value
            : profileStatsText.Store(key, string.Concat(Loc.Plural(L.Prism.GlamCount, stats.GlamCount), GlamMetaSeparator,
                Loc.Plural(L.Prism.TriedOnTotal, Clamp(stats.TryOnsReceived)), GlamMetaSeparator,
                Loc.Plural(L.Prism.LikeCount, Clamp(stats.LikesReceived))));
        if (mine && glams.Season is { Me: { Rank: > 0 } standing })
        {
            var rankKey = HashCode.Combine(standing.Rank, Loc.Current.Code);
            var rank = profileRankText.IsCurrent(rankKey)
                ? profileRankText.Value
                : profileRankText.Store(rankKey, Loc.T(L.Prism.SeasonRank, standing.Rank));
            var size = Typography.Measure(rank, PrismType.Caption);
            var room = right - size.X - Metrics.Space.Md * scale;
            if (left + Typography.Measure(line, PrismType.Body).X <= room)
            {
                Typography.Draw(drawList, new Vector2(right - size.X, top + (height - size.Y) * 0.5f), rank,
                    PrismArt.Muted, PrismType.Caption);
                right = room;
            }
        }

        var fittedLine = Typography.FitText(line, MathF.Max(1f, right - left), PrismType.Body);
        var lineSize = Typography.Measure(fittedLine, PrismType.Body);
        Typography.Draw(drawList, new Vector2(left, top + (height - lineSize.Y) * 0.5f), fittedLine, PrismArt.Fore,
            PrismType.Body);
        var counts = new Rect(new Vector2(left, top), new Vector2(left + lineSize.X, top + height));
        if (UiInteract.HoverClick(counts.Min, counts.Max))
        {
            SelectProfileTab(GlamsTab);
        }

        return top + height;
    }

    private static int Clamp(long value) => (int)Math.Min(value, int.MaxValue);

    private float DrawProfileTabs(float left, float right, float top, bool mine, float scale)
    {
        var count = 0;
        AddProfileTab(ref count, L.Prism.StatGlams, GlamsTab);
        if (mine)
        {
            AddProfileTab(ref count, L.Prism.Saved, SavedTab);
        }

        AddProfileTab(ref count, L.Prism.PlacingsHeading, PlacingsTab);
        var kind = showSaved ? SavedTab : showPlacings ? PlacingsTab : GlamsTab;
        var active = 0;
        for (var index = 0; index < count; index++)
        {
            profileTabActive[index] = profileTabKinds[index] == kind;
            if (profileTabActive[index])
            {
                active = index;
            }
        }

        var row = new Rect(new Vector2(left, top), new Vector2(right, top + Metrics.Size.TapTarget * scale));
        var labels = new ReadOnlySpan<string>(profileTabs, 0, count);
        var picked = TabsFit(labels, row.Width, scale)
            ? DrawTabs(row, labels, active, ref profileTabSlide)
            : profileTabRail.Draw(row, ui, labels, new ReadOnlySpan<bool>(profileTabActive, 0, count));
        if (picked >= 0 && picked != active)
        {
            SelectProfileTab(profileTabKinds[picked]);
        }

        return row.Max.Y;
    }

    private void AddProfileTab(ref int count, LocString label, int kind)
    {
        profileTabs[count] = Loc.T(label);
        profileTabKinds[count] = kind;
        count++;
    }

    private void SelectProfileTab(int kind)
    {
        var saved = kind == SavedTab;
        if (saved != showSaved)
        {
            showSaved = saved;
            OpenOwnProfile();
        }

        showPlacings = kind == PlacingsTab;
    }

    private float DrawProfileContent(ImDrawListPtr drawList, float left, float width, float top,
        GlamProfileDto? profile, bool mine, float scale)
    {
        var lane = glams.List;
        var items = lane.Items;
        var inset = Metrics.Space.Lg * scale;
        if (items.Length == 0 && (lane.Loading || lane.Failed))
        {
            return DrawLaneState(drawList, left, width, top, lane, scale);
        }

        if (showPlacings)
        {
            return DrawPlacings(drawList, left, width, top, items, profile, scale);
        }

        if (items.Length > 0)
        {
            return DrawGlamTiles(drawList, left, width, top, items, false, scale);
        }

        if (showSaved)
        {
            return DrawEmpty(left + inset, left + width - inset, top, PhoneIcons.Photo, Loc.T(L.Prism.Saved),
                Loc.T(L.Prism.SavedEmptyHint), scale);
        }

        if (!mine)
        {
            return DrawEmpty(left + inset, left + width - inset, top, PhoneIcons.Photo,
                Loc.T(L.Prism.ProfileEmptyHint), string.Empty, scale);
        }

        var bottom = DrawEmpty(left + inset, left + width - inset, top, PhoneIcons.Photo,
            Loc.T(L.Prism.FirstGlamTitle), Loc.T(L.Prism.FirstGlamHint), Loc.T(L.Prism.PostGlam), scale, out var post);
        if (post)
        {
            StartCompose();
        }

        return bottom;
    }

    private float DrawPlacings(ImDrawListPtr drawList, float left, float width, float top, GlamDto[] items,
        GlamProfileDto? profile, float scale)
    {
        var bottom = DrawGlamTiles(drawList, left, width, top, items, true, scale);
        var inset = Metrics.Space.Lg * scale;
        var placings = profile?.Placings ?? Array.Empty<GlamPlacingDto>();
        var radius = PlacingMedalUnits * scale;
        var pitch = MathF.Max(Metrics.Size.TapTarget * scale, Typography.LineHeight(PrismType.Headline));
        var textLeft = left + inset + radius * 2f + Metrics.Space.Md * scale;
        var right = left + width - inset;
        for (var index = 0; index < placings.Length; index++)
        {
            var placing = placings[index];
            if (HasPlacedGlam(items, placing))
            {
                continue;
            }

            var middle = bottom + pitch * 0.5f;
            DrawPlacingMedal(drawList, new Vector2(left + inset + radius, middle), radius, placing.Rank, false, scale);
            var track = placing.Track.Length > 0 ? PrismTracks.Name(placing.Track) : string.Empty;
            var trackSize = Typography.Measure(track, PrismType.Caption);
            Typography.Draw(drawList, new Vector2(right - trackSize.X, middle - trackSize.Y * 0.5f), track,
                PrismArt.Muted, PrismType.Caption);
            var room = right - textLeft - (trackSize.X > 0f ? trackSize.X + Metrics.Space.Md * scale : 0f);
            var title = Typography.FitText(placing.Title, MathF.Max(1f, room), PrismType.Headline);
            Typography.Draw(drawList,
                new Vector2(textLeft, middle - Typography.Measure(title, PrismType.Headline).Y * 0.5f), title,
                PrismArt.Fore, PrismType.Headline);
            bottom += pitch;
        }

        if (bottom > top)
        {
            return bottom;
        }

        return DrawEmpty(left + inset, right, top, PhoneIcons.Crown, Loc.T(L.Prism.NoPlacings), string.Empty, scale);
    }

    private static bool HasPlacedGlam(GlamDto[] items, GlamPlacingDto placing)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Placing == placing.Rank && items[index].WeekId == placing.WeekId)
            {
                return true;
            }
        }

        return false;
    }

    private float DrawGlamTiles(ImDrawListPtr drawList, float left, float width, float top, GlamDto[] items,
        bool placings, float scale)
    {
        var gap = Metrics.Space.Xxs * scale;
        var tileLeft = left + gap;
        var column = (width - gap * (GridColumns + 1)) / GridColumns;
        var height = column / GlamStore.PhotoRatio;
        var pitch = height + GridLine(scale) + gap;
        var shown = 0;
        for (var index = 0; index < items.Length; index++)
        {
            var glam = items[index];
            if (placings && glam.Placing <= 0)
            {
                continue;
            }

            var x = tileLeft + shown % GridColumns * (column + gap);
            var y = top + shown / GridColumns * pitch;
            shown++;
            var photo = new Rect(new Vector2(x, y), new Vector2(x + column, y + height));
            if (DrawGridTile(drawList, photo, Cover(glam), placings ? PlacingLabel(glam) : glam.Title,
                    string.Empty, null, PhoneIcons.Wand, glam.TryOns > 0 ? CountLabel(glam.TryOns) : string.Empty, scale))
            {
                OpenGlam(glam);
            }
        }

        var bottom = top + (shown + GridColumns - 1) / GridColumns * pitch;
        return glams.List.LoadingMore && shown > 0 ? DrawLoadingRowBelow(left, width, bottom) : bottom;
    }

    private bool IconButton(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip, float scale,
        HoverLabelSide side = HoverLabelSide.Above) =>
        RoundButton.Icon(drawList, center, Metrics.Size.TapTarget * 0.5f * scale, glyph, controls, ButtonStyle.Bare,
            tooltip, side);

    private float DrawLaneState(ImDrawListPtr drawList, float left, float width, float top, FeedLane<GlamDto> lane,
        float scale)
    {
        if (!lane.Failed)
        {
            var gap = Metrics.Space.Xxs * scale;
            return DrawGridPlaceholders(drawList, left + gap, top, width - gap * 2f, scale);
        }

        var inset = Metrics.Space.Lg * scale;
        var hint = Loc.T(L.Common.LoadFailedHint);
        top += Typography.DrawWrappedLeft(new Vector2(left + inset, top + Metrics.Space.Md * scale), hint,
            PrismArt.Muted, PrismType.Body, width - inset * 2f) + Metrics.Space.Md * scale;
        var label = Loc.T(L.Common.Retry);
        var retry = new Rect(new Vector2(left + inset, top),
            new Vector2(left + inset + ButtonWidth([label], width, scale), top + Button.LargeHeight * scale));
        if (DrawButton(drawList, retry, label, ButtonStyle.Plain, scale))
        {
            RetryLane(lane);
        }

        return retry.Max.Y;
    }

    private float DrawEmpty(float left, float right, float top, string glyph, string title, string hint, float scale) =>
        DrawEmpty(left, right, top, glyph, title, hint, string.Empty, scale, out _);

    private float DrawEmpty(float left, float right, float top, string glyph, string title, string hint, string action,
        float scale, out bool tapped)
    {
        var hintHeight = hint.Length == 0
            ? 0f
            : Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, right - left - Metrics.Space.Xxl * scale * 2f).Y;
        var actionHeight = action.Length > 0 ? Button.LargeHeight * scale * 2f : 0f;
        var block = new Rect(new Vector2(left, top),
            new Vector2(right, top + EmptyStateUnits * scale + hintHeight * 2f + actionHeight));
        var resume = ImGui.GetCursorScreenPos();
        tapped = EmptyState.Draw(block, ui, glyph, title, hint, action, ButtonStyle.Solid);
        ImGui.SetCursorScreenPos(resume);
        return block.Max.Y;
    }

    private void DrawProfileSignedOut(ImDrawListPtr drawList, Rect content, float scale)
    {
        var inset = Metrics.Space.Lg * scale;
        var top = content.Min.Y + content.Height * 0.2f;
        DrawEmpty(content.Min.X + inset, content.Max.X - inset, top, PhoneIcons.User, Loc.T(L.Prism.MeSignInTitle),
            Loc.T(L.Prism.MeSignInHint), Loc.T(L.Prism.OpenSettings), scale, out var open);
        if (open)
        {
            navigation.Open(SettingsAppId);
        }
    }

    private static string CountLabel(long value) =>
        value >= CompactFrom ? NumberText.Compact(value) : NumberText.Group(value);

    private string PlacingLabel(GlamDto glam)
    {
        SyncLabelLanguage();
        if (!placingLabels.TryGetValue(glam.Id, out var label))
        {
            label = Loc.T(L.Prism.PlacingLine, glam.Placing, glam.Title);
            placingLabels[glam.Id] = label;
        }

        return label;
    }

    private void SyncLabelLanguage()
    {
        if (ReferenceEquals(countLanguage, Loc.Current.Code))
        {
            return;
        }

        countLanguage = Loc.Current.Code;
        placingLabels.Clear();
    }

    private void DrawGlamList(ImDrawListPtr drawList, Rect content, float scale)
    {
        var top = DrawBackHeader(drawList, content, string.Empty, scale);
        var full = FullFrom(content, scale);
        var body = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, content.Max.Y));
        using var surface = BeginBody(body);
        profilePull.Draw(body, surface.Pull, surface.Dragging, glams.List.Loading, PrismArt.Muted,
            refreshProfileNow ??= RefreshProfile);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = Metrics.Space.Lg * scale;
        var cursor = DrawListHeader(list, origin.X + inset, origin.X + width - inset, origin.Y, scale) +
                     Metrics.Space.Lg * scale;
        var lane = glams.List;
        if (lane.Items.Length == 0 && (lane.Loading || lane.Failed))
        {
            cursor = DrawLaneState(list, origin.X, width, cursor, lane, scale);
        }
        else if (lane.Items.Length == 0)
        {
            cursor += Typography.DrawWrappedLeft(new Vector2(origin.X + inset, cursor), Loc.T(L.Prism.DiscoverEmptyHint),
                PrismArt.Muted, PrismType.Body, width - inset * 2f);
        }
        else
        {
            cursor = DrawGlamTiles(list, origin.X, width, cursor, lane.Items, false, scale);
        }

        ImGui.Dummy(new Vector2(width, cursor - origin.Y + PrismArt.BottomMargin(scale)));
        if (InfiniteScroll.ReachedBottom())
        {
            glams.LoadMoreList();
        }
    }

    private float DrawListHeader(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var captionHeight = Typography.LineHeight(PrismType.Caption);
        if (listKind != GlamListKind.Item)
        {
            top += PrismType.DrawHero(drawList, new Vector2(left, top), listTitle, right - left, PrismArt.Fore) +
                   Metrics.Space.Xxs * scale;
            var lane = glams.List;
            if (!lane.Loading && !lane.HasMore && lane.Items.Length > 0)
            {
                var count = listCountText.IsCurrent(lane.Items.Length)
                    ? listCountText.Value
                    : listCountText.Store(lane.Items.Length, Loc.Plural(L.Prism.GlamCount, lane.Items.Length));
                Typography.Draw(drawList, new Vector2(left, top), count, PrismArt.Muted, PrismType.Caption);
            }

            return top + captionHeight;
        }

        if (listIconFor != listItemId)
        {
            listIconFor = listItemId;
            listIconId = gameData.TryGetItem(listItemId, out _, out var iconId, out _) ? iconId : 0;
        }

        var icon = ListIconUnits * scale;
        GameIconTile.Draw(drawList, textures, listIconId, new Vector2(left, top), new Vector2(left + icon, top + icon),
            Metrics.Radius.Md * scale, scale);
        var textLeft = left + icon + Metrics.Space.Md * scale;
        var heroHeight = PrismType.DrawHero(drawList, new Vector2(textLeft, top), listTitle, right - textLeft,
            PrismArt.Fore);
        top = MathF.Max(top + icon, top + heroHeight) + Metrics.Space.Sm * scale;
        if (glams.Item is { } item && item.ItemId == listItemId)
        {
            var count = wornCountText.IsCurrent(item.GlamCount)
                ? wornCountText.Value
                : wornCountText.Store(item.GlamCount, Loc.Plural(L.Prism.WornInGlams, item.GlamCount));
            Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(count, right - left, PrismType.Caption),
                PrismArt.Muted, PrismType.Caption);
        }

        top += captionHeight;
        var market = marketIndex.TryGet(listItemId, out var entry) ? entry : default;
        var source = listOwned ? Loc.T(L.Prism.YouOwnThis) : market.VendorPrice > 0 ? VendorLine(market.VendorPrice) : string.Empty;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(source, right - left, PrismType.Caption),
            PrismArt.Muted, PrismType.Caption);
        top += captionHeight + Metrics.Space.Md * scale;
        var height = Button.LargeHeight * scale;
        var tryLabel = Loc.T(L.Prism.TryOnGlam);
        var tryOn = new Rect(new Vector2(left, top),
            new Vector2(left + ButtonWidth([tryLabel], (right - left) * 0.5f, scale), top + height));
        if (DrawButton(drawList, tryOn, tryLabel, ButtonStyle.Solid, scale))
        {
            glamour.TryOnPiece(new GlamGearDto(gameData.EquipSlot(listItemId), listItemId));
            StartWearing(string.Empty, listTitle);
        }

        var marketLabel = Loc.T(L.Prism.CheckMarket);
        var marketLeft = tryOn.Max.X + Metrics.Space.Sm * scale;
        if (market.IsValid && navigation.IsAvailable(MarketAppId) &&
            DrawButton(drawList, new Rect(new Vector2(marketLeft, top), new Vector2(
                    marketLeft + ButtonWidth([marketLabel], right - marketLeft, scale), top + height)), marketLabel,
                ButtonStyle.Plain, scale))
        {
            marketLauncher.RequestItem(listItemId);
            navigation.Open(MarketAppId);
        }

        return top + height;
    }

    private string VendorLine(long price)
    {
        var key = HashCode.Combine(price, Loc.Current.Code);
        return vendorText.IsCurrent(key)
            ? vendorText.Value
            : vendorText.Store(key, Loc.T(L.Prism.VendorPrice, NumberText.Group(price)));
    }
}
