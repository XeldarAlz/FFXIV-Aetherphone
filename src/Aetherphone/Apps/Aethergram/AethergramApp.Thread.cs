using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float ThreadPollSeconds = 2.5f;
    private const float TypingSendSeconds = 3f;

    private readonly ThreadView threadView;

    private sealed class ThreadView : ChatThreadView<GramMessageDto, GramThreadDto>
    {
        private readonly AethergramApp app;

        public ThreadView(AethergramApp app)
            : base(app.dmStore, app.ui, app.images, app.lodestone, app.http, app.library, app.configuration,
                app.confirm, app.report, app.wallpaperImages, ThreadPollSeconds, TypingSendSeconds)
        {
            this.app = app;
        }

        protected override PhoneTheme Theme => app.theme;
        protected override INavigator Navigation => app.navigation;
        protected override Action BackAction => app.back;
        protected override string MyUserId => app.dmStore.MyUserId;
        protected override Vector4 Accent => app.Accent;
        protected override string EmptyText => Loc.T(L.Aethergram.ThreadEmpty);
        protected override string LogTag => "Aethergram";
        protected override string PickerTitle => Loc.T(L.Common.SendPhoto);
        protected override string ImportLabel => Loc.T(L.Aethergram.ImportFromPc);
        protected override string NoPhotosLabel => Loc.T(L.Common.NoPhotos);
        protected override string SaveLabel => Loc.T(L.Common.SaveToGallery);
        protected override string SavedLabel => Loc.T(L.Common.SavedToGallery);

        protected override bool IsDeleted(GramMessageDto message) => message.Deleted;

        protected override string SenderIdOf(GramMessageDto message) => message.SenderId;

        protected override int KindOf(GramMessageDto message) => message.Kind;

        protected override string? BodyOf(GramMessageDto message) => message.Body;

        protected override int EncVersionOf(GramMessageDto message) => message.EncVersion;

        protected override byte[]? DecryptSealed(GramMessageDto message, string? threadId, byte[] sealedBytes) =>
            threadId is null ? null : app.dmStore.DecryptMedia(message, sealedBytes, threadId);

        protected override void OpenImageView(string messageId) =>
            app.router.Push(AethergramRoute.ImageView(messageId));

        protected override void OpenReactions(string messageId) =>
            app.router.Push(AethergramRoute.Reactions(messageId));

        protected override void PushImagePickerScreen(string threadId) =>
            app.router.Push(AethergramRoute.ChatImage(threadId));

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
            var nameSize = Typography.Measure(name, 1f, FontWeight.SemiBold);
            var gap = 9f * scale;
            var groupWidth = avatarRadius * 2f + gap + nameSize.X;
            var startX = MathF.Max(area.Center.X - groupWidth * 0.5f, area.Min.X + 48f * scale);
            var avatarCenter = new Vector2(startX + avatarRadius, rowCenterY);
            AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, monogram, 0.95f, avatarHandle, 32);
            app.PresenceDot(drawList, new Vector2(avatarCenter.X + avatarRadius - 3f * scale,
                avatarCenter.Y + avatarRadius - 3f * scale), presence);
            var nameLeft = avatarCenter.X + avatarRadius + gap;
            var offset = app.ThreadOffset(threadId);
            var textWidth = nameSize.X;
            if (offset is { } minutes)
            {
                var timeText = SocialTimeZone.Describe(minutes);
                var subSize = Typography.Measure(timeText, 0.72f, FontWeight.Regular);
                var gapY = 1f * scale;
                var stackTop = rowCenterY - (nameSize.Y + gapY + subSize.Y) * 0.5f;
                Typography.Draw(new Vector2(nameLeft, stackTop), name, Theme.TextStrong, 1f, FontWeight.SemiBold);
                Typography.Draw(new Vector2(nameLeft, stackTop + nameSize.Y + gapY), timeText,
                    AppPalettes.Aethergram.MutedInk, 0.72f);
                textWidth = MathF.Max(nameSize.X, subSize.X);
            }
            else
            {
                Typography.Draw(new Vector2(nameLeft, rowCenterY - nameSize.Y * 0.5f), name, Theme.TextStrong, 1f,
                    FontWeight.SemiBold);
            }

            var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
            var hitMax = new Vector2(nameLeft + textWidth, area.Min.Y + AppHeader.Height * scale);
            if (UiInteract.HoverClick(hitMin, hitMax))
            {
                app.OpenProfile(threadId);
            }
        }

        protected override TranscriptMessage[] MapTranscript(GramMessageDto[] source)
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

            return mapped;
        }

        private byte MessageFlags(GramMessageDto message)
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
        if (presence != 1)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        drawList.AddCircleFilled(center, 5f * scale, ImGui.GetColorU32(AppPalettes.Aethergram.BackdropBottom), 16);
        drawList.AddCircleFilled(center, 3.5f * scale, ImGui.GetColorU32(ui.Accent), 16);
    }

    private int? ThreadOffset(string threadId)
    {
        var threads = dmStore.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].OtherUserId == threadId)
            {
                return threads[index].UtcOffsetMinutes;
            }
        }

        if (store.ProfileUser is { } user && user.Id == threadId)
        {
            return user.UtcOffsetMinutes;
        }

        return null;
    }

    private string ThreadTitle(string threadId)
    {
        var threads = dmStore.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].OtherUserId == threadId)
            {
                return SocialIdentity.Name(threads[index].OtherDisplayName, threads[index].OtherHandle);
            }
        }

        if (store.ProfileUser is { } user && user.Id == threadId)
        {
            return SocialIdentity.Name(user.DisplayName, user.Handle);
        }

        return string.Empty;
    }

    private AvatarHandle ThreadAvatar(string threadId, out string monogram, out int presence)
    {
        var threads = dmStore.Threads;
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

        if (store.ProfileUser is { } user && user.Id == threadId)
        {
            monogram = Monogram(user.DisplayName, user.Handle);
            presence = 0;
            return lodestone.Remote(user.Id, ToUri(user.AvatarUrl));
        }

        monogram = "?";
        presence = 0;
        return AvatarHandle.Disabled;
    }

    private static string Monogram(string displayName, string handle)
    {
        var source = string.IsNullOrEmpty(displayName) ? handle : displayName;
        return source.Length > 0 ? source[..1].ToUpperInvariant() : "?";
    }

    private static Uri? ToUri(string? url) =>
        string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ? null : uri;
}
