namespace Aetherphone.Apps.Calendar;

[Serializable]
internal sealed class CalendarCustomEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public DateTime When { get; set; }
    public bool Notified { get; set; }
}
