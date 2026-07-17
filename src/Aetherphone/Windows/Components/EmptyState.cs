using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class EmptyState
{
    public static void Draw(Rect body, AppSkin ui, FontAwesomeIcon icon, string title, string hint)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var centerX = body.Center.X;
        var baseY = body.Center.Y - 40f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var iconCenter = new Vector2(centerX, baseY);
        drawList.AddCircleFilled(iconCenter, 34f * scale, ImGui.GetColorU32(ui.FieldSurface), 32);
        AppSkin.Icon(iconCenter, icon.ToIconString(), ui.MutedInk, 1.7f);
        Typography.DrawCentered(new Vector2(centerX, baseY + 58f * scale), title, ui.TitleInk, TextStyles.Title3);
        if (hint.Length == 0)
        {
            return;
        }

        var maxWidth = MathF.Min(body.Width - 56f * scale, 300f * scale);
        Typography.DrawWrappedCentered(new Vector2(centerX, baseY + 84f * scale), hint, ui.MutedInk,
            TextStyles.Subheadline, maxWidth);
    }
}
