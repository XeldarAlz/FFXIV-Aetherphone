using System;

namespace Aetherphone.Apps.Games.Tetris;

internal sealed class TetrisLevelSystem
{
    public const int LinesPerLevel = 10;

    private const float BaseDropInterval = 0.72f;

    private const float MinimumDropInterval = 0.08f;

    private const float DropIntervalStep = 0.055f;

    public int Level { get; private set; } = 1;

    public int TotalLinesCleared { get; private set; }

    public float DropInterval => GetDropInterval(Level);

    public void Reset()
    {
        Level = 1;
        TotalLinesCleared = 0;
    }

    public int RegisterClearedLines(int clearedLines)
    {
        if (clearedLines <= 0)
        {
            return Level;
        }

        TotalLinesCleared += clearedLines;
        var nextLevel = 1 + TotalLinesCleared / LinesPerLevel;
        if (nextLevel > Level)
        {
            Level = nextLevel;
        }

        return Level;
    }

    public float GetDropInterval(int level)
    {
        return MathF.Max(MinimumDropInterval, BaseDropInterval - DropIntervalStep * MathF.Max(0, level - 1));
    }
}