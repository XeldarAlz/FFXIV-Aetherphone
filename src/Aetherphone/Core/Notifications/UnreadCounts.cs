using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Notifications;

internal sealed class UnreadCounts
{
    public static readonly UnreadCounts Empty =
        new(new Dictionary<string, int>(StringComparer.Ordinal), Array.Empty<NotificationUnreadCountDto>());

    private readonly Dictionary<string, int> byApp;
    private readonly NotificationUnreadCountDto[]? byType;

    public UnreadCounts(Dictionary<string, int> byApp, NotificationUnreadCountDto[]? byType)
    {
        this.byApp = byApp;
        this.byType = byType;
    }

    public bool HasBreakdown => byType is not null;

    public bool AnyOutstanding
    {
        get
        {
            foreach (var count in byApp.Values)
            {
                if (count > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public int Of(string app) => byApp.GetValueOrDefault(app, 0);

    public int Of(string app, int type)
    {
        var entries = byType;
        if (entries is null)
        {
            return 0;
        }

        for (var index = 0; index < entries.Length; index++)
        {
            if (entries[index].Type == type && string.Equals(entries[index].App, app, StringComparison.Ordinal))
            {
                return entries[index].Count;
            }
        }

        return 0;
    }

    public int Excluding(string app, int excludedType)
    {
        var remaining = Of(app) - Of(app, excludedType);
        return remaining > 0 ? remaining : 0;
    }

    public UnreadCounts WithoutApp(string app)
    {
        var apps = new Dictionary<string, int>(byApp, StringComparer.Ordinal) { [app] = 0 };
        return new UnreadCounts(apps, byType is null ? null : EntriesOutside(app, 0).ToArray());
    }

    public UnreadCounts WithAppRecounted(string app, NotificationDto[] latest, long watermark)
    {
        var entries = byType is null ? null : EntriesOutside(app, latest.Length);
        var total = 0;
        for (var index = 0; index < latest.Length; index++)
        {
            var item = latest[index];
            if (item.Read || item.CreatedAtUnix <= watermark
                || !string.Equals(item.App, app, StringComparison.Ordinal))
            {
                continue;
            }

            total++;
            if (entries is not null)
            {
                Increment(entries, app, item.Type);
            }
        }

        var apps = new Dictionary<string, int>(byApp, StringComparer.Ordinal) { [app] = total };
        return new UnreadCounts(apps, entries?.ToArray());
    }

    private List<NotificationUnreadCountDto> EntriesOutside(string app, int extraCapacity)
    {
        var source = byType!;
        var kept = new List<NotificationUnreadCountDto>(source.Length + extraCapacity);
        for (var index = 0; index < source.Length; index++)
        {
            if (!string.Equals(source[index].App, app, StringComparison.Ordinal))
            {
                kept.Add(source[index]);
            }
        }

        return kept;
    }

    private static void Increment(List<NotificationUnreadCountDto> entries, string app, int type)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].Type == type && string.Equals(entries[index].App, app, StringComparison.Ordinal))
            {
                entries[index] = entries[index] with { Count = entries[index].Count + 1 };
                return;
            }
        }

        entries.Add(new NotificationUnreadCountDto(app, type, 1));
    }
}
