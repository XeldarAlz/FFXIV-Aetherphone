namespace Aetherphone.Apps.Dev;

internal enum DevTab
{
    Board,
    Chat,
}

internal enum DevScreen
{
    Root,
    CardDetail,
    CardCompose,
    CardEdit,
    ChatImage,
    ImageView,
}

internal readonly record struct DevRoute(DevScreen Screen, string? Id = null)
{
    public static readonly DevRoute Root = new(DevScreen.Root);
    public static readonly DevRoute CardCompose = new(DevScreen.CardCompose);
    public static readonly DevRoute ChatImage = new(DevScreen.ChatImage);
    public static DevRoute CardDetail(string cardId) => new(DevScreen.CardDetail, cardId);
    public static DevRoute CardEdit(string cardId) => new(DevScreen.CardEdit, cardId);
    public static DevRoute ImageView(string messageId) => new(DevScreen.ImageView, messageId);
}
