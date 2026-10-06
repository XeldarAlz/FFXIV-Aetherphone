namespace Aetherphone.Apps.Games.Tetris;

internal sealed class TetrisScoringSystem
{
    public const int ComboPointsPerLink = 50;
    public const float BackToBackMultiplier = 1.5f;
    private static readonly int[] ModernLineScores = { 0, 100, 300, 500, 800 };
    private static readonly int[] MiniTSpinScores = { 100, 200, 400 };
    private static readonly int[] RegularTSpinScores = { 400, 800, 1200, 1600 };
    private int pendingDropPoints;
    private bool backToBack;
    private int comboChain = -1;
    public int Score { get; private set; }
    public bool LastBackToBack { get; private set; }
    public int LastCombo { get; private set; } = -1;
    public int BestCombo { get; private set; }

    public void Reset()
    {
        Score = 0;
        pendingDropPoints = 0;
        backToBack = false;
        comboChain = -1;
        LastBackToBack = false;
        LastCombo = -1;
        BestCombo = 0;
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

    public int CommitPiece(int clearedLines, int level) => CommitPiece(clearedLines, level, TetrisSpin.None, TetrisRuleset.Classic);

    public int CommitPiece(int clearedLines, int level, TetrisSpin spin, TetrisRuleset ruleset)
    {
        var pieceScore = pendingDropPoints;
        pendingDropPoints = 0;
        if (clearedLines <= 0)
        {
            comboChain = -1;
            LastCombo = -1;
            LastBackToBack = false;
            if (ruleset == TetrisRuleset.Modern && spin != TetrisSpin.None)
            {
                pieceScore += (spin == TetrisSpin.Mini ? MiniTSpinScores[0] : RegularTSpinScores[0]) * level;
            }

            Score += pieceScore;
            return pieceScore;
        }

        comboChain = comboChain < 0 ? 0 : comboChain + 1;
        LastCombo = comboChain;
        BestCombo = Math.Max(BestCombo, comboChain + 1);
        var difficult = clearedLines == 4 || (ruleset == TetrisRuleset.Modern && spin != TetrisSpin.None);
        var applied = difficult && backToBack;
        LastBackToBack = applied;
        if (ruleset == TetrisRuleset.Modern)
        {
            pieceScore += ModernClearScore(clearedLines, level, spin, applied);
        }
        else
        {
            pieceScore += GetLineClearScore(clearedLines, level, applied);
        }

        if (comboChain > 0)
        {
            pieceScore += GetComboBonus(comboChain, level);
        }

        backToBack = difficult;
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
            _ => backToBackTetris ? 600 * level : 400 * level,
        };
    }

    public static int ModernClearScore(int clearedLines, int level, TetrisSpin spin, bool backToBack)
    {
        var baseScore = spin switch
        {
            TetrisSpin.Full => RegularTSpinScores[Math.Min(clearedLines, RegularTSpinScores.Length - 1)],
            TetrisSpin.Mini => MiniTSpinScores[Math.Min(clearedLines, MiniTSpinScores.Length - 1)],
            _ => ModernLineScores[Math.Min(clearedLines, ModernLineScores.Length - 1)],
        };
        var multiplier = backToBack ? BackToBackMultiplier : 1f;
        return (int)(baseScore * level * multiplier);
    }

    public static int GetComboBonus(int comboChain, int level)
    {
        return ComboPointsPerLink * comboChain * level;
    }
}
