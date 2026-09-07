namespace Aetherphone.Apps.Message;

internal enum MessageScreen : byte
{
    Root,
    Thread,
    NewChat,
    NewGroup,
    GroupInfo,
    EditGroup,
    GroupPhoto,
    AddMembers,
    ChatImage,
    ImageView,
    Archived,
    ContactDetail,
    AddContact,
    Safety,
    Call,
    AddToCall,
    NewCall,
    Encryption,
    MessageInfo,
    Forward,
    Starred,
    Reactions,
    SharePhoto,
    ChatTheme,
    Wallpaper,
}

internal readonly record struct MessageRoute(MessageScreen Screen, string? Id = null)
{
    public static readonly MessageRoute Root = new(MessageScreen.Root);
    public static readonly MessageRoute NewChat = new(MessageScreen.NewChat);
    public static readonly MessageRoute NewGroup = new(MessageScreen.NewGroup);
    public static readonly MessageRoute Archived = new(MessageScreen.Archived);
    public static readonly MessageRoute AddContact = new(MessageScreen.AddContact);
    public static readonly MessageRoute Safety = new(MessageScreen.Safety);
    public static readonly MessageRoute Call = new(MessageScreen.Call);
    public static readonly MessageRoute AddToCall = new(MessageScreen.AddToCall);
    public static readonly MessageRoute NewCall = new(MessageScreen.NewCall);
    public static readonly MessageRoute Starred = new(MessageScreen.Starred);
    public static readonly MessageRoute ChatTheme = new(MessageScreen.ChatTheme);
    public static readonly MessageRoute DefaultWallpaper = new(MessageScreen.Wallpaper);

    public static MessageRoute Thread(string conversationId) => new(MessageScreen.Thread, conversationId);

    public static MessageRoute GroupInfo(string conversationId) => new(MessageScreen.GroupInfo, conversationId);

    public static MessageRoute EditGroup(string conversationId) => new(MessageScreen.EditGroup, conversationId);

    public static MessageRoute GroupPhoto(string conversationId) => new(MessageScreen.GroupPhoto, conversationId);

    public static MessageRoute AddMembers(string conversationId) => new(MessageScreen.AddMembers, conversationId);

    public static MessageRoute ChatImage(string conversationId) => new(MessageScreen.ChatImage, conversationId);

    public static MessageRoute ImageView(string messageId) => new(MessageScreen.ImageView, messageId);

    public static MessageRoute Contact(string userId) => new(MessageScreen.ContactDetail, userId);

    public static MessageRoute Encryption(string conversationId) => new(MessageScreen.Encryption, conversationId);

    public static MessageRoute MessageInfo(string messageId) => new(MessageScreen.MessageInfo, messageId);

    public static MessageRoute Forward(string messageId) => new(MessageScreen.Forward, messageId);

    public static MessageRoute StarredIn(string conversationId) => new(MessageScreen.Starred, conversationId);

    public static MessageRoute Reactions(string messageId) => new(MessageScreen.Reactions, messageId);

    public static MessageRoute SharePhoto(string photoPath) => new(MessageScreen.SharePhoto, photoPath);

    public static MessageRoute ChatWallpaper(string conversationId) => new(MessageScreen.Wallpaper, conversationId);
}
