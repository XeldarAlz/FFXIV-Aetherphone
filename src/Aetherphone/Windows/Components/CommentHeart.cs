using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class CommentHeart
{
    public static readonly Vector4 LikeRed = new(0.95f, 0.27f, 0.36f, 1f);

    private const float CountTextScale = 0.7f;

    public static bool Draw(AppSkin ui, Vector2 center, bool liked, int likeCount, Vector4 idleColor,
        Vector4 countColor, string tooltip, out float bottom)
    {
        var scale = ImGuiHelpers.GlobalScale;
        bottom = center.Y + 10f * scale;
        var clicked = ui.IconButton(center, 9f * scale, FontAwesomeIcon.Heart.ToIconString(),
            liked ? LikeRed : idleColor, AppSkin.Transparent, 0.8f, tooltip);
        if (likeCount > 0)
        {
            var countText = likeCount.ToString(Loc.Culture);
            var countSize = Typography.Measure(countText, CountTextScale);
            var countPos = new Vector2(center.X - countSize.X * 0.5f, center.Y + 9f * scale);
            Typography.Draw(ImGui.GetWindowDrawList(), countPos, countText, countColor, CountTextScale);
            bottom = countPos.Y + countSize.Y;
        }

        return clicked;
    }
}
