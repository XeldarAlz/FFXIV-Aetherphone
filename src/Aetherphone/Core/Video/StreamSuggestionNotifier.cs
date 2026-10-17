using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Core.Video;

internal sealed class StreamSuggestionNotifier : IDisposable
{
    public const string AppId = "aetherstream";

    public const string GroupKey = "aetherstream:suggestions";

    private const long AttentionWindowMilliseconds = 1_200;

    private readonly WatchAlongSession watchAlong;
    private readonly NotificationService notifications;
    private readonly Vector4 accent;

    private long attentionStampedAtTick;

    public StreamSuggestionNotifier(WatchAlongSession watchAlong, NotificationService notifications)
    {
        this.watchAlong = watchAlong;
        this.notifications = notifications;
        accent = AppAccents.For(AppId);
        watchAlong.QueueSuggestionArrived += OnSuggestionArrived;
    }

    public void StampAttention()
    {
        Interlocked.Exchange(ref attentionStampedAtTick, Environment.TickCount64);
    }

    public void ClearShellNotices() => notifications.RemoveGroup(AppId, GroupKey);

    internal static bool Watching(long stampedAtTick, long nowTick)
    {
        return stampedAtTick != 0 && nowTick - stampedAtTick <= AttentionWindowMilliseconds;
    }

    private void OnSuggestionArrived(QueueSuggestion suggestion)
    {
        if (Watching(Interlocked.Read(ref attentionStampedAtTick), Environment.TickCount64))
        {
            return;
        }

        notifications.Notify(new PhoneNotification(AppId, Loc.T(L.AetherStream.SuggestionNotifyTitle),
            string.Format(Loc.T(L.AetherStream.SuggestionNotifyBody), suggestion.DisplayName), DateTime.Now, accent,
            GroupKey)
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
    }

    public void Dispose()
    {
        watchAlong.QueueSuggestionArrived -= OnSuggestionArrived;
    }
}
