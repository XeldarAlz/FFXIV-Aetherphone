using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Chirper;

internal enum ChirperScreen
{
    Home,
    Compose,
    Profile,
    EditProfile,
    Avatar,
    Discover,
    Thread,
    UserList,
    Activity,
}

internal readonly record struct ChirperRoute(
    ChirperScreen Screen,
    string? UserId = null,
    string? PostId = null,
    UserListKind Kind = UserListKind.Followers)
{
    public static readonly ChirperRoute Home = new(ChirperScreen.Home);
    public static readonly ChirperRoute Compose = new(ChirperScreen.Compose);
    public static readonly ChirperRoute EditProfile = new(ChirperScreen.EditProfile);
    public static readonly ChirperRoute Avatar = new(ChirperScreen.Avatar);
    public static readonly ChirperRoute Discover = new(ChirperScreen.Discover);
    public static readonly ChirperRoute Activity = new(ChirperScreen.Activity);
    public static ChirperRoute Profile(string userId) => new(ChirperScreen.Profile, userId);
    public static ChirperRoute Thread(string postId) => new(ChirperScreen.Thread, PostId: postId);

    public static ChirperRoute UserList(string sourceId, UserListKind kind) =>
        new(ChirperScreen.UserList, UserId: sourceId, Kind: kind);
}
