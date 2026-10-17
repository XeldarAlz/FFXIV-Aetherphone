using System.Globalization;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Dailies;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Home;

internal readonly struct DailySummaryItem
{
    public readonly string AppId;
    public readonly string Name;
    public readonly string Text;

    public DailySummaryItem(string appId, string name, string text)
    {
        AppId = appId;
        Name = name;
        Text = text;
    }

    public WidgetRoute Route => WidgetRoute.App(AppId);
}

internal sealed class DailySummary
{
    private const long RefreshMilliseconds = 1000;
    private const long FetchMilliseconds = 30_000;
    private const string CoinAppId = "coin";
    private const string GamesAppId = "games";
    private const string CasinoAppId = "casino";
    private const int ToDoCount = 4;

    private static readonly string[] InboxApps =
        { "message", "messages", "chirper", "aethergram", "yellowpages", "announcements" };

    private readonly PhoneServices phone;
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly DailySummaryItem[] items = new DailySummaryItem[InboxApps.Length + ToDoCount];
    private readonly int[] counts = new int[InboxApps.Length + ToDoCount];
    private long refreshAt;
    private long fetchAt;
    private long builtKey = -1;
    private CultureInfo? builtCulture;

    public DailySummary(WidgetServices services)
    {
        phone = services.Phone;
        apps = services.Apps;
    }

    public int Count { get; private set; }

    public ReadOnlySpan<DailySummaryItem> Items => items.AsSpan(0, Count);

    public void Refresh()
    {
        var now = Environment.TickCount64;
        if (now < refreshAt)
        {
            return;
        }

        refreshAt = now + RefreshMilliseconds;
        var signedIn = phone.AethernetSession.IsSignedIn;
        if (signedIn && now >= fetchAt)
        {
            fetchAt = now + FetchMilliseconds;
            Fetch();
        }

        var key = Tally(signedIn);
        if (key == builtKey && ReferenceEquals(builtCulture, Loc.Culture))
        {
            return;
        }

        builtKey = key;
        builtCulture = Loc.Culture;
        Build();
    }

    private void Fetch()
    {
        if (Find(CoinAppId) is { IsAvailable: true })
        {
            phone.Coins.EnsureFresh();
        }

        if (Find(CasinoAppId) is { IsAvailable: true })
        {
            phone.CasinoSpin.EnsureFresh();
        }
    }

    private long Tally(bool signedIn)
    {
        for (var index = 0; index < InboxApps.Length; index++)
        {
            counts[index] = Find(InboxApps[index]) is { IsAvailable: true } app ? app.BadgeCount : 0;
        }

        var dailies = phone.Dailies;
        var toDo = InboxApps.Length;
        counts[toDo] = Find(DailiesTracker.AppId) is { IsAvailable: true } && dailies.HasData
            ? Math.Max(0, dailies.Remaining(DailyCadence.Daily))
            : 0;
        counts[toDo + 1] = signedIn && Find(CoinAppId) is { IsAvailable: true } &&
                           phone.Coins.Wallet is { CheckInAvailable: true, Paused: false, FrozenUntilUnix: null }
            ? 1
            : 0;
        counts[toDo + 2] = Find(GamesAppId) is { IsAvailable: true } && !phone.GameStats.DailyDone ? 1 : 0;
        counts[toDo + 3] = signedIn && Find(CasinoAppId) is { IsAvailable: true } &&
                           DailySpinStatus.Of(phone.CasinoSpin.Answer) == DailySpinClaim.Available
            ? 1
            : 0;
        var key = 17L;
        for (var index = 0; index < counts.Length; index++)
        {
            key = key * 31 + counts[index];
        }

        return key;
    }

    private void Build()
    {
        var count = 0;
        for (var index = 0; index < InboxApps.Length; index++)
        {
            if (counts[index] > 0)
            {
                items[count++] = Item(InboxApps[index], Loc.T(L.Linkpearl.NewCount, counts[index]));
            }
        }

        var toDo = InboxApps.Length;
        if (counts[toDo] > 0)
        {
            items[count++] = Item(DailiesTracker.AppId, Loc.T(L.WidgetsAdventure.ToDo, counts[toDo]));
        }

        if (counts[toDo + 1] > 0)
        {
            items[count++] = Item(CoinAppId, Loc.T(L.WidgetsLife.CheckInReady));
        }

        if (counts[toDo + 2] > 0)
        {
            items[count++] = Item(GamesAppId, Loc.T(L.WidgetsUtility.TodaysGame));
        }

        if (counts[toDo + 3] > 0)
        {
            items[count++] = Item(CasinoAppId, Loc.T(L.WidgetsUtility.SpinReady));
        }

        Count = count;
    }

    public string NameOf(string appId) => Find(appId)?.DisplayName ?? appId;

    private DailySummaryItem Item(string appId, string text) => new(appId, NameOf(appId), text);

    private IPhoneApp? Find(string appId)
    {
        for (var index = 0; index < apps.Count; index++)
        {
            if (string.Equals(apps[index].Id, appId, StringComparison.Ordinal))
            {
                return apps[index];
            }
        }

        return null;
    }
}
