namespace Aetherphone.Apps.YellowPages;

internal enum YellowPagesScreen : byte
{
    Root,
    Category,
    Detail,
    Compose,
    Thread,
    NewInquiry,
    ChatImage,
    ImageView,
    Reactions,
    Encryption,
    InboxArchived,
}

internal enum YellowPagesTab : byte
{
    Browse,
    Saved,
    Inbox,
    Mine,
}

internal readonly record struct YellowPagesRoute(YellowPagesScreen Screen, string? Id = null, int Intent = 0)
{
    public static readonly YellowPagesRoute Root = new(YellowPagesScreen.Root);
    public static readonly YellowPagesRoute Compose = new(YellowPagesScreen.Compose);
    public static readonly YellowPagesRoute Encryption = new(YellowPagesScreen.Encryption);
    public static readonly YellowPagesRoute InboxArchived = new(YellowPagesScreen.InboxArchived);

    public static YellowPagesRoute Detail(string adId) => new(YellowPagesScreen.Detail, adId);

    public static YellowPagesRoute ForIntent(int intent) => new(YellowPagesScreen.Category, null, intent);

    public static YellowPagesRoute Thread(string inquiryId) => new(YellowPagesScreen.Thread, inquiryId);

    public static YellowPagesRoute NewInquiry(string adId) => new(YellowPagesScreen.NewInquiry, adId);

    public static YellowPagesRoute ChatImage(string inquiryId) => new(YellowPagesScreen.ChatImage, inquiryId);

    public static YellowPagesRoute ImageView(string messageId) => new(YellowPagesScreen.ImageView, messageId);

    public static YellowPagesRoute Reactions(string messageId) => new(YellowPagesScreen.Reactions, messageId);
}
