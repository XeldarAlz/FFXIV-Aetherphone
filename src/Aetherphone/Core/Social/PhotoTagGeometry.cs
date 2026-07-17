using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Social;

internal static class PhotoTagGeometry
{
    public static Vector2 ToScreen(Rect frame, float x, float y) =>
        frame.Min + new Vector2(x, y) * frame.Size;

    public static Vector2 ToNormalized(Rect frame, Vector2 point)
    {
        var size = frame.Size;
        if (size.X <= 0f || size.Y <= 0f)
        {
            return Vector2.Zero;
        }

        return new Vector2(
            Math.Clamp((point.X - frame.Min.X) / size.X, 0f, 1f),
            Math.Clamp((point.Y - frame.Min.Y) / size.Y, 0f, 1f));
    }

    public static int CountFor(PhotoTagDto[]? tags, int photoIndex)
    {
        if (tags is null)
        {
            return 0;
        }

        var count = 0;
        for (var index = 0; index < tags.Length; index++)
        {
            if (tags[index].PhotoIndex == photoIndex)
            {
                count++;
            }
        }

        return count;
    }
}
