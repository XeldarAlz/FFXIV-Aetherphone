using Aetherphone.Core.Contacts;
using Aetherphone.Core.Messaging;

namespace Aetherphone.Apps.Messages;

internal enum MessagesScreen : byte
{
    Root,
    DirectThread,
    LinkshellThread,
    FriendDetail,
    CharacterDetail,
    FreeCompanyDetail,
}

internal readonly struct MessagesRoute
{
    public static readonly MessagesRoute Root = new(MessagesScreen.Root, null, null, null, string.Empty,
        string.Empty, string.Empty);

    public readonly MessagesScreen Screen;
    public readonly Conversation? Conversation;
    public readonly LinkshellThread? Linkshell;
    public readonly FriendEntry? Friend;
    public readonly string LookupId;
    public readonly string LookupName;
    public readonly string LookupWorld;

    private MessagesRoute(MessagesScreen screen, Conversation? conversation, LinkshellThread? linkshell,
        FriendEntry? friend, string lookupId, string lookupName, string lookupWorld)
    {
        Screen = screen;
        Conversation = conversation;
        Linkshell = linkshell;
        Friend = friend;
        LookupId = lookupId;
        LookupName = lookupName;
        LookupWorld = lookupWorld;
    }

    public static MessagesRoute Direct(Conversation conversation) =>
        new(MessagesScreen.DirectThread, conversation, null, null, string.Empty, string.Empty, string.Empty);

    public static MessagesRoute Shell(LinkshellThread thread) =>
        new(MessagesScreen.LinkshellThread, null, thread, null, string.Empty, string.Empty, string.Empty);

    public static MessagesRoute Detail(FriendEntry friend) =>
        new(MessagesScreen.FriendDetail, null, null, friend, string.Empty, string.Empty, string.Empty);

    public static MessagesRoute Character(string id, string name, string world) =>
        new(MessagesScreen.CharacterDetail, null, null, null, id, name, world);

    public static MessagesRoute FreeCompany(string id, string name, string world) =>
        new(MessagesScreen.FreeCompanyDetail, null, null, null, id, name, world);
}
