using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Tetris;

internal enum TetrisPieceKind
{
    I,
    O,
    T,
    L,
    J,
    S,
    Z,
}

internal enum TetrisRuleset : byte
{
    Classic,
    Modern,
}

internal enum TetrisSpin : byte
{
    None,
    Mini,
    Full,
}

internal sealed class TetrisBoard
{
    public const int Columns = 10;
    public const int Rows = 20;
    public const float LockDelaySeconds = 0.5f;
    public const int MaxLockResets = 15;
    public const float HardDropLockoutSeconds = 0.15f;
    private const int SpawnX = 3;
    private const int SpawnY = 0;
    private static readonly (int X, int Y)[] WallKicks = { (0, 0), (-1, 0), (1, 0), (-2, 0), (2, 0), (0, -1) };

    private static readonly (int X, int Y)[][][] Shapes =
    {
        new[]
        {
            new[] { (0, 1), (1, 1), (2, 1), (3, 1) }, new[] { (2, 0), (2, 1), (2, 2), (2, 3) },
            new[] { (0, 1), (1, 1), (2, 1), (3, 1) }, new[] { (2, 0), (2, 1), (2, 2), (2, 3) },
        },
        new[]
        {
            new[] { (1, 1), (2, 1), (1, 2), (2, 2) }, new[] { (1, 1), (2, 1), (1, 2), (2, 2) },
            new[] { (1, 1), (2, 1), (1, 2), (2, 2) }, new[] { (1, 1), (2, 1), (1, 2), (2, 2) },
        },
        new[]
        {
            new[] { (1, 1), (0, 2), (1, 2), (2, 2) }, new[] { (1, 1), (1, 2), (2, 2), (1, 3) },
            new[] { (0, 2), (1, 2), (2, 2), (1, 3) }, new[] { (1, 1), (0, 2), (1, 2), (1, 3) },
        },
        new[]
        {
            new[] { (2, 1), (0, 2), (1, 2), (2, 2) }, new[] { (1, 1), (1, 2), (1, 3), (2, 3) },
            new[] { (0, 2), (1, 2), (2, 2), (0, 3) }, new[] { (0, 1), (1, 1), (1, 2), (1, 3) },
        },
        new[]
        {
            new[] { (0, 1), (0, 2), (1, 2), (2, 2) }, new[] { (1, 1), (2, 1), (1, 2), (1, 3) },
            new[] { (0, 2), (1, 2), (2, 2), (2, 3) }, new[] { (1, 1), (1, 2), (0, 3), (1, 3) },
        },
        new[]
        {
            new[] { (1, 1), (2, 1), (0, 2), (1, 2) }, new[] { (1, 1), (1, 2), (2, 2), (2, 3) },
            new[] { (1, 2), (2, 2), (0, 3), (1, 3) }, new[] { (0, 1), (0, 2), (1, 2), (1, 3) },
        },
        new[]
        {
            new[] { (0, 1), (1, 1), (1, 2), (2, 2) }, new[] { (2, 1), (1, 2), (2, 2), (1, 3) },
            new[] { (0, 2), (1, 2), (1, 3), (2, 3) }, new[] { (1, 1), (0, 2), (1, 2), (0, 3) },
        },
    };

    private static readonly (int X, int Y)[][] ModernIShapes =
    {
        new[] { (0, 2), (1, 2), (2, 2), (3, 2) }, new[] { (1, 0), (1, 1), (1, 2), (1, 3) },
    };

    private static readonly (int X, int Y)[][] SrsKicksClockwise =
    {
        new[] { (0, 0), (-1, 0), (-1, 1), (0, -2), (-1, -2) },
        new[] { (0, 0), (1, 0), (1, -1), (0, 2), (1, 2) },
        new[] { (0, 0), (1, 0), (1, 1), (0, -2), (1, -2) },
        new[] { (0, 0), (-1, 0), (-1, -1), (0, 2), (-1, 2) },
    };

    private static readonly (int X, int Y)[][] SrsIKicksClockwise =
    {
        new[] { (0, 0), (-2, 0), (1, 0), (-2, -1), (1, 2) },
        new[] { (0, 0), (-1, 0), (2, 0), (-1, 2), (2, -1) },
        new[] { (0, 0), (2, 0), (-1, 0), (2, 1), (-1, -2) },
        new[] { (0, 0), (1, 0), (-2, 0), (1, -2), (-2, 1) },
    };

    private static readonly (int X, int Y)[][] TFrontCorners =
    {
        new[] { (0, 1), (2, 1) }, new[] { (2, 1), (2, 3) }, new[] { (0, 3), (2, 3) }, new[] { (0, 1), (0, 3) },
    };

    private static readonly (int X, int Y)[][] TBackCorners =
    {
        new[] { (0, 3), (2, 3) }, new[] { (0, 1), (0, 3) }, new[] { (0, 1), (2, 1) }, new[] { (2, 1), (2, 3) },
    };

    private readonly int[] cells = new int[Columns * Rows];
    private readonly TetrisPieceKind[] bag = new TetrisPieceKind[7];
    private readonly TetrisLevelSystem levelSystem = new();
    private readonly TetrisScoringSystem scoring = new();
    private GameRandom random;
    private int bagIndex;
    private TetrisPieceKind? heldKind;
    private bool holdUsedThisTurn;
    private float dropTimer;
    private float lockTimer;
    private int lockResets;
    private int lowestY;
    private float hardDropLockout;
    private bool lastMoveWasRotation;
    private TetrisPieceKind activeKind;
    private int activeRotation;
    private int activeX;
    private int activeY;
    public TetrisRuleset Ruleset { get; private set; }
    public int Score => scoring.Score;
    public int Lines => levelSystem.TotalLinesCleared;
    public int Level => levelSystem.Level;
    public int ClearedLinesThisFrame { get; private set; }
    public int ClearedRowsMask { get; private set; }
    public bool LockedThisFrame { get; private set; }
    public int LastLockScore { get; private set; }
    public TetrisSpin LastSpin { get; private set; }
    public bool LastBackToBack => scoring.LastBackToBack;
    public int LastCombo => scoring.LastCombo;
    public int BestCombo => scoring.BestCombo;
    public bool GameOver { get; private set; }
    public bool HasActivePiece { get; private set; }
    public TetrisPieceKind? HeldKind => heldKind;
    public TetrisPieceKind ActiveKind => activeKind;
    public TetrisPieceKind NextPieceKind => bag[bagIndex];
    public int ActiveRotation => activeRotation;
    public int ActiveX => activeX;
    public int ActiveY => activeY;
    public int CellColor(int column, int row) => cells[row * Columns + column];

    public void Reset(TetrisRuleset ruleset, GameRandom seededRandom)
    {
        Ruleset = ruleset;
        random = seededRandom;
        Array.Clear(cells, 0, cells.Length);
        scoring.Reset();
        levelSystem.Reset(ruleset);
        ClearedLinesThisFrame = 0;
        ClearedRowsMask = 0;
        LockedThisFrame = false;
        LastLockScore = 0;
        LastSpin = TetrisSpin.None;
        GameOver = false;
        HasActivePiece = false;
        heldKind = null;
        holdUsedThisTurn = false;
        dropTimer = 0f;
        hardDropLockout = 0f;
        RefillBag();
        SpawnNextPiece();
    }

    public void Update(float deltaSeconds)
    {
        ClearedLinesThisFrame = 0;
        ClearedRowsMask = 0;
        LockedThisFrame = false;
        hardDropLockout = MathF.Max(0f, hardDropLockout - deltaSeconds);
        if (GameOver || !HasActivePiece)
        {
            return;
        }

        if (Ruleset == TetrisRuleset.Modern && Grounded)
        {
            lockTimer += deltaSeconds;
            if (lockTimer >= LockDelaySeconds)
            {
                LockPiece();
            }

            return;
        }

        dropTimer += deltaSeconds;
        while (dropTimer >= DropInterval)
        {
            dropTimer -= DropInterval;
            if (!StepDown())
            {
                break;
            }
        }
    }

    public bool Move(int direction)
    {
        if (GameOver || !HasActivePiece)
        {
            return false;
        }

        if (!CanPlace(activeX + direction, activeY, activeRotation))
        {
            return false;
        }

        activeX += direction;
        lastMoveWasRotation = false;
        ResetLock();
        return true;
    }

    public bool Rotate(int direction)
    {
        if (GameOver || !HasActivePiece)
        {
            return false;
        }

        var clockwise = direction >= 0;
        var nextRotation = (activeRotation + (clockwise ? 1 : 3)) & 3;
        if (Ruleset == TetrisRuleset.Modern && activeKind == TetrisPieceKind.O)
        {
            return false;
        }

        var kicks = Ruleset == TetrisRuleset.Modern ? SrsKicks(activeKind, activeRotation, clockwise) : WallKicks;
        var flip = Ruleset == TetrisRuleset.Modern && !clockwise ? -1 : 1;
        var srs = Ruleset == TetrisRuleset.Modern;
        for (var index = 0; index < kicks.Length; index++)
        {
            var kick = kicks[index];
            var offsetX = kick.X * flip;
            var offsetY = srs ? -kick.Y * flip : kick.Y;
            if (!CanPlace(activeX + offsetX, activeY + offsetY, nextRotation))
            {
                continue;
            }

            activeX += offsetX;
            activeY += offsetY;
            activeRotation = nextRotation;
            lastMoveWasRotation = true;
            ResetLock();
            return true;
        }

        return false;
    }

    private static (int X, int Y)[] SrsKicks(TetrisPieceKind kind, int fromRotation, bool clockwise)
    {
        var table = kind == TetrisPieceKind.I ? SrsIKicksClockwise : SrsKicksClockwise;
        var transitionFrom = clockwise ? fromRotation : (fromRotation + 3) & 3;
        return table[transitionFrom];
    }

    public bool SoftDrop()
    {
        if (GameOver || !HasActivePiece)
        {
            return false;
        }

        if (CanPlace(activeX, activeY + 1, activeRotation))
        {
            activeY += 1;
            lastMoveWasRotation = false;
            TrackDescent();
            scoring.AddSoftDrop(1);
            return true;
        }

        if (Ruleset == TetrisRuleset.Classic)
        {
            LockPiece();
        }

        return false;
    }

    public void HardDrop()
    {
        if (GameOver || !HasActivePiece || hardDropLockout > 0f)
        {
            return;
        }

        var distance = 0;
        while (CanPlace(activeX, activeY + 1, activeRotation))
        {
            activeY += 1;
            distance++;
        }

        if (distance > 0)
        {
            lastMoveWasRotation = false;
        }

        scoring.AddHardDrop(distance);
        LockPiece();
    }

    public bool HoldPiece()
    {
        if (GameOver || !HasActivePiece || holdUsedThisTurn)
        {
            return false;
        }

        holdUsedThisTurn = true;
        if (!heldKind.HasValue)
        {
            heldKind = activeKind;
            SpawnNextPiece(resetHoldLock: false);
            return true;
        }

        var swapKind = heldKind.Value;
        heldKind = activeKind;
        SpawnSpecificPiece(swapKind, resetHoldLock: false);
        return true;
    }

    public int GetGhostY()
    {
        var y = activeY;
        while (CanPlace(activeX, y + 1, activeRotation))
        {
            y++;
        }

        return y;
    }

    public (int X, int Y)[] ActiveCells() => GetCells(activeKind, activeRotation, Ruleset);

    public void Paint(int column, int row, int color)
    {
        cells[row * Columns + column] = color;
    }

    public bool PlaceActive(TetrisPieceKind kind, int x, int y, int rotation)
    {
        activeKind = kind;
        activeRotation = rotation & 3;
        activeX = x;
        activeY = y;
        lockTimer = 0f;
        lockResets = 0;
        lowestY = y;
        lastMoveWasRotation = false;
        HasActivePiece = CanPlace(activeX, activeY, activeRotation);
        return HasActivePiece;
    }

    public static (int X, int Y)[] GetCells(TetrisPieceKind kind, int rotation) => GetCells(kind, rotation, TetrisRuleset.Classic);

    public static (int X, int Y)[] GetCells(TetrisPieceKind kind, int rotation, TetrisRuleset ruleset)
    {
        if (ruleset == TetrisRuleset.Modern && kind == TetrisPieceKind.I && rotation >= 2)
        {
            return ModernIShapes[rotation - 2];
        }

        return Shapes[(int)kind][rotation];
    }

    private bool Grounded => !CanPlace(activeX, activeY + 1, activeRotation);

    private float DropInterval => levelSystem.DropInterval;

    private void ResetLock()
    {
        if (Ruleset != TetrisRuleset.Modern || !Grounded || lockResets >= MaxLockResets)
        {
            return;
        }

        lockTimer = 0f;
        lockResets++;
    }

    private void TrackDescent()
    {
        if (activeY <= lowestY)
        {
            return;
        }

        lowestY = activeY;
        lockResets = 0;
        lockTimer = 0f;
    }

    private void LockPiece()
    {
        var spin = Ruleset == TetrisRuleset.Modern && activeKind == TetrisPieceKind.T && lastMoveWasRotation
            ? DetectTSpin()
            : TetrisSpin.None;
        var cellsForPiece = ActiveCells();
        for (var index = 0; index < cellsForPiece.Length; index++)
        {
            var cell = cellsForPiece[index];
            var column = activeX + cell.X;
            var row = activeY + cell.Y;
            if (row < 0 || row >= Rows || column < 0 || column >= Columns)
            {
                continue;
            }

            cells[row * Columns + column] = (int)activeKind + 1;
        }

        HasActivePiece = false;
        var clearedLines = ClearLines(out var clearedMask);
        ClearedLinesThisFrame = clearedLines;
        ClearedRowsMask = clearedMask;
        LockedThisFrame = true;
        LastSpin = spin;
        LastLockScore = scoring.CommitPiece(clearedLines, levelSystem.Level, spin, Ruleset);
        if (clearedLines > 0)
        {
            levelSystem.RegisterClearedLines(clearedLines);
        }

        if (Ruleset == TetrisRuleset.Modern)
        {
            hardDropLockout = HardDropLockoutSeconds;
        }

        if (!GameOver)
        {
            SpawnNextPiece();
        }
    }

    private TetrisSpin DetectTSpin()
    {
        var front = CountFilled(TFrontCorners[activeRotation]);
        var back = CountFilled(TBackCorners[activeRotation]);
        if (front + back < 3)
        {
            return TetrisSpin.None;
        }

        return front < 2 ? TetrisSpin.Mini : TetrisSpin.Full;
    }

    private int CountFilled((int X, int Y)[] corners)
    {
        var count = 0;
        for (var index = 0; index < corners.Length; index++)
        {
            var column = activeX + corners[index].X;
            var row = activeY + corners[index].Y;
            if (row < 0)
            {
                continue;
            }

            if (column < 0 || column >= Columns || row >= Rows || cells[row * Columns + column] != 0)
            {
                count++;
            }
        }

        return count;
    }

    private bool StepDown()
    {
        if (CanPlace(activeX, activeY + 1, activeRotation))
        {
            activeY += 1;
            lastMoveWasRotation = false;
            TrackDescent();
            return true;
        }

        if (Ruleset == TetrisRuleset.Classic)
        {
            LockPiece();
        }

        return false;
    }

    private int ClearLines(out int clearedMask)
    {
        var cleared = 0;
        clearedMask = 0;
        for (var row = Rows - 1; row >= 0; row--)
        {
            var full = true;
            for (var column = 0; column < Columns; column++)
            {
                if (cells[row * Columns + column] == 0)
                {
                    full = false;
                    break;
                }
            }

            if (!full)
            {
                continue;
            }

            clearedMask |= 1 << (row - cleared);
            cleared++;
            for (var moveRow = row; moveRow > 0; moveRow--)
            {
                for (var column = 0; column < Columns; column++)
                {
                    cells[moveRow * Columns + column] = cells[(moveRow - 1) * Columns + column];
                }
            }

            for (var column = 0; column < Columns; column++)
            {
                cells[column] = 0;
            }

            row++;
        }

        return cleared;
    }

    private void SpawnNextPiece(bool resetHoldLock = true)
    {
        var kind = bag[bagIndex];
        bagIndex++;
        if (bagIndex >= bag.Length)
        {
            RefillBag();
        }

        SpawnSpecificPiece(kind, resetHoldLock);
    }

    private void SpawnSpecificPiece(TetrisPieceKind kind, bool resetHoldLock = true)
    {
        activeKind = kind;
        activeRotation = 0;
        activeX = SpawnX;
        activeY = SpawnY;
        lockTimer = 0f;
        lockResets = 0;
        lowestY = SpawnY;
        lastMoveWasRotation = false;
        HasActivePiece = CanPlace(activeX, activeY, activeRotation);
        holdUsedThisTurn = !resetHoldLock;
        if (!HasActivePiece)
        {
            GameOver = true;
        }
    }

    private bool CanPlace(int x, int y, int rotation)
    {
        var cellsForPiece = GetCells(activeKind, rotation, Ruleset);
        for (var index = 0; index < cellsForPiece.Length; index++)
        {
            var cell = cellsForPiece[index];
            var column = x + cell.X;
            var row = y + cell.Y;
            if (column < 0 || column >= Columns || row < 0 || row >= Rows)
            {
                return false;
            }

            if (cells[row * Columns + column] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private void RefillBag()
    {
        for (var index = 0; index < bag.Length; index++)
        {
            bag[index] = (TetrisPieceKind)index;
        }

        for (var index = bag.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (bag[index], bag[swap]) = (bag[swap], bag[index]);
        }

        bagIndex = 0;
    }
}
