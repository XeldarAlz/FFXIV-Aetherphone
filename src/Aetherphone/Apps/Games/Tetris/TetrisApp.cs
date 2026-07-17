using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Tetris;

internal sealed class TetrisApp : IMiniGame
{
    private const string GameId = "tetris";
    private static readonly Vector4[] TetrisPalette =
    {
        new(0.40f, 0.82f, 0.98f, 1f), new(0.95f, 0.84f, 0.36f, 1f), new(0.72f, 0.52f, 0.98f, 1f),
        new(0.96f, 0.62f, 0.32f, 1f), new(0.50f, 0.86f, 0.58f, 1f), new(0.95f, 0.48f, 0.52f, 1f),
    };

    private readonly TetrisBoard board = new();
    private readonly TetrisRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private RollingValue scoreRoll;
    private int previousLevel;
    private bool started;
    private bool statsLoaded;
    private int bestScore;
    private bool wasOver;
    private bool pendingSubmit;
    private bool newBest;
    private int finalScore;
    private float resultAppear;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Tetris);
    public string Genre => Loc.T(L.Games.GenrePuzzle);
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
        scoreRoll.Snap(0);
        previousLevel = board.Level;
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

        board.Update(fx.ScaleDelta(deltaSeconds));
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

        GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
        var slotSize = 76f * scale;
        var slotTop = body.Min.Y + 14f * scale;
        var holdRect = new Rect(new Vector2(body.Min.X + 12f * scale, slotTop),
            new Vector2(body.Min.X + 12f * scale + slotSize, slotTop + slotSize));
        var nextRect = new Rect(new Vector2(body.Max.X - 12f * scale - slotSize, slotTop),
            new Vector2(body.Max.X - 12f * scale, slotTop + slotSize));
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

        renderer.DrawNextSlot(board, nextRect, theme, Accent, scale);
        var beatingBest = board.Score > 0 && board.Score > bestScore;
        GameHud.ScorePill(new Vector2(body.Center.X - 44f * scale, body.Min.Y + 32f * scale), Loc.T(L.Games.Score),
            ref scoreRoll, board.Score, Accent, theme, deltaSeconds, beatingBest);
        var bestShown = board.Score > bestScore ? board.Score : bestScore;
        GameHud.Pill(new Vector2(body.Center.X + 44f * scale, body.Min.Y + 32f * scale), Loc.T(L.Games.Best),
            GameNumber.Label(bestShown), Accent, theme, bestScore > 0 && board.Score < bestScore);
        GameHud.Pill(new Vector2(body.Center.X - 30f * scale, body.Min.Y + 82f * scale), Loc.T(L.Games.Level),
            GameNumber.Label(board.Level), Accent, theme);
        if (GameHud.RestartButton(new Vector2(body.Center.X + 52f * scale, body.Min.Y + 82f * scale), 15f * scale,
                theme))
        {
            StartGame();
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var iconColor = ImGui.GetColorU32(GamePalette.InkOn(Accent));
        var controlY = body.Max.Y - 26f * scale;
        var controlSpacing = 8f * scale;
        var controlWidth = 46f * scale;
        var controlSize = new Vector2(controlWidth, 32f * scale);
        var centerX = body.Center.X;
        var holdCenter = new Vector2(centerX - (controlWidth + controlSpacing) * 2f, controlY);
        if (GameHud.Button(holdCenter, controlSize, string.Empty, Accent, theme))
        {
            board.HoldPiece();
        }

        DrawSwapIcon(drawList, holdCenter, scale, iconColor);
        var leftCenter = new Vector2(centerX - controlWidth - controlSpacing, controlY);
        if (GameHud.Button(leftCenter, controlSize, string.Empty, Accent, theme))
        {
            board.Move(-1);
        }

        DrawMoveIcon(drawList, leftCenter, -1, scale, iconColor);
        var rotateCenter = new Vector2(centerX, controlY);
        if (GameHud.Button(rotateCenter, controlSize, string.Empty, Accent, theme))
        {
            board.Rotate(1);
        }

        DrawRotateIcon(drawList, rotateCenter, scale, iconColor);
        var rightCenter = new Vector2(centerX + controlWidth + controlSpacing, controlY);
        if (GameHud.Button(rightCenter, controlSize, string.Empty, Accent, theme))
        {
            board.Move(1);
        }

        DrawMoveIcon(drawList, rightCenter, 1, scale, iconColor);
        var dropCenter = new Vector2(centerX + (controlWidth + controlSpacing) * 2f, controlY);
        if (GameHud.Button(dropCenter, controlSize, string.Empty, Accent, theme))
        {
            HardDrop();
        }

        DrawDropIcon(drawList, dropCenter, scale, iconColor);
        if (!board.GameOver)
        {
            HandleKeyboard();
        }

        var field = new Rect(new Vector2(body.Min.X + 12f * scale, body.Min.Y + 114f * scale),
            new Vector2(body.Max.X - 12f * scale, body.Max.Y - 52f * scale));
        if (board.ClearedLinesThisFrame > 0)
        {
            var lines = board.ClearedLinesThisFrame;
            fx.AddTrauma(MathF.Min(0.45f, 0.08f * lines));
            fx.HitStop(0.03f + 0.02f * lines);
            fx.Flash(new Vector4(0.95f, 0.92f, 1f, 1f), 0.16f);
            fx.Shockwave(field.Center, field.Width * (0.3f + 0.1f * lines), GamePalette.Lighten(Accent, 0.3f), 0.5f,
                3f);
            particles.Burst(field.Center, 10 * lines, GamePalette.Lighten(Accent, 0.2f), 170f * scale, 2.8f, 0.5f,
                320f);
            particles.Streaks(field.Center, 5 * lines, new Vector4(1f, 1f, 1f, 0.8f), 380f * scale, 2.4f, 0.45f);
            if (lines >= 4)
            {
                particles.Confetti(new Vector2(field.Center.X, field.Min.Y + field.Height * 0.25f), 60, TetrisPalette,
                    280f * scale, 4f, 1.4f);
            }

            fx.AddText($"+{GameNumber.Label(board.LastLockScore)}",
                new Vector2(field.Center.X, field.Min.Y + field.Height * 0.3f), Accent, 1.2f);
        }

        if (board.Level > previousLevel && !board.GameOver)
        {
            previousLevel = board.Level;
            fx.AddText($"{Loc.T(L.Games.Level)} {GameNumber.Label(board.Level)}",
                new Vector2(field.Center.X, field.Min.Y + field.Height * 0.2f), GamePalette.Lighten(Accent, 0.3f),
                1.35f);
            fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.14f);
        }

        var shake = fx.ShakeOffset(scale);
        var shakenField = new Rect(field.Min + shake, field.Max + shake);
        var grid = GameGrid.Centered(shakenField, TetrisBoard.Columns, TetrisBoard.Rows, 0.08f);
        renderer.Draw(board, grid, Accent, scale);
        fx.DrawFlash(drawList, body, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        if (board.GameOver)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void HandleKeyboard()
    {
        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
        {
            return;
        }

        ImGui.SetNextFrameWantCaptureKeyboard(true);
        if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow))
        {
            board.Move(-1);
        }

        if (ImGui.IsKeyPressed(ImGuiKey.RightArrow))
        {
            board.Move(1);
        }

        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow, false))
        {
            board.Rotate(1);
        }

        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
        {
            board.SoftDrop();
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Space, false))
        {
            HardDrop();
        }

        if (ImGui.IsKeyPressed(ImGuiKey.C, false))
        {
            board.HoldPiece();
        }
    }

    private void HardDrop()
    {
        board.HardDrop();
        fx.AddTrauma(0.14f);
    }

    private static void DrawMoveIcon(ImDrawListPtr drawList, Vector2 center, int direction, float scale, uint color)
    {
        var size = 5f * scale;
        drawList.AddTriangleFilled(new Vector2(center.X - direction * size * 0.6f, center.Y - size),
            new Vector2(center.X - direction * size * 0.6f, center.Y + size),
            new Vector2(center.X + direction * size, center.Y), color);
    }

    private static void DrawRotateIcon(ImDrawListPtr drawList, Vector2 center, float scale, uint color)
    {
        var radius = 5.5f * scale;
        const float tipAngle = -MathF.PI * 0.35f;
        drawList.PathClear();
        drawList.PathArcTo(center, radius, tipAngle, MathF.PI * 1.15f, 24);
        drawList.PathStroke(color, ImDrawFlags.None, 1.8f * scale);
        var tip = center + new Vector2(MathF.Cos(tipAngle), MathF.Sin(tipAngle)) * radius;
        var tangent = new Vector2(MathF.Sin(tipAngle), -MathF.Cos(tipAngle));
        var normal = new Vector2(-tangent.Y, tangent.X);
        var head = 4f * scale;
        drawList.AddTriangleFilled(tip + tangent * head, tip - tangent * head * 0.2f + normal * head * 0.6f,
            tip - tangent * head * 0.2f - normal * head * 0.6f, color);
    }

    private static void DrawSwapIcon(ImDrawListPtr drawList, Vector2 center, float scale, uint color)
    {
        var width = 6f * scale;
        var offset = 3.2f * scale;
        var head = 2.6f * scale;
        var thickness = 1.8f * scale;
        var topY = center.Y - offset;
        drawList.AddLine(new Vector2(center.X - width, topY), new Vector2(center.X + width * 0.3f, topY), color,
            thickness);
        drawList.AddTriangleFilled(new Vector2(center.X + width * 0.2f, topY - head),
            new Vector2(center.X + width * 0.2f, topY + head), new Vector2(center.X + width, topY), color);
        var bottomY = center.Y + offset;
        drawList.AddLine(new Vector2(center.X + width, bottomY), new Vector2(center.X - width * 0.3f, bottomY), color,
            thickness);
        drawList.AddTriangleFilled(new Vector2(center.X - width * 0.2f, bottomY - head),
            new Vector2(center.X - width * 0.2f, bottomY + head), new Vector2(center.X - width, bottomY), color);
    }

    private static void DrawDropIcon(ImDrawListPtr drawList, Vector2 center, float scale, uint color)
    {
        drawList.AddLine(new Vector2(center.X, center.Y - 5.5f * scale), new Vector2(center.X, center.Y + 1f * scale),
            color, 1.8f * scale);
        drawList.AddTriangleFilled(new Vector2(center.X - 3.2f * scale, center.Y + 0.5f * scale),
            new Vector2(center.X + 3.2f * scale, center.Y + 0.5f * scale),
            new Vector2(center.X, center.Y + 4.5f * scale), color);
        drawList.AddRectFilled(new Vector2(center.X - 4.5f * scale, center.Y + 5.4f * scale),
            new Vector2(center.X + 4.5f * scale, center.Y + 6.8f * scale), color);
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var secondary =
            $"{Loc.T(L.Games.Lines)} {GameNumber.Label(board.Lines)}  ·  {Loc.T(L.Games.Level)} {GameNumber.Label(board.Level)}";
        var result = new GameResult(Loc.T(L.Games.GameOver), theme.Danger, Loc.T(L.Games.Score),
            GameNumber.Label(finalScore), secondary, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartGame();
        }
    }
}
