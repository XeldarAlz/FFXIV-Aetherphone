namespace Aetherphone.Core.SystemMedia;

internal static class ArtworkFingerprint
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Of(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return 0;
        }

        var hash = OffsetBasis ^ (ulong)bytes.Length;
        for (var index = 0; index < bytes.Length; index++)
        {
            hash ^= bytes[index];
            hash *= Prime;
        }

        return hash == 0 ? 1 : hash;
    }
}
