using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private void DrawSearch(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        AppHeader.Draw(context, Loc.T(L.Common.Search), GoToBrowse);
        var barRect = SearchBarRect(content, scale);
        if (DrawSearchBar(barRect, theme))
        {
            BeginSearch(searchDraft);
        }

        var body = new Rect(new Vector2(content.Min.X, barRect.Max.Y),
            new Vector2(content.Max.X, BodyBottom(content, scale)));
        if (searching)
        {
            LoadingPulse.Draw(new Vector2(body.Center.X, body.Center.Y - 14f * scale), 13f * scale, theme.Accent,
                theme.TextMuted, Loc.T(L.Common.Searching));
            DrawMiniPlayer(context, scale);
            return;
        }

        if (results.Length == 0)
        {
            Typography.DrawCentered(body.Center, hasSearched ? Loc.T(L.Music.NoResults) : Loc.T(L.Music.SearchForSong),
                theme.TextMuted);
            DrawMiniPlayer(context, scale);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            for (var index = 0; index < results.Length; index++)
            {
                DrawSongRow(theme, scale, results[index], index);
            }
        }

        DrawMiniPlayer(context, scale);
    }

    private void DrawSongRow(PhoneTheme theme, float scale, Song song, int index)
    {
        var rowHeight = 64f * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var dl = ImGui.GetWindowDrawList();
        var playing = IsCurrentSong(song);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        if (hovered || playing)
        {
            Squircle.Fill(dl, min, max, 14f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(playing ? Accent : theme.TextStrong, playing ? 0.10f : 0.06f)));
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var thumbSize = 46f * scale;
        var thumbMin = new Vector2(min.X + 10f * scale, min.Y + (rowHeight - thumbSize) * 0.5f);
        var thumbMax = thumbMin + new Vector2(thumbSize, thumbSize);
        DrawThumb(dl, thumbMin, thumbMax, song.ThumbnailUrl, song.Title, 10f * scale);
        var trailing = playing ? 26f * scale : 12f * scale;
        var textLeft = thumbMax.X + 12f * scale;
        var nameColor = playing ? Accent : theme.TextStrong;
        Typography.Draw(new Vector2(textLeft, min.Y + 12f * scale), Truncate(song.Title, 26), nameColor, 1.05f);
        Typography.Draw(new Vector2(textLeft, min.Y + 36f * scale), SongRowSubtitle(song),
            Palette.WithAlpha(theme.TextStrong, 0.62f), 0.8f);
        if (playing)
        {
            Equalizer.Draw(dl, new Vector2(max.X - trailing, min.Y + rowHeight * 0.5f), scale, 16f * scale, clock,
                Accent, 1f, playback.Songs.State == SongPlaybackState.Playing);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            PlaySong(results, index);
        }
    }

    private static string SongRowSubtitle(Song song)
    {
        var key = (song.Author, song.DurationSeconds);
        if (SongSubtitleCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var author = Truncate(song.Author, 20);
        var subtitle = string.IsNullOrEmpty(author)
            ? FormatTime(song.DurationSeconds)
            : $"{author} · {FormatTime(song.DurationSeconds)}";
        SongSubtitleCache[key] = subtitle;
        return subtitle;
    }
}
