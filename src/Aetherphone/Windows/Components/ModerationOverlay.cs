using System.Numerics;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class ModerationOverlay
{
    public static void Draw(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, string? scanStatus)
    {
        if (!ContentModeration.IsInReview(scanStatus))
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)));
        var center = new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f);
        AppSkin.Icon(drawList, new Vector2(center.X, center.Y - 26f * scale), FontAwesomeIcon.Hourglass.ToIconString(),
            new Vector4(1f, 1f, 1f, 0.92f), 1.6f);
        Typography.DrawCentered(drawList, center, Loc.T(L.Moderation.InReview), new Vector4(1f, 1f, 1f, 0.95f),
            TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + 22f * scale),
            Loc.T(L.Moderation.InReviewHint), new Vector4(1f, 1f, 1f, 0.75f), TextStyles.Footnote);
    }
}
