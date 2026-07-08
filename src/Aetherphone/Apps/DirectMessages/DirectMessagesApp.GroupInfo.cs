using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.DirectMessages;

internal sealed partial class DirectMessagesApp
{
    private string renameDraft = string.Empty;
    private string? renameLoadedFor;
    private volatile bool backToListPending;

    private void DrawGroupInfo(Rect area, string conversationId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.DirectMessages.Details), back);
        var conversation = store.Conversation;
        if (conversation is null || !conversation.IsGroup || conversation.Id != conversationId)
        {
            return;
        }

        if (renameLoadedFor != conversationId)
        {
            renameLoadedFor = conversationId;
            renameDraft = conversation.Title;
        }

        var owner = IsOwner();
        var top = area.Min.Y + AppHeader.Height * scale;
        var sideInset = 16f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            if (owner)
            {
                DrawRenameRow(conversationId, sideInset, scale);
            }

            DrawSectionLabel(Loc.T(L.DirectMessages.Members), scale);
            var members = store.Members;
            for (var index = 0; index < members.Length; index++)
            {
                DrawMemberRow(conversationId, members[index], owner, scale);
            }

            ImGui.Dummy(new Vector2(0f, 10f * scale));
            var addRect = ActionRect(sideInset, scale);
            if (ui.PillButton(addRect, Loc.T(L.DirectMessages.AddPeople), true))
            {
                selectedContacts.Clear();
                filter = string.Empty;
                router.Push(DmRoute.AddMembers(conversationId));
            }

            ImGui.Dummy(new Vector2(0f, 10f * scale));
            var leaveRect = ActionRect(sideInset, scale);
            if (ui.DangerGhostButton(leaveRect, Loc.T(L.DirectMessages.LeaveChat)))
            {
                AskLeave(conversationId);
            }

            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void DrawRenameRow(string conversationId, float sideInset, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var fieldHeight = 46f * scale;
        var buttonWidth = 84f * scale;
        var gap = 8f * scale;
        var fieldRect = new Rect(new Vector2(origin.X + sideInset, origin.Y),
            new Vector2(origin.X + width - sideInset - buttonWidth - gap, origin.Y + fieldHeight));
        PillField(fieldRect, "##dmRename", Loc.T(L.DirectMessages.RenameHint), ref renameDraft, 60);
        var buttonRect = new Rect(new Vector2(fieldRect.Max.X + gap, origin.Y),
            new Vector2(origin.X + width - sideInset, origin.Y + fieldHeight));
        var canSave = renameDraft.Trim().Length > 0 && renameDraft.Trim() != (store.Conversation?.Title ?? string.Empty);
        if (ui.PillButton(buttonRect, Loc.T(L.DirectMessages.Save), canSave) && canSave)
        {
            store.Rename(conversationId, renameDraft.Trim(), _ => { });
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, fieldHeight + 16f * scale));
    }

    private void DrawSectionLabel(string label, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(new Vector2(origin.X + 20f * scale, origin.Y), label, AppPalettes.Messenger.MutedInk,
            TextStyles.FootnoteEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 24f * scale));
    }

    private void DrawMemberRow(string conversationId, ConversationMemberDto member, bool viewerIsOwner, float scale)
    {
        var rowHeight = 52f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 14f * scale);
        var pad = 12f * scale;
        var radius = 17f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        var label = member.DisplayName.Length > 0 ? member.DisplayName : member.Handle;
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, label, string.Empty, member.AvatarUrl, images,
            lodestone, 0.85f, 32);
        var textLeft = avatarCenter.X + radius + 12f * scale;
        Typography.Draw(new Vector2(textLeft, origin.Y + rowHeight * 0.5f - 9f * scale), label, theme.TextStrong, 1f,
            FontWeight.SemiBold);
        if (member.Role == 1)
        {
            var ownerLabel = Loc.T(L.DirectMessages.Owner);
            Typography.Draw(new Vector2(origin.X + width - pad - Typography.Measure(ownerLabel,
                TextStyles.Footnote).X, origin.Y + rowHeight * 0.5f - 7f * scale), ownerLabel,
                AppPalettes.Messenger.MutedInk, TextStyles.Footnote);
        }
        else if (viewerIsOwner && member.UserId != store.MyUserId)
        {
            var removeCenter = new Vector2(origin.X + width - pad - 6f * scale, origin.Y + rowHeight * 0.5f);
            if (ui.IconButton(removeCenter, 14f * scale, FontAwesomeIcon.Times.ToIconString(),
                    AppPalettes.Messenger.MutedInk, AppSkin.Transparent, 0.9f, Loc.T(L.Common.Close)))
            {
                store.RemoveMember(conversationId, member.UserId, ok =>
                {
                    if (ok)
                    {
                        store.RefreshDetail();
                    }
                });
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private void DrawAddMembers(Rect area, string conversationId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.DirectMessages.AddPeople), back);
        var top = area.Min.Y + AppHeader.Height * scale;
        var candidates = AddableContacts();
        if (candidates.Count == 0)
        {
            var body = new Rect(new Vector2(area.Min.X, top), area.Max);
            EmptyState.Draw(body, ui, FontAwesomeIcon.UserPlus, Loc.T(L.DirectMessages.NoMutualTitle),
                Loc.T(L.DirectMessages.NoMutualFriends));
            return;
        }

        var searchHeight = 52f * scale;
        SearchField.DrawSubmit(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)),
            "##dmAddFilter", Loc.T(L.Phone.FilterHint), ref filter, AppPalettes.Messenger);
        var selectedCount = CountSelected(candidates);
        var actionHeight = selectedCount > 0 ? 62f * scale : 0f;
        var listRect = new Rect(new Vector2(area.Min.X, top + searchHeight),
            new Vector2(area.Max.X, area.Max.Y - actionHeight));
        using (AppSurface.Begin(listRect))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < candidates.Count; index++)
            {
                DrawPickRow(candidates[index], scale);
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }

        if (actionHeight > 0f)
        {
            var sideInset = 16f * scale;
            var buttonTop = area.Max.Y - 62f * scale + 8f * scale;
            var buttonRect = new Rect(new Vector2(area.Min.X + sideInset, buttonTop),
                new Vector2(area.Max.X - sideInset, buttonTop + 46f * scale));
            if (ui.PillButton(buttonRect, Loc.T(L.DirectMessages.Add), true) && !composeBusy)
            {
                var ids = SelectedIds(candidates);
                if (ids.Length > 0)
                {
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
        }
    }

    private volatile bool backToDetailPending;

    private List<ContactDto> AddableContacts()
    {
        var members = store.Members;
        var existing = new HashSet<string>(members.Length);
        for (var index = 0; index < members.Length; index++)
        {
            existing.Add(members[index].UserId);
        }

        var snapshot = contacts.Contacts;
        var list = new List<ContactDto>(snapshot.Length);
        var query = filter.Trim();
        for (var index = 0; index < snapshot.Length; index++)
        {
            var contact = snapshot[index];
            if (!contact.IsMutual || existing.Contains(contact.UserId))
            {
                continue;
            }

            if (query.Length == 0 || ContactBook.DisplayLabel(contact).Contains(query,
                    StringComparison.OrdinalIgnoreCase))
            {
                list.Add(contact);
            }
        }

        list.Sort(static (left, right) => string.Compare(ContactBook.DisplayLabel(left), ContactBook.DisplayLabel(right),
            StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private bool IsOwner()
    {
        var members = store.Members;
        var myId = store.MyUserId;
        for (var index = 0; index < members.Length; index++)
        {
            if (members[index].UserId == myId)
            {
                return members[index].Role == 1;
            }
        }

        return false;
    }

    private Rect ActionRect(float sideInset, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rect = new Rect(new Vector2(origin.X + sideInset, origin.Y),
            new Vector2(origin.X + width - sideInset, origin.Y + 46f * scale));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 46f * scale));
        return rect;
    }

    private void AskLeave(string conversationId)
    {
        var myId = store.MyUserId;
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.DirectMessages.ConfirmLeave),
            ConfirmLabel = Loc.T(L.DirectMessages.LeaveChat),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.DirectMessages.Leaving),
            FailedMessage = Loc.T(L.DirectMessages.LeaveFailed),
            Danger = true,
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
