namespace Aetherphone.Core.Notifications;

internal sealed class AppNotificationSetting
{
    public bool Enabled { get; set; } = true;
    public uint? SoundId { get; set; }
    public string? Sound { get; set; }
}
