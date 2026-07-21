using System.Collections.Concurrent;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class NotificationService : IDisposable
{
    private const int MaxRetained = 50;
    private const double SoundRepeatSeconds = 3.0;
    private const int SoundHistoryPruneSize = 64;
    private readonly SoundService sound;
    private readonly Configuration configuration;
    private readonly IFramework framework;
    private readonly ConcurrentQueue<PhoneNotification> pending = new();
    private readonly List<PhoneNotification> recent = new();
    private readonly Dictionary<string, DateTime> lastSoundAt = new();
    private long sequence;
    public int UnreadCount { get; private set; }
    public IReadOnlyList<PhoneNotification> Recent => recent;
    public Func<string, bool>? AppAvailability { get; set; }
    public event Action? Changed;
    public event Action<PhoneNotification>? Presented;

    public NotificationService(SoundService sound, Configuration configuration, IFramework framework)
    {
        this.sound = sound;
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
        if (!configuration.IsAppNotificationEnabled(notification.AppId))
        {
            return;
        }

        if (AppAvailability is { } available && !available(notification.AppId))
        {
            return;
        }

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
            if (ShouldPlaySound(stamped.StackKey))
            {
                sound.PlayNotification(notification.AppId);
            }
        }

        Changed?.Invoke();
    }

    private bool ShouldPlaySound(string stackKey)
    {
        var now = DateTime.UtcNow;
        if (lastSoundAt.TryGetValue(stackKey, out var previous) && (now - previous).TotalSeconds < SoundRepeatSeconds)
        {
            return false;
        }

        if (lastSoundAt.Count >= SoundHistoryPruneSize)
        {
            PruneSoundHistory(now);
        }

        lastSoundAt[stackKey] = now;
        return true;
    }

    private void PruneSoundHistory(DateTime now)
    {
        var expired = new List<string>(lastSoundAt.Count);
        foreach (var entry in lastSoundAt)
        {
            if ((now - entry.Value).TotalSeconds >= SoundRepeatSeconds)
            {
                expired.Add(entry.Key);
            }
        }

        for (var index = 0; index < expired.Count; index++)
        {
            lastSoundAt.Remove(expired[index]);
        }
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
