namespace Aetherphone.Core.Clock;

internal enum AlarmRingKind : byte
{
    Alarm,
    Timer,
}

internal sealed class AlarmRinger
{
    public static readonly TimeSpan SnoozeLength = TimeSpan.FromMinutes(9);
    public static readonly TimeSpan RingLimit = TimeSpan.FromMinutes(5);

    private readonly Action<AlarmRingKind> startTone;
    private readonly Action stopTone;
    private DateTime ringStartedUtc;
    private DateTime? snoozeEndsUtc;
    private string snoozedLabel = string.Empty;

    public AlarmRinger(Action<AlarmRingKind> startTone, Action stopTone)
    {
        this.startTone = startTone;
        this.stopTone = stopTone;
    }

    public event Action? Presented;

    public bool IsRinging { get; private set; }

    public AlarmRingKind Kind { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public bool CanSnooze => IsRinging && Kind == AlarmRingKind.Alarm;

    public bool IsSnoozed => snoozeEndsUtc is not null;

    public DateTime? SnoozeEndsUtc => snoozeEndsUtc;

    public string SnoozedLabel => snoozedLabel;

    public void Ring(AlarmRingKind kind, string label, DateTime utcNow)
    {
        if (IsRinging)
        {
            stopTone();
        }

        Kind = kind;
        Label = label;
        ringStartedUtc = utcNow;
        IsRinging = true;
        startTone(kind);
        Presented?.Invoke();
    }

    public void Stop()
    {
        if (!IsRinging)
        {
            return;
        }

        IsRinging = false;
        stopTone();
    }

    public void Snooze(DateTime utcNow)
    {
        if (!CanSnooze)
        {
            return;
        }

        snoozedLabel = Label;
        snoozeEndsUtc = utcNow + SnoozeLength;
        Stop();
    }

    public void Tick(DateTime utcNow)
    {
        if (IsRinging && utcNow - ringStartedUtc >= RingLimit)
        {
            Stop();
        }

        if (snoozeEndsUtc is not { } snoozeEnd || utcNow < snoozeEnd)
        {
            return;
        }

        snoozeEndsUtc = null;
        Ring(AlarmRingKind.Alarm, snoozedLabel, utcNow);
    }
}
