using Aetherphone.Core.Theme;

namespace Aetherphone.Core.Home;

[Serializable]
internal sealed class HomeLook
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<HomePage> Pages { get; set; } = new();
    public List<string>? Dock { get; set; }
    public int GridRows { get; set; } = HomeLayoutService.DefaultRows;
    public bool ShowAppNames { get; set; } = true;
    public ThemeMode ThemeMode { get; set; } = ThemeMode.Dark;
    public string AccentName { get; set; } = string.Empty;
    public string AccentCustomHex { get; set; } = string.Empty;
    public string PhoneCaseName { get; set; } = string.Empty;
    public string LightWallpaperId { get; set; } = string.Empty;
    public string DarkWallpaperId { get; set; } = string.Empty;
}
