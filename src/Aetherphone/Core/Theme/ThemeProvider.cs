using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Core.Theme;

internal sealed class ThemeProvider
{
    private readonly Configuration configuration;
    private readonly WallpaperLibrary wallpapers;
    private PhoneTheme light = PhoneTheme.Default;
    private PhoneTheme dark = PhoneTheme.Default;

    public ThemeProvider(Configuration configuration, WallpaperLibrary wallpapers)
    {
        this.configuration = configuration;
        this.wallpapers = wallpapers;
        Rebuild();
    }

    public PhoneTheme Current => Select();
    public PhoneTheme Chrome => dark;
    public void Apply(Configuration configuration) => Rebuild();

    public PhoneTheme ForApp(bool wantsSystemTheme) => wantsSystemTheme ? Select() : dark;

    private void Rebuild()
    {
        var accent = ThemeCatalog.ResolveAccent(configuration.AccentName);
        var caseColor = ThemeCatalog.ResolveCase(configuration.PhoneCaseName);
        light = PhoneTheme.Light(accent, caseColor, configuration.LightWallpaperId, configuration.DarkWallpaperId);
        dark = PhoneTheme.Dark(accent, caseColor, configuration.LightWallpaperId, configuration.DarkWallpaperId);
    }

    private PhoneTheme Select() =>
        configuration.ThemeMode switch
        {
            ThemeMode.Light => light,
            ThemeMode.Dark => dark,
            _ => wallpapers.Darkness >= 0.5f ? dark : light,
        };
}
