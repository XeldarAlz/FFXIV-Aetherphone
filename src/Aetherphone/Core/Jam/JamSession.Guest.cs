using Aetherphone.Core.Playback;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Video;

namespace Aetherphone.Core.Jam;

internal sealed partial class JamSession
{
    // Past the server's own stale window there is nothing trustworthy left to chase.
    private const double StaleStateSeconds = 30.0;

    private readonly ServerClock serverClock = new();
    private readonly JamDriftController drift = new();
    private Song remoteSong;
    private double remotePosition;
    private long remoteStateAtUnixMs;
    private bool remotePaused = true;
    private bool hasRemoteState;
    private bool localHold;

    public Song RemoteSong => remoteSong;
    public bool RemotePaused => remotePaused;

    private void BecomeGuest()
    {
        var wasHost = Mode == JamMode.Hosting;
        Mode = JamMode.Listening;
        hub.Authority = guestAuthority;
        if (wasHost)
        {
            ResetHostState();
        }
    }

    private void ResetGuestState()
    {
        serverClock.Reset();
        drift.Reset();
        remoteSong = default;
        remotePosition = 0d;
        remoteStateAtUnixMs = 0;
        remotePaused = true;
        hasRemoteState = false;
        localHold = false;
    }

    private bool HandleGuestIntent(in PlaybackIntent intent)
    {
        var owns = intent.Kind == PlaybackIntentKind.RemoveQueued && OwnsEntry(intent.EntryId);
        var context = new JamGuestContext(MyPermissions, localHold, remotePaused, owns);
        var decision = JamIntentRouting.ForGuest(intent.Kind, context);
        switch (decision.Route)
        {
            case JamRoute.ReleaseHold:
                localHold = false;
                ApplyRemoteState();
                break;
            case JamRoute.ControlPlay:
                TrySendControl(JamControlAction.Play, null);
                break;
            case JamRoute.ControlPause:
                TrySendControl(JamControlAction.Pause, null);
                break;
            case JamRoute.ControlNext:
                TrySendControl(JamControlAction.Next, null);
                break;
            case JamRoute.ControlPrevious:
                TrySendControl(JamControlAction.Previous, null);
                break;
            case JamRoute.ControlSeek:
                TrySendControl(JamControlAction.Seek, Math.Max(0d, intent.Seconds));
                break;
            case JamRoute.QueueNext:
            case JamRoute.QueueEnd:
                EnqueueAdd(SongOf(intent), decision.Route == JamRoute.QueueNext);
                break;
            case JamRoute.QueueRemove:
                EnqueueRemove(intent.EntryId);
                break;
            case JamRoute.QueueMove:
                EnqueueMove(intent.EntryId, intent.Index);
                break;
            case JamRoute.HoldLocally:
                stopRequested = true;
                break;
            case JamRoute.Refuse:
                Refuse(decision.Refusal);
                break;
        }

        return decision.Handled;
    }

    private void AnchorRemoteState(CallControl message, bool absorbClock)
    {
        var stamp = message.StateAtUnixMs ?? 0;
        if (absorbClock && stamp > 0)
        {
            serverClock.Absorb(stamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        var song = JamWire.ToSong(message.Track);
        if (!string.Equals(song.VideoId, remoteSong.VideoId, StringComparison.Ordinal))
        {
            ReleaseRate();
        }

        remoteSong = song;
        remotePosition = message.PositionSeconds is { } position && double.IsFinite(position)
            ? Math.Max(0d, position)
            : 0d;
        remoteStateAtUnixMs = stamp;
        remotePaused = message.Paused ?? false;
        hasRemoteState = true;
    }

    private void ApplyRemoteState()
    {
        if (Mode != JamMode.Listening || !hasRemoteState || localHold)
        {
            return;
        }

        if (remoteSong.IsEmpty)
        {
            ReleaseRate();
            if (hub.SongActive)
            {
                StopWithoutAuthority();
            }

            return;
        }

        var sameTrack = hub.SongActive
            && string.Equals(hub.CurrentSong.VideoId, remoteSong.VideoId, StringComparison.Ordinal);
        hub.PlayRemote(remoteSong, ProjectedPosition(), remotePaused);
        if (sameTrack && remotePaused && !hub.IsBuffering
            && Math.Abs(hub.Position - remotePosition) > JamDriftController.HardSeekSeconds)
        {
            hub.Songs.Seek((float)remotePosition);
        }
    }

    private double ProjectedPosition()
    {
        if (remotePaused)
        {
            return remotePosition;
        }

        return remotePosition + Math.Min(StateAgeSeconds(), StaleStateSeconds);
    }

    private double StateAgeSeconds()
    {
        if (remoteStateAtUnixMs <= 0)
        {
            return 0d;
        }

        var serverNow = serverClock.ServerNowUnixMs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return Math.Max(0d, (serverNow - remoteStateAtUnixMs) / 1000d);
    }

    private void TickGuest(float deltaSeconds)
    {
        if (localHold || !hasRemoteState || remoteSong.IsEmpty || remotePaused || Stale || !serverClock.Anchored)
        {
            ReleaseRate();
            return;
        }

        var age = StateAgeSeconds();
        var sameTrack = hub.SongActive
            && string.Equals(hub.CurrentSong.VideoId, remoteSong.VideoId, StringComparison.Ordinal);
        if (age > StaleStateSeconds || !sameTrack)
        {
            ReleaseRate();
            return;
        }

        var holding = hub.IsBuffering || hub.IsPaused;
        var decision = drift.Step(hub.Position, remotePosition + age, hub.Duration, holding, deltaSeconds);
        if (decision.RateChanged)
        {
            hub.SetRate(decision.Rate);
        }

        if (decision.Seek)
        {
            hub.Songs.Seek((float)decision.SeekTarget);
        }
    }

    private void StopWithoutAuthority()
    {
        var authority = hub.Authority;
        hub.Authority = null;
        hub.Stop();
        hub.Authority = authority;
    }

    private void ReleaseRate()
    {
        if (drift.Release())
        {
            hub.SetRate(1f);
        }
    }
}
