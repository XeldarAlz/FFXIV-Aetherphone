using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Originals;

internal sealed class KenoDrawPlayback
{
    public const float RevealInterval = 0.14f;
    public const float PopSeconds = 0.24f;
    public const float HoldSeconds = 0.25f;
    public const int NotDrawn = -1;

    private readonly int[] drawOrder = new int[OriginalsRules.KenoTiles];
    private readonly bool[] picked = new bool[OriginalsRules.KenoTiles];

    private float elapsed;
    private bool drawing;
    private bool settlePending;
    private bool hasDraw;
    private int shown;
    private int hitsShown;
    private int pickCount;
    private int risk;
    private OriginalsOutcome outcome;

    public KenoDrawPlayback()
    {
        Array.Fill(drawOrder, NotDrawn);
    }

    public bool Drawing => drawing;

    public bool HasDraw => hasDraw;

    public int Shown => shown;

    public int HitsShown => hitsShown;

    public int Picks => pickCount;

    public int Risk => risk;

    public static bool IsValid(CasinoKenoDto dto)
    {
        var drawn = dto.Drawn;
        var picks = dto.Picks;
        if (!dto.Granted || dto.RoundId.Length == 0 || drawn is null || picks is null
            || drawn.Length != OriginalsRules.KenoDraws || !OriginalsRules.AreKenoPicks(picks))
        {
            return false;
        }

        for (var index = 0; index < drawn.Length; index++)
        {
            var tile = drawn[index];
            if (tile < 0 || tile >= OriginalsRules.KenoTiles || drawn.AsSpan(0, index).IndexOf(tile) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    public bool Begin(CasinoKenoDto dto, bool instant)
    {
        if (!IsValid(dto))
        {
            return false;
        }

        Array.Fill(drawOrder, NotDrawn);
        Array.Clear(picked);
        var drawn = dto.Drawn!;
        var picks = dto.Picks!;
        for (var index = 0; index < drawn.Length; index++)
        {
            drawOrder[drawn[index]] = index;
        }

        for (var index = 0; index < picks.Length; index++)
        {
            picked[picks[index]] = true;
        }

        pickCount = picks.Length;
        risk = dto.Risk;
        outcome = new OriginalsOutcome(dto.Stake, dto.Payout, dto.RoundId, dto.Capped);
        hasDraw = true;
        settlePending = true;
        elapsed = 0f;
        shown = 0;
        hitsShown = 0;
        drawing = true;
        if (instant)
        {
            Snap();
        }

        return true;
    }

    public void Advance(float deltaSeconds)
    {
        if (!drawing)
        {
            return;
        }

        elapsed += deltaSeconds;
        if (elapsed >= RevealInterval * OriginalsRules.KenoDraws + HoldSeconds)
        {
            Snap();
        }
    }

    public bool TakeReveal(out bool hit, out int hits)
    {
        hit = false;
        hits = hitsShown;
        var due = drawing
            ? Math.Min(OriginalsRules.KenoDraws, (int)(elapsed / RevealInterval) + 1)
            : OriginalsRules.KenoDraws;
        if (shown >= due)
        {
            return false;
        }

        hit = RevealNext();
        hits = hitsShown;
        return true;
    }

    public void Snap()
    {
        drawing = false;
        elapsed = RevealInterval * OriginalsRules.KenoDraws + HoldSeconds;
        while (shown < OriginalsRules.KenoDraws)
        {
            RevealNext();
        }
    }

    public bool IsShown(int tile) => hasDraw && drawOrder[tile] >= 0 && drawOrder[tile] < shown;

    public bool IsHit(int tile) => IsShown(tile) && picked[tile];

    public bool WasPicked(int tile) => hasDraw && picked[tile];

    public float Pop(int tile)
    {
        if (!IsShown(tile))
        {
            return 0f;
        }

        if (!drawing)
        {
            return 1f;
        }

        var start = drawOrder[tile] * RevealInterval;
        return Math.Clamp((elapsed - start) / PopSeconds, 0f, 1f);
    }

    public bool TakeSettled(out OriginalsOutcome settled)
    {
        settled = default;
        if (!settlePending || drawing)
        {
            return false;
        }

        settlePending = false;
        settled = outcome;
        return true;
    }

    public void Clear()
    {
        Array.Fill(drawOrder, NotDrawn);
        Array.Clear(picked);
        drawing = false;
        settlePending = false;
        hasDraw = false;
        shown = 0;
        hitsShown = 0;
        pickCount = 0;
    }

    private bool RevealNext()
    {
        var tile = TileAt(shown);
        shown++;
        var hit = tile >= 0 && picked[tile];
        if (hit)
        {
            hitsShown++;
        }

        return hit;
    }

    private int TileAt(int order)
    {
        for (var tile = 0; tile < drawOrder.Length; tile++)
        {
            if (drawOrder[tile] == order)
            {
                return tile;
            }
        }

        return NotDrawn;
    }
}
