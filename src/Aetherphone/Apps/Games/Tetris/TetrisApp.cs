using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Tetris;

internal sealed class TetrisApp : IMiniGame
{
    private const string GameId = "tetris";

    private readonly TetrisBoard board = new();

    private readonly TetrisRenderer renderer = new();

    private readonly ParticleSystem particles = new();

    private readonly FeedbackFx fx = new();

    private bool started;

    private bool statsLoaded;

    private int bestScore;

    private bool wasOver;

    private bool pendingSubmit;

    private bool newBest;

    private int finalScore;

    private float resultAppear;

    private int levelTextLevel;

    private string levelText = string.Empty;

    public string Id => GameId;

    public string Title => Loc.T(L.Games.Tetris);

    public string Genre => Loc.T(L.Games.GenrePuzzle);

    public Vector4 Accent => new(0.52f, 0.78f, 0.98f, 1f);

    public void Open()
    {
        started = false;
        statsLoaded = false;
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
        wasOver = false;
        pendingSubmit = false;
        newBest = false;
        resultAppear = 0f;
        started = true;
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

        if (!started)
        {
            StartGame();
        }

        if (pendingSubmit)
        {
            newBest = context.Stats.SubmitScore(GameId, finalScore);
            if (newBest)
            {
                bestScore = finalScore;
            }

            pendingSubmit = false;
        }

        board.Update(deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);

        if (board.GameOver && !wasOver)
        {
            wasOver = true;
            finalScore = board.Score;
            pendingSubmit = true;
            resultAppear = 0f;
            fx.AddTrauma(0.6f);
            fx.Flash(new Vector4(0.95f, 0.34f, 0.34f, 1f), 0.35f);
        }

        var rowY = body.Min.Y + 30f * scale;
        GameHud.Pill(new Vector2(body.Center.X - 62f * scale, rowY), Loc.T(L.Games.Score), GameNumber.Label(board.Score), Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 26f * scale, rowY), Loc.T(L.Games.Best), GameNumber.Label(bestScore), Accent, theme, bestScore > 0 && board.Score < bestScore);

        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            StartGame();
            return;
        }

        var holdRect = new Rect(new Vector2(body.Max.X - 96f * scale, body.Min.Y + 74f * scale), new Vector2(body.Max.X - 12f * scale, body.Min.Y + 168f * scale));
        var holdHovered = ImGui.IsMouseHoveringRect(holdRect.Min, holdRect.Max);
        renderer.DrawHoldSlot(board, holdRect, theme, Accent, holdHovered, scale);
        if (holdHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                board.HoldPiece();
            }
        }

        var nextRect = new Rect(new Vector2(body.Max.X - 96f * scale, body.Min.Y + 174f * scale), new Vector2(body.Max.X - 12f * scale, body.Min.Y + 268f * scale));
        renderer.DrawNextSlot(board, nextRect, theme, Accent, scale);

        var controlY = body.Max.Y - 26f * scale;
        var controlSpacing = 8f * scale;
        var controlWidth = 46f * scale;
        var centerX = body.Center.X;
        if (GameHud.Button(new Vector2(centerX - (controlWidth + controlSpacing) * 2f, controlY), new Vector2(controlWidth, 32f * scale), "S", Accent, theme))
        {
            board.HoldPiece();
        }

        if (GameHud.Button(new Vector2(centerX - controlWidth - controlSpacing, controlY), new Vector2(controlWidth, 32f * scale), "<", Accent, theme))
        {
            board.Move(-1);
        }

        if (GameHud.Button(new Vector2(centerX, controlY), new Vector2(controlWidth, 32f * scale), "R", Accent, theme))
        {
            board.Rotate(1);
        }

        if (GameHud.Button(new Vector2(centerX + controlWidth + controlSpacing, controlY), new Vector2(controlWidth, 32f * scale), ">", Accent, theme))
        {
            board.Move(1);
        }

        if (GameHud.Button(new Vector2(centerX + (controlWidth + controlSpacing) * 2f, controlY), new Vector2(controlWidth, 32f * scale), "D", Accent, theme))
        {
            board.HardDrop();
        }

        var field = new Rect(new Vector2(body.Min.X + 10f * scale, body.Min.Y + 72f * scale), new Vector2(body.Max.X - 108f * scale, body.Max.Y - 52f * scale));

        if (board.ClearedLinesThisFrame > 0)
        {
            fx.AddTrauma(MathF.Min(0.28f, 0.06f * board.ClearedLinesThisFrame));
            fx.Flash(new Vector4(0.95f, 0.92f, 1f, 1f), 0.16f);
            particles.Burst(field.Center, 10 * board.ClearedLinesThisFrame, GamePalette.Lighten(Accent, 0.2f), 170f * scale, 2.8f, 0.5f, 320f);
            fx.AddText($"+{GameNumber.Label(board.LastLockScore)}", new Vector2(field.Center.X, field.Min.Y + field.Height * 0.3f), Accent, 1.2f);
        }

        if (levelTextLevel != board.Level)
        {
            levelTextLevel = board.Level;
            levelText = $"{Loc.T(L.Games.Level)} {GameNumber.Label(board.Level)}";
        }

        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 64f * scale), levelText, theme.TextMuted, TextStyles.Caption1);

        var shake = fx.ShakeOffset(scale);
        var shakenField = new Rect(field.Min + shake, field.Max + shake);
        var grid = GameGrid.Centered(shakenField, TetrisBoard.Columns, TetrisBoard.Rows, 0.08f);

        renderer.Draw(board, grid, Accent, scale);

        var drawList = ImGui.GetWindowDrawList();
        fx.DrawFlash(drawList, body, 0f);
        particles.Draw(drawList, scale);
        fx.DrawText();

        if (board.GameOver)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);

        var secondary = $"{Loc.T(L.Games.Lines)} {GameNumber.Label(board.Lines)}  ·  {Loc.T(L.Games.Level)} {GameNumber.Label(board.Level)}";
        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score), GameNumber.Label(finalScore), secondary, newBest);

        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartGame();
        }
    }
}
