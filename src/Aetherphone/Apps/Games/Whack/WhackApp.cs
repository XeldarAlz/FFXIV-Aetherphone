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

namespace Aetherphone.Apps.Games.Whack;

internal sealed class WhackApp : IMiniGame
{
    private const string GameId = "whack";
    private readonly WhackBoard board = new();
    private readonly WhackRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private RollingValue scoreRoll;
    private bool statsLoaded;
    private int bestScore;
    private bool wasOver;
    private bool pendingSubmit;
    private bool newBest;
    private int finalScore;
    private float resultAppear;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Whack);
    public string Genre => Loc.T(L.Games.GenreArcade);
    public void Open()
    {
        statsLoaded = false;
        StartGame();
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartGame()
    {
        board.Reset();
        particles.Clear();
        fx.Clear();
        scoreRoll.Snap(0);
        wasOver = false;
        pendingSubmit = false;
        newBest = false;
        resultAppear = 0f;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        if (!statsLoaded)
        {
            bestScore = context.Stats.Get(GameId).BestScore;
            statsLoaded = true;
        }

        board.Step(deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        if (board.Over && !wasOver)
        {
            wasOver = true;
            finalScore = board.Score;
            pendingSubmit = true;
            resultAppear = 0f;
        }

        if (pendingSubmit)
        {
            newBest = context.Stats.SubmitScore(GameId, finalScore);
            pendingSubmit = false;
            if (newBest)
            {
                fx.Flash(Accent, 0.3f);
            }
        }

        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        DrawHud(body, theme, scale, deltaSeconds);
        var shake = fx.ShakeOffset(scale);
        var area = new Rect(new Vector2(body.Min.X + 8f * scale, body.Min.Y + 60f * scale) + shake,
            new Vector2(body.Max.X - 8f * scale, body.Max.Y - 10f * scale) + shake);
        var grid = GameGrid.Centered(area, WhackBoard.Columns, WhackBoard.Rows, 0.12f);
        if (!board.Over)
        {
            HandleInput(grid, scale);
        }

        renderer.Draw(board, grid, theme, scale);
        fx.DrawFlash(drawList, body, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        if (!board.Over && board.TimeLeft <= 10f)
        {
            DrawUrgency(drawList, body, scale);
        }

        if (board.Over)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private static void DrawUrgency(ImDrawListPtr drawList, Rect body, float scale)
    {
        var pulse = 0.10f + 0.14f * Styling.Pulse(Styling.PulseFast);
        var color = ImGui.GetColorU32(new Vector4(0.95f, 0.30f, 0.30f, pulse));
        drawList.AddRect(body.Min + new Vector2(2f * scale, 2f * scale), body.Max - new Vector2(2f * scale, 2f * scale),
            color, 14f * scale, ImDrawFlags.RoundCornersAll, 5f * scale);
    }

    private void DrawHud(Rect body, PhoneTheme theme, float scale, float deltaSeconds)
    {
        var rowY = body.Min.Y + 30f * scale;
        var beatingBest = board.Score > 0 && board.Score > bestScore;
        GameHud.ScorePill(new Vector2(body.Center.X - 50f * scale, rowY), Loc.T(L.Games.Score), ref scoreRoll,
            board.Score, Accent, theme, deltaSeconds, beatingBest);
        var low = board.TimeLeft <= 10f;
        var timeAccent = low ? theme.Danger : Accent;
        GameHud.Pill(new Vector2(body.Center.X + 50f * scale, rowY), Loc.T(L.Games.Time),
            GameNumber.Label((int)MathF.Ceiling(board.TimeLeft)), timeAccent, theme, low);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            StartGame();
        }
    }

    private void HandleInput(GameGrid grid, float scale)
    {
        var hole = HoleHit(grid);
        if (hole < 0)
        {
            return;
        }

        if (board.KindAt(hole) != Occupant.None && board.HeightAt(hole) > 0.3f)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var moleCenter = grid.CellCenter(hole % WhackBoard.Columns, hole / WhackBoard.Columns) +
                         new Vector2(0f, -grid.Pitch * 0.12f);
        var result = board.Whack(hole);
        if (result == WhackResult.Mole)
        {
            particles.Burst(moleCenter, 12, new Vector4(0.95f, 0.82f, 0.45f, 1f), 170f * scale, 3f, 0.5f, 320f);
            particles.Sparkle(moleCenter, 7, new Vector4(1f, 0.95f, 0.65f, 1f), 130f * scale, 2.4f, 0.6f);
            fx.Shockwave(moleCenter, 44f * scale, new Vector4(1f, 0.9f, 0.5f, 0.9f), 0.38f, 2.6f);
            fx.AddText($"+{10 * board.Combo}", moleCenter, Accent, 1.1f);
            fx.AddTrauma(0.08f);
        }
        else if (result == WhackResult.Bomb)
        {
            particles.Burst(moleCenter, 24, new Vector4(0.95f, 0.4f, 0.32f, 1f), 280f * scale, 4f, 0.7f, 360f);
            particles.Streaks(moleCenter, 12, new Vector4(1f, 0.7f, 0.4f, 1f), 420f * scale, 2.6f, 0.5f);
            fx.Shockwave(moleCenter, 100f * scale, new Vector4(1f, 0.6f, 0.35f, 1f), 0.55f, 3.4f);
            fx.AddText("-30", moleCenter, new Vector4(0.95f, 0.4f, 0.4f, 1f), 1.2f);
            fx.AddTrauma(0.6f);
            fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.4f);
        }
    }

    private int HoleHit(GameGrid grid)
    {
        var mouse = ImGui.GetMousePos();
        if (!grid.Bounds.Contains(mouse))
        {
            return -1;
        }

        var local = mouse - grid.Origin;
        var column = (int)(local.X / grid.Pitch);
        var row = (int)(local.Y / grid.Pitch);
        if (column < 0 || column >= WhackBoard.Columns || row < 0 || row >= WhackBoard.Rows)
        {
            return -1;
        }

        return row * WhackBoard.Columns + column;
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        string? secondary = null;
        if (bestScore > 0)
        {
            secondary = $"{Loc.T(L.Games.Best)} {GameNumber.Label(bestScore)}";
        }

        var result = new GameResult(Loc.T(L.Games.GameOver), Accent, Loc.T(L.Games.Score), GameNumber.Label(finalScore),
            secondary, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartGame();
        }
    }
}
