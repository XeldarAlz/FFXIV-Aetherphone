namespace Aetherphone.Apps.Games.Tetris;

internal sealed class TetrisScoringSystem
{
    private int pendingDropPoints;

    private bool backToBackTetris;

    private int comboChain = -1;

    public int Score { get; private set; }

    public int Combo => comboChain < 0 ? 0 : comboChain;

    public void Reset()
    {
        Score = 0;
        pendingDropPoints = 0;
        backToBackTetris = false;
        comboChain = -1;
    }

    public void AddSoftDrop(int cellsDropped)
    {
        if (cellsDropped > 0)
        {
            pendingDropPoints += cellsDropped;
        }
    }

    public void AddHardDrop(int cellsDropped)
    {
        if (cellsDropped > 0)
        {
            pendingDropPoints += cellsDropped * 2;
        }
    }

    public int CommitPiece(int clearedLines, int level)
    {
        var pieceScore = pendingDropPoints;
        pendingDropPoints = 0;

        if (clearedLines <= 0)
        {
            comboChain = -1;
            Score += pieceScore;
            return pieceScore;
        }

        comboChain = comboChain < 0 ? 0 : comboChain + 1;
        pieceScore += GetLineClearScore(clearedLines, level, backToBackTetris);

        if (comboChain > 0)
        {
            pieceScore += GetComboBonus(comboChain, level);
        }

        backToBackTetris = clearedLines == 4;
        Score += pieceScore;
        return pieceScore;
    }

    public static int GetLineClearScore(int clearedLines, int level, bool backToBackTetris)
    {
        return clearedLines switch
        {
            1 => 100 * level,
            2 => 200 * level,
            3 => 300 * level,
            _ => 400 * level,
        };
    }

    public static int GetComboBonus(int comboChain, int level)
    {
        return 50 * comboChain * level;
    }
}