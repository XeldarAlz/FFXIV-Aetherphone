using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class SeatSpot
{
    public const float MinimumRadius = 22f;

    private const int Dashes = 16;
    private const float PlusFraction = 0.36f;
    private const float LabelGap = 6f;

    public static bool Draw(ImDrawListPtr drawList, Vector2 center, float radius, string label, float labelWidth,
        float scale)
    {
        radius = MathF.Max(radius, MinimumRadius * scale);
        var corner = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - corner, center + corner)
            && Vector2.DistanceSquared(ImGui.GetMousePos(), center) <= radius * radius;
        var pulse = 0.55f + 0.45f * Pulse.Wave(Pulse.Breath);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(CasinoColors.Money with { W = hovered ? 0.22f : 0.08f + 0.05f * pulse }), 40);
        var ring = ImGui.GetColorU32(CasinoColors.Money with { W = hovered ? 0.95f : 0.55f + 0.25f * pulse });
        var thickness = MathF.Max(1.2f, 1.8f * scale);
        for (var dash = 0; dash < Dashes; dash++)
        {
            var start = MathF.PI * 2f * dash / Dashes;
            drawList.PathArcTo(center, radius, start, start + MathF.PI / Dashes, 6);
            drawList.PathStroke(ring, ImDrawFlags.None, thickness);
        }

        var arm = radius * PlusFraction;
        var plus = ImGui.GetColorU32(StageInks.Strong);
        drawList.AddLine(center - new Vector2(arm, 0f), center + new Vector2(arm, 0f), plus, thickness * 1.3f);
        drawList.AddLine(center - new Vector2(0f, arm), center + new Vector2(0f, arm), plus, thickness * 1.3f);
        if (label.Length > 0)
        {
            var labelCenter = new Vector2(center.X,
                center.Y + radius + LabelGap * scale + Typography.LineHeight(TextStyles.Footnote) * 0.5f);
            StageText.Status(drawList, labelCenter, label, labelWidth, scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - corner, center + corner, hovered);
    }
}
