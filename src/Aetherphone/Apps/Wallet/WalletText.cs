using System.Globalization;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Wallet;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Wallet;

internal sealed class WalletLine
{
    public uint ItemId;
    public long Delta;
    public int Day;
    public string DeltaText = string.Empty;
    public string ClockSubtitle = string.Empty;
    public string StampSubtitle = string.Empty;
    public string DayLabel = string.Empty;
}

internal sealed class WalletText
{
    private const string Separator = " · ";
    private const string Plus = "+";
    private const string Minus = "-";

    private sealed class EntryCache
    {
        public CachedText Subtitle;
        public CachedText Summary;
        public CachedText Remaining;
        public int NetRevision = -1;
        public int NetDay;
        public long NetWeekStart;
        public long Today;
        public long Week;
    }

    private readonly GameData gameData;
    private readonly Dictionary<uint, string> places = new();
    private readonly Dictionary<uint, EntryCache> caches = new();
    private readonly List<WalletLine> pool = new();
    private int lineCount;
    private int builtRevision = -1;
    private CultureInfo? builtCulture;
    private int builtFormat = -1;
    private int builtDay;

    public WalletText(GameData gameData)
    {
        this.gameData = gameData;
    }

    public int LineCount => lineCount;

    public WalletLine Line(int index) => pool[index];

    public void Sync(WalletService wallet)
    {
        var today = Today();
        if (builtRevision == wallet.HistoryRevision && ReferenceEquals(builtCulture, Loc.Culture) &&
            builtFormat == TimeText.FormatVersion && builtDay == today)
        {
            return;
        }

        builtRevision = wallet.HistoryRevision;
        builtCulture = Loc.Culture;
        builtFormat = TimeText.FormatVersion;
        builtDay = today;
        var changes = wallet.History.Changes;
        lineCount = 0;
        for (var index = changes.Count - 1; index >= 0; index--)
        {
            var change = changes[index];
            if (!wallet.TryGetEntry(change.ItemId, out _))
            {
                continue;
            }

            var line = NextLine();
            var place = Place(change.Territory);
            var clock = TimeText.Clock(change.Unix);
            var stamp = TimeText.Stamp(change.Unix);
            line.ItemId = change.ItemId;
            line.Delta = change.Delta;
            line.Day = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(change.Unix).LocalDateTime).DayNumber;
            line.DeltaText = Signed(change.Delta);
            line.ClockSubtitle = place.Length > 0 ? string.Concat(place, Separator, clock) : clock;
            line.StampSubtitle = place.Length > 0 ? string.Concat(place, Separator, stamp) : stamp;
            line.DayLabel = TimeText.DayLabel(change.Unix);
        }
    }

    public string Subtitle(WalletEntry entry)
    {
        if (entry.HasWeeklyCap)
        {
            return entry.WeeklyCapText;
        }

        var cache = Cache(entry.ItemId);
        switch (entry.Level)
        {
            case CapLevel.Full:
                return Loc.T(L.Wallet.Full);
            case CapLevel.Near:
            {
                var remaining = WalletMath.Remaining(entry.Amount, entry.Cap);
                var key = remaining * 2 + 1;
                return cache.Subtitle.IsCurrent(key)
                    ? cache.Subtitle.Value
                    : cache.Subtitle.Store(key, Loc.T(L.Wallet.LeftToCap, NumberText.Group(remaining)));
            }
            case CapLevel.Room:
            {
                var key = entry.Cap * 2;
                return cache.Subtitle.IsCurrent(key)
                    ? cache.Subtitle.Value
                    : cache.Subtitle.Store(key, Loc.T(L.Wallet.OfCap, NumberText.Group(entry.Cap)));
            }
            default:
                return string.Empty;
        }
    }

    public string CapDetail(WalletEntry entry)
    {
        if (entry.Cap <= 0)
        {
            return string.Empty;
        }

        if (entry.Level == CapLevel.Full)
        {
            return Loc.T(L.Wallet.Full);
        }

        var cache = Cache(entry.ItemId);
        var remaining = WalletMath.Remaining(entry.Amount, entry.Cap);
        var key = remaining ^ (entry.Cap << 32);
        return cache.Remaining.IsCurrent(key)
            ? cache.Remaining.Value
            : cache.Remaining.Store(key, string.Concat(Loc.T(L.Wallet.OfCap, NumberText.Group(entry.Cap)), Separator,
                Loc.T(L.Wallet.UntilFull, NumberText.Group(remaining))));
    }

    public string Summary(WalletService wallet, uint itemId)
    {
        var cache = Cache(itemId);
        Net(wallet, itemId, cache);
        var key = HashCode.Combine(cache.Today, cache.Week);
        if (cache.Summary.IsCurrent(key))
        {
            return cache.Summary.Value;
        }

        if (cache.Today == 0 && cache.Week == 0)
        {
            return cache.Summary.Store(key, Loc.T(L.Wallet.NoChangeThisWeek));
        }

        var week = Loc.T(L.Wallet.ThisWeek, Signed(cache.Week));
        return cache.Summary.Store(key, cache.Today == 0
            ? week
            : string.Concat(Loc.T(L.Wallet.Today, Signed(cache.Today)), Separator, week));
    }

    public static string Signed(long value)
    {
        if (value == 0)
        {
            return NumberText.Group(0);
        }

        return value > 0
            ? string.Concat(Plus, NumberText.Group(value))
            : string.Concat(Minus, NumberText.Group(-value));
    }

    public static int Today() => DateOnly.FromDateTime(DateTime.Now).DayNumber;

    private void Net(WalletService wallet, uint itemId, EntryCache cache)
    {
        var today = Today();
        var weekStart = new DateTimeOffset(GameSchedule.NextWeeklyReset(DateTime.UtcNow).AddDays(-7), TimeSpan.Zero)
            .ToUnixTimeSeconds();
        if (cache.NetRevision == wallet.HistoryRevision && cache.NetDay == today && cache.NetWeekStart == weekStart)
        {
            return;
        }

        cache.NetRevision = wallet.HistoryRevision;
        cache.NetDay = today;
        cache.NetWeekStart = weekStart;
        var midnight = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
        cache.Today = WalletJournal.NetSince(wallet.History, itemId, midnight);
        cache.Week = WalletJournal.NetSince(wallet.History, itemId, weekStart);
    }

    private EntryCache Cache(uint itemId)
    {
        if (!caches.TryGetValue(itemId, out var cache))
        {
            cache = new EntryCache();
            caches[itemId] = cache;
        }

        return cache;
    }

    private WalletLine NextLine()
    {
        if (lineCount == pool.Count)
        {
            pool.Add(new WalletLine());
        }

        return pool[lineCount++];
    }

    private string Place(uint territory)
    {
        if (territory == 0)
        {
            return string.Empty;
        }

        if (!places.TryGetValue(territory, out var name))
        {
            name = gameData.TerritoryName(territory);
            places[territory] = name;
        }

        return name;
    }
}
