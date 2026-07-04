using System.Collections.Concurrent;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class NotificationService : IDisposable
{
    private const int MaxRetained = 50;

    private readonly IRingtone ringtone;
    private readonly Configuration configuration;
    private readonly IFramework framework;
    private readonly ConcurrentQueue<PhoneNotification> pending = new();
    private readonly List<PhoneNotification> recent = new();

    private long sequence;

    public int UnreadCount { get; private set; }

    public IReadOnlyList<PhoneNotification> Recent => recent;

    public event Action? Changed;

    public event Action<PhoneNotification>? Presented;

    public NotificationService(IRingtone ringtone, Configuration configuration, IFramework framework)
    {
        this.ringtone = ringtone;
        this.configuration = configuration;
        this.framework = framework;
        framework.Update += OnFrameworkUpdate;
    }

    public void Notify(PhoneNotification notification)
    {
        pending.Enqueue(notification);
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        while (pending.TryDequeue(out var notification))
        {
            Present(notification);
        }
    }

    private void Present(PhoneNotification notification)
    {
        var stamped = notification with { Id = ++sequence };
        recent.Add(stamped);
        if (recent.Count > MaxRetained)
        {
            recent.RemoveAt(0);
        }

        UnreadCount++;

        if (!configuration.DoNotDisturb)
        {
            Presented?.Invoke(stamped);
            ringtone.Play();
        }

        Changed?.Invoke();
    }

    public void MarkAllRead()
    {
        if (UnreadCount == 0)
        {
            return;
        }

        UnreadCount = 0;
        Changed?.Invoke();
    }

    public void Remove(long id)
    {
        for (var index = 0; index < recent.Count; index++)
        {
            if (recent[index].Id != id)
            {
                continue;
            }

            recent.RemoveAt(index);
            ClampUnread();
            Changed?.Invoke();
            return;
        }
    }

    public void RemoveGroup(string stackKey)
    {
        var removed = false;
        for (var index = recent.Count - 1; index >= 0; index--)
        {
            if (recent[index].StackKey != stackKey)
            {
                continue;
            }

            recent.RemoveAt(index);
            removed = true;
        }

        if (!removed)
        {
            return;
        }

        ClampUnread();
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (recent.Count == 0 && UnreadCount == 0)
        {
            return;
        }

        recent.Clear();
        UnreadCount = 0;
        Changed?.Invoke();
    }

    private void ClampUnread()
    {
        if (UnreadCount > recent.Count)
        {
            UnreadCount = recent.Count;
        }
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
    }
}
