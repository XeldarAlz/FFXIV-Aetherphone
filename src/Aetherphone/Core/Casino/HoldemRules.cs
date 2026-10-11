namespace Aetherphone.Core.Casino;

internal static class HoldemPhases
{
    public const int Waiting = 0;

    public const int Preflop = 1;

    public const int Flop = 2;

    public const int Turn = 3;

    public const int River = 4;

    public const int Showdown = 5;

    public const int Intermission = 6;

    public const int Settled = 7;

    public const int Voided = 8;

    public static bool Betting(int phase) => phase is >= Preflop and <= River;

    public static bool Over(int phase) => phase is Intermission or Settled;
}

internal static class HoldemSeatStates
{
    public const int Empty = 0;

    public const int InHand = 1;

    public const int Folded = 2;

    public const int AllIn = 3;

    public const int SittingOut = 4;

    public const int Waiting = 5;

    public const int Seated = 6;

    public static bool DealtIn(int state) => state is InHand or Folded or AllIn;

    public static bool Live(int state) => state is InHand or AllIn;
}

internal static class HoldemActions
{
    public const int Fold = 1;

    public const int Check = 2;

    public const int Call = 4;

    public const int Bet = 8;

    public const int Raise = 16;

    public const int AllIn = 32;

    public const int Show = 64;

    public const int Muck = 128;

    public const string FoldVerb = "fold";

    public const string CheckVerb = "check";

    public const string CallVerb = "call";

    public const string BetVerb = "bet";

    public const string RaiseVerb = "raise";

    public const string AllInVerb = "allin";

    public const string ShowVerb = "show";

    public const string MuckVerb = "muck";

    public const string SmallBlindVerb = "sb";

    public const string BigBlindVerb = "bb";

    public const string AnteVerb = "ante";

    public const string PostVerb = "post";

    public const string AutoCheckVerb = "auto.check";

    public const string AutoFoldVerb = "auto.fold";

    public static bool Allows(int mask, int action) => (mask & action) != 0;

    public static bool Wagers(int mask) => (mask & (Bet | Raise | AllIn)) != 0;

    public static bool OnlyShowOrMuck(int mask) => mask != 0 && (mask & ~(Show | Muck)) == 0;

    public static string VerbFor(int action) => action switch
    {
        Fold => FoldVerb,
        Check => CheckVerb,
        Call => CallVerb,
        Bet => BetVerb,
        Raise => RaiseVerb,
        AllIn => AllInVerb,
        Show => ShowVerb,
        Muck => MuckVerb,
        _ => string.Empty,
    };

    public static bool CarriesAmount(int action) => action is Bet or Raise;
}

internal static class HoldemRules
{
    public const string Kind = "casino.holdem";

    public const string FeatureFlag = "holdem";

    public const string LowRoom = "holdem-low";

    public const string MidRoom = "holdem-mid";

    public const string HighRoom = "holdem-high";

    public const string RoyalRoom = "holdem-royal";

    public const int LowTier = 0;

    public const int MidTier = 1;

    public const int HighTier = 2;

    public const int RoyalTier = 3;

    public const int TierCount = 4;

    public const int HouseSeats = 6;

    public const int MinSeats = 2;

    public const int MaxSeats = 9;

    public const long MinBuyInBigBlinds = 20;

    public const long MaxBuyInBigBlinds = 100;

    public const long HostedMinBuyInBigBlinds = 10;

    public const long HostedMaxBuyInBigBlinds = 500;

    public const long BuyInStepBigBlinds = 10;

    public const long BuyInCeilingMultiple = 20;

    public const long RoyalBalanceFloor = 1_000_000_000;

    public const int RakeBasisPoints = 500;

    public const int RakeCapBigBlinds = 3;

    public const int TurnSeconds = 20;

    public const int RevealMillisecondsPerCard = 800;

    public const int ShowdownMillisecondsPerReveal = 1_000;

    public const int ShowWindowSeconds = 10;

    public const int IntermissionSeconds = 6;

    public const int SitOutMinutes = 10;

    public const int RejoinPenaltyMinutes = 30;

    public const int TimeBankUses = 3;

    public const int TimeBankSeconds = 10;

    public const int HistoryHands = 20;

    public const int HoleCards = 2;

    public const int BoardSize = 5;

    public const int FaceDown = -1;

    public const int WinChanceScale = 10_000;

    public static readonly string[] HouseRooms = { LowRoom, MidRoom, HighRoom, RoyalRoom };

    private static readonly long[] BigBlinds = { 100, 1_000, 10_000, 100_000 };

    public static long BigBlindFor(int tier) => BigBlinds[Math.Clamp(tier, 0, TierCount - 1)];

    public static long SmallBlindFor(int tier) => BigBlindFor(tier) / 2;

    public static long MinBuyInFor(int tier) => BigBlindFor(tier) * MinBuyInBigBlinds;

    public static long MaxBuyInFor(int tier) => BigBlindFor(tier) * MaxBuyInBigBlinds;

    public static int TierOfRoom(string roomId)
    {
        for (var tier = 0; tier < HouseRooms.Length; tier++)
        {
            if (string.Equals(HouseRooms[tier], roomId, StringComparison.Ordinal))
            {
                return tier;
            }
        }

        return -1;
    }

    public static bool IsHouseRoom(string roomId) => TierOfRoom(roomId) >= 0;

    public static bool RoyalOpenTo(long balance) => balance >= RoyalBalanceFloor;

    public static long Rake(long potTotal, long bigBlind, bool sawFlop)
    {
        if (!sawFlop || potTotal <= 0)
        {
            return 0;
        }

        var rake = (long)((Int128)potTotal * RakeBasisPoints / 10_000);
        return Math.Min(rake, bigBlind * RakeCapBigBlinds);
    }

    public static long SnapRaise(long amount, long bigBlind, long minimum, long maximum)
    {
        if (maximum <= 0)
        {
            return 0;
        }

        if (minimum > maximum || amount >= maximum)
        {
            return maximum;
        }

        var step = bigBlind > 0 ? bigBlind : 1;
        var snapped = (amount + step / 2) / step * step;
        if (snapped < minimum)
        {
            snapped = minimum;
        }

        return snapped > maximum ? maximum : snapped;
    }

    public static long PotFraction(int numerator, int denominator, long streetBet, long toCall, long potTotal,
        long bigBlind, long minimum, long maximum)
    {
        if (denominator <= 0)
        {
            return SnapRaise(minimum, bigBlind, minimum, maximum);
        }

        var called = streetBet + toCall;
        var potAfterCall = potTotal + toCall;
        var raiseBy = (long)((Int128)potAfterCall * numerator / denominator);
        return SnapRaise(called + raiseBy, bigBlind, minimum, maximum);
    }

    public static long Step(long amount, int direction, long bigBlind, long minimum, long maximum)
    {
        var step = bigBlind > 0 ? bigBlind : 1;
        var next = amount + step * Math.Sign(direction);
        if (direction < 0 && amount >= maximum && maximum > minimum)
        {
            next = (maximum - 1) / step * step;
        }

        return SnapRaise(next, bigBlind, minimum, maximum);
    }

    public static float SliderFraction(long amount, long minimum, long maximum)
    {
        if (maximum <= minimum)
        {
            return 1f;
        }

        return Math.Clamp((float)((double)(amount - minimum) / (maximum - minimum)), 0f, 1f);
    }

    public static long AmountAt(float fraction, long bigBlind, long minimum, long maximum)
    {
        if (maximum <= minimum)
        {
            return maximum;
        }

        var raw = minimum + (long)Math.Round((maximum - minimum) * (double)Math.Clamp(fraction, 0f, 1f));
        return SnapRaise(raw, bigBlind, minimum, maximum);
    }

    public static long BuyInCeiling(long maxBuyIn, long bankroll, long maxBet, bool practice)
    {
        if (practice)
        {
            return maxBuyIn;
        }

        var ceiling = maxBet > 0 ? maxBet * BuyInCeilingMultiple : long.MaxValue;
        var top = Math.Min(maxBuyIn, Math.Min(bankroll, ceiling));
        return top < 0 ? 0 : top;
    }

    public static bool CanBuyIn(long minBuyIn, long maxBuyIn, long bankroll, long maxBet, bool practice)
    {
        return practice || BuyInCeiling(maxBuyIn, bankroll, maxBet, false) >= minBuyIn;
    }

    public static long DefaultBuyIn(long minBuyIn, long maxBuyIn, long bankroll, long maxBet, long bigBlind,
        bool practice)
    {
        var top = BuyInCeiling(maxBuyIn, bankroll, maxBet, practice);
        if (top < minBuyIn)
        {
            return 0;
        }

        return SnapBuyIn(top, bigBlind, minBuyIn, top);
    }

    public static long SnapBuyIn(long amount, long bigBlind, long minimum, long maximum)
    {
        if (maximum < minimum)
        {
            return 0;
        }

        var step = bigBlind > 0 ? bigBlind : 1;
        var snapped = amount / step * step;
        if (amount >= maximum)
        {
            snapped = maximum;
        }

        return Math.Clamp(snapped, minimum, maximum);
    }

    public static long StepBuyIn(long amount, int direction, long bigBlind, long minimum, long maximum)
    {
        var step = (bigBlind > 0 ? bigBlind : 1) * BuyInStepBigBlinds;
        return SnapBuyIn(amount + step * Math.Sign(direction), bigBlind, minimum, maximum);
    }

    public static long TopUpRoom(long stack, long maxBuyIn) => maxBuyIn > stack ? maxBuyIn - stack : 0;

    public static int NextDealt(ReadOnlySpan<int> dealt, int after)
    {
        if (dealt.Length == 0)
        {
            return -1;
        }

        var best = -1;
        var lowest = int.MaxValue;
        for (var index = 0; index < dealt.Length; index++)
        {
            var seat = dealt[index];
            if (seat < lowest)
            {
                lowest = seat;
            }

            if (seat > after && (best < 0 || seat < best))
            {
                best = seat;
            }
        }

        return best >= 0 ? best : lowest;
    }

    public static void Blinds(ReadOnlySpan<int> dealt, int button, out int smallBlind, out int bigBlind)
    {
        smallBlind = -1;
        bigBlind = -1;
        if (dealt.Length < 2 || button < 0)
        {
            return;
        }

        smallBlind = dealt.Length == 2 ? button : NextDealt(dealt, button);
        bigBlind = NextDealt(dealt, smallBlind);
    }

    public static int DealOrder(ReadOnlySpan<int> dealt, int button, Span<int> order)
    {
        var count = Math.Min(dealt.Length, order.Length);
        var seat = button;
        for (var index = 0; index < count; index++)
        {
            seat = NextDealt(dealt, seat);
            order[index] = seat;
        }

        return count;
    }
}
