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
    private long startTicks;
    private float peakMicLevel;

    public CallAudioController(Configuration configuration, PlaybackHub playback, RealtimeConnection connection)
    {
        this.configuration = configuration;
        this.playback = playback;
        this.connection = connection;
    }

    public bool MutedLocked => muted;
    public float VolumeLocked => configuration.CallOutputVolume;
    public float InputGainLocked => configuration.CallInputGain;
    public bool AutoGainLocked => configuration.CallAutoGain;
    public float InputThresholdLocked => configuration.CallInputThreshold;
    public float MicLevelLocked => session?.MicLevel ?? 0f;
    public bool MicClippingLocked => session?.MicClipping ?? false;

    public float PeakMicLevelLocked
    {
        get
        {
            var level = session?.MicLevel ?? 0f;
            if (level > peakMicLevel)
            {
                peakMicLevel = level;
            }

            return peakMicLevel;
        }
    }
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
        var created = new CallSession(callId, connection, input, configuration.CallOutputDevice,
            configuration.CallOutputVolume, configuration.CallInputGain, configuration.CallAutoGain,
            configuration.CallInputThreshold) { Muted = muted, };
        if (localSlot >= 0)
        {
            created.SetLocalSlot(localSlot);
        }

        session = created;
        remoteSlots.Clear();
        startTicks = Environment.TickCount64;
        peakMicLevel = 0f;
        playback.Stop();
        return true;
    }

    public void SyncRemotesLocked(ParticipantInfo[] participants, string localId)
    {
        if (session is null)
        {
            return;
        }

        var present = participants.Length <= 16
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

            session.SetRemoteGain(participant.Slot, PeerGainLocked(participant.UserId));
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
        var volume = Math.Clamp(value, 0f, VoiceMixer.MaximumGain);
        configuration.CallOutputVolume = volume;
        if (session is not null)
        {
            session.Volume = volume;
        }
    }

    public void SetInputGainLocked(float value)
    {
        var gain = Math.Clamp(value, 0f, AudioCapture.MaximumGain);
        configuration.CallInputGain = gain;
        if (session is not null)
        {
            session.InputGain = gain;
        }
    }

    public void SetInputThresholdLocked(float value)
    {
        var threshold = Math.Clamp(value, 0f, 1f);
        configuration.CallInputThreshold = threshold;
        if (session is not null)
        {
            session.InputThreshold = threshold;
        }
    }

    public void SetAutoGainLocked(bool value)
    {
        configuration.CallAutoGain = value;
        if (session is not null)
        {
            session.AutoGain = value;
        }
    }

    public void SwitchInputLocked() => session?.SwitchInput(configuration.CallInputDevice);

    public void SwitchOutputLocked() => session?.SwitchOutput(configuration.CallOutputDevice);

    public float PeerVolumeLocked(string userId) =>
        configuration.CallPeerVolumes.TryGetValue(userId, out var stored) ? stored : 1f;

    public bool PeerMutedLocked(string userId) => configuration.CallPeerMuted.Contains(userId);

    public void SetPeerVolumeLocked(string userId, float value, ParticipantInfo[] participants)
    {
        var volume = Math.Clamp(value, 0f, VoiceMixer.MaximumGain);
        if (MathF.Abs(volume - 1f) < 0.005f)
        {
            configuration.CallPeerVolumes.Remove(userId);
        }
        else
        {
            configuration.CallPeerVolumes[userId] = volume;
        }

        ApplyPeerLocked(userId, participants);
    }

    public void SetPeerMutedLocked(string userId, bool value, ParticipantInfo[] participants)
    {
        if (value)
        {
            configuration.CallPeerMuted.Add(userId);
        }
        else
        {
            configuration.CallPeerMuted.Remove(userId);
        }

        ApplyPeerLocked(userId, participants);
    }

    private float PeerGainLocked(string userId) => PeerMutedLocked(userId) ? 0f : PeerVolumeLocked(userId);

    private void ApplyPeerLocked(string userId, ParticipantInfo[] participants)
    {
        if (session is null)
        {
            return;
        }

        for (var index = 0; index < participants.Length; index++)
        {
            if (participants[index].UserId == userId)
            {
                session.SetRemoteGain(participants[index].Slot, PeerGainLocked(userId));
            }
        }
    }

    public CallSession? TakeLocked()
    {
        var taken = session;
        session = null;
        remoteSlots.Clear();
        muted = false;
        startTicks = 0;
        peakMicLevel = 0f;
        return taken;
    }
}
