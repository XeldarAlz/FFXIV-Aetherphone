using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed class WidgetGallery
{
    private const float SlideSmoothTime = 0.24f;
    private const float HeaderUnits = 56f;

    private readonly HomeLayoutService layout;
    private readonly WidgetRegistry widgets;
    private readonly Dictionary<string, WidgetSize> selections = new();
    private Spring slide;
    private bool open;
    private float scrollY;
    private int targetPage;

    public WidgetGallery(HomeLayoutService layout, WidgetRegistry widgets)
    {
        this.layout = layout;
        this.widgets = widgets;
    }

    public bool Active => open || slide.Value > 0.01f;

    public void Open(int pageIndex)
    {
        open = true;
        targetPage = pageIndex;
        scrollY = 0f;
    }

    public void Close() => open = false;

    public void CloseImmediate()
    {
        open = false;
        slide.SnapTo(0f);
    }

    public void Draw(Rect screen, PhoneTheme theme, float delta, float scale)
    {
        slide.Step(open ? 1f : 0f, SlideSmoothTime, delta);
        var eased = Math.Clamp(slide.Value, 0f, 1f);
        if (eased <= 0.001f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, true);
        Material.Veil(drawList, screen.Min, screen.Max, 0.5f * eased);
        var sheetTop = screen.Min.Y + 54f * scale;
        var sheetHeight = screen.Max.Y - sheetTop;
        var top = screen.Max.Y - sheetHeight * eased;
        var sheet = new Rect(new Vector2(screen.Min.X, top), new Vector2(screen.Max.X, top + sheetHeight));
        var rounding = 34f * scale;
        Material.Frosted(drawList, sheet.Min, sheet.Max, rounding, scale, 1f);
        var interactive = open && eased > 0.95f;
        DrawHeader(drawList, sheet, theme, scale, interactive);
        DrawItems(drawList, sheet, theme, scale, delta, interactive);
        drawList.PopClipRect();
        if (interactive && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !sheet.Contains(ImGui.GetMousePos()))
        {
            Close();
        }
    }

    private void DrawHeader(ImDrawListPtr drawList, Rect sheet, PhoneTheme theme, float scale, bool interactive)
    {
        var grabberHalf = 19f * scale;
        var grabberY = sheet.Min.Y + 9f * scale;
        drawList.AddRectFilled(new Vector2(sheet.Center.X - grabberHalf, grabberY),
            new Vector2(sheet.Center.X + grabberHalf, grabberY + 4.6f * scale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.32f)), 2.3f * scale);
        Typography.DrawCentered(drawList, new Vector2(sheet.Center.X, sheet.Min.Y + 33f * scale),
            Loc.T(L.Home.Widgets), theme.TextStrong, TextStyles.Title3);
        var closeCenter = new Vector2(sheet.Max.X - 26f * scale, sheet.Min.Y + 33f * scale);
        var closeRadius = 12f * scale;
        var hovered = interactive &&
                      ImGui.IsMouseHoveringRect(closeCenter - new Vector2(closeRadius), closeCenter + new Vector2(closeRadius));
        drawList.AddCircleFilled(closeCenter, closeRadius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, hovered ? 0.22f : 0.13f)), 24);
        var arm = closeRadius * 0.42f;
        var ink = ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.85f));
        drawList.AddLine(closeCenter + new Vector2(-arm, -arm), closeCenter + new Vector2(arm, arm), ink, 1.8f * scale);
        drawList.AddLine(closeCenter + new Vector2(-arm, arm), closeCenter + new Vector2(arm, -arm), ink, 1.8f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                Close();
            }
        }
    }

    private void DrawItems(ImDrawListPtr drawList, Rect sheet, PhoneTheme theme, float scale, float uiDelta,
        bool interactive)
    {
        var contentTop = sheet.Min.Y + HeaderUnits * scale;
        var view = new Rect(new Vector2(sheet.Min.X, contentTop), sheet.Max);
        drawList.PushClipRect(view.Min, view.Max, true);
        if (interactive && view.Contains(ImGui.GetMousePos()))
        {
            scrollY -= ImGui.GetIO().MouseWheel * 46f * scale;
        }

        var cursorY = view.Min.Y - scrollY + 6f * scale;
        var all = widgets.All;
        for (var index = 0; index < all.Count; index++)
        {
            var widget = all[index];
            if (!widgets.IsAvailable(widget))
            {
                continue;
            }

            cursorY = DrawItem(drawList, view, widget, theme, scale, uiDelta, interactive, cursorY);
        }

        var contentHeight = cursorY + scrollY - view.Min.Y;
        scrollY = Math.Clamp(scrollY, 0f, MathF.Max(0f, contentHeight - view.Height + 12f * scale));
        drawList.PopClipRect();
    }

    private float DrawItem(ImDrawListPtr drawList, Rect view, IHomeWidget widget, PhoneTheme theme, float scale,
        float uiDelta, bool interactive, float cursorY)
    {
        var size = SelectedSize(widget);
        var pad = 20f * scale;
        var width = view.Width - pad * 2f;
        Typography.DrawCentered(drawList, new Vector2(view.Center.X, cursorY + 12f * scale), widget.DisplayName,
            theme.TextStrong, TextStyles.Headline);
        cursorY += 30f * scale;
        var previewWidth = size == WidgetSize.Small ? width * 0.44f : width * 0.94f;
        var previewHeight = size == WidgetSize.Large ? previewWidth * 0.94f : size == WidgetSize.Small
            ? previewWidth
            : previewWidth * 0.46f;
        var preview = new Rect(new Vector2(view.Center.X - previewWidth * 0.5f, cursorY),
            new Vector2(view.Center.X + previewWidth * 0.5f, cursorY + previewHeight));
        widget.Draw(new WidgetContext(drawList, preview, theme, size, scale, uiDelta, 1f));
        cursorY = preview.Max.Y + 12f * scale;
        cursorY = DrawSizeChips(drawList, view, widget, size, theme, scale, interactive, cursorY);
        cursorY = DrawAddButton(drawList, view, widget, size, theme, scale, interactive, cursorY);
        return cursorY + 22f * scale;
    }

    private float DrawSizeChips(ImDrawListPtr drawList, Rect view, IHomeWidget widget, WidgetSize selected,
        PhoneTheme theme, float scale, bool interactive, float cursorY)
    {
        Span<WidgetSize> options = stackalloc WidgetSize[3];
        var count = 0;
        if (WidgetSizes.Contains(widget.Sizes, WidgetSize.Small))
        {
            options[count++] = WidgetSize.Small;
        }

        if (WidgetSizes.Contains(widget.Sizes, WidgetSize.Medium))
        {
            options[count++] = WidgetSize.Medium;
        }

        if (WidgetSizes.Contains(widget.Sizes, WidgetSize.Large))
        {
            options[count++] = WidgetSize.Large;
        }

        if (count <= 1)
        {
            return cursorY;
        }

        var chipHeight = 26f * scale;
        var chipGap = 8f * scale;
        var totalWidth = 0f;
        Span<float> widths = stackalloc float[3];
        for (var index = 0; index < count; index++)
        {
            widths[index] = Typography.Measure(SizeLabel(options[index]), TextStyles.Footnote).X + 26f * scale;
            totalWidth += widths[index];
        }

        totalWidth += chipGap * (count - 1);
        var left = view.Center.X - totalWidth * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var chip = new Rect(new Vector2(left, cursorY), new Vector2(left + widths[index], cursorY + chipHeight));
            var isSelected = options[index] == selected;
            var hovered = interactive && ImGui.IsMouseHoveringRect(chip.Min, chip.Max);
            var fill = isSelected
                ? theme.Accent
                : new Vector4(1f, 1f, 1f, hovered ? 0.16f : 0.10f);
            Squircle.Fill(drawList, chip.Min, chip.Max, chipHeight * 0.5f, ImGui.GetColorU32(fill));
            Typography.DrawCentered(drawList, chip.Center, SizeLabel(options[index]),
                isSelected ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong, TextStyles.Footnote);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    selections[widget.Id] = options[index];
                }
            }

            left += widths[index] + chipGap;
        }

        return cursorY + chipHeight + 12f * scale;
    }

    private float DrawAddButton(ImDrawListPtr drawList, Rect view, IHomeWidget widget, WidgetSize size,
        PhoneTheme theme, float scale, bool interactive, float cursorY)
    {
        var label = Loc.T(L.Home.AddWidget);
        var width = Typography.Measure(label, TextStyles.SubheadlineEmphasized).X + 44f * scale;
        var height = 32f * scale;
        var button = new Rect(new Vector2(view.Center.X - width * 0.5f, cursorY),
            new Vector2(view.Center.X + width * 0.5f, cursorY + height));
        var hovered = interactive && ImGui.IsMouseHoveringRect(button.Min, button.Max);
        Squircle.Fill(drawList, button.Min, button.Max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, hovered ? 1f : 0.9f)));
        Typography.DrawCentered(drawList, button.Center, label, new Vector4(1f, 1f, 1f, 1f),
            TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                layout.AddWidget(widget, size, targetPage);
                Close();
            }
        }

        return button.Max.Y;
    }

    private WidgetSize SelectedSize(IHomeWidget widget)
    {
        if (selections.TryGetValue(widget.Id, out var size) && WidgetSizes.Contains(widget.Sizes, size))
        {
            return size;
        }

        return WidgetSizes.Contains(widget.Sizes, WidgetSize.Medium)
            ? WidgetSize.Medium
            : WidgetSizes.Smallest(widget.Sizes);
    }

    private static string SizeLabel(WidgetSize size) => size switch
    {
        WidgetSize.Small => Loc.T(L.Home.SizeSmall),
        WidgetSize.Large => Loc.T(L.Home.SizeLarge),
        _ => Loc.T(L.Home.SizeMedium),
    };
}
