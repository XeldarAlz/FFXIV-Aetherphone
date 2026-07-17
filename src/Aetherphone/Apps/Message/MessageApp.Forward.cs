using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private string forwardFilter = string.Empty;
    private bool forwardBusy;
    private string? forwardOpenPending;

    private void DrawForwardPicker(Rect area, string messageId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Message.ForwardTitle), back);
        var message = FindMessage(messageId);
        if (message is null || message.Deleted)
        {
            return;
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var searchHeight = 52f * scale;
        SearchField.DrawSubmit(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)),
            "##forwardFilter", Loc.T(L.Phone.FilterHint), ref forwardFilter, AppPalettes.Message);
        var listRect = new Rect(new Vector2(area.Min.X, top + searchHeight), area.Max);
        var snapshot = store.Conversations;
        var query = forwardFilter.Trim();
        using (AppSurface.Begin(listRect))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var shown = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                var item = snapshot[index];
                if (query.Length > 0 && !DirectMessagesStore.DisplayTitle(item).Contains(query,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DrawForwardRow(item, message, scale);
                shown++;
            }

            if (shown == 0)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                    Loc.T(L.Phone.NoOneFound), ui.MutedInk);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawForwardRow(ConversationDto item, ChatMessageDto message, float scale)
    {
        var rowHeight = 56f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 16f * scale);
        var pad = 12f * scale;
        var radius = 19f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        var title = DirectMessagesStore.DisplayTitle(item);
        if (item.IsGroup)
        {
            drawList.AddCircleFilled(avatarCenter, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.85f)), 32);
            AppSkin.Icon(avatarCenter, FontAwesomeIcon.Users.ToIconString(), White, 0.9f);
        }
        else
        {
            AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, title, string.Empty, item.OtherAvatarUrl,
                images, lodestone, 0.9f, 32);
        }

        var textLeft = avatarCenter.X + radius + 12f * scale;
        Typography.Draw(new Vector2(textLeft, origin.Y + rowHeight * 0.5f - 9f * scale),
            Typography.FitText(title, origin.X + width - pad - textLeft, 1f, FontWeight.SemiBold),
            theme.TextStrong, 1f, FontWeight.SemiBold);
        AppSkin.Icon(new Vector2(origin.X + width - pad - 8f * scale, origin.Y + rowHeight * 0.5f),
            FontAwesomeIcon.Share.ToIconString(), ui.MutedInk, 0.85f);
        if (UiInteract.HoverClick(origin, new Vector2(origin.X + width, origin.Y + rowHeight)) && !forwardBusy)
        {
            forwardBusy = true;
            store.ForwardMessage(message, item.Id, _ => forwardBusy = false);
            forwardOpenPending = item.Id;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }
}
