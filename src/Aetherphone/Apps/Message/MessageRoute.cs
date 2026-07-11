namespace Aetherphone.Apps.Message;

internal enum MessageScreen : byte
{
    Root,
    Thread,
    NewChat,
    GroupInfo,
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
}

internal readonly record struct MessageRoute(MessageScreen Screen, string? Id = null)
{
    public static readonly MessageRoute Root = new(MessageScreen.Root);
    public static readonly MessageRoute NewChat = new(MessageScreen.NewChat);
    public static readonly MessageRoute Archived = new(MessageScreen.Archived);
    public static readonly MessageRoute AddContact = new(MessageScreen.AddContact);
    public static readonly MessageRoute Safety = new(MessageScreen.Safety);
    public static readonly MessageRoute Call = new(MessageScreen.Call);
    public static readonly MessageRoute AddToCall = new(MessageScreen.AddToCall);
    public static readonly MessageRoute NewCall = new(MessageScreen.NewCall);

    public static MessageRoute Thread(string conversationId) => new(MessageScreen.Thread, conversationId);

    public static MessageRoute GroupInfo(string conversationId) => new(MessageScreen.GroupInfo, conversationId);

    public static MessageRoute AddMembers(string conversationId) => new(MessageScreen.AddMembers, conversationId);

    public static MessageRoute ChatImage(string conversationId) => new(MessageScreen.ChatImage, conversationId);

    public static MessageRoute ImageView(string messageId) => new(MessageScreen.ImageView, messageId);

    public static MessageRoute Contact(string userId) => new(MessageScreen.ContactDetail, userId);

    public static MessageRoute Encryption(string conversationId) => new(MessageScreen.Encryption, conversationId);

    public static MessageRoute MessageInfo(string messageId) => new(MessageScreen.MessageInfo, messageId);

    public static MessageRoute Forward(string messageId) => new(MessageScreen.Forward, messageId);

    public static readonly MessageRoute Starred = new(MessageScreen.Starred);

    public static MessageRoute Reactions(string messageId) => new(MessageScreen.Reactions, messageId);
}
