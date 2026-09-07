using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal sealed class GameChatMenu
{
    private const byte ActionCopyText = 0;
    private const byte ActionCopyName = 1;
    private const byte ActionSendTell = 2;
    private const byte ActionLookUp = 3;
    private const byte ActionInviteParty = 4;
    private const byte ActionOpenUrl = 5;
    private const byte ActionTryOn = 6;
    private const byte ActionCompare = 7;
    private const byte ActionRecipes = 8;
    private const byte ActionFindItem = 9;
    private const byte ActionLinkInChat = 10;
    private const byte ActionOpenMarket = 11;
    private const byte ActionOpenMap = 12;
    private const byte ActionFriendRequest = 13;
    private const byte ActionAdventurerPlate = 14;
    private const byte ActionTargetPlayer = 15;
    private const byte ActionBlacklist = 16;

    private readonly DropdownMenu menu = new();
    private readonly List<DropdownMenu.Item> items = new(12);
    private readonly List<byte> actions = new(12);
    private readonly string id;
    private string text = string.Empty;
    private string name = string.Empty;
    private string world = string.Empty;
    private PlayerActionAvailability playerActions;
    private ChatChunk link;
    private bool isLink;
    private Vector2 anchor;
    private bool pending;
    private int token;

    public GameChatMenu(string id)
    {
        this.id = id;
    }

    public Action<string, string>? SendTell { get; set; }

    public Action<string, string>? LookUp { get; set; }

    public Action<uint>? OpenMarket { get; set; }

    public bool IsOpen => menu.Open;

    public bool Detached
    {
        get => menu.Detached;
        set => menu.Detached = value;
    }

    public void Gate() => menu.Gate();

    public void Close() => menu.Close();

    public void Open(ChatEntry entry)
    {
        text = entry.Text;
        name = entry.IsSelf ? string.Empty : entry.AuthorName;
        world = entry.IsSelf ? string.Empty : entry.AuthorWorld;
        isLink = false;
        Arm();
    }

    public void OpenLink(ChatEntry entry, ChatChunk chunk)
    {
        text = chunk.Text;
        name = chunk.Kind == ChatChunkKind.Player ? chunk.Text : entry.AuthorName;
        world = chunk.Kind == ChatChunkKind.Player ? chunk.World : entry.AuthorWorld;
        link = chunk;
        isLink = true;
        Arm();
    }

    public void Draw(Rect screen, PhoneTheme theme)
    {
        var menuId = string.Concat(id, token.ToString(Loc.Culture));
        if (pending)
        {
            pending = false;
            menu.Toggle(menuId, new Rect(anchor, anchor + new Vector2(1f, 1f)));
        }

        if (!menu.IsOpenFor(menuId))
        {
            return;
        }

        items.Clear();
        actions.Clear();
        if (isLink)
        {
            BuildLinkItems();
        }
        else
        {
            BuildMessageItems();
        }

        var clicked = menu.Draw(screen, theme, CollectionsMarshal.AsSpan(items));
        if (clicked < 0)
        {
            return;
        }

        Run(actions[clicked]);
    }

    private void Arm()
    {
        playerActions = PlayerActions.Resolve(name, world);
        anchor = ImGui.GetMousePos();
        pending = true;
        token++;
    }

    private void BuildMessageItems()
    {
        Add(L.Messages.CopyMessage, FontAwesomeIcon.Copy, ActionCopyText);
        if (name.Length == 0)
        {
            return;
        }

        Add(L.Messages.CopyName, FontAwesomeIcon.User, ActionCopyName);
        if (SendTell is not null)
        {
            Add(L.Linkpearl.SendTell, FontAwesomeIcon.PenAlt, ActionSendTell);
        }

        if (LookUp is not null)
        {
            Add(L.Linkpearl.LookUp, FontAwesomeIcon.Search, ActionLookUp);
        }

        AddPlayerItems();
    }

    private void AddPlayerItems()
    {
        if (playerActions.Invite)
        {
            Add(L.Linkpearl.InviteToParty, FontAwesomeIcon.UserPlus, ActionInviteParty);
        }

        if (playerActions.FriendRequest)
        {
            Add(L.Linkpearl.SendFriendRequest, FontAwesomeIcon.UserFriends, ActionFriendRequest);
        }

        if (playerActions.AdventurerPlate)
        {
            Add(L.Linkpearl.AdventurerPlate, FontAwesomeIcon.IdCard, ActionAdventurerPlate);
        }

        if (playerActions.Target)
        {
            Add(L.Linkpearl.TargetPlayer, FontAwesomeIcon.Crosshairs, ActionTargetPlayer);
        }

        if (playerActions.Blacklist)
        {
            Add(L.Linkpearl.AddToBlacklist, FontAwesomeIcon.UserSlash, ActionBlacklist, danger: true);
        }
    }

    private void BuildLinkItems()
    {
        switch (link.Kind)
        {
            case ChatChunkKind.Url:
                Add(L.Common.OpenInBrowser, FontAwesomeIcon.ExternalLinkAlt, ActionOpenUrl);
                Add(L.Linkpearl.CopyLink, FontAwesomeIcon.Copy, ActionCopyText);
                return;
            case ChatChunkKind.Item:
                Add(L.Linkpearl.TryOn, FontAwesomeIcon.Tshirt, ActionTryOn);
                Add(L.Linkpearl.CompareItem, FontAwesomeIcon.BalanceScale, ActionCompare);
                Add(L.Linkpearl.SearchRecipes, FontAwesomeIcon.Hammer, ActionRecipes);
                Add(L.Linkpearl.FindItem, FontAwesomeIcon.BoxOpen, ActionFindItem);
                Add(L.Linkpearl.LinkInChat, FontAwesomeIcon.Comment, ActionLinkInChat);
                if (OpenMarket is not null)
                {
                    Add(L.Linkpearl.OpenInMarket, FontAwesomeIcon.Store, ActionOpenMarket);
                }

                Add(L.Messages.CopyName, FontAwesomeIcon.Copy, ActionCopyText);
                return;
            case ChatChunkKind.Map:
                Add(L.Linkpearl.OpenMap, FontAwesomeIcon.MapMarkedAlt, ActionOpenMap);
                Add(L.Linkpearl.CopyLink, FontAwesomeIcon.Copy, ActionCopyText);
                return;
            case ChatChunkKind.Player:
                if (SendTell is not null)
                {
                    Add(L.Linkpearl.SendTell, FontAwesomeIcon.PenAlt, ActionSendTell);
                }

                if (LookUp is not null)
                {
                    Add(L.Linkpearl.LookUp, FontAwesomeIcon.Search, ActionLookUp);
                }

                AddPlayerItems();
                Add(L.Messages.CopyName, FontAwesomeIcon.Copy, ActionCopyName);
                return;
            default:
                Add(L.Messages.CopyName, FontAwesomeIcon.Copy, ActionCopyText);
                return;
        }
    }

    private void Add(LocString label, FontAwesomeIcon icon, byte action, bool danger = false)
    {
        items.Add(new DropdownMenu.Item(Loc.T(label), IconGlyph.Of(icon), danger));
        actions.Add(action);
    }

    private void Run(byte action)
    {
        switch (action)
        {
            case ActionCopyText:
                Copy(text);
                break;
            case ActionCopyName:
                Copy(name);
                break;
            case ActionSendTell:
                SendTell?.Invoke(name, world);
                break;
            case ActionLookUp:
                LookUp?.Invoke(name, world);
                break;
            case ActionInviteParty:
                PlayerActions.InviteToParty(name, world);
                break;
            case ActionFriendRequest:
                PlayerActions.SendFriendRequest(name, world);
                break;
            case ActionAdventurerPlate:
                PlayerActions.OpenAdventurerPlate(name, world);
                break;
            case ActionTargetPlayer:
                PlayerActions.TargetPlayer(name, world);
                break;
            case ActionBlacklist:
                PlayerActions.AddToBlacklist(name, world);
                break;
            case ActionOpenUrl:
                UrlActions.AskThenOpen(text);
                break;
            case ActionTryOn:
                GameLinkActions.TryOn(link.Id);
                break;
            case ActionCompare:
                GameLinkActions.CompareItem(link.Id);
                break;
            case ActionRecipes:
                GameLinkActions.SearchRecipes(link.Id);
                break;
            case ActionFindItem:
                GameLinkActions.FindItem(link.Id);
                break;
            case ActionLinkInChat:
                GameLinkActions.LinkInChat(link.Id);
                break;
            case ActionOpenMarket:
                OpenMarket?.Invoke(link.Id);
                break;
            case ActionOpenMap:
                GameLinkActions.OpenMap(link.TerritoryId, link.MapId, link.RawX, link.RawY);
                break;
        }
    }

    private static void Copy(string value)
    {
        if (value.Length == 0)
        {
            return;
        }

        ImGui.SetClipboardText(value);
        ShellToast.Show();
    }
}
