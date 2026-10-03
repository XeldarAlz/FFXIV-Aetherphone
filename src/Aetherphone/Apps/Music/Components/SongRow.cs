using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music.Components;

internal enum SongRowAction : byte
{
    None,
    Play,
    Menu,
}

internal static class SongRow
{
    public const float Height = 58f;
    private const float TextGap = 12f;
    private const float MenuHitRadius = 15f;
    private const float MenuGlyphScale = 0.8f;
    private const float MenuReserve = 34f;
    private const float DurationGap = 8f;
    private const float EqualizerHeight = 15f;
    private const float NowPlayingVeilAlpha = 0.45f;
    private const float DownloadGlyphScale = 0.55f;
    private const float DownloadGlyphBox = 14f;
    private static readonly Vector4 NowPlayingVeil = new(0f, 0f, 0f, NowPlayingVeilAlpha);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static SongRowAction Draw(MusicKit kit, in Song song, bool showArt = true)
    {
        var scale = UiScale.Current;
        var height = Height * scale;
        var width = ScrollLayout.StableContentWidth();
        if (!ImGui.IsRectVisible(new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return SongRowAction.None;
        }

        var ui = kit.Ui;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, height, ui.HoverWash);
        var min = cell.Bounds.Min;
        var max = cell.Bounds.Max;
        var inset = MusicUi.Inset * scale;
        var current = kit.IsCurrent(song);
        var textLeft = min.X + inset;
        if (showArt)
        {
            var side = ArtworkTile.Side(ArtworkTile.RowArt);
            var artMin = new Vector2(min.X + inset, min.Y + (height - side) * 0.5f);
            ArtworkTile.Draw(drawList, kit.Images, artMin, side, song.ThumbnailUrl, song.Title);
            if (current)
            {
                var artMax = artMin + new Vector2(side, side);
                Squircle.Fill(drawList, artMin, artMax, side * ArtworkTile.TileRadiusFraction,
                    ImGui.GetColorU32(NowPlayingVeil));
                Equalizer.Draw(drawList, (artMin + artMax) * 0.5f, scale, EqualizerHeight * scale, kit.Clock, White,
                    1f, kit.Playback.IsPlaying);
            }

            textLeft = artMin.X + side + TextGap * scale;
        }

        var menuCenter = new Vector2(max.X - inset - MenuHitRadius * scale * 0.5f, min.Y + height * 0.5f);
        var duration = song.DurationSeconds > 0 ? MusicUi.Duration(song.DurationSeconds) : string.Empty;
        var durationWidth = duration.Length > 0 ? Typography.Measure(duration, TextStyles.Footnote).X : 0f;
        var downloaded = kit.Library.IsDownloaded(song.VideoId);
        var trailing = MenuReserve * scale + (durationWidth > 0f ? durationWidth + DurationGap * scale : 0f) +
                       (downloaded ? DownloadGlyphBox * scale : 0f);
        var textWidth = MathF.Max(1f, max.X - inset - trailing - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Body);
        var subtitleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var titleTop = min.Y + (height - titleHeight - subtitleHeight) * 0.5f;
        var title = Typography.FitText(song.Title, textWidth, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, titleTop), title, current ? ui.Accent : ui.TitleInk,
            TextStyles.Body);
        if (song.Author.Length > 0)
        {
            var subtitle = Typography.FitText(song.Author, textWidth, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(textLeft, titleTop + titleHeight), subtitle, ui.MutedInk,
                TextStyles.Subheadline);
        }

        var trailingX = menuCenter.X - MenuReserve * scale * 0.5f;
        if (durationWidth > 0f)
        {
            var durationHeight = Typography.LineHeight(TextStyles.Footnote);
            trailingX -= durationWidth;
            Typography.Draw(drawList, new Vector2(trailingX, min.Y + (height - durationHeight) * 0.5f), duration,
                ui.MutedInk, TextStyles.Footnote);
            trailingX -= DurationGap * scale;
        }

        if (downloaded)
        {
            AppSkin.Icon(drawList, new Vector2(trailingX - DownloadGlyphBox * scale * 0.5f, min.Y + height * 0.5f),
                IconGlyph.Of(FontAwesomeIcon.ArrowDown), ui.MutedInk, DownloadGlyphScale);
        }

        var menuTapped = ui.IconButton(menuCenter, MenuHitRadius * scale, IconGlyph.Of(FontAwesomeIcon.EllipsisH),
            ui.MutedInk, AppSkin.Transparent, MenuGlyphScale, Loc.T(L.Music.MoreOptions));
        var rightClicked = cell.Hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right);
        var separatorLeft = textLeft;
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, separatorLeft, max.X, max.Y, ui.Hairline);
        if (menuTapped || rightClicked)
        {
            return SongRowAction.Menu;
        }

        return cell.Tapped ? SongRowAction.Play : SongRowAction.None;
    }
}
