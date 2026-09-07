using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const float PickerRowHeight = 62f;
    private const float PickerRowAvatarRadius = 22f;

    private string forwardFilter = string.Empty;
    private bool forwardBusy;

    private void DrawForwardPicker(Rect area, string messageId)
    {
        var message = store.FindMessage(messageId);
        if (message is null || message.Deleted)
        {
            DrawScreenHeader(area, Loc.T(L.Message.ForwardTitle));
            return;
        }

        if (DrawConversationPicker(area, Loc.T(L.Message.ForwardTitle), ref forwardFilter) is not { } target ||
            forwardBusy)
        {
            return;
        }

        forwardBusy = true;
        store.ForwardMessage(message, target.Id, _ => forwardBusy = false);
        forwardOpenPending = target.Id;
    }

    private ConversationDto? DrawConversationPicker(Rect area, string title, ref string filterText)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, title);
        var top = area.Min.Y + AppHeader.Height * scale;
        var searchRect = new Rect(new Vector2(area.Min.X + CellPadX * scale, top),
            new Vector2(area.Max.X - CellPadX * scale, top + ChatSearchHeight * scale));
        SearchField.Draw(searchRect, "##conversationPickerFilter", Loc.T(L.Common.Search), ref filterText, ui.Palette);
        var listRect = new Rect(new Vector2(area.Min.X, searchRect.Max.Y), area.Max);
        var snapshot = store.Conversations;
        var query = filterText.Trim();
        ConversationDto? picked = null;
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            var drawList = ImGui.GetWindowDrawList();
            var shown = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (!PickerMatches(snapshot[index], query))
                {
                    continue;
                }

                shown++;
                if (DrawConversationPickerRow(drawList, snapshot[index]))
                {
                    picked = snapshot[index];
                }
            }

            if (shown == 0)
            {
                DrawInlineEmpty(drawList, Loc.T(L.Phone.NoOneFound));
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }

        return picked;
    }

    private static bool PickerMatches(ConversationDto item, string query) =>
        query.Length == 0 ||
        DirectMessagesStore.DisplayTitle(item).Contains(query, StringComparison.OrdinalIgnoreCase);

    private bool DrawConversationPickerRow(ImDrawListPtr drawList, ConversationDto item)
    {
        var scale = UiScale.Current;
        var row = BeginPersonRow(drawList, PickerRowHeight, PickerRowAvatarRadius, ChevronSize * scale, true,
            out var avatarCenter);
        DrawConversationAvatar(drawList, item, avatarCenter, PickerRowAvatarRadius * scale);
        var subtitle = item.IsGroup ? Loc.T(L.DirectMessages.MembersCount, item.MemberCount) : string.Empty;
        DrawRowTitleAndSub(drawList, new MarqueeId("picker.row.", item.Id), DirectMessagesStore.DisplayTitle(item),
            subtitle, row.TextLeft, row.TextRight, row.Bounds.Center.Y, ink.TitleInk, ink.MutedInk);
        PhoneIcon.Draw(drawList, new Vector2(row.Bounds.Max.X - CellPadX * scale - ChevronSize * 0.5f * scale,
            row.Bounds.Center.Y), PhoneIcons.ArrowForwardUp, ink.FaintInk, ChevronSize * scale);
        EndPersonRow(drawList, row);
        return row.Tapped;
    }
}
