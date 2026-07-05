namespace Aetherphone.Apps.Notes;

[Serializable]
internal sealed class ReminderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public bool Done { get; set; }
    public DateTime? DueAt { get; set; }
    public bool Notified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
