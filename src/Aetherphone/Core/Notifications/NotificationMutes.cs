namespace Aetherphone.Core.Notifications;

internal static class NotificationMutes
{
    public const long HourSeconds = 3600;

    public static bool IsMuted(IReadOnlyDictionary<string, AppNotificationSetting> settings, string appId,
        long nowUnix) =>
        settings.TryGetValue(appId, out var setting) && IsMuted(setting, nowUnix);

    public static bool IsMuted(AppNotificationSetting setting, long nowUnix) => setting.MutedUntilUnix > nowUnix;

    public static long ForAnHour(long nowUnix) => nowUnix + HourSeconds;

    public static long UntilTomorrow(DateTimeOffset localNow) =>
        new DateTimeOffset(localNow.Date.AddDays(1), localNow.Offset).ToUnixTimeSeconds();
}
