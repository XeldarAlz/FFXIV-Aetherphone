namespace Aetherphone.Core.Notifications;

internal sealed class NotificationGroup
{
    public readonly List<PhoneNotification> Items = new();

    public string Key { get; private set; } = string.Empty;

    public int Count => Items.Count;

    public PhoneNotification Newest => Items[0];

    public int HiddenCount => NotificationGroups.HiddenCount(Items.Count);

    public int VisibleLayers => NotificationGroups.VisibleLayers(Items.Count);

    public bool TryFind(long id, out PhoneNotification notification)
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index].Id != id)
            {
                continue;
            }

            notification = Items[index];
            return true;
        }

        notification = null!;
        return false;
    }

    internal void Begin(string key)
    {
        Key = key;
        Items.Clear();
    }
}

internal sealed class NotificationGroups
{
    public const int MaxVisibleLayers = 3;

    private readonly List<NotificationGroup> groups = new();
    private readonly Dictionary<string, NotificationGroup> lookup = new(StringComparer.Ordinal);
    private readonly Stack<NotificationGroup> pool = new();

    public IReadOnlyList<NotificationGroup> Groups => groups;

    public int Count => groups.Count;

    public int TotalCount { get; private set; }

    public DateTime OldestReceivedAt { get; private set; }

    public void Rebuild(IReadOnlyList<PhoneNotification> recent)
    {
        Clear();
        TotalCount = recent.Count;
        for (var index = recent.Count - 1; index >= 0; index--)
        {
            var notification = recent[index];
            if (OldestReceivedAt == default || notification.ReceivedAt < OldestReceivedAt)
            {
                OldestReceivedAt = notification.ReceivedAt;
            }

            if (!lookup.TryGetValue(notification.StackKey, out var group))
            {
                group = pool.Count > 0 ? pool.Pop() : new NotificationGroup();
                group.Begin(notification.StackKey);
                lookup[notification.StackKey] = group;
                groups.Add(group);
            }

            group.Items.Add(notification);
        }
    }

    public void Clear()
    {
        for (var index = 0; index < groups.Count; index++)
        {
            pool.Push(groups[index]);
        }

        groups.Clear();
        lookup.Clear();
        TotalCount = 0;
        OldestReceivedAt = default;
    }

    public bool TryGet(string key, out NotificationGroup group) => lookup.TryGetValue(key, out group!);

    public bool Contains(long id)
    {
        for (var index = 0; index < groups.Count; index++)
        {
            if (groups[index].TryFind(id, out _))
            {
                return true;
            }
        }

        return false;
    }

    public static int VisibleLayers(int itemCount) => Math.Clamp(itemCount, 0, MaxVisibleLayers);

    public static int HiddenCount(int itemCount) => Math.Max(0, itemCount - 1);
}
