namespace Aetherphone.Core.Calendar;

[Serializable]
internal sealed class CalendarEventGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool ShowInApp { get; set; } = true;
    public bool ShowInWidget { get; set; } = true;

    public bool ShowsOn(CalendarSurface surface) => surface == CalendarSurface.Widget ? ShowInWidget : ShowInApp;
}
