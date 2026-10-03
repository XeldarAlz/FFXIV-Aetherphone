using Aetherphone.Core.Playback;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Jam;

internal sealed partial class JamSession
{
    // Comfortably inside the server's 30 s stale window even if one heartbeat is lost.
    private const float HeartbeatSeconds = 8f;
    private const double PositionJumpSeconds = 2.0;

    // Seeding goes through the queue pacer (7 operations per 4.25 s), so 20 songs land in about 12 s.
    private const int SeedCap = 20;

    private bool publishRequested;
    private bool seedPending;
    private bool hasPublished;
    private float heartbeatTimer;
    private string lastPublishedVideoId = string.Empty;
    private double lastPublishedPosition;
    private long lastPublishedAtTicks;
    private bool lastPublishedPaused;
    private int advanceEntryId;
    private Song advanceSong;
    private bool advanceArmed;
    private bool advanceRetried;
    private bool advanceFromTrackEnd;
    private long advanceRequestedAtTicks;
    private long queueWaitSinceTicks;

    private void BecomeHost()
    {
        var wasGuest = Mode == JamMode.Listening;
        Mode = JamMode.Hosting;
        hub.Authority = hostAuthority;
        if (wasGuest)
        {
            ResetGuestState();
            hub.SetRate(1f);
        }

        publishRequested = true;
        if (seedPending)
        {
            seedPending = false;
            SeedFromLocalQueue();
        }
    }

    private void ResetHostState()
    {
        publishRequested = false;
        seedPending = false;
        hasPublished = false;
        heartbeatTimer = 0f;
        lastPublishedVideoId = string.Empty;
        lastPublishedPosition = 0d;
        lastPublishedAtTicks = 0;
        lastPublishedPaused = false;
        queueWaitSinceTicks = 0;
        ClearAdvance();
    }

    private bool HandleHostIntent(in PlaybackIntent intent)
    {
        var context = new JamHostContext(hub.SongActive, queue.Length, hub.RepeatMode == SongRepeatMode.One,
            AddInFlight(Environment.TickCount64));
        var decision = JamIntentRouting.ForHost(intent.Kind, context);
        switch (decision.Route)
        {
            case JamRoute.LocalThenPublish:
                publishRequested = true;
                return false;
            case JamRoute.Advance:
                RequestAdvance(0, intent.Kind == PlaybackIntentKind.TrackEnded);
                return true;
            case JamRoute.AdvanceTo:
                RequestAdvance(intent.EntryId, false);
                return true;
            case JamRoute.AwaitQueue:
                queueWaitSinceTicks = Environment.TickCount64;
                publishRequested = true;
                return true;
            case JamRoute.PlayNow:
                PlayNowAsHost(intent.Songs, intent.Index);
                return true;
            case JamRoute.QueueNext:
            case JamRoute.QueueEnd:
                EnqueueAdd(SongOf(intent), decision.Route == JamRoute.QueueNext);
                return true;
            case JamRoute.QueueRemove:
                EnqueueRemove(intent.EntryId);
                return true;
            case JamRoute.QueueMove:
                EnqueueMove(intent.EntryId, intent.Index);
                return true;
            case JamRoute.PauseForEveryone:
                stopRequested = true;
                return true;
            default:
                return decision.Handled;
        }
    }

    private void SeedFromLocalQueue()
    {
        var localQueue = hub.Queue;
        var count = Math.Min(localQueue.QueuedCount, SeedCap);
        for (var index = 0; index < count; index++)
        {
            EnqueueAdd(localQueue.QueuedAt(index).Song, false);
        }

        hub.ClearQueued();
        localQueue.ClearAutoplay();
    }

    private void PlayNowAsHost(Song[] songs, int index)
    {
        if (songs.Length == 0)
        {
            return;
        }

        var start = Math.Clamp(index, 0, songs.Length - 1);
        ClearAdvance();
        queueWaitSinceTicks = 0;
        hub.PlayRemote(songs[start], 0d, false);
        publishRequested = true;
        if (queue.Length > 0)
        {
            return;
        }

        var last = Math.Min(songs.Length, start + 1 + SeedCap);
        for (var songIndex = start + 1; songIndex < last; songIndex++)
        {
            EnqueueAdd(songs[songIndex], false);
        }
    }

    private void RequestAdvance(int entryId, bool fromTrackEnd)
    {
        var index = entryId == 0 ? (queue.Length > 0 ? 0 : -1) : JamWire.IndexOfEntry(queue, entryId);
        if (index < 0)
        {
            return;
        }

        var target = queue[index];
        queueWaitSinceTicks = 0;
        advanceEntryId = target.EntryId;
        advanceSong = target.Song;
        advanceArmed = false;
        advanceRetried = false;
        advanceFromTrackEnd = fromTrackEnd;
        SendAdvance();
    }

    private void SendAdvance()
    {
        advanceRequestedAtTicks = Environment.TickCount64;
        Enqueue(new CallControl { Type = SignalType.JamQueueAdvance, EntryId = advanceEntryId }, urgent: true);
    }

    private void ArmAdvanceIfPopped()
    {
        if (Mode == JamMode.Hosting && advanceEntryId != 0 && !advanceArmed
            && JamWire.IndexOfEntry(queue, advanceEntryId) < 0)
        {
            advanceArmed = true;
        }
    }

    private void OnHostQueue()
    {
        ArmAdvanceIfPopped();
        if (Mode == JamMode.Hosting && queueWaitSinceTicks != 0 && queue.Length > 0)
        {
            RequestAdvance(0, true);
        }
    }

    private void OnHostStateEcho(CallControl message)
    {
        if (!advanceArmed || message.Track is not { } track
            || !string.Equals(track.VideoId, advanceSong.VideoId, StringComparison.Ordinal))
        {
            return;
        }

        ClearAdvance();
        PlayAdvanced(JamWire.ToSong(track));
    }

    private void PlayAdvanced(in Song song)
    {
        if (hub.SongActive && string.Equals(hub.CurrentSong.VideoId, song.VideoId, StringComparison.Ordinal))
        {
            hub.Songs.Seek(0f);
            if (hub.IsPaused)
            {
                hub.ApplyTogglePlayPause();
            }
        }
        else
        {
            hub.PlayRemote(song, 0d, false);
        }

        publishRequested = true;
    }

    private void ClearAdvance()
    {
        advanceEntryId = 0;
        advanceSong = default;
        advanceArmed = false;
        advanceRetried = false;
        advanceFromTrackEnd = false;
        advanceRequestedAtTicks = 0;
    }

    private void TickAdvance(long now)
    {
        switch (JamHostRecovery.Advance(now - advanceRequestedAtTicks, advanceRetried, advanceArmed))
        {
            case JamAdvanceStep.Retry:
                advanceRetried = true;
                SendAdvance();
                return;
            case JamAdvanceStep.PlayPopped:
                var popped = advanceSong;
                ClearAdvance();
                PlayAdvanced(popped);
                return;
            case JamAdvanceStep.GiveUp:
                var stop = advanceFromTrackEnd;
                ClearAdvance();
                if (stop)
                {
                    StopCleanly();
                    return;
                }

                publishRequested = true;
                return;
        }
    }

    private void TickQueueWait(long now)
    {
        switch (JamHostRecovery.QueueWait(queue.Length, AddInFlight(now), now - queueWaitSinceTicks))
        {
            case JamQueueWaitStep.Advance:
                RequestAdvance(0, true);
                return;
            case JamQueueWaitStep.Stop:
                StopCleanly();
                return;
        }
    }

    private void StopCleanly()
    {
        queueWaitSinceTicks = 0;
        StopWithoutAuthority();
        publishRequested = true;
    }

    private void OnControlRequest(CallControl message)
    {
        if (Mode != JamMode.Hosting)
        {
            return;
        }

        switch (message.Action)
        {
            case JamControlAction.Play:
                if (hub.SongActive && hub.IsPaused)
                {
                    hub.ApplyTogglePlayPause();
                }

                break;
            case JamControlAction.Pause:
                if (hub.SongActive && !hub.IsPaused)
                {
                    hub.ApplyTogglePlayPause();
                }

                break;
            case JamControlAction.Seek:
                if (hub.CanSeek && message.PositionSeconds is { } seconds && double.IsFinite(seconds))
                {
                    hub.Songs.Seek((float)Math.Clamp(seconds, 0d, hub.Duration));
                }

                break;
            case JamControlAction.Next:
                hub.Next();
                break;
            case JamControlAction.Previous:
                hub.Previous();
                break;
            default:
                return;
        }

        publishRequested = true;
    }

    private void TickHost(float deltaSeconds, long now)
    {
        if (advanceEntryId != 0)
        {
            TickAdvance(now);
            if (advanceEntryId != 0)
            {
                return;
            }
        }
        else if (queueWaitSinceTicks != 0)
        {
            TickQueueWait(now);
        }

        heartbeatTimer += deltaSeconds;
        if (!signals.Connected || disconnectedSinceTicks != 0 || awaitingSinceTicks != 0)
        {
            return;
        }

        var active = hub.SongActive;
        var song = active ? hub.CurrentSong : default;
        var videoId = song.VideoId ?? string.Empty;
        var paused = !active || hub.IsPaused || hub.IsBuffering;
        var position = active ? (double)hub.Position : 0d;
        var publish = publishRequested || !hasPublished
            || !string.Equals(videoId, lastPublishedVideoId, StringComparison.Ordinal)
            || paused != lastPublishedPaused;
        if (!publish && !paused)
        {
            var expected = lastPublishedPosition + (now - lastPublishedAtTicks) / 1000d;
            publish = Math.Abs(position - expected) > PositionJumpSeconds || heartbeatTimer >= HeartbeatSeconds;
        }

        if (!publish)
        {
            return;
        }

        signals.State(JamWire.ToTrack(song), position, paused);
        hasPublished = true;
        publishRequested = false;
        heartbeatTimer = 0f;
        lastPublishedVideoId = videoId;
        lastPublishedPosition = position;
        lastPublishedAtTicks = now;
        lastPublishedPaused = paused;
    }
}
