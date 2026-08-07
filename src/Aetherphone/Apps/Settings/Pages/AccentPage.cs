using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AccentPage : ISettingsPage
{
    private const string FieldId = "##settingsAccentHex";
    private const int HexDigitCount = 6;
    private const float PickerHeight = 210f;

    public string Title => Loc.T(L.Settings.Accent);
    public string Summary => CatalogLabels.Accent(configuration.AccentName);
    public FontAwesomeIcon Icon => FontAwesomeIcon.Palette;
    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);
    private readonly Configuration configuration;
    private readonly ThemeProvider themes;
    private HsvColor picker;
    private HsvColor applied;
    private string hexDigits;

    public AccentPage(Configuration configuration, ThemeProvider themes)
    {
        this.configuration = configuration;
        this.themes = themes;
        var accent = ThemeCatalog.ResolveAccent(configuration.AccentName);
        picker = HsvColor.FromRgb(accent);
        applied = picker;
        hexDigits = HexColor.ToDigits(accent);
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (var surface = AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.AccentCustom), theme);
            var card = GroupCard.Begin(theme, 1, PickerHeight);
            var row = card.NextRow();
            var pad = Metrics.Space.Lg * scale;
            var field = new Rect(new Vector2(row.Min.X, row.Min.Y + pad), new Vector2(row.Max.X, row.Max.Y - pad));
            var result = ColorField.Draw("settings.accent", field, picker, theme,
                ImGui.GetColorU32(theme.GroupedCard));
            card.End();
            if (result.Dragging || result.Released)
            {
                surface.CancelDrag();
                Pick(result.Color, result.Released);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawHexField(theme, scale);
        }
    }

    private void DrawHexField(PhoneTheme theme, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = Metrics.Size.FieldHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height),
            Metrics.Radius.Field * scale, ImGui.GetColorU32(theme.GroupedCard));
        var chipRadius = height * 0.5f - Metrics.Space.Xs * scale;
        var chipCenter = new Vector2(origin.X + Metrics.Space.Md * scale + chipRadius, origin.Y + height * 0.5f);
        drawList.AddCircleFilled(chipCenter, chipRadius, ImGui.GetColorU32(picker.ToRgb()), 24);
        drawList.AddCircle(chipCenter, chipRadius, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.18f)), 24,
            Metrics.Stroke.Hairline * scale);
        var hashLeft = chipCenter.X + chipRadius + Metrics.Space.Md * scale;
        var hashSize = Typography.Measure("#", TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(hashLeft, origin.Y + height * 0.5f - hashSize.Y * 0.5f), "#",
            theme.TextMuted, TextStyles.BodyEmphasized);
        var inputLeft = hashLeft + hashSize.X + Metrics.Space.Xxs * scale;
        ImGui.SetCursorScreenPos(new Vector2(inputLeft, origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(origin.X + width - Metrics.Space.Md * scale - inputLeft);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f))
                   .Push(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputText(FieldId, ref hexDigits, HexDigitCount,
                    ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase))
            {
                Type();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void Pick(HsvColor color, bool released)
    {
        picker = color;
        if (picker != applied)
        {
            applied = picker;
            hexDigits = HexColor.ToDigits(picker.ToRgb());
            Apply(hexDigits, false);
        }

        if (released)
        {
            configuration.Save();
        }
    }

    private void Type()
    {
        if (!HexColor.TryParse(hexDigits, out var typed))
        {
            return;
        }

        picker = HsvColor.FromRgb(typed);
        applied = picker;
        Apply(hexDigits, true);
    }

    private void Apply(string digits, bool persist)
    {
        var hex = "#" + digits;
        configuration.AccentName = hex;
        configuration.AccentCustomHex = hex;
        themes.Apply(configuration);
        if (persist)
        {
            configuration.Save();
        }
    }
}
