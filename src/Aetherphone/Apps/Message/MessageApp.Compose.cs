using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private readonly HashSet<string> selectedContacts = new();
    private string groupTitleDraft = string.Empty;
    private volatile bool composeBusy;
    private volatile string? composeResult;
    private volatile bool backToListPending;
    private volatile bool backToDetailPending;

    private void DrawNewChat(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.DirectMessages.NewMessage), back);
        var top = area.Min.Y + AppHeader.Height * scale;
        var mutual = MutualContacts();
        if (mutual.Count == 0)
        {
            var body = new Rect(new Vector2(area.Min.X, top), area.Max);
            EmptyState.Draw(body, ui, FontAwesomeIcon.UserPlus, Loc.T(L.DirectMessages.NoMutualTitle),
                Loc.T(L.DirectMessages.NoMutualFriends));
            return;
        }

        var searchHeight = 52f * scale;
        SearchField.DrawSubmit(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)),
            "##msgNewFilter", Loc.T(L.Phone.FilterHint), ref filter, AppPalettes.Message);

        var selectedCount = CountSelected(mutual);
        var actionHeight = selectedCount >= 2 ? 116f * scale : (selectedCount == 1 ? 62f * scale : 0f);
        var listRect = new Rect(new Vector2(area.Min.X, top + searchHeight),
            new Vector2(area.Max.X, area.Max.Y - actionHeight));
        using (AppSurface.Begin(listRect))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < mutual.Count; index++)
            {
                DrawPickRow(mutual[index], scale);
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }

        if (actionHeight > 0f)
        {
            DrawComposeAction(area, mutual, selectedCount, scale);
        }
    }

    private void DrawComposeAction(Rect area, List<ContactDto> mutual, int selectedCount, float scale)
    {
        var sideInset = 16f * scale;
        var buttonHeight = 46f * scale;
        if (selectedCount >= 2)
        {
            var fieldTop = area.Max.Y - 116f * scale + 8f * scale;
            var fieldRect = new Rect(new Vector2(area.Min.X + sideInset, fieldTop),
                new Vector2(area.Max.X - sideInset, fieldTop + buttonHeight));
            PillField(fieldRect, "##msgGroupName", Loc.T(L.DirectMessages.GroupNameHint), ref groupTitleDraft, 60);
            var buttonTop = fieldRect.Max.Y + 10f * scale;
            var buttonRect = new Rect(new Vector2(area.Min.X + sideInset, buttonTop),
                new Vector2(area.Max.X - sideInset, buttonTop + buttonHeight));
            if (ui.PillButton(buttonRect, Loc.T(L.DirectMessages.CreateGroup), true) && !composeBusy)
            {
                SubmitGroup(mutual);
            }
        }
        else
        {
            var buttonTop = area.Max.Y - 62f * scale + 8f * scale;
            var buttonRect = new Rect(new Vector2(area.Min.X + sideInset, buttonTop),
                new Vector2(area.Max.X - sideInset, buttonTop + buttonHeight));
            if (ui.PillButton(buttonRect, Loc.T(L.DirectMessages.StartChat), true) && !composeBusy)
            {
                SubmitDirect(mutual);
            }
        }
    }

    private void SubmitDirect(List<ContactDto> mutual)
    {
        var target = FirstSelected(mutual);
        if (target is null)
        {
            return;
        }

        composeBusy = true;
        store.CreateDirect(target, id =>
        {
            composeBusy = false;
            if (!string.IsNullOrEmpty(id))
            {
                composeResult = id;
            }
        });
    }

    private void SubmitGroup(List<ContactDto> mutual)
    {
        var ids = SelectedIds(mutual);
        if (ids.Length < 2)
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

    private void DrawPickRow(ContactDto contact, float scale)
    {
        var rowHeight = 56f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var selected = selectedContacts.Contains(contact.UserId);
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 16f * scale, elevated: selected);
        var pad = 12f * scale;
        var radius = 18f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        var label = ContactBook.DisplayLabel(contact);
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, label, string.Empty, contact.AvatarUrl, images,
            lodestone, 0.85f, 32);
        var textLeft = avatarCenter.X + radius + 12f * scale;
        var labelWidth = origin.X + width - 44f * scale - textLeft;
        Typography.Draw(new Vector2(textLeft, origin.Y + rowHeight * 0.5f - 9f * scale),
            Typography.FitText(label, labelWidth, 1f, FontWeight.SemiBold), theme.TextStrong, 1f, FontWeight.SemiBold);
        var checkCenter = new Vector2(origin.X + width - 22f * scale, origin.Y + rowHeight * 0.5f);
        if (selected)
        {
            drawList.AddCircleFilled(checkCenter, 11f * scale, ImGui.GetColorU32(ui.Accent), 24);
            AppSkin.Icon(checkCenter, FontAwesomeIcon.Check.ToIconString(), White, 0.7f);
        }
        else
        {
            drawList.AddCircle(checkCenter, 11f * scale, ImGui.GetColorU32(ui.MutedInk), 24, 1.5f);
        }

        if (UiInteract.HoverClick(origin, new Vector2(origin.X + width, origin.Y + rowHeight)))
        {
            if (!selectedContacts.Add(contact.UserId))
            {
                selectedContacts.Remove(contact.UserId);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private List<ContactDto> MutualContacts()
    {
        var snapshot = contacts.Contacts;
        var list = new List<ContactDto>(snapshot.Length);
        var query = filter.Trim();
        for (var index = 0; index < snapshot.Length; index++)
        {
            var contact = snapshot[index];
            if (!contact.IsMutual)
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

    private string? FirstSelected(List<ContactDto> mutual)
    {
        for (var index = 0; index < mutual.Count; index++)
        {
            if (selectedContacts.Contains(mutual[index].UserId))
            {
                return mutual[index].UserId;
            }
        }

        return null;
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
        var scale = ImGuiHelpers.GlobalScale;
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
