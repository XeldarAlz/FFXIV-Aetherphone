namespace Aetherphone.Apps.YellowPages;

internal enum YellowPagesScreen : byte
{
    Browse,
    Detail,
    Compose,
    Mine,
    Saved,
}

internal readonly record struct YellowPagesRoute(YellowPagesScreen Screen, string? AdId = null)
{
    public static readonly YellowPagesRoute Browse = new(YellowPagesScreen.Browse);
    public static readonly YellowPagesRoute Compose = new(YellowPagesScreen.Compose);
    public static readonly YellowPagesRoute Mine = new(YellowPagesScreen.Mine);
    public static readonly YellowPagesRoute Saved = new(YellowPagesScreen.Saved);

    public static YellowPagesRoute Detail(string adId) => new(YellowPagesScreen.Detail, adId);
}
