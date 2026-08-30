using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Aethergram;

internal enum AethergramTab
{
    Home,
    Search,
    Profile,
}

internal enum PostSource
{
    Profile,
    Tagged,
    Saved,
    Hashtag,
    Explore,
}

internal enum AethergramScreen
{
    Home,
    Compose,
    Detail,
    Posts,
    Profile,
    EditProfile,
    UserList,
    Inbox,
    Thread,
    ChatImage,
    ImageView,
    Reactions,
    Settings,
    Share,
    FollowRequests,
    Saved,
    Encryption,
    Hashtag,
    Activity,
    NewMessage,
}

internal readonly record struct AethergramRoute(
    AethergramScreen Screen,
    string? Id = null,
    UserListKind Kind = UserListKind.Followers,
    PostSource Source = PostSource.Profile)
{
    public static readonly AethergramRoute Home = new(AethergramScreen.Home);
    public static readonly AethergramRoute Compose = new(AethergramScreen.Compose);
    public static readonly AethergramRoute EditProfile = new(AethergramScreen.EditProfile);
    public static readonly AethergramRoute Inbox = new(AethergramScreen.Inbox);
    public static readonly AethergramRoute NewMessage = new(AethergramScreen.NewMessage);
    public static readonly AethergramRoute Settings = new(AethergramScreen.Settings);
    public static readonly AethergramRoute FollowRequests = new(AethergramScreen.FollowRequests);
    public static readonly AethergramRoute Saved = new(AethergramScreen.Saved);
    public static readonly AethergramRoute Encryption = new(AethergramScreen.Encryption);
    public static readonly AethergramRoute Activity = new(AethergramScreen.Activity);
    public static AethergramRoute Detail(string postId) => new(AethergramScreen.Detail, postId);

    public static AethergramRoute Posts(string postId, PostSource source) =>
        new(AethergramScreen.Posts, postId, UserListKind.Followers, source);

    public static AethergramRoute Profile(string userId) => new(AethergramScreen.Profile, userId);
    public static AethergramRoute Thread(string userId) => new(AethergramScreen.Thread, userId);
    public static AethergramRoute ChatImage(string userId) => new(AethergramScreen.ChatImage, userId);
    public static AethergramRoute ImageView(string messageId) => new(AethergramScreen.ImageView, messageId);
    public static AethergramRoute Reactions(string messageId) => new(AethergramScreen.Reactions, messageId);
    public static AethergramRoute Share(string postId) => new(AethergramScreen.Share, postId);
    public static AethergramRoute Hashtag(string tag) => new(AethergramScreen.Hashtag, tag);

    public static AethergramRoute UserList(string sourceId, UserListKind kind) =>
        new(AethergramScreen.UserList, sourceId, kind);
}
