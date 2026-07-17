using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal sealed class DropdownMenu
{
    public readonly record struct Item(string Label, string Glyph = "", bool Danger = false, bool Selected = false);

    private const float RevealSeconds = 0.14f;
    private const float RowHeight = 36f;
    private const float MinWidth = 168f;
    private string ownerId = string.Empty;
    private bool open;
    private Rect anchor;
    private double openedAt;
    private int openedFrame;

    public bool Open => open;

    public bool IsOpenFor(string id) => open && ownerId == id;

    public void Toggle(string id, Rect anchorRect)
    {
        if (open && ownerId == id)
        {
            Close();
            return;
        }

        ownerId = id;
        anchor = anchorRect;
        open = true;
        openedAt = ImGui.GetTime();
        openedFrame = ImGui.GetFrameCount();
    }

    public void Close()
    {
        open = false;
        ownerId = string.Empty;
    }

    public void Gate()
    {
        if (open)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public int Draw(Rect screen, PhoneTheme theme, ReadOnlySpan<Item> items)
    {
        if (!open || items.Length == 0)
        {
            return -1;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetForegroundDrawList();
        var reveal = Easing.EaseOutQuint(Math.Clamp((float)((ImGui.GetTime() - openedAt) / RevealSeconds), 0f, 1f));
        var alpha = Easing.SmoothStep(Math.Clamp(reveal / 0.7f, 0f, 1f));
        var padX = 14f * scale;
        var padY = 6f * scale;
        var rowHeight = RowHeight * scale;
        var glyphReserve = 26f * scale;
        var checkReserve = 22f * scale;
        var width = MinWidth * scale;
        var anyGlyph = false;
        var anySelected = false;
        for (var index = 0; index < items.Length; index++)
        {
            var textWidth = Typography.Measure(items[index].Label, 0.9f, FontWeight.Medium).X;
            anyGlyph |= items[index].Glyph.Length > 0;
            anySelected |= items[index].Selected;
            width = MathF.Max(width, textWidth + padX * 2f);
        }

        if (anyGlyph)
        {
            width += glyphReserve;
        }

        if (anySelected)
        {
            width += checkReserve;
        }

        var height = items.Length * rowHeight + padY * 2f;
        var left = anchor.Min.X;
        if (left + width > screen.Max.X - 8f * scale)
        {
            left = anchor.Max.X - width;
        }

        left = Math.Clamp(left, screen.Min.X + 8f * scale, MathF.Max(screen.Min.X + 8f * scale, screen.Max.X - 8f * scale - width));
        var top = anchor.Max.Y + 4f * scale;
        if (top + height > screen.Max.Y - 8f * scale)
        {
            top = anchor.Min.Y - 4f * scale - height;
        }

        var pivot = new Vector2(Math.Clamp(anchor.Center.X, left, left + width), top < anchor.Min.Y ? top + height : top);
        var revealScale = 0.94f + 0.06f * reveal;
        var min = pivot + (new Vector2(left, top) - pivot) * revealScale;
        var max = pivot + (new Vector2(left + width, top + height) - pivot) * revealScale;
        Elevation.Floating(drawList, min, max, 14f * scale, scale);
        Squircle.Fill(drawList, min, max, 14f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, MathF.Min(0.98f, theme.GroupedCard.W + 0.4f) * alpha)));
        Material.EdgeSquircle(drawList, min, max, 14f * scale, scale);
        var clicked = -1;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var rowMin = new Vector2(min.X + padY, min.Y + padY * revealScale + index * rowHeight * revealScale);
            var rowMax = new Vector2(max.X - padY, rowMin.Y + rowHeight * revealScale);
            var centerY = (rowMin.Y + rowMax.Y) * 0.5f;
            var hovered = ImGui.IsMouseHoveringRect(rowMin, rowMax);
            if (hovered)
            {
                Squircle.Fill(drawList, rowMin, rowMax, 9f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.07f * alpha)));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    clicked = index;
                }
            }

            var ink = item.Danger ? theme.Danger : item.Selected ? theme.Accent : theme.TextStrong;
            var textLeft = rowMin.X + padX - padY;
            if (anyGlyph)
            {
                if (item.Glyph.Length > 0)
                {
                    AppSkin.Icon(drawList, new Vector2(textLeft + 8f * scale, centerY), item.Glyph,
                        Palette.WithAlpha(ink, ink.W * alpha), 0.88f);
                }

                textLeft += glyphReserve;
            }

            var textSize = Typography.Measure(item.Label, 0.9f, FontWeight.Medium);
            Typography.Draw(drawList, new Vector2(textLeft, centerY - textSize.Y * 0.5f), item.Label,
                Palette.WithAlpha(ink, ink.W * alpha), 0.9f, FontWeight.Medium);
            if (item.Selected)
            {
                DrawCheck(drawList, new Vector2(rowMax.X - 16f * scale, centerY), theme.Accent, alpha, scale);
            }
        }

        if (clicked >= 0)
        {
            Close();
            return clicked;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(min, max, false) &&
            ImGui.GetFrameCount() != openedFrame)
        {
            Close();
        }

        return -1;
    }

    private static void DrawCheck(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float alpha, float scale)
    {
        var color = ImGui.GetColorU32(Palette.WithAlpha(accent, accent.W * alpha));
        var thickness = 1.8f * scale;
        drawList.AddLine(center + new Vector2(-4f * scale, 0f), center + new Vector2(-1.2f * scale, 3.2f * scale),
            color, thickness);
        drawList.AddLine(center + new Vector2(-1.2f * scale, 3.2f * scale),
            center + new Vector2(4.4f * scale, -3.6f * scale), color, thickness);
    }
}
