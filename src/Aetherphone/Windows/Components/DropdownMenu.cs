using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal sealed class DropdownMenu
{
    public readonly record struct Item(string Label, string Glyph = "", bool Danger = false, bool Selected = false,
        bool CanEdit = false, bool CanDelete = false);

    public enum RowAction
    {
        Select,
        Edit,
        Delete,
    }

    private const float RevealSeconds = 0.14f;
    private const float RowHeight = 36f;
    private const float MinWidth = 168f;
    private const float ActionSlotWidth = 24f;
    private const float ActionIconRadius = 11f;
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

    public int Draw(Rect screen, PhoneTheme theme, ReadOnlySpan<Item> items) => Draw(screen, theme, items, out _);

    public int Draw(Rect screen, PhoneTheme theme, ReadOnlySpan<Item> items, out RowAction action)
    {
        action = RowAction.Select;
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
        var actionSlot = ActionSlotWidth * scale;
        var width = MinWidth * scale;
        var anyGlyph = false;
        var anySelected = false;
        var anyEdit = false;
        for (var index = 0; index < items.Length; index++)
        {
            var textWidth = Typography.Measure(items[index].Label, 0.9f, FontWeight.Medium).X;
            anyGlyph |= items[index].Glyph.Length > 0;
            anySelected |= items[index].Selected;
            anyEdit |= items[index].CanEdit;
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

        if (anyEdit)
        {
            width += actionSlot;
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
        PopoverSurface.Draw(drawList, min, max, 14f * scale, theme, scale, alpha);
        var clicked = -1;
        var clickedAction = RowAction.Select;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var rowMin = new Vector2(min.X + padY, min.Y + padY * revealScale + index * rowHeight * revealScale);
            var rowMax = new Vector2(max.X - padY, rowMin.Y + rowHeight * revealScale);
            var centerY = (rowMin.Y + rowMax.Y) * 0.5f;

            // Reserved slots are laid out right-to-left so every row's icons line up regardless of that
            // row's own capabilities: checkmark, then edit. Delete has no icon of its own: it is a right-click
            // anywhere on the row (confirmed by the caller before anything is actually removed).
            var cursorRight = rowMax.X - 10f * scale;
            if (anySelected)
            {
                cursorRight -= checkReserve;
            }

            Rect? editRect = null;
            if (anyEdit)
            {
                var center = new Vector2(cursorRight - actionSlot * 0.5f, centerY);
                editRect = new Rect(center - new Vector2(ActionIconRadius * scale), center + new Vector2(ActionIconRadius * scale));
                cursorRight -= actionSlot;
            }

            var rowHovered = ImGui.IsMouseHoveringRect(rowMin, rowMax);
            var editHovered = item.CanEdit && editRect is { } er && ImGui.IsMouseHoveringRect(er.Min, er.Max);
            if (rowHovered)
            {
                Squircle.Fill(drawList, rowMin, rowMax, 9f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.07f * alpha)));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (item.CanDelete && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    clicked = index;
                    clickedAction = RowAction.Delete;
                }
                else if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    clicked = index;
                    clickedAction = editHovered ? RowAction.Edit : RowAction.Select;
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

            if (item.CanEdit && editRect is { } editIconRect)
            {
                var tint = editHovered ? theme.Accent : Palette.WithAlpha(theme.TextMuted, theme.TextMuted.W * alpha);
                AppSkin.Icon(drawList, editIconRect.Center, FontAwesomeIcon.Pen.ToIconString(), tint, 0.7f);
            }
        }

        if (clicked >= 0)
        {
            action = clickedAction;
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
