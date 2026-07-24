using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float ThreadPollSeconds = 2.5f;
    private const float TypingSendSeconds = 3f;

    private readonly ThreadView threadView;

    private sealed class ThreadView : ChatThreadView<VelvetMessageDto, VelvetThreadDto>
    {
        private readonly VelvetShell app;

        public ThreadView(VelvetShell app)
            : base(app.store, app.ui, app.images, app.lodestone, app.http, app.library, app.configuration,
                app.confirm, app.report, app.wallpaperImages, ThreadPollSeconds, TypingSendSeconds)
        {
            this.app = app;
        }

        protected override PhoneTheme Theme => app.theme;
        protected override INavigator Navigation => app.navigation;
        protected override Action BackAction => app.back;
        protected override string MyUserId => app.store.Me?.UserId ?? string.Empty;
        protected override Vector4 Accent => app.Accent;
        protected override string EmptyText => Loc.T(L.Velvet.ThreadEmpty);
        protected override string LogTag => "Velvet";
        protected override string PickerTitle => Loc.T(L.Common.SendPhoto);
        protected override string ImportLabel => Loc.T(L.Velvet.ImportFromPc);
        protected override string NoPhotosLabel => Loc.T(L.Velvet.NoPhotos);
        protected override string SaveLabel => Loc.T(L.Velvet.SaveToGallery);
        protected override string SavedLabel => Loc.T(L.Velvet.SavedToGallery);

        protected override bool IsDeleted(VelvetMessageDto message) => message.Deleted;

        protected override string SenderIdOf(VelvetMessageDto message) => message.SenderId;

        protected override int KindOf(VelvetMessageDto message) => message.Kind;

        protected override string? BodyOf(VelvetMessageDto message) => message.Body;

        protected override int EncVersionOf(VelvetMessageDto message) => message.EncVersion;

        protected override byte[]? DecryptSealed(VelvetMessageDto message, string? threadId, byte[] sealedBytes) =>
            threadId is null ? null : app.store.DecryptMedia(message, sealedBytes, threadId);

        protected override void OpenImageView(string messageId) => app.router.Push(VelvetView.ImageView(messageId));

        protected override void OpenReactions(string messageId) => app.router.Push(VelvetView.Reactions(messageId));

        protected override void PushImagePickerScreen(string threadId) => app.router.Push(VelvetView.ChatImage(threadId));

        protected override void PopScreen() => app.router.Pop();

        protected override void BeginReply(string messageId)
        {
            var message = FindMessage(messageId);
            if (message is null || message.Deleted)
            {
                return;
            }

            var senderName = message.SenderId == MyUserId
                ? Loc.T(L.Message.You)
                : app.ThreadTitle(store.CurrentThreadId ?? messageId);
            composer.BeginReply(messageId, senderName, ChatText.QuotePreview(message.Body, message.Kind));
        }

        protected override ChatMenuModel BuildMenuModel()
        {
            return new ChatMenuModel
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
                OnCopy = CopyMessage,
                OnStar = _ => { },
                OnEdit = BeginEdit,
                OnInfo = _ => { },
                OnDelete = AskDeleteMessage,
                OnReport = OpenReportMessage,
                OnReact = store.SetReaction,
            };
        }

        protected override void DrawHeader(Rect area, string threadId)
        {
            var context = new PhoneContext(area, Theme, Navigation);
            AppHeader.Draw(context, string.Empty, BackAction);
            var scale = ImGuiHelpers.GlobalScale;
            var drawList = ImGui.GetWindowDrawList();
            var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
            ChatHeaderControls.DrawLock(ui, area, rowCenterY, store.EncryptingCurrent, store.VaultState, () => { });
            ChatHeaderControls.DrawSearchToggle(ui, area, rowCenterY, searchController.Open, searchController.Toggle);
            var name = app.ThreadTitle(threadId);
            var avatarHandle = app.ThreadAvatar(threadId, out var monogram, out var presence);
            var avatarRadius = 18f * scale;
            var leftLimit = area.Min.X + 48f * scale;
            var rightLimit = area.Max.X - ChatHeaderControls.ReservedRightWidth * scale;
            var gap = 9f * scale;
            var nameCap = MathF.Max(40f * scale,
                MathF.Min(area.Width * 0.42f, rightLimit - leftLimit - avatarRadius * 2f - gap));
            var nameSize = Typography.Measure(name, 1f, FontWeight.SemiBold);
            nameSize.X = MathF.Min(nameSize.X, nameCap);
            var groupWidth = avatarRadius * 2f + gap + nameSize.X;
            var startX = MathF.Min(MathF.Max(area.Center.X - groupWidth * 0.5f, leftLimit), rightLimit - groupWidth);
            var avatarCenter = new Vector2(startX + avatarRadius, rowCenterY);
            AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, monogram, 0.95f, avatarHandle, 32);
            app.PresenceDot(drawList, new Vector2(avatarCenter.X + avatarRadius - 3f * scale,
                avatarCenter.Y + avatarRadius - 3f * scale), presence);
            var nameLeft = avatarCenter.X + avatarRadius + gap;
            var offset = app.ThreadOffset(threadId);
            var textWidth = nameSize.X;
            if (offset is { } minutes)
            {
                var timeText = Aetherphone.Core.Social.SocialTimeZone.Describe(minutes);
                var subSize = Typography.Measure(timeText, 0.72f, FontWeight.Regular);
                subSize.X = MathF.Min(subSize.X, nameCap);
                var gapY = 1f * scale;
                var stackTop = rowCenterY - (nameSize.Y + gapY + subSize.Y) * 0.5f;
                var titleHovering = ImGui.IsMouseHoveringRect(new Vector2(nameLeft, stackTop),
                    new Vector2(nameLeft + nameCap, stackTop + nameSize.Y));
                Marquee.DrawLeft("velvet.thread.title." + threadId, name, nameLeft, stackTop, nameCap,
                    new TextStyle(1f, FontWeight.SemiBold), Theme.TextStrong, titleHovering);
                var subTop = stackTop + nameSize.Y + gapY;
                var subHovering = ImGui.IsMouseHoveringRect(new Vector2(nameLeft, subTop),
                    new Vector2(nameLeft + nameCap, subTop + subSize.Y));
                Marquee.DrawLeft("velvet.thread.subtitle." + threadId, timeText, nameLeft, subTop, nameCap,
                    new TextStyle(0.72f, FontWeight.Regular), VelvetTheme.MutedInk, subHovering);
                textWidth = MathF.Max(nameSize.X, subSize.X);
            }
            else
            {
                var soloTop = rowCenterY - nameSize.Y * 0.5f;
                var titleHovering = ImGui.IsMouseHoveringRect(new Vector2(nameLeft, soloTop),
                    new Vector2(nameLeft + nameCap, soloTop + nameSize.Y));
                Marquee.DrawLeft("velvet.thread.title." + threadId, name, nameLeft, soloTop,
                    nameCap, new TextStyle(1f, FontWeight.SemiBold), Theme.TextStrong, titleHovering);
            }

            var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
            var hitMax = new Vector2(nameLeft + textWidth, area.Min.Y + AppHeader.Height * scale);
            if (UiInteract.HoverClick(hitMin, hitMax))
            {
                app.OpenProfile(threadId);
            }
        }

        protected override TranscriptMessage[] MapTranscript(VelvetMessageDto[] source)
        {
            var myId = MyUserId;
            var otherName = store.CurrentThreadId is { } threadId ? app.ThreadTitle(threadId) : string.Empty;
            var mapped = new TranscriptMessage[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                var message = source[index];
                if (message.Deleted)
                {
                    mapped[index] = new TranscriptMessage(message.Id, message.SenderId, Loc.T(L.Message.DeletedBody),
                        0, message.CreatedAtUnix, 0, 0, null, string.Empty, default, TranscriptFlags.Deleted);
                    continue;
                }

                var replySender = string.Empty;
                var replyBody = string.Empty;
                var replyKind = message.ReplyKind;
                if (message.ReplyToId is not null)
                {
                    replySender = message.ReplySenderId == myId ? Loc.T(L.Message.You) : otherName;
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
                    message.CreatedAtUnix, message.MediaWidth, message.MediaHeight, message.ReadAtUnix, string.Empty,
                    default, MessageFlags(message), message.ReplyToId, replySender, replyBody, replyKind,
                    message.DurationSecs, reactions);
            }

            return mapped;
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
            else if (state.State == Aetherphone.Core.Crypto.DmBodyState.Decrypted && !state.Verified)
            {
                flags |= TranscriptFlags.Unverified;
            }

            return flags;
        }
    }

    private void PresenceDot(ImDrawListPtr drawList, Vector2 center, int presence)
    {
        if (presence > VelvetPresence.Offline)
        {
            VBadge.Dot(drawList, center, VelvetTheme.PresenceColor(presence));
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

        return FindConnectionInfo(threadId)?.UtcOffsetMinutes;
    }

    private string ThreadTitle(string threadId)
    {
        var threads = store.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].OtherUserId == threadId)
            {
                return string.IsNullOrEmpty(threads[index].OtherDisplayName)
                    ? threads[index].OtherHandle
                    : threads[index].OtherDisplayName;
            }
        }

        if (FindConnectionInfo(threadId) is { } connection)
        {
            return string.IsNullOrEmpty(connection.DisplayName) ? connection.Handle : connection.DisplayName;
        }

        return string.Empty;
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

        if (FindConnectionInfo(threadId) is { } connection)
        {
            monogram = Monogram(connection.DisplayName, connection.Handle);
            presence = connection.Presence;
            return lodestone.Remote(connection.UserId, ToUri(connection.AvatarUrl));
        }

        monogram = "?";
        presence = VelvetPresence.Offline;
        return AvatarHandle.Disabled;
    }

    private VelvetConnectionDto? FindConnectionInfo(string userId)
    {
        var connections = store.Connections;
        for (var index = 0; index < connections.Length; index++)
        {
            if (connections[index].UserId == userId)
            {
                return connections[index];
            }
        }

        var requests = store.Requests;
        for (var index = 0; index < requests.Length; index++)
        {
            if (requests[index].UserId == userId)
            {
                return requests[index];
            }
        }

        var sent = store.SentRequests;
        for (var index = 0; index < sent.Length; index++)
        {
            if (sent[index].UserId == userId)
            {
                return sent[index];
            }
        }

        return null;
    }

    private static string Monogram(string displayName, string handle)
    {
        var source = string.IsNullOrEmpty(displayName) ? handle : displayName;
        return source.Length > 0 ? source[..1].ToUpperInvariant() : "?";
    }

    private static Uri? ToUri(string? url) =>
        string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ? null : uri;
}
