using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class AppHeader
{
    public const float Height = 42f;

    public static void Draw(in PhoneContext context, string title, Action? onBack = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var rowCenterY = content.Min.Y + Height * scale * 0.5f;

        Typography.DrawCentered(new Vector2(content.Center.X, rowCenterY), title, context.Theme.TextStrong, 1.15f, FontWeight.SemiBold);

        var hitMin = new Vector2(content.Min.X, content.Min.Y);
        var hitMax = new Vector2(content.Min.X + 44f * scale, content.Min.Y + Height * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        DrawChevron(new Vector2(content.Min.X + 6f * scale, rowCenterY), 7f * scale, 2.4f * scale, hovered ? context.Theme.TextStrong : context.Theme.Accent);

        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        if (onBack is not null)
        {
            onBack();
        }
        else
        {
            context.Navigation.Back();
        }
    }

    private static void DrawChevron(Vector2 tip, float size, float thickness, Vector4 color)
    {
        var dl = ImGui.GetWindowDrawList();
        var packed = ImGui.GetColorU32(color);
        dl.AddLine(new Vector2(tip.X + size, tip.Y - size), tip, packed, thickness);
        dl.AddLine(tip, new Vector2(tip.X + size, tip.Y + size), packed, thickness);
    }
}
