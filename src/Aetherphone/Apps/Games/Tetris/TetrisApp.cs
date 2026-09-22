using Aetherphone.Core.Animation;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Tetris;

internal sealed class TetrisApp : IMiniGame
{
    private const string GameId = "tetris";
    private static readonly Vector4[] TetrisPalette =
    {
        new(0.40f, 0.82f, 0.98f, 1f), new(0.95f, 0.84f, 0.36f, 1f), new(0.72f, 0.52f, 0.98f, 1f),
        new(0.96f, 0.62f, 0.32f, 1f), new(0.50f, 0.86f, 0.58f, 1f), new(0.95f, 0.48f, 0.52f, 1f),
    };

    internal const string ModernStatId = "tetris.modern";
    private const float RulesetStripHeight = 26f;
    private const string HoldKeyLabel = "C";
    private const string LeftKeyLabel = "A";
    private const string RotateKeyLabel = "W";
    private const string RightKeyLabel = "D";
    private const string DropKeyLabel = "Space";
    private readonly TetrisBoard board = new();
    private readonly TetrisRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly string[] rulesetLabels = new string[2];
    private TetrisRuleset ruleset;
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
    public bool RunsOnAClock => true;

    public GameGenre Genre => GameGenre.Puzzle;
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

    private string StatId => ruleset == TetrisRuleset.Modern ? ModernStatId : GameId;

    private void StartGame()
    {
        board.Reset(ruleset);
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
        var scale = UiScale.Current;
        var theme = context.Theme;
        var body = context.Body;
        if (!statsLoaded)
        {
            ruleset = context.Stats.TetrisModern ? TetrisRuleset.Modern : TetrisRuleset.Classic;
            bestScore = context.Stats.Get(StatId).BestScore;
            statsLoaded = true;
        }

        if (!started)
        {
            StartGame();
        }

        if (pendingSubmit)
        {
            newBest = context.Stats.SubmitScore(StatId, finalScore);
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
        var outerMargin = 12f * scale;
        var slotTop = body.Min.Y + 14f * scale;
        var holdRect = new Rect(new Vector2(body.Min.X + outerMargin, slotTop),
            new Vector2(body.Min.X + outerMargin + slotSize, slotTop + slotSize));
        var nextRect = new Rect(new Vector2(body.Max.X - outerMargin - slotSize, slotTop),
            new Vector2(body.Max.X - outerMargin, slotTop + slotSize));
        var holdHovered = UiInteract.Hover(holdRect.Min, holdRect.Max);
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

        var restartDiameter = 30f * scale;
        var restartRadius = MathF.Max(10f * scale,
            MathF.Min(restartDiameter * 0.5f, (nextRect.Min.X - holdRect.Max.X) * 0.5f - 6f * scale));
        var restartCenter = new Vector2((holdRect.Max.X + nextRect.Min.X) * 0.5f, slotTop + slotSize - restartRadius);
        if (GameHud.RestartButton(restartCenter, restartRadius, theme))
        {
            StartGame();
            return;
        }

        var beatingBest = board.Score > 0 && board.Score > bestScore;
        var bestShown = board.Score > bestScore ? board.Score : bestScore;
        var scoreLabel = Loc.T(L.Games.Score);
        var scoreText = GameNumber.Label(board.Score);
        var bestLabel = Loc.T(L.Games.Best);
        var bestText = GameNumber.Label(bestShown);
        var levelLabel = Loc.T(L.Games.Level);
        var levelText = GameNumber.Label(board.Level);
        var itemGap = 10f * scale;
        var rowAvailableWidth = MathF.Max(1f, body.Width - 2f * outerMargin);
        var pillScale = 1f;
        var naturalWidth = GameHud.PillWidth(levelLabel, levelText) + GameHud.PillWidth(scoreLabel, scoreText)
            + GameHud.PillWidth(bestLabel, bestText) + itemGap * 2f;
        if (naturalWidth > rowAvailableWidth)
        {
            pillScale = Math.Clamp(rowAvailableWidth / naturalWidth, 0.6f, 1f);
        }

        var levelWidth = GameHud.PillWidth(levelLabel, levelText, pillScale);
        var scoreWidth = GameHud.PillWidth(scoreLabel, scoreText, pillScale);
        var bestWidth = GameHud.PillWidth(bestLabel, bestText, pillScale);
        var rowWidth = levelWidth + scoreWidth + bestWidth + itemGap * 2f;
        var rowY = slotTop + slotSize + 26f * scale;
        var cursorX = body.Center.X - rowWidth * 0.5f;
        var levelX = cursorX + levelWidth * 0.5f;
        cursorX += levelWidth + itemGap;
        var scoreX = cursorX + scoreWidth * 0.5f;
        cursorX += scoreWidth + itemGap;
        var bestX = cursorX + bestWidth * 0.5f;

        GameHud.Pill(new Vector2(levelX, rowY), levelLabel, levelText, Accent, theme, sizeScale: pillScale);
        GameHud.ScorePill(new Vector2(scoreX, rowY), scoreLabel, ref scoreRoll, board.Score, Accent, theme,
            deltaSeconds, beatingBest, pillScale);
        GameHud.Pill(new Vector2(bestX, rowY), bestLabel, bestText, Accent, theme,
            bestScore > 0 && board.Score < bestScore, pillScale);

        var hudBottom = rowY + GameHud.PillHeight * scale * pillScale * 0.5f;
        rulesetLabels[0] = Loc.T(L.Games.Classic);
        rulesetLabels[1] = Loc.T(L.Games.Modern);
        var stripTop = hudBottom + 8f * scale;
        var stripRow = new Rect(new Vector2(body.Min.X + 48f * scale, stripTop),
            new Vector2(body.Max.X - 48f * scale, stripTop + RulesetStripHeight * scale));
        var selectedRuleset = SegmentStrip.Draw("tetris.ruleset", stripRow, rulesetLabels, (int)ruleset, theme);
        if (selectedRuleset != (int)ruleset)
        {
            ruleset = (TetrisRuleset)selectedRuleset;
            context.Stats.TetrisModern = ruleset == TetrisRuleset.Modern;
            bestScore = context.Stats.Get(StatId).BestScore;
            StartGame();
            return;
        }

        hudBottom = stripRow.Max.Y - 12f * scale;

        var drawList = ImGui.GetWindowDrawList();
        var iconColor = GamePalette.InkOn(Accent);
        var controlY = body.Max.Y - 26f * scale;
        var controlMargin = 12f * scale;
        var controlAvailableWidth = body.Width - controlMargin * 2f;
        var controlSpacing = MathF.Min(8f * scale, controlAvailableWidth * 0.02f);
        var controlWidth = MathF.Min(46f * scale, MathF.Max(28f * scale, (controlAvailableWidth - controlSpacing * 4f) / 5f));
        var controlSize = new Vector2(controlWidth, 32f * scale);
        var centerX = body.Center.X;
        var holdCenter = new Vector2(centerX - (controlWidth + controlSpacing) * 2f, controlY);
        if (GameHud.Button(holdCenter, controlSize, string.Empty, Accent, theme))
        {
            board.HoldPiece();
        }

        DrawKeyLabel(drawList, holdCenter, HoldKeyLabel, controlWidth, iconColor);
        var leftCenter = new Vector2(centerX - controlWidth - controlSpacing, controlY);
        if (GameHud.Button(leftCenter, controlSize, string.Empty, Accent, theme))
        {
            board.Move(-1);
        }

        DrawKeyLabel(drawList, leftCenter, LeftKeyLabel, controlWidth, iconColor);
        var rotateCenter = new Vector2(centerX, controlY);
        if (GameHud.Button(rotateCenter, controlSize, string.Empty, Accent, theme))
        {
            board.Rotate(1);
        }

        DrawKeyLabel(drawList, rotateCenter, RotateKeyLabel, controlWidth, iconColor);
        var rightCenter = new Vector2(centerX + controlWidth + controlSpacing, controlY);
        if (GameHud.Button(rightCenter, controlSize, string.Empty, Accent, theme))
        {
            board.Move(1);
        }

        DrawKeyLabel(drawList, rightCenter, RightKeyLabel, controlWidth, iconColor);
        var dropCenter = new Vector2(centerX + (controlWidth + controlSpacing) * 2f, controlY);
        if (GameHud.Button(dropCenter, controlSize, string.Empty, Accent, theme))
        {
            HardDrop();
        }

        DrawKeyLabel(drawList, dropCenter, DropKeyLabel, controlWidth, iconColor);
        if (!board.GameOver)
        {
            HandleKeyboard();
        }

        var field = new Rect(new Vector2(body.Min.X + 12f * scale, hudBottom + 24f * scale),
            new Vector2(body.Max.X - 12f * scale, body.Max.Y - 52f * scale));
        if (board.ClearedLinesThisFrame > 0)
        {
            var lines = board.ClearedLinesThisFrame;
            UiFeedback.Play(lines >= 4 ? UiSound.GamePowerUp : UiSound.GameClear);
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

        if (board.LockedThisFrame && ruleset == TetrisRuleset.Modern)
        {
            AnnounceModernLock(field, scale);
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
        if (!GameInput.Claim())
        {
            return;
        }

        if (GameInput.Pressed(ImGuiKey.LeftArrow, ImGuiKey.A, true))
        {
            board.Move(-1);
        }

        if (GameInput.Pressed(ImGuiKey.RightArrow, ImGuiKey.D, true))
        {
            board.Move(1);
        }

        if (GameInput.Pressed(ImGuiKey.UpArrow, ImGuiKey.W))
        {
            board.Rotate(1);
        }

        if (GameInput.Pressed(ImGuiKey.DownArrow, ImGuiKey.S, true))
        {
            board.SoftDrop();
        }

        if (GameInput.Pressed(ImGuiKey.Z))
        {
            board.Rotate(-1);
        }

        if (GameInput.Pressed(ImGuiKey.X))
        {
            board.Rotate(1);
        }

        if (GameInput.Pressed(ImGuiKey.Space))
        {
            HardDrop();
        }

        if (GameInput.Pressed(ImGuiKey.C))
        {
            board.HoldPiece();
        }
    }

    private void HardDrop()
    {
        board.HardDrop();
        UiFeedback.Play(UiSound.GameHitWood);
        fx.AddTrauma(0.14f);
    }

    private void AnnounceModernLock(Rect field, float scale)
    {
        var position = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.42f);
        var lines = board.ClearedLinesThisFrame;
        if (board.LastSpin != TetrisSpin.None)
        {
            var spinLabel = board.LastSpin == TetrisSpin.Mini ? Loc.T(L.Games.TSpinMini) : Loc.T(L.Games.TSpin);
            var text = board.LastBackToBack ? $"{Loc.T(L.Games.BackToBack)} {spinLabel}" : spinLabel;
            fx.AddText(text, position, GamePalette.Lighten(Accent, 0.35f), 1.3f);
            fx.Shockwave(position, field.Width * 0.5f, GamePalette.Lighten(Accent, 0.4f), 0.5f, 3f);
            particles.Sparkle(position, 14, new Vector4(1f, 1f, 1f, 0.9f), 160f * scale, 2.6f, 0.7f);
            return;
        }

        if (lines >= 4 && board.LastBackToBack)
        {
            fx.AddText(Loc.T(L.Games.BackToBack), position, GamePalette.Lighten(Accent, 0.35f), 1.3f);
            return;
        }

        if (lines > 0 && board.LastCombo >= 1)
        {
            fx.AddText($"x{GameNumber.Label(board.LastCombo + 1)} {Loc.T(L.Games.Combo)}", position,
                GamePalette.Lighten(Accent, 0.3f), 1.1f);
        }
    }

    private static void DrawKeyLabel(ImDrawListPtr drawList, Vector2 center, string label, float buttonWidth,
        Vector4 color)
    {
        var maxWidth = buttonWidth - 8f * UiScale.Current;
        var style = TextStyles.FootnoteEmphasized;
        if (Typography.Measure(label, style).X > maxWidth)
        {
            style = TextStyles.Caption2;
        }

        Typography.DrawCentered(drawList, center, label, color, style);
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
