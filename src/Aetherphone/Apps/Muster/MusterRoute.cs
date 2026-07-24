namespace Aetherphone.Apps.Muster;

internal enum MusterScreen : byte
{
    Directory,
    Detail,
    Create,
    Manage,
}

internal readonly record struct MusterRoute(MusterScreen Screen, string? MusterId = null)
{
    public static readonly MusterRoute Directory = new(MusterScreen.Directory);
    public static readonly MusterRoute Create = new(MusterScreen.Create);
    public static readonly MusterRoute Manage = new(MusterScreen.Manage);

    public static MusterRoute Detail(string musterId) => new(MusterScreen.Detail, musterId);
}
