using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Updates;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed class UpdateChipWindow : Window
{
    private const ImGuiWindowFlags ChipFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar |
                                               ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
                                               ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground |
                                               ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing |
                                               ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoSavedSettings;

    private const float Gap = 9f;
    private const float ChipHeight = 32f;
    private const float SidePadding = 13f;
    private const float IconGap = 7f;
    private const float IconScale = 0.72f;
    private const float IconWidth = 11f;
    private const float TextScale = 0.84f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private readonly PhoneWindow phone;
    private readonly UpdateCheckService updates;
    private readonly ThemeProvider themes;

    public UpdateChipWindow(PhoneWindow phone, UpdateCheckService updates, ThemeProvider themes)
        : base($"{AepConstants.Name}##UpdateChip", ChipFlags)
    {
        this.phone = phone;
        this.updates = updates;
        this.themes = themes;
        IsOpen = true;
        RespectCloseHotkey = false;
    }

    public override bool DrawConditions() => phone.ShowsChrome && updates.UpdateAvailable;

    public override void PreDraw()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var label = Label();
        var textWidth = Typography.Measure(label, TextScale, FontWeight.SemiBold).X;
        var width = (textWidth + (SidePadding * 2f + IconWidth + IconGap) * scale) / scale;
        Size = new Vector2(width, ChipHeight);
        SizeCondition = ImGuiCond.Always;
        Position = new Vector2(phone.LastPosition.X + (phone.LastSize.X - width * scale) * 0.5f,
            phone.LastPosition.Y + phone.LastSize.Y + Gap * scale);
        PositionCondition = ImGuiCond.Always;
    }

    public override void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = themes.Chrome;
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        var hovered = ImGui.IsWindowHovered();
        DrawChip(theme, min, max, scale, hovered);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        HoverTooltip.Show(new Rect(min, max), Loc.T(L.Plugin.UpdateChipHint), HoverLabelSide.Below);
        HoverTooltip.Flush();
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            Plugin.PluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.UpdateablePlugins, AepConstants.Name);
        }
    }

    private void DrawChip(PhoneTheme theme, Vector2 min, Vector2 max, float scale, bool hovered)
    {
        var drawList = ImGui.GetForegroundDrawList();
        var rounding = (max.Y - min.Y) * 0.5f;
        Elevation.Floating(drawList, min, max, rounding, scale, 1f);
        var surface = IconTile.Surface(theme.Accent);
        var body = hovered ? Palette.Lighten(surface, 0.12f) : surface;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(body));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, hovered ? 0.34f : 0.18f)), scale);
        var ink = White;
        var label = Label();
        var textSize = Typography.Measure(label, TextScale, FontWeight.SemiBold);
        var contentWidth = textSize.X + (IconWidth + IconGap) * scale;
        var left = (min.X + max.X) * 0.5f - contentWidth * 0.5f;
        var centerY = (min.Y + max.Y) * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left + IconWidth * 0.5f * scale, centerY),
            FontAwesomeIcon.ArrowUp.ToIconString(), ink, IconScale);
        Typography.Draw(drawList, new Vector2(left + (IconWidth + IconGap) * scale, centerY - textSize.Y * 0.5f), label,
            ink, TextScale, FontWeight.SemiBold);
    }

    private string Label() => Loc.T(L.Plugin.UpdateChip, updates.LatestText);
}
