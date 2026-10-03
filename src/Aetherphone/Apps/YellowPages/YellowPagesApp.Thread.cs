using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp
{
    private const float ThreadPollSeconds = 2.5f;
    private const float TypingSendSeconds = 3f;
    private const float ThreadBackInset = 12f;
    private const float ThreadAvatarRadius = 19f;
    private const float ThreadAvatarGap = 6f;
    private const float ThreadNameGap = 9f;
    private const float ThreadToggleIconSize = 21f;
    private const float AdStripHeight = 54f;
    private const float AdStripThumb = 34f;
    private const float NewInquiryComposerHeight = 56f;
    private const int InquiryBodyMax = 1000;
    private const int InquiryMaxLines = 6;

    private static readonly TextStyle ThreadNameStyle = TextStyles.Headline;
    private static readonly TextStyle ThreadSubStyle = TextStyles.Caption1;

    private readonly SoftWrapEditor inquiryEditor = new();
    private bool inquiryBusy;
    private bool inquirySendFailed;

    private string ThreadTitle(string threadId)
    {
        var thread = inquiries.Thread(threadId);
        return thread is null ? Loc.T(L.YellowPages.InquiriesTitle) : SocialIdentity.Name(thread.OtherName, thread.OtherHandle);
    }

    private string ThreadSubtitle(string threadId)
    {
        var thread = inquiries.Thread(threadId);
        if (thread is null)
        {
            return string.Empty;
        }

        return thread.AdTitle.Length > 0 ? thread.AdTitle : Loc.T(L.YellowPages.UnavailableTitle);
    }

    private void OpenInquiryFor(AdDto ad)
    {
        var existing = inquiries.ThreadForAd(ad.Id);
        if (existing is not null)
        {
            OpenThread(existing.Id);
            return;
        }

        if (!ad.AllowInquiries)
        {
            return;
        }

        inquiryEditor.Adopt(string.Empty);
        inquirySendFailed = false;
        router.Push(YellowPagesRoute.NewInquiry(ad.Id));
    }

    private void DrawNewInquiry(Rect area, string adId)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var ad = ResolveAd(adId);
        DrawScreenHeader(area, Loc.T(L.YellowPages.InquireAction));
        DrawHairline(drawList, area.Min.X, area.Max.X, area.Min.Y + AppHeader.Height * scale);
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerTop = area.Max.Y - NewInquiryComposerHeight * scale - inquiryEditor.Growth(InquiryMaxLines);
        var body = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, composerTop));
        if (ad is null)
        {
            DrawEmptyState(body, Loc.T(L.YellowPages.UnavailableTitle), Loc.T(L.YellowPages.UnavailableHint));
            return;
        }

        if (!ad.AllowInquiries)
        {
            DrawEmptyState(body, Loc.T(L.YellowPages.InquiriesClosed), Loc.T(L.YellowPages.InquiriesClosedHint));
            return;
        }

        using (AppSurface.BeginEdgeToEdge(body))
        {
            AdCard.Draw(ad, new AdCardContext(images, NowUnix(), false));
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var pad = CellPadX * scale;
            var hintHeight = Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + Metrics.Space.Md * scale),
                Loc.T(L.YellowPages.InquiryConsentHint), Ink.MutedInk, TextStyles.Footnote, width - pad * 2f);
            var cursorY = origin.Y + Metrics.Space.Md * scale + hintHeight;
            if (inquirySendFailed)
            {
                cursorY += Metrics.Space.Sm * scale;
                cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X + pad, cursorY),
                    Loc.T(L.YellowPages.InquirySendFailed), Ink.Danger, TextStyles.FootnoteEmphasized, width - pad * 2f);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, cursorY - origin.Y + Metrics.Space.Lg * scale));
        }

        DrawNewInquiryComposer(new Rect(new Vector2(area.Min.X, composerTop), area.Max), adId, ad.OwnerId, scale);
    }

    private void DrawNewInquiryComposer(Rect bar, string adId, string ownerId, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        PaintBarBackdrop(drawList, bar);
        DrawHairline(drawList, bar.Min.X, bar.Max.X, bar.Min.Y + 1f);
        var inset = CellPadX * scale;
        var sendSide = 36f * scale;
        var fieldRect = new Rect(new Vector2(bar.Min.X + inset, bar.Min.Y + 10f * scale),
            new Vector2(bar.Max.X - inset - sendSide - 8f * scale, bar.Max.Y - 10f * scale));
        if (!inquiries.EncryptingCurrent && inquiries.VaultState != KeyVaultState.Unlocked)
        {
            var hint = Typography.FitText(Loc.T(L.YellowPages.InquiryLocked), fieldRect.Width, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(fieldRect.Min.X, fieldRect.Center.Y - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
                hint, Ink.MutedInk, TextStyles.Footnote);
            if (UiInteract.HoverClick(fieldRect.Min, fieldRect.Max))
            {
                router.Push(YellowPagesRoute.Encryption);
            }

            return;
        }

        var submitted = SubmitField.Multiline(fieldRect, "##adInquiryNew", Loc.T(L.YellowPages.InquiryHint), inquiryEditor,
            theme, InquiryBodyMax, InquiryMaxLines);
        var sendCenter = new Vector2(bar.Max.X - inset - sendSide * 0.5f, fieldRect.Max.Y - sendSide * 0.5f);
        if (inquiryBusy)
        {
            LoadingPulse.Spinner(sendCenter, 9f * scale, Ink.Accent);
            return;
        }

        var canSend = TrimmedLength(inquiryEditor.Text) > 0;
        var sendExtent = new Vector2(sendSide * 0.5f, sendSide * 0.5f);
        var sendHovered = canSend && UiInteract.Hover(sendCenter - sendExtent, sendCenter + sendExtent);
        if (canSend)
        {
            AccentGloss.Circle(drawList, sendCenter, sendSide * 0.5f, Palette.Lighten(Ink.Accent, 0.18f), Ink.AccentDeep,
                scale, sendHovered ? 1f : 0.4f, 0f);
        }
        else
        {
            drawList.AddCircleFilled(sendCenter, sendSide * 0.5f, ImGui.GetColorU32(Ink.FieldFill), 32);
        }

        PhoneIcon.Draw(drawList, sendCenter, PhoneIcons.SendFilled, canSend ? Ink.White : Ink.FaintInk, 18f * scale);
        if (sendHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tapped = UiInteract.Click(sendCenter - sendExtent, sendCenter + sendExtent, sendHovered);
        if (!canSend || (!tapped && !submitted))
        {
            return;
        }

        var text = inquiryEditor.Text.Trim();
        inquiryEditor.Adopt(string.Empty);
        inquiryBusy = true;
        inquirySendFailed = false;
        inquiries.OpenForAd(adId, ownerId, text, thread =>
        {
            inquiryBusy = false;
            if (thread is null)
            {
                inquiryEditor.Adopt(text);
                inquirySendFailed = true;
                return;
            }

            router.Pop(false);
            OpenThread(thread.Id);
        });
    }

    private sealed class ThreadView : ChatThreadView<AdInquiryMessageDto, AdInquiryDto>
    {
        private readonly YellowPagesApp app;

        public ThreadView(YellowPagesApp app)
            : base(app.inquiries, app.ui, app.images, app.lodestone, app.http, app.library, app.configuration,
                app.confirm, app.report, app.translation, app.wallpaperImages, app.encryptionHelp, ThreadPollSeconds,
                TypingSendSeconds)
        {
            this.app = app;
        }

        protected override PhoneTheme Theme => app.theme;
        protected override IPhoneApp Owner => app;
        protected override INavigator Navigation => app.navigation;
        protected override Action BackAction => app.back;
        protected override string MyUserId => app.inquiries.MyUserId;
        protected override Vector4 Accent => app.Accent;
        protected override string EmptyText => Loc.T(L.YellowPages.ThreadEmpty);
        protected override string LogTag => "YellowPages";
        protected override string PickerTitle => Loc.T(L.Common.SendPhoto);
        protected override string ImportLabel => Loc.T(L.Common.ImportFromPc);
        protected override string NoPhotosLabel => Loc.T(L.Common.NoPhotos);
        protected override string SaveLabel => Loc.T(L.Common.SaveToGallery);
        protected override string SavedLabel => Loc.T(L.Common.SavedToGallery);

        protected override ChatComposerStyle ComposerStyle => ChatComposerStyle.Pill;

        protected override string ComposerHint => Loc.T(L.YellowPages.InquiryHint);

        protected override bool IsDeleted(AdInquiryMessageDto message) => message.Deleted;

        protected override string SenderIdOf(AdInquiryMessageDto message) => message.SenderId;

        protected override int KindOf(AdInquiryMessageDto message) => message.Kind;

        protected override string? BodyOf(AdInquiryMessageDto message) => message.Body;

        protected override int EncVersionOf(AdInquiryMessageDto message) => message.EncVersion;

        protected override byte[]? DecryptSealed(AdInquiryMessageDto message, string? threadId, byte[] sealedBytes) =>
            threadId is null ? null : app.inquiries.DecryptMedia(message, sealedBytes, threadId);

        protected override void OpenImageView(string messageId) =>
            app.router.Push(YellowPagesRoute.ImageView(messageId));

        protected override void OpenReactions(string messageId) =>
            app.router.Push(YellowPagesRoute.Reactions(messageId));

        protected override void PushImagePickerScreen(string threadId) =>
            app.router.Push(YellowPagesRoute.ChatImage(threadId));

        protected override void PopScreen() => app.router.Pop();

        protected override void OpenEncryptionInfo(string threadId) => app.router.Push(YellowPagesRoute.Encryption);

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
            var thread = app.inquiries.Thread(threadId);
            if (thread is null)
            {
                return;
            }

            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var height = AdStripHeight * scale;
            var stripMax = new Vector2(listRect.Max.X, listRect.Min.Y + height);
            var hovered = thread.AdTitle.Length > 0 && UiInteract.Hover(listRect.Min, stripMax);
            drawList.AddRectFilled(listRect.Min, stripMax, ImGui.GetColorU32(hovered ? Ink.ChipHover : Ink.AccentWash));
            var pad = CellPadX * scale;
            var thumbSide = AdStripThumb * scale;
            var thumbMin = new Vector2(listRect.Min.X + pad, listRect.Min.Y + (height - thumbSide) * 0.5f);
            var thumbMax = thumbMin + new Vector2(thumbSide, thumbSide);
            var texture = string.IsNullOrEmpty(thread.AdMediaUrl) ? null : app.images.Get(thread.AdMediaUrl);
            if (texture is not null)
            {
                var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                Squircle.FillImage(drawList, thumbMin, thumbMax, 9f * scale, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
            }
            else
            {
                YellowPagesKit.Tile(drawList, thumbMin, thumbMax, Ink.Accent,
                    thread.AdTitle.Length == 0 ? FontAwesomeIcon.Ban : AdCategories.Icon(thread.AdCategory), 9f * scale, 0.8f);
            }

            var textLeft = thumbMax.X + 10f * scale;
            var textRight = listRect.Max.X - pad - 22f * scale;
            var title = thread.AdTitle.Length > 0 ? thread.AdTitle : Loc.T(L.YellowPages.UnavailableTitle);
            var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
            var subHeight = Typography.LineHeight(TextStyles.Caption1);
            var blockTop = listRect.Min.Y + (height - titleHeight - subHeight - 2f * scale) * 0.5f;
            Typography.Draw(drawList, new Vector2(textLeft, blockTop),
                Typography.FitText(title, textRight - textLeft, TextStyles.SubheadlineEmphasized), Ink.TitleInk,
                TextStyles.SubheadlineEmphasized);
            Typography.Draw(drawList, new Vector2(textLeft, blockTop + titleHeight + 2f * scale),
                Typography.FitText(Loc.T(thread.Mine ? L.YellowPages.ThreadAboutMine : L.YellowPages.ThreadAboutTheirs),
                    textRight - textLeft, TextStyles.Caption1), Ink.MutedInk, TextStyles.Caption1);
            if (thread.AdTitle.Length > 0)
            {
                PhoneIcon.Draw(drawList, new Vector2(listRect.Max.X - pad - 8f * scale, listRect.Min.Y + height * 0.5f),
                    PhoneIcons.ChevronRight, Ink.MutedInk, 16f * scale);
                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                if (UiInteract.Click(listRect.Min, stripMax, hovered))
                {
                    app.OpenDetail(thread.AdId);
                }
            }

            DrawHairline(drawList, listRect.Min.X, listRect.Max.X, stripMax.Y);
            listRect = new Rect(new Vector2(listRect.Min.X, stripMax.Y), listRect.Max);
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
            var thread = app.inquiries.Thread(threadId);
            var name = app.ThreadTitle(threadId);
            var avatarRadius = ThreadAvatarRadius * scale;
            var avatarCenter = new Vector2(chipCenter.X + chipRadius + ThreadAvatarGap * scale + avatarRadius, rowCenterY);
            var monogram = thread is null ? string.Empty : YellowPagesKit.Monogram(thread.OtherName, thread.OtherHandle);
            AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, monogram, 0.95f,
                app.images.Avatar(thread is null || thread.OtherAvatarUrl.Length == 0 ? null : thread.OtherAvatarUrl,
                    avatarRadius * 2f), 32);
            if (thread is not null)
            {
                var dotInset = avatarRadius * 0.72f;
                YellowPagesKit.PresenceDot(drawList, new Vector2(avatarCenter.X + dotInset, avatarCenter.Y + dotInset),
                    thread.Presence, scale);
            }

            var nameLeft = avatarCenter.X + avatarRadius + ThreadNameGap * scale;
            var nameRight = SocialChrome.HeaderSlot(area, slots - 1).X - SocialChrome.HeaderIconRadius * scale
                - ThreadNameGap * scale;
            var nameCap = MathF.Max(1f, nameRight - nameLeft);
            var nameSize = Typography.Measure(name, ThreadNameStyle);
            nameSize.X = MathF.Min(nameSize.X, nameCap);
            var titleId = "yellowpages.thread.title." + threadId;
            var sub = app.ThreadSubtitle(threadId);
            if (sub.Length > 0)
            {
                var subSize = Typography.Measure(sub, ThreadSubStyle);
                subSize.X = MathF.Min(subSize.X, nameCap);
                var gapY = 1f * scale;
                var stackTop = rowCenterY - (nameSize.Y + gapY + subSize.Y) * 0.5f;
                Marquee.DrawLeftAuto(drawList, new MarqueeId(titleId, string.Empty), name, nameLeft, stackTop, nameCap,
                    ThreadNameStyle, Ink.TitleInk);
                Marquee.DrawLeftAuto(drawList, new MarqueeId(titleId, ".sub"), sub, nameLeft, stackTop + nameSize.Y + gapY,
                    nameCap, ThreadSubStyle, Ink.MutedInk);
            }
            else
            {
                Marquee.DrawLeftAuto(drawList, new MarqueeId(titleId, string.Empty), name, nameLeft,
                    rowCenterY - nameSize.Y * 0.5f, nameCap, ThreadNameStyle, Ink.TitleInk);
            }

            DrawHairline(drawList, area.Min.X, area.Max.X, area.Min.Y + AppHeader.Height * scale);
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
                    ThreadToggleIconSize, lockTooltip, Ink, Ink.MutedInk, encrypted))
            {
                OpenEncryptionInfo(threadId);
            }

            if (YellowPagesApp.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 1), PhoneIcons.Search,
                    Loc.T(L.Common.Search), searchController.Open, 0, ThreadToggleIconSize))
            {
                searchController.Toggle();
            }

            if (!translation.Enabled)
            {
                return 2;
            }

            var scope = ConversationScope(threadId);
            var translated = translation.IsConversationTranslated(scope);
            if (!YellowPagesApp.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 2), PhoneIcons.Language,
                    Loc.T(translated ? L.Translate.ChatOn : L.Translate.ChatToggle), translated, 0, ThreadToggleIconSize))
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

        protected override TranscriptMessage[] MapTranscript(AdInquiryMessageDto[] source)
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
                    message.CreatedAtUnix, message.MediaWidth, message.MediaHeight,
                    message.ReadAtUnix > 0 ? message.ReadAtUnix : null, string.Empty, default, MessageFlags(message),
                    message.ReplyToId, replySender, replyBody, replyKind, message.DurationSecs, reactions);
            }

            return mapped;
        }

        private byte MessageFlags(AdInquiryMessageDto message)
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
    }
}
