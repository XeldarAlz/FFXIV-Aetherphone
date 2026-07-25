using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float SongRowHeight = 60f;
    private const float ScopeRowHeight = 42f;

    private static readonly Vector4 SearchFieldSurface = new(0.96f, 0.96f, 0.96f, 1f);
    private static readonly Vector4 SearchFieldHint = new(0.38f, 0.39f, 0.40f, 1f);
    private static readonly Vector4 SearchFieldInk = new(0.07f, 0.07f, 0.08f, 1f);

    private void DrawSearch(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        DrawTopBar(context, Loc.T(L.Common.Search), GoToHome);
        var barRect = SearchBarRect(content, scale);
        if (focusSearch)
        {
            focusSearch = false;
            ImGui.SetKeyboardFocusHere();
        }

        var submitted = SearchField.DrawSubmit(barRect, "##musicSearch", Loc.T(L.Music.SearchSongs), ref searchDraft,
            SearchFieldSurface, SearchFieldHint, SearchFieldInk, 120, 16f);
        if (submitted && !string.IsNullOrWhiteSpace(searchDraft))
        {
            BeginSearch(searchDraft);
        }

        DrawScopeChips(content, barRect, scale);
        var body = new Rect(new Vector2(content.Min.X, barRect.Max.Y + ScopeRowHeight * scale),
            new Vector2(content.Max.X, BodyBottom(content, scale)));
        if (searching)
        {
            LoadingPulse.Draw(new Vector2(body.Center.X, body.Center.Y - 14f * scale), 13f * scale, ui.Accent,
                ui.MutedInk, Loc.T(L.Common.Searching));
            return;
        }

        if (results.Length == 0)
        {
            DrawSearchPlaceholder(body, scale);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < results.Length; index++)
            {
                DrawSongRow(scale, results[index], index);
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
        }
    }

    private void DrawScopeChips(Rect content, Rect barRect, float scale)
    {
        var centerY = barRect.Max.Y + ScopeRowHeight * scale * 0.5f;
        var cursorX = content.Min.X + 16f * scale;
        var gap = 8f * scale;
        if (ui.FlowChip(ref cursorX, centerY, gap, Loc.T(L.Music.ScopeSongs), searchScope == SongSearchScope.Songs))
        {
            SetSearchScope(SongSearchScope.Songs);
        }

        if (ui.FlowChip(ref cursorX, centerY, gap, Loc.T(L.Music.ScopeLongPlays),
                searchScope == SongSearchScope.LongPlays))
        {
            SetSearchScope(SongSearchScope.LongPlays);
        }

        if (ui.FlowChip(ref cursorX, centerY, gap, Loc.T(L.Music.ScopeAll), searchScope == SongSearchScope.All))
        {
            SetSearchScope(SongSearchScope.All);
        }
    }

    private void DrawSearchPlaceholder(Rect body, float scale)
    {
        var title = hasSearched ? Loc.T(L.Music.NoResults) : Loc.T(L.Music.SearchEmptyTitle);
        var subtitle = hasSearched ? Loc.T(L.Music.NoResultsSub) : Loc.T(L.Music.SearchEmptySub);
        var center = new Vector2(body.Center.X, body.Center.Y - 20f * scale);
        Typography.DrawCentered(center, title, ui.TitleInk, TextStyles.Title3);
        var maxWidth = body.Width - 48f * scale;
        Typography.DrawWrappedCentered(new Vector2(center.X, center.Y + 20f * scale), subtitle, ui.MutedInk,
            TextStyles.Subheadline, maxWidth);
    }

    private void DrawSongRow(float scale, Song song, int index)
    {
        var rowHeight = SongRowHeight * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        var current = IsCurrentSong(song);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, 10f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var artSize = 44f * scale;
        var artMin = new Vector2(min.X + 6f * scale, min.Y + (rowHeight - artSize) * 0.5f);
        var artMax = artMin + new Vector2(artSize, artSize);
        DrawCover(drawList, artMin, artMax, song.ThumbnailUrl, song.Title, 6f * scale);
        var showAdd = hovered && !current;
        var trailing = current ? 32f * scale : showAdd ? 40f * scale : 10f * scale;
        var textLeft = artMax.X + 12f * scale;
        var textWidth = max.X - trailing - textLeft;
        var title = Typography.FitText(song.Title, textWidth, TextStyles.BodyEmphasized);
        Typography.Draw(new Vector2(textLeft, min.Y + 10f * scale), title, current ? ui.Accent : ui.TitleInk,
            TextStyles.BodyEmphasized);
        var subtitle = Typography.FitText(SongRowSubtitle(song), textWidth, TextStyles.Caption1);
        Typography.Draw(new Vector2(textLeft, min.Y + 34f * scale), subtitle, ui.MutedInk, TextStyles.Caption1);
        var addClicked = false;
        var overAdd = false;
        if (current)
        {
            Equalizer.Draw(drawList, new Vector2(max.X - 18f * scale, min.Y + rowHeight * 0.5f), scale, 17f * scale,
                clock, ui.Accent, 1f, playback.IsPlaying);
        }
        else if (showAdd)
        {
            var addCenter = new Vector2(max.X - 22f * scale, min.Y + rowHeight * 0.5f);
            var addRadius = 15f * scale;
            var addHit = new Vector2(addRadius, addRadius);
            overAdd = UiInteract.Hover(addCenter - addHit, addCenter + addHit);
            addClicked = ui.IconButton(addCenter, addRadius,
                FontAwesomeIcon.Plus.ToIconString(), ui.MutedInk, AppSkin.Transparent, 0.82f, Loc.T(L.Music.AddToPlaylist));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (addClicked)
        {
            OpenPicker(song);
            return;
        }

        if (overAdd || !UiInteract.Click(min, max, hovered))
        {
            return;
        }

        if (current)
        {
            playback.TogglePlayPause();
        }
        else
        {
            PlaySong(results, index, Loc.T(L.Music.SourceSearch));
        }
    }
}
