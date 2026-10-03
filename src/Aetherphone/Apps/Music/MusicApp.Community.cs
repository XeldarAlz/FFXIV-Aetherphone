using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Radio.Live;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float CommunityRowHeight = 68f;
    private const float CommunityShelfStatusHeight = 34f;
    private const int MaxStationTags = 5;

    private readonly ChipRail tagRail = new();
    private readonly string[] tagFilterLabels = new string[MaxStationTags * 8 + 1];
    private readonly bool[] tagFilterActive = new bool[MaxStationTags * 8 + 1];
    private readonly List<string> knownTags = new();
    private readonly List<CommunityStationDto> filteredStations = new();
    private readonly Dictionary<string, RadioStationText> communityRowTexts = new(StringComparer.Ordinal);
    private string tagFilter = string.Empty;

    private enum LiveGroup : byte
    {
        OnAir,
        Upcoming,
        Followed,
        Resting,
    }

    private void OpenCommunityWithTag(string tag)
    {
        tagFilter = tag;
        tagRail.Reset();
        routers[(int)MusicTab.Radio].Reset();
        tab = MusicTab.Radio;
    }

    private void OpenStationPage(CommunityStationDto station)
    {
        showAllTracks = false;
        community.OpenStation(station.Id, station);
        openedStationId = station.Id;
        Push(MusicRoute.CommunityStation(station.Id));
    }

    private bool IsCurrentCommunityStation(CommunityStationDto station)
    {
        return playback.RadioActive && playback.Radio.CurrentStationInfo.CommunityId == station.Id;
    }

    private void PlayCommunityStation(CommunityStationDto station)
    {
        var snapshot = community.Stations;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (string.Equals(snapshot[index].Id, station.Id, StringComparison.Ordinal))
            {
                playback.PlayStations(CommunityRadioService.ToQueue(snapshot), index);
                return;
            }
        }

        playback.PlayStations(new[] { CommunityRadioService.ToStation(station) }, 0);
    }

    private void DrawCommunityShelfStatus(float scale)
    {
        var retryable = !community.Loading && !community.Loaded && community.IsSignedIn;
        var label = community.Loading
            ? Loc.T(L.Common.Loading)
            : community.Loaded
                ? Loc.T(L.Music.CommunityEmpty)
                : retryable
                    ? Loc.T(L.Music.CommunityOffline)
                    : Loc.T(L.Music.StationSignedOut);

        var width = ScrollLayout.StableContentWidth();
        var height = CommunityShelfStatusHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var hovered = retryable && UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var drawList = ImGui.GetWindowDrawList();
        var retry = retryable ? Loc.T(L.Common.Retry) : string.Empty;
        var retryWidth = retry.Length == 0 ? 0f : Typography.Measure(retry, TextStyles.Subheadline).X + 12f * scale;
        var fitted = Typography.FitText(label, width - retryWidth, TextStyles.Subheadline);
        var size = Typography.Measure(fitted, TextStyles.Subheadline);
        var textY = origin.Y + (height - size.Y) * 0.5f;
        Typography.Draw(drawList, new Vector2(origin.X, textY), fitted, ui.MutedInk, TextStyles.Subheadline);
        if (retry.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(max.X - retryWidth + 12f * scale, textY), retry, ui.Accent,
                TextStyles.Subheadline);
        }

        ImGui.Dummy(new Vector2(width, height));
        if (retryable && UiInteract.Click(min, max, hovered))
        {
            community.RetryDirectory();
        }
    }

    private static LiveGroup GroupOf(CommunityStationDto station)
    {
        if (station.IsLive)
        {
            return LiveGroup.OnAir;
        }

        if (station.NextBroadcastAtUnix > 0)
        {
            return LiveGroup.Upcoming;
        }

        return station.IsFollowing ? LiveGroup.Followed : LiveGroup.Resting;
    }

    private void DrawLiveGroup(float scale, LiveGroup group, string heading)
    {
        var any = false;
        for (var index = 0; index < filteredStations.Count; index++)
        {
            if (GroupOf(filteredStations[index]) != group)
            {
                continue;
            }

            if (!any)
            {
                any = true;
                SectionHeader.Draw(ui, heading, false, 0f);
            }

            DrawCommunityRow(scale, filteredStations[index], 6f);
        }
    }

    private void ApplyTagFilter(CommunityStationDto[] stations)
    {
        filteredStations.Clear();
        for (var index = 0; index < stations.Length; index++)
        {
            if (GroupOf(stations[index]) == LiveGroup.OnAir || tagFilter.Length == 0 || HasTag(stations[index], tagFilter))
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

    private RadioStationText RowTextFor(CommunityStationDto station)
    {
        if (!communityRowTexts.TryGetValue(station.Id, out var texts))
        {
            texts = new RadioStationText();
            communityRowTexts[station.Id] = texts;
        }

        var now = ImGui.GetTime();
        if (texts.NeedsRefresh(station, now))
        {
            texts.Refresh(station, now, OffAirMark(station), ScheduleLine(station), string.Empty);
        }

        return texts;
    }

    private void DrawCommunityRow(float scale, CommunityStationDto station, float sideInset)
    {
        var rowHeight = CommunityRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, rowHeight, ui.HoverWash);
        var min = cell.Bounds.Min;
        var max = cell.Bounds.Max;
        var inset = sideInset * scale;
        var artSize = 50f * scale;
        var artMin = new Vector2(min.X + inset, min.Y + (rowHeight - artSize) * 0.5f);
        var artMax = artMin + new Vector2(artSize, artSize);
        DrawStationArt(drawList, artMin, artMax, station, 10f * scale);

        var texts = RowTextFor(station);
        var current = IsCurrentCommunityStation(station);
        var textLeft = artMax.X + 12f * scale;
        var textWidth = max.X - inset - (current ? 34f : 8f) * scale - textLeft;
        var nameY = min.Y + 12f * scale;
        var fittedName = texts.Name.Fit(station.Name, textWidth, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, nameY), fittedName, current ? ui.Accent : ui.TitleInk,
            TextStyles.BodyEmphasized);

        var statusY = min.Y + 33f * scale;
        DrawLiveMark(drawList, new Vector2(textLeft, statusY), scale, station, texts, textWidth);

        var nowPlaying = NowPlayingFor(station);
        var subtitle = nowPlaying.Length > 0 ? nowPlaying : texts.Schedule;
        if (subtitle.Length == 0)
        {
            subtitle = station.Description;
        }

        if (subtitle.Length > 0)
        {
            var fittedSubtitle = texts.Subtitle.Fit(subtitle, textWidth, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + 47f * scale), fittedSubtitle, ui.MutedInk,
                TextStyles.Caption1);
        }

        if (current)
        {
            Equalizer.Draw(drawList, new Vector2(max.X - inset - 14f * scale, min.Y + rowHeight * 0.5f), scale,
                17f * scale, clock, ui.Accent, 1f, playback.IsPlaying);
        }

        if (cell.Tapped)
        {
            OpenStationPage(station);
        }

        FeedCell.End(drawList, cell, ui.Hairline);
    }

    private void DrawStationArt(ImDrawListPtr drawList, Vector2 min, Vector2 max, CommunityStationDto station,
        float rounding, ImDrawFlags corners = ImDrawFlags.RoundCornersAll)
    {
        if (station.ArtworkUrl.Length > 0 && images.Sized(station.ArtworkUrl, max.X - min.X) is { } texture)
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, max.Y - min.Y);
            drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, corners);
            return;
        }

        drawList.AddImageRounded(artwork.HandleForName(station.Name), min, max, Vector2.Zero, Vector2.One,
            0xFFFFFFFFu, rounding, corners);
    }

    private void DrawLiveMark(ImDrawListPtr drawList, Vector2 origin, float scale, CommunityStationDto station,
        RadioStationText texts, float available)
    {
        if (!station.IsLive)
        {
            var offAir = texts.Status.Fit(texts.OffAir, available, TextStyles.Caption1);
            Typography.Draw(drawList, origin, offAir, ui.MutedInk, TextStyles.Caption1);
            return;
        }

        var label = texts.Live.Prefixed(Loc.T(L.Music.LiveBadge), L.Music.ListeningCount, station.Listeners);
        LivePill.Draw(drawList, origin, label, ui.Theme.Danger, clock, scale);
        DrawLiveChatMark(drawList, new Vector2(origin.X + LivePill.Width(label, scale) + Metrics.Space.Sm * scale,
            origin.Y), LivePill.Height(scale), origin.X + available, scale);
    }

    private void DrawLiveChatMark(ImDrawListPtr drawList, Vector2 origin, float height, float right, float scale)
    {
        var label = Loc.T(L.Music.Live.LiveChat);
        var glyphWidth = Metrics.Space.Lg * scale;
        var labelWidth = Typography.Measure(label, TextStyles.Caption2).X;
        if (origin.X + glyphWidth + labelWidth > right)
        {
            return;
        }

        var centerY = origin.Y + height * 0.5f;
        AppSkin.Icon(drawList, new Vector2(origin.X + glyphWidth * 0.4f, centerY), IconGlyph.Of(FontAwesomeIcon.Comments),
            ui.Accent, 0.6f);
        Typography.Draw(drawList, new Vector2(origin.X + glyphWidth,
            centerY - Typography.LineHeight(TextStyles.Caption2) * 0.5f), label, ui.Accent, TextStyles.Caption2);
    }

    private static string OffAirMark(CommunityStationDto station)
    {
        var offAir = Loc.T(L.Music.OffAir);
        if (station.NextBroadcastAtUnix > 0)
        {
            return offAir + " · " + TimeText.FutureMoment(station.NextBroadcastAtUnix);
        }

        if (station.LastLiveAtUnix > 0)
        {
            return string.Format(Loc.T(L.Music.LastLive), TimeText.Ago(station.LastLiveAtUnix));
        }

        return offAir;
    }

    private static string ScheduleLine(CommunityStationDto station)
    {
        if (station.NextBroadcastAtUnix <= 0)
        {
            return string.Empty;
        }

        return string.Format(Loc.T(L.Music.NextBroadcast), TimeText.FutureMoment(station.NextBroadcastAtUnix));
    }

    private string NowPlayingFor(CommunityStationDto station)
    {
        if (IsCurrentCommunityStation(station) && playback.RadioNowPlaying.Length > 0)
        {
            return playback.RadioNowPlaying;
        }

        return station.IsLive ? station.NowPlaying : string.Empty;
    }
}
