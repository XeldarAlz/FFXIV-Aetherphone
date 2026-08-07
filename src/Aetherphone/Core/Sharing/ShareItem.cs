namespace Aetherphone.Core.Sharing;

internal enum ShareKind
{
    Photo,
}

[Flags]
internal enum ShareKindSet
{
    None = 0,
    Photo = 1,
}

internal static class ShareKinds
{
    public static ShareKindSet FlagFor(ShareKind kind) => kind switch
    {
        ShareKind.Photo => ShareKindSet.Photo,
        _ => ShareKindSet.None,
    };

    public static bool Contains(ShareKindSet set, ShareKind kind) => (set & FlagFor(kind)) != 0;
}

internal readonly record struct ShareItem(ShareKind Kind, string LocalPath, string SourceAppId);
