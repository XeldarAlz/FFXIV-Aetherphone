using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private enum HomeHeroAction : byte
    {
        None,
        PlayDaily,
        PlaySpotlight,
        OpenTogether,
    }

    private const string HomeNavId = "games.home.nav";
    private const string HomeFeaturedAnchor = "games.featured";
    private const string HomeRecentRailId = "##games.rail.recent";
    private const string HomeLatestRailId = "##games.rail.latest";
    private const string HomeLatestSeeAllId = "games.seeAll.latest";
    private const string HomeDailyPlayId = "games.hero.daily.play";
    private const string HomeSpotlightPlayId = "games.hero.spotlight.play";
    private const string HomeTogetherStartId = "games.hero.together.start";
    private const string HomeJoinId = "games.home.join";
    private const string HomeRoomCardId = "games.home.room";
    private const string HomeBrowseCardId = "games.home.browse";
    private const string HomeTogetherAccentId = "uno";
    private const string HomeSeparator = " · ";
    private const float HeroPad = 18f;
    private const float HeroCapsuleInset = 14f;
    private const float HeroCapsuleHeight = 24f;
    private const float HeroCapsuleFill = 0.32f;
    private const float HeroIconSide = 52f;
    private const float HeroButtonMinWidth = 96f;
    private const float HeroArtFraction = 0.58f;
    private const float HeroSpotlightIcon = 112f;
    private const float HeroGlow = 0.35f;
    private const float HeroGlowLift = 0.45f;
    private const float HeroScrim = 0.6f;
    private const float HeroTogetherIcon = 56f;
    private const float HeroTogetherPitch = 0.72f;
    private const int HeroTogetherIcons = 4;
    private const float HeroTogetherLighten = 0.16f;
    private const float HeroTogetherDarken = 0.55f;
    private const float HeroStatusAlpha = 0.78f;
    private const float HeroHookAlpha = 0.82f;
    private const float HomeCardPad = 16f;
    private const float HomeBrowseHeight = 56f;
    private const float HomeChevron = 14f;
    private const float HomeAppearDone = 0.999f;
    private const int HomeShelfGenres = (int)GameGenre.Friends;

    private static readonly string[] HomeGenreRailIds =
    [
        "##games.rail.arcade", "##games.rail.action", "##games.rail.puzzle", "##games.rail.brain",
        "##games.rail.strategy", "##games.rail.tabletop",
    ];

    private static readonly string[] HomeGenreSeeAllIds =
    [
        "games.seeAll.arcade", "games.seeAll.action", "games.seeAll.puzzle", "games.seeAll.brain",
        "games.seeAll.strategy", "games.seeAll.tabletop",
    ];

    private readonly HeroCarousel homeHero = new();
    private readonly PosterCard homePosters = new();
    private readonly SnapRail homeRecentRail = new();
    private readonly SnapRail homeLatestRail = new();
    private readonly ShelfColumns[] homeShelves =
        [new ShelfColumns(), new ShelfColumns(), new ShelfColumns(), new ShelfColumns(), new ShelfColumns(),
            new ShelfColumns()];
    private LivePreview? homePreview;
    private Backdrop[]? homePresets;
    private DailyCountdown homeCountdown;
    private ClampedLines homeSpotlightHook;
    private Spring homeAppear;
    private HubGround homeGround;
    private float homeClipTop;
    private float homeClipBottom;
    private int homeHeroVersion = -1;
    private int homeHeroDay = -1;
    private int homeHeroFeatured = -1;
    private bool homeHeroSignedIn;
    private int homeSpotlight = -1;
    private string homeStatus = string.Empty;
    private string homeStatusCountdown = string.Empty;
    private int homeStatusVersion = -1;
    private int homeStatusFeatured = -1;
    private string homeRoomsLabel = string.Empty;
    private int homeRoomsCount = -1;
    private LanguageInfo? homeRoomsLanguage;
    private string homeBrowseLabel = string.Empty;
    private int homeBrowseCount = -1;
    private LanguageInfo? homeBrowseLanguage;
    private GameRoomCardDto? homeRoom;
    private string homeRoomSubtitle = string.Empty;
    private LanguageInfo? homeRoomLanguage;

    private void ResetHome()
    {
        homeHero.Reset();
        homeRecentRail.Reset();
        homeLatestRail.Reset();
        for (var index = 0; index < homeShelves.Length; index++)
        {
            homeShelves[index].Reset();
        }

        homeHeroVersion = -1;
        homeStatusVersion = -1;
        homeAppear.Launch(0f, TransitionTiming.LaunchVelocity(Motion.Appear));
    }

    private void DrawHome(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = origin.X;
            homeGround = new HubGround(ui.Palette, SceneChrome.ScreenFrom(context.Content, ui.Theme, scale));
            homeClipTop = drawList.GetClipRectMin().Y;
            homeClipBottom = drawList.GetClipRectMax().Y;
            var y = DrawWhatsNew(drawList, left, origin.Y, width, scale);
            y = DrawHomeHero(drawList, left, y, width, scale) + Metrics.Space.Md * scale;
            y = DrawHomeJoin(left, y, width, scale);
            y = DrawHomeRoom(drawList, left, y, width, scale);
            y = DrawHomeRecent(drawList, left, y, width, scale);
            y = DrawHomeLatest(drawList, left, y, width, scale);
            y = DrawTopThisWeek(left, y, width, scale);
            y = DrawHomeShelves(drawList, left, y, width, scale);
            y = DrawHomeBrowse(drawList, left, y, width, scale);
            DrawHomeAppear(drawList);
            FinishPage(origin, width, y, scale);
            if (HomeSwiping())
            {
                surface.CancelDrag();
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, HomeNavId, DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private bool HomeOutOfView(float top, float bottom) => bottom < homeClipTop || top > homeClipBottom;

    private static float HomeHeaderHeight(float scale) => (GamesHubArt.SectionHeight + HubMetrics.HeaderGap) * scale;

    private bool HomeSwiping()
    {
        if (homeHero.Dragging || homeRecentRail.Swiping || homeLatestRail.Swiping)
        {
            return true;
        }

        for (var index = 0; index < homeShelves.Length; index++)
        {
            if (homeShelves[index].Swiping)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawHomeAppear(ImDrawListPtr drawList)
    {
        var appear = homeAppear.Step(1f, Motion.Appear, frameSeconds);
        if (appear >= HomeAppearDone)
        {
            return;
        }

        var min = drawList.GetClipRectMin();
        var max = drawList.GetClipRectMax();
        var veil = 1f - Math.Clamp(appear, 0f, 1f);
        var topColor = ImGui.GetColorU32(homeGround.At(min.Y) with { W = veil });
        var bottomColor = ImGui.GetColorU32(homeGround.At(max.Y) with { W = veil });
        drawList.AddRectFilledMultiColor(min, max, topColor, topColor, bottomColor, bottomColor);
    }

    private Backdrop HomePreset(int entryIndex)
    {
        homePresets ??= BuildHomePresets();
        return homePresets[entryIndex];
    }

    private Backdrop[] BuildHomePresets()
    {
        var presets = new Backdrop[library.Entries.Length];
        for (var index = 0; index < presets.Length; index++)
        {
            ref readonly var entry = ref library.Entries[index];
            presets[index] = entry.Online ? Backdrop.Felt : games[entry.GameIndex].Spec.Backdrop;
        }

        return presets;
    }

    private float DrawHomeHero(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cardHeight = HeroCarousel.CardHeight(width, scale);
        var bottom = top + HeroCarousel.BlockHeight(width, scale);
        if (HomeOutOfView(top, bottom))
        {
            return bottom;
        }

        SyncHeroPages();
        var rail = new Rect(new Vector2(left, top), new Vector2(left + width, top + cardHeight));
        var stride = HeroCarousel.Stride(width, scale);
        var quiet = currentGame is not null || LaunchActive || router.IsTransitioning;
        homeHero.Update(rail, stride, quiet, UiAnchors.Recording, frameSeconds);
        GamesHubArt.ReportAnchor(HomeFeaturedAnchor, rail);
        var live = !quiet && !HomeOutOfView(rail.Min.Y, rail.Max.Y);
        var action = HomeHeroAction.None;
        var source = default(Rect);
        var rim = new Vector2(Metrics.Stroke.Ring * scale);
        drawList.PushClipRect(rail.Min - rim, rail.Max + rim, true);
        for (var index = 0; index < homeHero.Count; index++)
        {
            var cardLeft = homeHero.CardLeft(index, left, stride);
            if (cardLeft >= rail.Max.X || cardLeft + width <= rail.Min.X)
            {
                continue;
            }

            var card = new Rect(new Vector2(cardLeft, top), new Vector2(cardLeft + width, top + cardHeight));
            var hit = new Rect(Vector2.Max(card.Min, rail.Min), Vector2.Min(card.Max, rail.Max));
            var picked = homeHero.PageAt(index) switch
            {
                HeroPage.Spotlight => DrawHeroSpotlight(drawList, card, hit, scale),
                HeroPage.Together => DrawHeroTogether(drawList, card, hit, scale),
                _ => DrawHeroDaily(drawList, card, hit, live, scale),
            };
            if (picked == HomeHeroAction.None)
            {
                continue;
            }

            action = picked;
            source = card;
        }

        drawList.PopClipRect();
        homeHero.DrawDots(drawList,
            new Vector2(left + width * 0.5f, rail.Max.Y + (HeroCarousel.DotsGap + HeroCarousel.DotsRow * 0.5f) * scale),
            width, ui.TitleInk);
        RunHeroAction(action, source);
        return bottom;
    }

    private void RunHeroAction(HomeHeroAction action, Rect source)
    {
        switch (action)
        {
            case HomeHeroAction.PlayDaily:
                OpenGame(games[featuredIndex], source);
                break;
            case HomeHeroAction.PlaySpotlight:
                Activate(homeSpotlight, source);
                break;
            case HomeHeroAction.OpenTogether:
                OpenOnlineHub(string.Empty);
                break;
            default:
                break;
        }
    }

    private void SyncHeroPages()
    {
        var today = GameStatsStore.TodayIndex;
        var signedIn = gameRooms.AccountId.Length > 0;
        if (homeHeroVersion == library.Version && homeHeroDay == today && homeHeroFeatured == featuredIndex
            && homeHeroSignedIn == signedIn)
        {
            return;
        }

        homeHeroVersion = library.Version;
        homeHeroDay = today;
        homeHeroFeatured = featuredIndex;
        homeHeroSignedIn = signedIn;
        homeSpotlight = PickHeroSpotlight(today);
        homeHero.SetPages(homeSpotlight >= 0, signedIn);
    }

    private int PickHeroSpotlight(int today)
    {
        var latest = library.Latest;
        var candidates = 0;
        for (var position = 0; position < latest.Length; position++)
        {
            if (HeroSpotlightEligible(latest[position]))
            {
                candidates++;
            }
        }

        var slot = HeroCarousel.SpotlightSlot(today, candidates);
        for (var position = 0; position < latest.Length && slot >= 0; position++)
        {
            if (!HeroSpotlightEligible(latest[position]))
            {
                continue;
            }

            if (slot == 0)
            {
                return latest[position];
            }

            slot--;
        }

        return -1;
    }

    private bool HeroSpotlightEligible(int entryIndex) =>
        !library.Entries[entryIndex].Online && entryIndex != featuredIndex && library.Hook(entryIndex).Length > 0;

    private HomeHeroAction DrawHeroDaily(ImDrawListPtr drawList, Rect card, Rect hit, bool live, float scale)
    {
        var game = games[featuredIndex];
        var preview = homePreview ??= new LivePreview(stats);
        preview.Prepare(game, GameStatsStore.TodayIndex);
        var done = stats.DailyDone;
        var label = Loc.T(done ? L.Games.PlayAgain : L.Games.Play);
        var button = HeroButtonRect(card, label, scale);
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var hookHeight = Typography.LineHeight(TextStyles.Subheadline);
        var iconSide = HeroIconSide * scale;
        var blockHeight = MathF.Max(iconSide, titleHeight + hookHeight);
        var blockTop = button.Min.Y - Metrics.Space.Md * scale - blockHeight;
        var art = new Rect(new Vector2(card.Min.X, card.Min.Y + (HeroCapsuleInset + HeroCapsuleHeight) * scale),
            new Vector2(card.Max.X, blockTop));
        var interactive = !homeHero.Dragging;
        var inside = UiInteract.Hover(hit.Min, hit.Max);
        var overButton = interactive && UiInteract.Hover(button.Min, button.Max);
        var hovered = interactive && inside && !overButton;
        preview.Draw(drawList, card, art, ui.Theme, live, inside, frameSeconds, homeGround, scale);
        DrawHeroCapsules(drawList, card, done, scale);
        var pad = HeroPad * scale;
        var iconMin = new Vector2(card.Min.X + pad, blockTop + (blockHeight - iconSide) * 0.5f);
        GameIconArt.Draw(drawList, game.Id, game.Accent, iconMin, iconMin + new Vector2(iconSide),
            IconAppearance.Default, true);
        var textLeft = iconMin.X + iconSide + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, card.Max.X - pad - textLeft);
        var textTop = blockTop + (blockHeight - titleHeight - hookHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(game.Title, textWidth, TextStyles.Title2), PosterCard.White, TextStyles.Title2);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
            Typography.FitText(library.Hook(featuredIndex), textWidth, TextStyles.Subheadline),
            PosterCard.White with { W = HeroHookAlpha }, TextStyles.Subheadline);
        var play = Button.Draw(drawList, button, label, ui.Ink.WithAccent(game.Accent),
            done ? ButtonStyle.Gray : ButtonStyle.Prominent, id: HomeDailyPlayId);
        DrawHeroStatus(drawList, card, button, HeroStatus(), scale);
        return HeroTap(hit, hovered, play && interactive, HomeHeroAction.PlayDaily);
    }

    private HomeHeroAction DrawHeroSpotlight(ImDrawListPtr drawList, Rect card, Rect hit, float scale)
    {
        var entry = homeSpotlight;
        var accent = library.Accent(entry);
        homePosters.DrawBackground(drawList, card, HomePreset(entry), accent, 0f, HeroScrim, scale);
        var label = Loc.T(L.Games.Play);
        var button = HeroButtonRect(card, label, scale);
        var pad = HeroPad * scale;
        var textWidth = MathF.Max(1f, card.Width - pad * 2f);
        homeSpotlightHook.Update(library.Hook(entry), textWidth, TextStyles.Subheadline);
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var hookHeight = Typography.LineHeight(TextStyles.Subheadline);
        var hookTop = button.Min.Y - Metrics.Space.Md * scale - homeSpotlightHook.Count * hookHeight;
        var titleTop = hookTop - titleHeight;
        var eyebrowTop = titleTop - eyebrowHeight;
        var interactive = !homeHero.Dragging;
        var overButton = interactive && UiInteract.Hover(button.Min, button.Max);
        var hovered = interactive && !overButton && UiInteract.Hover(hit.Min, hit.Max);
        DrawHeroArtIcon(drawList, card, eyebrowTop, library.IconIds[entry], accent, scale);
        var textLeft = card.Min.X + pad;
        Typography.Draw(drawList, new Vector2(textLeft, eyebrowTop),
            Typography.FitText(library.Eyebrow(entry), textWidth, TextStyles.FootnoteEmphasized),
            PosterCard.White with { W = HeroStatusAlpha }, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, titleTop),
            Typography.FitText(library.Title(entry), textWidth, TextStyles.Title2), PosterCard.White,
            TextStyles.Title2);
        var hookInk = PosterCard.White with { W = HeroHookAlpha };
        Typography.Draw(drawList, new Vector2(textLeft, hookTop), homeSpotlightHook.First, hookInk,
            TextStyles.Subheadline);
        if (homeSpotlightHook.Count > 1)
        {
            Typography.Draw(drawList, new Vector2(textLeft, hookTop + hookHeight), homeSpotlightHook.Second, hookInk,
                TextStyles.Subheadline);
        }

        LivePreview.CoverCorners(drawList, card, HubMetrics.CardRadius * scale, homeGround, scale);
        var play = Button.Draw(drawList, button, label, ui.Ink.WithAccent(accent), ButtonStyle.Prominent,
            id: HomeSpotlightPlayId);
        return HeroTap(hit, hovered, play && interactive, HomeHeroAction.PlaySpotlight);
    }

    private HomeHeroAction DrawHeroTogether(ImDrawListPtr drawList, Rect card, Rect hit, float scale)
    {
        var accent = AppAccents.For(HomeTogetherAccentId);
        var radius = HubMetrics.CardRadius * scale;
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, radius,
            ImGui.GetColorU32(Palette.Lighten(accent, HeroTogetherLighten)),
            ImGui.GetColorU32(Palette.Darken(accent, HeroTogetherDarken)));
        LivePreview.Rim(drawList, card, radius, scale);
        var label = Loc.T(L.GamesHub.StartRoom);
        var button = HeroButtonRect(card, label, scale);
        var rooms = gameRooms.Rooms.Length;
        var pad = HeroPad * scale;
        var textWidth = MathF.Max(1f, card.Width - pad * 2f);
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var hintHeight = rooms > 0 ? 0f : Typography.LineHeight(TextStyles.Subheadline);
        var titleTop = button.Min.Y - Metrics.Space.Md * scale - hintHeight - titleHeight;
        var interactive = !homeHero.Dragging;
        var overButton = interactive && UiInteract.Hover(button.Min, button.Max);
        var hovered = interactive && !overButton && UiInteract.Hover(hit.Min, hit.Max);
        DrawHeroTogetherIcons(drawList, card, titleTop, scale);
        var textLeft = card.Min.X + pad;
        Typography.Draw(drawList, new Vector2(textLeft, titleTop),
            Typography.FitText(Loc.T(L.Games.OnlineTitle), textWidth, TextStyles.Title2), PosterCard.White,
            TextStyles.Title2);
        if (rooms == 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, titleTop + titleHeight),
                Typography.FitText(Loc.T(L.GamesHub.TogetherHint), textWidth, TextStyles.Subheadline),
                PosterCard.White with { W = HeroHookAlpha }, TextStyles.Subheadline);
        }

        var start = Button.Draw(drawList, button, label, ui.Ink.WithAccent(accent), ButtonStyle.Prominent,
            id: HomeTogetherStartId);
        if (rooms > 0)
        {
            var pill = HomeRoomsLabel(rooms);
            LivePill.Draw(drawList,
                new Vector2(button.Max.X + Metrics.Space.Md * scale, button.Center.Y - LivePill.Height(scale) * 0.5f),
                pill, PosterCard.White, (float)ImGui.GetTime(), scale);
        }

        return HeroTap(hit, hovered, start && interactive, HomeHeroAction.OpenTogether);
    }

    private static void DrawHeroTogetherIcons(ImDrawListPtr drawList, Rect card, float textTop, float scale)
    {
        var kinds = OnlineGameArt.Kinds;
        var count = Math.Min(HeroTogetherIcons, kinds.Length);
        var area = MathF.Min(card.Height * HeroArtFraction, textTop - Metrics.Space.Md * scale - card.Min.Y);
        var side = MathF.Min(HeroTogetherIcon * scale, area - Metrics.Space.Xs * 2f * scale);
        if (side <= 0f || count == 0)
        {
            return;
        }

        var pitch = side * HeroTogetherPitch;
        var span = side + (count - 1) * pitch;
        var top = card.Min.Y + (area - side) * 0.5f;
        var startX = card.Center.X - span * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(startX + index * pitch, top);
            GameIconArt.Draw(drawList, OnlineGameArt.AccentId(kinds[index]), OnlineGameArt.Accent(kinds[index]), min,
                min + new Vector2(side), IconAppearance.Default, true);
        }
    }

    private static void DrawHeroArtIcon(ImDrawListPtr drawList, Rect card, float textTop, string iconId,
        Vector4 accent, float scale)
    {
        var area = MathF.Min(card.Height * HeroArtFraction, textTop - Metrics.Space.Md * scale - card.Min.Y);
        var side = MathF.Min(HeroSpotlightIcon * scale, area - Metrics.Space.Xs * 2f * scale);
        if (side <= 0f)
        {
            return;
        }

        var center = new Vector2(card.Center.X, card.Min.Y + area * 0.5f);
        var half = new Vector2(side * 0.5f);
        ProgressRing.Glow(center, side * 0.5f, Palette.Lighten(accent, HeroGlowLift), HeroGlow);
        GameIconArt.Draw(drawList, iconId, accent, center - half, center + half, IconAppearance.Default, true);
    }

    private static Rect HeroButtonRect(Rect card, string label, float scale)
    {
        var pad = HeroPad * scale;
        var height = Button.RegularHeight * scale;
        var width = MathF.Max(HeroButtonMinWidth * scale, Button.WidthFor(label, ButtonSize.Regular));
        return new Rect(new Vector2(card.Min.X + pad, card.Max.Y - pad - height),
            new Vector2(card.Min.X + pad + width, card.Max.Y - pad));
    }

    private static HomeHeroAction HeroTap(Rect hit, bool hovered, bool played, HomeHeroAction action)
    {
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tapped = UiInteract.Click(hit.Min, hit.Max, hovered);
        return tapped || played ? action : HomeHeroAction.None;
    }

    private void DrawHeroCapsules(ImDrawListPtr drawList, Rect card, bool done, float scale)
    {
        var inset = HeroCapsuleInset * scale;
        var height = HeroCapsuleHeight * scale;
        var right = card.Max.X - inset;
        var streak = stats.DailyStreak;
        if (streak > 0)
        {
            var glyph = done ? PhoneIcons.CircleCheckFilled : PhoneIcons.FlameFilled;
            var glyphInk = done ? PosterCard.White : HubMetrics.Ember;
            right -= PosterCard.DrawCapsule(drawList, new Vector2(right, card.Min.Y + inset), true,
                GameNumber.Label(streak), glyph, glyphInk, height, HeroCapsuleFill) + Metrics.Space.Sm * scale;
        }

        var available = right - card.Min.X - inset - PosterCard.CapsuleWidth(string.Empty, string.Empty, height);
        var daily = Typography.FitText(Loc.T(L.Games.Daily), MathF.Max(1f, available), TextStyles.FootnoteEmphasized);
        PosterCard.DrawCapsule(drawList, card.Min + new Vector2(inset), false, daily, string.Empty, PosterCard.White,
            height, HeroCapsuleFill);
    }

    private static void DrawHeroStatus(ImDrawListPtr drawList, Rect card, Rect button, string status, float scale)
    {
        if (status.Length == 0)
        {
            return;
        }

        var left = button.Max.X + Metrics.Space.Md * scale;
        var width = card.Max.X - HeroPad * scale - left;
        if (width <= 0f)
        {
            return;
        }

        var height = Typography.LineHeight(TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left, button.Center.Y - height * 0.5f),
            Typography.FitText(status, width, TextStyles.Footnote), PosterCard.White with { W = HeroStatusAlpha },
            TextStyles.Footnote);
    }

    private string HeroStatus()
    {
        var countdown = homeCountdown.Label();
        if (ReferenceEquals(countdown, homeStatusCountdown) && homeStatusVersion == library.Version
            && homeStatusFeatured == featuredIndex)
        {
            return homeStatus;
        }

        homeStatusCountdown = countdown;
        homeStatusVersion = library.Version;
        homeStatusFeatured = featuredIndex;
        var hasRecord = library.BestKind(featuredIndex) != RecordKind.None || library.StarMax(featuredIndex) > 0;
        var progress = hasRecord ? library.ProgressLabel(featuredIndex) : string.Empty;
        var rank = library.RankLabel(featuredIndex);
        var status = progress.Length > 0 ? string.Concat(countdown, HomeSeparator, progress) : countdown;
        homeStatus = rank.Length > 0 ? string.Concat(status, HomeSeparator, rank) : status;
        return homeStatus;
    }

    private string HomeRoomsLabel(int count)
    {
        if (count == homeRoomsCount && ReferenceEquals(homeRoomsLanguage, Loc.Current))
        {
            return homeRoomsLabel;
        }

        homeRoomsCount = count;
        homeRoomsLanguage = Loc.Current;
        homeRoomsLabel = Loc.Plural(L.Games.OnlineRoomsOpen, count);
        return homeRoomsLabel;
    }

    private float DrawHomeJoin(float left, float top, float width, float scale) =>
        WhatsNewNotice.ShowsJoinCard(ShowsConsentCompact, whatsNewShowing)
            ? DrawConsentCompact(left, top, width, scale, HomeJoinId) + Metrics.Space.Md * scale
            : top;

    private float DrawHomeRoom(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        if (gameRooms.AccountId.Length == 0 || gameRooms.Rooms.Length == 0)
        {
            return top;
        }

        var room = gameRooms.Rooms[0];
        var rect = new Rect(new Vector2(left, top), new Vector2(left + width, top + RoomCard.Height * scale));
        if (!HomeOutOfView(rect.Min.Y, rect.Max.Y))
        {
            ImGui.PushID(HomeRoomCardId);
            var entered = RoomCard.Draw(drawList, ui, rect, room, Loc.T(GamesOnlineText.GameName(room.GameKind)),
                HomeRoomSubtitle(room), Initials.Of(room.OwnerName), scale);
            ImGui.PopID();
            if (entered)
            {
                gameRooms.Enter(room.RoomId);
                OpenOnlineRoom(room.RoomId, room.GameKind);
            }
        }

        return rect.Max.Y + Metrics.Space.Md * scale;
    }

    private string HomeRoomSubtitle(GameRoomCardDto room)
    {
        if (ReferenceEquals(room, homeRoom) && ReferenceEquals(homeRoomLanguage, Loc.Current))
        {
            return homeRoomSubtitle;
        }

        homeRoom = room;
        homeRoomLanguage = Loc.Current;
        homeRoomSubtitle = Loc.T(L.GamesHub.RoomOf, room.OwnerName);
        return homeRoomSubtitle;
    }

    private float DrawHomeRecent(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var recent = library.Recent;
        if (recent.Length == 0)
        {
            return top;
        }

        var tilesTop = top + HomeHeaderHeight(scale);
        var tileHeight = PosterCard.ResumeTileHeight(scale);
        var bottom = tilesTop + tileHeight;
        if (HomeOutOfView(top, bottom))
        {
            return bottom + HubMetrics.SectionGap * scale;
        }

        GamesHubArt.Section(drawList, ui, left, top, width, Loc.T(L.GamesHub.ContinuePlaying), string.Empty,
            string.Empty);
        var itemWidth = PosterCard.ResumeWidth * scale;
        var gap = PosterCard.ResumeGap * scale;
        var stride = itemWidth + gap;
        var contentWidth = SnapRail.ContentWidth(recent.Length, itemWidth, gap, HubMetrics.RailBleed * scale);
        var row = SnapRail.Row(left, tilesTop, width, tileHeight, scale);
        homeRecentRail.Begin(drawList, HomeRecentRailId, row, left, width, contentWidth, stride);
        ImGui.PushID(HomeRecentRailId);
        var interactive = homeRecentRail.TapAllowed;
        var tapped = -1;
        var source = default(Rect);
        for (var position = 0; position < recent.Length; position++)
        {
            var x = left + position * stride - homeRecentRail.Offset;
            if (x > row.Max.X || x + itemWidth < row.Min.X)
            {
                continue;
            }

            var entry = recent[position];
            if (homePosters.DrawResume(drawList, ui, library, entry, HomePreset(entry), new Vector2(x, tilesTop),
                    entry == featuredIndex, interactive, homeGround, out var card))
            {
                tapped = entry;
                source = card;
            }
        }

        ImGui.PopID();
        homeRecentRail.End(drawList, row, contentWidth, ui, stride);
        if (tapped >= 0)
        {
            Activate(tapped, source);
        }

        return bottom + HubMetrics.SectionGap * scale;
    }

    private float DrawHomeLatest(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var latest = library.Latest;
        var count = Math.Min(latest.Length, PosterCard.EditorialSlots);
        if (count == 0)
        {
            return top;
        }

        var cardsTop = top + HomeHeaderHeight(scale);
        var cardHeight = PosterCard.EditorialHeight * scale;
        var bottom = cardsTop + cardHeight;
        if (HomeOutOfView(top, bottom))
        {
            return bottom + HubMetrics.SectionGap * scale;
        }

        if (GamesHubArt.Section(drawList, ui, left, top, width, Loc.T(L.GamesHub.JustAdded),
                Loc.T(L.GamesHub.SeeAll), HomeLatestSeeAllId))
        {
            router.Push(GamesRoute.ShelfOf(GamesShelf.New));
        }

        var itemWidth = PosterCard.EditorialWidth(width);
        var gap = PosterCard.EditorialGap * scale;
        var stride = itemWidth + gap;
        var contentWidth = SnapRail.ContentWidth(count, itemWidth, gap, HubMetrics.RailBleed * scale);
        var row = SnapRail.Row(left, cardsTop, width, cardHeight, scale);
        homeLatestRail.Begin(drawList, HomeLatestRailId, row, left, width, contentWidth, stride);
        ImGui.PushID(HomeLatestRailId);
        var interactive = homeLatestRail.TapAllowed;
        var tapped = -1;
        var source = default(Rect);
        for (var position = 0; position < count; position++)
        {
            var x = left + position * stride - homeLatestRail.Offset;
            if (x > row.Max.X || x + itemWidth < row.Min.X)
            {
                continue;
            }

            var entry = latest[position];
            if (homePosters.DrawEditorial(drawList, ui, library, entry, position, HomePreset(entry),
                    new Vector2(x, cardsTop), itemWidth, interactive, homeGround, out var card))
            {
                tapped = entry;
                source = card;
            }
        }

        ImGui.PopID();
        homeLatestRail.End(drawList, row, contentWidth, ui, stride);
        if (tapped >= 0)
        {
            Activate(tapped, source);
        }

        return bottom + HubMetrics.SectionGap * scale;
    }

    private float DrawHomeShelves(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var order = library.GenreOrder;
        var y = top;
        for (var slot = 0; slot < order.Length; slot++)
        {
            var genre = order[slot];
            var genreIndex = (int)genre;
            var entries = library.Genre(genre);
            if (entries.Length == 0 || genreIndex >= HomeShelfGenres)
            {
                continue;
            }

            var rowsTop = y + HomeHeaderHeight(scale);
            var bottom = rowsTop + ShelfColumns.Height(scale);
            if (!HomeOutOfView(y, bottom))
            {
                DrawHomeShelf(drawList, genre, entries, left, y, rowsTop, width);
            }

            y = bottom + HubMetrics.SectionGap * scale;
        }

        return y;
    }

    private void DrawHomeShelf(ImDrawListPtr drawList, GameGenre genre, ReadOnlySpan<int> entries, float left,
        float top, float rowsTop, float width)
    {
        var genreIndex = (int)genre;
        if (GamesHubArt.Section(drawList, ui, left, top, width, Loc.T(GameGenres.Label(genre)),
                Loc.T(L.GamesHub.SeeAll), HomeGenreSeeAllIds[genreIndex]))
        {
            router.Push(GamesRoute.ShelfOf(GamesRoute.ShelfFor(genre)));
        }

        var tapped = homeShelves[genreIndex].Draw(drawList, ui, library, entries, HomeGenreRailIds[genreIndex], left,
            rowsTop, width, out var source);
        if (tapped >= 0)
        {
            Activate(tapped, source);
        }
    }

    private float DrawHomeBrowse(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var rect = new Rect(new Vector2(left, top), new Vector2(left + width, top + HomeBrowseHeight * scale));
        if (HomeOutOfView(rect.Min.Y, rect.Max.Y))
        {
            return rect.Max.Y;
        }

        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var card = rect.Scaled(PosterCard.Pose(HomeBrowseCardId, hovered));
        var radius = HubMetrics.CardRadius * scale;
        ui.Card(drawList, card.Min, card.Max, radius);
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = HomeCardPad * scale;
        var chevron = HomeChevron * scale;
        PhoneIcon.Draw(drawList, new Vector2(card.Max.X - pad - chevron * 0.5f, card.Center.Y),
            PhoneIcons.ChevronRight, ui.MutedInk, chevron);
        var labelWidth = MathF.Max(1f, card.Width - pad * 2f - chevron - Metrics.Space.Sm * scale);
        var labelHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, card.Center.Y - labelHeight * 0.5f),
            Typography.FitText(HomeBrowseLabel(), labelWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            SelectTab(GamesTab.Library);
        }

        return rect.Max.Y;
    }

    private string HomeBrowseLabel()
    {
        var count = library.Entries.Length;
        if (count == homeBrowseCount && ReferenceEquals(homeBrowseLanguage, Loc.Current))
        {
            return homeBrowseLabel;
        }

        homeBrowseCount = count;
        homeBrowseLanguage = Loc.Current;
        homeBrowseLabel = Loc.Plural(L.GamesHub.BrowseAll, count);
        return homeBrowseLabel;
    }
}
