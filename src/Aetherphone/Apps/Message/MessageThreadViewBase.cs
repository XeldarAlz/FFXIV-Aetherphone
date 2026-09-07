using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Message;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Social;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal abstract class MessageThreadViewBase : ChatThreadView<ChatMessageDto, ConversationDto>,
    IChatTranscriptSenders
{
    public const float ThreadPollSeconds = 3f;
    public const float TypingSendSeconds = 2.5f;
    public const float BubbleRounding = 9f;
    public const float ThreadSidePadding = AppSurface.SidePadding;
    private const float SenderAvatarMonogramScale = 0.9f;
    private const int SenderAvatarSegments = 24;

    protected readonly DirectMessagesStore messages;
    private readonly WallpaperImageCache wallpapers;
    private ConversationMemberDto[] memberLineSource = Array.Empty<ConversationMemberDto>();
    private string memberLine = string.Empty;

    protected MessageThreadViewBase(DirectMessagesStore store, AppSkin ui, RemoteImageCache images,
        LodestoneService lodestone, HttpService http, PhotoLibrary library, Configuration configuration,
        ConfirmService confirm, ReportService report, TranslationService translation,
        WallpaperImageCache wallpaperImages, EncryptionHelpService encryptionHelp)
        : base(store, ui, images, lodestone, http, library, configuration, confirm, report, translation,
            wallpaperImages, encryptionHelp, ThreadPollSeconds, TypingSendSeconds)
    {
        messages = store;
        wallpapers = wallpaperImages;
    }

    protected abstract MessageTheme ChatTheme { get; }

    protected override string MyUserId => messages.MyUserId;

    protected override Vector4 Accent => ui.Accent;

    protected override string EmptyText => Loc.T(L.Message.ThreadEmpty);

    protected override string LogTag => "Message";

    protected override string PickerTitle => Loc.T(L.Common.SendPhoto);

    protected override string ImportLabel => Loc.T(L.Common.ImportFromPc);

    protected override string NoPhotosLabel => Loc.T(L.Common.NoPhotos);

    protected override string SaveLabel => Loc.T(L.Common.SaveToGallery);

    protected override string SavedLabel => Loc.T(L.Common.SavedToGallery);

    protected override bool IsGroupThread => messages.Conversation?.IsGroup ?? false;

    protected override ChatComposerStyle ComposerStyle => ChatComposerStyle.Plus;

    protected override string ComposerHint => Loc.T(L.DirectMessages.StartChat);

    protected override ChatBubbleStyle BubbleStyle => new(ChatTheme.OutgoingBubble, MessageThemes.OutgoingInk,
        MessageThemes.IncomingBubble, MessageThemes.IncomingInk, BubbleRounding, true);

    protected override float TranscriptSidePadding => ThreadSidePadding;

    protected override IChatTranscriptSenders? Senders => this;

    public void DrawAvatar(ImDrawListPtr drawList, in TranscriptMessage message, Vector2 center, float radius)
    {
        var member = FindMember(message.SenderId);
        var avatarUrl = member is null ? message.SenderAvatarUrl : member.AvatarUrl;
        AvatarView.DrawRemote(drawList, center, radius, Theme, message.SenderName, string.Empty, avatarUrl, images,
            lodestone, SenderAvatarMonogramScale, SenderAvatarSegments, 1f,
            member is null ? null : Frames.Of(member.FrameId));
    }

    private ConversationMemberDto? FindMember(string userId)
    {
        var members = messages.Members;
        for (var index = 0; index < members.Length; index++)
        {
            if (members[index].UserId == userId)
            {
                return members[index];
            }
        }

        return null;
    }

    public bool SearchOpen => searchController.Open;

    public void ToggleSearch() => searchController.Toggle();

    public bool CanTranslate => TranslationAvailable;

    public bool TranslatingThread(string threadId) => IsConversationTranslated(threadId);

    public void ToggleTranslation(string threadId) => ToggleConversationTranslation(threadId);

    protected override void PaintTranscriptBackdrop(Rect listRect)
    {
        var conversationId = messages.CurrentThreadId ?? string.Empty;
        MessageWallpapers.Paint(ImGui.GetWindowDrawList(), listRect,
            MessageWallpapers.Effective(configuration, conversationId), configuration.MessageWallpaperPattern,
            wallpapers);
    }

    protected override bool IsDeleted(ChatMessageDto message) => message.Deleted;

    protected override string SenderIdOf(ChatMessageDto message) => message.SenderId;

    protected override int KindOf(ChatMessageDto message) => message.Kind;

    protected override string? BodyOf(ChatMessageDto message) => message.Body;

    protected override int EncVersionOf(ChatMessageDto message) => message.EncVersion;

    protected override byte[]? DecryptSealed(ChatMessageDto message, string? threadId, byte[] sealedBytes) =>
        messages.DecryptMedia(message, sealedBytes);

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

    protected override void DrawAboveTranscript(ref Rect listRect, string threadId)
    {
        var conversation = messages.Conversation;
        if (IsGroupThread || conversation is null || !messages.HasRotationNotice(conversation.OtherUserId))
        {
            return;
        }

        var dismissUserId = conversation.OtherUserId;
        var text = Loc.T(L.Encryption.SafetyChanged, DirectMessagesStore.DisplayTitle(conversation));
        ChatHeaderControls.DrawBanner(ui, ref listRect, text, ui.MutedInk,
            () => messages.ClearRotationNotice(dismissUserId));
    }

    protected string GroupSubtitle(ConversationDto conversation)
    {
        var members = messages.Members;
        if (members.Length == 0)
        {
            return Loc.T(L.DirectMessages.MembersCount, conversation.MemberCount);
        }

        if (ReferenceEquals(members, memberLineSource))
        {
            return memberLine;
        }

        memberLineSource = members;
        var builder = new System.Text.StringBuilder(64);
        var myId = MyUserId;
        for (var index = 0; index < members.Length; index++)
        {
            if (!members[index].IsActive)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(members[index].UserId == myId
                ? Loc.T(L.Message.You)
                : DirectMessagesStore.MemberLabel(members[index]));
        }

        memberLine = builder.ToString();
        return memberLine;
    }

    protected static string PresenceText(ConversationDto? conversation)
    {
        if (conversation is null)
        {
            return string.Empty;
        }

        if (ChatPresence.IsOnline(conversation.Presence))
        {
            return Loc.T(L.Message.PresenceOnline);
        }

        if (conversation.LastSeenAtUnix is { } lastSeen)
        {
            return Loc.T(L.Message.PresenceLastSeen, TimeText.Stamp(lastSeen));
        }

        return string.Empty;
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
            var senderAvatar = isGroup ? message.SenderAvatarUrl : null;
            var tint = isGroup ? SenderTint.Of(message.SenderDisplayName) : default;
            if (message.Deleted)
            {
                mapped[index] = new TranscriptMessage(message.Id, message.SenderId,
                    Loc.T(L.Message.DeletedBody), 0, message.CreatedAtUnix, 0, 0, null, senderName, tint,
                    TranscriptFlags.Deleted, senderAvatarUrl: senderAvatar);
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
                message.DurationSecs, reactions, message.SenderBadges, message.SenderBadgeIds,
                senderAvatarUrl: senderAvatar);
        }

        return mapped;
    }

    protected bool IsStarred(string messageId)
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

    private byte MessageFlags(ChatMessageDto message)
    {
        byte flags = 0;
        if (IsStarred(message.Id))
        {
            flags |= TranscriptFlags.Starred;
        }

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

        var state = messages.DecryptionState(message.Id);
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
            "promoted" => Loc.T(L.DirectMessages.SysPromoted, actor, argument),
            "demoted" => Loc.T(L.DirectMessages.SysDemoted, actor, argument),
            "photo" => Loc.T(L.DirectMessages.SysPhoto, actor),
            "description" => Loc.T(L.DirectMessages.SysDescription, actor),
            _ => body,
        };
    }
}
