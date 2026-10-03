using Aetherphone.Core.Jam;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Music.Jam;

internal static class JamMessages
{
    public static LocString? Decline(JamDeclineReason reason)
    {
        return reason switch
        {
            JamDeclineReason.BadCode => L.Music.Jam.DeclineBadCode,
            JamDeclineReason.Full => L.Music.Jam.DeclineFull,
            JamDeclineReason.Denied => L.Music.Jam.DeclineDenied,
            JamDeclineReason.Ended => L.Music.Jam.DeclineEnded,
            JamDeclineReason.Busy => L.Music.Jam.DeclineBusy,
            JamDeclineReason.Kicked => L.Music.Jam.DeclineKicked,
            JamDeclineReason.ConnectionLost => L.Music.Jam.DeclineConnectionLost,
            JamDeclineReason.NoResponse => L.Music.Jam.DeclineNoResponse,
            JamDeclineReason.Offline => L.Music.Jam.DeclineOffline,
            _ => null,
        };
    }

    public static LocString? Refusal(JamRefusal refusal)
    {
        return refusal switch
        {
            JamRefusal.HostControlsPlayback => L.Music.Jam.RefusalPlayback,
            JamRefusal.HostControlsQueue => L.Music.Jam.RefusalQueue,
            JamRefusal.NotYourEntry => L.Music.Jam.RefusalNotYours,
            _ => null,
        };
    }
}
