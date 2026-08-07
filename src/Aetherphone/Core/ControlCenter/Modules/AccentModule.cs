using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter.Modules;

internal sealed class AccentModule : IControlModule
{
    private static readonly ControlSpan[] SpanOptions = { ControlSpan.Wide, ControlSpan.Bar };

    private readonly ThemeProvider themes;
    private readonly Configuration configuration;

    public AccentModule(ThemeProvider themes, Configuration configuration)
    {
        this.themes = themes;
        this.configuration = configuration;
    }

    public string Id => "accent";
    public string GalleryLabel => Loc.T(L.ControlCenter.Accent);
    public FontAwesomeIcon GalleryIcon => FontAwesomeIcon.Palette;
    public IReadOnlyList<ControlSpan> Sizes => SpanOptions;
    public ControlSpan DefaultSpan => ControlSpan.Wide;

    public void Draw(in ControlModuleContext context)
    {
        var dl = context.DrawList;
        var rect = context.Rect;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var radius = MathF.Min(rect.Width, rect.Height) * 0.30f;
        Squircle.Fill(dl, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f * opacity)));
        Material.EdgeSquircle(dl, rect.Min, rect.Max, radius, scale, opacity);
        var accents = ThemeCatalog.Accents;
        if (accents.Count == 0)
        {
            return;
        }

        var customHex = configuration.AccentCustomHex;
        var hasCustom = HexColor.TryParse(customHex, out var customColor);
        var slots = accents.Count + (hasCustom ? 1 : 0);
        var inset = MathF.Min(22f * scale, rect.Width * 0.10f);
        var innerLeft = rect.Min.X + inset;
        var innerRight = rect.Max.X - inset;
        var cell = (innerRight - innerLeft) / slots;
        var swatchRadius = MathF.Min(MathF.Min(rect.Height * 0.28f, 11f * scale), cell * 0.32f);
        var centerY = rect.Center.Y;
        for (var index = 0; index < slots; index++)
        {
            var center = new Vector2(innerLeft + cell * (index + 0.5f), centerY);
            var isCustom = index == accents.Count;
            var name = isCustom ? customHex : accents[index].Name;
            var color = isCustom ? customColor : accents[index].Color;
            var selected = name == configuration.AccentName;
            if (ControlTile.Swatch(dl, center, swatchRadius, color, selected, opacity, context.Interactive) &&
                !selected)
            {
                configuration.AccentName = name;
                themes.Apply(configuration);
                configuration.Save();
            }
        }
    }
}
