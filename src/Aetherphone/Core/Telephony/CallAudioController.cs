using Aetherphone.Core.Playback;
using Aetherphone.Core.Telephony.Audio;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

internal sealed class CallAudioController
{
    private readonly Configuration configuration;
    private readonly PlaybackHub playback;
    private readonly RealtimeConnection connection;
    private readonly HashSet<int> remoteSlots = new();
    private CallSession? session;
    private bool muted;
    private float volume = 0.85f;
    private long startTicks;

    public CallAudioController(Configuration configuration, PlaybackHub playback, RealtimeConnection connection)
    {
        this.configuration = configuration;
        this.playback = playback;
        this.connection = connection;
    }

    public bool MutedLocked => muted;
    public float VolumeLocked => volume;
    public float MicLevelLocked => session?.MicLevel ?? 0f;
    public bool HasSessionLocked => session is not null;
    public long StartTicksLocked => startTicks;
    public CallSession? SessionLocked => session;

    public int ElapsedSecondsLocked =>
        startTicks == 0 ? 0 : (int)((Environment.TickCount64 - startTicks) / 1000);

    public bool EnsureStartedLocked(Guid callId, int localSlot)
    {
        if (session is not null)
        {
            if (localSlot >= 0)
            {
                session.SetLocalSlot(localSlot);
            }

            return false;
        }

        var input = AudioDevices.ResolveInput(configuration.CallInputDevice);
        var output = AudioDevices.ResolveOutput(configuration.CallOutputDevice);
        var created = new CallSession(callId, connection, input, output, volume) { Muted = muted, };
        if (localSlot >= 0)
        {
            created.SetLocalSlot(localSlot);
        }

        session = created;
        remoteSlots.Clear();
        startTicks = Environment.TickCount64;
        playback.Stop();
        return true;
    }

    public void SyncRemotesLocked(ParticipantInfo[] participants, string localId)
    {
        if (session is null)
        {
            return;
        }

        Span<int> present = participants.Length <= 16
            ? stackalloc int[participants.Length]
            : new int[participants.Length];
        var presentCount = 0;
        for (var index = 0; index < participants.Length; index++)
        {
            var participant = participants[index];
            if (participant.UserId == localId || participant.State != ParticipantState.Active)
            {
                continue;
            }

            present[presentCount++] = participant.Slot;
            if (remoteSlots.Add(participant.Slot))
            {
                session.AddRemote(participant.Slot);
            }
        }

        if (remoteSlots.Count == presentCount)
        {
            return;
        }

        var stale = new List<int>();
        foreach (var slot in remoteSlots)
        {
            var found = false;
            for (var index = 0; index < presentCount; index++)
            {
                if (present[index] == slot)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                stale.Add(slot);
            }
        }

        for (var index = 0; index < stale.Count; index++)
        {
            session.RemoveRemote(stale[index]);
            remoteSlots.Remove(stale[index]);
        }
    }

    public void ToggleMuteLocked()
    {
        muted = !muted;
        if (session is not null)
        {
            session.Muted = muted;
        }
    }

    public void SetVolumeLocked(float value)
    {
        volume = Math.Clamp(value, 0f, 1f);
        if (session is not null)
        {
            session.Volume = volume;
        }
    }

    public CallSession? TakeLocked()
    {
        var taken = session;
        session = null;
        remoteSlots.Clear();
        muted = false;
        startTicks = 0;
        return taken;
    }
}
