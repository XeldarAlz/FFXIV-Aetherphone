using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Aethergram;

internal enum AethergramScreen
{
    Home,
    Compose,
    Detail,
    Profile,
    EditProfile,
    Discover,
    UserList,
}

internal readonly record struct AethergramRoute(
    AethergramScreen Screen,
    string? Id = null,
    UserListKind Kind = UserListKind.Followers)
{
    public static readonly AethergramRoute Home = new(AethergramScreen.Home);
    public static readonly AethergramRoute Compose = new(AethergramScreen.Compose);
    public static readonly AethergramRoute EditProfile = new(AethergramScreen.EditProfile);
    public static readonly AethergramRoute Discover = new(AethergramScreen.Discover);
    public static AethergramRoute Detail(string postId) => new(AethergramScreen.Detail, postId);
    public static AethergramRoute Profile(string userId) => new(AethergramScreen.Profile, userId);

    public static AethergramRoute UserList(string sourceId, UserListKind kind) =>
        new(AethergramScreen.UserList, sourceId, kind);
}
