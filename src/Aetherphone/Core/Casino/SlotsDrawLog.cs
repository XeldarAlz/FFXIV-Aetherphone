using System.Globalization;

namespace Aetherphone.Core.Casino;

internal enum SlotsDrawProfile : byte
{
    Legacy,
    Bird,
    CascadeBase,
    CascadeAnte,
    CascadeBuy,
    Moogle,
}

internal static class SlotsDrawLog
{
    private const string JackpotPurpose = "jackpot";
    private const string ExpanderPurpose = "expander";
    private const string OrbPurpose = "orb";
    private const string CoinPurpose = "coin";
    private const string MiniMarkPurpose = "mini.mark";
    private const string MinorMarkPurpose = "minor.mark";
    private const string GiantSuffix = "giant";

    private static readonly SlotsDrawProfile[] Profiles =
    {
        SlotsDrawProfile.Legacy,
        SlotsDrawProfile.Bird,
        SlotsDrawProfile.CascadeBase,
        SlotsDrawProfile.CascadeAnte,
        SlotsDrawProfile.CascadeBuy,
        SlotsDrawProfile.Moogle,
    };

    private static readonly int CascadeBaseTotal = CrystalCascadeRules.Total(CrystalCascadeRules.BaseWeights);
    private static readonly int CascadeAnteTotal = CrystalCascadeRules.Total(CrystalCascadeRules.AnteWeights);
    private static readonly int CascadeFreeTotal = CrystalCascadeRules.Total(CrystalCascadeRules.FreeWeights);
    private static readonly int CascadeBuyTotal = CrystalCascadeRules.Total(CrystalCascadeRules.BuyWeights);
    private static readonly int OrbTotal = CrystalCascadeRules.Total(CrystalCascadeRules.OrbWeights);
    private static readonly int GiantTotal = MoogleMoneyRules.GiantWeightTotal();

    public static bool Replays(byte[] seed, string streamKeyInfo, string drawLog)
    {
        if (drawLog.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < Profiles.Length; index++)
        {
            if (ReplaysAs(Profiles[index], seed, streamKeyInfo, drawLog))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ReplaysAs(SlotsDrawProfile profile, byte[] seed, string streamKeyInfo, string drawLog)
    {
        var stream = new CasinoVerifier.DrawStream(seed, streamKeyInfo);
        var scan = default(CoinScan);
        var cursor = 0;
        while (cursor < drawLog.Length)
        {
            var separator = drawLog.IndexOf(';', cursor);
            var end = separator < 0 ? drawLog.Length : separator;
            var pair = drawLog.AsSpan(cursor, end - cursor);
            var colon = pair.IndexOf(':');
            if (colon <= 0 || colon == pair.Length - 1)
            {
                return false;
            }

            var purpose = pair[..colon];
            if (!uint.TryParse(pair[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture,
                    out var loggedValue))
            {
                return false;
            }

            if (!TryBound(profile, purpose, ref scan, out var bound) || loggedValue >= bound
                || stream.NextBelow(bound) != loggedValue)
            {
                return false;
            }

            scan.After(purpose, loggedValue);
            cursor = end + 1;
        }

        return true;
    }

    private static bool TryBound(SlotsDrawProfile profile, ReadOnlySpan<char> purpose, ref CoinScan scan,
        out uint bound)
    {
        bound = 0;
        if (purpose.SequenceEqual(JackpotPurpose))
        {
            bound = (uint)(profile == SlotsDrawProfile.Legacy
                ? SlotsRules.LegacyJackpotChipsPerHit
                : SlotsRules.JackpotChipsPerHit);
            return true;
        }

        return profile switch
        {
            SlotsDrawProfile.Legacy => LegacyBound(purpose, out bound),
            SlotsDrawProfile.Bird => BirdBound(purpose, out bound),
            SlotsDrawProfile.Moogle => MoogleBound(purpose, ref scan, out bound),
            _ => CascadeBound(profile, purpose, out bound),
        };
    }

    private static bool LegacyBound(ReadOnlySpan<char> purpose, out uint bound)
    {
        bound = 0;
        if (!TryReelTag(purpose, 's', out var spinIndex, out var reel)
            || spinIndex > SlotsRules.LegacyFreeSpinCap || reel >= SlotsRules.ReelCount)
        {
            return false;
        }

        bound = SlotsRules.LegacyStopsPerReel;
        return true;
    }

    private static bool BirdBound(ReadOnlySpan<char> purpose, out uint bound)
    {
        bound = 0;
        if (purpose.SequenceEqual(ExpanderPurpose))
        {
            bound = GoldenBirdRules.ExpanderWeightTotal;
            return true;
        }

        if (!TryReelTag(purpose, 's', out var spinIndex, out var reel)
            || spinIndex > GoldenBirdRules.FreeSpinCap || reel >= SlotsRules.ReelCount)
        {
            return false;
        }

        bound = (uint)(spinIndex == 0 ? GoldenBirdRules.StripLengths[reel] : GoldenBirdRules.FreeStripLengths[reel]);
        return true;
    }

    private static bool CascadeBound(SlotsDrawProfile profile, ReadOnlySpan<char> purpose, out uint bound)
    {
        bound = 0;
        if (purpose.SequenceEqual(OrbPurpose))
        {
            bound = (uint)OrbTotal;
            return true;
        }

        if (purpose[0] == 'b' && IsTumbleTag(purpose[1..]))
        {
            if (profile == SlotsDrawProfile.CascadeBuy)
            {
                return false;
            }

            bound = (uint)(profile == SlotsDrawProfile.CascadeAnte ? CascadeAnteTotal : CascadeBaseTotal);
            return true;
        }

        if (purpose[0] != 'f')
        {
            return false;
        }

        var rest = purpose[1..];
        var split = rest.IndexOf('t');
        var spinText = split < 0 ? rest : rest[..split];
        if (!TryIndex(spinText, out var spinIndex) || spinIndex < 1 || spinIndex > CrystalCascadeRules.FreeSpinCap
            || (split >= 0 && !IsTumbleTag(rest[split..])))
        {
            return false;
        }

        bound = (uint)(profile == SlotsDrawProfile.CascadeBuy ? CascadeBuyTotal : CascadeFreeTotal);
        return true;
    }

    private static bool MoogleBound(ReadOnlySpan<char> purpose, ref CoinScan scan, out uint bound)
    {
        bound = 0;
        if (purpose.SequenceEqual(MiniMarkPurpose))
        {
            bound = (uint)(MoogleMoneyRules.MiniCeilingUnits - MoogleMoneyRules.MiniResetUnits);
            return true;
        }

        if (purpose.SequenceEqual(MinorMarkPurpose))
        {
            bound = (uint)(MoogleMoneyRules.MinorCeilingUnits - MoogleMoneyRules.MinorResetUnits);
            return true;
        }

        if (purpose.SequenceEqual(CoinPurpose))
        {
            bound = MoogleMoneyRules.CoinWeightTotal;
            return true;
        }

        if (TryReelTag(purpose, 's', out var spinIndex, out var reel))
        {
            if (spinIndex != 0 || reel >= SlotsRules.ReelCount)
            {
                return false;
            }

            bound = (uint)MoogleMoneyRules.StripLengths[reel];
            return true;
        }

        if (purpose[0] == 'h')
        {
            var respin = purpose.LastIndexOf('r');
            if (respin <= 0 || !TryIndex(purpose[(respin + 1)..], out _))
            {
                return false;
            }

            bound = MoogleMoneyRules.ChanceScale;
            return true;
        }

        if (purpose[0] == 'c')
        {
            if (!TryIndex(purpose[1..], out var baseSpin) || baseSpin != 0)
            {
                return false;
            }

            return scan.Bound(purpose, false, out bound);
        }

        if (purpose[0] != 'g')
        {
            return false;
        }

        var rest = purpose[1..];
        var digits = 0;
        while (digits < rest.Length && char.IsAsciiDigit(rest[digits]))
        {
            digits++;
        }

        if (digits == 0 || !TryIndex(rest[..digits], out var game) || game < 1 || game > MoogleMoneyRules.FreeGames)
        {
            return false;
        }

        var tail = rest[digits..];
        if (tail.SequenceEqual(GiantSuffix))
        {
            bound = (uint)GiantTotal;
            return true;
        }

        if (tail.Length == 1 && tail[0] == 'c')
        {
            return scan.Bound(purpose, true, out bound);
        }

        if (tail.Length < 2 || tail[0] != 'r' || !TryIndex(tail[1..], out var freeReel)
            || freeReel >= SlotsRules.ReelCount)
        {
            return false;
        }

        bound = (uint)MoogleMoneyRules.FreeStripLengths[freeReel];
        return true;
    }

    private static bool IsTumbleTag(ReadOnlySpan<char> tail)
    {
        if (tail.Length == 0)
        {
            return true;
        }

        return tail[0] == 't' && TryIndex(tail[1..], out var refill) && refill >= 1;
    }

    private static bool TryReelTag(ReadOnlySpan<char> purpose, char marker, out int spinIndex, out int reel)
    {
        spinIndex = 0;
        reel = 0;
        if (purpose.Length < 4 || purpose[0] != marker)
        {
            return false;
        }

        var argument = purpose[1..];
        var split = argument.IndexOf('r');
        return split > 0 && split < argument.Length - 1 && TryIndex(argument[..split], out spinIndex)
            && TryIndex(argument[(split + 1)..], out reel);
    }

    private static bool TryIndex(ReadOnlySpan<char> text, out int value)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private struct CoinScan
    {
        private string tag;
        private int cell;
        private bool awaitingCoin;
        private bool giantCoin;
        private bool lastWasChance;

        public bool Bound(ReadOnlySpan<char> purpose, bool giantGame, out uint bound)
        {
            bound = 0;
            if (tag is null || !purpose.SequenceEqual(tag))
            {
                tag = purpose.ToString();
                cell = 0;
                awaitingCoin = false;
            }

            lastWasChance = false;
            if (awaitingCoin)
            {
                awaitingCoin = false;
                cell++;
                bound = MoogleMoneyRules.CoinWeightTotal;
                return true;
            }

            while (cell < SlotsRules.CellCount)
            {
                var giantCell = giantGame && MoogleMoneyRules.IsGiantReel(cell / SlotsRules.RowCount);
                if (!giantCell)
                {
                    lastWasChance = true;
                    bound = MoogleMoneyRules.ChanceScale;
                    return true;
                }

                if (giantCoin)
                {
                    cell++;
                    bound = MoogleMoneyRules.CoinWeightTotal;
                    return true;
                }

                cell++;
            }

            return false;
        }

        public void After(ReadOnlySpan<char> purpose, uint value)
        {
            if (purpose.Length > 4 && purpose[0] == 'g' && purpose.EndsWith(GiantSuffix))
            {
                giantCoin = MoogleMoneyRules.GiantFor((int)value) == MoogleMoneyRules.Coin;
                return;
            }

            if (!lastWasChance || tag is null || !purpose.SequenceEqual(tag))
            {
                return;
            }

            lastWasChance = false;
            if (value < MoogleMoneyRules.CoinChance)
            {
                awaitingCoin = true;
                return;
            }

            cell++;
        }
    }
}
