using Aetherphone.Core.Playback;

namespace Aetherphone.Core.Jam;

internal enum JamRoute : byte
{
    Local,
    LocalThenPublish,
    Advance,
    AdvanceTo,
    PlayNow,
    QueueNext,
    QueueEnd,
    QueueRemove,
    QueueMove,
    PauseForEveryone,
    ControlPlay,
    ControlPause,
    ControlSeek,
    ControlNext,
    ControlPrevious,
    HoldLocally,
    ReleaseHold,
    Swallow,
    Refuse,
    AwaitQueue,
}

internal readonly record struct JamRouteDecision(JamRoute Route, JamRefusal Refusal = JamRefusal.None)
{
    public bool Handled => Route is not (JamRoute.Local or JamRoute.LocalThenPublish);
}

internal readonly record struct JamHostContext(bool SongActive, int ServerQueueCount, bool RepeatOne,
    bool AddInFlight = false);

internal readonly record struct JamGuestContext(int Permissions, bool LocalHold, bool RoomPaused, bool OwnsEntry);

internal static class JamIntentRouting
{
    public static JamRouteDecision ForHost(PlaybackIntentKind kind, in JamHostContext context)
    {
        return kind switch
        {
            PlaybackIntentKind.TogglePlayPause => new(JamRoute.LocalThenPublish),
            PlaybackIntentKind.Seek => new(JamRoute.LocalThenPublish),
            PlaybackIntentKind.Previous => new(JamRoute.LocalThenPublish),
            PlaybackIntentKind.Next => new(context.ServerQueueCount > 0 ? JamRoute.Advance : JamRoute.LocalThenPublish),
            PlaybackIntentKind.PlayNext => new(context.SongActive ? JamRoute.QueueNext : JamRoute.Local),
            PlaybackIntentKind.PlayLast => new(context.SongActive ? JamRoute.QueueEnd : JamRoute.Local),
            PlaybackIntentKind.PlaySongs => new(JamRoute.PlayNow),
            PlaybackIntentKind.JumpTo => new(JamRoute.AdvanceTo),
            PlaybackIntentKind.RemoveQueued => new(JamRoute.QueueRemove),
            PlaybackIntentKind.MoveQueued => new(JamRoute.QueueMove),
            PlaybackIntentKind.Stop => new(JamRoute.PauseForEveryone),
            PlaybackIntentKind.TrackEnded => new(TrackEndRoute(context)),
            _ => new(JamRoute.Local),
        };
    }

    public static JamRouteDecision ForGuest(PlaybackIntentKind kind, in JamGuestContext context)
    {
        var control = JamPermission.Allows(context.Permissions, JamPermission.ControlPlayback);
        var add = JamPermission.Allows(context.Permissions, JamPermission.AddToQueue);
        return kind switch
        {
            PlaybackIntentKind.TogglePlayPause when context.LocalHold => new(JamRoute.ReleaseHold),
            PlaybackIntentKind.TogglePlayPause => Control(control,
                context.RoomPaused ? JamRoute.ControlPlay : JamRoute.ControlPause),
            PlaybackIntentKind.Seek => Control(control, JamRoute.ControlSeek),
            PlaybackIntentKind.Next => Control(control, JamRoute.ControlNext),
            PlaybackIntentKind.Previous => Control(control, JamRoute.ControlPrevious),
            PlaybackIntentKind.JumpTo => Refuse(JamRefusal.HostControlsPlayback),
            PlaybackIntentKind.PlayNext => Queue(add, JamRoute.QueueNext),
            PlaybackIntentKind.PlayLast => Queue(add, JamRoute.QueueEnd),
            PlaybackIntentKind.PlaySongs => Queue(add, JamRoute.QueueEnd),
            PlaybackIntentKind.MoveQueued => control ? new(JamRoute.QueueMove) : Refuse(JamRefusal.HostControlsQueue),
            PlaybackIntentKind.RemoveQueued => context.OwnsEntry
                ? new(JamRoute.QueueRemove)
                : Refuse(JamRefusal.NotYourEntry),
            PlaybackIntentKind.Stop => new(JamRoute.HoldLocally),
            PlaybackIntentKind.TrackEnded => new(JamRoute.Swallow),
            _ => new(JamRoute.Swallow),
        };
    }

    private static JamRoute TrackEndRoute(in JamHostContext context)
    {
        if (context.RepeatOne)
        {
            return JamRoute.LocalThenPublish;
        }

        if (context.ServerQueueCount > 0)
        {
            return JamRoute.Advance;
        }

        return context.AddInFlight ? JamRoute.AwaitQueue : JamRoute.LocalThenPublish;
    }

    private static JamRouteDecision Control(bool allowed, JamRoute route)
    {
        return allowed ? new(route) : Refuse(JamRefusal.HostControlsPlayback);
    }

    private static JamRouteDecision Queue(bool allowed, JamRoute route)
    {
        return allowed ? new(route) : Refuse(JamRefusal.HostControlsQueue);
    }

    private static JamRouteDecision Refuse(JamRefusal refusal) => new(JamRoute.Refuse, refusal);
}
