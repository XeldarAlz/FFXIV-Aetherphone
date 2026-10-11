using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Machines;

internal sealed class MachineTraceCycle
{
    public const int Capacity = 64;
    public const float SlotSeconds = 1.1f;
    public const float OverviewSeconds = 0.9f;

    private readonly int[] order = new int[Capacity];

    private int count;
    private int tracedRound = -1;
    private int tracedStep = -1;
    private float seconds;

    public int Count => count;

    public float Seconds => seconds;

    public bool Overview => count > 1 && seconds < OverviewSeconds;

    public float OverviewProgress => Overview ? seconds / OverviewSeconds : 1f;

    public int WinAt(int slot) => order[slot];

    public void Track(int round, int step, CasinoSlotsWinDto[]? wins, int cellCount)
    {
        if (round == tracedRound && step == tracedStep)
        {
            return;
        }

        tracedRound = round;
        tracedStep = step;
        seconds = 0f;
        count = 0;
        if (wins is null)
        {
            return;
        }

        for (var index = 0; index < wins.Length && count < Capacity; index++)
        {
            if (!Traceable(wins[index], cellCount))
            {
                continue;
            }

            var slot = count;
            while (slot > 0 && wins[order[slot - 1]].Pay < wins[index].Pay)
            {
                order[slot] = order[slot - 1];
                slot--;
            }

            order[slot] = index;
            count++;
        }
    }

    public void Advance(float deltaSeconds)
    {
        seconds += MathF.Max(0f, deltaSeconds);
    }

    public void Clear()
    {
        tracedRound = -1;
        tracedStep = -1;
        count = 0;
        seconds = 0f;
    }

    public bool TryActive(CasinoSlotsWinDto[]? wins, out int winIndex)
    {
        winIndex = -1;
        if (count == 0 || wins is null || Overview)
        {
            return false;
        }

        var cycled = count > 1 ? seconds - OverviewSeconds : seconds;
        var candidate = order[(int)(cycled / SlotSeconds) % count];
        if (candidate >= wins.Length)
        {
            return false;
        }

        winIndex = candidate;
        return true;
    }

    public float ActiveProgress
    {
        get
        {
            if (count <= 1)
            {
                return Math.Clamp(seconds / SlotSeconds, 0f, 1f);
            }

            var cycled = seconds - OverviewSeconds;
            var within = cycled - MathF.Floor(cycled / SlotSeconds) * SlotSeconds;
            return Math.Clamp(within / SlotSeconds, 0f, 1f);
        }
    }

    public static bool Traceable(CasinoSlotsWinDto win, int cellCount)
    {
        return IsLine(win) || HasCells(win, cellCount);
    }

    public static bool IsLine(CasinoSlotsWinDto win)
    {
        return win.Line >= 0 && win.Line < SlotsRules.PaylineCount && win.Count >= 2;
    }

    public static bool HasCells(CasinoSlotsWinDto win, int cellCount)
    {
        if (win.Cells is not { } cells)
        {
            return false;
        }

        for (var index = 0; index < cells.Length; index++)
        {
            if (cells[index] >= 0 && cells[index] < cellCount)
            {
                return true;
            }
        }

        return false;
    }
}
