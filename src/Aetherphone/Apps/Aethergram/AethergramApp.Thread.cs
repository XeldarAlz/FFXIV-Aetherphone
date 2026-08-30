using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float ThreadPollSeconds = 2.5f;
    private const float TypingSendSeconds = 3f;
    private const float ThreadBackInset = 12f;
    private const float ThreadAvatarRadius = 18f;
    private const float ThreadAvatarGap = 6f;
    private const float ThreadNameGap = 9f;
    private const float ThreadToggleIconSize = 21f;
    private const float RequestNoticeGap = 12f;
    private const float PresenceDotRadius = 4.5f;
    private const float PresenceRingRadius = 6.5f;

    private static readonly TextStyle ThreadNameStyle = TextStyles.Headline;
    private static readonly TextStyle ThreadSubStyle = TextStyles.Footnote;

    private string handleLabelSource = string.Empty;
    private string handleLabel = string.Empty;

    private sealed class ThreadView : ChatThreadView<GramMessageDto, GramThreadDto>, IChatTranscriptPostCards,
        IChatTranscriptStoryReplies
    {
        private readonly AethergramApp app;

        public ThreadView(AethergramApp app)
            : base(app.dmStore, app.ui, app.images, app.lodestone, app.http, app.library, app.configuration,
                app.confirm, app.report, app.translation, app.wallpaperImages, app.encryptionHelp, ThreadPollSeconds,
                TypingSendSeconds)
        {
            this.app = app;
        }

        protected override PhoneTheme Theme => app.theme;
        protected override IPhoneApp Owner => app;
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

        protected override IChatTranscriptPostCards? PostCards => this;

        public bool TryResolve(string messageId, string body, out ChatPostCard card)
        {
            card = default;
            if (body.Length == 0)
            {
                return false;
            }

            if (!app.dmStore.TryResolvePost(body, out var post))
            {
                return false;
            }

            if (post is null)
            {
                card = new ChatPostCard(body, string.Empty, string.Empty, null, false);
                return true;
            }

            var photos = PostMedia.Photos(post.MediaUrls, post.MediaUrl);
            card = new ChatPostCard(post.Id, SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle),
                post.Text, photos.Length > 0 ? photos[0] : null, true,
                SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, app.configuration.ShowSensitiveContent));
            return true;
        }

        public void Open(string postId) => app.OpenDetailFromLink(postId);

        public IDalamudTextureWrap? Thumbnail(string url) => app.images.Get(url);

        protected override IChatTranscriptStoryReplies? StoryReplies => this;

        public bool TryResolve(string messageId, out ChatStoryReplyContext context)
        {
            context = default;
            var message = FindMessage(messageId);
            if (message is null)
            {
                return false;
            }

            var contextText = Loc.T(message.SenderId == MyUserId
                ? L.Aethergram.YouRepliedToStory
                : L.Aethergram.RepliedToYourStory);
            var unavailable = message.StoryExpired || string.IsNullOrEmpty(message.StoryMediaUrl);
            context = new ChatStoryReplyContext(contextText, unavailable ? null : message.StoryMediaUrl, unavailable);
            return true;
        }

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

        protected override void OpenEncryptionInfo(string threadId) => app.router.Push(AethergramRoute.Encryption);

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
                CanTranslate = true,
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
                OnTranslate = TranslateMessage,
                OnReact = store.SetReaction,
            };
        }

        protected override void DrawAboveTranscript(ref Rect listRect, string threadId)
        {
            if (!app.dmStore.IsThreadPending(threadId))
            {
                return;
            }

            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var pad = CellPadX * scale;
            var gap = RequestNoticeGap * scale;
            var innerWidth = listRect.Width - pad * 2f;
            var text = Loc.T(L.Aethergram.RequestBanner, app.ThreadTitle(threadId));
            var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, innerWidth).Y;
            var buttonHeight = PillHeight * scale;
            var noticeMax = new Vector2(listRect.Max.X,
                listRect.Min.Y + gap + textHeight + gap + buttonHeight + gap);
            drawList.AddRectFilled(listRect.Min, noticeMax, ImGui.GetColorU32(Ink.AccentWash));
            Typography.DrawWrappedLeft(new Vector2(listRect.Min.X + pad, listRect.Min.Y + gap), text, Ink.BodyInk,
                TextStyles.Subheadline, innerWidth);
            var buttonsTop = listRect.Min.Y + gap + textHeight + gap;
            var buttonWidth = (innerWidth - gap) * 0.5f;
            var acceptRect = new Rect(new Vector2(listRect.Min.X + pad, buttonsTop),
                new Vector2(listRect.Min.X + pad + buttonWidth, buttonsTop + buttonHeight));
            var deleteRect = new Rect(new Vector2(acceptRect.Max.X + gap, buttonsTop),
                new Vector2(listRect.Min.X + pad + innerWidth, buttonsTop + buttonHeight));
            if (DrawAccentPill(acceptRect, Loc.T(L.Aethergram.AcceptRequest)))
            {
                app.dmStore.AcceptThread(threadId);
            }

            if (DrawGrayPill(deleteRect, Loc.T(L.Aethergram.DeleteConfirm)))
            {
                app.AskDeleteConversation(threadId);
            }

            DrawHairline(drawList, listRect.Min.X, listRect.Max.X, noticeMax.Y);
            listRect = new Rect(new Vector2(listRect.Min.X, noticeMax.Y), listRect.Max);
        }

        protected override void DrawHeader(Rect area, string threadId)
        {
            var scale = UiScale.Current;
            var sidePadding = app.theme.SidePadding * scale;
            area = new Rect(new Vector2(area.Min.X - sidePadding, area.Min.Y),
                new Vector2(area.Max.X + sidePadding, area.Max.Y));
            var drawList = ImGui.GetWindowDrawList();
            var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
            var chipRadius = SocialChrome.BackChipRadius * scale;
            var chipCenter = new Vector2(area.Min.X + ThreadBackInset * scale + chipRadius, rowCenterY);
            if (SocialChrome.DrawBackChip(drawList, chipCenter, chipRadius, Ink))
            {
                BackAction();
            }

            var slots = DrawHeaderToggles(drawList, area, threadId);
            var name = app.ThreadTitle(threadId);
            var avatarRadius = ThreadAvatarRadius * scale;
            var avatarHandle = app.ThreadAvatar(threadId, avatarRadius * 2f, out var monogram, out var presence);
            var avatarCenter = new Vector2(chipCenter.X + chipRadius + ThreadAvatarGap * scale + avatarRadius,
                rowCenterY);
            AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, monogram, 0.95f, avatarHandle, 32);
            var dotInset = avatarRadius * 0.72f;
            app.PresenceDot(drawList, new Vector2(avatarCenter.X + dotInset, avatarCenter.Y + dotInset), presence);
            var nameLeft = avatarCenter.X + avatarRadius + ThreadNameGap * scale;
            var nameRight = SocialChrome.HeaderSlot(area, slots - 1).X - SocialChrome.HeaderIconRadius * scale
                - ThreadNameGap * scale;
            var nameCap = MathF.Max(1f, nameRight - nameLeft);
            var nameSize = Typography.Measure(name, ThreadNameStyle);
            nameSize.X = MathF.Min(nameSize.X, nameCap);
            var titleId = "aethergramapp.thread.title." + threadId;
            var sub = app.ThreadSubtitle(threadId, presence);
            var textWidth = nameSize.X;
            if (sub.Length > 0)
            {
                var subSize = Typography.Measure(sub, ThreadSubStyle);
                subSize.X = MathF.Min(subSize.X, nameCap);
                var gapY = 1f * scale;
                var stackTop = rowCenterY - (nameSize.Y + gapY + subSize.Y) * 0.5f;
                var titleHovering = UiInteract.Hover(new Vector2(nameLeft, stackTop),
                    new Vector2(nameLeft + nameCap, stackTop + nameSize.Y));
                Marquee.DrawLeft(titleId, name, nameLeft, stackTop, nameCap, ThreadNameStyle, Ink.TitleInk,
                    titleHovering);
                var subTop = stackTop + nameSize.Y + gapY;
                var subHovering = UiInteract.Hover(new Vector2(nameLeft, subTop),
                    new Vector2(nameLeft + nameCap, subTop + subSize.Y));
                Marquee.DrawLeft(new MarqueeId(titleId, ".sub"), sub, nameLeft, subTop, nameCap, ThreadSubStyle,
                    Ink.MutedInk, subHovering);
                textWidth = MathF.Max(nameSize.X, subSize.X);
            }
            else
            {
                var soloTop = rowCenterY - nameSize.Y * 0.5f;
                var titleHovering = UiInteract.Hover(new Vector2(nameLeft, soloTop),
                    new Vector2(nameLeft + nameCap, soloTop + nameSize.Y));
                Marquee.DrawLeft(titleId, name, nameLeft, soloTop, nameCap, ThreadNameStyle, Ink.TitleInk,
                    titleHovering);
            }

            var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
            var hitMax = new Vector2(nameLeft + textWidth, area.Min.Y + AppHeader.Height * scale);
            if (UiInteract.HoverClick(hitMin, hitMax))
            {
                app.OpenProfile(threadId);
            }
        }

        private int DrawHeaderToggles(ImDrawListPtr drawList, Rect area, string threadId)
        {
            var scale = UiScale.Current;
            var encrypted = store.EncryptingCurrent;
            var vault = store.VaultState;
            var lockTooltip = encrypted
                ? Loc.T(L.Encryption.EncryptedIndicator)
                : vault == KeyVaultState.Provisioning
                    ? Loc.T(L.Encryption.SettingUp)
                    : vault == KeyVaultState.Locked
                        ? Loc.T(L.Encryption.StateLocked)
                        : Loc.T(L.Encryption.PlaintextIndicator);
            if (SocialChrome.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0),
                    SocialChrome.HeaderIconRadius * scale, encrypted ? PhoneIcons.Lock : PhoneIcons.LockOpen,
                    ThreadToggleIconSize, lockTooltip, Ink,
                    Ink.MutedInk, encrypted))
            {
                OpenEncryptionInfo(threadId);
            }

            if (DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 1), PhoneIcons.Search, Loc.T(L.Common.Search),
                    searchController.Open, 0, ThreadToggleIconSize))
            {
                searchController.Toggle();
            }

            if (!translation.Enabled)
            {
                return 2;
            }

            var scope = ConversationScope(threadId);
            var translated = translation.IsConversationTranslated(scope);
            if (!DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 2), PhoneIcons.Language,
                    Loc.T(translated ? L.Translate.ChatOn : L.Translate.ChatToggle), translated, 0,
                    ThreadToggleIconSize))
            {
                return 3;
            }

            if (translated)
            {
                translation.SetConversationTranslated(scope, false);
                return 3;
            }

            TranslateLink.WithDisclosure(translation, app.confirm,
                () => translation.SetConversationTranslated(scope, true));
            return 3;
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

        var scale = UiScale.Current;
        drawList.AddCircleFilled(center, PresenceRingRadius * scale, ImGui.GetColorU32(Ink.BackdropTop), 20);
        drawList.AddCircleFilled(center, PresenceDotRadius * scale, ImGui.GetColorU32(Ink.PresenceGreen), 20);
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

    private string ThreadSubtitle(string threadId, int presence)
    {
        if (ThreadOffset(threadId) is { } minutes)
        {
            return SocialTimeZone.Describe(minutes);
        }

        if (presence == 1)
        {
            return Loc.T(L.Aethergram.ActiveNow);
        }

        return ThreadHandle(threadId);
    }

    private string ThreadHandle(string threadId)
    {
        var threads = dmStore.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].OtherUserId == threadId)
            {
                return HandleLabel(threads[index].OtherHandle);
            }
        }

        if (store.ProfileUser is { } user && user.Id == threadId)
        {
            return HandleLabel(user.Handle);
        }

        return string.Empty;
    }

    private string HandleLabel(string handle)
    {
        if (handle.Length == 0)
        {
            return string.Empty;
        }

        if (!ReferenceEquals(handle, handleLabelSource))
        {
            handleLabelSource = handle;
            handleLabel = "@" + handle;
        }

        return handleLabel;
    }

    private AvatarHandle ThreadAvatar(string threadId, float drawnPixels, out string monogram, out int presence)
    {
        var threads = dmStore.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].OtherUserId == threadId)
            {
                var thread = threads[index];
                monogram = Monogram(thread.OtherDisplayName, thread.OtherHandle);
                presence = thread.Presence;
                return images.Avatar(thread.OtherAvatarUrl, drawnPixels);
            }
        }

        if (store.ProfileUser is { } user && user.Id == threadId)
        {
            monogram = Monogram(user.DisplayName, user.Handle);
            presence = 0;
            return images.Avatar(user.AvatarUrl, drawnPixels);
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

}
