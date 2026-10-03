using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal static class PostOrder
{
    public static int NewestFirst(PostDto left, PostDto right)
    {
        var byTime = right.CreatedAtUnix.CompareTo(left.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }

    public static int PinnedThenNewest(PostDto left, PostDto right)
    {
        var byPin = (right.PinnedAtUnix ?? long.MinValue).CompareTo(left.PinnedAtUnix ?? long.MinValue);
        return byPin != 0 ? byPin : NewestFirst(left, right);
    }

    public static long CreatedAtUnix(PostDto post) => post.CreatedAtUnix;
}
