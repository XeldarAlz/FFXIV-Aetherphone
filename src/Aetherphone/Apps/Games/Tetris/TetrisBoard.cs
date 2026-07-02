using System;

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

internal sealed class TetrisBoard
{
    public const int Columns = 10;

    public const int Rows = 20;

    private static readonly (int X, int Y)[] WallKicks = { (0, 0), (-1, 0), (1, 0), (-2, 0), (2, 0), (0, -1) };

    private static readonly (int X, int Y)[][][] Shapes =
    {
        new[]
        {
            new[] { (0, 1), (1, 1), (2, 1), (3, 1) },
            new[] { (2, 0), (2, 1), (2, 2), (2, 3) },
            new[] { (0, 1), (1, 1), (2, 1), (3, 1) },
            new[] { (2, 0), (2, 1), (2, 2), (2, 3) },
        },
        new[]
        {
            new[] { (1, 1), (2, 1), (1, 2), (2, 2) },
            new[] { (1, 1), (2, 1), (1, 2), (2, 2) },
            new[] { (1, 1), (2, 1), (1, 2), (2, 2) },
            new[] { (1, 1), (2, 1), (1, 2), (2, 2) },
        },
        new[]
        {
            new[] { (1, 1), (0, 2), (1, 2), (2, 2) },
            new[] { (1, 1), (1, 2), (2, 2), (1, 3) },
            new[] { (0, 2), (1, 2), (2, 2), (1, 3) },
            new[] { (1, 1), (0, 2), (1, 2), (1, 3) },
        },
        new[]
        {
            new[] { (2, 1), (0, 2), (1, 2), (2, 2) },
            new[] { (1, 1), (1, 2), (1, 3), (2, 3) },
            new[] { (0, 2), (1, 2), (2, 2), (0, 3) },
            new[] { (0, 1), (1, 1), (1, 2), (1, 3) },
        },
        new[]
        {
            new[] { (0, 1), (0, 2), (1, 2), (2, 2) },
            new[] { (1, 1), (2, 1), (1, 2), (1, 3) },
            new[] { (0, 2), (1, 2), (2, 2), (2, 3) },
            new[] { (1, 1), (1, 2), (0, 3), (1, 3) },
        },
        new[]
        {
            new[] { (1, 1), (2, 1), (0, 2), (1, 2) },
            new[] { (1, 1), (1, 2), (2, 2), (2, 3) },
            new[] { (1, 2), (2, 2), (0, 3), (1, 3) },
            new[] { (0, 1), (0, 2), (1, 2), (1, 3) },
        },
        new[]
        {
            new[] { (0, 1), (1, 1), (1, 2), (2, 2) },
            new[] { (2, 1), (1, 2), (2, 2), (1, 3) },
            new[] { (0, 2), (1, 2), (1, 3), (2, 3) },
            new[] { (1, 1), (0, 2), (1, 2), (0, 3) },
        },
    };

    private readonly int[] cells = new int[Columns * Rows];

    private readonly TetrisPieceKind[] bag = new TetrisPieceKind[7];

    private readonly TetrisLevelSystem levelSystem = new();

    private readonly TetrisScoringSystem scoring = new();

    private readonly Random random = new();

    private int bagIndex = 7;

    private TetrisPieceKind? heldKind;

    private bool holdUsedThisTurn;

    private float dropTimer;

    private TetrisPieceKind activeKind;

    private int activeRotation;

    private int activeX;

    private int activeY;

    public int Score => scoring.Score;

    public int Lines => levelSystem.TotalLinesCleared;

    public int Level => levelSystem.Level;

    public int Combo => scoring.Combo;

    public int ClearedLinesThisFrame { get; private set; }

    public bool GameOver { get; private set; }

    public bool HasActivePiece { get; private set; }

    public bool HasHeldPiece => heldKind.HasValue;

    public TetrisPieceKind? HeldKind => heldKind;

    public TetrisPieceKind ActiveKind => activeKind;

    public TetrisPieceKind NextPieceKind => bagIndex < bag.Length ? bag[bagIndex] : (TetrisPieceKind)0;

    public int ActiveRotation => activeRotation;

    public int ActiveX => activeX;

    public int ActiveY => activeY;

    public int CellColor(int column, int row) => cells[row * Columns + column];

    public void Reset()
    {
        Array.Clear(cells, 0, cells.Length);
        scoring.Reset();
        levelSystem.Reset();
        ClearedLinesThisFrame = 0;
        GameOver = false;
        HasActivePiece = false;
        heldKind = null;
        holdUsedThisTurn = false;
        dropTimer = 0f;
        bagIndex = bag.Length;
        RefillBag();
        SpawnNextPiece();
    }

    public void Update(float deltaSeconds)
    {
        ClearedLinesThisFrame = 0;

        if (GameOver || !HasActivePiece)
        {
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

    public bool Move(int dx)
    {
        if (GameOver || !HasActivePiece)
        {
            return false;
        }

        if (!CanPlace(activeX + dx, activeY, activeRotation))
        {
            return false;
        }

        activeX += dx;
        return true;
    }

    public bool Rotate(int direction)
    {
        if (GameOver || !HasActivePiece)
        {
            return false;
        }

        var nextRotation = (activeRotation + (direction >= 0 ? 1 : 3)) & 3;

        for (var index = 0; index < WallKicks.Length; index++)
        {
            var kick = WallKicks[index];
            if (!CanPlace(activeX + kick.X, activeY + kick.Y, nextRotation))
            {
                continue;
            }

            activeX += kick.X;
            activeY += kick.Y;
            activeRotation = nextRotation;
            return true;
        }

        return false;
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
            scoring.AddSoftDrop(1);
            return true;
        }

        LockPiece();
        return false;
    }

    public void HardDrop()
    {
        if (GameOver || !HasActivePiece)
        {
            return;
        }

        var distance = 0;
        while (CanPlace(activeX, activeY + 1, activeRotation))
        {
            activeY += 1;
            distance++;
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

    public (int X, int Y) GetCell(int index)
    {
        return Shapes[(int)activeKind][activeRotation][index];
    }

    public static (int X, int Y)[] GetCells(TetrisPieceKind kind, int rotation)
    {
        return Shapes[(int)kind][rotation];
    }

    private float DropInterval => levelSystem.DropInterval;

    private void LockPiece()
    {
        var cellsForPiece = Shapes[(int)activeKind][activeRotation];
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
        var clearedLines = ClearLines();
        ClearedLinesThisFrame = clearedLines;
        scoring.CommitPiece(clearedLines, levelSystem.Level);

        if (clearedLines > 0)
        {
            levelSystem.RegisterClearedLines(clearedLines);
        }

        if (!GameOver)
        {
            SpawnNextPiece();
        }
    }

    private bool StepDown()
    {
        if (CanPlace(activeX, activeY + 1, activeRotation))
        {
            activeY += 1;
            return true;
        }

        LockPiece();
        return false;
    }

    private int ClearLines()
    {
        var cleared = 0;
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
        if (bagIndex >= bag.Length)
        {
            RefillBag();
        }

        SpawnSpecificPiece(bag[bagIndex++], resetHoldLock);
    }

    private void SpawnSpecificPiece(TetrisPieceKind kind, bool resetHoldLock = true)
    {
        activeKind = kind;
        activeRotation = 0;
        activeX = 3;
        activeY = 0;
        HasActivePiece = CanPlace(activeX, activeY, activeRotation);
        holdUsedThisTurn = !resetHoldLock;

        if (!HasActivePiece)
        {
            GameOver = true;
        }
    }

    private bool CanPlace(int x, int y, int rotation)
    {
        var cellsForPiece = Shapes[(int)activeKind][rotation];
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