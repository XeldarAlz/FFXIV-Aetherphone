using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Aethergram;

internal enum AethergramTab
{
    Home,
    Search,
    Activity,
    Profile,
}

internal enum AethergramScreen
{
    Home,
    Compose,
    Detail,
    Profile,
    EditProfile,
    UserList,
    Inbox,
    Thread,
    ChatImage,
    ImageView,
    Reactions,
    Settings,
}

internal readonly record struct AethergramRoute(
    AethergramScreen Screen,
    string? Id = null,
    UserListKind Kind = UserListKind.Followers)
{
    public static readonly AethergramRoute Home = new(AethergramScreen.Home);
    public static readonly AethergramRoute Compose = new(AethergramScreen.Compose);
    public static readonly AethergramRoute EditProfile = new(AethergramScreen.EditProfile);
    public static readonly AethergramRoute Inbox = new(AethergramScreen.Inbox);
    public static readonly AethergramRoute Settings = new(AethergramScreen.Settings);
    public static AethergramRoute Detail(string postId) => new(AethergramScreen.Detail, postId);
    public static AethergramRoute Profile(string userId) => new(AethergramScreen.Profile, userId);
    public static AethergramRoute Thread(string userId) => new(AethergramScreen.Thread, userId);
    public static AethergramRoute ChatImage(string userId) => new(AethergramScreen.ChatImage, userId);
    public static AethergramRoute ImageView(string messageId) => new(AethergramScreen.ImageView, messageId);
    public static AethergramRoute Reactions(string messageId) => new(AethergramScreen.Reactions, messageId);

    public static AethergramRoute UserList(string sourceId, UserListKind kind) =>
        new(AethergramScreen.UserList, sourceId, kind);
}
