namespace Aetherphone.Core.Timers;

[Serializable]
internal sealed class TimerCharacterRecord
{
    public ulong ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public long RetainersSeenUnix { get; set; }
    public List<TimerRetainerRecord> Retainers { get; set; } = new();
    public long MapAllowanceUnix { get; set; }
    public long MapSeenUnix { get; set; }
}

[Serializable]
internal sealed class TimerRetainerRecord
{
    public ulong RetainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long CompleteUnix { get; set; }
    public int DurationSeconds { get; set; }
}

[Serializable]
internal sealed class TimerWorkshopRecord
{
    public string Key { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public long SeenUnix { get; set; }
    public List<TimerVesselRecord> Vessels { get; set; } = new();
}

[Serializable]
internal sealed class TimerVesselRecord
{
    public string Name { get; set; } = string.Empty;
    public bool Airship { get; set; }
    public long RegisterUnix { get; set; }
    public long ReturnUnix { get; set; }
}

internal readonly record struct CapturedRetainer(ulong RetainerId, string Name, long CompleteUnix, int DurationSeconds);

internal readonly record struct CapturedVessel(string Name, bool Airship, long RegisterUnix, long ReturnUnix);
