using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float MiniArtUnits = 40f;
    private const float MiniButtonRadius = 17f;
    private const float MiniButtonGap = 4f;
    private const float MiniPlayGlyph = 8f;
    private const float MiniSkipGlyph = 7f;
    private const float MiniStopGlyph = 6f;
    private const float MiniProgressThickness = 2f;
    private const float MiniProgressInset = 3f;
    private const float MiniHiddenArtAlpha = 0.06f;
    private const float MiniFlyingThreshold = 0.02f;

    private static readonly MarqueeId MiniTitleMarquee = new("music.mini.", "title");

    private void DrawMiniPlayer(Rect stage, float scale)
    {
        var presence = Math.Clamp(miniPresence.Value, 0f, 1f);
        if (presence <= 0.01f || !playback.IsActive)
        {
            miniArtKnown = false;
            return;
        }

        var height = MiniPlayerHeight * scale;
        var slide = (height + MiniPlayerGap * scale) * (1f - presence);
        var side = Metrics.Space.GlassInset * scale;
        var min = new Vector2(stage.Min.X + side, stage.Max.Y - height + slide);
        var max = new Vector2(stage.Max.X - side, stage.Max.Y + slide);
        using var layer = ScreenLayer.Begin("music.mini", new Rect(new Vector2(stage.Min.X, min.Y), stage.Max), false);
        var drawList = ImGui.GetWindowDrawList();
        var radius = height * 0.5f;
        Elevation.Card(drawList, min, max, radius, scale, presence);
        Material.ThemedGlass(drawList, min, max, radius, scale, ui.BackdropColor, TabBar.GlassOpacity);
        var centerY = (min.Y + max.Y) * 0.5f;
        var artSide = MiniArtUnits * scale;
        var artMin = new Vector2(min.X + (height - artSide) * 0.5f + MiniProgressInset * scale,
            centerY - artSide * 0.5f);
        miniArtRect = new Rect(artMin, artMin + new Vector2(artSide, artSide));
        miniArtKnown = true;
        DrawMiniArt(drawList, artMin, artSide);
        var buttonRadius = MiniButtonRadius * scale;
        var nextCenter = new Vector2(max.X - radius * 0.5f - buttonRadius * 0.5f, centerY);
        var playCenter = new Vector2(nextCenter.X - buttonRadius * 2f - MiniButtonGap * scale, centerY);
        var textLeft = artMin.X + artSide + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, playCenter.X - buttonRadius - Metrics.Space.Sm * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = centerY - (titleHeight + subtitleHeight) * 0.5f;
        Marquee.DrawLeft(drawList, MiniTitleMarquee, playback.Title, textLeft, top, textWidth,
            TextStyles.FootnoteEmphasized, ui.TitleInk, true);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
            Typography.FitText(playback.Subtitle, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        DrawMiniProgress(drawList, min, max, radius, scale);
        var playHit = new Vector2(buttonRadius, buttonRadius);
        var overButtons = UiInteract.Hover(playCenter - playHit, playCenter + playHit) ||
                          UiInteract.Hover(nextCenter - playHit, nextCenter + playHit);
        if (!overButtons && UiInteract.HoverClick(min, max))
        {
            OpenNowPlaying();
        }

        var playScale = MiniButton(drawList, "music.mini.play", playCenter, buttonRadius, true, out var playTapped);
        DrawPlayPause(drawList, playCenter, MiniPlayGlyph * scale * playScale, ui.TitleInk);
        if (playTapped)
        {
            playback.TogglePlayPause();
        }

        if (playback.IsPaused)
        {
            DrawMiniStop(drawList, nextCenter, buttonRadius, playHit, scale);
            return;
        }

        var nextEnabled = playback.SongActive || playback.HasQueue;
        var nextScale = MiniButton(drawList, "music.mini.next", nextCenter, buttonRadius, nextEnabled,
            out var nextTapped);
        MediaGlyph.Next(drawList, nextCenter, MiniSkipGlyph * scale * nextScale,
            ImGui.GetColorU32(nextEnabled ? ui.TitleInk : ui.MutedInk));
        HoverTooltip.Show(new Rect(nextCenter - playHit, nextCenter + playHit), Loc.T(L.Common.Next),
            HoverLabelSide.Above);
        if (nextTapped)
        {
            playback.Next();
        }
    }

    private void DrawMiniStop(ImDrawListPtr drawList, Vector2 center, float buttonRadius, Vector2 hit, float scale)
    {
        var stopScale = MiniButton(drawList, "music.mini.stop", center, buttonRadius, true, out var stopTapped);
        MediaGlyph.Stop(drawList, center, MiniStopGlyph * scale * stopScale, ImGui.GetColorU32(ui.TitleInk));
        HoverTooltip.Show(new Rect(center - hit, center + hit), Loc.T(L.Music.NowPlaying.StopPlaying),
            HoverLabelSide.Above);
        if (stopTapped)
        {
            StopPlaying();
        }
    }

    private void StopPlaying()
    {
        CloseNowPlaying();
        playback.Stop();
    }

    private void DrawMiniArt(ImDrawListPtr drawList, Vector2 artMin, float artSide)
    {
        if (nowPlaying.Openness > MiniFlyingThreshold)
        {
            Squircle.Fill(drawList, artMin, artMin + new Vector2(artSide, artSide), artSide * MiniArtRadiusFraction,
                ImGui.GetColorU32(ui.TitleInk with { W = MiniHiddenArtAlpha }));
            return;
        }

        ArtworkTile.Draw(drawList, images, artMin, artSide, playback.ArtworkUrl, playback.Title,
            MiniArtRadiusFraction);
    }

    private void DrawMiniProgress(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale)
    {
        if (!playback.CanSeek)
        {
            return;
        }

        var left = min.X + radius;
        var right = max.X - radius;
        if (right <= left)
        {
            return;
        }

        var y = max.Y - MiniProgressInset * scale;
        var half = MiniProgressThickness * scale * 0.5f;
        var fraction = Math.Clamp(playback.Position / playback.Duration, 0f, 1f);
        drawList.AddRectFilled(new Vector2(left, y - half), new Vector2(right, y + half),
            ImGui.GetColorU32(ui.TitleInk with { W = 0.12f }), half);
        drawList.AddRectFilled(new Vector2(left, y - half), new Vector2(left + (right - left) * fraction, y + half),
            ImGui.GetColorU32(ui.TitleInk with { W = 0.75f }), half);
    }

    private float MiniButton(ImDrawListPtr drawList, string id, Vector2 center, float radius, bool enabled,
        out bool tapped)
    {
        var hit = new Vector2(radius, radius);
        var hovered = enabled && UiInteract.Hover(center - hit, center + hit);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, down, PressFx.ControlPressedScale);
        if (hovered)
        {
            drawList.AddCircleFilled(center, radius * grow, ImGui.GetColorU32(ui.TitleInk with { W = 0.08f }));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        tapped = enabled && UiInteract.Click(center - hit, center + hit, hovered);
        return grow;
    }
}
