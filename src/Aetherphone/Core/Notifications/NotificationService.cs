using System.Collections.Concurrent;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class NotificationService : IDisposable
{
    private const int MaxRetained = 50;
    private const double SoundRepeatSeconds = 3.0;
    private const int SoundHistoryPruneSize = 64;
    private readonly SoundService sound;
    private readonly Configuration configuration;
    private readonly AppInstaller installer;
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
    public event Action<PhoneNotification>? Vibration;
    public event Action<PhoneNotification>? Added;

    public NotificationService(SoundService sound, Configuration configuration, AppInstaller installer,
        IFramework framework)
    {
        this.sound = sound;
        this.configuration = configuration;
        this.installer = installer;
        this.framework = framework;
        installer.Changed += OnInstalledChanged;
        framework.Update += OnFrameworkUpdate;
    }

    private void OnInstalledChanged(string appId)
    {
        if (installer.IsInstalled(appId))
        {
            return;
        }

        RemoveApp(appId);
    }

    public void RemoveSocial(string appId)
    {
        var removed = false;
        for (var index = recent.Count - 1; index >= 0; index--)
        {
            if (recent[index].SocialType < 0 || !string.Equals(recent[index].AppId, appId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!recent[index].Read)
            {
                UnreadCount--;
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

    public void RemoveApp(string appId)
    {
        var removed = false;
        for (var index = recent.Count - 1; index >= 0; index--)
        {
            if (!string.Equals(recent[index].AppId, appId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!recent[index].Read)
            {
                UnreadCount--;
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
        if (!installer.IsInstalled(notification.AppId))
        {
            AepLog.Warning($"[Notifications] dropped {notification.AppId}/{notification.StackKey}: app not installed");
            return;
        }

        if (!configuration.IsAppNotificationEnabled(notification.SettingsKey))
        {
            return;
        }

        if (AppAvailability is { } available && !available(notification.AppId))
        {
            AepLog.Warning($"[Notifications] dropped {notification.AppId}/{notification.StackKey}: app unavailable");
            return;
        }

        var stamped = notification with { Id = ++sequence };
        stamped.Read = false;
        recent.Add(stamped);
        if (recent.Count > MaxRetained)
        {
            if (!recent[0].Read)
            {
                UnreadCount--;
            }

            recent.RemoveAt(0);
        }

        UnreadCount++;
        Added?.Invoke(stamped);
        if (Plugin.ClientState.IsLoggedIn && !configuration.DoNotDisturb &&
            !(configuration.QuietWhileBusy && PlayerBusy.Now))
        {
            if (configuration.ShowNotificationBanner &&
                configuration.ShouldShowNotificationBanner(notification.SettingsKey))
            {
                Presented?.Invoke(stamped);
            }

            if (configuration.Vibration)
            {
                Vibration?.Invoke(stamped);
            }

            if (ShouldPlaySound(stamped.StackKey))
            {
                sound.PlayNotification(notification.SettingsKey);
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

        for (var index = 0; index < recent.Count; index++)
        {
            recent[index].Read = true;
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

            if (!recent[index].Read)
            {
                UnreadCount--;
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

            if (!recent[index].Read)
            {
                UnreadCount--;
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
        installer.Changed -= OnInstalledChanged;
        framework.Update -= OnFrameworkUpdate;
    }
}
