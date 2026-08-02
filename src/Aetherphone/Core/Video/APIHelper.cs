using Newtonsoft.Json;

namespace Aetherphone.Core.Video;

// Ported from AlphaChannel's APIHelper (Voudi, GPL-3.0), trimmed to the local-state query only.
// The original also received other players' state over Dalamud IPC and applied it to their
// scanned companion - that whole path was companion-tracking-dependent and is gone along with
// it (Stage 4); Aetherphone already has its own, better cross-player watch-along sync in
// WatchAlongSession, over Aethernet rather than a same-plugin Dalamud IPC handshake. What's kept
// here is just "let another local Dalamud plugin ask AetherStream what it's currently playing",
// which needs no companion at all.
internal class APIHelper
{
    private readonly VideoEngine _core;
    private readonly Resources _resources;

    internal sealed record IPCVideoState(
        [property: JsonRequired] string State,
        [property: JsonRequired] string Url,
        [property: JsonRequired] int PlaybackPosition,
        [property: JsonRequired] long Timestamp);

    internal APIHelper(VideoEngine core, Resources resources)
    {
        _core = core;
        _resources = resources;
    }

    internal string? GetLocalState()
    {
        if (!_core.IsActive)
        {
            return null;
        }

        string? url = _core.GetCurrentUrl();
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        int pos = (int)_core.GetInfo()[0];
        string stateStr = _core.GetPaused() ? "paused" : "playing";

        return JsonConvert.SerializeObject(new IPCVideoState(stateStr, Uri.EscapeDataString(url), pos, _resources.CurrentTimeNTPNormalizedMilliseconds));
    }
}
