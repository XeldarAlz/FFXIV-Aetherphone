using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Nonogram;

internal sealed class NonogramApp : IMiniGame
{
    private const string GameId = "nonogram";
    private const float FillPopSpeed = 6.5f;

    private enum PaintMode
    {
        None,
        Fill,
        Erase,
        Mark,
        Unmark,
    }

    private readonly NonogramBoard board = new();
    private readonly NonogramRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly float[] fillAnimation = new float[NonogramBoard.MaxSize * NonogramBoard.MaxSize];
    private readonly string[] difficultyLabels = new string[3];
    private int difficulty;
    private PaintMode painting;
    private float elapsed;
    private bool wasSolved;
    private bool pendingSubmit;
    private bool newBestTime;
    private int loadedBestTime;
    private float resultAppear;
    private string resultTimeText = "0:00";
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Nonogram);
    public string Genre => Loc.T(L.Games.GenreLogic);
    public void Open()
    {
        StartNewGame(difficulty);
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartNewGame(int target)
    {
        difficulty = target;
        board.Reset(SizeFor(target));
        particles.Clear();
        fx.Clear();
        Array.Clear(fillAnimation, 0, fillAnimation.Length);
        painting = PaintMode.None;
        elapsed = 0f;
        wasSolved = false;
        pendingSubmit = false;
        newBestTime = false;
        loadedBestTime = -1;
        resultAppear = 0f;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        if (loadedBestTime < 0)
        {
            loadedBestTime = context.Stats.Get(StatId(difficulty)).BestTimeSeconds;
        }

        if (!board.Solved)
        {
            elapsed += deltaSeconds;
        }

        UpdateFillAnimation(deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        if (pendingSubmit)
        {
            newBestTime = context.Stats.SubmitTime(StatId(difficulty), (int)elapsed);
            pendingSubmit = false;
        }

        DrawDifficultyRow(body, theme, scale);
        DrawStatsRow(body, theme, scale);
        var area = new Rect(new Vector2(body.Min.X + 6f * scale, body.Min.Y + 96f * scale),
            new Vector2(body.Max.X - 6f * scale, body.Max.Y - 8f * scale));
        var layout = NonogramRenderer.Layout(area, board, scale);
        var hoveredCell = ResolveHover(layout);
        if (!board.Solved)
        {
            HandleInput(hoveredCell);
        }

        renderer.Draw(board, layout, hoveredCell, fillAnimation, theme, Accent, scale);
        var drawList = ImGui.GetWindowDrawList();
        fx.DrawFlash(drawList, body, 0f);
        particles.Draw(drawList, scale);
        DetectSolved(layout);
        if (board.Solved)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void DrawDifficultyRow(Rect body, PhoneTheme theme, float scale)
    {
        difficultyLabels[0] = Loc.T(L.Games.Easy);
        difficultyLabels[1] = Loc.T(L.Games.Medium);
        difficultyLabels[2] = Loc.T(L.Games.Hard);
        var rowY = body.Min.Y + 22f * scale;
        var segmentRow = new Rect(new Vector2(body.Min.X + 4f * scale, rowY - 13f * scale),
            new Vector2(body.Max.X - 44f * scale, rowY + 13f * scale));
        var selected = SegmentStrip.Draw("nonogram.difficulty", segmentRow, difficultyLabels, difficulty, theme);
        if (selected != difficulty)
        {
            StartNewGame(selected);
            return;
        }

        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 15f * scale, theme))
        {
            StartNewGame(difficulty);
        }
    }

    private void DrawStatsRow(Rect body, PhoneTheme theme, float scale)
    {
        var rowY = body.Min.Y + 64f * scale;
        GameHud.Pill(new Vector2(body.Center.X - 50f * scale, rowY), Loc.T(L.Games.Time),
            GameNumber.Label((int)elapsed), Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 50f * scale, rowY), Loc.T(L.Games.Left),
            GameNumber.Label(board.FilledRemaining()), Accent, theme);
    }

    private int ResolveHover(NonogramLayout layout)
    {
        var mouse = ImGui.GetMousePos();
        var index = layout.HitTest(mouse, board.Size);
        if (index >= 0)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return index;
    }

    private void HandleInput(int hoveredCell)
    {
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && hoveredCell >= 0)
        {
            painting = board.MarkAt(hoveredCell) == CellMark.Filled ? PaintMode.Erase : PaintMode.Fill;
            ApplyPaint(hoveredCell, true);
        }
        else if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && hoveredCell >= 0)
        {
            painting = board.MarkAt(hoveredCell) switch
            {
                CellMark.Marked => PaintMode.Unmark,
                CellMark.Empty => PaintMode.Mark,
                _ => PaintMode.None,
            };
            ApplyPaint(hoveredCell, true);
        }

        if ((painting == PaintMode.Fill || painting == PaintMode.Erase) && ImGui.IsMouseDown(ImGuiMouseButton.Left) &&
            hoveredCell >= 0)
        {
            ApplyPaint(hoveredCell, false);
        }
        else if ((painting == PaintMode.Mark || painting == PaintMode.Unmark) &&
                 ImGui.IsMouseDown(ImGuiMouseButton.Right) && hoveredCell >= 0)
        {
            ApplyPaint(hoveredCell, false);
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            painting = PaintMode.None;
        }
    }

    private void ApplyPaint(int cell, bool initial)
    {
        var current = board.MarkAt(cell);
        switch (painting)
        {
            case PaintMode.Fill:
                if (current == CellMark.Empty || (initial && current == CellMark.Marked))
                {
                    if (board.SetMark(cell, CellMark.Filled))
                    {
                        fillAnimation[cell] = 1f;
                    }
                }

                break;
            case PaintMode.Erase:
                if (current == CellMark.Filled)
                {
                    board.SetMark(cell, CellMark.Empty);
                }

                break;
            case PaintMode.Mark:
                if (current == CellMark.Empty)
                {
                    board.SetMark(cell, CellMark.Marked);
                }

                break;
            case PaintMode.Unmark:
                if (current == CellMark.Marked)
                {
                    board.SetMark(cell, CellMark.Empty);
                }

                break;
        }
    }

    private void DetectSolved(NonogramLayout layout)
    {
        if (!board.Solved || wasSolved)
        {
            return;
        }

        wasSolved = true;
        painting = PaintMode.None;
        resultAppear = 0f;
        pendingSubmit = true;
        var seconds = (int)elapsed;
        resultTimeText = $"{seconds / 60}:{seconds % 60:D2}";
        fx.AddTrauma(0.35f);
        fx.Flash(Accent, 0.4f);
        ReadOnlySpan<Vector4> palette = new[] { Accent, Styling.AccentMint, Styling.AccentAmber, Styling.AccentPink, };
        var gridTop = layout.GridOrigin;
        var gridCenterX = gridTop.X + board.Size * layout.CellSize * 0.5f;
        particles.Confetti(new Vector2(gridCenterX, gridTop.Y), 72, palette, 260f * ImGuiHelpers.GlobalScale, 4f, 1.3f);
    }

    private void UpdateFillAnimation(float deltaSeconds)
    {
        for (var index = 0; index < fillAnimation.Length; index++)
        {
            if (fillAnimation[index] > 0f)
            {
                fillAnimation[index] = MathF.Max(0f, fillAnimation[index] - deltaSeconds * FillPopSpeed);
            }
        }
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        string? secondary = null;
        if (loadedBestTime > 0)
        {
            secondary = $"{Loc.T(L.Games.Best)} {loadedBestTime / 60}:{loadedBestTime % 60:D2}";
        }

        var result = new GameResult(Loc.T(L.Games.YouWin), Accent, Loc.T(L.Games.Time), resultTimeText, secondary,
            newBestTime);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame(difficulty);
        }
    }

    private static int SizeFor(int difficulty)
    {
        return difficulty switch
        {
            1 => 8,
            2 => 10,
            _ => 5,
        };
    }

    private static string StatId(int difficulty)
    {
        return difficulty switch
        {
            1 => "nonogram.medium",
            2 => "nonogram.hard",
            _ => "nonogram.easy",
        };
    }
}
