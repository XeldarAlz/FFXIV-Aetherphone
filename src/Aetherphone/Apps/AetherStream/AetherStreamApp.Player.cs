using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private static readonly Vector4 WhiteInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 HeroPillBacking = new(0f, 0f, 0f, 0.45f);

    private const float HeroAspect = 9f / 16f;
    private const float ActionButtonRadius = 22f;

    private string urlInput = string.Empty;
    private bool queueOnAdd;
    private float composerModeAnimation;
    private string? pendingLocalFile;
    private Spring heroActionsFade;

    private VideoQueueEntry? CurrentEntry => watchAlong.IsViewing ? watchAlong.ViewingEntry : queue.Current;

    private readonly ControlCenter controlCenter;

    private void DrawNowPlaying(Rect body, float scale)
    {
        if (Interlocked.Exchange(ref pendingLocalFile, null) is { } localPath)
        {
            SubmitLocalFile(localPath);
        }

        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawHero(width, scale);
            DrawPlaybackError(width, scale);
            DrawNowPlayingTitle(width, scale);
            DrawProgressBlock(width, scale);
            DrawTransportBlock(width, scale);
            DrawVolumeBlock(width, scale);
            DrawActionRow(width, scale);
            DrawComposer(width, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawHero(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = width * HeroAspect;
        var min = origin;
        var max = origin + new Vector2(width, height);
        UiAnchors.Report("aetherstream.hero", new Rect(min, max));
        var rounding = Metrics.Radius.Card * scale;
        var drawList = ImGui.GetWindowDrawList();
        var current = CurrentEntry;

        Elevation.Card(drawList, min, max, rounding, scale);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.FieldSurface));

        var liveHandle = screen.Engine.ScreenViewHandle;
        if (liveHandle != nint.Zero && video.HasMedia && video.FrameVersion > 0)
        {
            drawList.AddImageRounded(new ImTextureID(liveHandle), min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                rounding, ImDrawFlags.RoundCornersAll);
        }
        else
        {
            DrawHeroPlaceholder(drawList, min, max, rounding, current);
        }

        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ui.Palette.CardStroke), 1f);

        DrawHeroOverlay(drawList, min, max, scale, current);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawHeroPlaceholder(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding,
        VideoQueueEntry? current)
    {
        var thumbnail = VideoThumbnailResolver.Get(remoteImages, http, current?.Url, current?.ThumbnailUrl);
        if (thumbnail is not null)
        {
            drawList.AddImageRounded(thumbnail.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
            return;
        }

        if (current is not null)
        {
            AppSkin.Icon((min + max) * 0.5f, FontAwesomeIcon.Tv.ToIconString(), ui.MutedInk, 1.8f);
            return;
        }

        EmptyState.Draw(new Rect(min, max), ui, FontAwesomeIcon.Tv, Loc.T(L.AetherStream.NothingPlaying),
            Loc.T(L.AetherStream.NothingPlayingHint));
    }

    private void DrawHeroOverlay(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale,
        VideoQueueEntry? current)
    {
        var delta = ImGui.GetIO().DeltaTime;
        var loading = video.State == VideoPlaybackState.Loading;
        var presentable = video.HasMedia && !loading;
        var heroHovered = presentable && UiInteract.Hover(min, max);
        var eased = Math.Clamp(heroActionsFade.Step(heroHovered ? 1f : 0f, 0.12f, delta), 0f, 1f);
        if (eased > 0.01f)
        {
            Squircle.Fill(drawList, min, max, Metrics.Radius.Card * scale,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.38f * eased)));
        }

        if (loading)
        {
            Squircle.Fill(drawList, min, max, Metrics.Radius.Card * scale,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.45f)));
            LoadingPulse.Draw(new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f - 10f * scale),
                14f * scale, ui.Accent, WhiteInk, Loc.T(L.AetherStream.LoadingVideo), 1f, 0.8f, drawList);
        }

        if (screen.Engine.IsActive)
        {
            var pillLabel = current is not null
                ? Loc.T(L.Common.Live)
                : Loc.T(L.AetherStream.PlayerCastingWaiting);
            var pillOrigin = min + new Vector2(Metrics.Space.Md * scale, Metrics.Space.Md * scale);
            var pillSize = new Vector2(LivePill.Width(pillLabel, scale), LivePill.Height(scale));
            Squircle.Fill(drawList, pillOrigin, pillOrigin + pillSize, pillSize.Y * 0.34f,
                ImGui.GetColorU32(HeroPillBacking));
            LivePill.Draw(drawList, pillOrigin, pillLabel, current is not null ? theme.Danger : ui.MutedInk,
                (float)ImGui.GetTime(), scale);
        }

        var canStop = !watchAlong.IsViewing && (queue.Current is not null || video.HasMedia);
        if (canStop)
        {
            var stopRadius = 13f * scale;
            var stopCenter = new Vector2(max.X - Metrics.Space.Md * scale - stopRadius,
                min.Y + Metrics.Space.Md * scale + stopRadius);
            if (HoverButton.Circle(drawList, "aetherstream.hero.stop", stopCenter, stopRadius, FontAwesomeIcon.Stop,
                    HeroPillBacking, WhiteInk, ImGui.GetIO().DeltaTime, 1f, true, Loc.T(L.AetherStream.Stop)))
            {
                queue.StopPlayback();
            }
        }

        DrawHeroFacepile(drawList, min, max, scale);
        DrawHeroPresentActions(drawList, min, max, scale, eased, heroHovered, delta);
    }

    private void DrawHeroPresentActions(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale, float eased,
        bool heroHovered, float delta)
    {
        if (eased <= 0.01f)
        {
            return;
        }

        var center = (min + max) * 0.5f;
        var radius = 19f * scale;
        var spacing = radius + 13f * scale;
        var fullscreenCenter = new Vector2(center.X - spacing, center.Y);
        var windowCenter = new Vector2(center.X + spacing, center.Y);

        if (HoverButton.Circle(drawList, "aetherstream.hero.fullscreen", fullscreenCenter, radius,
                FontAwesomeIcon.Expand, HeroPillBacking, WhiteInk, delta, eased, heroHovered,
                Loc.T(L.AetherStream.Fullscreen)))
        {
            EnterTheater();
        }

        if (HoverButton.Circle(drawList, "aetherstream.hero.window", windowCenter, radius,
                FontAwesomeIcon.WindowRestore, HeroPillBacking, WhiteInk, delta, eased, heroHovered,
                Loc.T(L.AetherStream.OpenScreenWindow)))
        {
            screenWindow.IsOpen = true;
        }
    }

    private void EnterTheater()
    {
        upNextSheet.Close();
        partySheet.Close();
        screenSheet.Close();
        controlCenter.Dismiss();
        AppLandscape.Request(Id);
    }

    private void DrawHeroFacepile(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale)
    {
        var watchers = watchAlong.Watching();
        if (watchers.Count == 0)
        {
            return;
        }

        var radius = 11f * scale;
        var step = radius * 1.5f;
        var shown = Math.Min(watchers.Count, 3);
        var right = max.X - Metrics.Space.Md * scale - radius;
        var centerY = max.Y - Metrics.Space.Md * scale - radius;

        for (var index = 0; index < shown; index++)
        {
            var participant = watchers[watchers.Count - 1 - index];
            var center = new Vector2(right - step * index, centerY);
            drawList.AddCircleFilled(center, radius + 1.5f * scale, ImGui.GetColorU32(HeroPillBacking), 24);
            AvatarView.DrawRemote(drawList, center, radius, theme, participant.DisplayName, string.Empty,
                participant.AvatarUrl, remoteImages, lodestone, 0.6f, 16);
        }

        var label = string.Format(Loc.T(L.AetherStream.WatchingCount), watchers.Count);
        var labelSize = Typography.Measure(label, TextStyles.Caption2);
        var labelRight = right - step * (shown - 1) - radius - Metrics.Space.Sm * scale;
        var labelOrigin = new Vector2(labelRight - labelSize.X, centerY - labelSize.Y * 0.5f);
        var backingPad = 5f * scale;
        Squircle.Fill(drawList, labelOrigin - new Vector2(backingPad, backingPad * 0.6f),
            labelOrigin + labelSize + new Vector2(backingPad, backingPad * 0.6f), labelSize.Y * 0.5f,
            ImGui.GetColorU32(HeroPillBacking));
        Typography.Draw(drawList, labelOrigin, label, WhiteInk, TextStyles.Caption2);
    }

    private void DrawPlaybackError(float width, float scale)
    {
        if (video.LastError is { } error)
        {
            DrawPlaybackNotice(width, scale, error, theme.Danger);
            return;
        }

        if (video.RecoveryNotice is { } notice)
        {
            DrawPlaybackNotice(width, scale, notice, ui.MutedInk);
        }
    }

    private void DrawPlaybackNotice(float width, float scale, string text, Vector4 ink)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var origin = ImGui.GetCursorScreenPos();
        var pad = Metrics.Space.Md * scale;
        var textWidth = width - pad * 2f;
        var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, textWidth).Y;
        var cardHeight = textHeight + pad * 2f;
        var drawList = ImGui.GetWindowDrawList();
        var max = origin + new Vector2(width, cardHeight);
        Squircle.Fill(drawList, origin, max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ink, 0.10f)));
        Squircle.Stroke(drawList, origin, max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ink, 0.35f)), 1f);
        Typography.DrawWrappedLeft(origin + new Vector2(pad, pad), text, ink, TextStyles.Footnote,
            textWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private void DrawNowPlayingTitle(float width, float scale)
    {
        var current = CurrentEntry;
        if (current is null)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var origin = ImGui.GetCursorScreenPos();
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        Marquee.DrawLeftAuto("aetherstream.nowPlaying.title", current.Title, origin.X, origin.Y, width,
            TextStyles.Title3, ui.TitleInk);
        var sourceY = origin.Y + titleHeight + 2f * scale;
        var sourceHeight = 0f;
        if (current.Source.Length > 0)
        {
            sourceHeight = Typography.LineHeight(TextStyles.Caption1) + 2f * scale;
            Marquee.DrawLeftAuto("aetherstream.nowPlaying.source", current.Source, origin.X, sourceY, width,
                TextStyles.Caption1, ui.MutedInk);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, titleHeight + sourceHeight + Metrics.Space.Sm * scale));
    }

    private void DrawProgressBlock(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var interactive = !watchAlong.IsViewing;
        var progress = video.Progress;
        var position = progress.Position;
        var duration = progress.Duration;
        var normalized = progress.Fraction;
        var sliderRow = new Rect(origin, origin + new Vector2(width, 24f * scale));
        var shown = position;

        if (interactive && duration > 0f)
        {
            var result = Slider.Draw("aetherstream.progress", sliderRow, normalized, accentedTheme, 0f, 0f);
            if (result.Released)
            {
                video.Seek(result.Value * duration);
            }

            if (result.Dragging || result.Released)
            {
                shown = result.Value * duration;
            }
        }
        else
        {
            var track = new Rect(new Vector2(origin.X, sliderRow.Center.Y - 2f * scale),
                new Vector2(origin.X + width, sliderRow.Center.Y + 2f * scale));
            Scrubber.Draw(track, normalized, ui.Accent, Palette.WithAlpha(ui.MutedInk, 0.3f),
                interactive ? 1f : 0.4f);
        }

        var labelY = sliderRow.Max.Y + 2f * scale;
        Typography.Draw(new Vector2(origin.X, labelY), TimeText.MinutesSeconds((int)shown), ui.MutedInk,
            TextStyles.Caption1);
        var remainingText = $"-{TimeText.MinutesSeconds((int)MathF.Max(0f, duration - shown))}";
        var remainingSize = Typography.Measure(remainingText, TextStyles.Caption1);
        Typography.Draw(new Vector2(origin.X + width - remainingSize.X, labelY), remainingText, ui.MutedInk,
            TextStyles.Caption1);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 24f * scale + Typography.LineHeight(TextStyles.Caption1) + 4f * scale));
    }

    private void DrawTransportBlock(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = 60f * scale;
        UiAnchors.Report("aetherstream.transport",
            new Rect(origin, new Vector2(origin.X + width, origin.Y + rowHeight)));
        var centerY = origin.Y + rowHeight * 0.5f;
        var centerX = origin.X + width * 0.5f;
        var interactive = !watchAlong.IsViewing;
        var transportAlpha = interactive ? 1f : 0.4f;
        var progress = video.Progress;
        var position = progress.Position;
        var paused = progress.Paused;

        var drawList = ImGui.GetWindowDrawList();
        var delta = ImGui.GetIO().DeltaTime;
        if (interactive && HoverButton.Circle(drawList, "aetherstream.seek.back",
                new Vector2(centerX - 132f * scale, centerY), 16f * scale, FontAwesomeIcon.UndoAlt,
                AppSkin.Transparent, ui.TitleInk, delta, 1f, true))
        {
            video.Seek(Math.Max(0f, position - 10f));
        }

        if (TransportButton.Draw(new Vector2(centerX - 72f * scale, centerY), 18f * scale, TransportAction.Previous,
                ui.TitleInk, Palette.WithAlpha(ui.TitleInk, 0.85f), transportAlpha, interactive) && interactive)
        {
            queue.Restart();
        }

        var playAction = paused || !video.HasMedia ? TransportAction.Play : TransportAction.Pause;
        var centerRadius = 24f * scale;
        var centerPoint = new Vector2(centerX, centerY);
        drawList.AddCircleFilled(centerPoint, centerRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, transportAlpha)), 40);
        if (TransportButton.Draw(centerPoint, centerRadius, playAction, ui.Accent, WhiteInk, transportAlpha,
                interactive) && interactive)
        {
            TogglePlayback(paused);
        }

        var canAdvance = interactive && queue.HasNext;
        if (TransportButton.Draw(new Vector2(centerX + 72f * scale, centerY), 18f * scale, TransportAction.Next,
                ui.TitleInk, Palette.WithAlpha(ui.TitleInk, 0.85f), canAdvance ? transportAlpha : 0.35f,
                canAdvance) && canAdvance)
        {
            queue.Advance();
        }

        if (interactive && HoverButton.Circle(drawList, "aetherstream.seek.forward",
                new Vector2(centerX + 132f * scale, centerY), 16f * scale, FontAwesomeIcon.RedoAlt,
                AppSkin.Transparent, ui.TitleInk, delta, 1f, true))
        {
            video.Seek(position + 10f);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void TogglePlayback(bool paused)
    {
        if (!video.HasMedia)
        {
            if (queue.Current is null && queue.HasNext)
            {
                queue.Advance();
            }

            return;
        }

        video.Pause(!paused);
    }

    private void DrawVolumeBlock(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var row = new Rect(origin, origin + new Vector2(width, 26f * scale));
        var result = VolumeSlider.Draw("aetherstream.volume", row, configuration.VideoVolume, accentedTheme);
        if (result.Dragging && Math.Abs(result.Value - configuration.VideoVolume) > 0.001f)
        {
            configuration.VideoVolume = result.Value;
            video.SetVolume((int)(result.Value * 100f));
        }

        if (result.Released)
        {
            configuration.VideoVolume = result.Value;
            video.SetVolume((int)(result.Value * 100f));
            configuration.Save();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 26f * scale));
    }

    private void DrawActionRow(float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var origin = ImGui.GetCursorScreenPos();
        var radius = ActionButtonRadius * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Caption2);
        var rowHeight = radius * 2f + labelHeight + Metrics.Space.Xs * scale;
        UiAnchors.Report("aetherstream.actions",
            new Rect(origin, new Vector2(origin.X + width, origin.Y + rowHeight)));
        var slot = width / 3f;
        var centerY = origin.Y + radius;

        DrawActionButton("aetherstream.action.upNext", new Vector2(origin.X + slot * 0.5f, centerY), radius,
            FontAwesomeIcon.ListUl, Loc.T(L.AetherStream.UpNext), QueueBadgeCount(), upNextSheet, scale, labelHeight);
        DrawActionButton("aetherstream.action.party", new Vector2(origin.X + slot * 1.5f, centerY), radius,
            FontAwesomeIcon.UserFriends, Loc.T(L.AetherStream.Party), watchAlong.PendingRequests.Count, partySheet,
            scale, labelHeight);
        DrawActionButton("aetherstream.action.screen", new Vector2(origin.X + slot * 2.5f, centerY), radius,
            FontAwesomeIcon.Tv, Loc.T(L.AetherStream.Screen), 0, screenSheet, scale, labelHeight);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private int QueueBadgeCount()
    {
        if (watchAlong.IsViewing)
        {
            return watchAlong.HostQueue.Count;
        }

        return queue.Entries.Count + watchAlong.PendingQueueSuggestions.Count;
    }

    private void DrawActionButton(string id, Vector2 center, float radius, FontAwesomeIcon icon, string label,
        int badge, SheetSurface sheet, float scale, float labelHeight)
    {
        var drawList = ImGui.GetWindowDrawList();
        var active = sheet.IsOpen;
        var background = active ? Palette.WithAlpha(ui.Accent, 0.22f) : ui.FieldSurface;
        var ink = active ? ui.Accent : ui.TitleInk;

        if (HoverButton.Circle(drawList, id, center, radius, icon, background, ink, ImGui.GetIO().DeltaTime, 1f,
                true))
        {
            upNextSheet.Close();
            partySheet.Close();
            screenSheet.Close();
            sheet.Open();
        }

        if (badge > 0)
        {
            var badgeCenter = new Vector2(center.X + radius * 0.72f, center.Y - radius * 0.72f);
            AppBadge.Draw(badgeCenter, badge, theme, scale);
        }

        var labelSize = Typography.Measure(label, TextStyles.Caption2);
        Typography.Draw(drawList, new Vector2(center.X - labelSize.X * 0.5f,
                center.Y + radius + Metrics.Space.Xs * scale), label, ui.MutedInk, TextStyles.Caption2);
    }

    private void DrawComposer(float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var origin = ImGui.GetCursorScreenPos();
        var suggesting = watchAlong.IsViewing;
        var sendRadius = 17f * scale;
        var fieldRowHeight = 44f * scale;
        var fieldRect = new Rect(origin,
            origin + new Vector2(width - sendRadius * 2f - Metrics.Space.Md * scale, fieldRowHeight));
        var hint = suggesting ? Loc.T(L.AetherStream.SuggestHint) : Loc.T(L.AetherStream.UrlHint);
        var submitted = SubmitField.Draw(fieldRect, "##aetherstreamUrl", hint, ref urlInput, accentedTheme, 2000,
            FontAwesomeIcon.Link);

        var drawList = ImGui.GetWindowDrawList();
        var delta = ImGui.GetIO().DeltaTime;
        var canSubmit = urlInput.Trim().Length > 0;
        var sendCenter = new Vector2(origin.X + width - sendRadius, origin.Y + fieldRowHeight * 0.5f);
        var sendIcon = suggesting ? FontAwesomeIcon.PaperPlane : FontAwesomeIcon.ArrowUp;
        var sendBackground = canSubmit ? ui.Accent : Palette.WithAlpha(ui.Accent, 0.35f);
        var sendInk = canSubmit ? WhiteInk : Palette.WithAlpha(WhiteInk, 0.6f);
        if (HoverButton.Circle(drawList, "aetherstream.composer.send", sendCenter, sendRadius, sendIcon,
                sendBackground, sendInk, delta, 1f, canSubmit) && canSubmit)
        {
            submitted = true;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, fieldRowHeight));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));

        var rowOrigin = ImGui.GetCursorScreenPos();
        var rowHeight = 34f * scale;
        var circleRadius = rowHeight * 0.5f;
        var pasteCenter = new Vector2(rowOrigin.X + circleRadius, rowOrigin.Y + circleRadius);
        if (HoverButton.Circle(drawList, "aetherstream.composer.paste", pasteCenter, circleRadius,
                FontAwesomeIcon.Paste, ui.FieldSurface, ui.TitleInk, delta, 1f, true,
                Loc.T(L.AetherStream.PasteClipboard), HoverLabelSide.Above))
        {
            var clipboard = ImGui.GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboard))
            {
                urlInput = clipboard.Trim();
            }
        }

        if (!suggesting)
        {
            var folderCenter = new Vector2(pasteCenter.X + circleRadius * 2f + Metrics.Space.Sm * scale,
                rowOrigin.Y + circleRadius);
            if (HoverButton.Circle(drawList, "aetherstream.composer.browse", folderCenter, circleRadius,
                    FontAwesomeIcon.FolderOpen, ui.FieldSurface, ui.TitleInk, delta, 1f, true,
                    Loc.T(L.AetherStream.BrowseLocalFile), HoverLabelSide.Above))
            {
                FilePicker.PickVideo(Loc.T(L.AetherStream.BrowseLocalFile),
                    path => Interlocked.Exchange(ref pendingLocalFile, path));
            }

            var leftEdge = folderCenter.X + circleRadius + Metrics.Space.Md * scale;
            var segmentWidth = MathF.Min(210f * scale, rowOrigin.X + width - leftEdge);
            var segmentRect = new Rect(new Vector2(rowOrigin.X + width - segmentWidth, rowOrigin.Y),
                new Vector2(rowOrigin.X + width, rowOrigin.Y + rowHeight));
            var mode = SegmentSlider.Draw(segmentRect, Loc.T(L.AetherStream.PlayNow),
                Loc.T(L.AetherStream.AddToQueue), queueOnAdd ? 1 : 0, ref composerModeAnimation, ui.Accent,
                ui.MutedInk);
            queueOnAdd = mode == 1;
        }

        ImGui.SetCursorScreenPos(rowOrigin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        UiAnchors.Report("aetherstream.composer",
            new Rect(origin, new Vector2(origin.X + width, rowOrigin.Y + rowHeight)));

        if (!submitted || !canSubmit)
        {
            return;
        }

        if (suggesting)
        {
            watchAlong.SuggestQueueItem(urlInput.Trim());
            urlInput = string.Empty;
            return;
        }

        SubmitUrl();
    }

    private void SubmitUrl()
    {
        var url = urlInput.Trim();
        urlInput = string.Empty;
        SubmitEntry(queue.CreateDisplayEntry(url));
    }

    private void SubmitLocalFile(string path)
    {
        SubmitEntry(new VideoQueueEntry(path, Path.GetFileNameWithoutExtension(path),
            Loc.T(L.AetherStream.LocalFileSource), null, null));
    }

    private void SubmitEntry(VideoQueueEntry entry)
    {
        if (queueOnAdd)
        {
            queue.Add(entry);
            return;
        }

        queue.PlayNow(entry);
    }
}
