namespace Aetherphone.Apps.KindKupo;

internal enum KindKupoScreen
{
    Home,
    Inbox,
    Write,
    Respond,
    ResponseList,
    ComposeResponse,
}

internal readonly record struct KindKupoRoute(KindKupoScreen Screen, string? ConfessionId = null)
{
    public static readonly KindKupoRoute Home = new(KindKupoScreen.Home);
    public static readonly KindKupoRoute Inbox = new(KindKupoScreen.Inbox);
    public static readonly KindKupoRoute Write = new(KindKupoScreen.Write);
    public static readonly KindKupoRoute Respond = new(KindKupoScreen.Respond);

    public static KindKupoRoute ViewResponse(string confessionId) =>
        new(KindKupoScreen.ResponseList, confessionId);

    public static KindKupoRoute ComposeResponse(string confessionId) =>
        new(KindKupoScreen.ComposeResponse, confessionId);
}
