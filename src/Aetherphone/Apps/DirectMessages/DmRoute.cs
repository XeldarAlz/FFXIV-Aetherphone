namespace Aetherphone.Apps.DirectMessages;

internal enum DmScreen
{
    List,
    Thread,
    NewChat,
    GroupInfo,
    AddMembers,
    ChatImage,
    ImageView,
}

internal readonly record struct DmRoute(DmScreen Screen, string? Id = null)
{
    public static readonly DmRoute List = new(DmScreen.List);
    public static readonly DmRoute NewChat = new(DmScreen.NewChat);

    public static DmRoute Thread(string conversationId) => new(DmScreen.Thread, conversationId);

    public static DmRoute GroupInfo(string conversationId) => new(DmScreen.GroupInfo, conversationId);

    public static DmRoute AddMembers(string conversationId) => new(DmScreen.AddMembers, conversationId);

    public static DmRoute ChatImage(string conversationId) => new(DmScreen.ChatImage, conversationId);

    public static DmRoute ImageView(string messageId) => new(DmScreen.ImageView, messageId);
}
