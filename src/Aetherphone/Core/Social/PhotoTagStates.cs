using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal static class PhotoTagStates
{
    public const int Pending = 0;
    public const int Approved = 1;
    public const int Rejected = 2;

    public static PhotoTagDto? PendingFor(PhotoTagDto[]? tags, string? userId)
    {
        if (tags is null || string.IsNullOrEmpty(userId))
        {
            return null;
        }

        for (var index = 0; index < tags.Length; index++)
        {
            var tag = tags[index];
            if (tag.State == Pending && string.Equals(tag.UserId, userId, StringComparison.Ordinal))
            {
                return tag;
            }
        }

        return null;
    }

    public static PhotoTagDto[]? WithState(PhotoTagDto[]? tags, string tagId, int state)
    {
        if (tags is null)
        {
            return null;
        }

        for (var index = 0; index < tags.Length; index++)
        {
            if (!string.Equals(tags[index].Id, tagId, StringComparison.Ordinal) || tags[index].State == state)
            {
                continue;
            }

            var result = (PhotoTagDto[])tags.Clone();
            result[index] = tags[index] with { State = state };
            return result;
        }

        return tags;
    }

    public static PhotoTagDto[]? Without(PhotoTagDto[]? tags, string tagId)
    {
        if (tags is null)
        {
            return null;
        }

        var removeIndex = -1;
        for (var index = 0; index < tags.Length; index++)
        {
            if (string.Equals(tags[index].Id, tagId, StringComparison.Ordinal))
            {
                removeIndex = index;
                break;
            }
        }

        if (removeIndex < 0)
        {
            return tags;
        }

        var result = new PhotoTagDto[tags.Length - 1];
        var writeIndex = 0;
        for (var index = 0; index < tags.Length; index++)
        {
            if (index == removeIndex)
            {
                continue;
            }

            result[writeIndex++] = tags[index];
        }

        return result;
    }
}
