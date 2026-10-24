namespace Aetherphone.Apps.LoveIsland;

internal enum LoveIslandTab : byte
{
    Tonight,
    Djs,
    Experiences,
    Gallery,
    Perks,
}

internal enum LoveIslandScreen : byte
{
    Home,
    Experience,
    Posts,
    Post,
    Syncshells,
}

internal readonly record struct LoveIslandRoute(LoveIslandScreen Screen, string Id)
{
    public static readonly LoveIslandRoute Home = new(LoveIslandScreen.Home, string.Empty);
    public static readonly LoveIslandRoute Posts = new(LoveIslandScreen.Posts, string.Empty);
    public static readonly LoveIslandRoute Syncshells = new(LoveIslandScreen.Syncshells, string.Empty);

    public static LoveIslandRoute Experience(string experienceId) => new(LoveIslandScreen.Experience, experienceId);

    public static LoveIslandRoute Post(string postId) => new(LoveIslandScreen.Post, postId);
}
