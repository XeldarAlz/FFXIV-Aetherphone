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
    private const float CommunityRowHeight = 68f;
    private const float StationCoverSize = 132f;
    private const float LinkPillHeight = 30f;
    private const int CommunityHomeRows = 3;

    private static readonly string[] LinkLabels =
    {
        "Twitch", "YouTube", "Discord", "Bluesky", "X", "Ko-fi", "Patreon",
    };

    private const int MaxStationTags = 5;
    private const int RecentTrackRows = 12;
    private const float TrackRowHeight = 34f;

    private readonly ChipRail tagRail = new();
    private readonly ChipRail stationTagRail = new();
    private readonly string[] tagFilterLabels = new string[MaxStationTags * 8 + 1];
    private readonly bool[] tagFilterActive = new bool[MaxStationTags * 8 + 1];
    private readonly List<string> knownTags = new();
    private readonly List<CommunityStationDto> filteredStations = new();
    private string viewedStationId = string.Empty;
    private string tagFilter = string.Empty;

    private void OpenCommunity()
    {
        community.Refresh();
        router.Push(View.Community);
    }

    private void OpenCommunityWithTag(string tag)
    {
        tagFilter = tag;
        tagRail.Reset();
        router.Pop();
        if (!router.Current.Equals(View.Community))
        {
            router.Push(View.Community);
        }
    }

    private void OpenStationPage(CommunityStationDto station)
    {
        viewedStationId = station.Id;
        router.Push(View.Station);
    }

    private void PopToCommunity()
    {
        router.Pop();
    }

    private CommunityStationDto? ViewedStation()
    {
        return community.TryFind(viewedStationId, out var station) ? station : null;
    }

    private bool IsCurrentCommunityStation(CommunityStationDto station)
    {
        return playback.RadioActive && playback.Radio.CurrentStationInfo.CommunityId == station.Id;
    }

    private void PlayCommunityStation(CommunityStationDto station)
    {
        var snapshot = community.Stations;
        var start = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (string.Equals(snapshot[index].Id, station.Id, StringComparison.Ordinal))
            {
                start = index;
                break;
            }
        }

        playSource = Loc.T(L.Music.CommunityRadio);
        playback.PlayStations(CommunityRadioService.ToQueue(snapshot), start);
    }

    private void DrawCommunitySection(float scale)
    {
        var stations = community.Stations;
        if (stations.Length == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var iconBox = 30f * scale;
        var title = Typography.FitText(Loc.T(L.Music.CommunityRadio), width - iconBox - 8f * scale, TextStyles.Title3);
        var titleSize = Typography.Measure(title, TextStyles.Title3);
        var headingMin = origin;
        var headingMax = new Vector2(origin.X + width, origin.Y + titleSize.Y);
        var hovered = UiInteract.Hover(headingMin, headingMax);
        Typography.Draw(origin, title, ui.Palette.HeadingInk, TextStyles.Title3);
        var iconCenter = new Vector2(origin.X + width - iconBox * 0.5f, origin.Y + titleSize.Y * 0.5f);
        AppSkin.Icon(iconCenter, FontAwesomeIcon.ChevronRight.ToIconString(), ui.MutedInk, 0.8f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(headingMin, headingMax, hovered))
        {
            OpenCommunity();
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        var shown = Math.Min(stations.Length, CommunityHomeRows);
        for (var index = 0; index < shown; index++)
        {
            DrawCommunityRow(scale, stations[index]);
        }
    }

    private void DrawCommunity(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        community.EnsureFresh(true);
        community.EnsureMine();
        DrawTopBar(context, Loc.T(L.Music.CommunityRadio), GoToHome);
        DrawMyStationEntry(content, scale);
        var body = ScrollBody(content, scale);
        var stations = community.Stations;
        if (stations.Length == 0)
        {
            DrawCommunityEmpty(body, scale);
            return;
        }

        ApplyTagFilter(stations);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            DrawTagFilterRail(scale, stations);
            for (var index = 0; index < filteredStations.Count; index++)
            {
                DrawCommunityRow(scale, filteredStations[index]);
            }

            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void ApplyTagFilter(CommunityStationDto[] stations)
    {
        filteredStations.Clear();
        for (var index = 0; index < stations.Length; index++)
        {
            if (tagFilter.Length == 0 || HasTag(stations[index], tagFilter))
            {
                filteredStations.Add(stations[index]);
            }
        }
    }

    private static bool HasTag(CommunityStationDto station, string tag)
    {
        for (var index = 0; index < station.Tags.Length; index++)
        {
            if (string.Equals(station.Tags[index], tag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawTagFilterRail(float scale, CommunityStationDto[] stations)
    {
        knownTags.Clear();
        for (var index = 0; index < stations.Length && knownTags.Count < tagFilterLabels.Length - 1; index++)
        {
            var tags = stations[index].Tags;
            for (var tagIndex = 0; tagIndex < tags.Length; tagIndex++)
            {
                if (tags[tagIndex].Length > 0 && !knownTags.Contains(tags[tagIndex]))
                {
                    knownTags.Add(tags[tagIndex]);
                }
            }
        }

        if (knownTags.Count == 0)
        {
            return;
        }

        tagFilterLabels[0] = Loc.T(L.Music.AllTags);
        tagFilterActive[0] = tagFilter.Length == 0;
        for (var index = 0; index < knownTags.Count; index++)
        {
            tagFilterLabels[index + 1] = knownTags[index];
            tagFilterActive[index + 1] = string.Equals(knownTags[index], tagFilter, StringComparison.OrdinalIgnoreCase);
        }

        var count = knownTags.Count + 1;
        var tapped = tagRail.Draw(ui, tagFilterLabels.AsSpan(0, count), tagFilterActive.AsSpan(0, count));
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        if (tapped < 0)
        {
            return;
        }

        tagFilter = tapped == 0 || tagFilterActive[tapped] ? string.Empty : knownTags[tapped - 1];
    }

    private void DrawMyStationEntry(Rect content, float scale)
    {
        if (!community.OwnsStation)
        {
            return;
        }

        var center = new Vector2(content.Max.X - 26f * scale, content.Min.Y + TopBarHeight * scale * 0.5f);
        if (ui.IconButton(center, 16f * scale, FontAwesomeIcon.BroadcastTower.ToIconString(), ui.TitleInk,
                AppSkin.Transparent, 0.8f, Loc.T(L.Music.MyStation)))
        {
            OpenMyStation();
        }
    }

    private void DrawCommunityEmpty(Rect body, float scale)
    {
        if (community.Loading)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), ui.MutedInk, TextStyles.Callout);
            return;
        }

        var center = new Vector2(body.Center.X, body.Center.Y - 20f * scale);
        Typography.DrawCentered(center, Loc.T(L.Music.CommunityEmpty), ui.TitleInk, TextStyles.Title3);
        Typography.DrawWrappedCentered(new Vector2(center.X, center.Y + 20f * scale),
            Loc.T(L.Music.CommunityEmptySub), ui.MutedInk, TextStyles.Subheadline, body.Width - 48f * scale);
    }

    private void DrawCommunityRow(float scale, CommunityStationDto station)
    {
        var rowHeight = CommunityRowHeight * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, 10f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var artSize = 50f * scale;
        var artMin = new Vector2(min.X + 6f * scale, min.Y + (rowHeight - artSize) * 0.5f);
        var artMax = artMin + new Vector2(artSize, artSize);
        DrawStationArt(drawList, artMin, artMax, station, 10f * scale);

        var current = IsCurrentCommunityStation(station);
        var textLeft = artMax.X + 12f * scale;
        var textWidth = max.X - (current ? 40f * scale : 14f * scale) - textLeft;
        var nameY = min.Y + 12f * scale;
        var fittedName = Typography.FitText(station.Name, textWidth, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, nameY), fittedName, current ? ui.Accent : ui.TitleInk,
            TextStyles.BodyEmphasized);

        var statusY = min.Y + 33f * scale;
        DrawLiveMark(drawList, new Vector2(textLeft, statusY), scale, station, textWidth);

        var nowPlaying = NowPlayingFor(station);
        var subtitle = nowPlaying.Length > 0 ? nowPlaying : ScheduleLine(station);
        if (subtitle.Length == 0)
        {
            subtitle = station.Description;
        }

        if (subtitle.Length > 0)
        {
            var fittedSubtitle = Typography.FitText(subtitle, textWidth, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + 47f * scale), fittedSubtitle, ui.MutedInk,
                TextStyles.Caption1);
        }

        if (current)
        {
            Equalizer.Draw(drawList, new Vector2(max.X - 20f * scale, min.Y + rowHeight * 0.5f), scale, 17f * scale,
                clock, ui.Accent, 1f, playback.IsPlaying);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (UiInteract.Click(min, max, hovered))
        {
            OpenStationPage(station);
        }
    }

    private void DrawStationArt(ImDrawListPtr drawList, Vector2 min, Vector2 max, CommunityStationDto station,
        float rounding)
    {
        if (station.ArtworkUrl.Length > 0 && Thumb(station.ArtworkUrl).Texture is { } texture)
        {
            drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
            return;
        }

        drawList.AddImageRounded(artwork.HandleForName(station.Name), min, max, Vector2.Zero, Vector2.One,
            0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
    }

    private void DrawLiveMark(ImDrawListPtr drawList, Vector2 origin, float scale, CommunityStationDto station,
        float available)
    {
        if (!station.IsLive)
        {
            var offAir = Typography.FitText(Loc.T(L.Music.OffAir), available, TextStyles.Caption1);
            Typography.Draw(drawList, origin, offAir, ui.MutedInk, TextStyles.Caption1);
            return;
        }

        var dotRadius = 3.5f * scale;
        var dotCenter = new Vector2(origin.X + dotRadius, origin.Y + 7f * scale);
        drawList.AddCircleFilled(dotCenter, dotRadius, ImGui.GetColorU32(ui.Accent));
        var label = string.Format(Loc.T(L.Music.ListeningCount), station.Listeners);
        var textLeft = dotCenter.X + dotRadius + 6f * scale;
        var fitted = Typography.FitText(label, available - (textLeft - origin.X), TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y), fitted, ui.Accent, TextStyles.Caption1);
    }

    private void DrawStationPage(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        community.EnsureFresh(true);
        var station = ViewedStation();
        if (station is null)
        {
            DrawTopBar(context, Loc.T(L.Music.CommunityRadio), PopToCommunity);
            return;
        }

        community.EnsureTracks(station.Id);
        DrawTopBar(context, station.Name, PopToCommunity);
        var body = ScrollBody(content, scale);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            ImGui.Dummy(new Vector2(0f, 8f * scale));

            var coverSize = StationCoverSize * scale;
            var coverOrigin = ImGui.GetCursorScreenPos();
            var coverMin = new Vector2(coverOrigin.X + (ImGui.GetContentRegionAvail().X - coverSize) * 0.5f,
                coverOrigin.Y);
            DrawStationArt(drawList, coverMin, coverMin + new Vector2(coverSize, coverSize), station, 18f * scale);
            ImGui.SetCursorScreenPos(coverOrigin);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, coverSize + 14f * scale));

            DrawStationHeadline(scale, station);
            DrawStationActions(scale, station);
            DrawStationBody(scale, station);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private void DrawStationHeadline(float scale, CommunityStationDto station)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var name = Typography.FitText(station.Name, width, TextStyles.Title2);
        var nameSize = Typography.Measure(name, TextStyles.Title2);
        Typography.Draw(drawList, new Vector2(origin.X + (width - nameSize.X) * 0.5f, origin.Y), name, ui.TitleInk,
            TextStyles.Title2);

        var hostY = origin.Y + nameSize.Y + 6f * scale;
        DrawHost(drawList, station, origin.X, hostY, width, scale);

        // The host row is as tall as its avatar, so status clears the full diameter, not the text.
        var statusY = hostY + 28f * scale;
        var statusText = station.IsLive
            ? string.Format(Loc.T(L.Music.ListeningCount), station.Listeners)
            : OffAirStatus(station);
        var status = Typography.FitText(statusText, width, TextStyles.Subheadline);
        var statusSize = Typography.Measure(status, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(origin.X + (width - statusSize.X) * 0.5f, statusY), status,
            station.IsLive ? ui.Accent : ui.MutedInk, TextStyles.Subheadline);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, statusY - origin.Y + statusSize.Y + 12f * scale));
    }

    /// A raw handle names nobody. The directory already carries the owner's display name, avatar and
    /// badges, so the page shows the person rather than their id.
    private void DrawHost(ImDrawListPtr drawList, CommunityStationDto station, float left, float top, float width,
        float scale)
    {
        var display = station.OwnerDisplayName.Length > 0
            ? station.OwnerDisplayName
            : station.OwnerHandle.Length > 0
                ? "@" + station.OwnerHandle
                : string.Empty;
        if (display.Length == 0)
        {
            return;
        }

        var label = string.Format(Loc.T(L.Music.HostedBy), display);
        var radius = 11f * scale;
        var gap = 7f * scale;
        var available = width - 32f * scale - radius * 2f - gap;
        var fitted = Typography.FitText(label, available, TextStyles.Caption1);
        var textWidth = Typography.Measure(fitted, TextStyles.Caption1).X
            + UserName.Reserve(station.OwnerBadges, station.OwnerBadgeIds, TextStyles.Caption1);
        var rowLeft = left + (width - (radius * 2f + gap + textWidth)) * 0.5f;
        var center = new Vector2(rowLeft + radius, top + radius);

        if (station.OwnerAvatarUrl.Length > 0 && Thumb(station.OwnerAvatarUrl).Texture is { } avatar)
        {
            drawList.AddImageRounded(avatar.Handle, center - new Vector2(radius, radius),
                center + new Vector2(radius, radius), Vector2.Zero, Vector2.One, 0xFFFFFFFFu, radius,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.FieldSurface), 24);
            var initials = Initials.Of(display);
            var initialsSize = Typography.Measure(initials, TextStyles.Caption2);
            Typography.Draw(drawList, new Vector2(center.X - initialsSize.X * 0.5f, center.Y - initialsSize.Y * 0.5f),
                initials, ui.MutedInk, TextStyles.Caption2);
        }

        UserName.DrawAuto(drawList, "music.station.host", fitted, station.OwnerBadges, station.OwnerBadgeIds,
            rowLeft + radius * 2f + gap, top + radius - Typography.Measure(fitted, TextStyles.Caption1).Y * 0.5f,
            available, TextStyles.Caption1, ui.MutedInk, theme);
    }

    private static string OffAirStatus(CommunityStationDto station)
    {
        var offAir = Loc.T(L.Music.OffAir);
        if (station.Followers == 0)
        {
            return offAir;
        }

        return offAir + " · " + Loc.Plural(L.Music.StationFollowers, station.Followers);
    }

    private static string ScheduleLine(CommunityStationDto station)
    {
        if (station.NextBroadcastAtUnix <= 0)
        {
            return string.Empty;
        }

        return string.Format(Loc.T(L.Music.NextBroadcast), TimeText.FutureMoment(station.NextBroadcastAtUnix));
    }

    private void DrawStationActions(float scale, CommunityStationDto station)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var buttonHeight = 42f * scale;
        var buttonWidth = MathF.Min(width - 32f * scale, 220f * scale);
        var buttonMin = new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y);
        var buttonRect = new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, buttonHeight));
        var current = IsCurrentCommunityStation(station);
        var label = current ? Loc.T(L.Music.StopListening) : Loc.T(L.Music.ListenLive);
        var enabled = station.IsLive || current;
        if (enabled && ui.PillButton(buttonRect, label, !current, "music.station.play"))
        {
            if (current)
            {
                playback.Stop();
            }
            else
            {
                PlayCommunityStation(station);
            }
        }

        if (!enabled)
        {
            ui.PillButton(buttonRect, label, false, "music.station.playDisabled");
        }

        var owned = community.Mine is { } mine && string.Equals(mine.Station.Id, station.Id, StringComparison.Ordinal);
        if (!owned)
        {
            var followMin = new Vector2(buttonMin.X, buttonRect.Max.Y + 8f * scale);
            var followRect = new Rect(followMin, followMin + new Vector2(buttonWidth, 36f * scale));
            var followLabel = station.IsFollowing ? Loc.T(L.Music.FollowingStation) : Loc.T(L.Music.FollowStation);
            if (ui.GhostButton(followRect, followLabel))
            {
                community.ToggleFollow(station);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, buttonHeight + (owned ? 16f : 60f) * scale));
    }

    private const int TwitchLinkKind = 0;

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

    /// A broadcaster who streams on Twitch wants their audience there, not here, so their channel
    /// gets a real button rather than one pill among seven. It opens Twitch and plays nothing.
    private void DrawWatchOnTwitch(float scale, CommunityStationDto station)
    {
        var url = LinkUrl(station, TwitchLinkKind);
        if (url.Length == 0)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var buttonWidth = MathF.Min(width - 32f * scale, 220f * scale);
        var buttonMin = new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y);
        var buttonRect = new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, 36f * scale));
        if (ui.GhostButton(buttonRect, Loc.T(L.Music.WatchOnTwitch)))
        {
            Dalamud.Utility.Util.OpenLink(url);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 46f * scale));
    }

    private void DrawStationBody(float scale, CommunityStationDto station)
    {
        var width = ImGui.GetContentRegionAvail().X;
        DrawWatchOnTwitch(scale, station);
        DrawStationTagRail(scale, station);
        var track = NowPlayingFor(station);
        if (track.Length > 0)
        {
            DrawStationParagraph(scale, track, ui.Accent, TextStyles.Callout, width);
        }

        if (!station.IsLive && ScheduleLine(station) is { Length: > 0 } schedule)
        {
            DrawStationParagraph(scale, schedule, ui.TitleInk, TextStyles.Callout, width);
        }

        if (station.Description.Length > 0)
        {
            DrawStationParagraph(scale, station.Description, ui.BodyInk, TextStyles.Subheadline, width);
        }

        DrawStationLinks(scale, station);
        DrawRecentTracks(scale, width);

        var reportOrigin = ImGui.GetCursorScreenPos();
        var reportWidth = MathF.Min(width - 32f * scale, 200f * scale);
        var reportMin = new Vector2(reportOrigin.X + (width - reportWidth) * 0.5f, reportOrigin.Y + 8f * scale);
        var reportRect = new Rect(reportMin, reportMin + new Vector2(reportWidth, 34f * scale));
        if (ui.GhostButton(reportRect, Loc.T(L.Music.ReportStation)))
        {
            ReportStation(station);
        }

        ImGui.SetCursorScreenPos(reportOrigin);
        ImGui.Dummy(new Vector2(width, 50f * scale));
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

    private void DrawRecentTracks(float scale, float width)
    {
        var recent = community.Tracks;
        if (recent.Length == 0)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + 16f * scale, origin.Y + 10f * scale),
            Loc.T(L.Music.RecentlyPlayed), ui.MutedInk, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 32f * scale));

        var shown = Math.Min(recent.Length, RecentTrackRows);
        for (var index = 0; index < shown; index++)
        {
            DrawTrackRow(scale, recent[index], index);
        }
    }

    private void DrawTrackRow(float scale, RadioTrackDto track, int index)
    {
        var rowHeight = TrackRowHeight * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, 8f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var stamp = TimeText.Clock(track.PlayedAtUnix);
        var stampWidth = Typography.Measure(stamp, TextStyles.Caption2).X;
        var textLeft = min.X + 16f * scale;
        var textWidth = max.X - 16f * scale - stampWidth - 10f * scale - textLeft;
        var title = Typography.FitText(track.Title, textWidth, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + 8f * scale), title, ui.BodyInk,
            TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(max.X - 16f * scale - stampWidth, min.Y + 10f * scale), stamp,
            ui.MutedInk, TextStyles.Caption2);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (UiInteract.Click(min, max, hovered))
        {
            SearchForTrack(track.Title);
        }
    }

    private void SearchForTrack(string title)
    {
        searchDraft = title;
        BeginSearch(title);
        router.Reset();
        router.Push(View.Search, false);
    }

    private void DrawStationParagraph(float scale, string text, Vector4 color, TextStyle style, float width)
    {
        var origin = ImGui.GetCursorScreenPos();
        var wrapWidth = width - 32f * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + 16f * scale, origin.Y), text, color, style,
            wrapWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 12f * scale));
    }

    /// While we are the one playing, the stream's own metadata beats the directory snapshot, which
    /// is up to ten seconds behind and blank for stations the server sees no title for.
    private string NowPlayingFor(CommunityStationDto station)
    {
        if (IsCurrentCommunityStation(station) && playback.RadioNowPlaying.Length > 0)
        {
            return playback.RadioNowPlaying;
        }

        return station.IsLive ? station.NowPlaying : string.Empty;
    }

    private void DrawStationLinks(float scale, CommunityStationDto station)
    {
        if (station.Links.Length == 0)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pillHeight = LinkPillHeight * scale;
        var gap = 8f * scale;
        var cursorX = origin.X + 16f * scale;
        var cursorY = origin.Y;
        var rowCount = 1;
        for (var index = 0; index < station.Links.Length; index++)
        {
            var link = station.Links[index];
            if (link.Kind < 0 || link.Kind >= LinkLabels.Length || link.Kind == TwitchLinkKind)
            {
                continue;
            }

            var label = LinkLabels[link.Kind];
            var pillWidth = Typography.Measure(label, TextStyles.Caption1).X + 26f * scale;
            if (cursorX + pillWidth > origin.X + width - 16f * scale && cursorX > origin.X + 16f * scale)
            {
                cursorX = origin.X + 16f * scale;
                cursorY += pillHeight + gap;
                rowCount++;
            }

            var pillMin = new Vector2(cursorX, cursorY);
            var pillRect = new Rect(pillMin, pillMin + new Vector2(pillWidth, pillHeight));
            if (ui.Chip(pillRect, label, false))
            {
                Dalamud.Utility.Util.OpenLink(link.Url);
            }

            cursorX += pillWidth + gap;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowCount * (pillHeight + gap) + 6f * scale));
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
            var ok = false;
            try
            {
                ok = await aethernet.Safety.ReportAsync("radio_station", stationId, reason, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Radio] station report failed: {exception.Message}");
            }

            done(ok);
        });
    }
}
