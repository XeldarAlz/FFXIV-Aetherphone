using Aetherphone.Core;
using Aetherphone.Core.Message;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private readonly ThreadView threadView;

    private sealed class ThreadView : ChatThreadView<ChatMessageDto, ConversationDto>
    {
        private readonly MessageApp app;

        public ThreadView(MessageApp app)
            : base(app.store, app.ui, app.images, app.lodestone, app.http, app.library, app.configuration,
                app.confirm, app.report, app.wallpaperImages, ThreadPollSeconds, TypingSendSeconds)
        {
            this.app = app;
        }

        protected override PhoneTheme Theme => app.theme;
        protected override INavigator Navigation => app.navigation;
        protected override Action BackAction => app.back;
        protected override string MyUserId => store.MyUserId;
        protected override Vector4 Accent => ui.Accent;
        protected override string EmptyText => Loc.T(L.Message.ThreadEmpty);
        protected override string LogTag => "Message";
        protected override string PickerTitle => Loc.T(L.Common.SendPhoto);
        protected override string ImportLabel => Loc.T(L.Common.ImportFromPc);
        protected override string NoPhotosLabel => Loc.T(L.Common.NoPhotos);
        protected override string SaveLabel => Loc.T(L.Common.SaveToGallery);
        protected override string SavedLabel => Loc.T(L.Common.SavedToGallery);
        protected override bool IsGroupThread => app.store.Conversation?.IsGroup ?? false;

        protected override bool IsDeleted(ChatMessageDto message) => message.Deleted;

        protected override string SenderIdOf(ChatMessageDto message) => message.SenderId;

        protected override int KindOf(ChatMessageDto message) => message.Kind;

        protected override string? BodyOf(ChatMessageDto message) => message.Body;

        protected override int EncVersionOf(ChatMessageDto message) => message.EncVersion;

        protected override byte[]? DecryptSealed(ChatMessageDto message, string? threadId, byte[] sealedBytes) =>
            app.store.DecryptMedia(message, sealedBytes);

        protected override void OpenImageView(string messageId) => app.router.Push(MessageRoute.ImageView(messageId));

        protected override void OpenReactions(string messageId) => app.router.Push(MessageRoute.Reactions(messageId));

        protected override void PushImagePickerScreen(string threadId) => app.router.Push(MessageRoute.ChatImage(threadId));

        protected override void PopScreen() => app.router.Pop();

        protected override void OnThreadSwitchingFrom(string previousThreadId)
        {
            if (!composer.IsEditing)
            {
                SaveDraft(previousThreadId);
            }
        }

        protected override void OnThreadOpened(string threadId)
        {
            composer.Draft = configuration.MessageDrafts.GetValueOrDefault(threadId, string.Empty);
        }

        protected override void OnDraftConsumed(string threadId) => ClearDraft(threadId);

        public override void OnAppClosed()
        {
            if (!composer.IsEditing && store.CurrentThreadId is { } openConversation)
            {
                SaveDraft(openConversation);
            }

            base.OnAppClosed();
        }

        private void SaveDraft(string conversationId)
        {
            var trimmed = composer.Draft.Trim();
            var drafts = configuration.MessageDrafts;
            if (trimmed.Length == 0)
            {
                if (drafts.Remove(conversationId))
                {
                    configuration.Save();
                }

                return;
            }

            if (drafts.GetValueOrDefault(conversationId) == trimmed)
            {
                return;
            }

            drafts[conversationId] = trimmed;
            configuration.Save();
        }

        private void ClearDraft(string conversationId)
        {
            if (configuration.MessageDrafts.Remove(conversationId))
            {
                configuration.Save();
            }
        }

        protected override void BeginReply(string messageId)
        {
            var message = FindMessage(messageId);
            if (message is null || message.Kind == 2)
            {
                return;
            }

            var senderName = message.SenderId == MyUserId
                ? Loc.T(L.Message.You)
                : message.SenderDisplayName;
            composer.BeginReply(messageId, senderName, ChatText.QuotePreview(message.Body, message.Kind));
        }

        protected override ChatMenuModel BuildMenuModel()
        {
            return new ChatMenuModel
            {
                Ui = ui,
                ShowReactions = true,
                CanReply = true,
                CanForward = true,
                CanCopy = true,
                CanStar = true,
                CanEdit = true,
                CanInfo = true,
                CanDelete = true,
                CanReport = true,
                IsStarred = app.IsStarred,
                MyReactionTo = store.MyReactionTo,
                OnReply = BeginReply,
                OnForward = id => app.router.Push(MessageRoute.Forward(id)),
                OnCopy = CopyMessage,
                OnStar = app.ToggleStar,
                OnEdit = BeginEdit,
                OnInfo = id =>
                {
                    app.store.RefreshDetail();
                    app.router.Push(MessageRoute.MessageInfo(id));
                },
                OnDelete = AskDeleteMessage,
                OnReport = OpenReportMessage,
                OnReact = store.SetReaction,
            };
        }

        protected override void DrawAboveTranscript(ref Rect listRect, string threadId)
        {
            var conversation = app.store.Conversation;
            if (IsGroupThread || conversation is null || !app.store.HasRotationNotice(conversation.OtherUserId))
            {
                return;
            }

            var dismissUserId = conversation.OtherUserId;
            var text = Loc.T(L.Encryption.SafetyChanged, DirectMessagesStore.DisplayTitle(conversation));
            ChatHeaderControls.DrawBanner(ui, ref listRect, text, AppPalettes.Message.MutedInk,
                () => app.store.ClearRotationNotice(dismissUserId));
        }

        protected override void DrawHeader(Rect area, string threadId)
        {
            var conversation = app.store.Conversation;
            var isGroup = conversation?.IsGroup ?? false;
            var context = new PhoneContext(area, Theme, Navigation);
            AppHeader.Draw(context, string.Empty, BackAction);
            var scale = ImGuiHelpers.GlobalScale;
            var drawList = ImGui.GetWindowDrawList();
            var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
            ChatHeaderControls.DrawLock(ui, area, rowCenterY, store.EncryptingCurrent, store.VaultState,
                () =>
                {
                    if (conversation is not null)
                    {
                        app.router.Push(MessageRoute.Encryption(conversation.Id));
                    }
                });
            ChatHeaderControls.DrawSearchToggle(ui, area, rowCenterY, searchController.Open, searchController.Toggle);
            var name = conversation is null ? app.DisplayName : DirectMessagesStore.DisplayTitle(conversation);
            var avatarRadius = 18f * scale;
            var nameSize = Typography.Measure(name, 1f, FontWeight.SemiBold);
            var gap = 9f * scale;
            var groupWidth = avatarRadius * 2f + gap + nameSize.X;
            var startX = MathF.Max(area.Center.X - groupWidth * 0.5f, area.Min.X + 48f * scale);
            var avatarCenter = new Vector2(startX + avatarRadius, rowCenterY);
            if (isGroup)
            {
                drawList.AddCircleFilled(avatarCenter, avatarRadius,
                    ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.85f)), 32);
                AppSkin.Icon(avatarCenter, FontAwesomeIcon.Users.ToIconString(), White, 0.8f);
            }
            else
            {
                AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, Theme, name, string.Empty,
                    conversation?.OtherAvatarUrl, images, lodestone, 0.9f, 32);
            }

            var nameLeft = avatarCenter.X + avatarRadius + gap;
            if (isGroup && conversation is not null)
            {
                var sub = Loc.T(L.DirectMessages.MembersCount, conversation.MemberCount);
                var subSize = Typography.Measure(sub, 0.72f, FontWeight.Regular);
                var gapY = 1f * scale;
                var stackTop = rowCenterY - (nameSize.Y + gapY + subSize.Y) * 0.5f;
                Typography.Draw(new Vector2(nameLeft, stackTop), name, Theme.TextStrong, 1f, FontWeight.SemiBold);
                Typography.Draw(new Vector2(nameLeft, stackTop + nameSize.Y + gapY), sub,
                    AppPalettes.Message.MutedInk, 0.72f);
                var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
                var hitMax = new Vector2(nameLeft + MathF.Max(nameSize.X, subSize.X),
                    area.Min.Y + AppHeader.Height * scale);
                if (UiInteract.HoverClick(hitMin, hitMax))
                {
                    app.router.Push(MessageRoute.GroupInfo(conversation.Id));
                }
            }
            else
            {
                var presence = app.PresenceText(conversation);
                if (presence.Length > 0)
                {
                    var subSize = Typography.Measure(presence, 0.72f, FontWeight.Regular);
                    var gapY = 1f * scale;
                    var stackTop = rowCenterY - (nameSize.Y + gapY + subSize.Y) * 0.5f;
                    Typography.Draw(new Vector2(nameLeft, stackTop), name, Theme.TextStrong, 1f, FontWeight.SemiBold);
                    Typography.Draw(new Vector2(nameLeft, stackTop + nameSize.Y + gapY), presence,
                        conversation!.Presence == 1 ? ui.Accent : AppPalettes.Message.MutedInk, 0.72f);
                }
                else
                {
                    Typography.Draw(new Vector2(nameLeft, rowCenterY - nameSize.Y * 0.5f), name, Theme.TextStrong, 1f,
                        FontWeight.SemiBold);
                }

                if (conversation is not null && app.contacts.Find(conversation.OtherUserId) is not null)
                {
                    var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
                    var hitMax = new Vector2(nameLeft + nameSize.X, area.Min.Y + AppHeader.Height * scale);
                    if (UiInteract.HoverClick(hitMin, hitMax))
                    {
                        app.router.Push(MessageRoute.Contact(conversation.OtherUserId));
                    }
                }
            }
        }

        protected override TranscriptMessage[] MapTranscript(ChatMessageDto[] source)
        {
            var isGroup = IsGroupThread;
            var mapped = new TranscriptMessage[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                var message = source[index];
                if (message.Kind == 2)
                {
                    mapped[index] = new TranscriptMessage(message.Id, message.SenderId, SystemText(message), 2,
                        message.CreatedAtUnix, 0, 0, null, string.Empty, default);
                    continue;
                }

                var senderName = isGroup ? message.SenderDisplayName : string.Empty;
                var tint = isGroup ? SenderTint.Of(message.SenderDisplayName) : default;
                if (message.Deleted)
                {
                    mapped[index] = new TranscriptMessage(message.Id, message.SenderId,
                        Loc.T(L.Message.DeletedBody), 0, message.CreatedAtUnix, 0, 0, null, senderName, tint,
                        TranscriptFlags.Deleted);
                    continue;
                }

                var replySender = string.Empty;
                var replyBody = string.Empty;
                var replyKind = message.ReplyKind;
                if (message.ReplyToId is not null)
                {
                    replySender = message.ReplySenderId == MyUserId
                        ? Loc.T(L.Message.You)
                        : message.ReplySenderName ?? Loc.T(L.Message.OriginalUnavailable);
                    replyKind = ChatText.EffectiveKind(message.ReplyBody, replyKind);
                    replyBody = ChatText.QuotePreview(message.ReplyBody, replyKind);
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
                    message.CreatedAtUnix, message.MediaWidth, message.MediaHeight, message.ReadAtUnix, senderName,
                    tint, MessageFlags(message), message.ReplyToId, replySender, replyBody, replyKind,
                    message.DurationSecs, reactions);
            }

            return mapped;
        }

        private byte MessageFlags(ChatMessageDto message)
        {
            byte flags = 0;
            if (message.Forwarded)
            {
                flags |= TranscriptFlags.Forwarded;
            }

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
            else if (state.State == Aetherphone.Core.Crypto.DmBodyState.Decrypted && !state.Verified)
            {
                flags |= TranscriptFlags.Unverified;
            }

            return flags;
        }

        private static string SystemText(ChatMessageDto message)
        {
            var actor = message.SenderDisplayName;
            var body = message.Body ?? string.Empty;
            var separator = (char)0x1F;
            var separatorIndex = body.IndexOf(separator);
            var token = separatorIndex >= 0 ? body.Substring(0, separatorIndex) : body;
            var argument = separatorIndex >= 0 ? body.Substring(separatorIndex + 1) : string.Empty;
            return token switch
            {
                "created" => Loc.T(L.DirectMessages.SysCreated, actor),
                "added" => Loc.T(L.DirectMessages.SysAdded, actor, argument),
                "removed" => Loc.T(L.DirectMessages.SysRemoved, actor, argument),
                "left" => Loc.T(L.DirectMessages.SysLeft, actor),
                "renamed" => Loc.T(L.DirectMessages.SysRenamed, actor, argument),
                _ => body,
            };
        }
    }

    private bool IsStarred(string messageId)
    {
        var starred = configuration.MessageStarredMessages;
        for (var index = 0; index < starred.Count; index++)
        {
            if (starred[index].MessageId == messageId)
            {
                return true;
            }
        }

        return false;
    }

    private void ToggleStar(string messageId)
    {
        var starred = configuration.MessageStarredMessages;
        for (var index = 0; index < starred.Count; index++)
        {
            if (starred[index].MessageId == messageId)
            {
                starred.RemoveAt(index);
                configuration.Save();
                return;
            }
        }

        var message = store.FindMessage(messageId);
        var conversation = store.Conversation;
        if (message is null || message.Deleted || conversation is null)
        {
            return;
        }

        starred.Add(new StarredMessage
        {
            ConversationId = conversation.Id,
            MessageId = messageId,
            ConversationTitle = DirectMessagesStore.DisplayTitle(conversation),
            SenderName = message.SenderId == store.MyUserId ? Loc.T(L.Message.You) : message.SenderDisplayName,
            Preview = ChatText.QuotePreview(message.Body, message.Kind),
            Kind = ChatText.EffectiveKind(message.Body, message.Kind),
            CreatedAtUnix = message.CreatedAtUnix,
            StarredAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
        configuration.Save();
    }

    private string PresenceText(ConversationDto? conversation)
    {
        if (conversation is null)
        {
            return string.Empty;
        }

        if (conversation.Presence == 1)
        {
            return Loc.T(L.Message.PresenceOnline);
        }

        if (conversation.LastSeenAtUnix is { } lastSeen)
        {
            return Loc.T(L.Message.PresenceLastSeen, FormatStamp(lastSeen));
        }

        return string.Empty;
    }
}
