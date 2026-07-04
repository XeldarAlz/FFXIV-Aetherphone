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
}

internal readonly record struct ChirperRoute(ChirperScreen Screen, string? UserId = null, string? PostId = null)
{
    public static readonly ChirperRoute Home = new(ChirperScreen.Home);

    public static readonly ChirperRoute Compose = new(ChirperScreen.Compose);

    public static readonly ChirperRoute EditProfile = new(ChirperScreen.EditProfile);

    public static readonly ChirperRoute Avatar = new(ChirperScreen.Avatar);

    public static readonly ChirperRoute Discover = new(ChirperScreen.Discover);

    public static ChirperRoute Profile(string userId) => new(ChirperScreen.Profile, userId);

    public static ChirperRoute Thread(string postId) => new(ChirperScreen.Thread, PostId: postId);
}
