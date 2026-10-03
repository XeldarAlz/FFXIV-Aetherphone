using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Jam;

internal sealed class JamLauncher
{
    private readonly LaunchIntent lobby = new();

    public void RequestLobby(string code) => lobby.Request(code);

    public bool TryConsumeLobby(out string code) => lobby.TryConsume(out code);
}
