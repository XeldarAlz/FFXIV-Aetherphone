using System.Collections.Concurrent;
using System.Numerics;
using Aetherphone.Apps.DirectMessages;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Report;
using Aetherphone.Core.Social;
using Aetherphone.Core.Telephony.Audio;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetApp
{
    private readonly ChatMenuController messageMenuController = new();
    private readonly ChatComposer composer = new();
    private readonly VoiceNotePlayer voicePlayer = new();
    private readonly ConcurrentDictionary<string, byte[]> voiceBytes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> voiceFetching = new(StringComparer.Ordinal);
    private volatile string? pendingVoicePlay;
    private Action<string>? onMessageContext;
    private Action<string>? onThreadQuoteClick;
    private Action<string, string>? onThreadReactionClick;
    private Func<string, VoiceNoteState>? voiceStateFor;
    private Action<string>? onThreadVoiceToggle;
    private Action<string>? composerPickImage;
    private Action<string, string, string?>? composerSendText;
    private Action<string, string, string>? composerEditText;
    private Action<string, byte[], int>? composerSendVoice;
    private Func<int>? composerResolveVoice;
    private Action? onThreadLoadOlder;
    private readonly ChatSearchController searchController = new();

    private void OpenMessageMenu(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || message.Deleted)
        {
            return;
        }

        messageMenuController.Open(messageId, message.SenderId == (store.Me?.UserId ?? string.Empty), message.Kind);
    }

    private void DrawMessageMenu(Rect area)
    {
        if (!messageMenuController.Active)
        {
            return;
        }

        var model = new ChatMenuModel
        {
            Ui = ui,
            ShowReactions = true,
            CanReply = true,
            CanForward = false,
            CanCopy = true,
            CanStar = false,
            CanEdit = true,
            CanInfo = false,
            CanDelete = true,
            CanReport = true,
            IsStarred = _ => false,
            MyReactionTo = store.MyReactionTo,
            OnReply = BeginReply,
            OnForward = _ => { },
            OnCopy = id => ChatActions.CopyMessageText(transcriptCache, id, CanRevealBody),
            OnStar = _ => { },
            OnEdit = BeginEdit,
            OnInfo = _ => { },
            OnDelete = AskDeleteMessage,
            OnReport = OpenReportMessage,
            OnReact = store.SetReaction,
        };
        messageMenuController.Draw(area, model);
    }

    private VelvetMessageDto? FindMessage(string messageId)
    {
        var snapshot = store.Messages;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == messageId)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    private bool CanRevealBody(string id)
    {
        var message = FindMessage(id);
        if (message is null)
        {
            return false;
        }

        return message.EncVersion != 1 || store.DecryptionState(id).State == DmBodyState.Decrypted;
    }

    private void BeginReply(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || message.Deleted)
        {
            return;
        }

        var myId = store.Me?.UserId ?? string.Empty;
        var senderName = message.SenderId == myId ? Loc.T(L.Message.You) : ThreadTitle(store.ThreadId ?? messageId);
        composer.BeginReply(messageId, senderName, ChatText.QuotePreview(message.Body, message.Kind));
    }

    private void BeginEdit(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || message.Kind != 0 || message.Deleted)
        {
            return;
        }

        if (message.EncVersion != 0 && store.DecryptionState(messageId).State != DmBodyState.Decrypted)
        {
            return;
        }

        composer.BeginEdit(messageId, message.Body ?? string.Empty);
    }

    private void AskDeleteMessage(string messageId)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Message.DeleteConfirm),
            ConfirmLabel = Loc.T(L.Message.DeleteAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            ConfirmAsync = done => store.DeleteMessage(messageId, done),
        });
    }

    private void OpenReportMessage(string messageId)
    {
        Plugin.Report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Encryption.ReportMessageAction),
            Disclosure = Loc.T(L.Encryption.ReportDisclosure),
            Submit = (reason, done) => store.ReportMessage(messageId, reason, done),
        });
    }

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
        if (ui.IconButton(flagCenter, flagRadius, FontAwesomeIcon.Flag.ToIconString(), ui.Theme.Danger,
                Palette.WithAlpha(ui.Theme.Danger, 0.16f), 0.9f, Loc.T(L.Report.Action)))
        {
            OpenReport("velvet_profile", user.UserId, Loc.T(L.Report.UserTitle));
        }
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
            var timeLine = SocialTimeZone.Describe(profileOffset);
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
        if (ui.GhostButton(blockRect, isBlocked ? Loc.T(L.Velvet.Unblock) : Loc.T(L.Velvet.Block)))
        {
            if (isBlocked)
            {
                store.Unblock(user.UserId);
            }
            else
            {
                store.Block(user.UserId, _ => { });
            }
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

    private void DrawThreadRow(VelvetThreadDto thread, bool pinned)
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
        if (pinned)
        {
            var nameWidth = Typography.Measure(displayName, 1f, FontWeight.SemiBold).X;
            AppSkin.Icon(new Vector2(textLeft + nameWidth + 12f * scale, origin.Y + 20f * scale),
                FontAwesomeIcon.Thumbtack.ToIconString(), AppPalettes.Velvet.MutedInk, 0.62f);
        }

        var previewColor = thread.UnreadCount > 0 ? theme.TextStrong : AppPalettes.Velvet.MutedInk;
        Typography.Draw(new Vector2(textLeft, origin.Y + 32f * scale), UiText.Truncate(thread.LastMessagePreview, 42),
            previewColor, 0.85f);
        var moreCenter = new Vector2(origin.X + width - 12f * scale, origin.Y + rowHeight * 0.5f);
        var moreRadius = 12f * scale;
        if (ui.IconButton(moreCenter, moreRadius, FontAwesomeIcon.EllipsisH.ToIconString(), AppPalettes.Velvet.MutedInk,
                AppSkin.Transparent, 0.95f, Loc.T(L.Velvet.More)))
        {
            menuThreadId = thread.OtherUserId;
            threadMenu.Toggle(thread.OtherUserId, new Rect(moreCenter - new Vector2(moreRadius, moreRadius),
                moreCenter + new Vector2(moreRadius, moreRadius)));
        }

        if (thread.UnreadCount > 0)
        {
            var badgeCenter = new Vector2(origin.X + width - 38f * scale, origin.Y + rowHeight * 0.5f);
            drawList.AddCircleFilled(badgeCenter, 9f * scale, ImGui.GetColorU32(Accent), 20);
            Typography.DrawCentered(badgeCenter, thread.UnreadCount.ToString(Loc.Culture), new Vector4(1f, 1f, 1f, 1f),
                0.75f, FontWeight.SemiBold);
        }

        if (UiInteract.HoverClick(origin, new Vector2(origin.X + width - 26f * scale, origin.Y + rowHeight)))
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
            searchController.Close();
            composer.ClearTargets();
            composer.CancelVoice();
            voicePlayer.Stop();
        }

        store.NoteThreadViewed(threadId);
        TickThread(threadId);
        DrawThreadHeader(area, threadId);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerHeight = 52f * scale;
        var accessoryHeight = composer.AccessoryHeight;
        var transcriptMessages = BuildTranscript(store.Messages);
        if (searchController.Open)
        {
            var searchHeight = 44f * scale;
            searchController.Draw(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)),
                new ChatSearchModel(ui, transcriptMessages, transcript.RequestScrollTo));
            top += searchHeight;
        }

        var listRect = new Rect(new Vector2(area.Min.X, top),
            new Vector2(area.Max.X, area.Max.Y - composerHeight - accessoryHeight));
        threadMediaUrl ??= store.DmMediaUrl;
        onThreadImageClick ??= id => router.Push(VelvetRoute.ImageView(id));
        onMessageContext ??= OpenMessageMenu;
        onThreadQuoteClick ??= transcript.RequestScrollTo;
        onThreadReactionClick ??= (messageId, _) => router.Push(VelvetRoute.Reactions(messageId));
        voiceStateFor ??= voicePlayer.StateFor;
        onThreadVoiceToggle ??= ToggleVoice;
        onThreadLoadOlder ??= store.LoadOlder;
        var model = new ChatTranscriptModel(threadId, transcriptMessages, store.Me?.UserId ?? string.Empty, Accent,
            theme, AppPalettes.Velvet.MutedInk, AppPalettes.Velvet.BodyInk, store.OtherTyping, store.LoadingThread,
            false, images, threadMediaUrl, onThreadImageClick, Loc.T(L.Velvet.ThreadEmpty), Loc.T(L.Common.Loading),
            onMessageContext, onQuoteClick: onThreadQuoteClick, onReactionClick: onThreadReactionClick,
            voiceState: voiceStateFor, onVoiceToggle: onThreadVoiceToggle,
            onLoadOlder: onThreadLoadOlder, hasMoreOlder: store.HasMoreOlder, loadingOlder: store.LoadingOlder);
        transcript.Draw(listRect, model);
        composerPickImage ??= id => router.Push(VelvetRoute.ChatImage(id));
        composerSendText ??= ComposerSendText;
        composerEditText ??= ComposerEditText;
        composerSendVoice ??= ComposerSendVoice;
        composerResolveVoice ??= ResolveVoiceInput;
        composer.Draw(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), new ChatComposerModel
        {
            Ui = ui,
            ConversationId = threadId,
            MaxLength = MessageMax,
            Sending = store.Sending,
            CanImage = true,
            CanVoice = true,
            CanHandleEscape = !searchController.Open,
            ResolveVoiceInput = composerResolveVoice,
            OnPickImage = composerPickImage,
            OnSendText = composerSendText,
            OnEditText = composerEditText,
            OnSendVoice = composerSendVoice,
        });
        DrawMessageMenu(area);
    }

    private void DrawThreadHeader(Rect area, string threadId)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, string.Empty, back);
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        ChatHeaderControls.DrawLock(ui, area, rowCenterY, store.EncryptingCurrent, store.VaultState, () => { });
        ChatHeaderControls.DrawSearchToggle(ui, area, rowCenterY, searchController.Open, searchController.Toggle);
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

    private ReadOnlySpan<TranscriptMessage> BuildTranscript(VelvetMessageDto[] source)
    {
        if (ReferenceEquals(source, transcriptSource))
        {
            return transcriptCache;
        }

        transcriptSource = source;
        var myId = store.Me?.UserId ?? string.Empty;
        var otherName = store.ThreadId is { } threadId ? ThreadTitle(threadId) : string.Empty;
        var mapped = new TranscriptMessage[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var message = source[index];
            if (message.Deleted)
            {
                mapped[index] = new TranscriptMessage(message.Id, message.SenderId, Loc.T(L.Message.DeletedBody), 0,
                    message.CreatedAtUnix, 0, 0, null, string.Empty, default, TranscriptFlags.Deleted);
                continue;
            }

            var replySender = string.Empty;
            var replyBody = string.Empty;
            if (message.ReplyToId is not null)
            {
                replySender = message.ReplySenderId == myId ? Loc.T(L.Message.You) : otherName;
                replyBody = ChatText.QuotePreview(message.ReplyBody, message.ReplyKind);
            }

            TranscriptReaction[]? reactions = null;
            var summaries = message.Reactions;
            if (summaries is { Length: > 0 })
            {
                reactions = new TranscriptReaction[summaries.Length];
                for (var summaryIndex = 0; summaryIndex < summaries.Length; summaryIndex++)
                {
                    reactions[summaryIndex] = new TranscriptReaction(summaries[summaryIndex].Token,
                        summaries[summaryIndex].Count, summaries[summaryIndex].Mine);
                }
            }

            mapped[index] = new TranscriptMessage(message.Id, message.SenderId, message.Body, message.Kind,
                message.CreatedAtUnix, message.MediaWidth, message.MediaHeight, message.ReadAtUnix, string.Empty,
                default, MessageFlags(message), message.ReplyToId, replySender, replyBody, message.ReplyKind,
                message.DurationSecs, reactions);
        }

        transcriptCache = mapped;
        return transcriptCache;
    }

    private byte MessageFlags(VelvetMessageDto message)
    {
        byte flags = 0;
        if (message.EditedAtUnix is not null)
        {
            flags |= TranscriptFlags.Edited;
        }

        if (message.EncVersion == 0)
        {
            return flags;
        }

        var state = store.DecryptionState(message.Id);
        flags |= TranscriptFlags.Encrypted;
        if (state.IsPlaceholder)
        {
            flags |= TranscriptFlags.Placeholder;
        }
        else if (state.State == DmBodyState.Decrypted && !state.Verified)
        {
            flags |= TranscriptFlags.Unverified;
        }

        return flags;
    }

    private void TickThread(string threadId)
    {
        PumpPendingVoice();
        var delta = ImGui.GetIO().DeltaTime;
        sinceThreadPoll += delta;
        if (sinceThreadPoll >= ThreadPollSeconds)
        {
            sinceThreadPoll = 0f;
            store.RefreshThread();
            store.RefreshTyping(threadId);
        }

        sinceTypingSend += delta;
        var draft = composer.Draft;
        if (draft != lastTypingDraft)
        {
            lastTypingDraft = draft;
            if (draft.Trim().Length > 0 && sinceTypingSend >= TypingSendSeconds)
            {
                sinceTypingSend = 0f;
                store.SendTyping(threadId);
            }
        }
    }

    private void ComposerSendText(string threadId, string text, string? replyToId)
    {
        store.SendMessage(threadId, text, _ => { }, replyToId);
        transcript.RequestSnapToBottom();
        lastTypingDraft = string.Empty;
    }

    private void ComposerEditText(string threadId, string editId, string text)
    {
        store.EditMessage(threadId, editId, text, _ => { });
        lastTypingDraft = string.Empty;
    }

    private void ComposerSendVoice(string threadId, byte[] wavBytes, int durationSecs)
    {
        store.SendVoiceMessage(threadId, wavBytes, durationSecs, _ => { });
        transcript.RequestSnapToBottom();
    }

    private int ResolveVoiceInput()
    {
        return AudioDevices.ResolveInput(configuration.CallInputDevice);
    }

    private void ToggleVoice(string messageId)
    {
        if (voiceBytes.TryGetValue(messageId, out var bytes))
        {
            pendingVoicePlay = null;
            voicePlayer.Toggle(messageId, bytes);
            return;
        }

        pendingVoicePlay = messageId;
        FetchVoice(messageId);
    }

    private void FetchVoice(string messageId)
    {
        if (voiceBytes.ContainsKey(messageId))
        {
            return;
        }

        var url = store.DmMediaUrl(messageId);
        if (url is null || !voiceFetching.TryAdd(messageId, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var data = await http.GetBytesAsync(new Uri(url), CancellationToken.None).ConfigureAwait(false);
                if (data is not null)
                {
                    voiceBytes[messageId] = data;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Voice note download failed: {exception.Message}");
            }
            finally
            {
                voiceFetching.TryRemove(messageId, out _);
            }
        });
    }

    private void PumpPendingVoice()
    {
        if (pendingVoicePlay is not { } id)
        {
            return;
        }

        if (voiceBytes.TryGetValue(id, out var bytes))
        {
            pendingVoicePlay = null;
            voicePlayer.Toggle(id, bytes);
            return;
        }

        FetchVoice(id);
    }

    private volatile ReactorDto[]? reactors;
    private string? reactorsFor;

    private void DrawReactions(Rect area, string messageId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Message.ReactionsTitle), back);
        if (reactorsFor != messageId)
        {
            reactorsFor = messageId;
            reactors = null;
            store.LoadReactions(messageId, result => reactors = result);
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var snapshot = reactors;
        if (snapshot is null)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * scale),
                Loc.T(L.Common.Loading), ui.MutedInk);
            return;
        }

        if (snapshot.Length == 0)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.ThumbsUp, Loc.T(L.Message.ReactionsTitle), string.Empty);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < snapshot.Length; index++)
            {
                DrawReactorRow(messageId, snapshot[index], scale);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawReactorRow(string messageId, ReactorDto reactor, float scale)
    {
        var myId = store.Me?.UserId ?? string.Empty;
        var mine = reactor.UserId == myId;
        var rowHeight = 54f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);
        ui.Card(drawList, origin, rowMax, 14f * scale);
        var pad = 12f * scale;
        var radius = 17f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        var label = mine
            ? Loc.T(L.Message.You)
            : reactor.DisplayName.Length > 0 ? reactor.DisplayName : reactor.Handle;
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, label, string.Empty, reactor.AvatarUrl, images,
            lodestone, 0.85f, 32);
        var textLeft = avatarCenter.X + radius + 12f * scale;
        if (mine)
        {
            Typography.Draw(new Vector2(textLeft, origin.Y + 10f * scale), label, theme.TextStrong, 1f,
                FontWeight.SemiBold);
            Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale), Loc.T(L.Message.TapToRemove),
                ui.MutedInk, TextStyles.Footnote);
        }
        else
        {
            Typography.Draw(new Vector2(textLeft, origin.Y + rowHeight * 0.5f - 9f * scale), label,
                theme.TextStrong, 1f, FontWeight.SemiBold);
        }

        var tokenColor = ReactionArt.Color(reactor.Token);
        AppSkin.Icon(new Vector2(origin.X + width - pad - 10f * scale, origin.Y + rowHeight * 0.5f),
            ReactionArt.Glyph(reactor.Token), tokenColor, 1f);
        if (mine && UiInteract.HoverClick(origin, rowMax))
        {
            store.SetReaction(messageId, string.Empty);
            var current = reactors;
            if (current is not null)
            {
                var next = new List<ReactorDto>(current.Length);
                for (var index = 0; index < current.Length; index++)
                {
                    if (current[index].UserId != myId)
                    {
                        next.Add(current[index]);
                    }
                }

                reactors = next.ToArray();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
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
        transcript.RequestSnapToBottom();
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
