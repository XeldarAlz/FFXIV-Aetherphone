using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetApp
{
    private void DrawProfile(Rect area, string userId)
    {
        if (store.ProfileUserId != userId)
        {
            store.OpenProfile(userId);
        }

        var user = store.ProfileUser;
        var title = user is null
            ? DisplayName
            : (string.IsNullOrEmpty(user.DisplayName) ? user.Handle : user.DisplayName);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, title, back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (store.ProfileFailed)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.ComingSoon), AppPalettes.Velvet.MutedInk);
            return;
        }

        if (user is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), AppPalettes.Velvet.MutedInk);
            return;
        }

        using (AppSurface.Begin(body))
        {
            DrawProfileHeader(user);
        }
    }

    private void DrawProfileHeader(VelvetProfileDto user)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var centerX = origin.X + width * 0.5f;
        var flagRadius = 16f * scale;
        var flagCenter = new Vector2(origin.X + width - flagRadius, origin.Y + flagRadius + 2f * scale);
        var reportShown = report.Toggle(ui, flagCenter, flagRadius, "velvet_profile", user.UserId,
            Loc.T(L.Velvet.ReportSubmit));
        var avatarRadius = 66f * scale;
        var avatarCenter = new Vector2(centerX, origin.Y + 18f * scale + avatarRadius);
        drawList.AddCircleFilled(avatarCenter, avatarRadius + 3f * scale, ImGui.GetColorU32(theme.AppBackground), 72);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, MonogramFor(user), 2.2f, AvatarFor(user), 72);
        var y = avatarCenter.Y + avatarRadius + 16f * scale;
        var displayName = string.IsNullOrEmpty(user.DisplayName) ? user.Handle : user.DisplayName;
        y += DrawCenteredLine(drawList, centerX, y, displayName, theme.TextStrong, 1.45f, FontWeight.SemiBold) +
             3f * scale;
        var meta = user.Handle.Length > 0 ? $"@{user.Handle}" : string.Empty;
        if (user.Pronouns.Length > 0)
        {
            meta = meta.Length > 0 ? $"{meta} · {user.Pronouns}" : user.Pronouns;
        }

        if (meta.Length > 0)
        {
            y += DrawCenteredLine(drawList, centerX, y, meta, AppPalettes.Velvet.MutedInk, 0.92f, FontWeight.Regular) +
                 2f * scale;
        }

        var lookingLine = VelvetLookingFor.Label(user.LookingFor);
        if (user.RelationshipStatus != VelvetRelationship.NotSaying)
        {
            lookingLine += $"  ·  {VelvetRelationship.Label(user.RelationshipStatus)}";
        }

        y += DrawCenteredLine(drawList, centerX, y, lookingLine, Palette.Mix(Accent, theme.TextStrong, 0.35f), 0.92f,
            FontWeight.Medium);
        if (user.UtcOffsetMinutes is { } profileOffset)
        {
            y += 5f * scale;
            var timeLine = $"{Loc.T(L.Velvet.LocalTimeLabel)}  {SocialTimeZone.Describe(profileOffset)}";
            y += DrawCenteredLine(drawList, centerX, y, timeLine, AppPalettes.Velvet.MutedInk, 0.84f, FontWeight.Regular);
        }

        y += 18f * scale;
        var connected = user.ConnectionState == VelvetConnectionState.Connected;
        var actionWidth = MathF.Min((connected ? 280f : 220f) * scale, width);
        var actionHeight = 42f * scale;
        var actionRect = new Rect(new Vector2(centerX - actionWidth * 0.5f, y),
            new Vector2(centerX + actionWidth * 0.5f, y + actionHeight));
        DrawProfileAction(actionRect, user);
        y += actionHeight;
        if (reportShown)
        {
            ImGui.SetCursorScreenPos(new Vector2(origin.X, y + 12f * scale));
            report.Composer(ui, origin.X, width);
            y = ImGui.GetCursorScreenPos().Y + 4f * scale;
        }

        var hasDetails = user.Intro.Length > 0 || user.Dynamic.Length > 0 || user.Tags.Length > 0 ||
                         user.Limits.Length > 0;
        if (hasDetails)
        {
            y += 20f * scale;
            drawList.AddLine(new Vector2(origin.X, y), new Vector2(origin.X + width, y),
                ImGui.GetColorU32(theme.Separator), 1f);
            y += 20f * scale;
        }

        var contentWidth = width - 24f * scale;
        if (user.Intro.Length > 0)
        {
            y += UiText.WrappedCentered(centerX, y, user.Intro, contentWidth, AppPalettes.Velvet.BodyInk, scale, 1.02f) +
                 14f * scale;
        }

        if (user.Dynamic.Length > 0 || user.Tags.Length > 0)
        {
            y += DrawCenteredChips(centerX, y, contentWidth, SplitTokens(user.Dynamic), user.Tags) + 6f * scale;
        }

        if (user.Limits.Length > 0)
        {
            y += 8f * scale;
            y += DrawCenteredLine(drawList, centerX, y, Loc.T(L.Velvet.LimitsLabel), AppPalettes.Velvet.MutedInk, 0.78f,
                FontWeight.SemiBold) + 4f * scale;
            y += UiText.WrappedCentered(centerX, y, string.Join(", ", user.Limits), contentWidth, AppPalettes.Velvet.BodyInk,
                scale, 0.9f);
        }

        y += 26f * scale;
        var blockWidth = MathF.Min(160f * scale, width);
        var blockRect = new Rect(new Vector2(centerX - blockWidth * 0.5f, y),
            new Vector2(centerX + blockWidth * 0.5f, y + 34f * scale));
        var isBlocked = user.ConnectionState == VelvetConnectionState.Blocked;
        if (ui.GhostButton(blockRect, isBlocked ? Loc.T(L.Velvet.Blocked) : Loc.T(L.Velvet.Block)) && !isBlocked)
        {
            store.Block(user.UserId, _ => { });
        }

        y += 34f * scale;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + 30f * scale));
    }

    private void DrawProfileAction(Rect rect, VelvetProfileDto user)
    {
        if (user.ConnectionState == VelvetConnectionState.Connected)
        {
            var gap = 8f * ImGuiHelpers.GlobalScale;
            var half = (rect.Width - gap) * 0.5f;
            var messageRect = new Rect(rect.Min, new Vector2(rect.Min.X + half, rect.Max.Y));
            var connectedRect = new Rect(new Vector2(rect.Max.X - half, rect.Min.Y), rect.Max);
            if (ui.PillButton(messageRect, Loc.T(L.Velvet.Message), true))
            {
                OpenThreadWith(user.UserId);
            }

            if (ui.PillButton(connectedRect, Loc.T(L.Velvet.Connected), false))
            {
                AskDisconnect(user.UserId);
            }
        }
        else if (user.ConnectionState == VelvetConnectionState.OutgoingRequest)
        {
            if (ui.PillButton(rect, Loc.T(L.Velvet.Requested), false))
            {
                store.CancelRequest(user.UserId);
            }
        }
        else if (user.ConnectionState == VelvetConnectionState.IncomingRequest)
        {
            if (ui.PillButton(rect, Loc.T(L.Velvet.Accept), true))
            {
                store.AcceptRequest(user.UserId);
            }
        }
        else if (ui.PillButton(rect, Loc.T(L.Velvet.Connect), true))
        {
            store.Connect(user.UserId);
        }
    }

    private void AskDisconnect(string userId)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Velvet.DisconnectConfirmMessage),
            ConfirmLabel = Loc.T(L.Velvet.Disconnect),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            Confirm = () => store.Disconnect(userId),
        });
    }

    private static float DrawCenteredLine(ImDrawListPtr drawList, float centerX, float top, string text, Vector4 color,
        float fontScale, FontWeight weight)
    {
        var size = Typography.Measure(text, fontScale, weight);
        Typography.DrawCentered(drawList, new Vector2(centerX, top + size.Y * 0.5f), text, color, fontScale, weight);
        return size.Y;
    }

    private float DrawCenteredChips(float centerX, float top, float maxWidth, string[] vibeTokens, string[] tags)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var total = vibeTokens.Length + tags.Length;
        if (total == 0)
        {
            return 0f;
        }

        var drawList = ImGui.GetWindowDrawList();
        var chipHeight = 27f * scale;
        var rowGap = 8f * scale;
        var chipGap = 8f * scale;
        var padX = 13f * scale;
        var y = top;
        var index = 0;
        while (index < total)
        {
            var rowWidth = 0f;
            var rowEnd = index;
            while (rowEnd < total)
            {
                var next = ChipWidth(rowEnd, vibeTokens, tags, padX);
                var candidate = rowEnd == index ? next : rowWidth + chipGap + next;
                if (rowEnd > index && candidate > maxWidth)
                {
                    break;
                }

                rowWidth = candidate;
                rowEnd++;
            }

            var cursorX = centerX - rowWidth * 0.5f;
            for (var chip = index; chip < rowEnd; chip++)
            {
                var label = ChipLabel(chip, vibeTokens, tags);
                var filled = chip < vibeTokens.Length;
                var chipWidth = ChipWidth(chip, vibeTokens, tags, padX);
                var chipMin = new Vector2(cursorX, y);
                var chipMax = new Vector2(cursorX + chipWidth, y + chipHeight);
                var fill = filled ? Palette.WithAlpha(Accent, 0.9f) : Palette.WithAlpha(Accent, 0.16f);
                Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(fill));
                var ink = filled ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(0.99f, 0.80f, 0.88f, 1f);
                Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, label, ink, 0.82f, FontWeight.Medium);
                cursorX += chipWidth + chipGap;
            }

            y += chipHeight + rowGap;
            index = rowEnd;
        }

        return y - top - rowGap;
    }

    private static string ChipLabel(int index, string[] vibeTokens, string[] tags) =>
        index < vibeTokens.Length ? vibeTokens[index] : tags[index - vibeTokens.Length];

    private static float ChipWidth(int index, string[] vibeTokens, string[] tags, float padX) =>
        Typography.Measure(ChipLabel(index, vibeTokens, tags), 0.82f, FontWeight.Medium).X + padX * 2f;

    private void DrawProfileRow(VelvetProfileDto profile)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = 72f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var radius = 24f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(drawList, avatarCenter, radius, Accent, MonogramFor(profile), 1.05f, AvatarFor(profile), 40);
        var textLeft = origin.X + radius * 2f + 12f * scale;
        var displayName = SocialIdentity.Name(profile.DisplayName, profile.Handle);
        Typography.Draw(new Vector2(textLeft, origin.Y + 12f * scale), displayName, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        var regionCode = gameData.RegionCodeForWorld(profile.World);
        var sub = regionCode.Length > 0
            ? $"{VelvetLookingFor.Label(profile.LookingFor)} · {regionCode}"
            : VelvetLookingFor.Label(profile.LookingFor);
        Typography.Draw(new Vector2(textLeft, origin.Y + 32f * scale), sub, AppPalettes.Velvet.MutedInk, 0.82f);
        DrawTagsLine(new Vector2(textLeft, origin.Y + 50f * scale), profile.Tags);
        var buttonWidth = 92f * scale;
        var buttonHeight = 30f * scale;
        var buttonRect =
            new Rect(new Vector2(origin.X + width - buttonWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f),
                new Vector2(origin.X + width, origin.Y + rowHeight * 0.5f + buttonHeight * 0.5f));
        DrawConnectButton(buttonRect, profile);
        if (UiInteract.HoverClick(origin, new Vector2(origin.X + width - buttonWidth - 6f * scale, origin.Y + rowHeight)))
        {
            OpenProfile(profile.UserId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawConnectButton(Rect rect, VelvetProfileDto profile)
    {
        switch (profile.ConnectionState)
        {
            case VelvetConnectionState.Connected:
                if (ui.PillButton(rect, Loc.T(L.Velvet.Message), true))
                {
                    OpenThreadWith(profile.UserId);
                }

                break;
            case VelvetConnectionState.OutgoingRequest:
                if (ui.PillButton(rect, Loc.T(L.Velvet.Requested), false))
                {
                    store.CancelRequest(profile.UserId);
                }

                break;
            case VelvetConnectionState.IncomingRequest:
                if (ui.PillButton(rect, Loc.T(L.Velvet.Accept), true))
                {
                    store.AcceptRequest(profile.UserId);
                }

                break;
            default:
                if (ui.PillButton(rect, Loc.T(L.Velvet.Connect), true))
                {
                    store.Connect(profile.UserId);
                }

                break;
        }
    }

    private void DrawThreadRow(VelvetThreadDto thread)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = 62f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var radius = 22f * scale;
        var avatarCenter = new Vector2(origin.X + radius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(drawList, avatarCenter, radius, Accent, Monogram(thread.OtherDisplayName, thread.OtherHandle),
            0.95f, lodestone.Remote(thread.OtherUserId, ToUri(thread.OtherAvatarUrl)), 32);
        DrawPresenceDot(new Vector2(avatarCenter.X + radius - 4f * scale, avatarCenter.Y + radius - 4f * scale),
            thread.Presence);
        var textLeft = origin.X + radius * 2f + 12f * scale;
        var displayName = string.IsNullOrEmpty(thread.OtherDisplayName) ? thread.OtherHandle : thread.OtherDisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y + 12f * scale), displayName, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        var previewColor = thread.UnreadCount > 0 ? theme.TextStrong : AppPalettes.Velvet.MutedInk;
        Typography.Draw(new Vector2(textLeft, origin.Y + 32f * scale), UiText.Truncate(thread.LastMessagePreview, 42),
            previewColor, 0.85f);
        if (thread.UnreadCount > 0)
        {
            var badgeCenter = new Vector2(origin.X + width - 16f * scale, origin.Y + rowHeight * 0.5f);
            drawList.AddCircleFilled(badgeCenter, 9f * scale, ImGui.GetColorU32(Accent), 20);
            Typography.DrawCentered(badgeCenter, thread.UnreadCount.ToString(Loc.Culture), new Vector4(1f, 1f, 1f, 1f),
                0.75f, FontWeight.SemiBold);
        }

        if (UiInteract.HoverClick(origin, new Vector2(origin.X + width, origin.Y + rowHeight)))
        {
            OpenThreadWith(thread.OtherUserId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawThread(Rect area, string threadId)
    {
        if (store.ThreadId != threadId)
        {
            store.OpenThread(threadId);
            sinceThreadPoll = ThreadPollSeconds;
            lastTypingDraft = string.Empty;
        }

        var delta = ImGui.GetIO().DeltaTime;
        store.NoteThreadViewed(threadId);
        TickThread(threadId);
        DrawThreadHeader(area, threadId);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerHeight = 52f * scale;
        var listRect = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));
        var snapshot = store.Messages;
        SyncThreadEntrances(threadId, snapshot.Length, delta);
        var typingTarget = store.OtherTyping ? 1f : 0f;
        typingReveal += (typingTarget - typingReveal) * MathF.Min(1f, delta * 12f);
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0 && typingReveal < 0.01f)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                    store.LoadingThread ? Loc.T(L.Common.Loading) : Loc.T(L.Velvet.ThreadEmpty), AppPalettes.Velvet.MutedInk);
            }
            else
            {
                SyncThreadFollow(threadId);
                ImGui.Dummy(new Vector2(0f, 8f * scale));
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawMessageBubble(snapshot[index], index);
                }

                if (typingReveal > 0.01f)
                {
                    DrawTypingBubble(typingReveal);
                }

                ImGui.Dummy(new Vector2(0f, 8f * scale));
                if (followThreadBottom)
                {
                    ImGui.SetScrollHereY(1f);
                }
            }
        }

        DrawMessageComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), threadId);
    }

    private void DrawThreadHeader(Rect area, string threadId)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, string.Empty, back);
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var name = ThreadTitle(threadId);
        var avatar = ThreadAvatar(threadId, out var monogram, out var presence);
        var avatarRadius = 18f * scale;
        var nameSize = Typography.Measure(name, 1f, FontWeight.SemiBold);
        var gap = 9f * scale;
        var groupWidth = avatarRadius * 2f + gap + nameSize.X;
        var startX = MathF.Max(area.Center.X - groupWidth * 0.5f, area.Min.X + 48f * scale);
        var avatarCenter = new Vector2(startX + avatarRadius, rowCenterY);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, monogram, 0.95f, avatar, 32);
        DrawPresenceDot(
            new Vector2(avatarCenter.X + avatarRadius - 3f * scale, avatarCenter.Y + avatarRadius - 3f * scale),
            presence);
        var nameLeft = avatarCenter.X + avatarRadius + gap;
        var offset = ThreadOffset(threadId);
        var textWidth = nameSize.X;
        if (offset is { } minutes)
        {
            var timeText = SocialTimeZone.Describe(minutes);
            var subSize = Typography.Measure(timeText, 0.72f, FontWeight.Regular);
            var gapY = 1f * scale;
            var stackTop = rowCenterY - (nameSize.Y + gapY + subSize.Y) * 0.5f;
            Typography.Draw(new Vector2(nameLeft, stackTop), name, theme.TextStrong, 1f, FontWeight.SemiBold);
            Typography.Draw(new Vector2(nameLeft, stackTop + nameSize.Y + gapY), timeText, AppPalettes.Velvet.MutedInk, 0.72f);
            textWidth = MathF.Max(nameSize.X, subSize.X);
        }
        else
        {
            Typography.Draw(new Vector2(nameLeft, rowCenterY - nameSize.Y * 0.5f), name, theme.TextStrong, 1f,
                FontWeight.SemiBold);
        }

        var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
        var hitMax = new Vector2(nameLeft + textWidth, area.Min.Y + AppHeader.Height * scale);
        if (UiInteract.HoverClick(hitMin, hitMax))
        {
            OpenProfile(threadId);
        }
    }

    private int? ThreadOffset(string threadId)
    {
        var threads = store.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].OtherUserId == threadId)
            {
                return threads[index].UtcOffsetMinutes;
            }
        }

        var connections = store.Connections;
        for (var index = 0; index < connections.Length; index++)
        {
            if (connections[index].UserId == threadId)
            {
                return connections[index].UtcOffsetMinutes;
            }
        }

        return null;
    }

    private AvatarHandle ThreadAvatar(string threadId, out string monogram, out int presence)
    {
        var threads = store.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].OtherUserId == threadId)
            {
                var thread = threads[index];
                monogram = Monogram(thread.OtherDisplayName, thread.OtherHandle);
                presence = thread.Presence;
                return lodestone.Remote(thread.OtherUserId, ToUri(thread.OtherAvatarUrl));
            }
        }

        var connections = store.Connections;
        for (var index = 0; index < connections.Length; index++)
        {
            if (connections[index].UserId == threadId)
            {
                var connection = connections[index];
                monogram = Monogram(connection.DisplayName, connection.Handle);
                presence = connection.Presence;
                return lodestone.Remote(connection.UserId, ToUri(connection.AvatarUrl));
            }
        }

        monogram = "?";
        presence = VelvetPresence.Offline;
        return AvatarHandle.Disabled;
    }

    private void SyncThreadEntrances(string threadId, int count, float delta)
    {
        if (entranceThreadId != threadId)
        {
            entranceThreadId = threadId;
            entranceSettled = count;
            entrancePrimed = count > 0 || !store.LoadingThread;
            threadEntrances.Clear();
            return;
        }

        if (!entrancePrimed)
        {
            entranceSettled = count;
            entrancePrimed = count > 0 || !store.LoadingThread;
            return;
        }

        if (count < entranceSettled)
        {
            entranceSettled = count;
        }

        while (entranceSettled < count)
        {
            threadEntrances.Add(new BubbleEntrance { Line = entranceSettled, Elapsed = 0f });
            entranceSettled++;
        }

        for (var index = threadEntrances.Count - 1; index >= 0; index--)
        {
            var entrance = threadEntrances[index];
            entrance.Elapsed += delta;
            if (entrance.Elapsed >= TransitionTiming.BubbleSeconds || entrance.Line >= count)
            {
                threadEntrances.RemoveAt(index);
            }
            else
            {
                threadEntrances[index] = entrance;
            }
        }
    }

    private float ThreadEntranceProgress(int line)
    {
        for (var index = 0; index < threadEntrances.Count; index++)
        {
            if (threadEntrances[index].Line == line)
            {
                return threadEntrances[index].Elapsed / TransitionTiming.BubbleSeconds;
            }
        }

        return 1f;
    }

    private void SyncThreadFollow(string threadId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (followThreadId == threadId)
        {
            followThreadBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 4f * scale;
        }
        else
        {
            followThreadId = threadId;
            followThreadBottom = true;
        }

        if (snapThreadToBottom)
        {
            followThreadBottom = true;
            snapThreadToBottom = false;
        }
    }

    private void TickThread(string threadId)
    {
        var delta = ImGui.GetIO().DeltaTime;
        sinceThreadPoll += delta;
        if (sinceThreadPoll >= ThreadPollSeconds)
        {
            sinceThreadPoll = 0f;
            store.RefreshThread();
            store.RefreshTyping(threadId);
        }

        sinceTypingSend += delta;
        if (messageDraft != lastTypingDraft)
        {
            lastTypingDraft = messageDraft;
            if (messageDraft.Trim().Length > 0 && sinceTypingSend >= TypingSendSeconds)
            {
                sinceTypingSend = 0f;
                store.SendTyping(threadId);
            }
        }
    }

    private void DrawMessageBubble(VelvetMessageDto message, int index)
    {
        if (message.Kind == 1)
        {
            DrawImageBubble(message, index);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var mine = store.Me is { } me && me.UserId == message.SenderId;
        var drawList = ImGui.GetWindowDrawList();
        var available = ImGui.GetContentRegionAvail().X;
        var paddingX = 12f * scale;
        var paddingY = 8f * scale;
        var wrap = available * 0.74f - paddingX * 2f;
        var textSize = ImGui.CalcTextSize(message.Body, false, wrap);
        var bubbleWidth = textSize.X + paddingX * 2f;
        var bubbleHeight = textSize.Y + paddingY * 2f;
        var start = ImGui.GetCursorPos();
        var offsetX = mine ? available - bubbleWidth : 0f;
        var fill = mine ? Accent : new Vector4(1f, 1f, 1f, 0.10f);
        var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong;
        var entrance = ThreadEntranceProgress(index);
        if (entrance < 1f)
        {
            DrawBubbleEntering(message.Body, scale, start, offsetX, bubbleWidth, bubbleHeight, paddingX, paddingY, wrap,
                mine, fill, ink, entrance);
        }
        else
        {
            ImGui.SetCursorPos(new Vector2(start.X + offsetX, start.Y));
            var bubbleScreen = ImGui.GetCursorScreenPos();
            Squircle.Fill(drawList, bubbleScreen, bubbleScreen + new Vector2(bubbleWidth, bubbleHeight), 14f * scale,
                ImGui.GetColorU32(fill));
            ImGui.SetCursorPos(new Vector2(start.X + offsetX + paddingX, start.Y + paddingY));
            ImGui.PushTextWrapPos(start.X + offsetX + paddingX + wrap);
            using (ImRaii.PushColor(ImGuiCol.Text, ink))
            {
                ImGui.TextUnformatted(message.Body);
            }

            ImGui.PopTextWrapPos();
        }

        ImGui.SetCursorPos(new Vector2(start.X, start.Y + bubbleHeight + 6f * scale));
    }

    private static void DrawBubbleEntering(string text, float scale, Vector2 start, float offsetX, float bubbleWidth,
        float bubbleHeight, float paddingX, float paddingY, float wrap, bool mine, Vector4 fill, Vector4 ink,
        float entrance)
    {
        var pop = 0.80f + 0.20f * Easing.EaseOutBack(entrance);
        var alpha = MathF.Min(entrance * 1.8f, 1f);
        var rise = new Vector2(0f, (1f - Easing.EaseOutCubic(entrance)) * 10f * scale);
        ImGui.SetCursorPos(start);
        var screenStart = ImGui.GetCursorScreenPos();
        var fillMin = screenStart + new Vector2(offsetX, 0f);
        var fillMax = fillMin + new Vector2(bubbleWidth, bubbleHeight);
        var anchor = new Vector2(mine ? fillMax.X : fillMin.X, fillMax.Y);
        var scaledMin = anchor + (fillMin - anchor) * pop + rise;
        var scaledMax = anchor + (fillMax - anchor) * pop + rise;
        Squircle.Fill(ImGui.GetWindowDrawList(), scaledMin, scaledMax, 14f * scale * pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)));
        var textLocal = new Vector2(start.X + offsetX + paddingX, start.Y + paddingY);
        var anchorLocal = new Vector2(mine ? start.X + offsetX + bubbleWidth : start.X + offsetX,
            start.Y + bubbleHeight);
        var scaledTextLocal = anchorLocal + (textLocal - anchorLocal) * pop + rise;
        ImGui.SetWindowFontScale(pop);
        ImGui.SetCursorPos(scaledTextLocal);
        ImGui.PushTextWrapPos(scaledTextLocal.X + wrap * pop);
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.WithAlpha(ink, ink.W * alpha)))
        {
            ImGui.TextUnformatted(text);
        }

        ImGui.PopTextWrapPos();
        ImGui.SetWindowFontScale(1f);
    }

    private void DrawImageBubble(VelvetMessageDto message, int index)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var mine = store.Me is { } me && me.UserId == message.SenderId;
        var drawList = ImGui.GetWindowDrawList();
        var available = ImGui.GetContentRegionAvail().X;
        var padding = 5f * scale;
        var aspect = message.MediaWidth > 0 && message.MediaHeight > 0
            ? (float)message.MediaHeight / message.MediaWidth
            : 1f;
        var imageWidth = available * 0.62f;
        var imageHeight = imageWidth * aspect;
        var maxHeight = 280f * scale;
        if (imageHeight > maxHeight)
        {
            imageHeight = maxHeight;
            imageWidth = imageHeight / aspect;
        }

        var caption = message.Body ?? string.Empty;
        var captionHeight = caption.Length > 0 ? Typography.Measure(caption, 0.9f).Y + 6f * scale : 0f;
        var bubbleWidth = imageWidth + padding * 2f;
        var bubbleHeight = imageHeight + padding * 2f + captionHeight;
        var start = ImGui.GetCursorPos();
        var offsetX = mine ? available - bubbleWidth : 0f;
        var fill = mine ? Accent : new Vector4(1f, 1f, 1f, 0.10f);
        var entrance = ThreadEntranceProgress(index);
        var pop = entrance < 1f ? 0.80f + 0.20f * Easing.EaseOutBack(entrance) : 1f;
        var alpha = entrance < 1f ? MathF.Min(entrance * 1.8f, 1f) : 1f;
        var rise = new Vector2(0f, entrance < 1f ? (1f - Easing.EaseOutCubic(entrance)) * 10f * scale : 0f);
        ImGui.SetCursorPos(start);
        var screen = ImGui.GetCursorScreenPos();
        var bubbleMin = screen + new Vector2(offsetX, 0f);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        var anchor = new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y);
        var scaledMin = anchor + (bubbleMin - anchor) * pop + rise;
        var scaledMax = anchor + (bubbleMax - anchor) * pop + rise;
        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)));
        var imageMin = scaledMin + new Vector2(padding * pop, padding * pop);
        var imageMax = imageMin + new Vector2(imageWidth * pop, imageHeight * pop);
        var rounding = 10f * scale * pop;
        var texture = images.Get(store.DmMediaUrl(message.Id));
        if (texture is null)
        {
            Squircle.Fill(drawList, imageMin, imageMax, rounding,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f * alpha)));
            AppSkin.Icon((imageMin + imageMax) * 0.5f, FontAwesomeIcon.Image.ToIconString(),
                Palette.WithAlpha(AppPalettes.Velvet.MutedInk, alpha), 1.2f);
        }
        else
        {
            drawList.AddImageRounded(texture.Handle, imageMin, imageMax, Vector2.Zero, Vector2.One,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), rounding, ImDrawFlags.RoundCornersAll);
            if (entrance >= 1f && ImGui.IsMouseHoveringRect(imageMin, imageMax))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    router.Push(VelvetRoute.ImageView(message.Id));
                }
            }
        }

        if (caption.Length > 0)
        {
            var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong;
            Typography.Draw(drawList, new Vector2(imageMin.X, imageMax.Y + 4f * scale * pop),
                UiText.Truncate(caption, 60), Palette.WithAlpha(ink, alpha), 0.9f);
        }

        ImGui.SetCursorPos(new Vector2(start.X, start.Y + bubbleHeight + 6f * scale));
    }

    private void DrawTypingBubble(float reveal)
    {
        var scale = ImGuiHelpers.GlobalScale;
        typingPhase += ImGui.GetIO().DeltaTime;
        if (typingPhase > 1000f)
        {
            typingPhase -= 1000f;
        }

        var eased = Easing.EaseOutCubic(Math.Clamp(reveal, 0f, 1f));
        var drawList = ImGui.GetWindowDrawList();
        var paddingX = 14f * scale;
        var dotRadius = 3.2f * scale;
        var dotGap = 7f * scale;
        var bubbleWidth = paddingX * 2f + dotRadius * 6f + dotGap * 2f;
        var bubbleHeight = 28f * scale;
        var start = ImGui.GetCursorPos();
        var origin = ImGui.GetCursorScreenPos() + new Vector2(0f, (1f - eased) * 6f * scale);
        var bubbleMax = new Vector2(origin.X + bubbleWidth, origin.Y + bubbleHeight);
        Squircle.Fill(drawList, origin, bubbleMax, bubbleHeight * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f * eased)));
        var baseY = (origin.Y + bubbleMax.Y) * 0.5f;
        var firstDotX = origin.X + paddingX + dotRadius;
        for (var dot = 0; dot < 3; dot++)
        {
            var wave = MathF.Max(0f, MathF.Sin(typingPhase * 6f - dot * 0.9f));
            var offsetY = -wave * 4f * scale;
            var dotAlpha = (0.35f + 0.5f * wave) * eased;
            var center = new Vector2(firstDotX + dot * (dotRadius * 2f + dotGap), baseY + offsetY);
            drawList.AddCircleFilled(center, dotRadius,
                ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.Velvet.BodyInk, dotAlpha)), 16);
        }

        ImGui.SetCursorPos(start);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, (bubbleHeight + 8f * scale) * eased));
    }

    private struct BubbleEntrance
    {
        public int Line;
        public float Elapsed;
    }

    private void DrawMessageComposer(Rect area, string threadId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);
        var buttonRadius = 16f * scale;
        var pictureCenter = new Vector2(area.Min.X + 12f * scale + buttonRadius, area.Center.Y);
        var pictureMin = pictureCenter - new Vector2(buttonRadius, buttonRadius);
        var pictureMax = pictureCenter + new Vector2(buttonRadius, buttonRadius);
        var pictureHovered = ImGui.IsMouseHoveringRect(pictureMin, pictureMax);
        drawList.AddCircleFilled(pictureCenter, buttonRadius,
            ImGui.GetColorU32(pictureHovered ? Palette.Mix(Accent, theme.TextStrong, 0.12f) : Accent), 24);
        AppSkin.Icon(pictureCenter, FontAwesomeIcon.Image.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 0.85f);
        if (pictureHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            using (ImRaii.Tooltip())
            {
                ImGui.TextUnformatted(Loc.T(L.Velvet.SendPicture));
            }

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                router.Push(VelvetRoute.ChatImage(threadId));
            }
        }

        var sendWidth = 40f * scale;
        var pillMin = new Vector2(pictureMax.X + 10f * scale, area.Min.Y + 8f * scale);
        var pillMax = new Vector2(area.Max.X - sendWidth - 12f * scale, area.Max.Y - 8f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 24f * scale);
        if (threadFocus)
        {
            ImGui.SetKeyboardFocusHere();
            threadFocus = false;
        }

        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##velvetMessage", Loc.T(L.Velvet.MessageHint), ref messageDraft, MessageMax,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var canSend = messageDraft.Trim().Length > 0 && !store.Sending;
        var sendCenter = new Vector2(area.Max.X - sendWidth * 0.5f - 8f * scale, area.Center.Y);
        drawList.AddCircleFilled(sendCenter, 16f * scale, ImGui.GetColorU32(canSend ? Accent : theme.SurfaceMuted), 24);
        AppSkin.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 0.85f);
        var sendHitRadius = 16f * scale;
        if (ImGui.IsMouseHoveringRect(sendCenter - new Vector2(sendHitRadius, sendHitRadius),
                sendCenter + new Vector2(sendHitRadius, sendHitRadius)))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ui.DrawActionTooltip(sendCenter, sendHitRadius, Loc.T(L.Velvet.Send));
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && canSend)
            {
                submitted = true;
            }
        }

        if (submitted && canSend)
        {
            store.SendMessage(threadId, messageDraft, _ => { });
            messageDraft = string.Empty;
            lastTypingDraft = string.Empty;
            snapThreadToBottom = true;
            threadFocus = true;
        }
    }

    private readonly PhotoZoomView imageZoom = new();

    private void DrawImageViewer(Rect area, string messageId)
    {
        if (imageViewId != messageId)
        {
            imageViewId = messageId;
            imageSaveOutcome = 0;
            imageZoom.Reset();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.94f)));
        var headerHeight = AppHeader.Height * scale;
        var footerHeight = 60f * scale;
        var fitMin = new Vector2(area.Min.X + 8f * scale, area.Min.Y + headerHeight);
        var fitMax = new Vector2(area.Max.X - 8f * scale, area.Max.Y - footerHeight);
        var url = store.DmMediaUrl(messageId);
        var texture = images.Get(url);
        if (texture is null)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, (fitMin.Y + fitMax.Y) * 0.5f), Loc.T(L.Common.Loading),
                AppPalettes.Velvet.MutedInk);
        }
        else
        {
            imageZoom.Draw(new Rect(fitMin, fitMax), texture, theme, 10f * scale);
        }

        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, string.Empty, back);
        var saved = imageSaveOutcome == 1;
        var label = saved ? Loc.T(L.Velvet.SavedToGallery) : Loc.T(L.Velvet.SaveToGallery);
        var buttonWidth = MathF.Min(240f * scale, area.Width - 32f * scale);
        var buttonHeight = 42f * scale;
        var buttonTop = area.Max.Y - footerHeight + (footerHeight - buttonHeight) * 0.5f;
        var buttonRect = new Rect(new Vector2(area.Center.X - buttonWidth * 0.5f, buttonTop),
            new Vector2(area.Center.X + buttonWidth * 0.5f, buttonTop + buttonHeight));
        if (ui.PillButton(buttonRect, label, !saved) && !saved && !imageSaveBusy && texture is not null)
        {
            SaveDmImage(url);
        }
    }

    private void SaveDmImage(string? url)
    {
        if (string.IsNullOrEmpty(url) || imageSaveBusy)
        {
            return;
        }

        imageSaveBusy = true;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var bytes = await http.GetBytesAsync(new Uri(url), CancellationToken.None).ConfigureAwait(false);
                if (bytes is not null)
                {
                    using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(bytes);
                    var pixels = new byte[image.Width * image.Height * 4];
                    image.CopyPixelDataTo(pixels);
                    library.Save(pixels, image.Width, image.Height);
                    succeeded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] save image failed: {exception.Message}");
            }
            finally
            {
                imageSaveOutcome = succeeded ? 1 : 2;
                imageSaveBusy = false;
            }
        });
    }

    private void DrawChatImagePicker(Rect area, string threadId)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Velvet.ChangePhoto), back);
        if (chatPickerThreadId != threadId)
        {
            chatPickerThreadId = threadId;
            chatPickerPaths = library.List();
            chatPendingPickedPath = null;
        }

        var picked = Interlocked.Exchange(ref chatPendingPickedPath, null);
        if (!string.IsNullOrEmpty(picked))
        {
            SendChatImage(threadId, picked);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, Loc.T(L.Velvet.ImportFromPc), true))
        {
            LaunchChatImageDialog();
        }

        var gridRect = new Rect(new Vector2(area.Min.X, importRect.Max.Y + 12f * scale), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (chatPickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    Loc.T(L.Velvet.NoPhotos), AppPalettes.Velvet.MutedInk);
                return;
            }

            const int columns = 3;
            var gap = 6f * scale;
            var cell = (ImGui.GetContentRegionAvail().X - gap * (columns - 1)) / columns;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
            {
                for (var index = 0; index < chatPickerPaths.Length; index++)
                {
                    using (ImRaii.PushId(index))
                    {
                        var clicked = ImGui.InvisibleButton("chatpick", new Vector2(cell, cell));
                        DrawPickerThumbnail(chatPickerPaths[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(),
                            scale);
                        if (clicked)
                        {
                            SendChatImage(threadId, chatPickerPaths[index]);
                        }
                    }

                    if (index % columns != columns - 1)
                    {
                        ImGui.SameLine();
                    }
                }
            }
        }
    }

    private void SendChatImage(string threadId, string path)
    {
        store.SendImageMessage(threadId, path, string.Empty, _ => { });
        snapThreadToBottom = true;
        chatPickerThreadId = null;
        router.Pop();
    }

    private void LaunchChatImageDialog()
    {
        _ = NativeFileDialog.OpenImageAsync(Loc.T(L.Velvet.ChangePhoto)).ContinueWith(task =>
        {
            if (task.Status == TaskStatus.RanToCompletion && !string.IsNullOrEmpty(task.Result))
            {
                Interlocked.Exchange(ref chatPendingPickedPath, task.Result);
            }
        });
    }

    private static void DrawPickerThumbnail(string path, Vector2 min, Vector2 max, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = Plugin.WallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
            return;
        }

        var size = texture.Size;
        var uv0 = Vector2.Zero;
        var uv1 = Vector2.One;
        if (size.X > 0f && size.Y > 0f)
        {
            var aspect = size.X / size.Y;
            if (aspect > 1f)
            {
                var inset = (1f - 1f / aspect) * 0.5f;
                uv0 = new Vector2(inset, 0f);
                uv1 = new Vector2(1f - inset, 1f);
            }
            else if (aspect < 1f)
            {
                var inset = (1f - aspect) * 0.5f;
                uv0 = new Vector2(0f, inset);
                uv1 = new Vector2(1f, 1f - inset);
            }
        }

        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }
}
