using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Message;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const byte MemberActMessage = 0;
    private const byte MemberActView = 1;
    private const byte MemberActPromote = 2;
    private const byte MemberActDemote = 3;
    private const byte MemberActRemove = 4;
    private const float MemberRowHeight = 60f;
    private const float MemberAvatarRadius = 21f;
    private const float GroupCameraBadgeRadius = 14f;
    private const float GroupCameraGlyph = 15f;
    private const float DescriptionCardPad = 14f;
    private const float DescriptionFieldHeight = 110f;
    private const int DescriptionMaxLength = 500;

    private readonly ActionSheet memberSheet = new();
    private readonly ActionSheet.Item[] memberSheetItems = new ActionSheet.Item[5];
    private readonly byte[] memberSheetActions = new byte[5];
    private readonly ActionSheet groupPhotoSheet = new();
    private readonly ActionSheet.Item[] groupPhotoSheetItems = new ActionSheet.Item[2];
    private readonly ImagePickCrop groupPhotoPicker;
    private int memberSheetCount;
    private string memberSheetTitle = string.Empty;
    private string? memberSheetUserId;
    private string? memberSheetConversationId;
    private int groupPhotoSheetCount;
    private string? groupPhotoConversationId;
    private volatile bool groupPhotoBusy;
    private volatile int groupPhotoOutcome;
    private string editGroupTitle = string.Empty;
    private string editGroupDescription = string.Empty;
    private string? editGroupLoadedFor;
    private volatile bool editGroupBusy;
    private volatile int editGroupOutcome;

    private void ProcessGroupOutcomes()
    {
        var photo = groupPhotoOutcome;
        if (photo != 0)
        {
            groupPhotoOutcome = 0;
            groupPhotoBusy = false;
            if (photo == 1)
            {
                if (router.Current.Screen == MessageScreen.GroupPhoto)
                {
                    router.Pop();
                }
            }
            else
            {
                ShellToast.Show(Loc.T(L.Message.PhotoFailed));
            }
        }

        var edit = editGroupOutcome;
        if (edit == 0)
        {
            return;
        }

        editGroupOutcome = 0;
        editGroupBusy = false;
        if (edit == 1)
        {
            editGroupLoadedFor = null;
            if (router.Current.Screen == MessageScreen.EditGroup)
            {
                router.Pop();
            }
        }
        else
        {
            ShellToast.Show(Loc.T(L.Message.SaveFailed));
        }
    }

    private void DrawGroupInfo(Rect area, string conversationId)
    {
        var scale = UiScale.Current;
        var headerDrawList = ImGui.GetWindowDrawList();
        var conversation = store.Conversation;
        var valid = conversation is not null && conversation.IsGroup && conversation.Id == conversationId;
        var canManage = valid && ChatRoles.CanManage(store.MyRole);
        DrawScreenHeader(area, string.Empty, canManage ? 1 : 0);
        if (!valid || conversation is null)
        {
            return;
        }

        if (canManage && DrawHeaderIcon(headerDrawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.Pencil,
                Loc.T(L.Message.EditGroup)))
        {
            OpenEditGroup(conversation);
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var title = DirectMessagesStore.DisplayTitle(conversation);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            var origin = ImGui.GetCursorScreenPos();
            var centerX = origin.X + width * 0.5f;
            var radius = HeroAvatarRadius * scale;
            var avatarCenter = new Vector2(centerX, origin.Y + HeroTopPad * scale + radius);
            DrawGroupAvatar(drawList, avatarCenter, radius, title, conversation.AvatarUrl);
            var avatarExtent = new Vector2(radius, radius);
            var avatarHovered = UiInteract.Hover(avatarCenter - avatarExtent, avatarCenter + avatarExtent);
            if (canManage)
            {
                var badgeCenter = avatarCenter + new Vector2(radius * 0.7f, radius * 0.7f);
                drawList.AddCircleFilled(badgeCenter, GroupCameraBadgeRadius * scale + 2f * scale,
                    ImGui.GetColorU32(MessageThemes.Body), 24);
                drawList.AddCircleFilled(badgeCenter, GroupCameraBadgeRadius * scale, ImGui.GetColorU32(ui.Accent), 24);
                PhoneIcon.Draw(drawList, badgeCenter, PhoneIcons.Camera, White, GroupCameraGlyph * scale);
                HoverTooltip.Show(new Rect(avatarCenter - avatarExtent, avatarCenter + avatarExtent),
                    Loc.T(L.Message.GroupPhoto), HoverLabelSide.Below);
                if (UiInteract.Click(avatarCenter - avatarExtent, avatarCenter + avatarExtent, avatarHovered))
                {
                    OpenGroupPhotoSheet(conversation);
                }
            }
            else
            {
                avatarLightbox.TryOpen(avatarCenter, radius, conversation.AvatarUrl, images);
            }

            var nameY = avatarCenter.Y + radius + HeroNameGap * scale;
            var afterName = nameY + Typography.DrawWrappedCentered(new Vector2(centerX, nameY), title, ink.TitleInk,
                TextStyles.Title2, width - 24f * scale) + 4f * scale;
            afterName += Typography.DrawWrappedCentered(new Vector2(centerX, afterName),
                Loc.T(L.Message.GroupSubtitle, conversation.MemberCount), ink.MutedInk, TextStyles.Subheadline,
                width - 24f * scale) + 4f * scale;
            var actionsTop = afterName + 14f * scale;
            var actionsBottom = DrawGroupActions(drawList, conversation, origin.X, width, actionsTop, scale);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, actionsBottom + Metrics.Space.Lg * scale));
            ImGui.Dummy(new Vector2(width, 0f));
            DrawGroupDescriptionCard(drawList, conversation, canManage, scale);
            DrawGroupMembers(drawList, conversation, canManage, scale);
            var chatCard = GroupCard.Begin(ui, 2, SettingRowHeight);
            if (DrawCardRow(drawList, chatCard.NextRow(), PhoneIcons.Wallpaper, TintTeal, Loc.T(L.Message.Wallpaper)))
            {
                router.Push(MessageRoute.ChatWallpaper(conversation.Id));
            }

            if (DrawCardRow(drawList, chatCard.NextRow(), PhoneIcons.Lock, TintAzure, Loc.T(L.Encryption.InfoTitle)))
            {
                router.Push(MessageRoute.Encryption(conversation.Id));
            }

            chatCard.End();
            DrawCardGap();
            var dangerCard = GroupCard.Begin(ui, 1, SettingRowHeight);
            if (DrawCardDangerRow(drawList, dangerCard.NextRow(), PhoneIcons.Logout, Loc.T(L.Message.ExitGroup)))
            {
                AskLeave(conversation.Id);
            }

            dangerCard.End();
            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private float DrawGroupActions(ImDrawListPtr drawList, ConversationDto conversation, float left, float width,
        float top, float scale)
    {
        var gap = HeroActionGap * scale;
        var buttonWidth = (width - gap * 2f) / 3f;
        var height = HeroActionHeight * scale;
        var addRect = new Rect(new Vector2(left, top), new Vector2(left + buttonWidth, top + height));
        if (DrawHeroActionButton(drawList, addRect, PhoneIcons.UserPlus, Loc.T(L.DirectMessages.Add), true))
        {
            OpenAddMembers(conversation.Id);
        }

        var muteRect = new Rect(new Vector2(addRect.Max.X + gap, top),
            new Vector2(addRect.Max.X + gap + buttonWidth, top + height));
        if (DrawHeroActionButton(drawList, muteRect, conversation.Muted ? PhoneIcons.Bell : PhoneIcons.BellOff,
                Loc.T(conversation.Muted ? L.Message.UnmuteAction : L.Message.MuteAction), true))
        {
            store.SetMuted(conversation.Id, !conversation.Muted, _ => { });
        }

        var starredRect = new Rect(new Vector2(muteRect.Max.X + gap, top), new Vector2(left + width, top + height));
        var starredCount = StarredCountIn(conversation.Id);
        if (DrawHeroActionButton(drawList, starredRect, PhoneIcons.Star, Loc.T(L.Message.StarAction),
                starredCount > 0))
        {
            router.Push(MessageRoute.StarredIn(conversation.Id));
        }

        return top + height;
    }

    private void DrawGroupDescriptionCard(ImDrawListPtr drawList, ConversationDto conversation, bool canManage,
        float scale)
    {
        var description = conversation.Description ?? string.Empty;
        if (description.Length == 0)
        {
            if (!canManage)
            {
                return;
            }

            var addCard = GroupCard.Begin(ui, 1, SettingRowHeight);
            if (DrawCardRow(drawList, addCard.NextRow(), string.Empty, default, Loc.T(L.Message.AddDescription),
                    labelInk: ink.AccentLink))
            {
                OpenEditGroup(conversation);
            }

            addCard.End();
            DrawCardGap();
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = DescriptionCardPad * scale;
        var textWidth = width - pad * 2f;
        var textHeight = Typography.MeasureWrappedBlock(description, TextStyles.Subheadline, textWidth).Y;
        var cardMax = new Vector2(origin.X + width, origin.Y + textHeight + pad * 2f);
        ui.Card(drawList, origin, cardMax, Metrics.Radius.Md * scale);
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad), description, ink.BodyInk,
            TextStyles.Subheadline, textWidth);
        if (canManage && UiInteract.HoverClick(origin, cardMax))
        {
            OpenEditGroup(conversation);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardMax.Y - origin.Y));
        DrawCardGap();
    }

    private void DrawGroupMembers(ImDrawListPtr drawList, ConversationDto conversation, bool canManage, float scale)
    {
        var members = store.Members;
        var activeCount = 0;
        for (var index = 0; index < members.Length; index++)
        {
            if (members[index].IsActive)
            {
                activeCount++;
            }
        }

        DrawInsetSectionLabel(Loc.T(L.DirectMessages.MembersCount, activeCount));
        var card = GroupCard.Begin(ui, activeCount + 1, MemberRowHeight);
        var addRow = card.NextRow();
        var addTileRadius = MemberAvatarRadius * scale;
        var addTileCenter = new Vector2(addRow.Min.X + addTileRadius, addRow.Center.Y);
        var addBand = RowBand(addRow, scale);
        var addHovered = UiInteract.Hover(addBand.Min, addBand.Max);
        if (addHovered)
        {
            drawList.AddRectFilled(addBand.Min, addBand.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        drawList.AddCircleFilled(addTileCenter, addTileRadius, ImGui.GetColorU32(ui.Accent), 32);
        PhoneIcon.Draw(drawList, addTileCenter, PhoneIcons.UserPlus, White, ActionTileGlyph * scale);
        var addLabelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(addTileCenter.X + addTileRadius + RowTextGap * scale,
            addRow.Center.Y - addLabelHeight * 0.5f), Loc.T(L.DirectMessages.AddPeople), ink.TitleInk, RowTitleStyle);
        if (UiInteract.Click(addBand.Min, addBand.Max, addHovered))
        {
            OpenAddMembers(conversation.Id);
        }

        var myId = store.MyUserId;
        for (var index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (!member.IsActive)
            {
                continue;
            }

            DrawMemberRow(drawList, card.NextRow(), conversation, member, member.UserId == myId, canManage, scale);
        }

        card.End();
        if (canManage)
        {
            var hintOrigin = ImGui.GetCursorScreenPos();
            var hintWidth = ScrollLayout.StableContentWidth();
            var hintHeight = Typography.DrawWrappedLeft(new Vector2(hintOrigin.X + 4f * scale, hintOrigin.Y + 8f * scale),
                Loc.T(L.Message.GroupMembersHint), ink.FaintInk, TextStyles.Footnote, hintWidth - 8f * scale);
            ImGui.SetCursorScreenPos(hintOrigin);
            ImGui.Dummy(new Vector2(hintWidth, hintHeight + 8f * scale));
        }

        DrawCardGap();
    }

    private void DrawMemberRow(ImDrawListPtr drawList, Rect row, ConversationDto conversation,
        ConversationMemberDto member, bool isMe, bool canManage, float scale)
    {
        var band = RowBand(row, scale);
        var interactive = !isMe;
        var hovered = interactive && UiInteract.Hover(band.Min, band.Max);
        if (hovered)
        {
            drawList.AddRectFilled(band.Min, band.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var radius = MemberAvatarRadius * scale;
        var avatarCenter = new Vector2(row.Min.X + radius, row.Center.Y);
        DrawMemberAvatar(drawList, member, avatarCenter, radius);
        var textLeft = avatarCenter.X + radius + RowTextGap * scale;
        var right = row.Max.X;
        if (member.Role == ChatRoles.Owner)
        {
            DrawRoleTag(drawList, right, row.Center.Y, Loc.T(L.DirectMessages.Owner), out right);
            right -= RowTrailingGap * scale;
        }
        else if (member.Role == ChatRoles.Admin)
        {
            DrawRoleTag(drawList, right, row.Center.Y, Loc.T(L.Message.Admin), out right);
            right -= RowTrailingGap * scale;
        }

        var label = isMe ? Loc.T(L.Message.You) : DirectMessagesStore.MemberLabel(member);
        var subtitle = member.Handle.Length > 0 ? "@" + member.Handle : string.Empty;
        var titleHeight = Typography.LineHeight(RowTitleStyle);
        var subHeight = subtitle.Length > 0 ? Typography.LineHeight(RowSubStyle) : 0f;
        var top = row.Center.Y - (titleHeight + (subtitle.Length > 0 ? RowLineGap * scale + subHeight : 0f)) * 0.5f;
        var maxWidth = MathF.Max(1f, right - textLeft);
        UserName.Draw(drawList, "messageapp.groupinfo.member." + member.UserId, label, member.Badges, member.BadgeIds,
            textLeft, top, maxWidth, RowTitleStyle, ink.TitleInk, hovered, theme);
        if (subtitle.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight + RowLineGap * scale),
                Typography.FitText(subtitle, maxWidth, RowSubStyle), ink.MutedInk, RowSubStyle);
        }

        if (interactive && UiInteract.Click(band.Min, band.Max, hovered))
        {
            OpenMemberSheet(conversation, member, canManage);
        }
    }

    private void OpenMemberSheet(ConversationDto conversation, ConversationMemberDto member, bool canManage)
    {
        memberSheetConversationId = conversation.Id;
        memberSheetUserId = member.UserId;
        memberSheetTitle = DirectMessagesStore.MemberLabel(member);
        var count = 0;
        var contact = contacts.Find(member.UserId);
        if (contact is { IsMutual: true })
        {
            memberSheetItems[count] = new ActionSheet.Item(Loc.T(L.DirectMessages.StartChat), PhoneIcons.MessageCircle);
            memberSheetActions[count++] = MemberActMessage;
        }

        if (contact is not null)
        {
            memberSheetItems[count] = new ActionSheet.Item(Loc.T(L.Message.ViewContact), PhoneIcons.UserCircle);
            memberSheetActions[count++] = MemberActView;
        }

        var myRole = store.MyRole;
        if (canManage && member.Role != ChatRoles.Owner)
        {
            if (member.Role == ChatRoles.Admin)
            {
                memberSheetItems[count] = new ActionSheet.Item(Loc.T(L.Message.DismissAdmin), PhoneIcons.ShieldCheck);
                memberSheetActions[count++] = MemberActDemote;
            }
            else
            {
                memberSheetItems[count] = new ActionSheet.Item(Loc.T(L.Message.MakeAdmin), PhoneIcons.ShieldCheck);
                memberSheetActions[count++] = MemberActPromote;
            }

            if (member.Role != ChatRoles.Admin || myRole == ChatRoles.Owner)
            {
                memberSheetItems[count] = new ActionSheet.Item(Loc.T(L.Message.RemoveFromGroup), PhoneIcons.Trash, true);
                memberSheetActions[count++] = MemberActRemove;
            }
        }

        memberSheetCount = count;
        if (count == 0)
        {
            return;
        }

        memberSheet.Open();
    }

    private void DrawMemberSheet(Rect screen)
    {
        if (!memberSheet.CapturesPointer)
        {
            return;
        }

        var picked = memberSheet.Draw(screen, ActionSheetStyle.From(ui), memberSheetItems.AsSpan(0, memberSheetCount),
            Loc.T(L.Common.Cancel), false, memberSheetTitle);
        if (picked < 0 || memberSheetConversationId is not { } conversationId || memberSheetUserId is not { } userId)
        {
            return;
        }

        switch (memberSheetActions[picked])
        {
            case MemberActMessage:
                if (contacts.Find(userId) is { } contact)
                {
                    StartMessage(contact);
                }

                break;
            case MemberActView:
                router.Push(MessageRoute.Contact(userId));
                break;
            case MemberActPromote:
                store.SetMemberRole(conversationId, userId, ChatRoles.Admin, ok => NoteGroupOutcome(ok));
                break;
            case MemberActDemote:
                store.SetMemberRole(conversationId, userId, ChatRoles.Member, ok => NoteGroupOutcome(ok));
                break;
            case MemberActRemove:
                store.RemoveMember(conversationId, userId, ok =>
                {
                    if (ok)
                    {
                        store.RefreshThreadDetail();
                    }

                    NoteGroupOutcome(ok);
                });
                break;
        }
    }

    private void NoteGroupOutcome(bool ok)
    {
        if (!ok)
        {
            ShellToast.Show(Loc.T(L.Message.SaveFailed));
        }
    }

    private void OpenGroupPhotoSheet(ConversationDto conversation)
    {
        groupPhotoConversationId = conversation.Id;
        var count = 0;
        groupPhotoSheetItems[count++] = new ActionSheet.Item(Loc.T(L.Common.ChangePhoto), PhoneIcons.Photo);
        if (!string.IsNullOrEmpty(conversation.AvatarUrl))
        {
            groupPhotoSheetItems[count++] = new ActionSheet.Item(Loc.T(L.Message.RemovePhoto), PhoneIcons.Trash, true);
        }

        groupPhotoSheetCount = count;
        groupPhotoSheet.Open();
    }

    private void DrawGroupPhotoSheet(Rect screen)
    {
        if (!groupPhotoSheet.CapturesPointer)
        {
            return;
        }

        var picked = groupPhotoSheet.Draw(screen, ActionSheetStyle.From(ui),
            groupPhotoSheetItems.AsSpan(0, groupPhotoSheetCount), Loc.T(L.Common.Cancel), false,
            Loc.T(L.Message.GroupPhoto));
        if (picked < 0 || groupPhotoConversationId is not { } conversationId)
        {
            return;
        }

        if (picked == 0)
        {
            groupPhotoPicker.Open();
            router.Push(MessageRoute.GroupPhoto(conversationId));
            return;
        }

        store.UpdateGroup(conversationId, new UpdateConversationRequest(AvatarUrl: string.Empty), NoteGroupOutcome);
    }

    private void DrawGroupPhoto(Rect area, string conversationId)
    {
        var context = new PhoneContext(area, theme, navigation);
        var labels = new ImagePickCropLabels(Loc.T(L.Message.GroupPhoto), Loc.T(L.Common.ImportFromPc),
            Loc.T(L.Common.NoPhotos), Loc.T(L.Account.MoveAndScale), Loc.T(L.Account.Use), Loc.T(L.Account.Saving),
            Loc.T(L.Account.GestureHint));
        var result = groupPhotoPicker.Draw(area, context, labels, ui.Accent, groupPhotoBusy);
        if (result == ImagePickCropEvent.Cancelled)
        {
            router.Pop();
            return;
        }

        if (result != ImagePickCropEvent.Committed || groupPhotoBusy)
        {
            return;
        }

        groupPhotoBusy = true;
        store.SetGroupPhoto(conversationId, groupPhotoPicker.SourcePath, groupPhotoPicker.Crop,
            ok => groupPhotoOutcome = ok ? 1 : 2);
    }

    private void OpenEditGroup(ConversationDto conversation)
    {
        editGroupLoadedFor = conversation.Id;
        editGroupTitle = conversation.Title;
        editGroupDescription = conversation.Description ?? string.Empty;
        router.Push(MessageRoute.EditGroup(conversation.Id));
    }

    private void DrawEditGroup(Rect area, string conversationId)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        DrawScreenHeader(area, Loc.T(L.Message.EditGroup));
        var conversation = store.Conversation;
        if (conversation is null || conversation.Id != conversationId)
        {
            return;
        }

        if (editGroupLoadedFor != conversationId)
        {
            editGroupLoadedFor = conversationId;
            editGroupTitle = conversation.Title;
            editGroupDescription = conversation.Description ?? string.Empty;
        }

        var sideInset = CellPadX * scale;
        var top = area.Min.Y + AppHeader.Height * scale + 12f * scale;
        var fieldRect = new Rect(new Vector2(area.Min.X + sideInset, top),
            new Vector2(area.Max.X - sideInset, top + FieldHeight * scale));
        PillField(fieldRect, "##msgEditGroupName", Loc.T(L.DirectMessages.RenameHint), ref editGroupTitle,
            GroupTitleMaxLength);
        var labelTop = fieldRect.Max.Y + 18f * scale;
        Typography.Draw(drawList, new Vector2(fieldRect.Min.X + 4f * scale, labelTop),
            Loc.Upper(Loc.T(L.Message.GroupDescription)), ink.FaintInk, SectionStyle);
        var cardMin = new Vector2(fieldRect.Min.X, labelTop + Typography.LineHeight(SectionStyle) + 8f * scale);
        var cardMax = new Vector2(fieldRect.Max.X, cardMin.Y + DescriptionFieldHeight * scale);
        ui.Card(drawList, cardMin, cardMax, Metrics.Radius.Md * scale);
        var pad = 12f * scale;
        ImGui.SetCursorScreenPos(cardMin + new Vector2(pad, pad));
        var inputWidth = cardMax.X - cardMin.X - pad * 2f;
        var wrapWidth = inputWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            SoftWrapField.Multiline("##msgEditGroupDescription", ref editGroupDescription, DescriptionMaxLength,
                new Vector2(inputWidth, cardMax.Y - cardMin.Y - pad * 2f), wrapWidth);
        }

        if (editGroupDescription.Length == 0)
        {
            ImGui.SetCursorScreenPos(cardMin + new Vector2(pad + 4f * scale, pad + 2f * scale));
            using (ImRaii.PushColor(ImGuiCol.Text, ui.MutedInk))
            {
                Typography.Plain(Loc.T(L.Message.AddDescription));
            }
        }

        var buttonTop = cardMax.Y + 18f * scale;
        var buttonRect = new Rect(new Vector2(fieldRect.Min.X, buttonTop),
            new Vector2(fieldRect.Max.X, buttonTop + FieldHeight * scale));
        var title = editGroupTitle.Trim();
        var description = editGroupDescription.Trim();
        var changed = !string.Equals(title, conversation.Title, StringComparison.Ordinal)
            || !string.Equals(description, conversation.Description ?? string.Empty, StringComparison.Ordinal);
        var canSave = changed && !editGroupBusy && title.Length > 0;
        if (ui.PillButton(buttonRect, editGroupBusy ? Loc.T(L.Account.Saving) : Loc.T(L.DirectMessages.Save), canSave)
            && canSave)
        {
            editGroupBusy = true;
            store.UpdateGroup(conversationId, new UpdateConversationRequest(title, description),
                ok => editGroupOutcome = ok ? 1 : 2);
        }
    }

    private void OpenAddMembers(string conversationId)
    {
        selectedContacts.Clear();
        filter = string.Empty;
        router.Push(MessageRoute.AddMembers(conversationId));
    }

    private void DrawAddMembers(Rect area, string conversationId)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        CollectMutualContacts(composeRows, excludeMembers: true);
        var selectedCount = CountSelected(composeRows);
        DrawScreenHeader(area, Loc.T(L.DirectMessages.AddPeople),
            subtitle: selectedCount > 0 ? Loc.T(L.Message.SelectedCount, selectedCount) : string.Empty);
        var top = area.Min.Y + AppHeader.Height * scale;
        if (composeRows.Count == 0 && filter.Trim().Length == 0)
        {
            EmptyState.Draw(new Rect(new Vector2(area.Min.X, top), area.Max), ui, PhoneIcons.UserPlus,
                Loc.T(L.DirectMessages.NoMutualTitle), Loc.T(L.DirectMessages.NoMutualFriends));
            return;
        }

        var searchRect = new Rect(new Vector2(area.Min.X + CellPadX * scale, top),
            new Vector2(area.Max.X - CellPadX * scale, top + ChatSearchHeight * scale));
        SearchField.Draw(searchRect, "##msgAddFilter", Loc.T(L.Phone.FilterHint), ref filter, ui.Palette);
        var actionHeight = selectedCount > 0 ? ComposeActionHeight * scale : 0f;
        var listRect = new Rect(new Vector2(area.Min.X, searchRect.Max.Y),
            new Vector2(area.Max.X, area.Max.Y - actionHeight));
        DrawPickList(listRect, composeRows);
        if (selectedCount == 0)
        {
            return;
        }

        var barTop = area.Max.Y - actionHeight;
        SocialChrome.PaintBarBackdrop(ui, drawList, new Rect(new Vector2(area.Min.X, barTop), area.Max), screenRect);
        var sideInset = CellPadX * scale;
        var buttonTop = barTop + 8f * scale;
        var buttonRect = new Rect(new Vector2(area.Min.X + sideInset, buttonTop),
            new Vector2(area.Max.X - sideInset, buttonTop + FieldHeight * scale));
        if (ui.PillButton(buttonRect, Loc.T(L.DirectMessages.Add), true) && !composeBusy)
        {
            var ids = SelectedIds(composeRows);
            if (ids.Length == 0)
            {
                return;
            }

            composeBusy = true;
            store.AddMembers(conversationId, ids, ok =>
            {
                composeBusy = false;
                if (ok)
                {
                    backToDetailPending = true;
                }
            });
        }
    }

    private void AskLeave(string conversationId)
    {
        var myId = store.MyUserId;
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.DirectMessages.ConfirmLeave),
            ConfirmLabel = Loc.T(L.Message.ExitGroup),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.DirectMessages.Leaving),
            FailedMessage = Loc.T(L.DirectMessages.LeaveFailed),
            Danger = true,
            Sheet = true,
            ConfirmAsync = done => store.RemoveMember(conversationId, myId, ok =>
            {
                if (ok)
                {
                    backToListPending = true;
                }

                done(ok);
            }),
        });
    }
}
