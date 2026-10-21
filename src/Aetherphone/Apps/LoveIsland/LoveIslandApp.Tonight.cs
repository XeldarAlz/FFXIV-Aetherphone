using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Aetherphone.Windows;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal sealed partial class LoveIslandApp
{
    private const float HeroLogoSide = 56f;
    private const float HeroLogoRim = 3f;
    private const float HeroInset = 14f;
    private const float HeroScrimShare = 0.6f;
    private const float SectionGap = 14f;
    private const int PinnedBodyLines = 3;
    private const int LinkshellBodyLines = 2;
    private const int AboutBodyLines = 3;
    private const string StatusCardId = "loveisland.tonight.status";
    private const string LiveCardId = "loveisland.tonight.live";
    private const string ShareCardId = "loveisland.tonight.share";
    private const string PinnedCardId = "loveisland.tonight.pinned";
    private const string PostsCardId = "loveisland.tonight.posts";
    private const string LinkshellsCardId = "loveisland.tonight.linkshells";
    private const string AboutCardId = "loveisland.tonight.about";
    private const string EndedCardId = "loveisland.tonight.ended";
    private const string TeleportButtonId = "loveisland.tonight.teleport";
    private const string WatchButtonId = "loveisland.tonight.watch";
    private const string TwitchButtonId = "loveisland.tonight.twitch";
    private const string PostButtonId = "loveisland.tonight.post";
    private const string GalleryButtonId = "loveisland.tonight.gallery";
    private const string NightToggleId = "loveisland.tonight.night";
    private const string TwitchHostPrefix = "https://twitch.tv/";

    private readonly ChipRail linkRail = new();
    private readonly bool[] linkActive = new bool[16];
    private readonly CampaignTextSlot announcementsTitle = new();
    private CachedText postCountText;
    private CountdownText countdown;
    private string? twitchChannelSource;
    private string twitchChannelUrl = string.Empty;
    private string? shareBodySource;
    private string? websiteNoteSource;
    private string shareBodyWithNote = string.Empty;

    private void DrawTonightTab(Rect area)
    {
        var frame = BeginRoot(area, out var context);
        using (var surface = AppSurface.BeginEdgeToEdge(frame.Body))
        {
            ConsumeScrollTop(surface);
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawHero(drawList, origin, width, scale);
            var left = LeftOf(origin.X);
            var inner = InnerWidth(width);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, cursorY + SectionGap * scale));
            if (DrawLoadState(frame.Body, out var stateBottom))
            {
                Reserve(origin, width, stateBottom);
            }
            else
            {
                cursorY = DrawTonightCards(drawList, left, cursorY + SectionGap * scale, inner, scale);
                Reserve(origin, width, cursorY);
                cursorY = DrawLinkRail(cursorY + SectionGap * scale);
                cursorY = DrawAboutCard(drawList, left, cursorY + SectionGap * scale, inner);
                Reserve(origin, width, cursorY + Metrics.Space.Xl * scale);
            }
        }

        EndRoot(in frame, in context, "loveisland.nav.tonight");
    }

    private float DrawHero(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var campaign = frameCampaign;
        var height = MathF.Min(width * LoveIslandKit.HeroAspect, LoveIslandKit.HeroMaxHeight * scale);
        var hero = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        UiAnchors.Report("loveisland.hero", hero);
        LoveIslandKit.CoverImage(drawList, images, campaign?.HeroUrl, hero, 0f, ui.Accent);
        MediaOverlay.BottomScrim(drawList, hero.Min, hero.Max, HeroScrimShare);
        var inset = LoveIslandKit.PadX * scale;
        var side = HeroLogoSide * scale;
        var logo = new Rect(new Vector2(hero.Min.X + inset, hero.Max.Y - HeroInset * scale - side),
            new Vector2(hero.Min.X + inset + side, hero.Max.Y - HeroInset * scale));
        var rim = HeroLogoRim * scale;
        var logoRadius = side * Metrics.Radius.TileFactor;
        Squircle.Fill(drawList, logo.Min - new Vector2(rim, rim), logo.Max + new Vector2(rim, rim), logoRadius + rim,
            ImGui.GetColorU32(MediaOverlay.Fill));
        var logoUrl = campaign?.LogoUrl;
        var logoTexture = string.IsNullOrEmpty(logoUrl) ? null : images.Sized(logoUrl, side * LoveIslandKit.ThumbOversample);
        if (logoTexture is null)
        {
            Squircle.Fill(drawList, logo.Min, logo.Max, logoRadius, ImGui.GetColorU32(ui.Accent));
            PhoneIcon.Draw(drawList, logo.Center, PhoneIcons.MoonStars, LoveIslandKit.White, side * 0.5f);
        }
        else
        {
            var (uv0, uv1) = ImageFit.CoverSquare(logoTexture.Size);
            Squircle.FillImage(drawList, logo.Min, logo.Max, logoRadius, logoTexture.Handle, 0xFFFFFFFFu, uv0, uv1);
        }

        var textLeft = logo.Max.X + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, hero.Max.X - inset - textLeft);
        var windowLine = labels.WindowLine;
        var lineHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var showName = string.IsNullOrEmpty(campaign?.HeroUrl);
        var nameHeight = showName ? Typography.LineHeight(TextStyles.Title3) : 0f;
        var blockHeight = nameHeight + (windowLine.Length > 0 ? lineHeight : 0f);
        var cursorY = logo.Center.Y - blockHeight * 0.5f;
        if (showName)
        {
            Typography.Draw(drawList, new Vector2(textLeft, cursorY),
                Typography.FitText(DisplayName, textWidth, TextStyles.Title3), LoveIslandKit.White, TextStyles.Title3);
            cursorY += nameHeight;
        }

        if (windowLine.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, cursorY),
                Typography.FitText(windowLine, textWidth, TextStyles.FootnoteEmphasized),
                LoveIslandKit.White with { W = 0.85f }, TextStyles.FootnoteEmphasized);
        }

        return hero.Max.Y;
    }

    private float DrawTonightCards(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var campaign = frameCampaign!;
        var moment = CampaignCountdown.Evaluate(campaign, nowUnix);
        var gap = LoveIslandKit.CardGap * scale;
        var cursorY = DrawStatusCard(drawList, left, top, width, campaign, in moment);
        cursorY = DrawNightRow(drawList, left, cursorY + gap, width, in moment);
        cursorY = DrawLiveCard(drawList, left, cursorY + gap, width, campaign, in moment);
        cursorY = DrawShareCard(drawList, left, cursorY + gap, width);
        cursorY = DrawPinnedCard(drawList, left, cursorY + gap, width);
        cursorY = DrawPostsCard(drawList, left, cursorY + gap, width);
        return DrawLinkshellsCard(drawList, left, cursorY + gap, width, campaign);
    }

    private float DrawStatusCard(ImDrawListPtr drawList, float left, float top, float width, CampaignDto campaign,
        in CampaignMoment moment)
    {
        var texts = Texts;
        if (moment.Phase == CampaignPhase.Ended)
        {
            return DrawCard(drawList, left, top, width, new CardContent(EndedCardId, DisplayName, CardPill.None,
                string.Empty, texts.T(CampaignTextKeys.EndedTitle), texts.T(CampaignTextKeys.EndedBody), 0, 0, false), 0,
                out _);
        }

        if (moment.Phase == CampaignPhase.Before)
        {
            var when = moment.NextIndex >= 0 ? labels.Experiences[moment.NextIndex].DoorsLine : labels.StartsLine;
            var bottom = DrawCard(drawList, left, top, width, new CardContent(StatusCardId,
                texts.T(CampaignTextKeys.DoorsOpenIn), CardPill.None, string.Empty,
                countdown.Get(moment.SecondsToStart), when, 1, 0, true, true), 0, out var action);
            if (action == CardTapped)
            {
                SelectTab(LoveIslandTab.Experiences);
            }

            return bottom;
        }

        var live = moment.CurrentIndex >= 0 ? campaign.Experiences[moment.CurrentIndex] : null;
        var buttonCount = 0;
        if (live is not null)
        {
            cardButtons[buttonCount++] = new CardButton(Loc.T(L.Maps.Teleport), true, TeleportButtonId);
            if (!string.IsNullOrEmpty(live.TwitchUrl))
            {
                cardButtons[buttonCount++] = new CardButton(texts.T(CampaignTextKeys.WatchTwitch), false, WatchButtonId);
            }
        }

        var title = live is null ? texts.T(CampaignTextKeys.BetweenSets) : live.Title;
        var line = live is not null
            ? labels.Experiences[moment.CurrentIndex].LiveLine
            : moment.NextIndex >= 0 ? labels.Experiences[moment.NextIndex].NextLine : texts.T(CampaignTextKeys.TabExperiences);
        var liveBottom = DrawCard(drawList, left, top, width, new CardContent(StatusCardId, string.Empty, CardPill.Live,
            texts.T(CampaignTextKeys.LiveNow), title, line, 0, 0, true), buttonCount, out var liveAction);
        if (liveAction == CardTapped)
        {
            if (live is null)
            {
                SelectTab(LoveIslandTab.Experiences);
            }
            else
            {
                OpenExperience(live.Id);
            }
        }
        else if (liveAction == 0 && live is not null)
        {
            Teleport(live, labels.Experiences[moment.CurrentIndex]);
        }
        else if (liveAction == 1 && live is not null)
        {
            UrlActions.AskThenOpen(live.TwitchUrl!);
        }

        return liveBottom;
    }

    private float DrawNightRow(ImDrawListPtr drawList, float left, float top, float width, in CampaignMoment moment)
    {
        var texts = Texts;
        var hint = texts.T(moment.WindowOpen ? CampaignTextKeys.KeepNightHintLive : CampaignTextKeys.KeepNightHintBefore);
        var current = Ledger.KeepNight;
        var bottom = DrawToggleRow(drawList, left, top, width, NightToggleId, PhoneIcons.Moon,
            texts.T(CampaignTextKeys.KeepNight), hint, current, out var next);
        UiAnchors.Report("loveisland.night", new Rect(new Vector2(left, top), new Vector2(left + width, bottom)));
        if (next == current)
        {
            return bottom;
        }

        Ledger.SetKeepNight(next);
        toast.Show(Loc.T(next ? L.LoveIsland.NightOn : L.LoveIsland.NightOff));
        return bottom;
    }

    private float DrawLiveCard(ImDrawListPtr drawList, float left, float top, float width, CampaignDto campaign,
        in CampaignMoment moment)
    {
        var texts = Texts;
        var onAir = campaign.LiveOnAir;
        var channel = campaign.LiveChannel ?? string.Empty;
        string title;
        string body;
        if (onAir)
        {
            title = string.IsNullOrWhiteSpace(campaign.LiveTitle) ? Loc.T(L.LoveIsland.LiveSet) : campaign.LiveTitle;
            body = labels.LiveChannelLine;
        }
        else if (moment.NextStreamedIndex >= 0)
        {
            title = labels.Experiences[moment.NextStreamedIndex].NextUpLine;
            body = labels.Experiences[moment.NextStreamedIndex].StreamLine;
        }
        else
        {
            title = texts.T(CampaignTextKeys.NoStream);
            body = texts.T(CampaignTextKeys.StreamHint);
        }

        var buttonCount = 0;
        if (onAir && channel.Length > 0)
        {
            cardButtons[buttonCount++] = new CardButton(texts.T(CampaignTextKeys.WatchTwitch), true, TwitchButtonId);
        }

        var bottom = DrawCard(drawList, left, top, width, new CardContent(LiveCardId, texts.T(CampaignTextKeys.LiveDj),
            onAir ? CardPill.Live : CardPill.Off, Loc.T(onAir ? L.LoveIsland.OnAir : L.LoveIsland.OffAir), title, body,
            0, 0, false), buttonCount, out var action);
        if (action == 0)
        {
            UrlActions.AskThenOpen(TwitchUrlFor(channel));
        }

        return bottom;
    }

    private string TwitchUrlFor(string channel)
    {
        if (ReferenceEquals(channel, twitchChannelSource))
        {
            return twitchChannelUrl;
        }

        twitchChannelSource = channel;
        twitchChannelUrl = channel.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? channel
            : string.Concat(TwitchHostPrefix, channel);
        return twitchChannelUrl;
    }

    private float DrawShareCard(ImDrawListPtr drawList, float left, float top, float width)
    {
        var texts = Texts;
        cardButtons[0] = new CardButton(texts.T(CampaignTextKeys.PostCta), true, PostButtonId);
        cardButtons[1] = new CardButton(texts.T(CampaignTextKeys.GalleryCta), false, GalleryButtonId);
        var bottom = DrawCard(drawList, left, top, width, new CardContent(ShareCardId,
            texts.T(CampaignTextKeys.ShareTitle), CardPill.None, string.Empty, labels.HashtagTitle,
            ShareBody(texts), 0, 0, false), 2, out var action);
        if (action == 0)
        {
            PostWithHashtag();
        }
        else if (action == 1)
        {
            SelectTab(LoveIslandTab.Gallery);
        }

        return bottom;
    }

    private string ShareBody(CampaignTexts texts)
    {
        var body = texts.T(CampaignTextKeys.ShareBody);
        if (store.Campaign is not { WebsitePhotos: true })
        {
            return body;
        }

        var note = texts.T(CampaignTextKeys.WebsiteNote);
        if (ReferenceEquals(body, shareBodySource) && ReferenceEquals(note, websiteNoteSource))
        {
            return shareBodyWithNote;
        }

        shareBodySource = body;
        websiteNoteSource = note;
        shareBodyWithNote = string.Concat(body, " ", note);
        return shareBodyWithNote;
    }

    private float DrawPinnedCard(ImDrawListPtr drawList, float left, float top, float width)
    {
        var pinned = Ledger.PinnedPost;
        if (pinned is null)
        {
            return top - LoveIslandKit.CardGap * UiScale.Current;
        }

        var bottom = DrawCard(drawList, left, top, width, new CardContent(PinnedCardId,
            Texts.T(CampaignTextKeys.Pinned), CardPill.None, string.Empty, pinned.Title, pinned.Body, 0,
            PinnedBodyLines, true), 0, out var action);
        if (action == CardTapped)
        {
            OpenPost(pinned.Id);
        }

        return bottom;
    }

    private float DrawPostsCard(ImDrawListPtr drawList, float left, float top, float width)
    {
        var texts = Texts;
        var count = store.Posts.Length;
        var countText = postCountText.IsCurrent(count)
            ? postCountText.Value
            : postCountText.Store(count, NumberText.Group(count));
        var title = announcementsTitle.Get(texts, CampaignTextKeys.AnnouncementsFrom, new CampaignTextArgs(N: countText));
        var unread = store.UnreadCount;
        var bottom = DrawCard(drawList, left, top, width, new CardContent(PostsCardId,
            texts.T(CampaignTextKeys.Announcements), unread > 0 ? CardPill.Accent : CardPill.None,
            Loc.T(L.LoveIsland.NewBadge), title, texts.T(CampaignTextKeys.AnnouncementsBody), 0, 0, true), 0,
            out var action);
        if (action == CardTapped)
        {
            OpenPosts();
        }

        return bottom;
    }

    private float DrawLinkshellsCard(ImDrawListPtr drawList, float left, float top, float width, CampaignDto campaign)
    {
        if (campaign.Linkshells.Length == 0)
        {
            return top - LoveIslandKit.CardGap * UiScale.Current;
        }

        var bottom = DrawCard(drawList, left, top, width, new CardContent(LinkshellsCardId,
            Texts.T(CampaignTextKeys.LinkshellsTitle), CardPill.None, string.Empty, labels.LinkshellNames,
            Texts.T(CampaignTextKeys.LinkshellsBody), 0, LinkshellBodyLines, true), 0, out var action);
        if (action == CardTapped)
        {
            OpenLinkshells();
        }

        return bottom;
    }

    private float DrawLinkRail(float top)
    {
        var campaign = frameCampaign;
        var linkLabels = labels.LinkLabels;
        if (campaign is null || linkLabels.Length == 0)
        {
            return top - SectionGap * UiScale.Current;
        }

        var count = Math.Min(linkLabels.Length, linkActive.Length);
        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, top));
        var tapped = linkRail.Draw(ui, linkLabels.AsSpan(0, count), linkActive.AsSpan(0, count), "loveisland.links");
        if (tapped >= 0 && tapped < campaign.Links.Length)
        {
            UrlActions.AskThenOpen(campaign.Links[tapped].Url);
        }

        return ImGui.GetCursorScreenPos().Y;
    }

    private float DrawAboutCard(ImDrawListPtr drawList, float left, float top, float width)
    {
        var campaign = frameCampaign;
        if (campaign is null || string.IsNullOrWhiteSpace(campaign.About))
        {
            return top;
        }

        var bottom = DrawCard(drawList, left, top, width, new CardContent(AboutCardId, Texts.T(CampaignTextKeys.About),
            CardPill.None, string.Empty, string.Empty, campaign.About, 0, AboutBodyLines, true), 0, out var action);
        var pad = LoveIslandKit.CardPad * UiScale.Current;
        var more = Texts.T(CampaignTextKeys.More);
        var moreWidth = Typography.Measure(more, LoveIslandKit.EyebrowStyle).X;
        Typography.Draw(drawList, new Vector2(left + width - pad - moreWidth, top + pad), more, ui.Accent,
            LoveIslandKit.EyebrowStyle);
        if (action == CardTapped)
        {
            OpenAbout();
        }

        return bottom;
    }

    private void Teleport(CampaignExperienceDto experience, ExperienceLabels experienceLabels)
    {
        UiFeedback.Play(UiSound.Tap);
        TeleportActions.AskThenTravel(confirm, experience.Title,
            string.Concat(experienceLabels.Address, LoveIslandKit.Separator, experienceLabels.WardPlot),
            experienceLabels.Destination);
    }
}

internal struct CountdownText
{
    private long second;
    private LanguageInfo? language;
    private string? value;

    public string Get(long remainingSeconds)
    {
        if (value is not null && second == remainingSeconds && ReferenceEquals(language, Loc.Current))
        {
            return value;
        }

        second = remainingSeconds;
        language = Loc.Current;
        value = CampaignCountdown.Text(remainingSeconds);
        return value;
    }
}
