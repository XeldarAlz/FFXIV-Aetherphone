using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter;

internal sealed class ControlGallery
{
    private const float PopSmoothTime = 0.15f;
    private const float RowUnits = 52f;

    private readonly ControlLayoutService layout;
    private Spring pop;
    private bool open;
    private bool closing;
    private bool openedThisFrame;

    public ControlGallery(ControlLayoutService layout)
    {
        this.layout = layout;
    }

    public bool Active => open;

    public void Open()
    {
        open = true;
        closing = false;
        openedThisFrame = true;
        pop.SnapTo(0f);
    }

    public void Close() => closing = true;

    public void Draw(Rect region, PhoneTheme theme, float delta, float scale, float opacity)
    {
        if (!open)
        {
            return;
        }

        pop.Step(closing ? 0f : 1f, PopSmoothTime, delta);
        if (closing && pop.Value < 0.02f)
        {
            open = false;
            closing = false;
            return;
        }

        var hidden = layout.Hidden();
        var eased = Math.Clamp(pop.Value, 0f, 1f);
        var pad = 14f * scale;
        var rowHeight = RowUnits * scale;
        var headerHeight = 40f * scale;
        var count = Math.Max(1, hidden.Count);
        var contentHeight = headerHeight + count * rowHeight + pad;
        var height = MathF.Min(contentHeight, region.Height);
        var panelFull = new Rect(new Vector2(region.Min.X, region.Max.Y - height),
            new Vector2(region.Max.X, region.Max.Y));
        var pivot = new Vector2(panelFull.Center.X, panelFull.Max.Y);
        var panel = new Rect(pivot + (panelFull.Min - pivot) * eased, pivot + (panelFull.Max - pivot) * eased);
        var dl = ImGui.GetForegroundDrawList();
        var rounding = 26f * scale;
        Elevation.Floating(dl, panel.Min, panel.Max, rounding, scale, eased * opacity);
        Material.Frosted(dl, panel.Min, panel.Max, rounding, scale, eased * opacity);
        var interactive = !closing && eased > 0.9f;
        var alpha = eased * opacity;
        var closeRect = new Rect(new Vector2(panel.Max.X - 42f * scale, panel.Min.Y + 8f * scale),
            new Vector2(panel.Max.X - 8f * scale, panel.Min.Y + 42f * scale));
        var titleLeft = panel.Min.X + 20f * scale;
        var titleMaxWidth = MathF.Max(1f, closeRect.Min.X - 8f * scale - titleLeft);
        Typography.Draw(dl, new Vector2(titleLeft, panel.Min.Y + 14f * scale),
            Typography.FitText(Loc.T(L.ControlCenter.AddControls), titleMaxWidth, 0.95f, FontWeight.Bold),
            Palette.WithAlpha(theme.TextStrong, alpha), 0.95f, FontWeight.Bold);
        if (IconButton(dl, closeRect, FontAwesomeIcon.Times, theme, alpha, interactive))
        {
            Close();
        }

        if (hidden.Count == 0)
        {
            Typography.DrawCentered(dl, new Vector2(panel.Center.X, panel.Min.Y + headerHeight + rowHeight * 0.5f),
                Loc.T(L.ControlCenter.AllControlsAdded), Palette.WithAlpha(theme.TextMuted, alpha), 0.82f);
        }

        for (var index = 0; index < hidden.Count; index++)
        {
            var top = panel.Min.Y + headerHeight + index * rowHeight;
            var row = new Rect(new Vector2(panel.Min.X + pad, top),
                new Vector2(panel.Max.X - pad, top + rowHeight - 6f * scale));
            if (DrawRow(dl, row, hidden[index], theme, scale, alpha, interactive))
            {
                layout.Add(hidden[index]);
                if (layout.Hidden().Count == 0)
                {
                    Close();
                }
            }
        }

        if (interactive && !openedThisFrame && ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            !panel.Contains(ImGui.GetMousePos()))
        {
            Close();
        }

        openedThisFrame = false;
    }

    private static bool DrawRow(ImDrawListPtr dl, Rect row, IControlModule module, PhoneTheme theme, float scale,
        float alpha, bool interactive)
    {
        var hovered = interactive && ImGui.IsMouseHoveringRect(row.Min, row.Max);
        if (hovered)
        {
            Squircle.Fill(dl, row.Min, row.Max, 16f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f * alpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconRect = new Rect(new Vector2(row.Min.X + 6f * scale, row.Center.Y - 16f * scale),
            new Vector2(row.Min.X + 38f * scale, row.Center.Y + 16f * scale));
        Squircle.Fill(dl, iconRect.Min, iconRect.Max, 11f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, 0.9f * alpha)));
        ProgressRing.CenterIcon(dl, iconRect.Center, module.GalleryIcon, new Vector4(1f, 1f, 1f, alpha), 15f * scale);
        var addIconCenter = new Vector2(row.Max.X - 20f * scale, row.Center.Y);
        var labelStyle = new TextStyle(0.9f, FontWeight.Medium);
        var labelLeft = iconRect.Max.X + 14f * scale;
        var labelMaxWidth = MathF.Max(1f, addIconCenter.X - 17f * scale - 8f * scale - labelLeft);
        Marquee.DrawLeft(dl, "controlgallery.label." + module.Id, module.GalleryLabel, labelLeft,
            row.Center.Y - Typography.Measure(module.GalleryLabel, labelStyle).Y * 0.5f, labelMaxWidth, labelStyle,
            Palette.WithAlpha(theme.TextStrong, alpha), hovered);
        ProgressRing.CenterIcon(dl, addIconCenter, FontAwesomeIcon.PlusCircle,
            Palette.WithAlpha(theme.Accent, alpha), 17f * scale);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static bool IconButton(ImDrawListPtr dl, Rect rect, FontAwesomeIcon icon, PhoneTheme theme, float alpha,
        bool interactive)
    {
        var hovered = interactive && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var bg = hovered ? 0.16f : 0.10f;
        Squircle.Fill(dl, rect.Min, rect.Max, rect.Width * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, bg * alpha)));
        ProgressRing.CenterIcon(dl, rect.Center, icon, Palette.WithAlpha(theme.TextStrong, alpha), rect.Width * 0.34f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
