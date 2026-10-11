using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Originals;

internal enum MinesTile : byte
{
    Hidden,
    Safe,
    Mine,
    Boom,
}

internal sealed class MinesBoard
{
    public const float FlipSeconds = 0.26f;
    public const float MineStagger = 0.045f;
    public const float BoomLead = 0.35f;

    private readonly MinesTile[] tiles = new MinesTile[OriginalsRules.MinesTiles];
    private readonly float[] flips = new float[OriginalsRules.MinesTiles];
    private readonly float[] delays = new float[OriginalsRules.MinesTiles];

    private string roundId = string.Empty;
    private bool capped;
    private int phase = OriginalsRules.PhaseLive;
    private int mines;
    private int safePicks;
    private long stake;
    private long payout;
    private long multiplier;
    private long nextMultiplier;
    private bool settlePending;
    private int revealedStep;
    private int newestTile = -1;
    private bool busted;

    public string RoundId => roundId;

    public int NewestTile => newestTile;

    public bool HasRound => roundId.Length > 0;

    public bool Live => HasRound && phase == OriginalsRules.PhaseLive;

    public int Phase => phase;

    public int Mines => mines;

    public int SafePicks => safePicks;

    public long Stake => stake;

    public long MultiplierTenThousandths => multiplier;

    public long NextMultiplierTenThousandths => nextMultiplier;

    public long CashOutValue => stake * multiplier / OriginalsRules.MultiplierUnit;

    public bool CanCashOut => Live && safePicks > 0;

    public bool Animating
    {
        get
        {
            for (var tile = 0; tile < flips.Length; tile++)
            {
                if (tiles[tile] != MinesTile.Hidden && flips[tile] < 1f)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public MinesTile Tile(int tile) => tiles[tile];

    public float Flip(int tile) => flips[tile];

    public static bool IsValid(CasinoMinesDto dto)
    {
        if (!dto.Granted || dto.RoundId.Length == 0 || !OriginalsRules.IsMineCount(dto.Mines))
        {
            return false;
        }

        return TilesInRange(dto.Revealed) && TilesInRange(dto.MineTiles);
    }

    public bool Apply(CasinoMinesDto dto, bool instant)
    {
        if (!IsValid(dto))
        {
            return false;
        }

        if (!string.Equals(dto.RoundId, roundId, StringComparison.Ordinal))
        {
            Clear();
            roundId = dto.RoundId;
        }

        var wasLive = phase == OriginalsRules.PhaseLive;
        Absorb(dto);
        var revealed = dto.Revealed ?? Array.Empty<int>();
        var finished = dto.Phase != OriginalsRules.PhaseLive;
        var lastIndex = revealed.Length - 1;
        var newestSafe = -1;
        for (var index = 0; index < revealed.Length; index++)
        {
            var tile = revealed[index];
            if (tiles[tile] != MinesTile.Hidden)
            {
                continue;
            }

            var boom = dto.Phase == OriginalsRules.PhaseBusted && index == lastIndex;
            tiles[tile] = boom ? MinesTile.Boom : MinesTile.Safe;
            flips[tile] = instant ? 1f : 0f;
            delays[tile] = 0f;
            newestTile = tile;
            if (boom)
            {
                busted = true;
            }
            else
            {
                newestSafe = index;
            }
        }

        if (newestSafe >= 0)
        {
            revealedStep = newestSafe + 1;
        }

        if (finished)
        {
            RevealMines(dto.MineTiles ?? Array.Empty<int>(), instant);
            if (wasLive)
            {
                settlePending = true;
            }
        }

        return true;
    }

    public bool Resume(CasinoMinesDto dto)
    {
        if (!IsValid(dto) || dto.Phase != OriginalsRules.PhaseLive)
        {
            return false;
        }

        Clear();
        roundId = dto.RoundId;
        Apply(dto, true);
        revealedStep = 0;
        busted = false;
        return true;
    }

    public void Advance(float deltaSeconds)
    {
        for (var tile = 0; tile < flips.Length; tile++)
        {
            if (tiles[tile] == MinesTile.Hidden || flips[tile] >= 1f)
            {
                continue;
            }

            if (delays[tile] > 0f)
            {
                delays[tile] = MathF.Max(0f, delays[tile] - deltaSeconds);
                continue;
            }

            flips[tile] = MathF.Min(1f, flips[tile] + deltaSeconds / FlipSeconds);
        }
    }

    public void Snap()
    {
        for (var tile = 0; tile < flips.Length; tile++)
        {
            if (tiles[tile] != MinesTile.Hidden)
            {
                flips[tile] = 1f;
                delays[tile] = 0f;
            }
        }
    }

    public bool TakeSafeReveal(out int step)
    {
        step = revealedStep;
        if (revealedStep <= 0)
        {
            return false;
        }

        revealedStep = 0;
        return true;
    }

    public bool TakeBust()
    {
        if (!busted)
        {
            return false;
        }

        busted = false;
        return true;
    }

    public bool TakeSettled(out OriginalsOutcome outcome)
    {
        outcome = default;
        if (!settlePending || Animating)
        {
            return false;
        }

        settlePending = false;
        outcome = new OriginalsOutcome(stake, payout, roundId, capped);
        return true;
    }

    public void Clear()
    {
        Array.Clear(tiles);
        Array.Clear(flips);
        Array.Clear(delays);
        roundId = string.Empty;
        phase = OriginalsRules.PhaseLive;
        mines = 0;
        safePicks = 0;
        stake = 0;
        payout = 0;
        capped = false;
        multiplier = 0;
        nextMultiplier = 0;
        settlePending = false;
        revealedStep = 0;
        newestTile = -1;
        busted = false;
    }

    private void Absorb(CasinoMinesDto dto)
    {
        phase = dto.Phase;
        mines = dto.Mines;
        stake = dto.Stake;
        payout = dto.Payout;
        capped = dto.Capped;
        multiplier = dto.MultiplierTenThousandths;
        nextMultiplier = dto.NextMultiplierTenThousandths;
        var revealed = dto.Revealed ?? Array.Empty<int>();
        safePicks = dto.Phase == OriginalsRules.PhaseBusted ? Math.Max(0, revealed.Length - 1) : revealed.Length;
    }

    private void RevealMines(int[] mineTiles, bool instant)
    {
        var order = 0;
        for (var index = 0; index < mineTiles.Length; index++)
        {
            var tile = mineTiles[index];
            if (tiles[tile] != MinesTile.Hidden)
            {
                continue;
            }

            tiles[tile] = MinesTile.Mine;
            flips[tile] = instant ? 1f : 0f;
            delays[tile] = instant ? 0f : BoomLead + order * MineStagger;
            order++;
        }
    }

    private static bool TilesInRange(int[]? tiles)
    {
        if (tiles is null)
        {
            return true;
        }

        for (var index = 0; index < tiles.Length; index++)
        {
            if (tiles[index] < 0 || tiles[index] >= OriginalsRules.MinesTiles)
            {
                return false;
            }
        }

        return true;
    }
}
