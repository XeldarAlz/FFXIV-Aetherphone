using Aetherphone.Core.Apps;
using Aetherphone.Core.ControlCenter.Modules;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter;

internal sealed class ControlRegistry : IControlRegistry
{
    private readonly List<IControlModule> modules = new();
    private readonly Dictionary<string, IControlModule> byId = new();

    public ControlRegistry(Configuration configuration, ThemeProvider themes, PlaybackHub playback, CallHub calls,
        INavigator navigation, Action dismiss, Coins.CoinStore coins, Aethernet.AethernetSession session,
        SystemMedia.PcMediaSource pcMedia)
    {
        Add(new ToggleModule("dnd", FontAwesomeIcon.Moon, L.Settings.DoNotDisturb,
            () => configuration.DoNotDisturb, () =>
            {
                configuration.DoNotDisturb = !configuration.DoNotDisturb;
                configuration.Save();
            }));
        Add(new ToggleModule("silent", FontAwesomeIcon.BellSlash, L.Settings.SilentMode,
            () => configuration.SilentMode, () =>
            {
                configuration.SilentMode = !configuration.SilentMode;
                configuration.Save();
            }));
        Add(new ToggleModule("calls", FontAwesomeIcon.Phone, L.Phone.Calls,
            () => configuration.CallsEnabled, () => calls.SetEnabled(!configuration.CallsEnabled)));
        Add(new ToggleModule("lock", FontAwesomeIcon.Thumbtack, L.ControlCenter.LockPosition,
            () => configuration.LockPosition, () =>
            {
                configuration.LockPosition = !configuration.LockPosition;
                configuration.Save();
            }));
        Add(new ToggleModule("idle", FontAwesomeIcon.HandPointUp, L.Settings.ScrollWhileIdle,
            () => configuration.ScrollWhileIdle, () =>
            {
                configuration.ScrollWhileIdle = !configuration.ScrollWhileIdle;
                configuration.Save();
            }));
        Add(new ClusterModule(ControlDefaults.ClusterId, ClusterMembers()));
        Add(new MediaModule(playback, pcMedia));
        Add(new SliderModule("brightness", L.ControlCenter.Brightness, () => FontAwesomeIcon.Sun,
            () => configuration.ScreenBrightness, value => configuration.ScreenBrightness = value,
            configuration.Save));
        Add(new SliderModule("volume", L.ControlCenter.Volume, VolumeIcon(playback),
            () => playback.Volume, value => playback.Volume = value, playback.CommitVolume));
        Add(new ToggleModule("camera", FontAwesomeIcon.Camera, L.Apps.Camera, () => false, () =>
        {
            navigation.Open("camera");
            dismiss();
        }));
        Add(new ToggleModule("settings", FontAwesomeIcon.Cog, L.Apps.Settings, () => false, () =>
        {
            navigation.Open("settings");
            dismiss();
        }));
        Add(new CoinModule(coins, session, navigation, dismiss));
        Add(new AccentModule(themes, configuration));
    }

    public IReadOnlyList<IControlModule> Modules => modules;

    public bool TryGet(string id, out IControlModule module) => byId.TryGetValue(id, out module!);

    private static Func<FontAwesomeIcon> VolumeIcon(PlaybackHub playback) => () =>
        playback.Volume <= 0.001f ? FontAwesomeIcon.VolumeMute
        : playback.Volume < 0.5f ? FontAwesomeIcon.VolumeDown : FontAwesomeIcon.VolumeUp;

    private ToggleModule[] ClusterMembers()
    {
        var ids = ControlDefaults.ClusterMembers;
        var members = new List<ToggleModule>(ids.Length);
        for (var index = 0; index < ids.Length; index++)
        {
            if (byId.TryGetValue(ids[index], out var module) && module is ToggleModule toggle)
            {
                members.Add(toggle);
            }
        }

        return members.ToArray();
    }

    private void Add(IControlModule module)
    {
        modules.Add(module);
        byId[module.Id] = module;
    }
}
