using Dalamud.Interface;

namespace Aetherphone.Core.SystemMedia;

internal static class PcMediaGlyph
{
    private static readonly (string Name, FontAwesomeIcon Icon)[] Known =
    {
        ("Spotify", FontAwesomeIcon.Headphones),
        ("TIDAL", FontAwesomeIcon.Headphones),
        ("Apple Music", FontAwesomeIcon.Headphones),
        ("foobar2000", FontAwesomeIcon.Music),
        ("MusicBee", FontAwesomeIcon.Music),
        ("AIMP", FontAwesomeIcon.Music),
        ("Winamp", FontAwesomeIcon.Music),
        ("Media Player", FontAwesomeIcon.Music),
        ("VLC", FontAwesomeIcon.Film),
        ("Movies & TV", FontAwesomeIcon.Film),
        ("Google Chrome", FontAwesomeIcon.Globe),
        ("Microsoft Edge", FontAwesomeIcon.Globe),
        ("Firefox", FontAwesomeIcon.Globe),
        ("Brave", FontAwesomeIcon.Globe),
        ("Opera", FontAwesomeIcon.Globe),
        ("Vivaldi", FontAwesomeIcon.Globe),
        ("Discord", FontAwesomeIcon.Comments),
    };

    public static FontAwesomeIcon For(string appName)
    {
        for (var index = 0; index < Known.Length; index++)
        {
            if (string.Equals(Known[index].Name, appName, StringComparison.Ordinal))
            {
                return Known[index].Icon;
            }
        }

        return FontAwesomeIcon.Desktop;
    }
}
