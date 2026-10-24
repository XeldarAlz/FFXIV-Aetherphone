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
    private const float SectionGap = 14f;
    private const int PinnedBodyLines = 3;
    private const int SyncshellBodyLines = 2;
    private const int AboutBodyLines = 3;
    private const string StatusCardId = "loveisland.tonight.status";
    private const string LiveCardId = "loveisland.tonight.live";
    private const string ShareCardId = "loveisland.tonight.share";
    private const string PinnedCardId = "loveisland.tonight.pinned";
    private const string PostsCardId = "loveisland.tonight.posts";
    private const string SyncshellsCardId = "loveisland.tonight.syncshells";
    private const string AboutCardId = "loveisland.tonight.about";
    private const string EndedCardId = "loveisland.tonight.ended";
    private const string TeleportButtonId = "loveisland.tonight.teleport";
    private const string WatchButtonId = "loveisland.tonight.watch";
    private const string TwitchButtonId = "loveisland.tonight.twitch";
    private const string PostButtonId = "loveisland.tonight.post";
    private const string GalleryButtonId = "loveisland.tonight.gallery";
    private const string NightToggleId = "loveisland.tonight.night";

    private readonly ChipRail linkRail = new();
    private readonly bool[] linkActive = new bool[16];
    private readonly CampaignTextSlot announcementsTitle = new();
    private CachedText postCountText;
    private string? twitchChannelSource;
    private string twitchChannelUrl = string.Empty;
    private string? shareBodySource;
    private string? websiteNoteSource;
    private string shareBodyWithNote = string.Empty;

    private void DrawTonightTab(Rect area)
    {
        gallery.EnsureFresh(Ledger.Hashtag);
        var frame = BeginRoot(area, out var context);
        var scale = UiScale.Current;
        var heroHeight = HeroHeight(area.Width, scale);
        using (var surface = AppSurface.BeginEdgeToEdge(frame.Body))
        {
            PrepareRootSurface(surface);
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = LeftOf(origin.X);
            var inner = InnerWidth(width);
            var heroLine = origin.Y + area.Min.Y + heroHeight - frame.Body.Min.Y;
            var cursorY = DrawEmblem(drawList, origin.X + width * 0.5f, heroLine, inner, scale);
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

        DrawHeroBackdrop(area, heroHeight, rootScroll, scale);
        EndRoot(in frame, in context, "loveisland.nav.tonight");
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
        return DrawSyncshellsCard(drawList, left, cursorY + gap, width, campaign);
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
            var bottom = DrawCountdownCard(drawList, left, top, width, moment.SecondsToStart, when, out var tapped);
            if (tapped)
            {
                SelectTab(LoveIslandTab.Experiences);
            }

            return bottom;
        }

        var live = moment.CurrentIndex >= 0 ? campaign.Experiences[moment.CurrentIndex] : null;
        var buttonCount = 0;
        var teleportAction = CardIdle;
        var watchAction = CardIdle;
        if (live is not null)
        {
            if (labels.Experiences[moment.CurrentIndex].CanTeleport)
            {
                teleportAction = buttonCount;
                cardButtons[buttonCount++] = new CardButton(Loc.T(L.Maps.Teleport), true, TeleportButtonId);
            }

            if (!string.IsNullOrEmpty(live.TwitchUrl))
            {
                watchAction = buttonCount;
                cardButtons[buttonCount++] = new CardButton(texts.T(CampaignTextKeys.WatchTwitch), teleportAction < 0,
                    WatchButtonId);
            }
        }

        var title = live is null ? texts.T(CampaignTextKeys.BetweenSets) : live.Title;
        var line = live is not null
            ? labels.Experiences[moment.CurrentIndex].LiveLine
            : moment.NextIndex >= 0 ? labels.Experiences[moment.NextIndex].NextLine : texts.T(CampaignTextKeys.TabExperiences);
        var progress = live is null ? -1f : LiveProgress(moment.CurrentIndex);
        var liveBottom = DrawCard(drawList, left, top, width, new CardContent(StatusCardId, string.Empty, CardPill.Live,
            texts.T(CampaignTextKeys.LiveNow), title, line, 0, 0, true, Featured: live is not null,
            MediumTitle: true, Progress: progress), buttonCount, out var liveAction);
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
        else if (liveAction >= 0 && live is not null)
        {
            if (liveAction == teleportAction)
            {
                Teleport(live, labels.Experiences[moment.CurrentIndex]);
            }
            else if (liveAction == watchAction)
            {
                UrlActions.AskThenOpen(live.TwitchUrl!);
            }
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
        var watchUrl = string.Empty;
        string title;
        string body;
        if (onAir && moment.CurrentDjIndex >= 0)
        {
            var current = labels.DjSets[moment.CurrentDjIndex];
            title = campaign.DjSets[moment.CurrentDjIndex].Name;
            body = current.OnAirLine;
            watchUrl = current.WatchUrl;
        }
        else if (onAir)
        {
            title = string.IsNullOrWhiteSpace(campaign.LiveTitle) ? Loc.T(L.LoveIsland.LiveSet) : campaign.LiveTitle;
            body = labels.LiveChannelLine;
            watchUrl = channel.Length > 0 ? TwitchUrlFor(channel) : string.Empty;
        }
        else if (moment.NextDjIndex >= 0)
        {
            title = labels.DjSets[moment.NextDjIndex].NextUpLine;
            body = labels.DjSets[moment.NextDjIndex].StreamLine;
        }
        else
        {
            title = texts.T(CampaignTextKeys.NoStream);
            body = texts.T(CampaignTextKeys.StreamHint);
        }

        var buttonCount = 0;
        if (watchUrl.Length > 0)
        {
            cardButtons[buttonCount++] = new CardButton(texts.T(CampaignTextKeys.WatchTwitch), true, TwitchButtonId);
        }

        var bottom = DrawCard(drawList, left, top, width, new CardContent(LiveCardId, texts.T(CampaignTextKeys.LiveDj),
            onAir ? CardPill.Live : CardPill.Off, Loc.T(onAir ? L.LoveIsland.OnAir : L.LoveIsland.OffAir), title, body,
            0, 0, true), buttonCount, out var action);
        if (action == CardTapped)
        {
            SelectTab(LoveIslandTab.Djs);
        }
        else if (action == 0)
        {
            UrlActions.AskThenOpen(watchUrl);
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
        twitchChannelUrl = string.Concat(LoveIslandKit.TwitchHostPrefix, LoveIslandKit.TwitchChannelName(channel));
        return twitchChannelUrl;
    }

    private float DrawShareCard(ImDrawListPtr drawList, float left, float top, float width)
    {
        var texts = Texts;
        cardButtons[0] = new CardButton(texts.T(CampaignTextKeys.PostCta), true, PostButtonId);
        cardButtons[1] = new CardButton(texts.T(CampaignTextKeys.GalleryCta), false, GalleryButtonId);
        var bottom = DrawCard(drawList, left, top, width, new CardContent(ShareCardId,
            texts.T(CampaignTextKeys.ShareTitle), CardPill.None, string.Empty, labels.HashtagTitle,
            ShareBody(texts), 0, 0, false, ChromeTitle: true, MediumTitle: true, PhotoStrip: true), 2,
            out var action);
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

    private float DrawSyncshellsCard(ImDrawListPtr drawList, float left, float top, float width, CampaignDto campaign)
    {
        if (campaign.Syncshells.Length == 0)
        {
            return top - LoveIslandKit.CardGap * UiScale.Current;
        }

        var bottom = DrawCard(drawList, left, top, width, new CardContent(SyncshellsCardId,
            Texts.T(CampaignTextKeys.SyncshellsTitle), CardPill.None, string.Empty, labels.SyncshellNames,
            Texts.T(CampaignTextKeys.SyncshellsBody), 0, SyncshellBodyLines, true), 0, out var action);
        if (action == CardTapped)
        {
            OpenSyncshells();
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
