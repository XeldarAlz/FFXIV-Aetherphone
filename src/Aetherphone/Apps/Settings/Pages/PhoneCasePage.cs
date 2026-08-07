using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class PhoneCasePage : ISettingsPage
{
    private const int Columns = 3;

    private const float DeviceAspect = 0.443f;
    private const float LabelHeight = 36f;
    private const float PreviewScale = 0.55f;

    public string Title => Loc.T(L.Settings.PhoneCase);
    public string Summary => CatalogLabels.PhoneCase(configuration.PhoneCaseName);
    public FontAwesomeIcon Icon => FontAwesomeIcon.MobileAlt;
    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);
    private readonly Configuration configuration;
    private readonly ThemeProvider themes;

    public PhoneCasePage(Configuration configuration, ThemeProvider themes)
    {
        this.configuration = configuration;
        this.themes = themes;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        var gap = 12f * scale;
        var cases = ThemeCatalog.Cases;
        using (AppSurface.Begin(body))
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
        {
            var cellWidth = (ScrollLayout.StableContentWidth() - gap * (Columns - 1)) / Columns;
            var cellHeight = cellWidth / DeviceAspect + LabelHeight * scale;
            for (var index = 0; index < cases.Count; index++)
            {
                if (index % Columns != 0)
                {
                    ImGui.SameLine();
                }

                using (ImRaii.PushId(index))
                {
                    ImGui.Dummy(new Vector2(cellWidth, cellHeight));
                    DrawTile(cases[index], new Rect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax()), theme, scale);
                }
            }
        }
    }

    private void DrawTile(PhoneCase option, Rect cell, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var selected = option.Id == configuration.PhoneCaseName;
        var device = new Rect(cell.Min, new Vector2(cell.Max.X, cell.Max.Y - LabelHeight * scale));
        var hovered = UiInteract.Hover(cell.Min, cell.Max);
        if (selected)
        {
            Elevation.Floating(drawList, device.Min, device.Max, device.Width * 0.155f, scale, 0.7f);
        }

        PhoneCasePreview.Draw(drawList, device, option, theme, scale * PreviewScale);
        if (selected)
        {
            Squircle.Stroke(drawList, device.Min, device.Max, device.Width * 0.155f,
                ImGui.GetColorU32(theme.Accent), 2.5f * scale);
        }

        var label = CatalogLabels.PhoneCase(option.Id);
        var labelColor = selected ? theme.Accent : theme.TextMuted;
        var labelStyle = selected ? TextStyles.SubheadlineEmphasized : TextStyles.Subheadline;
        Typography.DrawWrappedCentered(drawList, new Vector2(cell.Center.X, device.Max.Y + LabelHeight * scale * 0.5f),
            label, labelColor, labelStyle, cell.Width);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!selected && UiInteract.Click(cell.Min, cell.Max, hovered))
        {
            Apply(option.Id);
        }
    }

    private void Apply(string name)
    {
        configuration.PhoneCaseName = name;
        themes.Apply(configuration);
        configuration.Save();
    }
}
