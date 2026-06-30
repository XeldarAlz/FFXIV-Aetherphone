namespace Aetherphone.Apps.Chirper;

internal enum ChirperScreen
{
    Home,
    Compose,
    Profile,
    EditProfile,
    Discover,
}

internal readonly record struct ChirperRoute(ChirperScreen Screen, string? UserId = null)
{
    public static readonly ChirperRoute Home = new(ChirperScreen.Home);

    public static readonly ChirperRoute Compose = new(ChirperScreen.Compose);

    public static readonly ChirperRoute EditProfile = new(ChirperScreen.EditProfile);

    public static readonly ChirperRoute Discover = new(ChirperScreen.Discover);

    public static ChirperRoute Profile(string userId) => new(ChirperScreen.Profile, userId);
}
