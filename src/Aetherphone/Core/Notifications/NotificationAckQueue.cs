namespace Aetherphone.Core.Notifications;

internal sealed class NotificationAckQueue
{
    public const string GlobalScope = "*";
    private readonly Dictionary<string, long> pending;
    private readonly Action save;

    public NotificationAckQueue(Dictionary<string, long> pending, Action save)
    {
        this.pending = pending;
        this.save = save;
    }

    public static string KeyFor(string accountId, string? app) =>
        accountId + ":" + (string.IsNullOrEmpty(app) ? GlobalScope : app);

    public bool Enqueue(string accountId, string? app, long upToUnix)
    {
        if (upToUnix <= 0 || string.IsNullOrEmpty(accountId))
        {
            return false;
        }

        var key = KeyFor(accountId, app);
        if (pending.TryGetValue(key, out var existing) && existing >= upToUnix)
        {
            return false;
        }

        pending[key] = upToUnix;
        if (string.IsNullOrEmpty(app))
        {
            DropScopesCoveredBy(accountId, upToUnix);
        }

        save();
        return true;
    }

    private void DropScopesCoveredBy(string accountId, long upToUnix)
    {
        var globalKey = KeyFor(accountId, null);
        var prefix = accountId + ":";
        List<string>? covered = null;
        foreach (var entry in pending)
        {
            if (entry.Value > upToUnix || string.Equals(entry.Key, globalKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (!entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            covered ??= new List<string>();
            covered.Add(entry.Key);
        }

        if (covered is null)
        {
            return;
        }

        for (var index = 0; index < covered.Count; index++)
        {
            pending.Remove(covered[index]);
        }
    }

    public long WatermarkFor(string accountId, string app)
    {
        if (pending.Count == 0 || string.IsNullOrEmpty(accountId))
        {
            return 0;
        }

        var scoped = pending.GetValueOrDefault(KeyFor(accountId, app), 0L);
        var global = pending.GetValueOrDefault(KeyFor(accountId, null), 0L);
        return scoped > global ? scoped : global;
    }

    public bool TryPeek(string accountId, out string key, out string? app, out long watermark)
    {
        key = string.Empty;
        app = null;
        watermark = 0;
        if (pending.Count == 0 || string.IsNullOrEmpty(accountId))
        {
            return false;
        }

        var prefix = accountId + ":";
        foreach (var entry in pending)
        {
            if (!entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            key = entry.Key;
            watermark = entry.Value;
            var scope = entry.Key.Substring(prefix.Length);
            app = string.Equals(scope, GlobalScope, StringComparison.Ordinal) ? null : scope;
            return true;
        }

        return false;
    }

    public bool ConfirmSent(string key, long sentWatermark)
    {
        if (!pending.TryGetValue(key, out var queued) || queued > sentWatermark)
        {
            return false;
        }

        pending.Remove(key);
        save();
        return true;
    }
}
