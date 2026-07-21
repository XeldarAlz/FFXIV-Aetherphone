using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;

namespace Aetherphone.Windows.Components;

internal static class CommentReviewTag
{
    private static readonly Vector4 Ink = new(1f, 0.72f, 0.32f, 0.95f);

    public static void Draw(Vector2 position, float maxRight, string? scanStatus, float typeScale)
    {
        if (!ContentModeration.IsInReview(scanStatus))
        {
            return;
        }

        var label = Loc.T(L.Moderation.InReview);
        var size = Typography.Measure(label, typeScale);
        if (position.X + size.X > maxRight)
        {
            return;
        }

        Typography.Draw(position, label, Ink, typeScale);
    }
}
