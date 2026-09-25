using Aetherphone.Core.Theme;

namespace Aetherphone.Core.Home;

internal interface ILookConfiguration
{
    HomeLayout? Home { get; set; }
    int HomeGridRows { get; set; }
    bool ShowAppNames { get; set; }
    ThemeMode ThemeMode { get; set; }
    string AccentName { get; set; }
    string AccentCustomHex { get; set; }
    string PhoneCaseName { get; set; }
    string LightWallpaperId { get; set; }
    string DarkWallpaperId { get; set; }
    List<HomeLook> Looks { get; }
    Dictionary<ulong, Guid> LookByCharacter { get; }
    Guid ActiveLookId { get; set; }
    void Save();
}
