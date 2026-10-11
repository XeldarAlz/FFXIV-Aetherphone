using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Plinko;

internal sealed class PlinkoBoardFx
{
    public const float FlashSeconds = 0.32f;
    public const float SquashSeconds = 0.42f;
    public const float PopSeconds = 0.9f;
    public const float EdgeSeconds = 2.4f;
    public const float SquashDepth = 0.28f;

    private readonly float[] pegFlash = new float[PlinkoBoardLayout.PegCount(PlinkoRules.MaxRows)];
    private readonly float[] slotSquash = new float[PlinkoRules.MaxSlots];
    private readonly float[] slotPop = new float[PlinkoRules.MaxSlots];
    private readonly bool[] slotWon = new bool[PlinkoRules.MaxSlots];

    public float Edge { get; private set; }

    public void Clear()
    {
        Array.Clear(pegFlash);
        Array.Clear(slotSquash);
        Array.Clear(slotPop);
        Array.Clear(slotWon);
        Edge = 0f;
    }

    public void Flash(int row, int column)
    {
        var index = PlinkoBoardLayout.PegIndex(row, column);
        if (index < 0 || index >= pegFlash.Length || column < 0 || column >= PlinkoBoardLayout.PegsInRow(row))
        {
            return;
        }

        pegFlash[index] = 1f;
    }

    public void Land(int slot, bool won, bool edge)
    {
        if (slot < 0 || slot >= slotSquash.Length)
        {
            return;
        }

        slotSquash[slot] = 1f;
        slotPop[slot] = 1f;
        slotWon[slot] = won;
        if (edge)
        {
            Edge = 1f;
        }
    }

    public void Advance(float deltaSeconds)
    {
        var step = MathF.Max(0f, deltaSeconds);
        Decay(pegFlash, step / FlashSeconds);
        Decay(slotSquash, step / SquashSeconds);
        Decay(slotPop, step / PopSeconds);
        Edge = MathF.Max(0f, Edge - step / EdgeSeconds);
    }

    public float PegGlow(int row, int column)
    {
        var index = PlinkoBoardLayout.PegIndex(row, column);
        return index >= 0 && index < pegFlash.Length ? pegFlash[index] : 0f;
    }

    public float SlotPop(int slot) => slot >= 0 && slot < slotPop.Length ? slotPop[slot] : 0f;

    public bool SlotWon(int slot) => slot >= 0 && slot < slotWon.Length && slotWon[slot];

    public float SlotSquash(int slot)
    {
        if (slot < 0 || slot >= slotSquash.Length)
        {
            return 0f;
        }

        var remaining = slotSquash[slot];
        return remaining <= 0f ? 0f : SquashDepth * MathF.Sin(MathF.PI * (1f - remaining)) * remaining;
    }

    private static void Decay(float[] values, float amount)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] > 0f)
            {
                values[index] = MathF.Max(0f, values[index] - amount);
            }
        }
    }
}
