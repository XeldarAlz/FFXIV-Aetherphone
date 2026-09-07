using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const byte ChatFilterAll = 0;
    private const byte ChatFilterUnread = 1;
    private const byte ChatFilterFavorites = 2;
    private const byte ChatFilterGroups = 3;
    private const float ChatRowHeight = 72f;
    private const float ChatRowAvatarRadius = 25f;
    private const float ChatRowTitleTop = 13f;
    private const float ChatSearchHeight = 52f;
    private const float ChatSearchRevealSeconds = 0.12f;
    private const float ChatChipsHeight = 44f;
    private const float ArchivedRowHeight = 52f;
    private const float UnreadBadgeRadius = 10f;
    private const float RowStatusGlyph = 16f;
    private const float RowStatusPitch = 20f;
    private const float PreviewGlyph = 15f;
    private const float PreviewGlyphGap = 4f;
    private const float TimeGap = 8f;

    private static readonly TextStyle UnreadCountStyle = new(0.68f, FontWeight.SemiBold);

    private readonly ActionSheet.Item[] chatSheetItems = new ActionSheet.Item[4];
    private readonly ChipRail chatFilterRail = new();
    private readonly string[] chatFilterLabels = new string[4];
    private readonly bool[] chatFilterActive = new bool[4];
    private readonly List<ConversationDto> pinnedChats = new();
    private readonly List<ConversationDto> regularChats = new();
    private string? sheetConversationId;
    private string chatSheetTitle = string.Empty;
    private string chatQuery = string.Empty;
    private byte chatFilter = ChatFilterAll;
    private bool chatSearchOpen;
    private bool chatSearchFocus;
    private Spring chatSearchReveal = new(0f);

    private void DrawChatsTab(Rect area)
    {
        if (!session.IsSignedIn)
        {
            EmptyState.Draw(area, ui, PhoneIcons.MessageCircle, DisplayName, Loc.T(L.DirectMessages.SignInPrompt));
            return;
        }

        if (!store.ConversationsLoaded && !store.LoadingConversations)
        {
            store.RefreshConversations();
        }

        var scale = UiScale.Current;
        var chipsTop = DrawChatSearchRow(area, scale);
        var railTop = chipsTop + (ChatChipsHeight - ChipRail.RowHeight) * 0.5f * scale;
        var railRow = new Rect(new Vector2(area.Min.X + CellPadX * scale, railTop),
            new Vector2(area.Max.X - CellPadX * scale, railTop + ChipRail.RowHeight * scale));
        chatFilterLabels[0] = Loc.T(L.Collections.FilterAll);
        chatFilterLabels[1] = Loc.T(L.Message.FilterUnread);
        chatFilterLabels[2] = Loc.T(L.Message.Favorites);
        chatFilterLabels[3] = Loc.T(L.Message.FilterGroups);
        for (var index = 0; index < chatFilterActive.Length; index++)
        {
            chatFilterActive[index] = chatFilter == index;
        }

        var tappedChip = chatFilterRail.Draw(railRow, ui, chatFilterLabels, chatFilterActive, centered: true);
        if (tappedChip >= 0)
        {
            chatFilter = (byte)tappedChip;
        }

        var listRect = new Rect(new Vector2(area.Min.X, chipsTop + ChatChipsHeight * scale), area.Max);
        DrawRecoveryNudge(ref listRect);
        CollectChats(pinnedChats, regularChats, archived: false);
        var query = chatQuery.Trim();
        if (pinnedChats.Count == 0 && regularChats.Count == 0)
        {
            if (query.Length > 0 || chatFilter != ChatFilterAll)
            {
                EmptyState.Draw(listRect, ui, PhoneIcons.Search, Loc.T(L.Phone.NoOneFound), string.Empty);
            }
            else if (store.ThreadListFailed)
            {
                threadListFailure.Set(store.ThreadListFailure);
                if (EmptyState.Draw(listRect, ui, PhoneIcons.HelpCircle, Loc.T(L.Failure.CouldNotLoad),
                        threadListFailure.Text(), Loc.T(L.Common.Retry)))
                {
                    store.RefreshConversations();
                }
            }
            else if (EmptyState.Draw(listRect, ui, PhoneIcons.MessageCircle, Loc.T(L.DirectMessages.Empty),
                         Loc.T(L.DirectMessages.EmptyHint), Loc.T(L.Message.NewChat)))
            {
                OpenNewChat();
            }

            return;
        }

        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            var drawList = ImGui.GetWindowDrawList();
            if (query.Length == 0 && chatFilter == ChatFilterAll && configuration.MessageArchivedChats.Count > 0)
            {
                DrawArchivedRow(drawList);
            }

            for (var index = 0; index < pinnedChats.Count; index++)
            {
                DrawConversationRow(drawList, pinnedChats[index], pinned: true);
            }

            for (var index = 0; index < regularChats.Count; index++)
            {
                DrawConversationRow(drawList, regularChats[index], pinned: false);
            }

            if (store.LoadingMoreThreads)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, ui.MutedInk);
            }
            else if (store.HasMoreThreads && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreThreads();
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private float DrawChatSearchRow(Rect area, float scale)
    {
        var target = chatSearchOpen ? 1f : 0f;
        var frameSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var reveal = chatSearchReveal.Step(target, ChatSearchRevealSeconds, frameSeconds);
        if (chatSearchReveal.IsResting(target, 0.005f, 0.05f))
        {
            chatSearchReveal.SnapTo(target);
            reveal = target;
        }

        var height = ChatSearchHeight * scale * Math.Clamp(reveal, 0f, 1f);
        if (height < 1f)
        {
            return area.Min.Y;
        }

        var drawList = ImGui.GetWindowDrawList();
        var bottom = area.Min.Y + height;
        drawList.PushClipRect(area.Min, new Vector2(area.Max.X, bottom), true);
        var bar = new Rect(new Vector2(area.Min.X + CellPadX * scale, bottom - ChatSearchHeight * scale),
            new Vector2(area.Max.X - CellPadX * scale, bottom));
        SearchField.Draw(bar, "##messageFilter", Loc.T(L.Common.Search), ref chatQuery, ui.Palette,
            focus: chatSearchFocus);
        chatSearchFocus = false;
        drawList.PopClipRect();
        return bottom;
    }

    private void ToggleChatSearch()
    {
        if (chatSearchOpen)
        {
            CloseChatSearch();
            return;
        }

        chatSearchOpen = true;
        chatSearchFocus = true;
    }

    private void CloseChatSearch()
    {
        chatSearchOpen = false;
        chatSearchFocus = false;
        chatQuery = string.Empty;
    }

    private void ResetChatSearch()
    {
        CloseChatSearch();
        chatSearchReveal.SnapTo(0f);
    }

    private void DrawArchivedRow(ImDrawListPtr drawList)
    {
        var scale = UiScale.Current;
        var cell = FeedCell.Begin(drawList, ArchivedRowHeight * scale, ui.HoverWash);
        var pad = CellPadX * scale;
        var glyphCenter = new Vector2(cell.Bounds.Min.X + pad + ChatRowAvatarRadius * scale, cell.Bounds.Center.Y);
        PhoneIcon.Draw(drawList, glyphCenter, PhoneIcons.Archive, ink.MutedInk, RowStatusGlyph * 1.3f * scale);
        var textLeft = glyphCenter.X + ChatRowAvatarRadius * scale + RowAvatarGap * scale;
        var count = configuration.MessageArchivedChats.Count.ToString(Loc.Culture);
        var countSize = Typography.Measure(count, RowSubStyle);
        Typography.Draw(drawList, new Vector2(cell.Bounds.Max.X - pad - countSize.X, cell.Bounds.Center.Y - countSize.Y * 0.5f),
            count, ink.MutedInk, RowSubStyle);
        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, cell.Bounds.Center.Y - labelHeight * 0.5f),
            Loc.T(L.Message.Archived), ink.TitleInk, RowTitleStyle);
        if (cell.Tapped)
        {
            router.Push(MessageRoute.Archived);
        }

        DrawRowHairline(drawList, cell, textLeft);
    }

    private void DrawRecoveryNudge(ref Rect listRect)
    {
        if (store.VaultState != KeyVaultState.Unlocked)
        {
            return;
        }

        if (store.Vault.UnsavedRecoveryCode is not null)
        {
            ChatHeaderControls.DrawBanner(ui, ref listRect, Loc.T(L.Encryption.SaveCodeBanner), ui.Accent,
                () =>
                {
                    encryptionSetup.Request();
                    navigation.Open("settings");
                });
            return;
        }

        if (recoveryNudgeDismissed || store.Vault.RecoveryConfigured
            || !configuration.RecoveryNudgeDue())
        {
            return;
        }

        ChatHeaderControls.DrawPromptBanner(ui, ref listRect, Loc.T(L.Encryption.RecoveryNudgeBanner), ui.MutedInk,
            () =>
            {
                recoveryNudgeDismissed = true;
                encryptionSetup.Request();
                navigation.Open("settings");
            },
            () =>
            {
                recoveryNudgeDismissed = true;
                configuration.SnoozeRecoveryNudge();
            });
    }

    private void DrawArchived(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Message.Archived));
        var top = area.Min.Y + AppHeader.Height * scale;
        var listRect = new Rect(new Vector2(area.Min.X, top), area.Max);
        CollectChats(pinnedChats, regularChats, archived: true);
        if (regularChats.Count == 0)
        {
            EmptyState.Draw(listRect, ui, PhoneIcons.Archive, Loc.T(L.Message.NoArchived), string.Empty);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            var drawList = ImGui.GetWindowDrawList();
            for (var index = 0; index < regularChats.Count; index++)
            {
                DrawConversationRow(drawList, regularChats[index], pinned: false);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void CollectChats(List<ConversationDto> pinnedTarget, List<ConversationDto> regularTarget, bool archived)
    {
        pinnedTarget.Clear();
        regularTarget.Clear();
        var snapshot = store.Conversations;
        var query = chatQuery.Trim();
        for (var index = 0; index < snapshot.Length; index++)
        {
            var item = snapshot[index];
            if (configuration.MessageArchivedChats.Contains(item.Id) != archived)
            {
                continue;
            }

            if (!archived && !PassesChatFilter(item))
            {
                continue;
            }

            if (query.Length > 0 && !DirectMessagesStore.DisplayTitle(item).Contains(query,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!archived && configuration.MessagePinnedChats.Contains(item.Id))
            {
                pinnedTarget.Add(item);
            }
            else
            {
                regularTarget.Add(item);
            }
        }
    }

    private bool PassesChatFilter(ConversationDto item)
    {
        return chatFilter switch
        {
            ChatFilterUnread => item.UnreadCount > 0,
            ChatFilterFavorites => !item.IsGroup && configuration.MessageFavoriteContacts.Contains(item.OtherUserId),
            ChatFilterGroups => item.IsGroup,
            _ => true,
        };
    }

    private void DrawConversationRow(ImDrawListPtr drawList, ConversationDto item, bool pinned)
    {
        var scale = UiScale.Current;
        var row = BeginPersonRow(drawList, ChatRowHeight, ChatRowAvatarRadius, 0f, true, out var avatarCenter);
        DrawConversationAvatar(drawList, item, avatarCenter, ChatRowAvatarRadius * scale);
        var title = DirectMessagesStore.DisplayTitle(item);
        var unread = item.UnreadCount > 0;
        var lineTop = row.Bounds.Min.Y + ChatRowTitleTop * scale;
        var titleHeight = Typography.LineHeight(RowTitleStyle);
        var timeLabel = ChatTime(item.LastMessageAtUnix);
        var timeSize = Typography.Measure(timeLabel, RowMetaStyle);
        var timeLeft = row.TextRight - timeSize.X;
        Typography.Draw(drawList, new Vector2(timeLeft, lineTop + (titleHeight - timeSize.Y) * 0.5f), timeLabel,
            unread ? ink.AccentLink : ink.MutedInk, RowMetaStyle);
        var titleRight = timeLabel.Length > 0 ? timeLeft - TimeGap * scale : row.TextRight;
        var titleHovering = UiInteract.Hover(new Vector2(row.TextLeft, lineTop),
            new Vector2(titleRight, lineTop + titleHeight));
        Marquee.DrawLeft(drawList, new MarqueeId("messageapp.chats.title.", item.Id), title, row.TextLeft, lineTop,
            MathF.Max(1f, titleRight - row.TextLeft), RowTitleStyle, ink.TitleInk, titleHovering);

        var subHeight = Typography.LineHeight(RowSubStyle);
        var subTop = lineTop + titleHeight + RowLineGap * scale;
        var subCenterY = subTop + subHeight * 0.5f;
        var right = row.TextRight;
        if (unread)
        {
            var badgeRadius = UnreadBadgeRadius * scale;
            var label = item.UnreadCount > 99 ? "99+" : item.UnreadCount.ToString(Loc.Culture);
            var labelWidth = Typography.Measure(label, UnreadCountStyle).X;
            var badgeWidth = MathF.Max(badgeRadius * 2f, labelWidth + 10f * scale);
            var badgeMin = new Vector2(right - badgeWidth, subCenterY - badgeRadius);
            var badgeMax = new Vector2(right, subCenterY + badgeRadius);
            Squircle.Fill(drawList, badgeMin, badgeMax, badgeRadius,
                ImGui.GetColorU32(item.Muted ? ink.MutedInk : activeTheme.Badge));
            Typography.DrawCentered(drawList, (badgeMin + badgeMax) * 0.5f, label,
                item.Muted ? MessageThemes.Body : White, UnreadCountStyle);
            right = badgeMin.X - RowTrailingGap * scale;
        }

        if (item.Muted)
        {
            PhoneIcon.Draw(drawList, new Vector2(right - RowStatusGlyph * 0.5f * scale, subCenterY), PhoneIcons.BellOff,
                ink.MutedInk, RowStatusGlyph * scale);
            right -= RowStatusPitch * scale;
        }

        if (pinned)
        {
            PhoneIcon.Draw(drawList, new Vector2(right - RowStatusGlyph * 0.5f * scale, subCenterY),
                PhoneIcons.PinFilled, ink.MutedInk, RowStatusGlyph * scale);
            right -= RowStatusPitch * scale;
        }

        if (!item.IsGroup && musters.ContactMusterFor(item.OtherUserId) is { } hosted)
        {
            var musterCenter = new Vector2(right - RowStatusGlyph * 0.5f * scale, subCenterY);
            var musterExtent = new Vector2(RowStatusGlyph * 0.6f * scale, RowStatusGlyph * 0.6f * scale);
            var musterRect = new Rect(musterCenter - musterExtent, musterCenter + musterExtent);
            var overMuster = UiInteract.Hover(musterRect.Min, musterRect.Max);
            PhoneIcon.Draw(drawList, musterCenter, PhoneIcons.Compass, AppAccents.For(MusterStore.AppId),
                RowStatusGlyph * scale);
            HoverTooltip.Show(musterRect, Loc.T(L.Message.HostingMuster), HoverLabelSide.Above);
            if (UiInteract.Click(musterRect.Min, musterRect.Max, overMuster))
            {
                musterLauncher.RequestDetail(hosted.Id);
                navigation.Open(MusterStore.AppId);
            }

            right -= RowStatusPitch * scale;
        }

        var previewRight = right - RowTrailingGap * scale;
        var previewInk = unread ? ink.BodyInk : ink.MutedInk;
        var draft = configuration.MessageDrafts.GetValueOrDefault(item.Id, string.Empty);
        if (draft.Length > 0)
        {
            var prefix = Loc.T(L.Message.DraftPrefix);
            var prefixStyle = new TextStyle(RowSubStyle.Scale, FontWeight.SemiBold);
            var prefixSize = Typography.Measure(prefix, prefixStyle);
            Typography.Draw(drawList, new Vector2(row.TextLeft, subTop), prefix, ink.AccentLink, prefixStyle);
            var draftLeft = row.TextLeft + prefixSize.X + PreviewGlyphGap * scale;
            Typography.Draw(drawList, new Vector2(draftLeft, subTop),
                Typography.FitText(draft, MathF.Max(1f, previewRight - draftLeft), RowSubStyle), ink.MutedInk,
                RowSubStyle);
        }
        else
        {
            var previewLeft = row.TextLeft;
            if (item.LastMessageSenderId.Length > 0 && item.LastMessageSenderId == store.MyUserId)
            {
                PhoneIcon.Draw(drawList, new Vector2(previewLeft + PreviewGlyph * 0.5f * scale, subCenterY),
                    PhoneIcons.Check, ink.MutedInk, PreviewGlyph * scale);
                previewLeft += PreviewGlyph * scale + PreviewGlyphGap * scale;
            }

            var kindGlyph = item.LastMessageKind switch
            {
                1 => PhoneIcons.Camera,
                3 => PhoneIcons.Microphone,
                _ => string.Empty,
            };
            if (kindGlyph.Length > 0)
            {
                PhoneIcon.Draw(drawList, new Vector2(previewLeft + PreviewGlyph * 0.5f * scale, subCenterY), kindGlyph,
                    previewInk, PreviewGlyph * scale);
                previewLeft += PreviewGlyph * scale + PreviewGlyphGap * scale;
            }

            var preview = item.LastMessagePreview.Length > 0
                ? ChatText.ListPreview(item.LastMessagePreview)
                : item.LastMessageKind switch
                {
                    1 => Loc.T(L.DirectMessages.PhotoPreview),
                    3 => Loc.T(L.DirectMessages.VoicePreview),
                    _ => string.Empty,
                };
            Typography.Draw(drawList, new Vector2(previewLeft, subTop),
                Typography.FitText(preview, MathF.Max(1f, previewRight - previewLeft), RowSubStyle), previewInk,
                RowSubStyle);
        }

        if (UiInteract.Hover(row.Bounds.Min, row.Bounds.Max) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenChatSheet(item);
        }
        else if (row.Tapped)
        {
            router.Push(MessageRoute.Thread(item.Id));
        }

        EndPersonRow(drawList, row);
    }

    private void OpenChatSheet(ConversationDto conversation)
    {
        var id = conversation.Id;
        sheetConversationId = id;
        chatSheetTitle = DirectMessagesStore.DisplayTitle(conversation);
        var isPinned = configuration.MessagePinnedChats.Contains(id);
        var isArchived = configuration.MessageArchivedChats.Contains(id);
        chatSheetItems[0] = new ActionSheet.Item(Loc.T(isPinned ? L.Common.Unpin : L.Common.Pin),
            isPinned ? PhoneIcons.PinFilled : PhoneIcons.Pin);
        chatSheetItems[1] = new ActionSheet.Item(Loc.T(isArchived ? L.Message.Unarchive : L.Message.Archive),
            PhoneIcons.Archive);
        chatSheetItems[2] = new ActionSheet.Item(Loc.T(conversation.Muted
            ? L.Message.UnmuteAction
            : L.Message.MuteAction), conversation.Muted ? PhoneIcons.Bell : PhoneIcons.BellOff);
        chatSheetItems[3] = new ActionSheet.Item(Loc.T(L.Message.DeleteConversation), PhoneIcons.Trash, true);
        chatSheet.Open();
    }

    private void DrawChatSheet(Rect screen)
    {
        if (!chatSheet.CapturesPointer)
        {
            return;
        }

        if (sheetConversationId is not { } id || FindConversationDto(id) is null)
        {
            chatSheet.Close();
        }

        var picked = chatSheet.Draw(screen, ActionSheetStyle.From(ui), chatSheetItems, Loc.T(L.Common.Cancel), false,
            chatSheetTitle);
        if (picked < 0 || sheetConversationId is not { } conversationId)
        {
            return;
        }

        if (picked == 0)
        {
            TogglePinned(conversationId);
        }
        else if (picked == 1)
        {
            ToggleArchived(conversationId);
        }
        else if (picked == 2)
        {
            store.SetMuted(conversationId, !(FindConversationDto(conversationId)?.Muted ?? false), _ => { });
        }
        else if (picked == 3)
        {
            AskDeleteConversation(conversationId);
        }
    }

    private void AskDeleteConversation(string conversationId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Message.DeleteConversation),
            Message = Loc.T(L.Message.DeleteConversationMessage),
            ConfirmLabel = Loc.T(L.Common.Delete),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            Confirm = () => DeleteConversation(conversationId),
        });
    }

    private void DeleteConversation(string conversationId)
    {
        var current = router.Current;
        var threadOpen = current.Screen == MessageScreen.Thread && current.Id == conversationId;
        configuration.MessagePinnedChats.Remove(conversationId);
        configuration.MessageArchivedChats.Remove(conversationId);
        configuration.MessageChatWallpapers.Remove(conversationId);
        configuration.Save();
        store.DeleteThread(conversationId);
        if (threadOpen)
        {
            router.Pop();
        }
    }

    private ConversationDto? FindConversationDto(string id)
    {
        var snapshot = store.Conversations;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == id)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    private void TogglePinned(string conversationId)
    {
        if (!configuration.MessagePinnedChats.Remove(conversationId))
        {
            configuration.MessagePinnedChats.Add(conversationId);
            configuration.MessageArchivedChats.Remove(conversationId);
        }

        configuration.Save();
    }

    private void ToggleArchived(string conversationId)
    {
        if (!configuration.MessageArchivedChats.Remove(conversationId))
        {
            configuration.MessageArchivedChats.Add(conversationId);
            configuration.MessagePinnedChats.Remove(conversationId);
        }

        configuration.Save();
    }
}
