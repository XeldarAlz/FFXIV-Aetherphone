using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Windows.Components;

namespace Aetherphone.Core.Casino;

internal enum CasinoRoundVerdict
{
    Unrevealed,
    Match,
    Mismatch,
}

internal static class CasinoVerifier
{
    private const string ScratchPrizePurpose = "prize";
    private const string BarkeepPatronsPurpose = "patrons";
    private const string SlotsJackpotPurpose = "jackpot";
    private const string GamblePurpose = "gamble";
    private const string SegmentPurpose = "segment";
    private const string BingoCardPurpose = "card";
    private const string BingoBallPurpose = "ball";
    private const string BlackjackShufflePurpose = "shuffle";
    private const string BlackjackShuffleEntry = "shuffle:";
    private const string MinePurpose = "mine";
    private const string RollPurpose = "roll";
    private const string LimboPurpose = "limbo";
    private const string KenoPurpose = "keno";
    private const string CardPurpose = "card";
    private const string RaceFieldPurpose = "field";
    private const string RaceStrengthPurpose = "strength";
    private const string RaceRunnerPurpose = "runner";
    private const uint BarkeepJitterBound = 3;
    private const uint BarkeepStepCountBound = 3;
    private const uint PegBound = 2;
    private const int PlayingCardsDeck = 52;

    private static readonly uint ScratchWinnerBagSize = (ScratchRules.SymbolCount - 1) * 2;
    private static readonly uint ScratchLoserBagSize = ScratchRules.SymbolCount * 2;

    private static readonly uint[] BingoCardBounds =
    {
        15, 14, 13, 12, 11,
        15, 14, 13, 12, 11,
        15, 14, 13, 12,
        15, 14, 13, 12, 11,
        15, 14, 13, 12, 11,
    };

    public static CasinoRoundVerdict Verify(CasinoRoundVerifyDto round)
    {
        if (!round.Granted || round.State == CasinoRoundStates.Open || round.SeedRevealed.Length == 0)
        {
            return CasinoRoundVerdict.Unrevealed;
        }

        return Verify(round.GameKind, round.SeedRevealed, round.SeedCommitHash, round.RoundId, round.DrawLog,
            round.StreamBinding);
    }

    public static CasinoRoundVerdict Verify(string gameKind, string seedHex, string seedCommitHash, string roundId,
        string drawLog, string streamBinding = "")
    {
        byte[] seed;
        try
        {
            seed = Convert.FromHexString(seedHex);
        }
        catch (FormatException)
        {
            return CasinoRoundVerdict.Mismatch;
        }

        var computedCommit = Convert.ToHexStringLower(SHA256.HashData(seed));
        if (!string.Equals(computedCommit, seedCommitHash, StringComparison.OrdinalIgnoreCase))
        {
            return CasinoRoundVerdict.Mismatch;
        }

        var streamKeyInfo = string.IsNullOrEmpty(streamBinding) ? roundId : streamBinding;
        var replays = IsOriginalsKind(gameKind)
            ? ReplaysFloatDrawLog(seed, streamKeyInfo, drawLog)
            : string.Equals(gameKind, CasinoWire.SlotsKind, StringComparison.Ordinal)
                ? SlotsDrawLog.Replays(seed, streamKeyInfo, drawLog)
                : ReplaysDrawLog(gameKind, seed, streamKeyInfo, drawLog);
        return replays ? CasinoRoundVerdict.Match : CasinoRoundVerdict.Mismatch;
    }

    internal static bool IsOriginalsKind(string gameKind) => gameKind switch
    {
        CasinoWire.MinesKind or CasinoWire.DiceKind or CasinoWire.LimboKind or CasinoWire.KenoKind
            or CasinoWire.HiLoKind => true,
        _ => false,
    };

    internal static bool ReplaysFloatDrawLog(byte[] seed, string streamKeyInfo, string drawLog)
    {
        if (drawLog.Length == 0)
        {
            return false;
        }

        var stream = new DrawStream(seed, streamKeyInfo);
        var mines = 0;
        var keno = 0;
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

            if (!TryFloatBoundFor(purpose, ref mines, ref keno, out var bound) || loggedValue >= bound
                || stream.NextFloatBelow(bound) != loggedValue)
            {
                return false;
            }

            cursor = end + 1;
        }

        return true;
    }

    private static bool TryFloatBoundFor(ReadOnlySpan<char> purpose, ref int mines, ref int keno, out uint bound)
    {
        bound = 0;
        if (purpose.SequenceEqual(MinePurpose))
        {
            if (mines >= OriginalsRules.MaxMines)
            {
                return false;
            }

            bound = (uint)(OriginalsRules.MinesTiles - mines);
            mines++;
            return true;
        }

        if (purpose.SequenceEqual(KenoPurpose))
        {
            if (keno >= OriginalsRules.KenoDraws)
            {
                return false;
            }

            bound = (uint)(OriginalsRules.KenoTiles - keno);
            keno++;
            return true;
        }

        if (purpose.SequenceEqual(RollPurpose))
        {
            bound = OriginalsRules.DiceRollBound;
            return true;
        }

        if (purpose.SequenceEqual(LimboPurpose))
        {
            bound = OriginalsRules.LimboBound;
            return true;
        }

        if (purpose.SequenceEqual(CardPurpose))
        {
            bound = OriginalsRules.HiLoDeck;
            return true;
        }

        return false;
    }

    internal static bool TrySegmentBound(string gameKind, out uint bound)
    {
        if (string.Equals(gameKind, CasinoWire.DailySpinKind, StringComparison.Ordinal))
        {
            bound = DailySpinRules.SegmentCount;
            return true;
        }

        if (string.Equals(gameKind, CasinoWire.WheelKind, StringComparison.Ordinal))
        {
            bound = WheelRules.SegmentCount;
            return true;
        }

        bound = 0;
        return false;
    }

    internal static bool ReplaysDrawLog(string gameKind, byte[] seed, string streamKeyInfo, string drawLog)
    {
        if (drawLog.Length == 0)
        {
            return false;
        }

        if (string.Equals(gameKind, CasinoWire.RaceKind, StringComparison.Ordinal))
        {
            Span<int> order = stackalloc int[RaceRules.FieldSize];
            Span<int> birds = stackalloc int[RaceRules.FieldSize];
            return ReplaysRaceLog(seed, streamKeyInfo, drawLog, birds, order);
        }

        TrySegmentBound(gameKind, out var segmentBound);
        var holdem = string.Equals(gameKind, HoldemRules.Kind, StringComparison.Ordinal)
                     || string.Equals(gameKind, DealerHoldemRules.Kind, StringComparison.Ordinal);
        var shoeCards = ShoeCardsOf(drawLog);
        var stream = new DrawStream(seed, streamKeyInfo);
        var shuffles = default(ShuffleRun);
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

            var occurrence = shuffles.Next(purpose);
            var bounded = holdem && purpose.SequenceEqual(BlackjackShufflePurpose)
                ? TryHoldemShuffleBound(occurrence, out var bound)
                : TryBoundFor(purpose, occurrence, segmentBound, shoeCards, out bound);
            if (!bounded || loggedValue >= bound)
            {
                return false;
            }

            if (stream.NextBelow(bound) != loggedValue)
            {
                return false;
            }

            cursor = end + 1;
        }

        return true;
    }

    internal static bool ReplaysRaceLog(byte[] seed, string streamKeyInfo, string drawLog, Span<int> birds,
        Span<int> order)
    {
        if (drawLog.Length == 0 || birds.Length < RaceRules.FieldSize || order.Length < RaceRules.FieldSize)
        {
            return false;
        }

        var stream = new DrawStream(seed, streamKeyInfo);
        Span<int> bank = stackalloc int[RaceRules.BirdBank];
        for (var bird = 0; bird < bank.Length; bird++)
        {
            bank[bird] = bird;
        }

        Span<int> strengths = stackalloc int[RaceRules.FieldSize];
        Span<bool> placed = stackalloc bool[RaceRules.FieldSize];
        var fields = 0;
        var strengthCount = 0;
        var runners = 0;
        var remaining = 0L;
        var cursor = 0;
        while (cursor < drawLog.Length)
        {
            var separator = drawLog.IndexOf(';', cursor);
            var end = separator < 0 ? drawLog.Length : separator;
            var pair = drawLog.AsSpan(cursor, end - cursor);
            var colon = pair.IndexOf(':');
            if (colon <= 0 || colon == pair.Length - 1
                || !uint.TryParse(pair[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture,
                    out var logged))
            {
                return false;
            }

            var purpose = pair[..colon];
            if (purpose.SequenceEqual(RaceFieldPurpose))
            {
                if (fields >= RaceRules.FieldSize || strengthCount > 0)
                {
                    return false;
                }

                var bound = (uint)(RaceRules.BirdBank - fields);
                if (logged >= bound || stream.NextBelow(bound) != logged)
                {
                    return false;
                }

                var pick = fields + (int)logged;
                (bank[fields], bank[pick]) = (bank[pick], bank[fields]);
                birds[fields] = bank[fields];
                fields++;
            }
            else if (purpose.SequenceEqual(RaceStrengthPurpose))
            {
                if (fields != RaceRules.FieldSize || strengthCount >= RaceRules.FieldSize || runners > 0)
                {
                    return false;
                }

                const uint bound = RaceRules.StrengthSpread;
                if (logged >= bound || stream.NextBelow(bound) != logged)
                {
                    return false;
                }

                strengths[strengthCount] = RaceRules.StrengthBase + (int)logged;
                remaining += strengths[strengthCount];
                strengthCount++;
            }
            else if (purpose.SequenceEqual(RaceRunnerPurpose))
            {
                if (strengthCount != RaceRules.FieldSize || runners >= RaceRules.FieldSize - 1 || remaining <= 0)
                {
                    return false;
                }

                var bound = (uint)remaining;
                if (logged >= bound || stream.NextBelow(bound) != logged)
                {
                    return false;
                }

                var slot = WalkToRunner(strengths, placed, logged);
                placed[slot] = true;
                order[runners] = slot;
                remaining -= strengths[slot];
                runners++;
            }
            else
            {
                return false;
            }

            cursor = end + 1;
        }

        if (fields != RaceRules.FieldSize || strengthCount != RaceRules.FieldSize
            || runners != RaceRules.FieldSize - 1)
        {
            return false;
        }

        for (var slot = 0; slot < RaceRules.FieldSize; slot++)
        {
            if (!placed[slot])
            {
                order[runners] = slot;
                return true;
            }
        }

        return false;
    }

    private static int WalkToRunner(ReadOnlySpan<int> strengths, ReadOnlySpan<bool> placed, uint pick)
    {
        var left = (long)pick;
        var last = -1;
        for (var slot = 0; slot < strengths.Length; slot++)
        {
            if (placed[slot])
            {
                continue;
            }

            last = slot;
            if (left < strengths[slot])
            {
                return slot;
            }

            left -= strengths[slot];
        }

        return last;
    }

    internal static bool TryHoldemShuffleBound(int occurrence, out uint bound)
    {
        var deck = PlayingCardsDeck;
        if (occurrence < 0 || occurrence >= deck - 1)
        {
            bound = 0;
            return false;
        }

        bound = (uint)(deck - occurrence);
        return true;
    }

    internal static bool TryBoundFor(ReadOnlySpan<char> purpose, uint segmentBound, out uint bound)
    {
        return TryBoundFor(purpose, 0, segmentBound, out bound);
    }

    internal static int ShoeCardsOf(string drawLog)
    {
        var shuffles = 0;
        var cursor = drawLog.IndexOf(BlackjackShuffleEntry, StringComparison.Ordinal);
        while (cursor >= 0)
        {
            if (cursor == 0 || drawLog[cursor - 1] == ';')
            {
                shuffles++;
            }

            cursor = drawLog.IndexOf(BlackjackShuffleEntry, cursor + BlackjackShuffleEntry.Length,
                StringComparison.Ordinal);
        }

        var shoeCards = shuffles + 1;
        var decks = CasinoRuleSheet.Decks;
        for (var index = 0; index < decks.Length; index++)
        {
            if (decks[index] * PlayingCards.DeckSize == shoeCards)
            {
                return shoeCards;
            }
        }

        return BlackjackRules.ShoeCards;
    }

    internal static bool TryBoundFor(ReadOnlySpan<char> purpose, int occurrence, uint segmentBound, out uint bound)
    {
        return TryBoundFor(purpose, occurrence, segmentBound, BlackjackRules.ShoeCards, out bound);
    }

    internal static bool TryBoundFor(ReadOnlySpan<char> purpose, int occurrence, uint segmentBound, int shoeCards,
        out uint bound)
    {
        bound = 0;
        if (occurrence < 0)
        {
            return false;
        }

        if (purpose.SequenceEqual(ScratchPrizePurpose))
        {
            bound = ScratchRules.TableScale;
            return true;
        }

        if (purpose.SequenceEqual(BarkeepPatronsPurpose))
        {
            bound = (uint)BarkeepRules.PatronBuckets.Length;
            return true;
        }

        if (purpose.SequenceEqual(SlotsJackpotPurpose))
        {
            bound = (uint)SlotsRules.LegacyJackpotChipsPerHit;
            return true;
        }

        if (purpose.SequenceEqual(GamblePurpose))
        {
            bound = 2;
            return true;
        }

        if (purpose.SequenceEqual(SegmentPurpose))
        {
            bound = segmentBound;
            return segmentBound > 0;
        }

        if (purpose.SequenceEqual(BingoCardPurpose))
        {
            bound = BingoCardBounds[occurrence % BingoCardBounds.Length];
            return true;
        }

        if (purpose.SequenceEqual(BingoBallPurpose))
        {
            if (occurrence >= BingoRules.Balls - 1)
            {
                return false;
            }

            bound = (uint)(BingoRules.Balls - occurrence);
            return true;
        }

        if (purpose.SequenceEqual(BlackjackShufflePurpose))
        {
            if (occurrence >= shoeCards - 1)
            {
                return false;
            }

            bound = (uint)(shoeCards - occurrence);
            return true;
        }

        if (purpose.SequenceEqual(PlinkoRules.PegPurpose))
        {
            if (occurrence >= PlinkoRules.MaxRows)
            {
                return false;
            }

            bound = PegBound;
            return true;
        }

        if (purpose.Length < 2)
        {
            return false;
        }

        var marker = purpose[0];
        var argument = purpose[1..];
        switch (marker)
        {
            case 's':
            {
                var reelSplit = argument.IndexOf('r');
                if (reelSplit <= 0 || reelSplit == argument.Length - 1
                    || !TryParseIndex(argument[..reelSplit], out var spinIndex)
                    || !TryParseIndex(argument[(reelSplit + 1)..], out var reelIndex)
                    || spinIndex > SlotsRules.LegacyFreeSpinCap || reelIndex >= SlotsRules.ReelCount)
                {
                    return false;
                }

                bound = SlotsRules.LegacyStopsPerReel;
                return true;
            }
            case 'w':
            {
                if (!TryParseIndex(argument, out var pickIndex)
                    || pickIndex >= ScratchRules.CellCount - ScratchRules.MatchesToWin)
                {
                    return false;
                }

                bound = ScratchWinnerBagSize - (uint)pickIndex;
                return true;
            }
            case 'g':
            {
                if (!TryParseIndex(argument, out var cellIndex)
                    || cellIndex < 1 || cellIndex >= ScratchRules.CellCount)
                {
                    return false;
                }

                bound = (uint)cellIndex + 1;
                return true;
            }
            case 'l':
            {
                if (!TryParseIndex(argument, out var pickIndex) || pickIndex >= ScratchRules.CellCount)
                {
                    return false;
                }

                bound = ScratchLoserBagSize - (uint)pickIndex;
                return true;
            }
            case 'a':
            {
                if (!TryParseIndex(argument, out var patronIndex) || patronIndex >= BarkeepRules.MaxPatrons)
                {
                    return false;
                }

                bound = BarkeepJitterBound;
                return true;
            }
            case 'n':
            {
                if (!TryParseIndex(argument, out var patronIndex) || patronIndex >= BarkeepRules.MaxPatrons)
                {
                    return false;
                }

                bound = BarkeepStepCountBound;
                return true;
            }
            case 'k':
            {
                var stepSplit = argument.IndexOf('.');
                if (stepSplit <= 0 || stepSplit == argument.Length - 1
                    || !TryParseIndex(argument[..stepSplit], out var patronIndex)
                    || !TryParseIndex(argument[(stepSplit + 1)..], out var stepIndex)
                    || patronIndex >= BarkeepRules.MaxPatrons || stepIndex >= BarkeepRules.StepKindCount)
                {
                    return false;
                }

                bound = BarkeepRules.StepKindCount;
                return true;
            }
            default:
                return false;
        }
    }

    private static bool TryParseIndex(ReadOnlySpan<char> text, out int value)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private struct ShuffleRun
    {
        private int cards;

        private int balls;

        private int shoeSwaps;

        private int pegs;

        public int Next(ReadOnlySpan<char> purpose)
        {
            if (purpose.SequenceEqual(PlinkoRules.PegPurpose))
            {
                var taken = pegs;
                pegs++;
                return taken;
            }

            if (purpose.SequenceEqual(BingoCardPurpose))
            {
                var taken = cards;
                cards++;
                return taken;
            }

            if (purpose.SequenceEqual(BingoBallPurpose))
            {
                var taken = balls;
                balls++;
                return taken;
            }

            if (purpose.SequenceEqual(BlackjackShufflePurpose))
            {
                var taken = shoeSwaps;
                shoeSwaps++;
                return taken;
            }

            return 0;
        }
    }

    internal sealed class DrawStream
    {
        private const int BlockBytes = 32;
        private const int WordBytes = 4;
        private const ulong WordSpace = 0x1_0000_0000UL;

        private readonly byte[] streamKey;

        private readonly byte[] block = new byte[BlockBytes];

        private uint blockCounter;

        private int blockOffset = BlockBytes;

        public DrawStream(byte[] seed, string streamKeyInfo)
        {
            streamKey = HMACSHA256.HashData(seed, Encoding.UTF8.GetBytes(streamKeyInfo));
        }

        public uint NextBelow(uint bound)
        {
            var limit = WordSpace / bound * bound;
            while (true)
            {
                var raw = NextUInt32();
                if (raw >= limit)
                {
                    continue;
                }

                return raw % bound;
            }
        }

        public uint NextFloatBelow(uint bound)
        {
            return (uint)((ulong)NextUInt32() * bound >> 32);
        }

        private uint NextUInt32()
        {
            if (blockOffset > block.Length - WordBytes)
            {
                Span<byte> counter = stackalloc byte[WordBytes];
                BinaryPrimitives.WriteUInt32BigEndian(counter, blockCounter);
                blockCounter++;
                HMACSHA256.HashData(streamKey, counter, block);
                blockOffset = 0;
            }

            var value = BinaryPrimitives.ReadUInt32BigEndian(block.AsSpan(blockOffset, WordBytes));
            blockOffset += WordBytes;
            return value;
        }
    }
}
