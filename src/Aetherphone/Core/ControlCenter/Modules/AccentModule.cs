using System.Numerics;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter.Modules;

internal sealed class AccentModule : IControlModule
{
    private static readonly ControlSpan[] SpanOptions = { ControlSpan.Bar };

    private readonly ThemeProvider themes;

    public AccentModule(ThemeProvider themes)
    {
        this.themes = themes;
    }

    public string Id => "accent";
    public string GalleryLabel => Loc.T(L.ControlCenter.Accent);
    public FontAwesomeIcon GalleryIcon => FontAwesomeIcon.Palette;
    public IReadOnlyList<ControlSpan> Sizes => SpanOptions;
    public ControlSpan DefaultSpan => ControlSpan.Bar;

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

        var swatchRadius = MathF.Min(rect.Height * 0.28f, 11f * scale);
        var innerLeft = rect.Min.X + 22f * scale;
        var innerRight = rect.Max.X - 22f * scale;
        var cell = (innerRight - innerLeft) / accents.Count;
        var centerY = rect.Center.Y;
        for (var index = 0; index < accents.Count; index++)
        {
            var center = new Vector2(innerLeft + cell * (index + 0.5f), centerY);
            var selected = accents[index].Name == Plugin.Cfg.AccentName;
            if (ControlTile.Swatch(dl, center, swatchRadius, accents[index].Color, selected, opacity,
                    context.Interactive) && !selected)
            {
                Plugin.Cfg.AccentName = accents[index].Name;
                themes.Apply(Plugin.Cfg);
                Plugin.Cfg.Save();
            }
        }
    }
}
