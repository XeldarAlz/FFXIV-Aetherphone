namespace Aetherphone.Core.Notifications;

internal static class RetiredSounds
{
    private static readonly string[] RetiredRingtones =
    {
        "Ringtone_1.mp3", "Ringtone_2.mp3", "Ringtone_3.mp3", "Ringtone_4.mp3", "Ringtone_5.mp3", "Ringtone_6.mp3",
    };

    private static readonly string[] RingtoneReplacements =
    {
        "Signal.mp3", "Cascade.mp3", "Horizon.mp3", "Lumen.mp3", "Orbit.mp3", "Prism.mp3",
    };

    private static readonly string[] RetiredNotifications =
    {
        "Notification_1.mp3", "Notification_2.mp3", "Notification_3.mp3", "Notification_4.mp3",
        "Notification_5.mp3", "Notification_6.mp3", "Notification_7.mp3",
    };

    private static readonly string[] NotificationReplacements =
    {
        "Chime.mp3", "Bloom.mp3", "Note.mp3", "Glint.mp3", "Ping.mp3", "Ripple.mp3", "Spark.mp3",
    };

    public static string? Replace(string? token, SoundKind kind, ref bool changed)
    {
        if (token is null || !SoundTokens.TryFile(token, out var fileName))
        {
            return token;
        }

        var retired = kind == SoundKind.Ringtone ? RetiredRingtones : RetiredNotifications;
        var replacements = kind == SoundKind.Ringtone ? RingtoneReplacements : NotificationReplacements;
        for (var index = 0; index < retired.Length; index++)
        {
            if (string.Equals(retired[index], fileName, StringComparison.OrdinalIgnoreCase))
            {
                changed = true;
                return SoundTokens.File(replacements[index]);
            }
        }

        return token;
    }
}
