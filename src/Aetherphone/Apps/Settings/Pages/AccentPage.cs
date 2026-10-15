using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AccentPage : ISettingsPage
{
    private const string FieldId = "##settingsAccentHex";

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
            var result = ColorField.DrawCard("settings.accent", picker, theme);
            if (result.Dragging || result.Released)
            {
                surface.CancelDrag();
                Pick(result.Color, result.Released);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            if (HexField.Draw(FieldId, ref hexDigits, picker.ToRgb(), theme, scale))
            {
                Type();
            }
        }
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
