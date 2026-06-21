using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games;

internal sealed class Twenty48App : IPhoneApp
{
    private const int GridSize = 4;
    private const int TotalCells = GridSize * GridSize;
    private const float SlideDuration = 0.10f;
    private const float MergePopDuration = 0.18f;
    private const float SpawnDuration = 0.10f;

    private static readonly Vector4[] TileColors =
    {
        new(0.20f, 0.22f, 0.27f, 1f),
        new(0.93f, 0.89f, 0.85f, 1f),
        new(0.93f, 0.87f, 0.78f, 1f),
        new(0.95f, 0.69f, 0.47f, 1f),
        new(0.96f, 0.58f, 0.39f, 1f),
        new(0.96f, 0.49f, 0.37f, 1f),
        new(0.96f, 0.37f, 0.23f, 1f),
        new(0.93f, 0.81f, 0.45f, 1f),
        new(0.93f, 0.80f, 0.38f, 1f),
        new(0.93f, 0.78f, 0.31f, 1f),
        new(0.93f, 0.76f, 0.24f, 1f),
        new(0.40f, 0.70f, 0.95f, 1f),
    };

    public string Id => "2048";
    public string DisplayName => "2048";
    public string Glyph => "2";
    public Vector4 Accent => new(0.96f, 0.58f, 0.39f, 1f);
    public int BadgeCount => 0;

    private readonly int[] board = new int[TotalCells];
    private readonly int[] prevBoard = new int[TotalCells];
    private readonly float[] mergeAnim = new float[TotalCells];
    private readonly float[] spawnAnim = new float[TotalCells];
    private readonly int[] slideFrom = new int[TotalCells];
    private readonly int[] mergeFrom = new int[TotalCells];
    private readonly Random rng = new();

    private int score;
    private bool won;
    private bool lost;
    private bool isSliding;
    private float slideTimer;
    private int lastDirection = -1;
    private Vector2 swipeStart;
    private bool swipeActive;
    private PhoneTheme frameTheme = PhoneTheme.Default;

    public void OnOpened() => ResetGame();

    public void OnClosed() { }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        var body = GameCommon.LayoutBelowHeader(context.Content);
        AppHeader.Draw(context, DisplayName);

        using (AppSurface.Begin(body))
        {
            var dt = ImGui.GetIO().DeltaTime;
            var contentMax = new Vector2(body.Min.X, body.Min.Y) + ImGui.GetContentRegionAvail();
            var surface = new Rect(new Vector2(body.Min.X, body.Min.Y), contentMax);
            var scale = ImGuiHelpers.GlobalScale;

            UpdateAnimation(dt);

            var statsY = body.Min.Y + 26f * scale;
            GameCommon.DrawScorePill(new Vector2(surface.Center.X, statsY), "Score", score, frameTheme);

            var gridTop = statsY + 32f * scale;
            var gridArea = new Rect(new Vector2(surface.Min.X, gridTop), surface.Max);
            var grid = GameCommon.LayoutGameGrid(gridArea, GridSize, GridSize, 0.08f);

            DrawTileGrid(grid, scale);
            HandleSwipeInput(grid);

            if (won)
            {
                if (GameCommon.DrawWinOverlay(surface, frameTheme, score, 0))
                {
                    ResetGame();
                }
            }

            if (lost)
            {
                if (GameCommon.DrawGameOverOverlay(surface, frameTheme, score, "Score"))
                {
                    ResetGame();
                }
            }
        }
    }

    private void ResetGame()
    {
        Array.Clear(board, 0, TotalCells);
        Array.Clear(prevBoard, 0, TotalCells);
        Array.Clear(mergeAnim, 0, TotalCells);
        Array.Clear(spawnAnim, 0, TotalCells);
        for (var i = 0; i < TotalCells; i++)
        {
            slideFrom[i] = -1;
            mergeFrom[i] = -1;
        }

        score = 0;
        won = false;
        lost = false;
        isSliding = false;
        slideTimer = 0f;
        lastDirection = -1;
        SpawnTile();
        SpawnTile();
    }

    private void DrawTileGrid(Rect grid, float scale)
    {
        for (var row = 0; row < GridSize; row++)
        {
            for (var col = 0; col < GridSize; col++)
            {
                var (cellMin, cellMax) = GameCommon.CellBounds(grid, col, row, GridSize, GridSize, 0.08f);
                GameCommon.FillRect(cellMin, cellMax, new Vector4(0.12f, 0.14f, 0.18f, 1f), 6f * scale);
            }
        }

        for (var row = 0; row < GridSize; row++)
        {
            for (var col = 0; col < GridSize; col++)
            {
                var index = row * GridSize + col;
                var value = board[index];
                if (value == 0)
                {
                    continue;
                }

                var (cellMin, cellMax) = GameCommon.CellBounds(grid, col, row, GridSize, GridSize, 0.08f);
                var destCenter = (cellMin + cellMax) * 0.5f;
                var halfW = (cellMax.X - cellMin.X) * 0.5f;
                var halfH = (cellMax.Y - cellMin.Y) * 0.5f;
                var rounding = 6f * scale;

                var drawCenter = destCenter;
                var alpha = 1f;

                var hasSlide = slideFrom[index] >= 0;
                if (hasSlide && isSliding)
                {
                    var t = MathF.Min(1f, slideTimer / SlideDuration);
                    var fromIdx = slideFrom[index];
                    var fromRow = fromIdx / GridSize;
                    var fromCol = fromIdx % GridSize;
                    var (fromMin, fromMax) = GameCommon.CellBounds(grid, fromCol, fromRow, GridSize, GridSize, 0.08f);
                    var srcCenter = (fromMin + fromMax) * 0.5f;

                    if (mergeFrom[index] >= 0)
                    {
                        drawCenter = Vector2.Lerp(srcCenter, destCenter, t);
                        alpha = 1f - t;
                    }
                    else
                    {
                        drawCenter = Vector2.Lerp(srcCenter, destCenter, t);
                    }
                }

                var colorIdx = value == 2 ? 1 : Math.Min(11, (int)Math.Log(value, 2) - 1);
                var tileColor = TileColors[colorIdx];

                var spawn = spawnAnim[index];
                if (spawn > 0f && !isSliding)
                {
                    spawn = MathF.Max(0f, spawn - ImGui.GetIO().DeltaTime / SpawnDuration);
                    spawnAnim[index] = spawn;
                    var pop = 0.5f + 0.5f * (1f - spawn);
                    var dMin = new Vector2(drawCenter.X - halfW * pop, drawCenter.Y - halfH * pop);
                    var dMax = new Vector2(drawCenter.X + halfW * pop, drawCenter.Y + halfH * pop);
                    GameCommon.FillRect(dMin, dMax, Styling.WithAlpha(tileColor, alpha), rounding);
                }
                else
                {
                    var dMin = new Vector2(drawCenter.X - halfW, drawCenter.Y - halfH);
                    var dMax = new Vector2(drawCenter.X + halfW, drawCenter.Y + halfH);
                    GameCommon.FillRect(dMin, dMax, Styling.WithAlpha(tileColor, alpha), rounding);

                    var merge = mergeAnim[index];
                    if (merge > 0f && !isSliding)
                    {
                        var popScale = 1f + 0.12f * merge;
                        var popMin = new Vector2(destCenter.X - halfW * popScale, destCenter.Y - halfH * popScale);
                        var popMax = new Vector2(destCenter.X + halfW * popScale, destCenter.Y + halfH * popScale);
                        GameCommon.FillRect(popMin, popMax, Styling.WithAlpha(tileColor, alpha), rounding);
                    }
                }

                if (alpha > 0.3f)
                {
                    var textColor = colorIdx <= 2 ? new Vector4(0.25f, 0.25f, 0.30f, 1f) : new Vector4(0.97f, 0.97f, 0.98f, 1f);
                    var fontScale = value >= 1000 ? 1f : 1.25f;
                    Typography.DrawCentered(new Vector2(drawCenter.X, drawCenter.Y), value.ToString(), Styling.WithAlpha(textColor, alpha), fontScale);
                }
            }
        }
    }

    private void HandleSwipeInput(Rect grid)
    {
        if (isSliding || lost)
        {
            return;
        }

        var mousePos = ImGui.GetMousePos();

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && GameCommon.HitTest(grid.Min, grid.Max))
        {
            if (!swipeActive)
            {
                swipeActive = true;
                swipeStart = mousePos;
            }
        }

        if (swipeActive && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            swipeActive = false;
            var delta = mousePos - swipeStart;
            var minSwipe = 20f * ImGuiHelpers.GlobalScale;

            if (MathF.Abs(delta.X) > minSwipe || MathF.Abs(delta.Y) > minSwipe)
            {
                int dir;
                if (MathF.Abs(delta.X) > MathF.Abs(delta.Y))
                {
                    dir = delta.X > 0 ? 1 : 3;
                }
                else
                {
                    dir = delta.Y > 0 ? 2 : 0;
                }

                if (CanMove(dir))
                {
                    DoMove(dir);
                }
                else if (!HasAdjacentMerge())
                {
                    var hasEmpty = false;
                    for (var r = 0; r < GridSize && !hasEmpty; r++)
                    {
                        for (var c = 0; c < GridSize; c++)
                        {
                            if (board[r * GridSize + c] == 0)
                            {
                                hasEmpty = true;
                                break;
                            }
                        }
                    }

                    if (!hasEmpty)
                    {
                        lost = true;
                    }
                }
            }
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            swipeActive = false;
        }
    }

    private bool CanMove(int direction)
    {
        var rs = direction is 0 or 2 ? -1 : 0;
        var cs = direction is 1 or 3 ? -1 : 0;
        if (direction == 2) rs = 1;
        if (direction == 1) cs = 1;

        for (var ri = 0; ri < GridSize; ri++)
        {
            for (var ci = 0; ci < GridSize; ci++)
            {
                var r = direction is 0 or 2 ? (direction == 0 ? ri : GridSize - 1 - ri) : ri;
                var c = direction is 1 or 3 ? (direction == 3 ? ci : GridSize - 1 - ci) : ci;

                var idx = r * GridSize + c;
                if (board[idx] == 0)
                {
                    continue;
                }

                var nr = r + rs;
                var nc = c + cs;
                if (nr >= 0 && nr < GridSize && nc >= 0 && nc < GridSize)
                {
                    var nIdx = nr * GridSize + nc;
                    if (board[nIdx] == 0 || board[nIdx] == board[idx])
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void DoMove(int direction)
    {
        Array.Copy(board, prevBoard, TotalCells);
        for (var i = 0; i < TotalCells; i++)
        {
            slideFrom[i] = -1;
            mergeFrom[i] = -1;
            mergeAnim[i] = 0f;
        }

        var rs = 0;
        var cs = 0;
        if (direction == 0) rs = -1;
        if (direction == 2) rs = 1;
        if (direction == 3) cs = -1;
        if (direction == 1) cs = 1;

        var mergedThisTurn = new bool[TotalCells];

        var rowOrder = new int[GridSize];
        var colOrder = new int[GridSize];
        for (var i = 0; i < GridSize; i++)
        {
            rowOrder[i] = direction == 2 ? GridSize - 1 - i : i;
            colOrder[i] = direction == 1 ? GridSize - 1 - i : i;
        }

        for (var ri = 0; ri < GridSize; ri++)
        {
            for (var ci = 0; ci < GridSize; ci++)
            {
                var r = rowOrder[ri];
                var c = colOrder[ci];
                var idx = r * GridSize + c;

                if (board[idx] == 0)
                {
                    continue;
                }

                var cr = r;
                var cc = c;

                while (true)
                {
                    var nr = cr + rs;
                    var nc = cc + cs;
                    if (nr < 0 || nr >= GridSize || nc < 0 || nc >= GridSize)
                    {
                        break;
                    }

                    var nIdx = nr * GridSize + nc;
                    if (board[nIdx] == 0)
                    {
                        board[nIdx] = board[cr * GridSize + cc];
                        board[cr * GridSize + cc] = 0;
                        cr = nr;
                        cc = nc;
                    }
                    else if (board[nIdx] == board[cr * GridSize + cc] && !mergedThisTurn[nIdx])
                    {
                        var dstIdx = nIdx;
                        var srcIdx = cr * GridSize + cc;
                        slideFrom[srcIdx] = srcIdx;
                        mergeFrom[srcIdx] = dstIdx;
                        slideFrom[dstIdx] = srcIdx;
                        mergeAnim[dstIdx] = 1f;

                        board[dstIdx] *= 2;
                        score += board[dstIdx];
                        mergedThisTurn[dstIdx] = true;

                        if (board[dstIdx] == 2048)
                        {
                            won = true;
                        }

                        board[srcIdx] = 0;
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        for (var r = 0; r < GridSize; r++)
        {
            for (var c = 0; c < GridSize; c++)
            {
                var idx = r * GridSize + c;
                if (board[idx] != 0 && slideFrom[idx] < 0 && prevBoard[idx] != board[idx])
                {
                    var pr = r;
                    var pc = c;
                    for (var step = 1; step < GridSize; step++)
                    {
                        var sr = r - rs * step;
                        var sc = c - cs * step;
                        if (sr < 0 || sr >= GridSize || sc < 0 || sc >= GridSize)
                        {
                            break;
                        }

                        var sIdx = sr * GridSize + sc;
                        if (prevBoard[sIdx] == board[idx])
                        {
                            slideFrom[idx] = sIdx;
                            break;
                        }
                    }

                    if (slideFrom[idx] < 0)
                    {
                        slideFrom[idx] = r * GridSize + c;
                    }
                }
            }
        }

        isSliding = true;
        slideTimer = 0f;
        lastDirection = direction;

        var canMove = false;
        for (var r = 0; r < GridSize && !canMove; r++)
        {
            for (var c = 0; c < GridSize; c++)
            {
                if (board[r * GridSize + c] == 0)
                {
                    canMove = true;
                    break;
                }
            }
        }

        if (!canMove)
        {
            canMove = HasAdjacentMerge();
        }

        if (!canMove)
        {
            lost = true;
            isSliding = false;
        }
    }

    private bool HasAdjacentMerge()
    {
        for (var row = 0; row < GridSize; row++)
        {
            for (var col = 0; col < GridSize; col++)
            {
                var val = board[row * GridSize + col];
                if (col < GridSize - 1 && board[row * GridSize + col + 1] == val)
                {
                    return true;
                }

                if (row < GridSize - 1 && board[(row + 1) * GridSize + col] == val)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void SpawnTile()
    {
        var empty = 0;
        for (var i = 0; i < TotalCells; i++)
        {
            if (board[i] == 0)
            {
                empty++;
            }
        }

        if (empty == 0)
        {
            return;
        }

        var target = rng.Next(empty);
        var count = 0;
        for (var i = 0; i < TotalCells; i++)
        {
            if (board[i] == 0)
            {
                if (count == target)
                {
                    board[i] = rng.Next(10) == 0 ? 4 : 2;
                    spawnAnim[i] = 1f;
                    slideFrom[i] = -1;
                    return;
                }

                count++;
            }
        }
    }

    private void UpdateAnimation(float dt)
    {
        for (var i = 0; i < TotalCells; i++)
        {
            if (mergeAnim[i] > 0f && !isSliding)
            {
                mergeAnim[i] = MathF.Max(0f, mergeAnim[i] - dt / MergePopDuration);
            }
        }

        if (isSliding)
        {
            slideTimer += dt;
            if (slideTimer >= SlideDuration)
            {
                isSliding = false;
                slideTimer = 0f;
                for (var i = 0; i < TotalCells; i++)
                {
                    slideFrom[i] = -1;
                    mergeFrom[i] = -1;
                }

                SpawnTile();
            }
        }
    }

    public void Dispose() { }
}
