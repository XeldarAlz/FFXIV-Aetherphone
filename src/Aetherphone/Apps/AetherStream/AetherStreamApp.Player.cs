using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private string urlInput = string.Empty;
    private bool queueOnAdd;
    private string? pendingLocalFile;

    private void DrawPlayerTab(Rect body, float scale)
    {
        if (Interlocked.Exchange(ref pendingLocalFile, null) is { } localPath)
        {
            SubmitLocalFile(localPath);
        }

        var margin = Metrics.Space.Lg * scale;
        var content = new Rect(new Vector2(body.Min.X + margin, body.Min.Y), new Vector2(body.Max.X - margin, body.Max.Y));

        // Whole tab scrolls now - nothing here reserved space for overflow before, so a long
        // watching list (once real remote participants exist, not just the local-only stub today)
        // would otherwise just get clipped off the bottom with no way to reach it.
        using (AppSurface.Begin(body))
        {
            // Temporary diagnostic for the Player-tab scroll bug (wheel + kinetic drag both
            // reported dead while Lock Position is on) - remove once confirmed fixed. Fires only
            // on actual wheel/click activity so it doesn't spam the log every frame.
            var scrollIo = ImGui.GetIO();
            if (scrollIo.MouseWheel != 0f || (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.IsWindowHovered()))
            {
                AepLog.Info($"[PlayerScroll] wheel={scrollIo.MouseWheel:F2} scrollY={ImGui.GetScrollY():F1} " +
                    $"scrollMaxY={ImGui.GetScrollMaxY():F1} lockPosition={configuration.LockPosition} " +
                    $"dragEnabled={DragScrollHost.Enabled} windowHovered={ImGui.IsWindowHovered()}");
            }

            // Screen stays active (and this row keeps showing) between videos now - see
            // AetherStreamQueue.Advance's doc comment on why natural queue-end no longer
            // deactivates the screen. "Nothing current" covers exactly that "waiting for the next
            // video" gap - checks ViewingEntry too, since a viewer's queue.Current is never set
            // (see DrawNowPlaying's own note) and would otherwise always read as waiting.
            var isCasting = screen.Engine.IsActive;
            var hasCurrent = watchAlong.IsViewing ? watchAlong.ViewingEntry is not null : queue.Current is not null;
            var watchers = watchAlong.Watching();

            var bodyTop = content.Min.Y;
            if (isCasting)
            {
                var castingRect = new Rect(new Vector2(content.Min.X, bodyTop),
                    new Vector2(content.Max.X, bodyTop + CastingRowHeight * scale));
                DrawCastingStatus(castingRect, scale, !hasCurrent);
                bodyTop = castingRect.Max.Y + 10f * scale;
            }

            var nowPlayingHeight = 200f * scale;
            DrawNowPlaying(
                new Rect(new Vector2(content.Min.X, bodyTop), new Vector2(content.Max.X, bodyTop + nowPlayingHeight)),
                scale);

            var progressTop = bodyTop + nowPlayingHeight + 12f * scale;
            var progressRect = new Rect(new Vector2(content.Min.X, progressTop), new Vector2(content.Max.X, progressTop + 24f * scale));
            DrawProgress(progressRect, scale);

            var transportTop = progressRect.Max.Y + 8f * scale;
            DrawTransport(new Rect(new Vector2(content.Min.X, transportTop), new Vector2(content.Max.X, transportTop + 48f * scale)), scale);

            var volumeTop = transportTop + 56f * scale;
            DrawVolume(new Rect(new Vector2(content.Min.X, volumeTop), new Vector2(content.Max.X, volumeTop + 24f * scale)), scale);

            var urlTop = volumeTop + 36f * scale;
            var urlEntryHeight = UrlEntryHeight * scale;
            DrawUrlEntry(new Rect(new Vector2(content.Min.X, urlTop), new Vector2(content.Max.X, urlTop + urlEntryHeight)), scale);

            var joinTop = urlTop + urlEntryHeight + 12f * scale;
            var joinRect = new Rect(new Vector2(content.Min.X, joinTop), new Vector2(content.Max.X, joinTop + 34f * scale));
            DrawJoinRow(joinRect, scale);
            var bottom = joinRect.Max.Y;

            if (watchers.Count > 0)
            {
                var watchingTop = bottom + 14f * scale;
                var watchingHeight = WatchingTotalHeight(watchers, scale);
                DrawWatching(new Rect(new Vector2(content.Min.X, watchingTop), new Vector2(content.Max.X, watchingTop + watchingHeight)),
                    watchers, scale);
                bottom = watchingTop + watchingHeight;
            }

            // Reserves the child window's real content height so it knows how far there is to
            // scroll - everything above was drawn straight onto the draw list (absolute
            // positions, not ImGui's layout cursor), which on its own leaves the child thinking
            // its content is empty.
            ImGui.SetCursorScreenPos(content.Min);
            ImGui.Dummy(new Vector2(content.Width, bottom - content.Min.Y + Metrics.Space.Lg * scale));
        }

    }

    // Join a friend's stream (mutual-contact + block checks happen entirely server-side - a
    // decline never says why, see WatchAlongSession.OnDeclined) or leave the one you're in.
    // Positioned directly above the Host/Watching list below it, not buried in a menu.
    private void DrawJoinRow(Rect rect, float scale)
    {
        if (watchAlong.IsViewing)
        {
            // Resync alongside Leave - a manual safety valve for when drift/lag makes playback or
            // screen placement look off, instead of waiting on the next heartbeat.
            var viewingHalf = rect.Width * 0.5f - 5f * scale;
            var resyncRect = new Rect(rect.Min, new Vector2(rect.Min.X + viewingHalf, rect.Max.Y));
            var leaveRect = new Rect(new Vector2(rect.Max.X - viewingHalf, rect.Min.Y), rect.Max);

            if (SmallButton(resyncRect, Loc.T(L.AetherStream.Resync), true, scale))
            {
                watchAlong.ResyncNow();
            }

            if (SmallButton(leaveRect, Loc.T(L.AetherStream.LeaveStream), true, scale, danger: true))
            {
                watchAlong.Leave();
            }

            return;
        }

        if (watchAlong.IsAwaitingApproval)
        {
            // True while waiting on the host's approve/deny decision (see
            // WatchAlongSession.OnJoinPending). Tapping this just cancels the request, same
            // Leave() the button above uses.
            if (SmallButton(rect, Loc.T(L.AetherStream.JoinWaitingApproval), true, scale, danger: true))
            {
                watchAlong.Leave();
            }

            return;
        }

        if (watchAlong.IsHosting)
        {
            // "Function like a party" - a room open via OpenParty() doesn't need a queued video to
            // stay alive, so ending it needs its own explicit action rather than just waiting for
            // the queue to empty (see WatchAlongSession's partyOpen).
            if (SmallButton(rect, Loc.T(L.AetherStream.EndParty), true, scale, danger: true))
            {
                watchAlong.Leave();
            }

            return;
        }

        // Neither hosting nor viewing - two separate entry points side by side: start your own
        // room (works with nothing queued yet) or join someone else's.
        var half = rect.Width * 0.5f - 5f * scale;
        var startRect = new Rect(rect.Min, new Vector2(rect.Min.X + half, rect.Max.Y));
        var joinRect = new Rect(new Vector2(rect.Max.X - half, rect.Min.Y), rect.Max);

        if (SmallButton(startRect, Loc.T(L.AetherStream.StartParty), true, scale))
        {
            watchAlong.OpenParty();
        }

        if (SmallButton(joinRect, Loc.T(L.AetherStream.JoinStream), true, scale))
        {
            nearbyRefreshTimer = NearbyRefreshIntervalSeconds; // Refresh immediately, not on next tick.
            router.Push(AetherStreamScreen.Join);
        }
    }

    private const float CastingRowHeight = 40f;

    // Matches DrawUrlEntry's own fixed layout math (field 38 + gap 10 + toggle row 30 + gap 10 +
    // submit button 38) - needed here so the watching list below it knows where it actually ends,
    // since DrawUrlEntry positions everything itself rather than through ImGui's layout cursor.
    private const float UrlEntryHeight = 126f;

    // Status left, stop right - deliberately just "casting or not", no product name and no viewer
    // count (that's the Watching section below, a separate opt-in feature with its own toggle).
    // Reuses SmallButton/the stop action already built for the Casting tab rather than a second
    // copy - this is the same "turn the screen off" action from a second entry point.
    private void DrawCastingStatus(Rect rect, float scale, bool waiting)
    {
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, 10f * scale, ImGui.GetColorU32(ui.FieldSurface));

        var dotColor = waiting ? new Vector4(1f, 0.8f, 0.3f, 1f) : new Vector4(0.4f, 1f, 0.5f, 1f);
        var dotCenter = new Vector2(rect.Min.X + 18f * scale, rect.Center.Y);
        drawList.AddCircleFilled(dotCenter, 4f * scale, ImGui.GetColorU32(dotColor), 16);

        var label = waiting ? Loc.T(L.AetherStream.PlayerCastingWaiting) : Loc.T(L.AetherStream.PlayerCastingStatus);
        var textLeft = dotCenter.X + 12f * scale;
        var textSize = Typography.Measure(label, TextStyles.Subheadline);
        Typography.Draw(new Vector2(textLeft, rect.Center.Y - textSize.Y * 0.5f),
            label, ui.TitleInk, TextStyles.Subheadline);

        // Hidden entirely while viewing someone else's stream - this row now shows for a viewer
        // too (isCasting no longer requires queue.Current, see DrawPlayerTab), but queue.Clear()
        // would desync their local screen from the host's without actually leaving the session.
        // Leave (see DrawJoinRow) is the correct exit for a viewer.
        if (watchAlong.IsViewing)
        {
            return;
        }

        var stopRect = new Rect(new Vector2(rect.Max.X - 84f * scale, rect.Min.Y + 6f * scale),
            new Vector2(rect.Max.X - 12f * scale, rect.Max.Y - 6f * scale));
        var canStop = queue.Current is not null || video.State != VideoPlaybackState.Idle;
        if (SmallButton(stopRect, Loc.T(L.AetherStream.Stop), canStop, scale, danger: true) && canStop)
        {
            queue.Clear();
        }
    }

    // Title/source stacked above a full-width thumbnail, not beside a small square one - matches
    // the layout you asked for over the concept art's own side-by-side arrangement.
    private void DrawNowPlaying(Rect rect, float scale)
    {
        // A viewer's own queue.Current never gets touched by the sync path (see
        // WatchAlongSession.ViewingEntry's doc comment) - use the display-only mirror instead so
        // the card actually reflects what's playing instead of reading "Nothing playing" the
        // whole time.
        var current = watchAlong.IsViewing ? watchAlong.ViewingEntry : queue.Current;
        var title = current?.Title ?? Loc.T(L.AetherStream.NothingPlaying);
        Typography.Draw(rect.Min, title, ui.TitleInk, TextStyles.Headline);

        var sourceTop = rect.Min.Y + 26f * scale;
        if (current is not null)
        {
            Typography.Draw(new Vector2(rect.Min.X, sourceTop), current.Source, ui.MutedInk, TextStyles.Subheadline);
        }

        var artTop = rect.Min.Y + 48f * scale;
        if (video.LastError is not null)
        {
            Typography.DrawWrappedLeft(new Vector2(rect.Min.X, sourceTop + 22f * scale), video.LastError,
                Palette.WithAlpha(theme.Danger, 0.9f), TextStyles.Footnote, rect.Width);
            artTop += 34f * scale;
        }

        var artMin = new Vector2(rect.Min.X, artTop);
        var artMax = new Vector2(rect.Max.X, rect.Max.Y);
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, artMin, artMax, 10f * scale, ImGui.GetColorU32(ui.FieldSurface));

        var thumbnail = VideoThumbnailResolver.Get(remoteImages, http, current?.Url, current?.ThumbnailUrl);
        if (thumbnail is not null)
        {
            drawList.AddImageRounded(thumbnail.Handle, artMin, artMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu,
                10f * scale, ImDrawFlags.RoundCornersAll);
        }
        else
        {
            AppSkin.Icon((artMin + artMax) * 0.5f, FontAwesomeIcon.Tv.ToIconString(), ui.MutedInk, 1.8f);
        }
    }

    // Unscaled row metrics, used both here and by DrawPlayerTab to size the reserved space before
    // any scaling is applied. Sized and positioned to match the concept art's watch-party list -
    // full-size avatar rows directly under the transport row, not the earlier compact strip.
    private const float WatchingHeaderHeight = 26f;
    private const float WatchingRowHeight = 52f;

    // Host gets its own labeled row up top; everyone else is a separate "Watching" group below it -
    // only ever one real entry right now (see WatchAlongRoster), so the second group stays hidden
    // until a real roster exists. The roster is already host-first; watchers[0] is always the host.
    private void DrawWatching(Rect rect, IReadOnlyList<WatchAlongParticipant> watchers, float scale)
    {
        var rowHeight = WatchingRowHeight * scale;
        var headerHeight = WatchingHeaderHeight * scale;
        var y = rect.Min.Y;

        Typography.Draw(new Vector2(rect.Min.X, y), Loc.T(L.AetherStream.WatchingHostLabel), ui.MutedInk,
            TextStyles.Caption1);
        y += headerHeight;
        // The host's own row (watchers[0]) never gets a kick control regardless of IsHosting -
        // that's you, not something to remove.
        DrawWatchingRow(new Rect(new Vector2(rect.Min.X, y), new Vector2(rect.Max.X, y + rowHeight)), watchers[0],
            false, scale);
        y += rowHeight;

        if (watchers.Count <= 1)
        {
            return;
        }

        y += 10f * scale;
        Typography.Draw(new Vector2(rect.Min.X, y), Loc.T(L.AetherStream.WatchingSectionLabel), ui.MutedInk,
            TextStyles.Caption1);
        y += headerHeight;
        // See WatchAlongSession.KickParticipant. Gated on IsHosting so a viewer never sees a
        // control for an action they have no authority to take.
        var canKick = watchAlong.IsHosting;
        for (var index = 1; index < watchers.Count; index++)
        {
            DrawWatchingRow(new Rect(new Vector2(rect.Min.X, y), new Vector2(rect.Max.X, y + rowHeight)),
                watchers[index], canKick, scale);
            y += rowHeight;
        }
    }

    private void DrawWatchingRow(Rect row, WatchAlongParticipant participant, bool canKick, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var avatarRadius = 18f * scale;
        var centerY = row.Center.Y;
        var avatarCenter = new Vector2(row.Min.X + avatarRadius, centerY);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, participant.Name, participant.World,
            participant.AvatarUrl, remoteImages, lodestone, 0.7f, 20);

        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var textRight = row.Max.X;
        if (canKick)
        {
            var kickRect = new Rect(new Vector2(row.Max.X - 64f * scale, row.Min.Y + 8f * scale),
                new Vector2(row.Max.X - 4f * scale, row.Max.Y - 8f * scale));
            if (SmallButton(kickRect, Loc.T(L.AetherStream.WatchingKick), true, scale, danger: true))
            {
                watchAlong.KickParticipant(participant.UserId);
            }

            textRight = kickRect.Min.X - 8f * scale;
        }

        var name = Ellipsize(participant.DisplayName, textRight - textLeft);
        var nameSize = Typography.Measure(name, TextStyles.Body);
        Typography.Draw(new Vector2(textLeft, centerY - nameSize.Y * 0.5f), name, ui.TitleInk, TextStyles.Body);
    }

    // Mirrors DrawWatching's own layout math (host label + host row, then - only when there's
    // anyone besides the host - a gap, a second label, and one row per remaining participant) so
    // DrawPlayerTab can size the reserved rect without duplicating the two-group logic.
    private static float WatchingTotalHeight(IReadOnlyList<WatchAlongParticipant> watchers, float scale)
    {
        if (watchers.Count == 0)
        {
            return 0f;
        }

        var height = WatchingHeaderHeight * scale + WatchingRowHeight * scale;
        var othersCount = watchers.Count - 1;
        if (othersCount > 0)
        {
            height += 10f * scale + WatchingHeaderHeight * scale + othersCount * WatchingRowHeight * scale;
        }

        return height;
    }

    private void DrawProgress(Rect rect, float scale)
    {
        // While watching someone else's stream, playback is entirely host-controlled - the next
        // stream.state would just override a local scrub/pause within moments anyway, so block the
        // interaction here instead of letting it silently snap back and look broken. Volume is
        // deliberately exempt (see DrawVolume) - that's a personal preference, not playback state.
        var interactive = !watchAlong.IsViewing;
        var (position, duration, _) = video.GetProgress();
        var normalized = duration > 0f ? Math.Clamp(position / duration, 0f, 1f) : 0f;
        var track = new Rect(new Vector2(rect.Min.X + 46f * scale, rect.Center.Y - 2f * scale),
            new Vector2(rect.Max.X - 46f * scale, rect.Center.Y + 2f * scale));

        // Stage 0 established seeking is cheap on this pipeline - mpv's own "seek <pos>
        // absolute" command, not a reload (docs/video-pipeline.md §3 in the AlphaChannel repo) -
        // so this scrubs live rather than only previewing a target and committing on release.
        var updated = Scrubber.Draw(track, normalized, ui.Accent, Palette.WithAlpha(ui.MutedInk, 0.3f),
            interactive ? 1f : 0.4f);
        if (interactive && Scrubber.IsHovered(track) && ImGui.IsMouseDown(ImGuiMouseButton.Left) && duration > 0f)
        {
            video.Seek(updated * duration);
        }

        Typography.Draw(new Vector2(rect.Min.X, rect.Center.Y - 7f * scale), TimeText.MinutesSeconds((int)position),
            ui.MutedInk, TextStyles.Caption1);
        var totalText = TimeText.MinutesSeconds((int)duration);
        var totalSize = Typography.Measure(totalText, TextStyles.Caption1);
        Typography.Draw(new Vector2(rect.Max.X - totalSize.X, rect.Center.Y - 7f * scale), totalText, ui.MutedInk,
            TextStyles.Caption1);
    }

    private void DrawTransport(Rect rect, float scale)
    {
        // See DrawProgress's note - transport is host-controlled while watching someone else's
        // stream. queue.Advance() specifically also needs blocking here: tapping it while Viewing
        // could populate queue.Current from a stale local queue, which OnFrameworkUpdate's own
        // "local queue action wins" rule would then read as the user choosing to leave the stream.
        var interactive = !watchAlong.IsViewing;
        var transportAlpha = interactive ? 1f : 0.4f;
        var centerY = rect.Center.Y;
        var centerX = rect.Center.X;
        var (_, _, paused) = video.GetProgress();

        if (interactive && ui.IconButton(new Vector2(centerX - 132f * scale, centerY), 16f * scale,
                FontAwesomeIcon.UndoAlt.ToIconString(), ui.TitleInk, AppSkin.Transparent, 0.6f))
        {
            var (position, _, _) = video.GetProgress();
            video.Seek(Math.Max(0f, position - 10f));
        }

        if (TransportButton.Draw(new Vector2(centerX - 66f * scale, centerY), 18f * scale, TransportAction.Previous,
                ui.TitleInk, Palette.WithAlpha(ui.TitleInk, 0.85f), transportAlpha, interactive) && interactive)
        {
            queue.Advance();
        }

        // Solid accent fill behind the centre button, not just TransportButton's own hover glow -
        // matches the concept art's filled play/pause circle. Same fill technique DrawSubmitButton
        // already uses below, just as a circle instead of a squircle.
        var playAction = paused ? TransportAction.Play : TransportAction.Pause;
        var centerRadius = 22f * scale;
        var centerPoint = new Vector2(centerX, centerY);
        ImGui.GetWindowDrawList().AddCircleFilled(centerPoint, centerRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, transportAlpha)), 32);
        if (TransportButton.Draw(centerPoint, centerRadius, playAction, ui.Accent, new Vector4(1f, 1f, 1f, 1f),
                transportAlpha, interactive) && interactive)
        {
            video.Pause(!paused);
        }

        if (TransportButton.Draw(new Vector2(centerX + 66f * scale, centerY), 18f * scale, TransportAction.Next,
                ui.TitleInk, Palette.WithAlpha(ui.TitleInk, 0.85f), transportAlpha, interactive) && interactive)
        {
            queue.Advance();
        }

        if (interactive && ui.IconButton(new Vector2(centerX + 132f * scale, centerY), 16f * scale,
                FontAwesomeIcon.RedoAlt.ToIconString(), ui.TitleInk, AppSkin.Transparent, 0.6f))
        {
            var (position, _, _) = video.GetProgress();
            video.Seek(position + 10f);
        }
    }

    private void DrawVolume(Rect rect, float scale)
    {
        var iconCenter = new Vector2(rect.Min.X + 10f * scale, rect.Center.Y);
        var glyph = configuration.VideoVolume <= 0.001f
            ? FontAwesomeIcon.VolumeMute
            : configuration.VideoVolume < 0.5f
                ? FontAwesomeIcon.VolumeDown
                : FontAwesomeIcon.VolumeUp;
        AppSkin.Icon(iconCenter, glyph.ToIconString(), ui.MutedInk, 0.55f);

        var track = new Rect(new Vector2(rect.Min.X + 28f * scale, rect.Center.Y - 2f * scale),
            new Vector2(rect.Max.X - 34f * scale, rect.Center.Y + 2f * scale));
        var updated = Scrubber.Draw(track, configuration.VideoVolume, ui.Accent, Palette.WithAlpha(ui.MutedInk, 0.3f),
            1f);
        if (Math.Abs(updated - configuration.VideoVolume) > 0.001f)
        {
            configuration.VideoVolume = updated;
            configuration.Save();
            video.SetVolume((int)(updated * 100));
        }

        var valueText = $"{configuration.VideoVolume * 100f:F0}%";
        var valueSize = Typography.Measure(valueText, TextStyles.Caption1);
        Typography.Draw(new Vector2(rect.Max.X - valueSize.X, rect.Center.Y - valueSize.Y * 0.5f), valueText,
            ui.MutedInk, TextStyles.Caption1);
    }

    private void DrawUrlEntry(Rect rect, float scale)
    {
        var fieldHeight = 38f * scale;
        var fieldRect = new Rect(rect.Min, new Vector2(rect.Max.X - 88f * scale, rect.Min.Y + fieldHeight));
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, fieldRect.Min, fieldRect.Max, 10f * scale, ImGui.GetColorU32(ui.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(fieldRect.Min.X + 12f * scale,
            fieldRect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldRect.Width - 24f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##aetherstreamUrl", Loc.T(L.AetherStream.UrlHint), ref urlInput, 2000,
                ImGuiInputTextFlags.None);
        }

        var browseCenter = new Vector2(fieldRect.Max.X + 22f * scale, fieldRect.Center.Y);
        if (ui.IconButton(browseCenter, 16f * scale, FontAwesomeIcon.FolderOpen.ToIconString(), ui.TitleInk,
                ui.FieldSurface, 0.6f, Loc.T(L.AetherStream.BrowseLocalFile)))
        {
            NativeFileDialog.PickVideo(Loc.T(L.AetherStream.BrowseLocalFile),
                path => Interlocked.Exchange(ref pendingLocalFile, path));
        }

        var pasteCenter = new Vector2(fieldRect.Max.X + 22f * scale + 44f * scale, fieldRect.Center.Y);
        if (ui.IconButton(pasteCenter, 16f * scale, FontAwesomeIcon.Paste.ToIconString(), ui.TitleInk,
                ui.FieldSurface, 0.6f))
        {
            var clipboard = ImGui.GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboard))
            {
                urlInput = clipboard.Trim();
            }
        }

        // While watching someone else's stream, this field proposes a suggestion to the host
        // instead of touching the local queue directly - queue.Add/PlayNow would populate
        // queue.Current, which OnFrameworkUpdate's "local action wins" rule already treats as the
        // user choosing to leave the stream. The Play Now/Add to Queue choice is meaningless here
        // (there's no local queue position to pick), so it's hidden entirely rather than shown
        // disabled.
        var suggesting = watchAlong.IsViewing;
        var submitTop = fieldRect.Max.Y + 10f * scale;
        if (!suggesting)
        {
            var toggleRow = new Rect(new Vector2(rect.Min.X, submitTop), new Vector2(rect.Max.X, submitTop + 30f * scale));
            var half = toggleRow.Width * 0.5f - 5f * scale;
            var playNowRect = new Rect(toggleRow.Min, new Vector2(toggleRow.Min.X + half, toggleRow.Max.Y));
            var queueRect = new Rect(new Vector2(toggleRow.Max.X - half, toggleRow.Min.Y), toggleRow.Max);
            DrawModeButton(playNowRect, Loc.T(L.AetherStream.PlayNow), !queueOnAdd, scale);
            DrawModeButton(queueRect, Loc.T(L.AetherStream.AddToQueue), queueOnAdd, scale);
            submitTop = toggleRow.Max.Y + 10f * scale;
        }

        var enabled = urlInput.Trim().Length > 0;
        var submitRect = new Rect(new Vector2(rect.Min.X, submitTop), new Vector2(rect.Max.X, submitTop + 38f * scale));
        var submitLabel = suggesting ? Loc.T(L.AetherStream.SuggestToHost)
            : queueOnAdd ? Loc.T(L.AetherStream.AddToQueue) : Loc.T(L.AetherStream.PlayNow);
        if (DrawSubmitButton(submitRect, submitLabel, enabled) && enabled)
        {
            if (suggesting)
            {
                watchAlong.SuggestQueueItem(urlInput.Trim());
                urlInput = string.Empty;
            }
            else
            {
                SubmitUrl();
            }
        }
    }

    private void DrawModeButton(Rect rect, string label, bool selected, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var fill = selected ? Palette.WithAlpha(ui.Accent, 0.18f) : ui.FieldSurface;
        Squircle.Fill(drawList, rect.Min, rect.Max, 8f * scale, ImGui.GetColorU32(fill));
        var ink = selected ? ui.Accent : ui.MutedInk;
        Typography.DrawCentered(rect.Center, label, ink, TextStyles.Footnote.Scale, FontWeight.SemiBold);
        if (UiInteract.HoverClick(rect.Min, rect.Max))
        {
            queueOnAdd = label == Loc.T(L.AetherStream.AddToQueue);
        }
    }

    private bool DrawSubmitButton(Rect rect, string label, bool enabled)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var fill = !enabled ? Palette.WithAlpha(ui.Accent, 0.35f) :
            hovered ? Palette.Mix(ui.Accent, new Vector4(0f, 0f, 0f, 1f), 0.12f) : ui.Accent;
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(fill));
        Typography.DrawCentered(drawList, rect.Center, label, new Vector4(1f, 1f, 1f, 1f), TextStyles.Headline.Scale,
            TextStyles.Headline.Weight);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void SubmitUrl()
    {
        var url = urlInput.Trim();
        urlInput = string.Empty;
        SubmitEntry(new VideoQueueEntry(url, url, string.Empty, null, null));
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
