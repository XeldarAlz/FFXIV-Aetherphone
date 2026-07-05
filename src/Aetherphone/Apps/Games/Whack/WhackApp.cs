using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
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

        DrawHud(body, theme, scale);
        var area = new Rect(new Vector2(body.Min.X + 8f * scale, body.Min.Y + 60f * scale),
            new Vector2(body.Max.X - 8f * scale, body.Max.Y - 10f * scale));
        var grid = GameGrid.Centered(area, WhackBoard.Columns, WhackBoard.Rows, 0.12f);
        if (!board.Over)
        {
            HandleInput(grid, scale);
        }

        renderer.Draw(board, grid, theme, scale);
        var drawList = ImGui.GetWindowDrawList();
        fx.DrawFlash(drawList, body, 0f);
        particles.Draw(drawList, scale);
        fx.DrawText();
        if (board.Over)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void DrawHud(Rect body, PhoneTheme theme, float scale)
    {
        var rowY = body.Min.Y + 30f * scale;
        GameHud.Pill(new Vector2(body.Center.X - 50f * scale, rowY), Loc.T(L.Games.Score),
            GameNumber.Label(board.Score), Accent, theme);
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
            fx.AddText($"+{10 * board.Combo}", moleCenter, Accent, 1.1f);
            fx.AddTrauma(0.06f);
        }
        else if (result == WhackResult.Bomb)
        {
            particles.Burst(moleCenter, 24, new Vector4(0.95f, 0.4f, 0.32f, 1f), 280f * scale, 4f, 0.7f, 360f);
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
