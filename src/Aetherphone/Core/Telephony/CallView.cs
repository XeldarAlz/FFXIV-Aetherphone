using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

internal readonly struct CallView
{
    public readonly CallState State;
    public readonly bool Muted;
    public readonly float Volume;
    public readonly float MicLevel;
    public readonly int Seconds;
    public readonly ParticipantInfo[] Participants;
    public readonly ParticipantInfo? IncomingFrom;
    public readonly bool Connected;
    public readonly string LocalUserId;
    public readonly string PeerLabel;
    public readonly int OthersCount;

    public CallView(
        CallState state,
        bool muted,
        float volume,
        float micLevel,
        int seconds,
        ParticipantInfo[] participants,
        ParticipantInfo? incomingFrom,
        bool connected,
        string localUserId,
        string peerLabel,
        int othersCount)
    {
        State = state;
        Muted = muted;
        Volume = volume;
        MicLevel = micLevel;
        Seconds = seconds;
        Participants = participants;
        IncomingFrom = incomingFrom;
        Connected = connected;
        LocalUserId = localUserId;
        PeerLabel = peerLabel;
        OthersCount = othersCount;
    }

    public bool IsActive => State is not CallState.Idle and not CallState.Ended;

    public bool InCall => State == CallState.Active;
}
