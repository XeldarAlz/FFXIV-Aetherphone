using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Aethergram;

internal enum AethergramTab
{
    Home,
    Search,
    Messages,
    Profile,
}

internal enum PostSource
{
    Profile,
    Tagged,
    Saved,
    Liked,
    Hashtag,
    Explore,
    Archive,
    PendingTags,
}

internal enum AethergramScreen
{
    Home,
    Compose,
    Detail,
    Posts,
    Profile,
    EditProfile,
    EditPost,
    UserList,
    Dm,
    Settings,
    FollowRequests,
    Saved,
    Liked,
    Hashtag,
    Activity,
    BadgeProgress,
    Archive,
}

internal readonly record struct AethergramRoute(
    AethergramScreen Screen,
    string? Id = null,
    UserListKind Kind = UserListKind.Followers,
    PostSource Source = PostSource.Profile,
    SocialDmScreen DmScreen = SocialDmScreen.Inbox)
{
    public static readonly AethergramRoute Home = new(AethergramScreen.Home);
    public static readonly AethergramRoute Compose = new(AethergramScreen.Compose);
    public static readonly AethergramRoute EditProfile = new(AethergramScreen.EditProfile);
    public static readonly AethergramRoute Settings = new(AethergramScreen.Settings);
    public static readonly AethergramRoute FollowRequests = new(AethergramScreen.FollowRequests);
    public static readonly AethergramRoute Saved = new(AethergramScreen.Saved);
    public static readonly AethergramRoute Liked = new(AethergramScreen.Liked);
    public static readonly AethergramRoute Activity = new(AethergramScreen.Activity);
    public static readonly AethergramRoute BadgeProgress = new(AethergramScreen.BadgeProgress);
    public static readonly AethergramRoute Archive = new(AethergramScreen.Archive);
    public static AethergramRoute Detail(string postId) => new(AethergramScreen.Detail, postId);
    public static AethergramRoute EditPost(string postId) => new(AethergramScreen.EditPost, postId);

    public static AethergramRoute Posts(string postId, PostSource source) =>
        new(AethergramScreen.Posts, postId, UserListKind.Followers, source);

    public static AethergramRoute Profile(string userId) => new(AethergramScreen.Profile, userId);

    public static AethergramRoute Dm(SocialDmScreen screen, string? id = null) =>
        new(AethergramScreen.Dm, id, DmScreen: screen);

    public static AethergramRoute Hashtag(string tag) => new(AethergramScreen.Hashtag, tag);

    public static AethergramRoute UserList(string sourceId, UserListKind kind) =>
        new(AethergramScreen.UserList, sourceId, kind);
}
