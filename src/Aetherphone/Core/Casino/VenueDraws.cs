using System.Globalization;
using System.Security.Cryptography;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal static class VenueDraws
{
    public const string RollPurpose = "roll";

    public const string DeathrollPurpose = "deathroll";

    public const string RafflePurpose = "raffle";

    public static string StreamBinding(string roomId, long seq)
    {
        return string.Concat(roomId, "#", seq.ToString(CultureInfo.InvariantCulture));
    }

    public static long Roll(byte[] seed, string roomId, long seq, long sides)
    {
        return Draw(seed, roomId, seq, sides);
    }

    public static long Deathroll(byte[] seed, string roomId, long seq, long bound)
    {
        return Draw(seed, roomId, seq, bound);
    }

    public static int Raffle(byte[] seed, string roomId, long seq, ReadOnlySpan<int> tickets, int winners,
        Span<int> drawn)
    {
        var total = 0;
        for (var entrant = 0; entrant < tickets.Length; entrant++)
        {
            total += Math.Max(0, tickets[entrant]);
        }

        var owners = new int[total];
        var cursor = 0;
        for (var entrant = 0; entrant < tickets.Length; entrant++)
        {
            for (var ticket = 0; ticket < tickets[entrant]; ticket++)
            {
                owners[cursor++] = entrant;
            }
        }

        var stream = new CasinoVerifier.DrawStream(seed, StreamBinding(roomId, seq));
        var remaining = total;
        var count = 0;
        while (count < winners && count < drawn.Length && remaining > 0)
        {
            var pick = (int)stream.NextBelow((uint)remaining);
            var owner = owners[pick];
            drawn[count++] = owner;
            var kept = 0;
            for (var index = 0; index < remaining; index++)
            {
                if (owners[index] != owner)
                {
                    owners[kept++] = owners[index];
                }
            }

            remaining = kept;
        }

        return count;
    }

    private static long Draw(byte[] seed, string roomId, long seq, long bound)
    {
        if (bound < 1 || bound > uint.MaxValue)
        {
            return 0;
        }

        var stream = new CasinoVerifier.DrawStream(seed, StreamBinding(roomId, seq));
        return stream.NextBelow((uint)bound) + 1L;
    }
}

internal static class VenueVerifier
{
    public static CasinoRoundVerdict VerifyRoll(CasinoRoundVerifyDto proof, string roomId, long seq, long bound,
        long shownValue, bool deathroll)
    {
        if (!TrySeed(proof, out var seed))
        {
            return Unproven(proof);
        }

        var value = deathroll
            ? VenueDraws.Deathroll(seed, roomId, seq, bound)
            : VenueDraws.Roll(seed, roomId, seq, bound);
        return value == shownValue ? CasinoRoundVerdict.Match : CasinoRoundVerdict.Mismatch;
    }

    public static CasinoRoundVerdict VerifyRaffle(CasinoRoundVerifyDto proof, string roomId, CasinoRaffleDto raffle)
    {
        if (!TrySeed(proof, out var seed))
        {
            return Unproven(proof);
        }

        var entrants = raffle.Entrants ?? Array.Empty<CasinoRaffleEntrantDto>();
        var shown = raffle.WinnersDrawn ?? Array.Empty<CasinoRaffleEntrantDto>();
        var tickets = new int[entrants.Length];
        for (var index = 0; index < entrants.Length; index++)
        {
            tickets[index] = entrants[index].Tickets;
        }

        var drawn = new int[Math.Max(raffle.Winners, 1)];
        var count = VenueDraws.Raffle(seed, roomId, raffle.DrawSeq, tickets, raffle.Winners, drawn);
        if (count != shown.Length)
        {
            return CasinoRoundVerdict.Mismatch;
        }

        for (var index = 0; index < count; index++)
        {
            if (!string.Equals(entrants[drawn[index]].UserId, shown[index].UserId, StringComparison.Ordinal))
            {
                return CasinoRoundVerdict.Mismatch;
            }
        }

        return CasinoRoundVerdict.Match;
    }

    private static CasinoRoundVerdict Unproven(CasinoRoundVerifyDto proof)
    {
        return !proof.Granted || proof.SeedRevealed.Length == 0
            ? CasinoRoundVerdict.Unrevealed
            : CasinoRoundVerdict.Mismatch;
    }

    private static bool TrySeed(CasinoRoundVerifyDto proof, out byte[] seed)
    {
        seed = Array.Empty<byte>();
        if (!proof.Granted || proof.SeedRevealed.Length == 0)
        {
            return false;
        }

        try
        {
            seed = Convert.FromHexString(proof.SeedRevealed);
        }
        catch (FormatException)
        {
            return false;
        }

        var commit = Convert.ToHexStringLower(SHA256.HashData(seed));
        return string.Equals(commit, proof.SeedCommitHash, StringComparison.OrdinalIgnoreCase);
    }
}
