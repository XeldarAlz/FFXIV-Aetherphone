using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Linkpearl;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const byte ChatMenuCopyText = 0;
    private const byte ChatMenuCopyName = 1;

    private readonly DropdownMenu chatMenu = new();
    private readonly DropdownMenu.Item[] chatMenuItems = new DropdownMenu.Item[2];
    private readonly byte[] chatMenuActions = new byte[2];
    private string chatMenuText = string.Empty;
    private string? chatMenuName;
    private Vector2 chatMenuAnchor;
    private bool chatMenuPending;
    private int chatMenuToken;

    private void OpenChatMenu(ChatLine line, string? name)
    {
        chatMenuText = line.Text;
        chatMenuName = string.IsNullOrWhiteSpace(name) ? null : name;
        chatMenuAnchor = ImGui.GetMousePos();
        chatMenuPending = true;
        chatMenuToken++;
    }

    private void DrawChatMenu(Rect area)
    {
        var id = chatMenuToken.ToString(Loc.Culture);
        if (chatMenuPending)
        {
            chatMenuPending = false;
            chatMenu.Toggle(id, new Rect(chatMenuAnchor, chatMenuAnchor + new Vector2(1f, 1f)));
        }

        if (!chatMenu.IsOpenFor(id))
        {
            return;
        }

        var count = 0;
        chatMenuItems[count] = new DropdownMenu.Item(Loc.T(L.Messages.CopyMessage), FontAwesomeIcon.Copy.ToIconString());
        chatMenuActions[count++] = ChatMenuCopyText;
        if (chatMenuName is not null)
        {
            chatMenuItems[count] = new DropdownMenu.Item(Loc.T(L.Messages.CopyName), FontAwesomeIcon.User.ToIconString());
            chatMenuActions[count++] = ChatMenuCopyName;
        }

        var clicked = chatMenu.Draw(area, frameTheme, chatMenuItems.AsSpan(0, count));
        if (clicked < 0)
        {
            return;
        }

        switch (chatMenuActions[clicked])
        {
            case ChatMenuCopyText:
                ImGui.SetClipboardText(chatMenuText);
                break;
            case ChatMenuCopyName when chatMenuName is { } name:
                ImGui.SetClipboardText(name);
                break;
        }
    }

    private static string? SenderName(ChatLine line)
    {
        if (line.Direction != MessageDirection.Incoming || line.Author is not { } author)
        {
            return null;
        }

        return author.Name;
    }
}
