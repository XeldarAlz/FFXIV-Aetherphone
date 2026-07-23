using Aetherphone.Core.Localization;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed class HomeChrome
{
    private readonly Pager pager;
    private readonly HomeInteractionController interaction;

    public HomeChrome(Pager pager, HomeInteractionController interaction)
    {
        this.pager = pager;
        this.interaction = interaction;
    }

    public void DrawPageControls(in HomeMetrics metrics, PhoneTheme theme, float alpha, bool interactive)
    {
        var pageCount = interaction.DisplayPageCount();
        if (pageCount <= 1 || alpha <= 0.01f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var scale = metrics.Scale;
        var spacing = 14f * scale;
        var radius = 3f * scale;
        var totalWidth = (pageCount - 1) * spacing;
        var startX = metrics.Content.Center.X - totalWidth * 0.5f;
        var y = metrics.DotsCenterY;
        var active = Math.Clamp((int)MathF.Round(pager.Value), 0, pageCount - 1);
        for (var index = 0; index < pageCount; index++)
        {
            var center = new Vector2(startX + index * spacing, y);
            var hovered = interactive && interaction.DragTile is null &&
                          ImGui.IsMouseHoveringRect(center - new Vector2(spacing * 0.5f),
                              center + new Vector2(spacing * 0.5f));
            var dotAlpha = index == active ? 0.95f : hovered ? 0.55f : 0.32f;
            drawList.AddCircleFilled(center, radius,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, dotAlpha * alpha)), 16);
            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                pager.AnimateTo(index, pageCount);
                interaction.CancelPress();
            }
        }

        DrawPageArrow(metrics, theme, alpha, interactive, -1, pageCount);
        DrawPageArrow(metrics, theme, alpha, interactive, 1, pageCount);
    }

    private void DrawPageArrow(in HomeMetrics metrics, PhoneTheme theme, float alpha, bool interactive, int direction,
        int pageCount)
    {
        var target = pager.Page + direction;
        if (target < 0 || target > pageCount - 1)
        {
            return;
        }

        var scale = metrics.Scale;
        var tabWidth = 20f * scale;
        var tabHalfHeight = 30f * scale;
        var centerY = metrics.Grid.Center.Y;
        var leftEdge = metrics.Content.Min.X - theme.SidePadding * scale;
        var rightEdge = metrics.Content.Max.X + theme.SidePadding * scale;
        var tab = direction < 0
            ? new Rect(new Vector2(leftEdge, centerY - tabHalfHeight),
                new Vector2(leftEdge + tabWidth, centerY + tabHalfHeight))
            : new Rect(new Vector2(rightEdge - tabWidth, centerY - tabHalfHeight),
                new Vector2(rightEdge, centerY + tabHalfHeight));
        var hit = new Rect(tab.Min - new Vector2(4f * scale), tab.Max + new Vector2(4f * scale));
        var hovered = interactive && interaction.DragTile is null && ImGui.IsMouseHoveringRect(hit.Min, hit.Max);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 7f * scale;
        var corners = direction < 0 ? ImDrawFlags.RoundCornersRight : ImDrawFlags.RoundCornersLeft;
        drawList.AddRectFilled(tab.Min, tab.Max,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, (hovered ? 0.42f : 0.28f) * alpha)), rounding, corners);
        drawList.AddRect(tab.Min, tab.Max,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (hovered ? 0.35f : 0.18f) * alpha)), rounding, corners,
            1f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var center = tab.Center;
        var reach = 4.2f * scale;
        var thickness = 2.2f * scale;
        var tip = new Vector2(center.X + reach * 0.55f * direction, center.Y);
        var upper = new Vector2(tip.X - reach * direction, tip.Y - reach);
        var lower = new Vector2(tip.X - reach * direction, tip.Y + reach);
        var ink = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (hovered ? 1f : 0.85f) * alpha));
        drawList.AddLine(upper, tip, ink, thickness);
        drawList.AddLine(tip, lower, ink, thickness);
        var cap = thickness * 0.5f;
        drawList.AddCircleFilled(upper, cap, ink, 8);
        drawList.AddCircleFilled(tip, cap, ink, 8);
        drawList.AddCircleFilled(lower, cap, ink, 8);
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pager.AnimateTo(target, pageCount);
            interaction.CancelPress();
            interaction.CancelTap();
        }
    }

    public void DrawEditChrome(Rect content, in HomeMetrics metrics, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = metrics.Scale;
        if (interaction.CanAddWidget)
        {
            var add = AddRect(content, metrics);
            var addHovered = ImGui.IsMouseHoveringRect(add.Min, add.Max);
            var addCenter = add.Center;
            drawList.AddCircleFilled(addCenter, add.Width * 0.5f,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, addHovered ? 0.26f : 0.17f)), 32);
            var arm = add.Width * 0.22f;
            var ink = ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.95f));
            drawList.AddLine(addCenter - new Vector2(arm, 0f), addCenter + new Vector2(arm, 0f), ink, 2f * scale);
            drawList.AddLine(addCenter - new Vector2(0f, arm), addCenter + new Vector2(0f, arm), ink, 2f * scale);
            if (addHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
        }

        var done = DoneRect(content, metrics);
        var doneHovered = ImGui.IsMouseHoveringRect(done.Min, done.Max);
        Squircle.Fill(drawList, done.Min, done.Max, done.Height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, doneHovered ? 1f : 0.88f)));
        Typography.DrawCentered(done.Center, Loc.T(L.Home.Done), new Vector4(1f, 1f, 1f, 1f), 0.82f,
            FontWeight.SemiBold);
        if (doneHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    public static Rect DoneRect(Rect content, in HomeMetrics metrics)
    {
        var width = 64f * metrics.Scale;
        var height = 30f * metrics.Scale;
        var max = new Vector2(content.Max.X - 4f * metrics.Scale, content.Min.Y + height);
        return new Rect(new Vector2(max.X - width, content.Min.Y), max);
    }

    public static Rect AddRect(Rect content, in HomeMetrics metrics)
    {
        var size = 30f * metrics.Scale;
        var min = new Vector2(content.Min.X + 4f * metrics.Scale, content.Min.Y);
        return new Rect(min, min + new Vector2(size, size));
    }
}
