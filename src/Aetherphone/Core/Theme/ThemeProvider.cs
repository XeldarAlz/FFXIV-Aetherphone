namespace Aetherphone.Core.Theme;

internal sealed class ThemeProvider
{
    public PhoneTheme Current { get; private set; }

    public ThemeProvider(Configuration configuration) => Current = Build(configuration);

    public void Apply(Configuration configuration) => Current = Build(configuration);

    private static PhoneTheme Build(Configuration configuration)
        => PhoneTheme.From(ThemeCatalog.ResolveAccent(configuration.AccentName), configuration.LightWallpaperId, configuration.DarkWallpaperId);
}
