using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float TracksSheetHeight = 0.62f;
    private const int SubtitlesOff = 0;

    private readonly SheetSurface tracksSheet = new("aetherstream.tracks");
    private readonly Action<Rect> drawTracksContent;
    private MediaTrack[] tracks = [];
    private string[] trackLabels = [];
    private int tracksLoadCount = -1;

    private void OpenTracksSheet()
    {
        RefreshTracks(true);
        addSheet.Close();
        tracksSheet.Open();
    }

    private void RefreshTracks(bool force)
    {
        if (!force && tracksLoadCount == video.LoadCount)
        {
            return;
        }

        tracksLoadCount = video.LoadCount;
        tracks = video.ReadTracks();
        var labels = new string[tracks.Length];
        var audioNumber = 0;
        var subtitleNumber = 0;
        for (var index = 0; index < tracks.Length; index++)
        {
            var number = tracks[index].Kind switch
            {
                MediaTrackKind.Audio => ++audioNumber,
                MediaTrackKind.Subtitle => ++subtitleNumber,
                _ => 0,
            };
            labels[index] = MediaTracks.Label(tracks[index],
                string.Format(Loc.Culture, Loc.T(L.AetherStream.TrackNumber), number));
        }

        trackLabels = labels;
    }

    private void DrawTracksSheet(Rect area, float scale)
    {
        tracksSheet.Draw(area, SheetSkin.From(ui.Palette, Ink), Loc.T(L.AetherStream.TracksTitle), TracksSheetHeight,
            drawTracksContent);
    }

    private void DrawTracksContent(Rect content)
    {
        RefreshTracks(false);
        using (AppSurface.Begin(content, 0f))
        {
            var audioCount = MediaTracks.Count(tracks, MediaTrackKind.Audio);
            var subtitleCount = MediaTracks.Count(tracks, MediaTrackKind.Subtitle);
            if (audioCount == 0 && subtitleCount == 0)
            {
                SettingsSection.Hint(Loc.T(L.AetherStream.TracksNone), accentedTheme);
                return;
            }

            if (audioCount > 0)
            {
                SettingsSection.Header(Loc.T(L.AetherStream.TracksAudio), accentedTheme);
                DrawTrackGroup(MediaTrackKind.Audio, audioCount, false);
                Gap(Metrics.Space.Md);
            }

            SettingsSection.Header(Loc.T(L.AetherStream.TracksSubtitles), accentedTheme);
            if (subtitleCount == 0)
            {
                SettingsSection.Hint(Loc.T(L.AetherStream.TracksNoSubtitles), accentedTheme);
            }
            else
            {
                DrawTrackGroup(MediaTrackKind.Subtitle, subtitleCount, true);
            }

            Gap(Metrics.Space.Lg);
        }
    }

    private void DrawTrackGroup(MediaTrackKind kind, int count, bool offersOff)
    {
        var card = GroupCard.Begin(accentedTheme, count + (offersOff ? 1 : 0));
        var picked = -1;
        var anySelected = false;
        for (var index = 0; index < tracks.Length; index++)
        {
            if (tracks[index].Kind != kind)
            {
                continue;
            }

            anySelected |= tracks[index].Selected;
            if (SettingsRow.Selectable(card.NextRow(), trackLabels[index], tracks[index].Selected, accentedTheme))
            {
                picked = tracks[index].Id;
            }
        }

        if (offersOff && SettingsRow.Selectable(card.NextRow(), Loc.T(L.Common.Off), !anySelected, accentedTheme))
        {
            picked = SubtitlesOff;
        }

        card.End();
        if (picked < 0)
        {
            return;
        }

        video.SelectTrack(kind, picked);
        for (var index = 0; index < tracks.Length; index++)
        {
            if (tracks[index].Kind == kind)
            {
                tracks[index] = tracks[index] with { Selected = tracks[index].Id == picked };
            }
        }
    }
}
