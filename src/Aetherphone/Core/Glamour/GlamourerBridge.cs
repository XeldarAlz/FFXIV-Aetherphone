using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Newtonsoft.Json.Linq;

namespace Aetherphone.Core.Glamour;

internal sealed class GlamourerBridge : IDisposable
{
    private const string InternalName = "Glamourer";
    private const int SupportedMajorVersion = 1;
    private const ulong OnceFlag = 0x01;
    private const ulong EquipmentFlag = 0x02;
    private const int Success = 0;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICallGateSubscriber<(int, int)> apiVersionGate;
    private readonly ICallGateSubscriber<int, byte, ulong, IReadOnlyList<byte>, uint, ulong, int> setItemGate;
    private readonly ICallGateSubscriber<int, uint, ulong, int> revertGate;
    private readonly ICallGateSubscriber<int, uint, (int, JObject?)> getStateGate;
    private readonly ICallGateSubscriber<object, int, uint, ulong, int> applyStateGate;
    private readonly ICallGateSubscriber<Dictionary<Guid, (string, string, uint, bool)>> designListGate;
    private readonly ICallGateSubscriber<Guid, JObject?> designGate;
    private readonly ICallGateSubscriber<string, string, (int, Guid)> addDesignGate;
    private readonly ICallGateSubscriber<Guid, int, uint, ulong, int> applyDesignGate;
    private readonly ICallGateSubscriber<object> initializedGate;
    private readonly ICallGateSubscriber<object> disposedGate;
    private volatile bool readySignaled;
    private volatile bool disposingSignaled;

    public GlamourerBridge(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        apiVersionGate = pluginInterface.GetIpcSubscriber<(int, int)>("Glamourer.ApiVersion.V2");
        setItemGate = pluginInterface
            .GetIpcSubscriber<int, byte, ulong, IReadOnlyList<byte>, uint, ulong, int>("Glamourer.SetItem.V3");
        revertGate = pluginInterface.GetIpcSubscriber<int, uint, ulong, int>("Glamourer.RevertState");
        getStateGate = pluginInterface.GetIpcSubscriber<int, uint, (int, JObject?)>("Glamourer.GetState");
        applyStateGate = pluginInterface.GetIpcSubscriber<object, int, uint, ulong, int>("Glamourer.ApplyState");
        designListGate = pluginInterface
            .GetIpcSubscriber<Dictionary<Guid, (string, string, uint, bool)>>("Glamourer.GetDesignListExtended");
        designGate = pluginInterface.GetIpcSubscriber<Guid, JObject?>("Glamourer.GetDesignJObject");
        addDesignGate = pluginInterface.GetIpcSubscriber<string, string, (int, Guid)>("Glamourer.AddDesign");
        applyDesignGate = pluginInterface.GetIpcSubscriber<Guid, int, uint, ulong, int>("Glamourer.ApplyDesign");
        initializedGate = pluginInterface.GetIpcSubscriber<object>("Glamourer.Initialized");
        disposedGate = pluginInterface.GetIpcSubscriber<object>("Glamourer.Disposed");
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
            var (major, _) = apiVersionGate.InvokeFunc();
            return major == SupportedMajorVersion;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Glamourer API version check failed: {exception.Message}");
            return false;
        }
    }

    public bool SetItem(GearSlot slot, uint itemId, byte stain0, byte stain1)
    {
        var stains = StainList(stain0, stain1);
        return Call(() => setItemGate.InvokeFunc(PenumbraBridge.PlayerIndex, GearSlots.GlamourerSlot(slot), itemId,
            stains, 0, OnceFlag | EquipmentFlag), -1, "SetItem") == Success;
    }

    // Dalamud round-trips IPC arguments through JSON unless the type matches exactly: a byte[] turns into base64 that
    // IReadOnlyList<byte> cannot read, while a List<byte> turns into a number array that it can.
    internal static List<byte> StainList(byte stain0, byte stain1) => new(2) { stain0, stain1 };

    public void RevertEquipment() =>
        Call(() => revertGate.InvokeFunc(PenumbraBridge.PlayerIndex, 0, EquipmentFlag), -1, "RevertState");

    public string State()
    {
        var (result, state) = Call(() => getStateGate.InvokeFunc(PenumbraBridge.PlayerIndex, 0), default, "GetState");
        return result == Success && state is not null ? state.ToString(Newtonsoft.Json.Formatting.None) : string.Empty;
    }

    public bool ApplyState(string state)
    {
        if (state.Length == 0)
        {
            return false;
        }

        JObject parsed;
        try
        {
            parsed = JObject.Parse(state);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Glamourer state could not be read: {exception.Message}");
            return false;
        }

        return Call(() => applyStateGate.InvokeFunc(parsed, PenumbraBridge.PlayerIndex, 0, OnceFlag | EquipmentFlag), -1,
            "ApplyState") == Success;
    }

    public Dictionary<Guid, (string, string, uint, bool)> Designs() =>
        Call(designListGate.InvokeFunc, new Dictionary<Guid, (string, string, uint, bool)>(), "GetDesignListExtended");

    public string Design(Guid id)
    {
        var design = Call(() => designGate.InvokeFunc(id), null, "GetDesignJObject");
        return design is null ? string.Empty : design.ToString(Newtonsoft.Json.Formatting.None);
    }

    public bool ApplyDesign(Guid id) =>
        Call(() => applyDesignGate.InvokeFunc(id, PenumbraBridge.PlayerIndex, 0, OnceFlag | EquipmentFlag), -1,
            "ApplyDesign") == Success;

    public Guid AddDesign(string state, string name)
    {
        var (result, id) = Call(() => addDesignGate.InvokeFunc(state, name), (-1, Guid.Empty), "AddDesign");
        return result == Success ? id : Guid.Empty;
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
            AepLog.Warning($"Glamourer {name} failed: {exception.Message}");
            return fallback;
        }
    }

    private void OnReady() => readySignaled = true;

    private void OnDisposing() => disposingSignaled = true;
}
