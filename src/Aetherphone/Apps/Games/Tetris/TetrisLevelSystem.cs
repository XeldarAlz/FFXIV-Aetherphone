namespace Aetherphone.Apps.Games.Tetris;

internal sealed class TetrisLevelSystem
{
    private const int LinesPerLevel = 10;

    private const float BaseDropInterval = 0.72f;

    private const float MinimumDropInterval = 0.08f;

    private const float DropIntervalStep = 0.055f;

    public int Level { get; private set; } = 1;

    public int TotalLinesCleared { get; private set; }

    public float DropInterval => MathF.Max(MinimumDropInterval, BaseDropInterval - DropIntervalStep * (Level - 1));

    public void Reset()
    {
        Level = 1;
        TotalLinesCleared = 0;
    }

    public void RegisterClearedLines(int clearedLines)
    {
        if (clearedLines <= 0)
        {
            return;
        }

        TotalLinesCleared += clearedLines;
        var nextLevel = 1 + TotalLinesCleared / LinesPerLevel;
        if (nextLevel > Level)
        {
            Level = nextLevel;
        }
    }
}
