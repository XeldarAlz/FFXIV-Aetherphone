using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float ChipHeight = 52f;
    private const float ChipGap = 8f;
    private const float CardGap = 12f;
    private const float CategoryTileHeight = 64f;
    private const float StationRowHeight = 60f;

    private void DrawHome(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        EnsureFeatured();
        DrawTopBar(context, Greeting(), null);
        var barRect = SearchBarRect(content, scale);
        UiAnchors.Report("music.search", barRect);
        DrawSearchPill(barRect, scale);
        var body = new Rect(new Vector2(content.Min.X, barRect.Max.Y),
            new Vector2(content.Max.X, BodyBottom(content, scale)));
        var gridWidth = body.Width - 32f * scale;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            DrawRecentChips(scale, gridWidth);
            DrawShelfHeading(Loc.T(L.Music.MadeForYou), scale);
            DrawFeaturedShelf(scale, gridWidth);
            DrawRadioHeading(scale);
            DrawCategoryGrid(scale, gridWidth);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawSearchPill(Rect bar, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X + 16f * scale, bar.Min.Y + 8f * scale);
        var pillMax = new Vector2(bar.Max.X - 16f * scale, bar.Max.Y - 8f * scale);
        var rounding = (pillMax.Y - pillMin.Y) * 0.5f;
        var hovered = UiInteract.Hover(pillMin, pillMax);
        var fill = hovered ? Palette.WithAlpha(ui.TitleInk, 0.16f) : ui.FieldSurface;
        Squircle.Fill(drawList, pillMin, pillMax, rounding, ImGui.GetColorU32(fill));
        var centerY = (pillMin.Y + pillMax.Y) * 0.5f;
        AppSkin.Icon(new Vector2(pillMin.X + 18f * scale, centerY), FontAwesomeIcon.Search.ToIconString(),
            ui.MutedInk, 0.85f);
        var hint = Typography.FitText(Loc.T(L.Music.SearchSongs), pillMax.X - pillMin.X - 48f * scale,
            TextStyles.Callout);
        var hintSize = Typography.Measure(hint, TextStyles.Callout);
        Typography.Draw(new Vector2(pillMin.X + 34f * scale, centerY - hintSize.Y * 0.5f), hint, ui.MutedInk,
            TextStyles.Callout);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            OpenSearch();
        }
    }

    private void DrawShelfHeading(string title, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var fitted = Typography.FitText(title, width, TextStyles.Title3);
        Typography.Draw(origin, fitted, ui.Palette.HeadingInk, TextStyles.Title3);
        ImGui.Dummy(new Vector2(0f, 8f * scale));
    }

    private void DrawRadioHeading(float scale)
    {
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var iconBox = 30f * scale;
        var title = Typography.FitText(Loc.T(L.Music.RadioStations), width - iconBox - 8f * scale, TextStyles.Title3);
        var titleSize = Typography.Measure(title, TextStyles.Title3);
        Typography.Draw(origin, title, ui.Palette.HeadingInk, TextStyles.Title3);
        var iconCenter = new Vector2(origin.X + width - iconBox * 0.5f, origin.Y + titleSize.Y * 0.5f);
        var iconMin = iconCenter - new Vector2(iconBox * 0.5f, iconBox * 0.5f);
        var iconMax = iconCenter + new Vector2(iconBox * 0.5f, iconBox * 0.5f);
        var hovered = UiInteract.Hover(iconMin, iconMax);
        if (hovered)
        {
            Squircle.Fill(ImGui.GetWindowDrawList(), iconMin, iconMax, iconBox * 0.5f, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        AppSkin.Icon(iconCenter, FontAwesomeIcon.Search.ToIconString(), ui.MutedInk, 0.9f);
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            OpenRadioSearch();
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
    }

    private void DrawRecentChips(float scale, float available)
    {
        var recents = history.Recent(RecentTiles);
        if (recents.Length == 0)
        {
            return;
        }

        var gap = ChipGap * scale;
        var chipWidth = (available - gap) * 0.5f;
        var chipHeight = ChipHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var rows = (recents.Length + 1) / 2;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < recents.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (chipWidth + gap), origin.Y + row * (chipHeight + gap));
            var max = min + new Vector2(chipWidth, chipHeight);
            var rounding = 8f * scale;
            var song = recents[index];
            var current = IsCurrentSong(song);
            var hovered = UiInteract.Hover(min, max);
            var fill = current
                ? Palette.WithAlpha(ui.Accent, 0.16f)
                : Palette.WithAlpha(ui.TitleInk, hovered ? 0.13f : 0.07f);
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(fill));
            var artMax = new Vector2(min.X + chipHeight, max.Y);
            DrawCover(drawList, min, artMax, song.ThumbnailUrl, song.Title, rounding);
            var textLeft = artMax.X + 10f * scale;
            var trailing = current ? 26f * scale : 10f * scale;
            var textWidth = max.X - trailing - textLeft;
            var title = Typography.FitText(song.Title, textWidth, TextStyles.FootnoteEmphasized);
            Typography.Draw(new Vector2(textLeft, min.Y + 9f * scale), title, current ? ui.Accent : ui.TitleInk,
                TextStyles.FootnoteEmphasized);
            var author = Typography.FitText(song.Author, textWidth, TextStyles.Caption1);
            Typography.Draw(new Vector2(textLeft, min.Y + 28f * scale), author, ui.MutedInk, TextStyles.Caption1);
            if (current)
            {
                Equalizer.Draw(drawList, new Vector2(max.X - 15f * scale, (min.Y + max.Y) * 0.5f), scale,
                    15f * scale, clock, ui.Accent, 1f, playback.IsPlaying);
            }

            if (!hovered)
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                continue;
            }

            if (current)
            {
                playback.TogglePlayPause();
            }
            else
            {
                PlaySong(recents, index, Loc.T(L.Music.RecentlyPlayed));
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(available, rows * chipHeight + (rows - 1) * gap));
    }

    private void DrawFeaturedShelf(float scale, float available)
    {
        if (featured.Length == 0)
        {
            if (featuredLoading)
            {
                DrawFeaturedSkeleton(scale, available);
            }

            return;
        }

        var gap = CardGap * scale;
        var cardWidth = (available - gap) * 0.5f;
        var artSize = cardWidth;
        var cardHeight = artSize + 40f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var rows = (featured.Length + 1) / 2;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < featured.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (cardWidth + gap), origin.Y + row * (cardHeight + gap));
            var artMin = min;
            var artMax = artMin + new Vector2(artSize, artSize);
            var rounding = 9f * scale;
            var song = featured[index];
            var current = IsCurrentSong(song);
            var cardMax = new Vector2(min.X + cardWidth, min.Y + cardHeight);
            var hovered = UiInteract.Hover(min, cardMax);
            drawList.AddRectFilled(artMin + new Vector2(0f, 4f * scale), artMax + new Vector2(0f, 5f * scale),
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), rounding);
            DrawCover(drawList, artMin, artMax, song.ThumbnailUrl, song.Title, rounding);
            var badgeClicked = false;
            if (hovered || current)
            {
                var badgeCenter = new Vector2(artMax.X - 24f * scale, artMax.Y - 24f * scale);
                badgeClicked = MusicRenderer.PlayButton(FeaturedPlayIds[index % FeaturedPlayIds.Length], badgeCenter,
                    15f * scale, ui.Accent, new Vector4(0.04f, 0.05f, 0.04f, 1f), current && playback.IsPlaying);
            }

            var textWidth = cardWidth - 2f * scale;
            var title = Typography.FitText(song.Title, textWidth, TextStyles.FootnoteEmphasized);
            Typography.Draw(new Vector2(artMin.X, artMax.Y + 6f * scale), title, current ? ui.Accent : ui.TitleInk,
                TextStyles.FootnoteEmphasized);
            var author = Typography.FitText(song.Author, textWidth, TextStyles.Caption1);
            Typography.Draw(new Vector2(artMin.X, artMax.Y + 24f * scale), author, ui.MutedInk, TextStyles.Caption1);
            if (badgeClicked || (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)))
            {
                if (current)
                {
                    playback.TogglePlayPause();
                }
                else
                {
                    PlaySong(featured, index, Loc.T(L.Music.MadeForYou));
                }
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(available, rows * cardHeight + (rows - 1) * gap));
    }

    private void DrawFeaturedSkeleton(float scale, float available)
    {
        var gap = CardGap * scale;
        var cardWidth = (available - gap) * 0.5f;
        var artSize = cardWidth;
        var cardHeight = artSize + 40f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pulse = 0.05f + 0.04f * Pulse.Wave(1600f);
        var fill = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, pulse));
        for (var index = 0; index < FeaturedTiles; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (cardWidth + gap), origin.Y + row * (cardHeight + gap));
            var artMax = min + new Vector2(artSize, artSize);
            Squircle.Fill(drawList, min, artMax, 9f * scale, fill);
            Squircle.Fill(drawList, new Vector2(min.X, artMax.Y + 8f * scale),
                new Vector2(min.X + cardWidth * 0.72f, artMax.Y + 18f * scale), 4f * scale, fill);
            Squircle.Fill(drawList, new Vector2(min.X, artMax.Y + 25f * scale),
                new Vector2(min.X + cardWidth * 0.45f, artMax.Y + 33f * scale), 4f * scale, fill);
        }

        var rows = (FeaturedTiles + 1) / 2;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(available, rows * cardHeight + (rows - 1) * gap));
    }

    private void DrawCategoryGrid(float scale, float available)
    {
        var categories = RadioService.Categories;
        var gap = CardGap * scale;
        var tileWidth = (available - gap) * 0.5f;
        var tileHeight = CategoryTileHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var rows = (categories.Length + 1) / 2;
        UiAnchors.Report("music.categories",
            new Rect(origin, origin + new Vector2(available, rows * tileHeight + (rows - 1) * gap)));
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < categories.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            var max = min + new Vector2(tileWidth, tileHeight);
            var rounding = 10f * scale;
            var hovered = UiInteract.Hover(min, max);
            var seed = ArtGradient.Seed(categories[index].Tag);
            drawList.AddImageRounded(artwork.Handle(seed), min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
            drawList.AddRectFilledMultiColor(min, new Vector2(max.X, min.Y + tileHeight * 0.7f), 0x59000000u,
                0x59000000u, 0u, 0u);
            if (hovered)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), rounding);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var label = Typography.FitText(CatalogLabels.RadioCategory(categories[index].Display),
                tileWidth - 24f * scale, TextStyles.SubheadlineEmphasized);
            Typography.Draw(new Vector2(min.X + 12f * scale, min.Y + 10f * scale), label,
                new Vector4(1f, 1f, 1f, 1f), TextStyles.SubheadlineEmphasized);
            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                OpenCategory(index);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(available, rows * tileHeight + (rows - 1) * gap));
    }

    private void DrawStations(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        DrawTopBar(context, StationsTitle(), GoToHome);
        var barRect = SearchBarRect(content, scale);
        if (focusRadioSearch)
        {
            focusRadioSearch = false;
            ImGui.SetKeyboardFocusHere();
        }

        var submitted = SearchField.DrawSubmit(barRect, "##radioSearch", Loc.T(L.Music.SearchStations),
            ref radioSearchDraft, SearchFieldSurface, SearchFieldHint, SearchFieldInk, 80, 16f);
        if (submitted && !string.IsNullOrWhiteSpace(radioSearchDraft))
        {
            BeginRadioSearch(radioSearchDraft);
        }

        var body = new Rect(new Vector2(content.Min.X, barRect.Max.Y),
            new Vector2(content.Max.X, BodyBottom(content, scale)));
        if (loading)
        {
            LoadingPulse.Draw(new Vector2(body.Center.X, body.Center.Y - 14f * scale), 13f * scale, ui.Accent,
                ui.MutedInk, Loc.T(L.Music.TuningIn));
            return;
        }

        if (stations.Length == 0)
        {
            DrawStationsPlaceholder(body, scale);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < stations.Length; index++)
            {
                DrawStationRow(scale, stations[index], index);
            }

            if (loadingMore)
            {
                InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            if (InfiniteScroll.ReachedBottom() && stationHasMore && !loadingMore)
            {
                LoadMoreStations();
            }
        }
    }

    private void DrawStationsPlaceholder(Rect body, float scale)
    {
        if (categoryIndex >= 0 || !string.IsNullOrEmpty(radioQuery))
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Music.NoStations), ui.MutedInk, TextStyles.Callout);
            return;
        }

        var center = new Vector2(body.Center.X, body.Center.Y - 20f * scale);
        Typography.DrawCentered(center, Loc.T(L.Music.RadioSearchTitle), ui.TitleInk, TextStyles.Title3);
        var maxWidth = body.Width - 48f * scale;
        Typography.DrawWrappedCentered(new Vector2(center.X, center.Y + 20f * scale), Loc.T(L.Music.RadioSearchSub),
            ui.MutedInk, TextStyles.Subheadline, maxWidth);
    }

    private void DrawStationRow(float scale, RadioStation station, int index)
    {
        var rowHeight = StationRowHeight * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        var current = IsCurrentStation(station);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, 10f * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var artSize = 44f * scale;
        var artMin = new Vector2(min.X + 6f * scale, min.Y + (rowHeight - artSize) * 0.5f);
        var artMax = artMin + new Vector2(artSize, artSize);
        drawList.AddImageRounded(artwork.HandleForName(station.Name), artMin, artMax, Vector2.Zero, Vector2.One,
            0xFFFFFFFFu, 8f * scale, ImDrawFlags.RoundCornersAll);
        var trailing = current ? 32f * scale : 10f * scale;
        var textLeft = artMax.X + 12f * scale;
        var textWidth = max.X - trailing - textLeft;
        var name = Typography.FitText(station.Name, textWidth, TextStyles.BodyEmphasized);
        Typography.Draw(new Vector2(textLeft, min.Y + 10f * scale), name, current ? ui.Accent : ui.TitleInk,
            TextStyles.BodyEmphasized);
        var subtitle = Typography.FitText(StationSubtitle(station), textWidth, TextStyles.Caption1);
        Typography.Draw(new Vector2(textLeft, min.Y + 34f * scale), subtitle, ui.MutedInk, TextStyles.Caption1);
        if (current)
        {
            Equalizer.Draw(drawList, new Vector2(max.X - 18f * scale, min.Y + rowHeight * 0.5f), scale, 17f * scale,
                clock, ui.Accent, 1f, playback.IsPlaying);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (!hovered || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        if (current)
        {
            playback.TogglePlayPause();
        }
        else
        {
            PlayStation(index);
        }
    }
}
