using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal sealed partial class SocialDms
{
    private const float ThreadPollSeconds = 2.5f;
    private const float TypingSendSeconds = 3f;
    private const float ThreadBackInset = 12f;
    private const float ThreadAvatarRadius = 19f;
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

    private sealed class ThreadView : ChatThreadView<DmMessageDto, DmThreadDto>, IChatTranscriptPostCards,
        IChatTranscriptStoryReplies
    {
        private readonly SocialDms dms;

        public ThreadView(SocialDms dms, HttpService http, PhotoLibrary library, ReportService report,
            TranslationService translation, WallpaperImageCache wallpaperImages, EncryptionHelpService encryptionHelp)
            : base(dms.dm, dms.ui, dms.images, dms.lodestone, http, library, dms.configuration, dms.confirm, report,
                translation, wallpaperImages, encryptionHelp, ThreadPollSeconds, TypingSendSeconds)
        {
            this.dms = dms;
        }

        protected override PhoneTheme Theme => dms.host.Theme;
        protected override IPhoneApp Owner => dms.host;
        protected override INavigator Navigation => dms.host.Navigation;
        protected override Action BackAction => dms.back;
        protected override string MyUserId => dms.dm.MyUserId;
        protected override Vector4 Accent => dms.host.Accent;
        protected override string EmptyText => Loc.T(dms.labels.ThreadEmpty);
        protected override string LogTag => dms.labels.LogTag;
        protected override string PickerTitle => Loc.T(L.Common.SendPhoto);
        protected override string ImportLabel => Loc.T(dms.labels.ImportFromPc);
        protected override string NoPhotosLabel => Loc.T(L.Common.NoPhotos);
        protected override string SaveLabel => Loc.T(L.Common.SaveToGallery);
        protected override string SavedLabel => Loc.T(L.Common.SavedToGallery);

        protected override ChatComposerStyle ComposerStyle => ChatComposerStyle.Pill;

        protected override string ComposerHint => Loc.T(dms.labels.MessageHint);

        protected override IChatTranscriptPostCards? PostCards => this;

        public bool TryResolve(string messageId, string body, out ChatPostCard card)
        {
            card = default;
            if (body.Length == 0)
            {
                return false;
            }

            if (!dms.dm.TryResolvePost(body, out var post))
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
                SensitiveReveals.ShouldVeil(post.Sensitive, post.Id, dms.configuration.ShowSensitiveContent));
            return true;
        }

        public void Open(string postId) => dms.host.OpenPost(postId);

        public IDalamudTextureWrap? Thumbnail(string url) => dms.images.Get(url);

        protected override IChatTranscriptStoryReplies? StoryReplies =>
            dms.labels.YouRepliedToStory is null ? null : this;

        public bool TryResolve(string messageId, out ChatStoryReplyContext context)
        {
            context = default;
            var message = FindMessage(messageId);
            if (message is null || dms.labels.YouRepliedToStory is not { } mine
                || dms.labels.RepliedToYourStory is not { } theirs)
            {
                return false;
            }

            var contextText = Loc.T(message.SenderId == MyUserId ? mine : theirs);
            var unavailable = message.StoryExpired || string.IsNullOrEmpty(message.StoryMediaUrl);
            context = new ChatStoryReplyContext(contextText, unavailable ? null : message.StoryMediaUrl, unavailable);
            return true;
        }

        protected override bool IsDeleted(DmMessageDto message) => message.Deleted;

        protected override string SenderIdOf(DmMessageDto message) => message.SenderId;

        protected override int KindOf(DmMessageDto message) => message.Kind;

        protected override string? BodyOf(DmMessageDto message) => message.Body;

        protected override int EncVersionOf(DmMessageDto message) => message.EncVersion;

        protected override byte[]? DecryptSealed(DmMessageDto message, string? threadId, byte[] sealedBytes) =>
            threadId is null ? null : dms.dm.DecryptMedia(message, sealedBytes, threadId);

        protected override void OpenImageView(string messageId) =>
            dms.host.PushDm(SocialDmScreen.ImageView, messageId);

        protected override void OpenReactions(string messageId) =>
            dms.host.PushDm(SocialDmScreen.Reactions, messageId);

        protected override void PushImagePickerScreen(string threadId) =>
            dms.host.PushDm(SocialDmScreen.ChatImage, threadId);

        protected override void PopScreen() => dms.host.PopDm();

        protected override void OpenEncryptionInfo(string threadId) => dms.host.PushDm(SocialDmScreen.Encryption);

        protected override void BeginReply(string messageId) =>
            composer.BeginReply(FindMessage(messageId), MyUserId, dms.ThreadTitle(store.CurrentThreadId ?? messageId));

        protected override ChatMenuModel BuildMenuModel() =>
            ChatMenuModel.Direct(ui, store.MyReactionTo, store.SetReaction, BeginReply, CopyMessage, BeginEdit,
                AskDeleteMessage, OpenReportMessage, TranslateMessage);

        protected override void DrawAboveTranscript(ref Rect listRect, string threadId)
        {
            if (!dms.dm.IsThreadPending(threadId))
            {
                return;
            }

            var ink = dms.Ink;
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var pad = CellPadX * scale;
            var gap = RequestNoticeGap * scale;
            var innerWidth = listRect.Width - pad * 2f;
            var text = Loc.T(dms.labels.RequestBanner, dms.ThreadTitle(threadId));
            var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, innerWidth).Y;
            var buttonHeight = PillHeight * scale;
            var noticeMax = new Vector2(listRect.Max.X,
                listRect.Min.Y + gap + textHeight + gap + buttonHeight + gap);
            drawList.AddRectFilled(listRect.Min, noticeMax, ImGui.GetColorU32(ink.AccentWash));
            Typography.DrawWrappedLeft(new Vector2(listRect.Min.X + pad, listRect.Min.Y + gap), text, ink.BodyInk,
                TextStyles.Subheadline, innerWidth);
            var buttonsTop = listRect.Min.Y + gap + textHeight + gap;
            var buttonWidth = (innerWidth - gap) * 0.5f;
            var acceptRect = new Rect(new Vector2(listRect.Min.X + pad, buttonsTop),
                new Vector2(listRect.Min.X + pad + buttonWidth, buttonsTop + buttonHeight));
            var deleteRect = new Rect(new Vector2(acceptRect.Max.X + gap, buttonsTop),
                new Vector2(listRect.Min.X + pad + innerWidth, buttonsTop + buttonHeight));
            if (dms.DrawAccentPill(acceptRect, Loc.T(dms.labels.AcceptRequest)))
            {
                dms.dm.AcceptThread(threadId);
            }

            if (dms.DrawGrayPill(deleteRect, Loc.T(dms.labels.DeleteConfirm)))
            {
                dms.AskDeleteConversation(threadId);
            }

            DrawHairline(drawList, listRect.Min.X, listRect.Max.X, noticeMax.Y, ink);
            listRect = new Rect(new Vector2(listRect.Min.X, noticeMax.Y), listRect.Max);
        }

        protected override void DrawHeader(Rect area, string threadId)
        {
            var ink = dms.Ink;
            var scale = UiScale.Current;
            var sidePadding = dms.host.Theme.SidePadding * scale;
            area = new Rect(new Vector2(area.Min.X - sidePadding, area.Min.Y),
                new Vector2(area.Max.X + sidePadding, area.Max.Y));
            var drawList = ImGui.GetWindowDrawList();
            var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
            var chipRadius = SocialChrome.BackChipRadius * scale;
            var chipCenter = new Vector2(area.Min.X + ThreadBackInset * scale + chipRadius, rowCenterY);
            if (SocialChrome.DrawBackChip(drawList, chipCenter, chipRadius, ink))
            {
                BackAction();
            }

            var slots = DrawHeaderToggles(drawList, area, threadId, ink);
            var name = dms.ThreadTitle(threadId);
            var avatarRadius = ThreadAvatarRadius * scale;
            var avatarHandle = dms.ThreadAvatar(threadId, avatarRadius * 2f, out var monogram, out var presence);
            var avatarCenter = new Vector2(chipCenter.X + chipRadius + ThreadAvatarGap * scale + avatarRadius,
                rowCenterY);
            AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, monogram, 0.95f, avatarHandle, 32);
            var dotInset = avatarRadius * 0.72f;
            dms.PresenceDot(drawList, new Vector2(avatarCenter.X + dotInset, avatarCenter.Y + dotInset), presence);
            var nameLeft = avatarCenter.X + avatarRadius + ThreadNameGap * scale;
            var nameRight = SocialChrome.HeaderSlot(area, slots - 1).X - SocialChrome.HeaderIconRadius * scale
                - ThreadNameGap * scale;
            var nameCap = MathF.Max(1f, nameRight - nameLeft);
            var nameSize = Typography.Measure(name, ThreadNameStyle);
            nameSize.X = MathF.Min(nameSize.X, nameCap);
            var sub = dms.ThreadSubtitle(threadId, presence);
            var textWidth = nameSize.X;
            if (sub.Length > 0)
            {
                var subSize = Typography.Measure(sub, ThreadSubStyle);
                subSize.X = MathF.Min(subSize.X, nameCap);
                var gapY = 1f * scale;
                var stackTop = rowCenterY - (nameSize.Y + gapY + subSize.Y) * 0.5f;
                var titleHovering = UiInteract.Hover(new Vector2(nameLeft, stackTop),
                    new Vector2(nameLeft + nameCap, stackTop + nameSize.Y));
                Marquee.DrawLeft(new MarqueeId("dm.thread.title.", threadId), name, nameLeft, stackTop, nameCap,
                    ThreadNameStyle, ink.TitleInk, titleHovering);
                var subTop = stackTop + nameSize.Y + gapY;
                var subHovering = UiInteract.Hover(new Vector2(nameLeft, subTop),
                    new Vector2(nameLeft + nameCap, subTop + subSize.Y));
                Marquee.DrawLeft(new MarqueeId("dm.thread.subtitle.", threadId), sub, nameLeft, subTop, nameCap,
                    ThreadSubStyle, ink.MutedInk, subHovering);
                textWidth = MathF.Max(nameSize.X, subSize.X);
            }
            else
            {
                var soloTop = rowCenterY - nameSize.Y * 0.5f;
                var titleHovering = UiInteract.Hover(new Vector2(nameLeft, soloTop),
                    new Vector2(nameLeft + nameCap, soloTop + nameSize.Y));
                Marquee.DrawLeft(new MarqueeId("dm.thread.title.", threadId), name, nameLeft, soloTop, nameCap,
                    ThreadNameStyle, ink.TitleInk, titleHovering);
            }

            var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
            var hitMax = new Vector2(nameLeft + textWidth, area.Min.Y + AppHeader.Height * scale);
            if (UiInteract.HoverClick(hitMin, hitMax))
            {
                dms.host.OpenProfile(threadId);
            }

            DrawHairline(drawList, area.Min.X, area.Max.X, area.Min.Y + AppHeader.Height * scale, ink);
        }

        private int DrawHeaderToggles(ImDrawListPtr drawList, Rect area, string threadId, SocialInk ink)
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
                    ThreadToggleIconSize, lockTooltip, ink, ink.MutedInk, encrypted))
            {
                OpenEncryptionInfo(threadId);
            }

            if (DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 1), PhoneIcons.Search, Loc.T(L.Common.Search),
                    ink, searchController.Open, ThreadToggleIconSize))
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
                    Loc.T(translated ? L.Translate.ChatOn : L.Translate.ChatToggle), ink, translated,
                    ThreadToggleIconSize))
            {
                return 3;
            }

            if (translated)
            {
                translation.SetConversationTranslated(scope, false);
                return 3;
            }

            TranslateLink.WithDisclosure(translation, dms.confirm,
                () => translation.SetConversationTranslated(scope, true));
            return 3;
        }

        protected override TranscriptMessage[] MapTranscript(DmMessageDto[] source) =>
            DirectTranscript.Map(store, source, MyUserId,
                store.CurrentThreadId is { } threadId ? dms.ThreadTitle(threadId) : string.Empty);
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

    private DmThreadDto? FindThread(string threadId)
    {
        var threads = dm.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].OtherUserId == threadId)
            {
                return threads[index];
            }
        }

        return null;
    }

    private UserDto? ProfileFor(string threadId) =>
        store.ProfileUser is { } user && user.Id == threadId ? user : null;

    private string ThreadTitle(string threadId)
    {
        if (FindThread(threadId) is { } thread)
        {
            return SocialIdentity.Name(thread.OtherDisplayName, thread.OtherHandle);
        }

        return ProfileFor(threadId) is { } user ? SocialIdentity.Name(user.DisplayName, user.Handle) : string.Empty;
    }

    private string ThreadSubtitle(string threadId, int presence)
    {
        if (presence == 1)
        {
            return Loc.T(labels.ActiveNow);
        }

        var thread = FindThread(threadId);
        var user = thread is null ? ProfileFor(threadId) : null;
        if ((thread?.UtcOffsetMinutes ?? user?.UtcOffsetMinutes) is { } minutes)
        {
            return SocialTimeZone.Describe(minutes);
        }

        return HandleLabel(thread?.OtherHandle ?? user?.Handle ?? string.Empty);
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
        if (FindThread(threadId) is { } thread)
        {
            monogram = Monogram(thread.OtherDisplayName, thread.OtherHandle);
            presence = thread.Presence;
            return images.Avatar(thread.OtherAvatarUrl, drawnPixels);
        }

        presence = 0;
        if (ProfileFor(threadId) is { } user)
        {
            monogram = Monogram(user.DisplayName, user.Handle);
            return images.Avatar(user.AvatarUrl, drawnPixels);
        }

        monogram = "?";
        return AvatarHandle.Disabled;
    }

    private static string Monogram(string displayName, string handle)
    {
        var source = string.IsNullOrEmpty(displayName) ? handle : displayName;
        return source.Length > 0 ? source[..1].ToUpperInvariant() : "?";
    }
}
