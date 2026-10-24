using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Report;
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
    private const float ProfileCoverFocus = 0.1f;
    private const float CoverMedalUnits = 11f;
    private const float PlacingMedalUnits = 14f;
    private const float ListIconUnits = 56f;
    private const long CompactFrom = 10000;
    private const string MarketAppId = "market";
    private const string SettingsAppId = "settings";
    private const int GlamsTab = 0;
    private const int SavedTab = 1;
    private const int PlacingsTab = 2;

    private static readonly string[] StatGlyphs = { PhoneIcons.Photo, PhoneIcons.Wand, PhoneIcons.Heart };
    private static readonly string[] StatIds = { "prism.stat.glams", "prism.stat.triedOn", "prism.stat.likes" };
    private static readonly LocPlural[] StatLabels = { L.Prism.GlamCount, L.Prism.TriedOnTotal, L.Prism.LikeCount };

    private readonly CachedText[] statCountTexts = new CachedText[3];
    private readonly CachedText[] statLabelTexts = new CachedText[3];
    private readonly string[] profileTabs = new string[3];
    private readonly string[] profileTabGlyphs = new string[3];
    private readonly int[] profileTabKinds = new int[3];
    private readonly ActionSheet profileMenu = new();
    private readonly ActionSheet.Item[] profileMenuItems = new ActionSheet.Item[1];
    private readonly PullToRefresh profilePull = new();
    private Action? refreshProfileNow;
    private readonly Dictionary<string, string> placingLabels = new(StringComparer.Ordinal);
    private string? countLanguage;
    private Spring profileTabSlide;
    private bool showSaved;
    private bool showPlacings;
    private string coverUser = string.Empty;
    private GlamDto? profileCover;
    private GlamDto[]? coverItems;
    private GlamBannerDto? coverBanner;
    private string coverChosen = string.Empty;
    private bool coverMissing;
    private GlamListKind listKind;
    private uint listItemId;
    private uint listIconFor;
    private uint listIconId;
    private GlamModDto? listMod;
    private string listTag = string.Empty;
    private string listTitle = string.Empty;
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
        if (glams.Me is not { } me)
        {
            glams.EnsureMe();
            return;
        }

        glams.OpenProfile(me, showSaved);
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

    private void DrawProfile(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (!glams.IsSignedIn)
        {
            DrawProfileSignedOut(drawList, content, scale);
            return;
        }

        var mine = screen == PrismScreen.Me;
        if (mine && !ShowsOwnProfile())
        {
            if (glams.Me is null)
            {
                glams.EnsureMe();
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
        SyncProfileCover(author?.Id ?? string.Empty, lane, mine ? OwnBanner() : author?.BannerGlam);
        var covered = profileCover is not null || profile is { GlamCount: > 0 } ||
                      profile is null && !showSaved && lane.Loading;
        var top = covered ? PhotoTop(full, content) : content.Min.Y;
        profileMenu.Gate();
        if (!covered && !mine)
        {
            top = DrawBackHeader(drawList, content, string.Empty, scale);
            var reach = Metrics.Size.TapTarget * scale * 0.5f;
            if (author is not null && IconButton(drawList, new Vector2(content.Max.X - reach, content.Min.Y + reach),
                    PhoneIcons.Dots, Loc.T(L.Prism.More), scale, HoverLabelSide.Below))
            {
                OpenProfileMenu();
            }
        }

        DrawProfileBody(new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, content.Max.Y)), full, content,
            profile, covered, mine, scale);
        DrawProfileMenu(full, author);
    }

    private void DrawProfileBody(Rect body, Rect full, Rect content, GlamProfileDto? profile, bool covered, bool mine,
        float scale)
    {
        var author = profile?.User;
        var lane = glams.List;
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

        cursor = DrawProfileBio(left, right, cursor, mine ? glams.Me : author, scale);
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

    private void SyncProfileCover(string userId, FeedLane<GlamDto> lane, GlamBannerDto? banner)
    {
        var chosen = banner?.GlamId ?? string.Empty;
        if (coverUser != userId || coverChosen != chosen)
        {
            coverUser = userId;
            coverChosen = chosen;
            profileCover = null;
            coverItems = null;
            coverMissing = false;
        }

        coverBanner = banner;
        if (chosen.Length > 0 && !coverMissing)
        {
            profileCover ??= FindBanner(chosen, lane, out coverMissing);
            if (!coverMissing)
            {
                return;
            }
        }

        var items = lane.Items;
        if (showSaved || lane.Loading || ReferenceEquals(items, coverItems))
        {
            return;
        }

        coverItems = items;
        if (profileCover is null || !Lists(items, profileCover.Id))
        {
            profileCover = MostWorn(items);
        }
    }

    private static bool Lists(GlamDto[] items, string id)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id == id)
            {
                return true;
            }
        }

        return false;
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
            var focus = coverBanner is { } chosen && chosen.GlamId == cover.Id
                ? new Vector2(chosen.FocusX, BannerFocus(photo, topInset, chosen.FocusY))
                : new Vector2(ImageFit.CenterFocus, ProfileFocus(photo, topInset));
            DrawLook(drawList, photo, Cover(cover), false, native: true, focusY: focus.Y, focusX: focus.X);
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
        var following = profile is { Following: true };
        var followWidth = mine || author is null ? 0f : ButtonWidth([Loc.T(L.Prism.Follow), Loc.T(L.Prism.Following)],
            (right - left) * 0.5f, scale);
        DrawCoverIdentity(drawList, photo, author, BestPlacing(profile),
            followWidth > 0f ? right - followWidth - Metrics.Space.Sm * scale : right, scale);
        if (mine)
        {
            if (DrawPhotoButton(drawList, photo, topInset, 1, PhoneIcons.Pencil, Loc.T(L.Prism.EditProfile), scale))
            {
                OpenEditProfile();
            }

            return;
        }

        DrawPhotoBack(drawList, photo, topInset, scale);
        if (author is null)
        {
            return;
        }

        if (DrawPhotoButton(drawList, photo, topInset, 1, PhoneIcons.Dots, Loc.T(L.Prism.More), scale))
        {
            OpenProfileMenu();
        }

        var height = Metrics.Size.TapTarget * scale;
        var pill = new Rect(new Vector2(right - followWidth, bottom - height), new Vector2(right, bottom));
        var overPill = UiInteract.HoverOverlay(pill);
        if (DrawButton(drawList, pill, Loc.T(following ? L.Prism.Following : L.Prism.Follow),
                following ? ButtonStyle.Tinted : ButtonStyle.Solid, scale, overlay: true, ink: photoControls,
                id: "prism.follow") && overPill)
        {
            glams.SetFollow(author, !following);
        }
    }

    private void DrawCoverIdentity(ImDrawListPtr drawList, Rect photo, GlamAuthorDto? author, GlamPlacingDto? best,
        float heroRight, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var left = photo.Min.X + pad;
        var bottom = photo.Max.Y - pad;
        var handleHeight = Typography.LineHeight(PrismType.Caption);
        var name = AuthorName(author);
        var heroHeight = PrismType.HeroHeight(name, heroRight - left);
        var placingHeight = best is null ? 0f : MathF.Max(handleHeight, CoverMedalUnits * 2f * scale);
        var handleTop = bottom - placingHeight - handleHeight;
        PrismArt.Scrim(drawList, photo, heroHeight + handleHeight + placingHeight, scale);
        PrismType.DrawHero(drawList, new Vector2(left, handleTop - heroHeight), name, heroRight - left,
            PrismArt.PhotoFore);
        Typography.Draw(drawList, new Vector2(left, handleTop),
            Typography.FitText(ProfileHandle(author), MathF.Max(1f, heroRight - left), PrismType.Caption),
            PrismArt.PhotoMuted, PrismType.Caption);
        if (best is not null)
        {
            DrawCoverPlacing(drawList, left, bottom - placingHeight * 0.5f, heroRight - left, best, scale);
        }
    }

    private static float BannerFocus(Rect photo, float topInset, float focus)
    {
        var overflow = photo.Width / GlamStore.PhotoRatio - photo.Height;
        return overflow <= 0f
            ? ImageFit.CenterFocus
            : Math.Clamp((focus * (overflow + topInset) - topInset) / overflow, 0f, 1f);
    }

    private static float ProfileFocus(Rect photo, float topInset)
    {
        var shown = photo.Height / MathF.Max(1f, photo.Width) * GlamStore.PhotoRatio;
        return shown >= 1f
            ? ImageFit.CenterFocus
            : MathF.Max(0f, ProfileCoverFocus - topInset / MathF.Max(1f, photo.Height) * shown / (1f - shown));
    }

    private void OpenProfileMenu()
    {
        profileMenuItems[0] = new ActionSheet.Item(Loc.T(L.Report.UserTitle), PhoneIcons.Flag, true);
        profileMenu.Open();
    }

    private void DrawProfileMenu(Rect full, GlamAuthorDto? author)
    {
        if (!profileMenu.CapturesPointer)
        {
            return;
        }

        if (profileMenu.Draw(full, ActionSheetStyle.From(ui), profileMenuItems, Loc.T(L.Common.Cancel), false) == 0 &&
            author is { Id.Length: > 0 })
        {
            ReportProfile(author.Id);
        }
    }

    private void ReportProfile(string userId)
    {
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Report.UserTitle),
            Venue = ReportVenue.Social,
            Submit = (reason, done) => store.ReportUser(userId, reason, done),
        });
    }

    private static GlamPlacingDto? BestPlacing(GlamProfileDto? profile)
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

        return best;
    }

    private void DrawCoverPlacing(ImDrawListPtr drawList, float left, float middle, float room, GlamPlacingDto best,
        float scale)
    {
        var radius = CoverMedalUnits * scale;
        DrawPlacingMedal(drawList, new Vector2(left + radius, middle), radius, best.Rank, true, scale);
        var textLeft = left + radius * 2f + Metrics.Space.Xs * scale;
        var title = Typography.FitText(best.Title, MathF.Max(1f, left + room - textLeft), PrismType.Caption);
        Typography.Draw(drawList, new Vector2(textLeft, middle - Typography.LineHeight(PrismType.Caption) * 0.5f),
            title, PrismArt.PhotoFore, PrismType.Caption);
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
                OpenEditProfile();
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

    private static float DrawProfileBio(float left, float right, float top, GlamAuthorDto? author, float scale)
    {
        if (author is not { Bio.Length: > 0 } person)
        {
            return top;
        }

        top += Metrics.Space.Xs * scale;
        return top + Typography.DrawWrappedLeft(new Vector2(left, top), person.Bio, PrismArt.Fore, PrismType.Body,
            right - left);
    }

    private float DrawProfileStats(ImDrawListPtr drawList, float left, float right, float top, GlamProfileDto? profile,
        bool mine, float scale)
    {
        var height = MathF.Max(Metrics.Size.TapTarget * scale, Typography.LineHeight(PrismType.Body));
        if (profile is not { GlamCount: > 0 } stats)
        {
            return top + height;
        }

        var middle = top + height * 0.5f;
        var end = DrawProfileStat(drawList, left, middle, height, 0, stats.GlamCount, scale);
        end = DrawProfileStat(drawList, end, middle, height, 1, stats.TryOnsReceived, scale);
        end = DrawProfileStat(drawList, end, middle, height, 2, stats.LikesReceived, scale);
        if (mine && glams.Season is { Me: { Rank: > 0 } standing })
        {
            var rankKey = HashCode.Combine(standing.Rank, Loc.Current.Code);
            var rank = profileRankText.IsCurrent(rankKey)
                ? profileRankText.Value
                : profileRankText.Store(rankKey, Loc.T(L.Prism.SeasonRank, standing.Rank));
            var size = Typography.Measure(rank, PrismType.Caption);
            if (end <= right - size.X)
            {
                Typography.Draw(drawList, new Vector2(right - size.X, middle - size.Y * 0.5f), rank, PrismArt.Muted,
                    PrismType.Caption);
            }
        }

        return top + height;
    }

    private float DrawProfileStat(ImDrawListPtr drawList, float left, float middle, float height, int index,
        long value, float scale)
    {
        ref var countText = ref statCountTexts[index];
        var count = countText.IsCurrent(value) ? countText.Value : countText.Store(value, CountLabel(value));
        var glyph = PrismArt.RowIconUnits * scale;
        var textLeft = left + glyph + Metrics.Space.Xs * scale;
        var size = Typography.Measure(count, PrismType.Headline);
        PhoneIcon.Draw(drawList, new Vector2(left + glyph * 0.5f, middle), StatGlyphs[index], PrismArt.Fore, glyph);
        Typography.Draw(drawList, new Vector2(textLeft, middle - size.Y * 0.5f), count, PrismArt.Fore,
            PrismType.Headline);
        var half = height * 0.5f;
        var hit = new Rect(new Vector2(left, middle - half), new Vector2(textLeft + size.X, middle + half));
        ref var labelText = ref statLabelTexts[index];
        var key = HashCode.Combine(value, Loc.Current.Code);
        var label = labelText.IsCurrent(key)
            ? labelText.Value
            : labelText.Store(key, Loc.Plural(StatLabels[index], Clamp(value)));
        HoverTooltip.Show(ImGui.GetID(StatIds[index]), hit, label, UiInteract.Hover(hit.Min, hit.Max),
            HoverLabelSide.Above);
        return hit.Max.X + Metrics.Space.Lg * scale;
    }

    private static int Clamp(long value) => (int)Math.Min(value, int.MaxValue);

    private float DrawProfileTabs(float left, float right, float top, bool mine, float scale)
    {
        var count = 0;
        AddProfileTab(ref count, L.Prism.StatGlams, PhoneIcons.Photo, GlamsTab);
        if (mine)
        {
            AddProfileTab(ref count, L.Prism.Saved, PhoneIcons.Bookmark, SavedTab);
        }

        AddProfileTab(ref count, L.Prism.PlacingsHeading, PhoneIcons.Crown, PlacingsTab);
        var kind = showSaved ? SavedTab : showPlacings ? PlacingsTab : GlamsTab;
        var active = 0;
        for (var index = 0; index < count; index++)
        {
            if (profileTabKinds[index] == kind)
            {
                active = index;
            }
        }

        var row = new Rect(new Vector2(left, top), new Vector2(right, top + Metrics.Size.TapTarget * scale));
        var picked = UnderlineTabs.DrawIcons(row, new ReadOnlySpan<string>(profileTabGlyphs, 0, count),
            new ReadOnlySpan<string>(profileTabs, 0, count), active, ref profileTabSlide, tabInk!, PrismArt.IconUnits,
            Metrics.Stroke.Ring, Motion.Release);
        if (picked >= 0 && picked != active)
        {
            SelectProfileTab(profileTabKinds[picked]);
        }

        return row.Max.Y;
    }

    private void AddProfileTab(ref int count, LocString label, string glyph, int kind)
    {
        profileTabs[count] = Loc.T(label);
        profileTabGlyphs[count] = glyph;
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
            return DrawGlamTiles(drawList, left, width, top, items, null, scale);
        }

        var area = EmptyArea(left, width, top, scale);
        if (showSaved)
        {
            var saved = DrawEmpty(drawList, area, PhoneIcons.Bookmark, Loc.T(L.Prism.Saved),
                Loc.T(L.Prism.SavedEmptyHint), Loc.T(L.Prism.TabDiscover), ButtonStyle.Tinted, scale, out var browse);
            if (browse)
            {
                OpenTab(PrismTab.Discover);
            }

            return saved;
        }

        if (!mine)
        {
            return DrawEmpty(drawList, area, PhoneIcons.Photo, Loc.T(L.Prism.ProfileEmptyHint), string.Empty,
                string.Empty, ButtonStyle.Solid, scale, out _);
        }

        var bottom = DrawEmpty(drawList, area, PhoneIcons.Photo, Loc.T(L.Prism.FirstGlamTitle),
            Loc.T(L.Prism.FirstGlamHint), Loc.T(L.Prism.PostGlam), ButtonStyle.Solid, scale, out var post);
        if (post)
        {
            StartCompose();
        }

        return bottom;
    }

    private float DrawPlacings(ImDrawListPtr drawList, float left, float width, float top, GlamDto[] items,
        GlamProfileDto? profile, float scale)
    {
        var placings = profile?.Placings ?? Array.Empty<GlamPlacingDto>();
        var bottom = DrawGlamTiles(drawList, left, width, top, items, placings, scale);
        var inset = Metrics.Space.Lg * scale;
        var radius = PlacingMedalUnits * scale;
        var pitch = MathF.Max(Metrics.Size.TapTarget * scale, Typography.LineHeight(PrismType.Headline));
        var textLeft = left + inset + radius * 2f + Metrics.Space.Md * scale;
        var glyph = PrismArt.IconUnits * scale;
        var right = left + width - inset - glyph - Metrics.Space.Xs * scale;
        for (var index = 0; index < placings.Length; index++)
        {
            var placing = placings[index];
            if (PlacedGlam(items, placing))
            {
                continue;
            }

            var row = new Rect(new Vector2(left, bottom), new Vector2(left + width, bottom + pitch));
            if (placing.WeekId.Length > 0 && UiInteract.Click(row.Min, row.Max, HoverWash(drawList, row)))
            {
                OpenGallery(placing.WeekId, placing.Track, placing.Title);
            }

            var middle = bottom + pitch * 0.5f;
            DrawPlacingMedal(drawList, new Vector2(left + inset + radius, middle), radius, placing.Rank, false, scale);
            PhoneIcon.Draw(drawList, new Vector2(left + width - inset - glyph * 0.5f, middle), PhoneIcons.ChevronRight,
                PrismArt.Muted, glyph);
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

        return DrawEmpty(drawList, EmptyArea(left, width, top, scale), PhoneIcons.Crown, Loc.T(L.Prism.NoPlacings),
            string.Empty, string.Empty, ButtonStyle.Solid, scale, out _);
    }

    private static bool PlacedGlam(GlamDto[] items, GlamPlacingDto placing)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (Placed(items[index], placing))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Placed(GlamDto glam, GlamPlacingDto placing) =>
        glam.Placing == placing.Rank && glam.WeekId == placing.WeekId &&
        (placing.Track.Length == 0 || (placing.Track == PrismTracks.Vanilla) == glam.Vanilla);

    private float DrawGlamTiles(ImDrawListPtr drawList, float left, float width, float top, GlamDto[] items,
        GlamPlacingDto[]? placings, float scale)
    {
        var gap = Metrics.Space.Xxs * scale;
        var gridWidth = width - gap * 2f;
        var shown = 0;
        for (var index = 0; index < items.Length; index++)
        {
            var glam = items[index];
            if (placings is not null && glam.Placing <= 0)
            {
                continue;
            }

            if (DrawGridTile(drawList, GridCell(left + gap, top, gridWidth, shown++, scale), Cover(glam),
                    placings is null ? glam.Title : PlacingLabel(glam, placings), PhoneIcons.Heart,
                    glam.Likes > 0 ? CountLabel(glam.Likes) : string.Empty, scale))
            {
                OpenGlam(glam);
            }
        }

        var bottom = top + GridHeight(gridWidth, shown, scale);
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

        var bottom = DrawEmpty(drawList, EmptyArea(left, width, top, scale), PhoneIcons.Refresh,
            Loc.T(L.Common.LoadFailed), Loc.T(L.Common.LoadFailedHint), Loc.T(L.Common.Retry), ButtonStyle.Solid, scale,
            out var retry);
        if (retry)
        {
            RetryLane(lane);
        }

        return bottom;
    }

    private static Rect EmptyArea(float left, float width, float top, float scale)
    {
        var at = new Vector2(left, top + Metrics.Space.Xl * scale);
        return new Rect(at, at + new Vector2(width, 0f));
    }

    private void DrawProfileSignedOut(ImDrawListPtr drawList, Rect content, float scale)
    {
        DrawEmpty(drawList, new Rect(content.Min, new Vector2(content.Max.X, content.Max.Y - bottomChrome)),
            PhoneIcons.User, Loc.T(L.Prism.MeSignInTitle), Loc.T(L.Prism.MeSignInHint), Loc.T(L.Prism.OpenSettings),
            ButtonStyle.Solid, scale, out var open);
        if (open)
        {
            navigation.Open(SettingsAppId);
        }
    }

    private static string CountLabel(long value) =>
        value >= CompactFrom ? NumberText.Compact(value) : NumberText.Group(value);

    private string PlacingLabel(GlamDto glam, GlamPlacingDto[] placings)
    {
        SyncLabelLanguage();
        if (placingLabels.TryGetValue(glam.Id, out var label))
        {
            return label;
        }

        for (var index = 0; index < placings.Length; index++)
        {
            if (Placed(glam, placings[index]))
            {
                label = Loc.T(L.Prism.PlacingLine, glam.Placing, placings[index].Title);
                placingLabels[glam.Id] = label;
                return label;
            }
        }

        return glam.Title;
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

    private void DrawGlamList(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        var top = DrawBackHeader(drawList, content, string.Empty, scale);
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
            cursor = DrawGlamTiles(list, origin.X, width, cursor, lane.Items, null, scale);
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
                var key = HashCode.Combine(lane.Items.Length, Loc.Current.Code);
                var count = listCountText.IsCurrent(key)
                    ? listCountText.Value
                    : listCountText.Store(key, Loc.Plural(L.Prism.GlamCount, lane.Items.Length));
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
            var key = HashCode.Combine(item.GlamCount, Loc.Current.Code);
            var count = wornCountText.IsCurrent(key)
                ? wornCountText.Value
                : wornCountText.Store(key, Loc.Plural(L.Prism.WornInGlams, item.GlamCount));
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
            TryOnPiece(new GlamGearDto(gameData.EquipSlot(listItemId), listItemId), string.Empty);
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
