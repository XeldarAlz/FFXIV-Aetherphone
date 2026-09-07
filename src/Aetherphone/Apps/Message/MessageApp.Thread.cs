using Aetherphone.Core;
using Aetherphone.Core.Message;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const byte ThreadActInfo = 0;
    private const byte ThreadActSearch = 1;
    private const byte ThreadActTranslate = 2;
    private const byte ThreadActMute = 3;
    private const byte ThreadActWallpaper = 4;
    private const byte ThreadActEncryption = 5;
    private const byte ThreadActStarred = 6;
    private const byte ThreadActDelete = 7;
    private const byte ThreadActPopout = 8;
    private const float ThreadHeaderAvatarRadius = 18f;
    private const float ThreadHeaderAvatarGap = 6f;
    private const float ThreadHeaderNameGap = 10f;

    private static readonly TextStyle ThreadNameStyle = TextStyles.Headline;
    private static readonly TextStyle ThreadSubStyle = new(0.76f, FontWeight.Regular);

    private readonly ActionSheet threadSheet = new();
    private readonly ActionSheet.Item[] threadSheetItems = new ActionSheet.Item[9];
    private readonly byte[] threadSheetActions = new byte[9];
    private int threadSheetCount;
    private string threadSheetTitle = string.Empty;
    private string? threadSheetConversationId;

    private sealed class ThreadView : MessageThreadViewBase
    {
        private readonly MessageApp app;

        public ThreadView(MessageApp app)
            : base(app.store, app.ui, app.images, app.lodestone, app.http, app.library, app.configuration,
                app.confirm, app.report, app.translation, app.wallpaperImages, app.encryptionHelp)
        {
            this.app = app;
        }

        protected override PhoneTheme Theme => app.theme;

        protected override IPhoneApp Owner => app;

        protected override INavigator Navigation => app.navigation;

        protected override Action BackAction => app.back;

        protected override MessageTheme ChatTheme => app.activeTheme;

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
                CanTranslate = true,
                IsStarred = IsStarred,
                MyReactionTo = store.MyReactionTo,
                OnReply = BeginReply,
                OnForward = id => app.router.Push(MessageRoute.Forward(id)),
                OnCopy = CopyMessage,
                OnStar = ToggleStar,
                OnEdit = BeginEdit,
                OnInfo = id =>
                {
                    app.store.RefreshThreadDetail();
                    app.router.Push(MessageRoute.MessageInfo(id));
                },
                OnDelete = AskDeleteMessage,
                OnReport = OpenReportMessage,
                OnTranslate = TranslateMessage,
                OnReact = store.SetReaction,
            };
        }

        private void ToggleStar(string messageId)
        {
            app.ToggleStar(messageId);
            InvalidateTranscript();
        }

        protected override void OpenImageView(string messageId) => app.router.Push(MessageRoute.ImageView(messageId));

        protected override void OpenReactions(string messageId) => app.router.Push(MessageRoute.Reactions(messageId));

        protected override void PushImagePickerScreen(string threadId) => app.router.Push(MessageRoute.ChatImage(threadId));

        protected override void PopScreen() => app.router.Pop();

        protected override void OpenEncryptionInfo(string threadId)
        {
            var conversation = app.store.Conversation;
            if (conversation is not null)
            {
                app.router.Push(MessageRoute.Encryption(conversation.Id));
            }
        }

        protected override void DrawHeader(Rect area, string threadId)
        {
            var conversation = app.store.Conversation;
            var isGroup = conversation?.IsGroup ?? false;
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var header = app.PaintHeaderBand(area);
            var rowCenterY = header.Center.Y;
            var chipRadius = SocialChrome.BackChipRadius * scale;
            var chipCenter = new Vector2(area.Min.X + 12f * scale + chipRadius, rowCenterY);
            if (SocialChrome.DrawBackChip(drawList, chipCenter, chipRadius, app.ink))
            {
                BackAction();
            }

            var slots = 1;
            var callable = !isGroup && conversation is not null && app.calls.Enabled
                && app.contacts.Find(conversation.OtherUserId) is { IsMutual: true };
            if (callable)
            {
                slots = 2;
            }

            if (app.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.DotsVertical,
                    Loc.T(L.Message.MoreOptions)) && conversation is not null)
            {
                app.OpenThreadSheet(conversation);
            }

            if (callable && app.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 1), PhoneIcons.Phone,
                    Loc.T(L.Friends.Call)) && conversation is not null
                && app.contacts.Find(conversation.OtherUserId) is { } callTarget)
            {
                app.StartCall(callTarget);
            }

            var avatarRadius = ThreadHeaderAvatarRadius * scale;
            var avatarCenter = new Vector2(chipCenter.X + chipRadius + ThreadHeaderAvatarGap * scale + avatarRadius,
                rowCenterY);
            var name = conversation is null ? app.DisplayName : DirectMessagesStore.DisplayTitle(conversation);
            if (conversation is null)
            {
                app.DrawGroupAvatar(drawList, avatarCenter, avatarRadius, name, null);
            }
            else
            {
                app.DrawConversationAvatar(drawList, conversation, avatarCenter, avatarRadius);
            }

            var nameLeft = avatarCenter.X + avatarRadius + ThreadHeaderNameGap * scale;
            var nameRight = area.Max.X - (CellPadX + SocialChrome.HeaderReserve(slots)) * scale;
            var nameWidth = MathF.Max(1f, nameRight - nameLeft);
            var subtitle = conversation is null
                ? string.Empty
                : isGroup ? GroupSubtitle(conversation) : PresenceText(conversation);
            var subtitleInk = !isGroup && conversation is { Presence: 1 } ? app.ink.AccentLink : app.ink.MutedInk;
            var titleId = "messageapp.thread.title." + (conversation?.Id ?? "self");
            var nameHeight = Typography.LineHeight(ThreadNameStyle);
            if (subtitle.Length == 0)
            {
                var soloTop = rowCenterY - nameHeight * 0.5f;
                var soloHovering = UiInteract.Hover(new Vector2(nameLeft, soloTop),
                    new Vector2(nameRight, soloTop + nameHeight));
                Marquee.DrawLeft(drawList, titleId, name, nameLeft, soloTop, nameWidth, ThreadNameStyle,
                    app.ink.TitleInk, soloHovering);
            }
            else
            {
                var subHeight = Typography.LineHeight(ThreadSubStyle);
                var top = rowCenterY - (nameHeight + subHeight) * 0.5f;
                var hovering = UiInteract.Hover(new Vector2(nameLeft, top), new Vector2(nameRight, top + nameHeight));
                Marquee.DrawLeft(drawList, titleId, name, nameLeft, top, nameWidth, ThreadNameStyle, app.ink.TitleInk,
                    hovering);
                Typography.Draw(drawList, new Vector2(nameLeft, top + nameHeight),
                    Typography.FitText(subtitle, nameWidth, ThreadSubStyle), subtitleInk, ThreadSubStyle);
            }

            if (conversation is null)
            {
                return;
            }

            var hitMin = new Vector2(avatarCenter.X - avatarRadius, header.Min.Y);
            var hitMax = new Vector2(nameRight, header.Max.Y);
            if (!UiInteract.HoverClick(hitMin, hitMax))
            {
                return;
            }

            if (isGroup)
            {
                app.store.RefreshThreadDetail();
                app.router.Push(MessageRoute.GroupInfo(conversation.Id));
            }
            else if (app.contacts.Find(conversation.OtherUserId) is not null)
            {
                app.router.Push(MessageRoute.Contact(conversation.OtherUserId));
            }
        }
    }

    private Rect PaintHeaderBand(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var band = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale));
        ui.PaintGradient(drawList, band, screenRect, 0f);
        drawList.AddLine(new Vector2(band.Min.X, band.Max.Y), band.Max, ImGui.GetColorU32(ui.Hairline), 1f);
        return band;
    }

    private void OpenThreadSheet(ConversationDto conversation)
    {
        threadSheetConversationId = conversation.Id;
        threadSheetTitle = DirectMessagesStore.DisplayTitle(conversation);
        var count = 0;
        var isGroup = conversation.IsGroup;
        if (isGroup || contacts.Find(conversation.OtherUserId) is not null)
        {
            threadSheetItems[count] = new ActionSheet.Item(Loc.T(isGroup ? L.Message.GroupInfo : L.Message.ContactInfo),
                isGroup ? PhoneIcons.Users : PhoneIcons.UserCircle);
            threadSheetActions[count++] = ThreadActInfo;
        }

        threadSheetItems[count] = new ActionSheet.Item(Loc.T(L.Common.Search), PhoneIcons.Search,
            Selected: threadView.SearchOpen);
        threadSheetActions[count++] = ThreadActSearch;
        if (threadView.CanTranslate)
        {
            var translating = threadView.TranslatingThread(conversation.Id);
            threadSheetItems[count] = new ActionSheet.Item(
                Loc.T(translating ? L.Translate.ChatOn : L.Translate.ChatToggle), PhoneIcons.Language,
                Selected: translating);
            threadSheetActions[count++] = ThreadActTranslate;
        }

        threadSheetItems[count] = new ActionSheet.Item(
            Loc.T(conversation.Muted ? L.Message.UnmuteAction : L.Message.MuteAction),
            conversation.Muted ? PhoneIcons.Bell : PhoneIcons.BellOff);
        threadSheetActions[count++] = ThreadActMute;
        threadSheetItems[count] = new ActionSheet.Item(Loc.T(L.Message.Wallpaper), PhoneIcons.Wallpaper);
        threadSheetActions[count++] = ThreadActWallpaper;
        threadSheetItems[count] = new ActionSheet.Item(
            Loc.T(popouts.IsOpen(conversation.Id) ? L.Message.ClosePopout : L.Message.PopoutChat),
            PhoneIcons.ExternalLink, Selected: popouts.IsOpen(conversation.Id));
        threadSheetActions[count++] = ThreadActPopout;
        threadSheetItems[count] = new ActionSheet.Item(Loc.T(L.Encryption.InfoTitle),
            store.EncryptingCurrent ? PhoneIcons.Lock : PhoneIcons.LockOpen);
        threadSheetActions[count++] = ThreadActEncryption;
        if (StarredCountIn(conversation.Id) > 0)
        {
            threadSheetItems[count] = new ActionSheet.Item(Loc.T(L.Message.StarredTitle), PhoneIcons.Star);
            threadSheetActions[count++] = ThreadActStarred;
        }

        threadSheetItems[count] = new ActionSheet.Item(Loc.T(L.Message.DeleteConversation), PhoneIcons.Trash, true);
        threadSheetActions[count++] = ThreadActDelete;
        threadSheetCount = count;
        threadSheet.Open();
    }

    private void DrawThreadSheet(Rect screen)
    {
        if (!threadSheet.CapturesPointer)
        {
            return;
        }

        var picked = threadSheet.Draw(screen, ActionSheetStyle.From(ui), threadSheetItems.AsSpan(0, threadSheetCount),
            Loc.T(L.Common.Cancel), false, threadSheetTitle);
        if (picked < 0 || threadSheetConversationId is not { } conversationId)
        {
            return;
        }

        var conversation = store.Conversation;
        switch (threadSheetActions[picked])
        {
            case ThreadActInfo:
                if (conversation is { IsGroup: true })
                {
                    store.RefreshThreadDetail();
                    router.Push(MessageRoute.GroupInfo(conversationId));
                }
                else if (conversation is not null)
                {
                    router.Push(MessageRoute.Contact(conversation.OtherUserId));
                }

                break;
            case ThreadActSearch:
                threadView.ToggleSearch();
                break;
            case ThreadActTranslate:
                threadView.ToggleTranslation(conversationId);
                break;
            case ThreadActMute:
                store.SetMuted(conversationId, !(conversation?.Muted ?? false), _ => { });
                break;
            case ThreadActWallpaper:
                router.Push(MessageRoute.ChatWallpaper(conversationId));
                break;
            case ThreadActEncryption:
                router.Push(MessageRoute.Encryption(conversationId));
                break;
            case ThreadActStarred:
                router.Push(MessageRoute.StarredIn(conversationId));
                break;
            case ThreadActDelete:
                AskDeleteConversation(conversationId);
                break;
            case ThreadActPopout:
                TogglePopout(conversationId, conversation);
                break;
        }
    }

    private void TogglePopout(string conversationId, ConversationDto? conversation)
    {
        if (popouts.IsOpen(conversationId))
        {
            popouts.Close(conversationId);
            return;
        }

        var title = conversation is null ? DisplayName : DirectMessagesStore.DisplayTitle(conversation);
        if (!popouts.Open(conversationId, title))
        {
            ShellToast.Show(Loc.T(L.Message.PopoutLimit, MessagePopouts.MaxWindows));
            return;
        }

        router.Pop();
    }

    private int StarredCountIn(string conversationId)
    {
        var starred = configuration.MessageStarredMessages;
        var count = 0;
        for (var index = 0; index < starred.Count; index++)
        {
            if (starred[index].ConversationId == conversationId)
            {
                count++;
            }
        }

        return count;
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
}
