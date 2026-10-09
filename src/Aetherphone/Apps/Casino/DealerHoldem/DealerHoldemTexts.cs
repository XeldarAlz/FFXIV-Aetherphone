using System.Globalization;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.DealerHoldem;

internal sealed class DealerHoldemTexts
{
    public const int TableRows = 7;
    public const int BlindRows = 6;

    private static readonly LocString[] SpotNames =
    {
        L.DealerHoldem.SpotTrips,
        L.DealerHoldem.SpotAnte,
        L.DealerHoldem.SpotBlind,
        L.DealerHoldem.SpotPlay,
    };

    private static string topPay = string.Empty;
    private static LanguageInfo? topPayLanguage;

    private readonly string[] handNames = new string[TableRows];
    private readonly string[] blindOdds = new string[BlindRows];
    private readonly string[] tripsOdds = new string[TableRows];
    private readonly string[] spots = new string[DealerHoldemRules.SpotCount];
    private LanguageInfo? language;

    public ReadOnlySpan<string> BlindNames
    {
        get
        {
            Validate();
            return handNames.AsSpan(0, BlindRows);
        }
    }

    public ReadOnlySpan<string> TripsNames
    {
        get
        {
            Validate();
            return handNames;
        }
    }

    public ReadOnlySpan<string> BlindOdds
    {
        get
        {
            Validate();
            return blindOdds;
        }
    }

    public ReadOnlySpan<string> TripsOdds
    {
        get
        {
            Validate();
            return tripsOdds;
        }
    }

    public string Spot(DealerHoldemSpot spot)
    {
        Validate();
        return spots[(int)spot];
    }

    public static LocString HandName(int category) => category switch
    {
        Core.Casino.HoldemHands.RoyalFlush => L.DealerHoldem.HandRoyal,
        Core.Casino.HoldemHands.StraightFlush => L.DealerHoldem.HandStraightFlush,
        Core.Casino.HoldemHands.Quads => L.DealerHoldem.HandQuads,
        Core.Casino.HoldemHands.FullHouse => L.DealerHoldem.HandFullHouse,
        Core.Casino.HoldemHands.Flush => L.DealerHoldem.HandFlush,
        Core.Casino.HoldemHands.Straight => L.DealerHoldem.HandStraight,
        _ => L.DealerHoldem.HandTrips,
    };

    public static string TopPay()
    {
        if (ReferenceEquals(topPayLanguage, Loc.Current))
        {
            return topPay;
        }

        topPayLanguage = Loc.Current;
        topPay = Loc.T(L.DealerHoldem.Odds,
            Games.Framework.GameNumber.Label(DealerHoldemRules.BlindNumerators[Core.Casino.HoldemHands.RoyalFlush]),
            Games.Framework.GameNumber.Label(1));
        return topPay;
    }

    public static string Ratio(int numerator, int denominator) =>
        string.Concat(numerator.ToString(CultureInfo.InvariantCulture), ":",
            denominator.ToString(CultureInfo.InvariantCulture));

    public static string BlindRatio(int category) =>
        Ratio(DealerHoldemRules.BlindNumerators[category], DealerHoldemRules.BlindDenominators[category]);

    public static string TripsRatio(int category) => Ratio(DealerHoldemRules.TripsPays[category], 1);

    private void Validate()
    {
        if (ReferenceEquals(language, Loc.Current))
        {
            return;
        }

        language = Loc.Current;
        for (var index = 0; index < TableRows; index++)
        {
            var category = DealerHoldemRules.PayCategories[index];
            handNames[index] = Loc.T(HandName(category));
            tripsOdds[index] = TripsRatio(category);
            if (index < BlindRows)
            {
                blindOdds[index] = BlindRatio(category);
            }
        }

        for (var index = 0; index < spots.Length; index++)
        {
            spots[index] = Loc.T(SpotNames[index]);
        }
    }
}
