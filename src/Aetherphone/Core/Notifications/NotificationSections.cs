namespace Aetherphone.Core.Notifications;

internal enum NotificationSection : byte
{
    Today,
    Yesterday,
    Earlier,
}

internal readonly record struct NotificationAppCount(string AppId, int Count);

internal static class NotificationSections
{
    public const int Count = 3;

    public static NotificationSection Of(DateTime receivedLocal, DateTime todayLocal)
    {
        var day = receivedLocal.Date;
        var today = todayLocal.Date;
        if (day >= today)
        {
            return NotificationSection.Today;
        }

        return day == today.AddDays(-1) ? NotificationSection.Yesterday : NotificationSection.Earlier;
    }

    public static bool Matches(NotificationGroup group, string? appFilter)
    {
        if (appFilter is null)
        {
            return true;
        }

        return string.Equals(group.Newest.AppId, appFilter, StringComparison.Ordinal);
    }

    public static void Tally(IReadOnlyList<PhoneNotification> recent, List<NotificationAppCount> output)
    {
        output.Clear();
        for (var index = recent.Count - 1; index >= 0; index--)
        {
            var appId = recent[index].AppId;
            var found = -1;
            for (var slot = 0; slot < output.Count; slot++)
            {
                if (string.Equals(output[slot].AppId, appId, StringComparison.Ordinal))
                {
                    found = slot;
                    break;
                }
            }

            if (found < 0)
            {
                output.Add(new NotificationAppCount(appId, 1));
                continue;
            }

            output[found] = output[found] with { Count = output[found].Count + 1 };
        }
    }
}
