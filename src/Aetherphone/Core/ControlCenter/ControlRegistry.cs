using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.ControlCenter.Modules;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter;

internal sealed class ControlRegistry
{
    private readonly List<IControlModule> modules = new();
    private readonly Dictionary<string, IControlModule> byId = new();

    public ControlRegistry(Configuration configuration, ThemeProvider themes, PlaybackHub playback, CallHub calls,
        INavigator navigation, Action dismiss)
    {
        Add(new ToggleModule("dnd", FontAwesomeIcon.Moon, L.Settings.DoNotDisturb,
            () => configuration.DoNotDisturb, () =>
            {
                configuration.DoNotDisturb = !configuration.DoNotDisturb;
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
        Add(new MediaModule(playback));
        Add(new SliderModule("brightness", L.ControlCenter.Brightness, () => FontAwesomeIcon.Sun,
            () => configuration.ScreenBrightness, value => configuration.ScreenBrightness = value,
            configuration.Save));
        Add(new SliderModule("volume", L.ControlCenter.Volume, VolumeIcon(playback),
            () => playback.Volume, value => playback.Volume = value, () => { }));
        Add(new ToggleModule("camera", FontAwesomeIcon.Camera, L.Apps.Camera, () => false, () =>
        {
            navigation.Open("camera", AppOpenSource.ControlCenter);
            dismiss();
        }));
        Add(new ToggleModule("settings", FontAwesomeIcon.Cog, L.Apps.Settings, () => false, () =>
        {
            navigation.Open("settings", AppOpenSource.ControlCenter);
            dismiss();
        }));
        Add(new AccentModule(themes, configuration));
    }

    public IReadOnlyList<IControlModule> Modules => modules;

    public bool TryGet(string id, out IControlModule module) => byId.TryGetValue(id, out module!);

    private static Func<FontAwesomeIcon> VolumeIcon(PlaybackHub playback) => () =>
        playback.Volume <= 0.001f ? FontAwesomeIcon.VolumeMute
        : playback.Volume < 0.5f ? FontAwesomeIcon.VolumeDown : FontAwesomeIcon.VolumeUp;

    private void Add(IControlModule module)
    {
        modules.Add(module);
        byId[module.Id] = module;
    }
}
