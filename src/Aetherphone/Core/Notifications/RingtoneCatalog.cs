namespace Aetherphone.Core.Notifications;

internal sealed record RingtoneOption(string Name, uint SoundId);

internal static class RingtoneCatalog
{
    public static readonly IReadOnlyList<RingtoneOption> Options = new RingtoneOption[]
    {
        new("Ping", 7), new("Chime", 1), new("Bell", 3), new("Alert", 10), new("Knock", 16), new("Silent", 0),
    };
}
