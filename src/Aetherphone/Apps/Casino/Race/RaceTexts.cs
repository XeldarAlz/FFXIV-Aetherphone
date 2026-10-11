using System.Text;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Race;

internal sealed class RaceTexts
{
    private const char NoRun = '-';

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
    private readonly string[] tickets = new string[RaceRules.MaxTickets];
    private readonly string[] results = new string[RaceRules.ResultRows];
    private readonly string[] places = new string[Places.Length];
    private readonly string[] ticketPays = new string[RaceRules.MaxTickets];
    private readonly StringBuilder builder = new();

    private LanguageInfo? language;
    private CasinoRaceRunnerDto[]? formSource;
    private CasinoRaceTicketDto[]? ticketSource;
    private CasinoRaceResultDto[]? resultSource;
    private string rideLine = string.Empty;
    private long rideValue = -1;
    private string wonLine = string.Empty;
    private long wonValue = -1;
    private string countLine = string.Empty;
    private int countValue = -1;
    private string raceLine = string.Empty;
    private long raceValue = -1;
    private CasinoRaceTicketDto[]? paysSource;
    private CasinoRaceRunnerDto[]? paysRunners;
    private string betLine = string.Empty;
    private long betAmount = -1;
    private int betKey = int.MinValue;
    private long betPay = -1;
    private CasinoRaceRunnerDto[]? betRunners;

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
        paysSource = null;
        betKey = int.MinValue;
        rideValue = -1;
        wonValue = -1;
        countValue = -1;
        raceValue = -1;
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

    public string TicketPays(CasinoRaceTicketDto[] list, CasinoRaceRunnerDto[] runners, int index)
    {
        if (!ReferenceEquals(paysSource, list) || !ReferenceEquals(paysRunners, runners))
        {
            paysSource = list;
            paysRunners = runners;
            for (var row = 0; row < ticketPays.Length; row++)
            {
                ticketPays[row] = row < list.Length ? PaysFor(list[row], runners) : string.Empty;
            }
        }

        return index >= 0 && index < ticketPays.Length ? ticketPays[index] : string.Empty;
    }

    public string BetLine(long amount, int kind, int first, int second, CasinoRaceRunnerDto[] runners)
    {
        var key = RaceRules.TicketKey(kind, first, second);
        var pay = PayOf(kind, first, second, runners);
        if (amount == betAmount && key == betKey && pay == betPay && ReferenceEquals(betRunners, runners))
        {
            return betLine;
        }

        betAmount = amount;
        betKey = key;
        betPay = pay;
        betRunners = runners;
        var target = RaceRules.IsPair(kind)
            ? string.Concat(GameNumber.Label(first + 1), "-", GameNumber.Label(second + 1))
            : runners[first].Name;
        betLine = Loc.T(L.Race.BetOn, RaceAmounts.Text(amount), target,
            CasinoMultiples.Label((int)Math.Min(int.MaxValue, pay)));
        return betLine;
    }

    public static long PayOf(int kind, int first, int second, CasinoRaceRunnerDto[] runners)
    {
        if (!RaceRules.IsRunner(first) || first >= runners.Length)
        {
            return 0;
        }

        var partner = RaceRules.IsRunner(second) && second < runners.Length ? runners[second].OddsHundredths : 0;
        return RaceRules.PotentialPayHundredths(kind, runners[first].OddsHundredths,
            runners[first].PlaceOddsHundredths, partner);
    }

    private string PaysFor(CasinoRaceTicketDto ticket, CasinoRaceRunnerDto[] runners)
    {
        var pay = PayOf(ticket.Kind, ticket.Runner, ticket.RunnerB, runners);
        var amount = RaceAmounts.Text(RaceRules.Payout(ticket.Amount, pay));
        return Loc.T(ticket.Kind == RaceRules.KindReverse ? L.Race.PaysUpTo : L.Race.Pays, amount);
    }

    public string Ride(long amount)
    {
        if (amount == rideValue)
        {
            return rideLine;
        }

        rideValue = amount;
        rideLine = Loc.T(L.Race.LetItRide, RaceAmounts.Text(amount));
        return rideLine;
    }

    public string Won(long amount)
    {
        if (amount == wonValue)
        {
            return wonLine;
        }

        wonValue = amount;
        wonLine = Loc.T(L.Race.YouWon, RaceAmounts.Text(amount));
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
