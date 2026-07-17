using Aetherphone.Core.Contacts;
using Aetherphone.Core.Linkpearl;

namespace Aetherphone.Apps.Linkpearl;

internal enum LinkpearlScreen : byte
{
    Root,
    DirectThread,
    LinkshellThread,
    FriendDetail,
    CharacterDetail,
    FreeCompanyDetail,
}

internal readonly struct LinkpearlRoute
{
    public static readonly LinkpearlRoute Root = new(LinkpearlScreen.Root, null, null, null, string.Empty,
        string.Empty, string.Empty);

    public readonly LinkpearlScreen Screen;
    public readonly Conversation? Conversation;
    public readonly LinkshellThread? Linkshell;
    public readonly FriendEntry? Friend;
    public readonly string LookupId;
    public readonly string LookupName;
    public readonly string LookupWorld;

    private LinkpearlRoute(LinkpearlScreen screen, Conversation? conversation, LinkshellThread? linkshell,
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

    public static LinkpearlRoute Direct(Conversation conversation) =>
        new(LinkpearlScreen.DirectThread, conversation, null, null, string.Empty, string.Empty, string.Empty);

    public static LinkpearlRoute Shell(LinkshellThread thread) =>
        new(LinkpearlScreen.LinkshellThread, null, thread, null, string.Empty, string.Empty, string.Empty);

    public static LinkpearlRoute Detail(FriendEntry friend) =>
        new(LinkpearlScreen.FriendDetail, null, null, friend, string.Empty, string.Empty, string.Empty);

    public static LinkpearlRoute Character(string id, string name, string world) =>
        new(LinkpearlScreen.CharacterDetail, null, null, null, id, name, world);

    public static LinkpearlRoute FreeCompany(string id, string name, string world) =>
        new(LinkpearlScreen.FreeCompanyDetail, null, null, null, id, name, world);
}
