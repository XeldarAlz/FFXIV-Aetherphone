using Dalamud.Configuration;

namespace Aetherphone;

[Serializable]
internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool OpenOnStartup { get; set; }

    public bool WelcomeShown { get; set; }

    public bool LockPosition { get; set; }

    public bool DoNotDisturb { get; set; }

    public string AccentName { get; set; } = "Violet";

    public string WallpaperName { get; set; } = "Aurora";

    public uint RingtoneId { get; set; } = 7;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
