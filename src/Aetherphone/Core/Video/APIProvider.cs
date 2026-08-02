using Dalamud.Plugin.Ipc;

namespace Aetherphone.Core.Video;

// Ported from AlphaChannel's ApiProvider (Voudi, GPL-3.0), trimmed along with APIHelper - the
// SetState/ApplyStateUpdate/ClearState/StateChange gates existed only to receive other players'
// companion-derived state, which no longer applies. GetState/Version/OnReady/OnDispose remain: a
// low-cost way for another local Dalamud plugin to ask what AetherStream is currently playing.
internal static class ApiProvider
{
    private const int ApiVersionMajor = 1;
    private const int ApiVersionMinor = 0;

    private static ICallGateProvider<(int, int)>? _version;
    private static ICallGateProvider<string?>? _getState;
    private static ICallGateProvider<object?>? _onReady;
    private static ICallGateProvider<object?>? _onDispose;

    public static void Init(APIHelper helper)
    {
        _version   = Plugin.PluginInterface.GetIpcProvider<(int, int)>("Aetherphone.AetherStream.Version");
        _getState  = Plugin.PluginInterface.GetIpcProvider<string?>("Aetherphone.AetherStream.GetState");
        _onReady   = Plugin.PluginInterface.GetIpcProvider<object?>("Aetherphone.AetherStream.OnReady");
        _onDispose = Plugin.PluginInterface.GetIpcProvider<object?>("Aetherphone.AetherStream.OnDispose");

        _version.RegisterFunc(() => (ApiVersionMajor, ApiVersionMinor));
        _getState.RegisterFunc(helper.GetLocalState);

        _onReady.SendMessage();
    }

    public static void DeInit()
    {
        _onDispose?.SendMessage();

        _version?.UnregisterFunc();
        _getState?.UnregisterFunc();

        _version = null;
        _getState = null;
        _onReady = null;
        _onDispose = null;
    }
}
