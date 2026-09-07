using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const float PickRowHeight = 60f;
    private const float PickRowAvatarRadius = 21f;
    private const float PickCheckSize = 24f;
    private const float ComposeActionHeight = 62f;
    private const float ComposeGroupActionHeight = 118f;
    private const int GroupTitleMaxLength = 60;

    private readonly List<ContactDto> composeRows = new();
    private volatile bool composeBusy;

    private void DrawNewChat(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Message.NewChat));
        var top = area.Min.Y + AppHeader.Height * scale;
        var searchRect = new Rect(new Vector2(area.Min.X + CellPadX * scale, top),
            new Vector2(area.Max.X - CellPadX * scale, top + ChatSearchHeight * scale));
        SearchField.Draw(searchRect, "##msgNewFilter", Loc.T(L.Phone.FilterHint), ref filter, ui.Palette);
        var listRect = new Rect(new Vector2(area.Min.X, searchRect.Max.Y), area.Max);
        CollectMutualContacts(composeRows, excludeMembers: false);
        var query = filter.Trim();
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            var drawList = ImGui.GetWindowDrawList();
            if (query.Length == 0)
            {
                if (DrawActionRow(drawList, PhoneIcons.Users, Loc.T(L.Message.NewGroup)))
                {
                    selectedContacts.Clear();
                    groupTitleDraft = string.Empty;
                    filter = string.Empty;
                    router.Push(MessageRoute.NewGroup);
                }

                if (DrawActionRow(drawList, PhoneIcons.UserPlus, Loc.T(L.Message.NewContact)))
                {
                    addError = string.Empty;
                    router.Push(MessageRoute.AddContact);
                }
            }

            if (composeRows.Count == 0)
            {
                DrawInlineEmpty(drawList, query.Length > 0
                    ? Loc.T(L.Phone.NoOneFound)
                    : Loc.T(L.DirectMessages.NoMutualFriends));
            }
            else
            {
                DrawSectionLabel(Loc.T(L.Phone.ContactsSection));
                var lastLetter = string.Empty;
                for (var index = 0; index < composeRows.Count; index++)
                {
                    var contact = composeRows[index];
                    var letter = TrimmedLetter(ContactBook.DisplayLabel(contact));
                    if (!string.Equals(letter, lastLetter, StringComparison.Ordinal))
                    {
                        DrawLetterHeader(drawList, letter);
                        lastLetter = letter;
                    }

                    var row = BeginPersonRow(drawList, PickRowHeight, PickRowAvatarRadius, 0f, true, out var avatarCenter);
                    DrawContactAvatar(drawList, contact, avatarCenter, PickRowAvatarRadius * scale);
                    DrawRowTitleAndSub(drawList, new MarqueeId("compose.contact.", contact.UserId),
                        ContactBook.DisplayLabel(contact), ContactBook.Format(contact.PhoneNumber), row.TextLeft,
                        row.TextRight, row.Bounds.Center.Y, ink.TitleInk, ink.MutedInk);
                    if (row.Tapped)
                    {
                        StartMessage(contact);
                    }

                    EndPersonRow(drawList, row);
                }
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawNewGroup(Rect area)
    {
        var scale = UiScale.Current;
        CollectMutualContacts(composeRows, excludeMembers: false);
        var selectedCount = CountSelected(composeRows);
        DrawScreenHeader(area, Loc.T(L.Message.NewGroup),
            subtitle: selectedCount > 0 ? Loc.T(L.Message.SelectedCount, selectedCount) : string.Empty);
        var top = area.Min.Y + AppHeader.Height * scale;
        if (contacts.Contacts.Length == 0)
        {
            EmptyState.Draw(new Rect(new Vector2(area.Min.X, top), area.Max), ui, PhoneIcons.UserPlus,
                Loc.T(L.DirectMessages.NoMutualTitle), Loc.T(L.DirectMessages.NoMutualFriends));
            return;
        }

        var searchRect = new Rect(new Vector2(area.Min.X + CellPadX * scale, top),
            new Vector2(area.Max.X - CellPadX * scale, top + ChatSearchHeight * scale));
        SearchField.Draw(searchRect, "##msgGroupFilter", Loc.T(L.Phone.FilterHint), ref filter, ui.Palette);
        var actionHeight = selectedCount >= 1 ? ComposeGroupActionHeight * scale : 0f;
        var listRect = new Rect(new Vector2(area.Min.X, searchRect.Max.Y),
            new Vector2(area.Max.X, area.Max.Y - actionHeight));
        DrawPickList(listRect, composeRows);
        if (selectedCount >= 1)
        {
            DrawGroupCreateBar(area, actionHeight, scale);
        }
    }

    private void DrawPickList(Rect listRect, List<ContactDto> rows)
    {
        var scale = UiScale.Current;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            var drawList = ImGui.GetWindowDrawList();
            if (rows.Count == 0)
            {
                DrawInlineEmpty(drawList, Loc.T(L.Phone.NoOneFound));
            }

            var lastLetter = string.Empty;
            for (var index = 0; index < rows.Count; index++)
            {
                var letter = TrimmedLetter(ContactBook.DisplayLabel(rows[index]));
                if (!string.Equals(letter, lastLetter, StringComparison.Ordinal))
                {
                    DrawLetterHeader(drawList, letter);
                    lastLetter = letter;
                }

                DrawPickRow(drawList, rows[index]);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawGroupCreateBar(Rect area, float actionHeight, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var barTop = area.Max.Y - actionHeight;
        SocialChrome.PaintBarBackdrop(ui, drawList, new Rect(new Vector2(area.Min.X, barTop), area.Max), screenRect);
        drawList.AddLine(new Vector2(area.Min.X, barTop), new Vector2(area.Max.X, barTop), ImGui.GetColorU32(ui.Hairline),
            1f);
        var sideInset = CellPadX * scale;
        var buttonHeight = FieldHeight * scale;
        var fieldTop = barTop + 10f * scale;
        var fieldRect = new Rect(new Vector2(area.Min.X + sideInset, fieldTop),
            new Vector2(area.Max.X - sideInset, fieldTop + buttonHeight));
        var submitted = PillField(fieldRect, "##msgGroupName", Loc.T(L.DirectMessages.GroupNameHint),
            ref groupTitleDraft, GroupTitleMaxLength);
        var buttonTop = fieldRect.Max.Y + 10f * scale;
        var buttonRect = new Rect(new Vector2(area.Min.X + sideInset, buttonTop),
            new Vector2(area.Max.X - sideInset, buttonTop + buttonHeight));
        if ((ui.PillButton(buttonRect, Loc.T(L.DirectMessages.CreateGroup), true) || submitted) && !composeBusy)
        {
            SubmitGroup(composeRows);
        }
    }

    private void SubmitGroup(List<ContactDto> mutual)
    {
        var ids = SelectedIds(mutual);
        if (ids.Length < 1)
        {
            return;
        }

        composeBusy = true;
        store.CreateGroup(groupTitleDraft.Trim(), ids, id =>
        {
            composeBusy = false;
            if (!string.IsNullOrEmpty(id))
            {
                composeResult = id;
            }
        });
    }

    private void DrawPickRow(ImDrawListPtr drawList, ContactDto contact)
    {
        var scale = UiScale.Current;
        var selected = selectedContacts.Contains(contact.UserId);
        var row = BeginPersonRow(drawList, PickRowHeight, PickRowAvatarRadius, PickCheckSize * scale, true,
            out var avatarCenter);
        DrawContactAvatar(drawList, contact, avatarCenter, PickRowAvatarRadius * scale);
        DrawRowTitleAndSub(drawList, new MarqueeId("compose.pick.", contact.UserId), ContactBook.DisplayLabel(contact),
            ContactBook.Format(contact.PhoneNumber), row.TextLeft, row.TextRight, row.Bounds.Center.Y, ink.TitleInk,
            ink.MutedInk);
        var checkCenter = new Vector2(row.Bounds.Max.X - CellPadX * scale - PickCheckSize * 0.5f * scale,
            row.Bounds.Center.Y);
        PhoneIcon.Draw(drawList, checkCenter, selected ? PhoneIcons.CircleCheckFilled : PhoneIcons.Circle,
            selected ? ink.Accent : ink.FaintInk, PickCheckSize * scale);
        if (row.Tapped)
        {
            if (!selectedContacts.Add(contact.UserId))
            {
                selectedContacts.Remove(contact.UserId);
            }
        }

        EndPersonRow(drawList, row);
    }

    private void CollectMutualContacts(List<ContactDto> target, bool excludeMembers)
    {
        target.Clear();
        var snapshot = contacts.Contacts;
        var query = filter.Trim();
        var members = excludeMembers ? store.Members : Array.Empty<ConversationMemberDto>();
        for (var index = 0; index < snapshot.Length; index++)
        {
            var contact = snapshot[index];
            if (!contact.IsMutual)
            {
                continue;
            }

            if (excludeMembers && IsActiveMember(members, contact.UserId))
            {
                continue;
            }

            if (query.Length == 0 || MatchesContact(contact, query))
            {
                target.Add(contact);
            }
        }

        target.Sort(CompareContactsByLabel);
    }

    private static bool IsActiveMember(ConversationMemberDto[] members, string userId)
    {
        for (var index = 0; index < members.Length; index++)
        {
            if (members[index].IsActive && members[index].UserId == userId)
            {
                return true;
            }
        }

        return false;
    }

    private int CountSelected(List<ContactDto> mutual)
    {
        var count = 0;
        for (var index = 0; index < mutual.Count; index++)
        {
            if (selectedContacts.Contains(mutual[index].UserId))
            {
                count++;
            }
        }

        return count;
    }

    private string[] SelectedIds(List<ContactDto> mutual)
    {
        var ids = new List<string>(selectedContacts.Count);
        for (var index = 0; index < mutual.Count; index++)
        {
            if (selectedContacts.Contains(mutual[index].UserId))
            {
                ids.Add(mutual[index].UserId);
            }
        }

        return ids.ToArray();
    }

    private bool PillField(Rect rect, string imguiId, string hint, ref string value, int maxLength)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, (rect.Max.Y - rect.Min.Y) * 0.5f,
            ImGui.GetColorU32(ui.FieldSurface));
        ImGui.SetNextItemWidth(rect.Width - 36f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (Plugin.Fonts.Push(1.05f))
        {
            ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + 18f * scale,
                rect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
            return ImGui.InputTextWithHint(imguiId, hint, ref value, maxLength, ImGuiInputTextFlags.EnterReturnsTrue);
        }
    }
}
