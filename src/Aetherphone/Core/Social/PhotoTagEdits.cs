using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal static class PhotoTagEdits
{
    public static bool Unchanged(PhotoTagDto[]? existing, PhotoTagInput[] wanted)
    {
        var existingCount = existing?.Length ?? 0;
        if (existingCount != wanted.Length)
        {
            return false;
        }

        for (var wantedIndex = 0; wantedIndex < wanted.Length; wantedIndex++)
        {
            if (!Matches(existing!, wanted[wantedIndex]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Matches(PhotoTagDto[] existing, PhotoTagInput wanted)
    {
        for (var index = 0; index < existing.Length; index++)
        {
            var tag = existing[index];
            if (!string.Equals(tag.UserId, wanted.UserId, StringComparison.Ordinal))
            {
                continue;
            }

            return tag.PhotoIndex == wanted.PhotoIndex && tag.X == wanted.X && tag.Y == wanted.Y;
        }

        return false;
    }
}
