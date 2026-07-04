using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
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
        var title = user is null ? DisplayName : (string.IsNullOrEmpty(user.DisplayName) ? user.Handle : user.DisplayName);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, title, back);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        if (store.ProfileFailed)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.ComingSoon), VelvetUi.MutedInk);
            return;
        }

        if (user is null)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Common.Loading), VelvetUi.MutedInk);
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

        var bannerHeight = 82f * scale;
        var bannerMin = new Vector2(origin.X - 16f * scale, origin.Y - 8f * scale);
        var bannerMax = new Vector2(bannerMin.X + width + 32f * scale, bannerMin.Y + bannerHeight);
        Squircle.FillVerticalGradient(drawList, bannerMin, bannerMax, 0f,
            ImGui.GetColorU32(Palette.Mix(Accent, theme.TextStrong, 0.10f)),
            ImGui.GetColorU32(Palette.Mix(Accent, new Vector4(0f, 0f, 0f, 1f), 0.34f)));

        var avatarRadius = 34f * scale;
        var avatarCenter = new Vector2(origin.X + avatarRadius, bannerMax.Y);
        drawList.AddCircleFilled(avatarCenter, avatarRadius + 3f * scale, ImGui.GetColorU32(theme.AppBackground), 40);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, MonogramFor(user), 1.25f, AvatarFor(user), 48);

        var buttonWidth = 108f * scale;
        var buttonHeight = 32f * scale;
        var buttonMin = new Vector2(origin.X + width - buttonWidth, bannerMax.Y + 8f * scale);
        var buttonRect = new Rect(buttonMin, new Vector2(buttonMin.X + buttonWidth, buttonMin.Y + buttonHeight));
        var reportCenter = new Vector2(buttonMin.X - buttonHeight * 0.5f - 8f * scale, buttonMin.Y + buttonHeight * 0.5f);
        var reportShown = report.Toggle(ui, reportCenter, buttonHeight * 0.5f, "velvet_profile", user.UserId);

        if (user.ConnectionState == VelvetConnectionState.Connected)
        {
            if (ui.PillButton(buttonRect, Loc.T(L.Velvet.Message), true))
            {
                OpenThreadWith(user.UserId);
            }
        }
        else if (user.ConnectionState == VelvetConnectionState.OutgoingRequest)
        {
            ui.PillButton(buttonRect, Loc.T(L.Velvet.Requested), false);
        }
        else if (user.ConnectionState == VelvetConnectionState.IncomingRequest)
        {
            if (ui.PillButton(buttonRect, Loc.T(L.Velvet.Accept), true))
            {
                store.Connect(user.UserId);
            }
        }
        else
        {
            if (ui.PillButton(buttonRect, Loc.T(L.Velvet.Connect), true))
            {
                store.Connect(user.UserId);
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, avatarCenter.Y + avatarRadius + 8f * scale));
        if (reportShown)
        {
            report.Composer(ui, origin.X, width);
            ImGui.Dummy(new Vector2(0f, 8f * scale));
        }

        var displayName = string.IsNullOrEmpty(user.DisplayName) ? user.Handle : user.DisplayName;
        using (Plugin.Fonts.Push(1.35f, FontWeight.SemiBold))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.TextUnformatted(displayName);
        }

        using (ImRaii.PushColor(ImGuiCol.Text, VelvetUi.MutedInk))
        {
            var meta = user.Handle.Length > 0 ? $"@{user.Handle}" : string.Empty;
            if (user.Pronouns.Length > 0)
            {
                meta = meta.Length > 0 ? $"{meta} · {user.Pronouns}" : user.Pronouns;
            }

            if (meta.Length > 0)
            {
                ImGui.TextUnformatted(meta);
            }

            var line = VelvetLookingFor.Label(user.LookingFor);
            if (user.RelationshipStatus != VelvetRelationship.NotSaying)
            {
                line += $" · {VelvetRelationship.Label(user.RelationshipStatus)}";
            }

            ImGui.TextUnformatted(line);
        }

        if (user.Intro.Length > 0)
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            ImGui.PushTextWrapPos(0f);
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            {
                ImGui.TextWrapped(user.Intro);
            }

            ImGui.PopTextWrapPos();
        }

        if (user.Dynamic.Length > 0)
        {
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            ui.LabelValue(Loc.T(L.Velvet.DynamicLabel), user.Dynamic);
        }

        if (user.Tags.Length > 0)
        {
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            using (ImRaii.PushColor(ImGuiCol.Text, VelvetUi.MutedInk))
            {
                ImGui.TextUnformatted(Loc.T(L.Velvet.TagsLabel));
            }

            DrawTagChips(user.Tags);
        }

        if (user.Limits.Length > 0)
        {
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            ui.LabelValue(Loc.T(L.Velvet.LimitsLabel), string.Join(", ", user.Limits));
        }

        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var blockWidth = 120f * scale;
        var blockMin = ImGui.GetCursorScreenPos();
        var blockRect = new Rect(blockMin, new Vector2(blockMin.X + blockWidth, blockMin.Y + 30f * scale));
        var isBlocked = user.ConnectionState == VelvetConnectionState.Blocked;
        if (ui.GhostButton(blockRect, isBlocked ? Loc.T(L.Velvet.Blocked) : Loc.T(L.Velvet.Block)) && !isBlocked)
        {
            store.Block(user.UserId, _ => { });
        }

        ImGui.Dummy(new Vector2(width, 40f * scale));
    }

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
        var displayName = string.IsNullOrEmpty(profile.DisplayName) ? profile.Handle : profile.DisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y + 12f * scale), displayName, theme.TextStrong, 1f, FontWeight.SemiBold);

        var sub = $"{VelvetLookingFor.Label(profile.LookingFor)} · {profile.DataCenter}";
        Typography.Draw(new Vector2(textLeft, origin.Y + 32f * scale), sub, VelvetUi.MutedInk, 0.82f);
        DrawTagsLine(new Vector2(textLeft, origin.Y + 50f * scale), profile.Tags);

        var buttonWidth = 92f * scale;
        var buttonHeight = 30f * scale;
        var buttonRect = new Rect(new Vector2(origin.X + width - buttonWidth, origin.Y + rowHeight * 0.5f - buttonHeight * 0.5f), new Vector2(origin.X + width, origin.Y + rowHeight * 0.5f + buttonHeight * 0.5f));
        DrawConnectButton(buttonRect, profile);

        if (VelvetUi.HoverClick(origin, new Vector2(origin.X + width - buttonWidth - 6f * scale, origin.Y + rowHeight)))
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
                ui.PillButton(rect, Loc.T(L.Velvet.Requested), false);
                break;
            case VelvetConnectionState.IncomingRequest:
                if (ui.PillButton(rect, Loc.T(L.Velvet.Accept), true))
                {
                    store.Connect(profile.UserId);
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
        AvatarView.Draw(drawList, avatarCenter, radius, Accent, Monogram(thread.OtherDisplayName, thread.OtherHandle), 0.95f, lodestone.Remote(thread.OtherUserId, ToUri(thread.OtherAvatarUrl)), 32);
        DrawPresenceDot(new Vector2(avatarCenter.X + radius - 4f * scale, avatarCenter.Y + radius - 4f * scale), thread.Presence);

        var textLeft = origin.X + radius * 2f + 12f * scale;
        var displayName = string.IsNullOrEmpty(thread.OtherDisplayName) ? thread.OtherHandle : thread.OtherDisplayName;
        Typography.Draw(new Vector2(textLeft, origin.Y + 12f * scale), displayName, theme.TextStrong, 1f, FontWeight.SemiBold);
        var previewColor = thread.UnreadCount > 0 ? theme.TextStrong : VelvetUi.MutedInk;
        Typography.Draw(new Vector2(textLeft, origin.Y + 32f * scale), VelvetUi.Truncate(thread.LastMessagePreview, 42), previewColor, 0.85f);

        if (thread.UnreadCount > 0)
        {
            var badgeCenter = new Vector2(origin.X + width - 16f * scale, origin.Y + rowHeight * 0.5f);
            drawList.AddCircleFilled(badgeCenter, 9f * scale, ImGui.GetColorU32(Accent), 20);
            Typography.DrawCentered(badgeCenter, thread.UnreadCount.ToString(Loc.Culture), new Vector4(1f, 1f, 1f, 1f), 0.75f, FontWeight.SemiBold);
        }

        if (VelvetUi.HoverClick(origin, new Vector2(origin.X + width, origin.Y + rowHeight)))
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

        if (sendOutcome != 0)
        {
            sendOutcome = 0;
            messageDraft = string.Empty;
        }

        TickThread(threadId);
        var title = ThreadTitle(threadId);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, title, back);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerHeight = 52f * scale;
        var listRect = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));
        var snapshot = store.Messages;
        using (AppSurface.Begin(listRect))
        {
            if (snapshot.Length == 0)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale), store.LoadingThread ? Loc.T(L.Common.Loading) : Loc.T(L.Velvet.ThreadEmpty), VelvetUi.MutedInk);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 8f * scale));
                for (var index = 0; index < snapshot.Length; index++)
                {
                    DrawMessageBubble(snapshot[index]);
                }

                ImGui.Dummy(new Vector2(0f, 8f * scale));
            }

            if (store.OtherTyping)
            {
                Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(2f * scale, 0f), "…", VelvetUi.MutedInk, 1.2f, FontWeight.SemiBold);
                ImGui.Dummy(new Vector2(0f, 20f * scale));
            }
        }

        DrawMessageComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), threadId);
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

    private void DrawMessageBubble(VelvetMessageDto message)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var mine = store.Me is { } me && me.UserId == message.SenderId;
        var drawList = ImGui.GetWindowDrawList();
        var maxWidth = ImGui.GetContentRegionAvail().X * 0.74f;
        var textSize = Typography.Measure(message.Body, 0.95f);
        var wrapWidth = MathF.Min(textSize.X, maxWidth);
        var paddingX = 12f * scale;
        var paddingY = 8f * scale;

        var lines = Math.Max(1, (int)MathF.Ceiling(textSize.X / MathF.Max(wrapWidth, 1f)));
        var bubbleWidth = wrapWidth + paddingX * 2f;
        var bubbleHeight = textSize.Y * lines + paddingY * 2f;

        var origin = ImGui.GetCursorScreenPos();
        var regionWidth = ImGui.GetContentRegionAvail().X;
        var bubbleMinX = mine ? origin.X + regionWidth - bubbleWidth : origin.X;
        var bubbleMin = new Vector2(bubbleMinX, origin.Y);
        var bubbleMax = new Vector2(bubbleMinX + bubbleWidth, origin.Y + bubbleHeight);
        var fill = mine ? Accent : new Vector4(1f, 1f, 1f, 0.10f);
        Squircle.Fill(drawList, bubbleMin, bubbleMax, 14f * scale, ImGui.GetColorU32(fill));

        var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong;
        ImGui.SetCursorScreenPos(new Vector2(bubbleMin.X + paddingX, bubbleMin.Y + paddingY));
        ImGui.PushTextWrapPos(bubbleMin.X + paddingX + wrapWidth);
        using (ImRaii.PushColor(ImGuiCol.Text, ink))
        {
            ImGui.TextWrapped(message.Body);
        }

        ImGui.PopTextWrapPos();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(regionWidth, bubbleHeight + 6f * scale));
    }

    private void DrawMessageComposer(Rect area, string threadId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);

        var sendWidth = 40f * scale;
        var pillMin = new Vector2(area.Min.X + 12f * scale, area.Min.Y + 8f * scale);
        var pillMax = new Vector2(area.Max.X - sendWidth - 12f * scale, area.Max.Y - 8f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));

        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale, (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
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
            if (ImGui.InputTextWithHint("##velvetMessage", Loc.T(L.Velvet.MessageHint), ref messageDraft, MessageMax, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var canSend = messageDraft.Trim().Length > 0 && !store.Sending;
        var sendCenter = new Vector2(area.Max.X - sendWidth * 0.5f - 8f * scale, area.Center.Y);
        drawList.AddCircleFilled(sendCenter, 16f * scale, ImGui.GetColorU32(canSend ? Accent : theme.SurfaceMuted), 24);
        VelvetUi.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 0.85f);
        if (VelvetUi.HoverClick(sendCenter - new Vector2(16f * scale, 16f * scale), sendCenter + new Vector2(16f * scale, 16f * scale)) && canSend)
        {
            submitted = true;
        }

        if (submitted && canSend)
        {
            store.SendMessage(threadId, messageDraft, ok => sendOutcome = ok ? 1 : 2);
            threadFocus = true;
        }
    }
}
