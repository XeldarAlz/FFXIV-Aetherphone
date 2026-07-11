using System.Collections.Concurrent;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Report;
using Aetherphone.Core.Telephony.Audio;
using Aetherphone.Core.Theme;
using Aetherphone.Apps.DirectMessages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private readonly DropdownMenu messageMenu = new();
    private readonly DropdownMenu.Item[] messageMenuItems = new DropdownMenu.Item[5];
    private string? menuMessageId;
    private bool menuMessageMine;
    private Vector2 menuAnchor;
    private Action<string>? onMessageContext;
    private Action<string>? onQuoteClick;
    private Action<string, string>? onReactionClick;
    private Func<string, VoiceNoteState>? voiceStateFor;
    private Action<string>? onVoiceToggle;
    private string messageDraft = string.Empty;
    private bool threadFocus;
    private float sinceThreadPoll;
    private float sinceTypingSend = TypingSendSeconds;
    private string lastTypingDraft = string.Empty;
    private ChatMessageDto[] transcriptSource = Array.Empty<ChatMessageDto>();
    private TranscriptMessage[] transcriptCache = Array.Empty<TranscriptMessage>();
    private Func<string, string?>? threadMediaUrl;
    private Action<string>? onThreadImageClick;
    private string? replyTargetId;
    private string replyBarName = string.Empty;
    private string replyBarPreview = string.Empty;
    private readonly VoiceNoteRecorder voiceRecorder = new();
    private readonly VoiceNotePlayer voicePlayer = new();
    private readonly ConcurrentDictionary<string, byte[]> voiceBytes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> voiceFetching = new(StringComparer.Ordinal);
    private volatile string? pendingVoicePlay;
    private bool searchOpen;
    private string searchQuery = string.Empty;
    private bool searchFocus;
    private readonly List<string> searchMatches = new();
    private int searchIndex;
    private ChatMessageDto[] searchSource = Array.Empty<ChatMessageDto>();
    private string searchLastQuery = string.Empty;

    private void DrawThread(Rect area, string conversationId)
    {
        if (store.ConversationId != conversationId)
        {
            store.OpenConversation(conversationId);
            sinceThreadPoll = ThreadPollSeconds;
            lastTypingDraft = string.Empty;
            ClearReplyTarget();
            CloseSearch();
            voiceRecorder.Cancel();
            voicePlayer.Stop();
        }

        store.NoteConversationViewed(conversationId);
        TickThread(conversationId);
        var conversation = store.Conversation;
        var isGroup = conversation?.IsGroup ?? false;
        DrawThreadHeader(area, conversation, isGroup);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerHeight = 52f * scale;
        var replyBarHeight = replyTargetId is not null ? 46f * scale : 0f;
        if (searchOpen)
        {
            var searchHeight = 44f * scale;
            DrawSearchBar(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)));
            top += searchHeight;
        }

        var listRect = new Rect(new Vector2(area.Min.X, top),
            new Vector2(area.Max.X, area.Max.Y - composerHeight - replyBarHeight));
        DrawEncryptionBanner(ref listRect, conversation, isGroup);
        var transcriptMessages = BuildTranscript(store.Messages, isGroup);
        threadMediaUrl ??= store.DmMediaUrl;
        onThreadImageClick ??= id => router.Push(MessageRoute.ImageView(id));
        onMessageContext ??= OpenMessageMenu;
        onQuoteClick ??= transcript.RequestScrollTo;
        onReactionClick ??= store.SetReaction;
        voiceStateFor ??= voicePlayer.StateFor;
        onVoiceToggle ??= ToggleVoice;
        var model = new ChatTranscriptModel(conversationId, transcriptMessages, store.MyUserId, ui.Accent, theme,
            AppPalettes.Message.MutedInk, AppPalettes.Message.BodyInk, store.OtherTyping, store.LoadingThread,
            isGroup, images, threadMediaUrl, onThreadImageClick, Loc.T(L.Velvet.ThreadEmpty), Loc.T(L.Common.Loading),
            onMessageContext, onQuoteClick, onReactionClick, voiceStateFor, onVoiceToggle);
        transcript.Draw(listRect, model);
        if (replyBarHeight > 0f)
        {
            DrawReplyBar(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight - replyBarHeight),
                new Vector2(area.Max.X, area.Max.Y - composerHeight)));
        }

        DrawMessageComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), conversationId);
        DrawMessageMenu(area);
    }

    private void OpenMessageMenu(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || message.Deleted)
        {
            return;
        }

        menuMessageId = messageId;
        menuMessageMine = message.SenderId == store.MyUserId;
        menuAnchor = ImGui.GetMousePos();
        messageMenu.Toggle(messageId, new Rect(menuAnchor, menuAnchor + new Vector2(1f, 1f)));
    }

    private void DrawMessageMenu(Rect area)
    {
        if (menuMessageId is not { } id || !messageMenu.IsOpenFor(id))
        {
            return;
        }

        DrawReactionStrip(area, id);
        var count = 0;
        messageMenuItems[count++] = new DropdownMenu.Item(Loc.T(L.Message.ReplyAction),
            FontAwesomeIcon.Reply.ToIconString());
        messageMenuItems[count++] = new DropdownMenu.Item(Loc.T(L.Message.ForwardAction),
            FontAwesomeIcon.Share.ToIconString());
        messageMenuItems[count++] = new DropdownMenu.Item(Loc.T(L.Encryption.CopyTextAction),
            FontAwesomeIcon.Copy.ToIconString());
        if (menuMessageMine)
        {
            messageMenuItems[count++] = new DropdownMenu.Item(Loc.T(L.Message.InfoAction),
                FontAwesomeIcon.InfoCircle.ToIconString());
            messageMenuItems[count++] = new DropdownMenu.Item(Loc.T(L.Message.DeleteAction),
                FontAwesomeIcon.TrashAlt.ToIconString(), Danger: true);
        }
        else
        {
            messageMenuItems[count++] = new DropdownMenu.Item(Loc.T(L.Encryption.ReportMessageAction),
                FontAwesomeIcon.Flag.ToIconString(), Danger: true);
        }

        var clicked = messageMenu.Draw(area, theme, messageMenuItems.AsSpan(0, count));
        switch (clicked)
        {
            case 0:
                BeginReply(id);
                break;
            case 1:
                router.Push(MessageRoute.Forward(id));
                break;
            case 2:
                CopyMessageText(id);
                break;
            case 3 when menuMessageMine:
                store.RefreshDetail();
                router.Push(MessageRoute.MessageInfo(id));
                break;
            case 3:
                OpenReportMessage(id);
                break;
            case 4:
                AskDeleteMessage(id);
                break;
        }
    }

    private void DrawReactionStrip(Rect area, string messageId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetForegroundDrawList();
        var slot = 34f * scale;
        var padding = 7f * scale;
        var width = ReactionArt.Tokens.Length * slot + padding * 2f;
        var height = 38f * scale;
        var left = Math.Clamp(menuAnchor.X - width * 0.5f, area.Min.X + 8f * scale,
            MathF.Max(area.Min.X + 8f * scale, area.Max.X - 8f * scale - width));
        var top = menuAnchor.Y - height - 10f * scale;
        if (top < area.Min.Y + 8f * scale)
        {
            top = area.Min.Y + 8f * scale;
        }

        var min = new Vector2(left, top);
        var max = min + new Vector2(width, height);
        Elevation.Floating(drawList, min, max, height * 0.5f, scale);
        Squircle.Fill(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, MathF.Min(0.98f, theme.GroupedCard.W + 0.4f))));
        Material.EdgeSquircle(drawList, min, max, height * 0.5f, scale);
        var myReaction = store.MyReactionTo(messageId);
        for (var index = 0; index < ReactionArt.Tokens.Length; index++)
        {
            var token = ReactionArt.Tokens[index];
            var center = new Vector2(min.X + padding + slot * (index + 0.5f), (min.Y + max.Y) * 0.5f);
            var hitMin = new Vector2(center.X - slot * 0.5f, min.Y);
            var hitMax = new Vector2(center.X + slot * 0.5f, max.Y);
            var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
            if (token == myReaction)
            {
                drawList.AddCircleFilled(center, 14f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.25f)), 24);
            }
            else if (hovered)
            {
                drawList.AddCircleFilled(center, 14f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f)), 24);
            }

            var color = ReactionArt.Color(token);
            AppSkin.Icon(drawList, center, ReactionArt.Glyph(token), color, hovered ? 1.08f : 0.95f);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    store.SetReaction(messageId, token == myReaction ? string.Empty : token);
                    messageMenu.Close();
                }
            }
        }
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

    private ChatMessageDto? FindMessage(string messageId)
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

    private void BeginReply(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || message.Kind == 2)
        {
            return;
        }

        replyTargetId = messageId;
        replyBarName = message.SenderId == store.MyUserId
            ? Loc.T(L.Message.You)
            : message.SenderDisplayName;
        replyBarPreview = QuotePreview(message.Body, message.Kind);
        threadFocus = true;
    }

    private void ClearReplyTarget()
    {
        replyTargetId = null;
        replyBarName = string.Empty;
        replyBarPreview = string.Empty;
    }

    private static string QuotePreview(string? body, int kind)
    {
        var text = body ?? string.Empty;
        if (kind == 3)
        {
            return Loc.T(L.DirectMessages.VoicePreview);
        }

        if (kind == 1 && text.Length == 0)
        {
            return Loc.T(L.DirectMessages.PhotoPreview);
        }

        return UiText.Truncate(text.Replace('\n', ' ').Replace('\r', ' '), 90);
    }

    private void CloseSearch()
    {
        searchOpen = false;
        searchQuery = string.Empty;
        searchLastQuery = string.Empty;
        searchMatches.Clear();
        searchIndex = 0;
    }

    private void DrawSearchBar(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(new Vector2(area.Min.X, area.Max.Y), area.Max, ImGui.GetColorU32(theme.Separator), 1f);
        var fieldHeight = 32f * scale;
        var controlsWidth = 136f * scale;
        var fieldMin = new Vector2(area.Min.X + 14f * scale, area.Center.Y - fieldHeight * 0.5f);
        var fieldMax = new Vector2(area.Max.X - controlsWidth, area.Center.Y + fieldHeight * 0.5f);
        Squircle.Fill(drawList, fieldMin, fieldMax, fieldHeight * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 12f * scale,
            area.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldMax.X - fieldMin.X - 20f * scale);
        if (searchFocus)
        {
            ImGui.SetKeyboardFocusHere();
            searchFocus = false;
        }

        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.InputTextWithHint("##threadSearch", Loc.T(L.Common.Search), ref searchQuery, 80);
        }

        SyncSearch();
        var countText = searchMatches.Count > 0
            ? string.Concat((searchIndex + 1).ToString(Loc.Culture), "/", searchMatches.Count.ToString(Loc.Culture))
            : searchLastQuery.Length > 0 ? "0/0" : string.Empty;
        var buttonRadius = 12f * scale;
        var upCenter = new Vector2(area.Max.X - 66f * scale, area.Center.Y);
        var downCenter = new Vector2(area.Max.X - 42f * scale, area.Center.Y);
        var closeCenter = new Vector2(area.Max.X - 18f * scale, area.Center.Y);
        var countSize = Typography.Measure(countText, TextStyles.Footnote);
        Typography.Draw(new Vector2(upCenter.X - buttonRadius - 4f * scale - countSize.X,
            area.Center.Y - countSize.Y * 0.5f), countText, ui.MutedInk, TextStyles.Footnote);
        var hasMatches = searchMatches.Count > 0;
        if (ui.IconButton(upCenter, buttonRadius, FontAwesomeIcon.ChevronUp.ToIconString(),
                hasMatches ? ui.BodyInk : ui.MutedInk, Transparent, 0.85f) && hasMatches)
        {
            searchIndex = (searchIndex - 1 + searchMatches.Count) % searchMatches.Count;
            transcript.RequestScrollTo(searchMatches[searchIndex]);
        }

        if (ui.IconButton(downCenter, buttonRadius, FontAwesomeIcon.ChevronDown.ToIconString(),
                hasMatches ? ui.BodyInk : ui.MutedInk, Transparent, 0.85f) && hasMatches)
        {
            searchIndex = (searchIndex + 1) % searchMatches.Count;
            transcript.RequestScrollTo(searchMatches[searchIndex]);
        }

        if (ui.IconButton(closeCenter, buttonRadius, FontAwesomeIcon.Times.ToIconString(), ui.MutedInk,
                Transparent, 0.85f)
            || ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            CloseSearch();
        }
    }

    private void SyncSearch()
    {
        var snapshot = store.Messages;
        var query = searchQuery.Trim();
        if (ReferenceEquals(snapshot, searchSource) && query == searchLastQuery)
        {
            return;
        }

        var queryChanged = query != searchLastQuery;
        searchSource = snapshot;
        searchLastQuery = query;
        searchMatches.Clear();
        if (query.Length == 0)
        {
            searchIndex = 0;
            return;
        }

        for (var index = 0; index < snapshot.Length; index++)
        {
            var message = snapshot[index];
            if (message.Kind == 2 || message.Deleted)
            {
                continue;
            }

            if ((message.Body ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                searchMatches.Add(message.Id);
            }
        }

        searchIndex = Math.Clamp(searchIndex, 0, Math.Max(0, searchMatches.Count - 1));
        if (queryChanged && searchMatches.Count > 0)
        {
            searchIndex = searchMatches.Count - 1;
            transcript.RequestScrollTo(searchMatches[searchIndex]);
        }
    }

    private void DrawReplyBar(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.05f)));
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);
        var barMin = new Vector2(area.Min.X + 14f * scale, area.Min.Y + 8f * scale);
        var barMax = new Vector2(barMin.X + 3f * scale, area.Max.Y - 8f * scale);
        Squircle.Fill(drawList, barMin, barMax, 1.5f * scale, ImGui.GetColorU32(ui.Accent));
        var textLeft = barMax.X + 9f * scale;
        var closeRadius = 13f * scale;
        var textWidth = area.Max.X - 20f * scale - closeRadius * 2f - textLeft;
        Typography.Draw(new Vector2(textLeft, area.Min.Y + 7f * scale),
            Typography.FitText(Loc.T(L.Message.ReplyingTo, replyBarName), textWidth, 0.78f, FontWeight.SemiBold),
            ui.Accent, 0.78f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, area.Min.Y + 24f * scale),
            Typography.FitText(replyBarPreview, textWidth, 0.82f, FontWeight.Regular), ui.MutedInk, 0.82f);
        var closeCenter = new Vector2(area.Max.X - 14f * scale - closeRadius, area.Center.Y);
        if (ui.IconButton(closeCenter, closeRadius, FontAwesomeIcon.Times.ToIconString(), ui.MutedInk,
                Transparent, 0.9f, Loc.T(L.Common.Cancel))
            || (!searchOpen && ImGui.IsKeyPressed(ImGuiKey.Escape)))
        {
            ClearReplyTarget();
        }
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

    private void CopyMessageText(string id)
    {
        var snapshot = store.Messages;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id != id)
            {
                continue;
            }

            var message = snapshot[index];
            if (message.EncVersion == 1 && store.DecryptionState(id).State != DmBodyState.Decrypted)
            {
                return;
            }

            ImGui.SetClipboardText(message.Body ?? string.Empty);
            return;
        }
    }

    private void DrawEncryptionBanner(ref Rect listRect, ConversationDto? conversation, bool isGroup)
    {
        string? text = null;
        string? dismissUserId = null;
        if (!isGroup && conversation is not null && store.HasRotationNotice(conversation.OtherUserId))
        {
            text = Loc.T(L.Encryption.SafetyChanged, DirectMessagesStore.DisplayTitle(conversation));
            dismissUserId = conversation.OtherUserId;
        }

        if (text is null)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var height = 26f * scale;
        var min = listRect.Min;
        var max = new Vector2(listRect.Max.X, listRect.Min.Y + height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.14f)));
        Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f),
            text, AppPalettes.Message.MutedInk, 0.76f, FontWeight.Medium);
        if (dismissUserId is not null && ImGui.IsMouseHoveringRect(min, max)
            && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            store.ClearRotationNotice(dismissUserId);
        }

        listRect = new Rect(new Vector2(listRect.Min.X, max.Y), listRect.Max);
    }

    private void DrawThreadHeader(Rect area, ConversationDto? conversation, bool isGroup)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, string.Empty, back);
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        DrawEncryptionStatusIcon(area, conversation, rowCenterY, scale);
        var name = conversation is null ? DisplayName : DirectMessagesStore.DisplayTitle(conversation);
        var avatarRadius = 18f * scale;
        var nameSize = Typography.Measure(name, 1f, FontWeight.SemiBold);
        var gap = 9f * scale;
        var groupWidth = avatarRadius * 2f + gap + nameSize.X;
        var startX = MathF.Max(area.Center.X - groupWidth * 0.5f, area.Min.X + 48f * scale);
        var avatarCenter = new Vector2(startX + avatarRadius, rowCenterY);
        if (isGroup)
        {
            drawList.AddCircleFilled(avatarCenter, avatarRadius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.85f)),
                32);
            AppSkin.Icon(avatarCenter, FontAwesomeIcon.Users.ToIconString(), White, 0.8f);
        }
        else
        {
            AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, name, string.Empty,
                conversation?.OtherAvatarUrl, images, lodestone, 0.9f, 32);
        }

        var nameLeft = avatarCenter.X + avatarRadius + gap;
        if (isGroup && conversation is not null)
        {
            var sub = Loc.T(L.DirectMessages.MembersCount, conversation.MemberCount);
            var subSize = Typography.Measure(sub, 0.72f, FontWeight.Regular);
            var gapY = 1f * scale;
            var stackTop = rowCenterY - (nameSize.Y + gapY + subSize.Y) * 0.5f;
            Typography.Draw(new Vector2(nameLeft, stackTop), name, theme.TextStrong, 1f, FontWeight.SemiBold);
            Typography.Draw(new Vector2(nameLeft, stackTop + nameSize.Y + gapY), sub, AppPalettes.Message.MutedInk,
                0.72f);
            var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
            var hitMax = new Vector2(nameLeft + MathF.Max(nameSize.X, subSize.X), area.Min.Y + AppHeader.Height * scale);
            if (UiInteract.HoverClick(hitMin, hitMax))
            {
                router.Push(MessageRoute.GroupInfo(conversation.Id));
            }
        }
        else
        {
            Typography.Draw(new Vector2(nameLeft, rowCenterY - nameSize.Y * 0.5f), name, theme.TextStrong, 1f,
                FontWeight.SemiBold);
            if (conversation is not null && contacts.Find(conversation.OtherUserId) is not null)
            {
                var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
                var hitMax = new Vector2(nameLeft + nameSize.X, area.Min.Y + AppHeader.Height * scale);
                if (UiInteract.HoverClick(hitMin, hitMax))
                {
                    router.Push(MessageRoute.Contact(conversation.OtherUserId));
                }
            }
        }
    }

    private void DrawEncryptionStatusIcon(Rect area, ConversationDto? conversation, float rowCenterY, float scale)
    {
        var encrypted = store.EncryptingCurrent;
        var tooltip = encrypted
            ? Loc.T(L.Encryption.EncryptedIndicator)
            : store.VaultState == KeyVaultState.Provisioning
                ? Loc.T(L.Encryption.SettingUp)
                : Loc.T(L.Encryption.PlaintextIndicator);
        var glyph = encrypted ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;
        var center = new Vector2(area.Max.X - 24f * scale, rowCenterY);
        if (ui.IconButton(center, 16f * scale, glyph.ToIconString(), encrypted ? ui.Accent : ui.MutedInk,
                Transparent, 1f, tooltip, HoverLabelSide.Below) && conversation is not null)
        {
            router.Push(MessageRoute.Encryption(conversation.Id));
        }

        var searchCenter = new Vector2(area.Max.X - 52f * scale, rowCenterY);
        if (ui.IconButton(searchCenter, 16f * scale, FontAwesomeIcon.Search.ToIconString(),
                searchOpen ? ui.Accent : ui.MutedInk, Transparent, 0.95f, Loc.T(L.Common.Search),
                HoverLabelSide.Below))
        {
            if (searchOpen)
            {
                CloseSearch();
            }
            else
            {
                searchOpen = true;
                searchFocus = true;
            }
        }
    }

    private ReadOnlySpan<TranscriptMessage> BuildTranscript(ChatMessageDto[] source, bool isGroup)
    {
        if (ReferenceEquals(source, transcriptSource))
        {
            return transcriptCache;
        }

        transcriptSource = source;
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
            if (message.ReplyToId is not null)
            {
                replySender = message.ReplySenderId == store.MyUserId
                    ? Loc.T(L.Message.You)
                    : message.ReplySenderName ?? Loc.T(L.Message.OriginalUnavailable);
                replyBody = QuotePreview(message.ReplyBody, message.ReplyKind);
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
                message.CreatedAtUnix, message.MediaWidth, message.MediaHeight, message.ReadAtUnix, senderName, tint,
                MessageFlags(message), message.ReplyToId, replySender, replyBody, message.ReplyKind,
                message.DurationSecs, reactions);
        }

        transcriptCache = mapped;
        return transcriptCache;
    }

    private byte MessageFlags(ChatMessageDto message)
    {
        byte flags = 0;
        if (message.Forwarded)
        {
            flags |= TranscriptFlags.Forwarded;
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

    private void TickThread(string conversationId)
    {
        PumpPendingVoice();
        var delta = ImGui.GetIO().DeltaTime;
        sinceThreadPoll += delta;
        if (sinceThreadPoll >= ThreadPollSeconds)
        {
            sinceThreadPoll = 0f;
            store.RefreshThread();
            store.RefreshTyping(conversationId);
        }

        sinceTypingSend += delta;
        if (messageDraft != lastTypingDraft)
        {
            lastTypingDraft = messageDraft;
            if (messageDraft.Trim().Length > 0 && sinceTypingSend >= TypingSendSeconds)
            {
                sinceTypingSend = 0f;
                store.SendTyping(conversationId);
            }
        }
    }

    private void DrawMessageComposer(Rect area, string conversationId)
    {
        if (voiceRecorder.Recording)
        {
            DrawRecordingComposer(area, conversationId);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);
        var buttonRadius = 16f * scale;
        var pictureCenter = new Vector2(area.Min.X + 12f * scale + buttonRadius, area.Center.Y);
        var pictureMin = pictureCenter - new Vector2(buttonRadius, buttonRadius);
        var pictureMax = pictureCenter + new Vector2(buttonRadius, buttonRadius);
        var pictureHovered = ImGui.IsMouseHoveringRect(pictureMin, pictureMax);
        drawList.AddCircleFilled(pictureCenter, buttonRadius,
            ImGui.GetColorU32(pictureHovered ? Palette.Mix(ui.Accent, theme.TextStrong, 0.12f) : ui.Accent), 24);
        AppSkin.Icon(pictureCenter, FontAwesomeIcon.Image.ToIconString(), White, 0.85f);
        HoverTooltip.Show(new Rect(pictureMin, pictureMax), Loc.T(L.Velvet.SendPicture), HoverLabelSide.Above);
        if (pictureHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                router.Push(MessageRoute.ChatImage(conversationId));
            }
        }

        var sendWidth = 40f * scale;
        var pillMin = new Vector2(pictureMax.X + 10f * scale, area.Min.Y + 8f * scale);
        var pillMax = new Vector2(area.Max.X - sendWidth - 12f * scale, area.Max.Y - 8f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 14f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
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
            if (ImGui.InputTextWithHint("##messageMessage", Loc.T(L.Velvet.MessageHint), ref messageDraft, MessageMax,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var hasDraft = messageDraft.Trim().Length > 0;
        var canSend = hasDraft && !store.Sending;
        var sendCenter = new Vector2(area.Max.X - sendWidth * 0.5f - 8f * scale, area.Center.Y);
        var sendHitRadius = 16f * scale;
        var sendRect = new Rect(sendCenter - new Vector2(sendHitRadius, sendHitRadius),
            sendCenter + new Vector2(sendHitRadius, sendHitRadius));
        if (hasDraft)
        {
            drawList.AddCircleFilled(sendCenter, 16f * scale,
                ImGui.GetColorU32(canSend ? ui.Accent : theme.SurfaceMuted), 24);
            AppSkin.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), White, 0.9f);
            HoverTooltip.Show(sendRect, Loc.T(L.Velvet.Send), HoverLabelSide.Above);
            if (UiInteract.Hover(sendRect.Min, sendRect.Max))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && canSend)
                {
                    submitted = true;
                }
            }
        }
        else
        {
            drawList.AddCircleFilled(sendCenter, 16f * scale, ImGui.GetColorU32(ui.Accent), 24);
            AppSkin.Icon(sendCenter, FontAwesomeIcon.Microphone.ToIconString(), White, 0.9f);
            HoverTooltip.Show(sendRect, Loc.T(L.Message.RecordVoiceHint), HoverLabelSide.Above);
            if (UiInteract.Hover(sendRect.Min, sendRect.Max))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !store.Sending)
                {
                    voiceRecorder.Start(AudioDevices.ResolveInput(configuration.CallInputDevice));
                }
            }
        }

        if (submitted && canSend)
        {
            store.SendMessage(conversationId, messageDraft, _ => { }, replyTargetId);
            messageDraft = string.Empty;
            lastTypingDraft = string.Empty;
            ClearReplyTarget();
            transcript.RequestSnapToBottom();
            threadFocus = true;
        }
    }

    private void DrawRecordingComposer(Rect area, string conversationId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);
        var cancelCenter = new Vector2(area.Min.X + 28f * scale, area.Center.Y);
        if (ui.IconButton(cancelCenter, 16f * scale, FontAwesomeIcon.TrashAlt.ToIconString(), theme.Danger,
                Transparent, 1f, Loc.T(L.Common.Cancel), HoverLabelSide.Above))
        {
            voiceRecorder.Cancel();
            return;
        }

        var pulse = 0.55f + 0.45f * MathF.Sin((float)ImGui.GetTime() * 5f);
        var dotCenter = new Vector2(cancelCenter.X + 34f * scale, area.Center.Y);
        drawList.AddCircleFilled(dotCenter, 5f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, 0.4f + 0.6f * pulse)), 16);
        var elapsed = TimeText.MinutesSeconds((int)voiceRecorder.ElapsedSeconds);
        Typography.Draw(new Vector2(dotCenter.X + 12f * scale, area.Center.Y
            - Typography.Measure(elapsed, 1f, FontWeight.SemiBold).Y * 0.5f), elapsed, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        var meterLeft = dotCenter.X + 64f * scale;
        var meterRight = area.Max.X - 64f * scale;
        if (meterRight > meterLeft + 30f * scale)
        {
            var meterY = area.Center.Y;
            drawList.AddRectFilled(new Vector2(meterLeft, meterY - 2f * scale),
                new Vector2(meterRight, meterY + 2f * scale),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 2f * scale);
            var level = Math.Clamp(voiceRecorder.Level * 6f, 0f, 1f);
            drawList.AddRectFilled(new Vector2(meterLeft, meterY - 2f * scale),
                new Vector2(meterLeft + (meterRight - meterLeft) * level, meterY + 2f * scale),
                ImGui.GetColorU32(ui.Accent), 2f * scale);
        }

        var sendCenter = new Vector2(area.Max.X - 28f * scale, area.Center.Y);
        drawList.AddCircleFilled(sendCenter, 16f * scale, ImGui.GetColorU32(ui.Accent), 24);
        AppSkin.Icon(sendCenter, FontAwesomeIcon.PaperPlane.ToIconString(), White, 0.9f);
        var sendRect = new Rect(sendCenter - new Vector2(16f * scale, 16f * scale),
            sendCenter + new Vector2(16f * scale, 16f * scale));
        HoverTooltip.Show(sendRect, Loc.T(L.Velvet.Send), HoverLabelSide.Above);
        var sendClicked = UiInteract.HoverClick(sendRect.Min, sendRect.Max);
        if (sendClicked || voiceRecorder.AtCapacity)
        {
            if (voiceRecorder.Stop(out var wavBytes, out var durationSecs))
            {
                store.SendVoiceMessage(conversationId, wavBytes, durationSecs, _ => { });
                transcript.RequestSnapToBottom();
            }
        }
    }
}
