using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace Aetherphone.Core.Glamour;

internal sealed class PenumbraBridge : IDisposable
{
    public const int PlayerIndex = 0;

    private const string InternalName = "Penumbra";
    private const int SupportedBreakingVersion = 5;
    private const int SettingsKey = 0x41455050;
    private const string SettingsSource = "Aetherphone";
    private const int TemporaryPriority = 100;
    private const int RedrawNow = 0;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICallGateSubscriber<(int, int)> apiVersionGate;
    private readonly ICallGateSubscriber<bool> enabledGate;
    private readonly ICallGateSubscriber<string> modDirectoryGate;
    private readonly ICallGateSubscriber<Dictionary<string, string>> modListGate;
    private readonly ICallGateSubscriber<Dictionary<ushort, Dictionary<string, HashSet<string>>>> playerPathsGate;
    private readonly ICallGateSubscriber<int, (bool, bool, (Guid, string))> collectionGate;
    private readonly ICallGateSubscriber<Guid, string, string, bool, (int, (bool, int, Dictionary<string, List<string>>, bool)?)>
        settingsGate;
    private readonly ICallGateSubscriber<int, string, string,
        (bool, bool, int, IReadOnlyDictionary<string, IReadOnlyList<string>>), string, int, int> temporaryGate;
    private readonly ICallGateSubscriber<int, int, int> clearTemporaryGate;
    private readonly ICallGateSubscriber<int, int, object> redrawGate;
    private readonly ICallGateSubscriber<object> initializedGate;
    private readonly ICallGateSubscriber<object> disposedGate;
    private volatile bool readySignaled;
    private volatile bool disposingSignaled;

    public PenumbraBridge(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        apiVersionGate = pluginInterface.GetIpcSubscriber<(int, int)>("Penumbra.ApiVersion.V5");
        enabledGate = pluginInterface.GetIpcSubscriber<bool>("Penumbra.GetEnabledState");
        modDirectoryGate = pluginInterface.GetIpcSubscriber<string>("Penumbra.GetModDirectory");
        modListGate = pluginInterface.GetIpcSubscriber<Dictionary<string, string>>("Penumbra.GetModList");
        playerPathsGate = pluginInterface
            .GetIpcSubscriber<Dictionary<ushort, Dictionary<string, HashSet<string>>>>("Penumbra.GetPlayerResourcePaths.V5");
        collectionGate = pluginInterface
            .GetIpcSubscriber<int, (bool, bool, (Guid, string))>("Penumbra.GetCollectionForObject.V5");
        settingsGate = pluginInterface
            .GetIpcSubscriber<Guid, string, string, bool, (int, (bool, int, Dictionary<string, List<string>>, bool)?)>(
                "Penumbra.GetCurrentModSettings.V5");
        temporaryGate = pluginInterface.GetIpcSubscriber<int, string, string,
            (bool, bool, int, IReadOnlyDictionary<string, IReadOnlyList<string>>), string, int, int>(
            "Penumbra.SetTemporaryModSettingsPlayer.V5");
        clearTemporaryGate = pluginInterface.GetIpcSubscriber<int, int, int>("Penumbra.RemoveAllTemporaryModSettingsPlayer.V5");
        redrawGate = pluginInterface.GetIpcSubscriber<int, int, object>("Penumbra.RedrawObject.V5");
        initializedGate = pluginInterface.GetIpcSubscriber<object>("Penumbra.Initialized");
        disposedGate = pluginInterface.GetIpcSubscriber<object>("Penumbra.Disposed");
        initializedGate.Subscribe(OnReady);
        disposedGate.Subscribe(OnDisposing);
    }

    public bool ConsumeReady()
    {
        if (!readySignaled)
        {
            return false;
        }

        readySignaled = false;
        return true;
    }

    public bool ConsumeDisposing()
    {
        if (!disposingSignaled)
        {
            return false;
        }

        disposingSignaled = false;
        return true;
    }

    public bool Probe()
    {
        if (!GlamourPlugins.IsLoaded(pluginInterface, InternalName))
        {
            return false;
        }

        try
        {
            var (breaking, _) = apiVersionGate.InvokeFunc();
            return breaking == SupportedBreakingVersion;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Penumbra API version check failed: {exception.Message}");
            return false;
        }
    }

    public bool Enabled() => Call(enabledGate.InvokeFunc, false, "GetEnabledState");

    public string ModDirectory() => Call(modDirectoryGate.InvokeFunc, string.Empty, "GetModDirectory");

    public Dictionary<string, string> ModList() => Call(modListGate.InvokeFunc, new Dictionary<string, string>(), "GetModList");

    public Dictionary<string, HashSet<string>>? PlayerPaths()
    {
        var trees = Call(playerPathsGate.InvokeFunc, null, "GetPlayerResourcePaths");
        return trees is not null && trees.TryGetValue(PlayerIndex, out var paths) ? paths : null;
    }

    public bool TryPlayerCollection(out Guid collection)
    {
        var (valid, _, (id, _)) = Call(() => collectionGate.InvokeFunc(PlayerIndex), default, "GetCollectionForObject");
        collection = id;
        return valid && id != Guid.Empty;
    }

    public Dictionary<string, List<string>>? CurrentOptions(Guid collection, string directory)
    {
        var (_, settings) = Call(() => settingsGate.InvokeFunc(collection, directory, string.Empty, false), default,
            "GetCurrentModSettings");
        return settings is { Item1: true } found ? found.Item3 : null;
    }

    public bool SetTemporary(string directory, IReadOnlyDictionary<string, IReadOnlyList<string>> options)
    {
        var result = Call(() => temporaryGate.InvokeFunc(PlayerIndex, directory, string.Empty,
            (false, true, TemporaryPriority, options), SettingsSource, SettingsKey), -1, "SetTemporaryModSettingsPlayer");
        return result == 0;
    }

    public void ClearTemporary() =>
        Call(() => clearTemporaryGate.InvokeFunc(PlayerIndex, SettingsKey), -1, "RemoveAllTemporaryModSettingsPlayer");

    public void Redraw()
    {
        try
        {
            redrawGate.InvokeAction(PlayerIndex, RedrawNow);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Penumbra RedrawObject failed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        initializedGate.Unsubscribe(OnReady);
        disposedGate.Unsubscribe(OnDisposing);
    }

    private static T Call<T>(Func<T> call, T fallback, string name)
    {
        try
        {
            return call();
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Penumbra {name} failed: {exception.Message}");
            return fallback;
        }
    }

    private void OnReady() => readySignaled = true;

    private void OnDisposing() => disposingSignaled = true;
}
