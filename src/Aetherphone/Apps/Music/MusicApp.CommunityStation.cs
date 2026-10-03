using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Radio.Live;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float StageArtSize = 84f;
    private const float StageArtRounding = 0.12f;
    private const float StagePadTop = 4f;
    private const float StationPlayRadius = 26f;
    private const float OnAirCardHeight = 52f;
    private const float StageFollowHeight = 30f;
    private const float StageHostRadius = 10f;
    private const float StageStripHeight = 46f;
    private const float SegmentRowHeight = 40f;
    private const int TwitchLinkKind = 0;
    private const int RecentTrackRows = 12;
    private const int RecentTrackPreviewRows = 5;
    private const float TrackRowHeight = 44f;
    private const float TrackSquareSize = 26f;
    private const double TrackStampRefreshSeconds = 30.0;

    private static readonly string[] LinkLabels =
    {
        "Twitch", "YouTube", "Discord", "Bluesky", "X", "Ko-fi", "Patreon",
    };

    private readonly ChipRail stationTagRail = new();
    private readonly ChipRail linkRail = new();
    private readonly string[] linkLabels = new string[7];
    private readonly bool[] linkActive = new bool[7];
    private readonly string[] linkTargets = new string[7];
    private readonly string[] listenerSegments = new string[3];
    private readonly string[] hostSegments = new string[4];
    private readonly RadioStationText stageText = new();
    private readonly RadioCountLabel stageLiveLabel = new();
    private readonly RadioCountLabel stageHostLabel = new();
    private readonly RadioFittedText stageHostFit = new();
    private readonly RadioFittedText stageStatusFit = new();
    private readonly RadioFittedText stageCaptionFit = new();
    private readonly RadioWrappedText aboutScheduleText = new();
    private readonly RadioWrappedText aboutDescriptionText = new();
    private RadioTrackDto[] splitTrackSource = Array.Empty<RadioTrackDto>();
    private string[] trackTitles = Array.Empty<string>();
    private string[] trackArtists = Array.Empty<string>();
    private string[] trackStamps = Array.Empty<string>();
    private RadioFittedText[] trackTitleFits = Array.Empty<RadioFittedText>();
    private RadioFittedText[] trackArtistFits = Array.Empty<RadioFittedText>();
    private double trackStampsExpireAt;
    private LanguageInfo? trackStampsLanguage;
    private CommunityStationDto? hostDisplayStation;
    private string hostDisplay = string.Empty;
    private string openedStationId = string.Empty;
    private bool showAllTracks;

    private CommunityStationDto? ViewedStation(string stationId)
    {
        if (community.TryResolve(stationId, out var station))
        {
            return station;
        }

        if (!string.Equals(openedStationId, stationId, StringComparison.Ordinal))
        {
            openedStationId = stationId;
            community.OpenStation(stationId, null);
        }

        return null;
    }

    private string CommunityStationTitle(string stationId)
    {
        return community.TryResolve(stationId, out var station) ? station.Name : Loc.T(L.Music.CommunityRadio);
    }

    private void DrawCommunityStation(in PhoneContext context, in MusicRoute route)
    {
        var scale = UiScale.Current;
        community.EnsureFresh(true);
        var station = ViewedStation(route.Key);
        var frame = BeginPage(context);
        if (station is null)
        {
            DrawStationPlaceholder(Unobstructed(frame.Body), scale);
            EndPage(in frame, context, Loc.T(L.Music.CommunityRadio));
            return;
        }

        community.EnsureTracks(station.Id);
        VisitStationRoom(station.Id);
        var body = frame.Body;
        var stageBottom = DrawStationStage(body, station, scale);
        var inset = Metrics.Space.Lg * scale;
        var segmentRect = new Rect(new Vector2(body.Min.X + inset, stageBottom),
            new Vector2(body.Max.X - inset, stageBottom + SegmentRowHeight * scale));
        var panel = SelectStationPanel(segmentRect);
        var panelRect = new Rect(new Vector2(body.Min.X, segmentRect.Max.Y + Metrics.Space.Xxs * scale), body.Max);
        switch (panel)
        {
            case StationPanel.Requests:
                DrawStationRequests(panelRect, scale);
                break;
            case StationPanel.About:
                DrawStationAbout(panelRect, station, scale);
                break;
            case StationPanel.Host:
                DrawStationHost(panelRect, scale);
                break;
            default:
                DrawStationChat(new Rect(panelRect.Min, new Vector2(body.Max.X,
                    MathF.Max(panelRect.Min.Y, Unobstructed(body).Max.Y))), scale);
                break;
        }

        EndPage(in frame, context, station.Name);
    }

    private bool RoomShows(CommunityStationDto station)
    {
        return room.IsAttached && string.Equals(room.StationId, station.Id, StringComparison.Ordinal);
    }

    private StationPanel SelectStationPanel(Rect row)
    {
        var hosting = room.IsAttached && room.IsDj
                      && string.Equals(room.StationId, roomStationId, StringComparison.Ordinal);
        var options = hosting ? hostSegments : listenerSegments;
        options[0] = Loc.T(L.Music.Live.TabChat);
        options[1] = Loc.T(L.Music.Live.TabRequests);
        options[2] = Loc.T(L.Music.Live.TabAbout);
        if (hosting)
        {
            options[3] = Loc.T(L.Music.Live.TabHost);
        }
        else if (stationPanel == StationPanel.Host)
        {
            stationPanel = StationPanel.Chat;
        }

        var picked = SegmentStrip.Draw("music.station.panels", row, options, (int)stationPanel, ui.Palette);
        if (picked >= 0 && picked < options.Length)
        {
            stationPanel = (StationPanel)picked;
        }

        return stationPanel;
    }

    private void RefreshStageText(CommunityStationDto station)
    {
        var now = ImGui.GetTime();
        if (!stageText.NeedsRefresh(station, now))
        {
            return;
        }

        stageText.Refresh(station, now, OffAirMark(station), ScheduleLine(station), StationHeaderStatus(station));
    }

    private float DrawStationStage(Rect body, CommunityStationDto station, float scale)
    {
        RefreshStageText(station);
        var drawList = ImGui.GetWindowDrawList();
        var inset = Metrics.Space.Lg * scale;
        var gap = Metrics.Space.Md * scale;
        var top = body.Min.Y + StagePadTop * scale;
        var art = StageArtSize * scale;
        var artMin = new Vector2(body.Min.X + inset, top);
        var artMax = artMin + new Vector2(art, art);
        DrawStationArt(drawList, artMin, artMax, station, art * StageArtRounding);

        var radius = StationPlayRadius * scale;
        var playCenter = new Vector2(body.Max.X - inset - radius, artMin.Y + art * 0.5f);
        var infoLeft = artMax.X + gap;
        var infoWidth = MathF.Max(1f, playCenter.X - radius - gap - infoLeft);
        var showsRoom = RoomShows(station);
        var live = station.IsLive || (showsRoom && room.IsLive);
        var listeners = showsRoom ? room.ListenerCount : station.Listeners;
        var pillHeight = LivePill.Height(scale);
        if (live)
        {
            var label = stageLiveLabel.Prefixed(Loc.T(L.Music.LiveBadge), L.Music.ListeningCount, listeners);
            LivePill.Draw(drawList, new Vector2(infoLeft, top), label, ui.Theme.Danger, clock, scale);
        }
        else
        {
            var resting = stageStatusFit.Fit(stageText.Header, infoWidth, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(infoLeft, top + (pillHeight - Typography.LineHeight(TextStyles.Caption1))
                * 0.5f), resting, ui.MutedInk, TextStyles.Caption1);
        }

        var hostTop = top + pillHeight + Metrics.Space.Xs * scale;
        DrawHost(drawList, station, infoLeft, hostTop, infoWidth, scale);
        DrawStageFollow(station, new Vector2(infoLeft, artMax.Y - StageFollowHeight * scale), infoWidth, live, scale);
        DrawStagePlay(station, playCenter, radius, live);
        return DrawStageStrip(new Rect(new Vector2(body.Min.X + inset, artMax.Y + Metrics.Space.Md * scale),
            new Vector2(body.Max.X - inset, artMax.Y + Metrics.Space.Md * scale + StageStripHeight * scale)), station,
            live, scale);
    }

    private void DrawStageFollow(CommunityStationDto station, Vector2 origin, float maxWidth, bool live, float scale)
    {
        var owned = community.Mine is { } mine && string.Equals(mine.Station.Id, station.Id, StringComparison.Ordinal);
        if (owned)
        {
            return;
        }

        var label = station.IsFollowing
            ? Loc.T(L.Music.FollowingStation)
            : live
                ? Loc.T(L.Music.FollowStation)
                : Loc.T(L.Music.NotifyWhenLive);
        var height = StageFollowHeight * scale;
        var width = MathF.Min(maxWidth, AppSkin.PillWidthFor(label, height));
        var rect = new Rect(origin, origin + new Vector2(width, height));
        var tapped = station.IsFollowing ? ui.GhostButton(rect, label) : ui.PillButton(rect, label, true);
        if (tapped)
        {
            community.ToggleFollow(station);
        }
    }

    private void DrawStagePlay(CommunityStationDto station, Vector2 center, float radius, bool live)
    {
        var current = IsCurrentCommunityStation(station);
        if (live || current)
        {
            if (!MusicRenderer.PlayButton("music.station.play", center, radius, ui.Accent, ui.Palette.BackdropBottom,
                    current && playback.IsPlaying))
            {
                return;
            }

            if (current)
            {
                playback.TogglePlayPause();
            }
            else
            {
                PlayCommunityStation(station);
            }

            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.FieldSurface), 32);
        AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.Bell), ui.MutedInk, 1f);
    }

    private float DrawStageStrip(Rect strip, CommunityStationDto station, bool live, float scale)
    {
        var track = NowPlayingFor(station);
        var schedule = stageText.Schedule;
        if (track.Length == 0 && (live || schedule.Length == 0))
        {
            return strip.Min.Y;
        }

        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, strip.Min, strip.Max, Metrics.Radius.Md * scale, ImGui.GetColorU32(ui.Palette.CardFill));
        var lampCenter = new Vector2(strip.Min.X + 20f * scale, strip.Center.Y);
        var textLeft = lampCenter.X + 18f * scale;
        var available = MathF.Max(1f, strip.Max.X - Metrics.Space.Md * scale - textLeft);
        if (track.Length == 0)
        {
            AppSkin.Icon(drawList, lampCenter, IconGlyph.Of(FontAwesomeIcon.CalendarAlt), ui.MutedInk, 0.8f);
            var fitted = stageCaptionFit.Fit(schedule, available, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(textLeft,
                strip.Center.Y - Typography.LineHeight(TextStyles.Subheadline) * 0.5f), fitted, ui.BodyInk,
                TextStyles.Subheadline);
            return strip.Max.Y + Metrics.Space.Xs * scale;
        }

        Equalizer.Draw(drawList, lampCenter, scale, 16f * scale, clock, ui.Accent, 1f, playback.IsPlaying);
        var captionHeight = Typography.LineHeight(TextStyles.Caption2);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var blockTop = strip.Center.Y - (captionHeight + titleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, blockTop), Loc.T(L.Music.OnAirNow), ui.MutedInk,
            TextStyles.Caption2);
        Marquee.DrawLeftAuto(drawList, "music.station.track", track, textLeft, blockTop + captionHeight, available,
            TextStyles.BodyEmphasized, ui.TitleInk);
        return strip.Max.Y + Metrics.Space.Xs * scale;
    }

    private void DrawStationAbout(Rect panel, CommunityStationDto station, float scale)
    {
        ImGui.PushID("radio.about");
        using (AppSurface.Begin(panel))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawWatchOnTwitch(scale, station);
            DrawStationTagRail(scale, station);
            if (!station.IsLive && stageText.Schedule.Length > 0)
            {
                DrawStationParagraph(scale, aboutScheduleText, stageText.Schedule, ui.TitleInk, TextStyles.Callout,
                    width);
            }

            if (station.Description.Length > 0)
            {
                DrawStationParagraph(scale, aboutDescriptionText, station.Description, ui.BodyInk,
                    TextStyles.Subheadline, width);
            }

            DrawStationLinks(scale, station);
            DrawRecentTracks(scale, width);
            DrawReportStation(scale, station, width);
        }

        ImGui.PopID();
    }

    private void DrawReportStation(float scale, CommunityStationDto station, float width)
    {
        var origin = ImGui.GetCursorScreenPos();
        var reportWidth = MathF.Min(width - 32f * scale, 200f * scale);
        var reportMin = new Vector2(origin.X + (width - reportWidth) * 0.5f, origin.Y + 8f * scale);
        var reportRect = new Rect(reportMin, reportMin + new Vector2(reportWidth, 34f * scale));
        var tapped = ui.GhostButton(reportRect, Loc.T(L.Music.ReportStation));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 50f * scale));
        if (tapped)
        {
            ReportStation(station);
        }
    }

    private void SearchForTrack(string title) => OpenSearchFor(title);

    private void DrawStationPlaceholder(Rect body, float scale)
    {
        switch (community.StationState)
        {
            case CommunityStationLoad.NotFound:
                EmptyState.Draw(body, ui, FontAwesomeIcon.BroadcastTower, Loc.T(L.Music.StationGone),
                    Loc.T(L.Music.StationGoneSub));
                return;
            case CommunityStationLoad.SignedOut:
                EmptyState.Draw(body, ui, FontAwesomeIcon.UserSlash, Loc.T(L.Music.StationSignedOut),
                    Loc.T(L.Music.StationSignedOutSub));
                return;
            case CommunityStationLoad.Unavailable:
                if (EmptyState.Draw(body, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Music.StationOffline),
                        Loc.T(L.Music.StationOfflineSub), Loc.T(L.Common.Retry)))
                {
                    community.RetryStation();
                }

                return;
            default:
                LoadingPulse.Draw(body.Center, 16f * scale, ui.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
                return;
        }
    }

    private string HostDisplay(CommunityStationDto station)
    {
        if (ReferenceEquals(hostDisplayStation, station))
        {
            return hostDisplay;
        }

        hostDisplayStation = station;
        hostDisplay = station.OwnerDisplayName.Length > 0
            ? station.OwnerDisplayName
            : station.OwnerHandle.Length > 0
                ? "@" + station.OwnerHandle
                : string.Empty;
        return hostDisplay;
    }

    private void DrawHost(ImDrawListPtr drawList, CommunityStationDto station, float left, float top, float width,
        float scale)
    {
        var display = HostDisplay(station);
        if (display.Length == 0)
        {
            return;
        }

        var label = stageHostLabel.Format(L.Music.HostedBy, display);
        var radius = StageHostRadius * scale;
        var gap = 7f * scale;
        var available = MathF.Max(1f, width - radius * 2f - gap);
        var fitted = stageHostFit.Fit(label, available, TextStyles.Caption1);
        var center = new Vector2(left + radius, top + radius);
        if (station.OwnerAvatarUrl.Length > 0 && images.Sized(station.OwnerAvatarUrl, radius * 2f) is { } avatar)
        {
            drawList.AddImageRounded(avatar.Handle, center - new Vector2(radius, radius),
                center + new Vector2(radius, radius), Vector2.Zero, Vector2.One, 0xFFFFFFFFu, radius,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.FieldSurface), 24);
        }

        UserName.DrawAuto(drawList, "music.station.host", fitted, station.OwnerBadges, station.OwnerBadgeIds,
            left + radius * 2f + gap, top + radius - Typography.LineHeight(TextStyles.Caption1) * 0.5f,
            available, TextStyles.Caption1, ui.MutedInk, theme);
    }

    private static string StationHeaderStatus(CommunityStationDto station)
    {
        var resting = OffAirMark(station);
        if (station.Followers == 0)
        {
            return resting;
        }

        return resting + " · " + Loc.Plural(L.Music.StationFollowers, station.Followers);
    }

    private static string LinkUrl(CommunityStationDto station, int kind)
    {
        for (var index = 0; index < station.Links.Length; index++)
        {
            if (station.Links[index].Kind == kind)
            {
                return station.Links[index].Url;
            }
        }

        return string.Empty;
    }

    private void DrawWatchOnTwitch(float scale, CommunityStationDto station)
    {
        var url = LinkUrl(station, TwitchLinkKind);
        if (url.Length == 0)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var buttonWidth = MathF.Min(width - 32f * scale, 220f * scale);
        var buttonMin = new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y);
        var buttonRect = new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, 36f * scale));
        if (ui.GhostButton(buttonRect, Loc.T(L.Music.WatchOnTwitch)))
        {
            Windows.UrlActions.AskThenOpen(url);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 46f * scale));
    }

    private void DrawStationTagRail(float scale, CommunityStationDto station)
    {
        if (station.Tags.Length == 0)
        {
            return;
        }

        var count = Math.Min(station.Tags.Length, MaxStationTags);
        for (var index = 0; index < count; index++)
        {
            tagFilterLabels[index] = station.Tags[index];
            tagFilterActive[index] = false;
        }

        var tapped = stationTagRail.Draw(ui, tagFilterLabels.AsSpan(0, count), tagFilterActive.AsSpan(0, count));
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        if (tapped >= 0)
        {
            OpenCommunityWithTag(station.Tags[tapped]);
        }
    }

    private void EnsureTrackSplits()
    {
        var source = community.Tracks;
        if (!ReferenceEquals(source, splitTrackSource))
        {
            splitTrackSource = source;
            trackTitles = new string[source.Length];
            trackArtists = new string[source.Length];
            trackStamps = new string[source.Length];
            trackTitleFits = new RadioFittedText[source.Length];
            trackArtistFits = new RadioFittedText[source.Length];
            trackStampsExpireAt = 0;
            for (var index = 0; index < source.Length; index++)
            {
                trackTitleFits[index] = new RadioFittedText();
                trackArtistFits[index] = new RadioFittedText();
                var raw = source[index].Title;
                var cut = raw.IndexOf(" - ", StringComparison.Ordinal);
                if (cut <= 0)
                {
                    trackTitles[index] = raw;
                    trackArtists[index] = string.Empty;
                    continue;
                }

                trackArtists[index] = raw[..cut];
                trackTitles[index] = raw[(cut + 3)..];
            }
        }

        var now = ImGui.GetTime();
        if (now < trackStampsExpireAt && ReferenceEquals(trackStampsLanguage, Loc.Current))
        {
            return;
        }

        trackStampsExpireAt = now + TrackStampRefreshSeconds;
        trackStampsLanguage = Loc.Current;
        for (var index = 0; index < source.Length; index++)
        {
            trackStamps[index] = TimeText.Ago(source[index].PlayedAtUnix);
        }
    }

    private void DrawRecentTracks(float scale, float width)
    {
        if (community.TracksLoading)
        {
            SectionHeader.Draw(ui, Loc.T(L.Music.LastPlayed), false, 0f);
            InfiniteScroll.DrawLoadingRow(ImGui.GetCursorScreenPos().X + width * 0.5f, ui.MutedInk);
            return;
        }

        var recent = community.Tracks;
        if (recent.Length == 0)
        {
            return;
        }

        EnsureTrackSplits();
        SectionHeader.Draw(ui, Loc.T(L.Music.LastPlayed), false, 0f);
        var shown = Math.Min(recent.Length, showAllTracks ? RecentTrackRows : RecentTrackPreviewRows);
        for (var index = 0; index < shown; index++)
        {
            DrawTrackRow(scale, recent[index], index);
        }

        if (showAllTracks || recent.Length <= RecentTrackPreviewRows)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = TrackRowHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var label = Loc.T(L.Music.ShowAll);
        var size = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(min.X + Metrics.Space.Md * scale, min.Y + (rowHeight - size.Y) * 0.5f), label, ui.Accent,
            TextStyles.SubheadlineEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (UiInteract.Click(min, max, hovered))
        {
            showAllTracks = true;
        }
    }

    private void DrawTrackRow(float scale, RadioTrackDto track, int index)
    {
        var rowHeight = TrackRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, rowHeight, ui.HoverWash);
        var min = cell.Bounds.Min;
        var max = cell.Bounds.Max;
        var squareSize = TrackSquareSize * scale;
        var squareMin = new Vector2(min.X + Metrics.Space.Md * scale, min.Y + (rowHeight - squareSize) * 0.5f);
        drawList.AddImageRounded(artwork.HandleForName(track.Title), squareMin,
            squareMin + new Vector2(squareSize, squareSize), Vector2.Zero, Vector2.One, 0xFFFFFFFFu, 6f * scale,
            ImDrawFlags.RoundCornersAll);

        var stamp = trackStamps[index];
        var stampSize = Typography.Measure(stamp, TextStyles.Caption2);
        var textLeft = squareMin.X + squareSize + 10f * scale;
        var textWidth = max.X - Metrics.Space.Md * scale - stampSize.X - 10f * scale - textLeft;
        var artist = trackArtists[index];
        var title = trackTitleFits[index].Fit(trackTitles[index], textWidth, TextStyles.Subheadline);
        if (artist.Length == 0)
        {
            var singleHeight = Typography.LineHeight(TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + (rowHeight - singleHeight) * 0.5f), title,
                ui.BodyInk, TextStyles.Subheadline);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + 7f * scale), title, ui.BodyInk,
                TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + 24f * scale),
                trackArtistFits[index].Fit(artist, textWidth, TextStyles.Caption2), ui.MutedInk, TextStyles.Caption2);
        }

        Typography.Draw(drawList, new Vector2(max.X - Metrics.Space.Md * scale - stampSize.X,
            min.Y + (rowHeight - stampSize.Y) * 0.5f), stamp, ui.MutedInk, TextStyles.Caption2);

        if (cell.Tapped)
        {
            SearchForTrack(track.Title);
        }

        FeedCell.End(drawList, cell, ui.Hairline);
    }

    private static void DrawStationParagraph(float scale, RadioWrappedText wrapped, string text, Vector4 color,
        in TextStyle style, float width)
    {
        var origin = ImGui.GetCursorScreenPos();
        wrapped.Wrap(text, width - 32f * scale, style);
        var height = wrapped.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + 16f * scale, origin.Y), color,
            style);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 12f * scale));
    }

    private void DrawStationLinks(float scale, CommunityStationDto station)
    {
        if (station.Links.Length == 0)
        {
            return;
        }

        var count = 0;
        for (var index = 0; index < station.Links.Length && count < linkLabels.Length; index++)
        {
            var link = station.Links[index];
            if (link.Kind < 0 || link.Kind >= LinkLabels.Length || link.Kind == TwitchLinkKind)
            {
                continue;
            }

            linkLabels[count] = LinkLabels[link.Kind];
            linkActive[count] = false;
            linkTargets[count] = link.Url;
            count++;
        }

        if (count == 0)
        {
            return;
        }

        var tapped = linkRail.Draw(ui, linkLabels.AsSpan(0, count), linkActive.AsSpan(0, count));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        if (tapped >= 0)
        {
            Windows.UrlActions.AskThenOpen(linkTargets[tapped]);
        }
    }

    private void ReportStation(CommunityStationDto station)
    {
        var stationId = station.Id;
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Music.ReportStationTitle),
            Submit = (reason, done) => SubmitStationReport(stationId, reason, done),
        });
    }

    private void SubmitStationReport(string stationId, string? reason, Action<bool> done)
    {
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                succeeded = await aethernet.Safety.ReportAsync("radio_station", stationId, reason, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Radio] station report failed");
            }

            done(succeeded);
        });
    }
}
