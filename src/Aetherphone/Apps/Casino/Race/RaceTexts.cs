using System.Text;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Race;

internal sealed class RaceTexts
{
    private const int OddsCacheLimit = 512;
    private const char NoRun = '-';

    private static readonly Dictionary<int, string> OddsCache = new();
    private static LanguageInfo? oddsLanguage;

    private static readonly LocString[] ShortKinds =
    {
        L.Race.KindWin, L.Race.KindPlace, L.Race.KindForecast, L.Race.KindReverse,
    };

    private static readonly LocString[] LongKinds =
    {
        L.Race.KindWin, L.Race.KindPlace, L.Race.KindForecast, L.Race.KindReverseLong,
    };

    private static readonly LocString[] Places = { L.Race.PlaceFirst, L.Race.PlaceSecond, L.Race.PlaceThird };

    private readonly string[] kindLabels = new string[RaceRules.KindCount];
    private readonly string[] form = new string[RaceRules.FieldSize];
    private readonly LabelSlot[] backers = new LabelSlot[RaceRules.FieldSize];
    private readonly string[] tickets = new string[RaceRules.MaxTickets];
    private readonly string[] results = new string[RaceRules.ResultRows];
    private readonly string[] places = new string[Places.Length];
    private readonly StringBuilder builder = new();

    private LanguageInfo? language;
    private CasinoRaceRunnerDto[]? formSource;
    private CasinoRaceTicketDto[]? ticketSource;
    private CasinoRaceResultDto[]? resultSource;
    private string payLine = string.Empty;
    private int payKind = -1;
    private long payValue = -1;
    private string rideLine = string.Empty;
    private long rideValue = -1;
    private string wonLine = string.Empty;
    private long wonValue = -1;
    private string countLine = string.Empty;
    private int countValue = -1;
    private string raceLine = string.Empty;
    private long raceValue = -1;

    public RaceTexts()
    {
        Sync();
    }

    public string[] KindLabels => kindLabels;

    public void Sync()
    {
        if (ReferenceEquals(language, Loc.Current) && kindLabels[0] is not null)
        {
            return;
        }

        language = Loc.Current;
        for (var kind = 0; kind < RaceRules.KindCount; kind++)
        {
            kindLabels[kind] = Loc.T(ShortKinds[kind]);
        }

        for (var place = 0; place < Places.Length; place++)
        {
            places[place] = Loc.T(Places[place]);
        }

        ticketSource = null;
        resultSource = null;
        payKind = -1;
        rideValue = -1;
        wonValue = -1;
        countValue = -1;
        raceValue = -1;
    }

    public static string Odds(int hundredths)
    {
        if (!ReferenceEquals(oddsLanguage, Loc.Current))
        {
            oddsLanguage = Loc.Current;
            OddsCache.Clear();
        }

        if (OddsCache.TryGetValue(hundredths, out var cached))
        {
            return cached;
        }

        if (OddsCache.Count >= OddsCacheLimit)
        {
            OddsCache.Clear();
        }

        var text = (hundredths / 100m).ToString("0.00", Loc.Culture);
        OddsCache[hundredths] = text;
        return text;
    }

    public string Place(int place) => place >= 0 && place < places.Length ? places[place] : string.Empty;

    public string KindLong(int kind) => RaceRules.IsKind(kind) ? Loc.T(LongKinds[kind]) : string.Empty;

    public string Form(CasinoRaceRunnerDto[] runners, int slot)
    {
        if (!ReferenceEquals(formSource, runners))
        {
            formSource = runners;
            for (var index = 0; index < form.Length; index++)
            {
                form[index] = index < runners.Length ? FormLine(runners[index].Form) : string.Empty;
            }
        }

        return slot >= 0 && slot < form.Length ? form[slot] : string.Empty;
    }

    public string Backers(int slot, int count) => backers[slot].Get(L.Race.Backers, count);

    public string Ticket(CasinoRaceTicketDto[] list, int index)
    {
        if (!ReferenceEquals(ticketSource, list))
        {
            ticketSource = list;
            for (var row = 0; row < tickets.Length; row++)
            {
                tickets[row] = row < list.Length
                    ? TicketLabel(list[row].Kind, list[row].Runner, list[row].RunnerB)
                    : string.Empty;
            }
        }

        return index >= 0 && index < tickets.Length ? tickets[index] : string.Empty;
    }

    public string Result(CasinoRaceResultDto[] list, int index)
    {
        if (!ReferenceEquals(resultSource, list))
        {
            resultSource = list;
            for (var row = 0; row < results.Length; row++)
            {
                results[row] = row < list.Length
                    ? TicketLabel(list[row].Kind, list[row].Runner, list[row].RunnerB)
                    : string.Empty;
            }
        }

        return index >= 0 && index < results.Length ? results[index] : string.Empty;
    }

    public string PayLine(int kind, long payHundredths)
    {
        if (kind == payKind && payHundredths == payValue)
        {
            return payLine;
        }

        payKind = kind;
        payValue = payHundredths;
        var multiple = CasinoMultiples.Label((int)Math.Min(int.MaxValue, payHundredths));
        payLine = Loc.T(kind == RaceRules.KindReverse ? L.Race.PaysUpTo : L.Race.Pays, multiple);
        return payLine;
    }

    public string Ride(long amount)
    {
        if (amount == rideValue)
        {
            return rideLine;
        }

        rideValue = amount;
        rideLine = Loc.T(L.Race.LetItRide, NumberText.Compact(amount));
        return rideLine;
    }

    public string Won(long amount)
    {
        if (amount == wonValue)
        {
            return wonLine;
        }

        wonValue = amount;
        wonLine = Loc.T(L.Race.YouWon, NumberText.Compact(amount));
        return wonLine;
    }

    public string Count(int count)
    {
        if (count == countValue)
        {
            return countLine;
        }

        countValue = count;
        countLine = Loc.T(L.Race.TicketsCount, GameNumber.Label(count), GameNumber.Label(RaceRules.MaxTickets));
        return countLine;
    }

    public string RaceNumber(long roundIndex)
    {
        if (roundIndex == raceValue)
        {
            return raceLine;
        }

        raceValue = roundIndex;
        raceLine = Loc.T(L.Race.RaceNumber, NumberText.Group(roundIndex));
        return raceLine;
    }

    private string TicketLabel(int kind, int runner, int runnerB)
    {
        var name = KindLong(kind);
        var first = GameNumber.Label(runner + 1);
        return RaceRules.IsPair(kind) && RaceRules.IsRunner(runnerB)
            ? Loc.T(L.Race.TicketPair, name, first, GameNumber.Label(runnerB + 1))
            : Loc.T(L.Race.TicketSingle, name, first);
    }

    private string FormLine(int[]? finishes)
    {
        builder.Clear();
        for (var index = 0; index < RaceRules.FormLength; index++)
        {
            var finish = finishes is not null && index < finishes.Length ? finishes[index] : 0;
            builder.Append(finish > 0 && finish <= RaceRules.FieldSize ? (char)('0' + finish) : NoRun);
        }

        return builder.ToString();
    }
}
