using Aetherphone.Core;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed partial class MinimizedPhone
{
    private const float StatusTop = 6f;
    private const float StatusHeight = 11f;
    private const float StatusInset = 10f;
    private const float HeroTop = 20f;
    private const float HeroInset = 6f;
    private const float ClockMaxScale = 2.4f;
    private const float ClockMinScale = 1.3f;
    private const float MapClockMaxScale = 1.3f;
    private const float MapClockMinScale = 0.9f;
    private const float SlotInset = 6f;
    private const float SlotBottom = 13f;
    private const float SlotHeight = 74f;
    private const float SlotRadius = 17f;
    private const float SlotPadding = 8f;
    private const float PageDotRoom = 5f;
    private const float BannerTop = 17f;
    private const float BannerInset = 5f;
    private const float BannerRadius = 14f;
    private const float BannerDrop = 12f;
    private const float BannerDim = 0.7f;
    private const float PillInset = 6f;
    private const float PillBottom = 13f;
    private const float PillHeight = 24f;
    private const float PillGap = 6f;
    private const float PillButtonRadius = 8f;
    private const float PillTitleScale = 0.6f;
    private const float PillDotRadius = 3f;

    private void DrawClockFace(ImDrawListPtr drawList, Rect screen)
    {
        var scale = frameScale;
        var heroAlpha = frameAlpha * (1f - BannerDim * BannerPresence);
        var line = new Rect(new Vector2(screen.Min.X + StatusInset * scale, screen.Min.Y + StatusTop * scale),
            new Vector2(screen.Max.X - StatusInset * scale, screen.Min.Y + (StatusTop + StatusHeight) * scale));
        MinimizedPhoneRenderer.DrawStatusLine(drawList, line, frameInk, heroAlpha, Math.Clamp(dnd.Value, 0f, 1f),
            countLabel, Math.Clamp(badge.Value, 0f, 1f), scale);
        var musicPlaying = ShowsPcMusic() ? pcSnapshot.IsPlaying : playback.IsPlaying;
        MinimizedPhoneRenderer.DrawIsland(drawList, line.Center, liveCall, musicPlaying, clock,
            heroAlpha * Math.Clamp(island.Value, 0f, 1f), scale);
        var showDate = layout.IsEnabled(MinimizedPart.Date);
        MinimizedPhoneRenderer.DrawHero(drawList, screen, screen.Min.Y + HeroTop * scale,
            showDate ? dateLabel : string.Empty, showDate ? dateSize : Vector2.Zero,
            layout.IsEnabled(MinimizedPart.Clock) ? timeLabel : string.Empty, clockScale, frameInk, heroAlpha);
        DrawSlot(drawList, screen);
        MinimizedPhoneRenderer.DrawIndicator(drawList, screen, frameInk, frameAlpha, scale);
    }

    private float BannerPresence => cardNotification is null ? 0f : Math.Clamp(card.Value, 0f, 1f);

    private void DrawSlot(ImDrawListPtr drawList, Rect screen)
    {
        var presence = Math.Clamp(slotPresence.Value, 0f, 1f);
        if (presence <= 0.01f || displayedMode == SlotMode.None)
        {
            return;
        }

        var scale = frameScale;
        var slot = new Rect(
            new Vector2(screen.Min.X + SlotInset * scale, screen.Max.Y - (SlotBottom + SlotHeight) * scale),
            new Vector2(screen.Max.X - SlotInset * scale, screen.Max.Y - SlotBottom * scale));
        DrawGlass(drawList, slot, SlotRadius * scale, frameAlpha * presence);
        slotHovered = frameBodyHovered && presence > 0.9f && UiInteract.Hover(slot.Min, slot.Max);
        var padding = SlotPadding * scale;
        var inner = new Rect(slot.Min + new Vector2(padding, padding), slot.Max - new Vector2(padding, padding));
        var fade = Math.Clamp(contentFade.Value, 0f, 1f);
        var alpha = frameAlpha * presence * fade;
        var active = frameInteractive && !dragging && fade > ControlThreshold;
        drawList.PushClipRect(slot.Min, slot.Max, true);
        switch (displayedMode)
        {
            case SlotMode.Pages:
                DrawPages(drawList, slot, inner, alpha);
                break;
            case SlotMode.Music:
                DrawMusicCard(drawList, inner, alpha, active);
                break;
            case SlotMode.PcMusic:
                DrawPcMusicCard(drawList, inner, alpha, active);
                break;
            case SlotMode.Call:
                var call = MinimizedPhoneRenderer.DrawCallCard(drawList, inner, frameView, DurationLabel(frameView),
                    clock, frameInk, alpha, active, scale);
                ApplyCallControl(call.Action);
                controlHovered |= call.Hovered;
                break;
        }

        drawList.PopClipRect();
    }

    private void DrawPages(ImDrawListPtr drawList, Rect slot, Rect inner, float alpha)
    {
        var scale = frameScale;
        if (pageCount > 1)
        {
            var room = PageDotRoom * scale;
            inner = new Rect(new Vector2(inner.Min.X + room, inner.Min.Y), new Vector2(inner.Max.X - room, inner.Max.Y));
        }

        var position = Math.Clamp(pagePosition.Value, 0f, MathF.Max(0f, pageCount - 1));
        var first = (int)MathF.Floor(position);
        for (var page = first; page <= first + 1; page++)
        {
            if (page < 0 || page >= pageCount)
            {
                continue;
            }

            var offset = (page - position) * slot.Height;
            var distance = MathF.Abs(offset) / slot.Height;
            if (distance >= 1f)
            {
                continue;
            }

            var part = pageParts[page];
            var height = MinimizedWidgetRenderer.Height(part, scale);
            var top = inner.Center.Y - height * 0.5f + offset;
            var rect = new Rect(new Vector2(inner.Min.X, top), new Vector2(inner.Max.X, top + height));
            MinimizedWidgetRenderer.Draw(drawList, rect, part, feed, configuration, frameTheme, frameInk,
                alpha * (1f - distance), scale);
        }

        MinimizedPhoneRenderer.DrawPageDots(drawList, slot, pageCount, position, frameInk, alpha, scale);
    }

    private void DrawMusicCard(ImDrawListPtr drawList, Rect inner, float alpha, bool active)
    {
        var scale = frameScale;
        var art = MinimizedPhoneRenderer.MusicArtRect(inner, scale);
        var radius = MinimizedPhoneRenderer.ArtRadius(art.Width);
        if (!NowPlayingArt.TryDrawSquircle(drawList, art.Min, art.Width, radius, playback.ArtworkUrl, alpha))
        {
            var swatch = ArtGradient.FromName(playback.Title);
            Squircle.FillVerticalGradient(drawList, art.Min, art.Max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(swatch.Top, alpha)),
                ImGui.GetColorU32(Palette.WithAlpha(swatch.Bottom, alpha)));
        }

        var state = new TransportState(playback.HasQueue, playback.HasQueue, true, playback.IsPlaying);
        var result = MinimizedPhoneRenderer.DrawTransport(drawList, inner, state, frameInk, alpha, active, scale);
        ApplyMusicControl(result.Action);
        controlHovered |= result.Hovered;
    }

    private void DrawBanner(ImDrawListPtr drawList, Rect screen)
    {
        if (cardNotification is not { } notification)
        {
            return;
        }

        var presence = BannerPresence;
        if (presence <= 0.01f)
        {
            return;
        }

        var scale = frameScale;
        var height = MinimizedPhoneRenderer.BannerHeight(scale);
        var top = screen.Min.Y + (BannerTop - BannerDrop * (1f - presence)) * scale;
        var banner = new Rect(new Vector2(screen.Min.X + BannerInset * scale, top),
            new Vector2(screen.Max.X - BannerInset * scale, top + height));
        var alpha = frameAlpha * presence;
        DrawGlass(drawList, banner, BannerRadius * scale, alpha);
        bannerHovered = frameBodyHovered && presence > 0.9f && UiInteract.Hover(banner.Min, banner.Max);
        drawList.PushClipRect(banner.Min, banner.Max, true);
        MinimizedPhoneRenderer.DrawBanner(drawList, banner, notification, frameInk, alpha, scale);
        drawList.PopClipRect();
    }

    private void DrawMapFace(ImDrawListPtr drawList, Rect screen)
    {
        var scale = frameScale;
        var alpha = frameAlpha;
        var drewMap = MinimapFace.Draw(drawList, screen, minimap, frameTheme, alpha, scale, mapSpan.Value);
        var headerAlpha = alpha * (1f - BannerDim * BannerPresence);
        MinimapFace.DrawHeader(drawList, screen, timeLabel, mapClockScale, drewMap ? minimap.ZoneName : string.Empty,
            headerAlpha, scale);
        var pill = MapPillPresence;
        if (drewMap)
        {
            MinimapFace.DrawCoordinates(drawList, screen, minimap.Coordinates, PillBottom * scale,
                alpha * (1f - pill), scale);
            var reveal = Math.Clamp(hover.Value, 0f, 1f);
            var zoom = MinimapFace.DrawZoom(drawList, screen, frameTheme, alpha * reveal, scale,
                configuration.MinimizedMapZoom, frameInteractive && !dragging);
            controlHovered |= zoom.Hovered;
            StepZoom(zoom.Step);
        }

        DrawMapPill(drawList, screen, pill);
        MinimizedPhoneRenderer.DrawIndicator(drawList, screen, frameInk, alpha, scale);
    }

    private float MapPillPresence => displayedMode is SlotMode.Music or SlotMode.PcMusic or SlotMode.Call
        ? Math.Clamp(slotPresence.Value, 0f, 1f) * Math.Clamp(contentFade.Value, 0f, 1f)
        : 0f;

    private void DrawMapPill(ImDrawListPtr drawList, Rect screen, float presence)
    {
        if (presence <= 0.01f)
        {
            return;
        }

        var scale = frameScale;
        var height = PillHeight * scale;
        var bottom = screen.Max.Y - PillBottom * scale;
        var pill = new Rect(new Vector2(screen.Min.X + PillInset * scale, bottom - height),
            new Vector2(screen.Max.X - PillInset * scale, bottom));
        var alpha = frameAlpha * presence;
        DrawGlass(drawList, pill, height * 0.5f, alpha);
        slotHovered = frameBodyHovered && presence > 0.9f && UiInteract.Hover(pill.Min, pill.Max);
        var active = frameInteractive && !dragging && presence > ControlThreshold;
        var gap = PillGap * scale;
        var iconRadius = height * 0.5f - gap * 0.5f;
        var iconCenter = new Vector2(pill.Min.X + height * 0.5f, pill.Center.Y);
        var buttonRadius = PillButtonRadius * scale;
        var buttonCenter = new Vector2(pill.Max.X - height * 0.5f, pill.Center.Y);
        var title = DrawPillLead(drawList, iconCenter, iconRadius, alpha, scale);
        var textLeft = iconCenter.X + iconRadius + gap;
        var textWidth = MathF.Max(1f, buttonCenter.X - buttonRadius - gap - textLeft);
        var style = new TextStyle(TextScale(PillTitleScale), FontWeight.SemiBold);
        var textHeight = Typography.Measure(title, style).Y;
        Marquee.DrawLeftAuto(drawList, "minimized.map.live", title, textLeft, pill.Center.Y - textHeight * 0.5f,
            textWidth, style, Palette.WithAlpha(frameInk.Strong, alpha));
        DrawPillControl(drawList, buttonCenter, buttonRadius, alpha, active, scale);
    }

    private string DrawPillLead(ImDrawListPtr drawList, Vector2 center, float radius, float alpha, float scale)
    {
        switch (displayedMode)
        {
            case SlotMode.Call:
                drawList.AddCircleFilled(center, PillDotRadius * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(MinimizedPhoneRenderer.CallTone, alpha)), 16);
                return frameView.PeerLabel;
            case SlotMode.PcMusic:
                var texture = pcMedia.Artwork(pcSnapshot, radius * 2f);
                PcMediaView.DrawDisc(drawList, center, radius, texture, pcSnapshot, MusicAccent, alpha);
                return PcMediaView.Title(pcSnapshot);
            default:
                NowPlayingArt.DrawDisc(drawList, center, radius, playback.ArtworkUrl, playback.Title, alpha);
                return playback.Title;
        }
    }

    private void DrawPillControl(ImDrawListPtr drawList, Vector2 center, float radius, float alpha, bool active,
        float scale)
    {
        if (displayedMode == SlotMode.Call)
        {
            var hangup = MinimizedPhoneRenderer.DrawHangup(drawList, center, radius, alpha, active, scale);
            ApplyCallControl(hangup.Action);
            controlHovered |= hangup.Hovered;
            return;
        }

        var pc = displayedMode == SlotMode.PcMusic;
        var playing = pc ? pcSnapshot.IsPlaying : playback.IsPlaying;
        var canToggle = !pc || pcSnapshot.CanPlayPause;
        var corner = new Vector2(radius, radius);
        controlHovered |= active && canToggle && UiInteract.Hover(center - corner, center + corner);
        if (!TransportButton.Draw(center, radius, playing ? TransportAction.Pause : TransportAction.Play, MusicAccent,
                frameInk.Strong, canToggle ? alpha : alpha * 0.4f, active && canToggle, drawList))
        {
            return;
        }

        if (pc)
        {
            ApplyPcMusicControl(MinimizedControl.PlayPause);
            return;
        }

        ApplyMusicControl(MinimizedControl.PlayPause);
    }
}
