using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class MinimizedPhonePage : ISettingsPage
{
    private const float ReorderRadius = 10f;
    private const float ReorderGap = 3f;
    private const float ToggleGap = 10f;

    public string Title => Loc.T(L.Minimized.Title);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.MobileAlt;
    public Vector4 Tint => new(0.30f, 0.62f, 0.92f, 1f);
    private readonly MinimizedLayoutService layout;
    private readonly Configuration configuration;
    private readonly string[] shapeLabels = new string[MinimizedShapes.ShapeCount];
    private readonly string[] mapSizeLabels = new string[MinimizedShapes.MapSizeCount];
    private int moveIndex = -1;
    private int moveDelta;

    public MinimizedPhonePage(MinimizedLayoutService layout, Configuration configuration)
    {
        this.layout = layout;
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            SettingsSection.Header(Loc.T(L.Minimized.Shape), theme);
            DrawShapePicker(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            SettingsSection.Hint(Loc.T(L.Minimized.ShapeHint), theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            if (configuration.MinimizedShape == MinimizedShape.Minimap)
            {
                DrawMinimapSettings(theme, scale);
            }
            else
            {
                DrawPhoneSettings(theme, scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }

        ApplyPendingMove();
    }

    private void DrawShapePicker(PhoneTheme theme)
    {
        for (var index = 0; index < shapeLabels.Length; index++)
        {
            shapeLabels[index] = Loc.T(MinimizedShapes.Label((MinimizedShape)index));
        }

        var card = GroupCard.Begin(theme, 1);
        var picked = SegmentStrip.Draw("minimized.shape", card.NextRow(), shapeLabels,
            (int)configuration.MinimizedShape, theme);
        card.End();
        if (picked == (int)configuration.MinimizedShape)
        {
            return;
        }

        configuration.MinimizedShape = (MinimizedShape)picked;
        configuration.Save();
    }

    private void DrawMinimapSettings(PhoneTheme theme, float scale)
    {
        for (var index = 0; index < mapSizeLabels.Length; index++)
        {
            mapSizeLabels[index] = Loc.T(MinimizedShapes.Label((MinimizedMapSize)index));
        }

        SettingsSection.Header(Loc.T(L.Minimized.Size), theme);
        var card = GroupCard.Begin(theme, 1);
        var picked = SegmentStrip.Draw("minimized.mapSize", card.NextRow(), mapSizeLabels,
            (int)configuration.MinimizedMapSize, theme);
        card.End();
        if (picked != (int)configuration.MinimizedMapSize)
        {
            configuration.MinimizedMapSize = (MinimizedMapSize)picked;
            configuration.Save();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        SettingsSection.Hint(Loc.T(L.Minimized.MinimapHint), theme);
    }

    private void DrawPhoneSettings(PhoneTheme theme, float scale)
    {
        var wallpaperCard = GroupCard.Begin(theme, 1);
        var wallpaper = SettingsRow.Bool(wallpaperCard.NextRow(), Loc.T(L.Minimized.Wallpaper),
            configuration.MinimizedWallpaper, theme, "minimized.wallpaper");
        wallpaperCard.End();
        if (wallpaper != configuration.MinimizedWallpaper)
        {
            configuration.MinimizedWallpaper = wallpaper;
            configuration.Save();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        SettingsSection.Hint(Loc.T(L.Minimized.WallpaperHint), theme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        SettingsSection.Hint(Loc.T(L.Minimized.Hint), theme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var slots = layout.Slots;
        var card = GroupCard.Begin(theme, slots.Length);
        for (var index = 0; index < slots.Length; index++)
        {
            DrawPartRow(card.NextRow(), index, slots[index], slots.Length, theme, scale);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        var resetCard = GroupCard.Begin(theme, 1);
        if (SettingsRow.Action(resetCard.NextRow(), Loc.T(L.Minimized.Reset), theme.Danger, theme))
        {
            layout.Reset();
        }

        resetCard.End();
    }

    private void DrawPartRow(Rect row, int index, in MinimizedSlot slot, int count, PhoneTheme theme, float scale)
    {
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var toggleMin = new Vector2(row.Max.X - toggleWidth, row.Center.Y - toggleHeight * 0.5f);
        var radius = ReorderRadius * scale;
        var downCenter = new Vector2(toggleMin.X - ToggleGap * scale - radius, row.Center.Y);
        var upCenter = new Vector2(downCenter.X - radius * 2f - ReorderGap * scale, row.Center.Y);
        var label = Loc.T(MinimizedParts.Label(slot.Part));
        var rowId = "minimized.part." + MinimizedParts.Id(slot.Part);
        var labelMaxWidth = MathF.Max(1f, upCenter.X - radius - 8f * scale - row.Min.X);
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        Marquee.DrawLeftAuto(rowId, label, row.Min.X, row.Center.Y - labelSize.Y * 0.5f, labelMaxWidth,
            TextStyles.BodyEmphasized, slot.Enabled ? theme.TextStrong : theme.TextMuted);
        if (ReorderButton(upCenter, radius, FontAwesomeIcon.ChevronUp, theme, index > 0))
        {
            moveIndex = index;
            moveDelta = -1;
        }

        if (ReorderButton(downCenter, radius, FontAwesomeIcon.ChevronDown, theme, index < count - 1))
        {
            moveIndex = index;
            moveDelta = 1;
        }

        var enabled = Toggle.Draw(rowId, new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, toggleHeight)),
            slot.Enabled, theme);
        if (enabled != slot.Enabled)
        {
            layout.SetEnabled(index, enabled);
        }
    }

    private static bool ReorderButton(Vector2 center, float radius, FontAwesomeIcon icon, PhoneTheme theme,
        bool enabled)
    {
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && UiInteract.Hover(min, max);
        if (hovered)
        {
            drawList.AddCircleFilled(center, radius,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.10f)), 24);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var ink = enabled ? theme.TextMuted : Palette.WithAlpha(theme.TextMuted, theme.TextMuted.W * 0.25f);
        ProgressRing.CenterIcon(drawList, center, icon, ink, radius);
        return enabled && UiInteract.Click(min, max, hovered);
    }

    private void ApplyPendingMove()
    {
        if (moveIndex < 0)
        {
            return;
        }

        layout.Move(moveIndex, moveDelta);
        moveIndex = -1;
        moveDelta = 0;
    }
}
