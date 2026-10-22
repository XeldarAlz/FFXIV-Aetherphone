using Aetherphone.Core.Localization;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Aethernet;

internal sealed class NetworkStatusNotices : IDisposable
{
    private const string ChatTag = "Aetherphone";

    private readonly AethernetSession session;
    private readonly RealtimeSignalBus signals;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IChatGui chat;
    private readonly Configuration configuration;
    private readonly NetworkOutageTracker tracker = new();
    private volatile bool restartAnnounced;
    private bool tracking;

    public NetworkStatusNotices(AethernetSession session, RealtimeSignalBus signals, IFramework framework,
        IClientState clientState, IChatGui chat, Configuration configuration)
    {
        this.session = session;
        this.signals = signals;
        this.framework = framework;
        this.clientState = clientState;
        this.chat = chat;
        this.configuration = configuration;
        signals.ServerRestarting += OnServerRestarting;
        framework.Update += OnFrameworkUpdate;
    }

    private void OnServerRestarting()
    {
        restartAnnounced = true;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (!session.IsSignedIn)
        {
            if (tracking)
            {
                tracking = false;
                tracker.Reset();
            }

            restartAnnounced = false;
            return;
        }

        tracking = true;
        var now = Environment.TickCount64;
        var online = signals.RealtimeActive;
        if (online != tracker.Online)
        {
            if (online)
            {
                tracker.Connected(now);
            }
            else
            {
                tracker.Disconnected(now);
            }
        }

        if (restartAnnounced)
        {
            restartAnnounced = false;
            tracker.RestartAnnounced(now);
        }

        var notice = tracker.Tick(now);
        if (notice == NetworkNotice.None || !configuration.NetworkStatusInChat || !clientState.IsLoggedIn)
        {
            return;
        }

        chat.Print(Loc.T(TextFor(notice)), ChatTag);
    }

    private static LocString TextFor(NetworkNotice notice) => notice switch
    {
        NetworkNotice.Restarting => L.Plugin.NetworkRestarting,
        NetworkNotice.Lost => L.Plugin.NetworkLost,
        _ => L.Plugin.NetworkBack,
    };

    public void Dispose()
    {
        signals.ServerRestarting -= OnServerRestarting;
        framework.Update -= OnFrameworkUpdate;
    }
}
