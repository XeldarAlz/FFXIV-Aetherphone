using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Tetris;

internal sealed class TetrisApp : IMiniGame
{
    internal const string ModernStatId = "tetris.modern";
    private const string GameId = "tetris";
    private const string GestureSurfaceId = "tetris.well";
    private const int ModernMode = 1;
    private const int DangerRows = 4;
    private const int MaxComboLabel = 30;
    private const int PunchLines = 2;
    private const int TetrisLines = 4;
    private const float TapSeconds = 0.25f;
    private const float LongPressSeconds = 0.45f;
    private const float DeadZoneFraction = 0.35f;
    private const float FlickMinPitches = 1.2f;
    private const float FlickPitchesPerSecond = 9f;
    private const float TetrisSlowMoFactor = 0.5f;
    private const float TetrisSlowMoSeconds = 0.25f;
    private const float ComboGlowPerLink = 1f / 6f;
    private static readonly LocString[] Modes = { L.Games.Classic, L.Games.Modern };
    private static readonly string[] ModeStatIds = { GameId, ModernStatId };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Tetris, GameGenre.Puzzle, L.Tetris.Hook,
        Backdrop.Neon, HudStyle.Compact, ScoreKind.Score, Modes, ModeStatIds, clocked: true, keyboard: true);
    private static readonly Dictionary<int, string> GainLabels = new();
    private static readonly Vector4[] TetrisPalette =
    {
        new(0.40f, 0.82f, 0.98f, 1f), new(0.95f, 0.84f, 0.36f, 1f), new(0.72f, 0.52f, 0.98f, 1f),
        new(0.96f, 0.62f, 0.32f, 1f), new(0.50f, 0.86f, 0.58f, 1f), new(0.95f, 0.48f, 0.52f, 1f),
    };
    private static readonly Vector4 Danger = new(0.95f, 0.34f, 0.34f, 1f);
    private static readonly Vector4 ClearFlashColor = new(0.95f, 0.92f, 1f, 1f);
    private static readonly Vector4 StreakInk = new(1f, 1f, 1f, 0.8f);
    private static readonly Vector4 SparkleInk = new(1f, 1f, 1f, 0.9f);

    private readonly TetrisBoard board = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly string?[] comboLabels = new string?[MaxComboLabel + 1];
    private LabelSlot levelLabel;
    private LanguageInfo? labelLanguage;
    private string backToBackSpin = string.Empty;
    private string backToBackMini = string.Empty;
    private string linesValue = string.Empty;
    private int linesShown = -1;
    private GameGrid grid;
    private int previousLevel;
    private int clearedMask;
    private float clearFlash;
    private bool finished;
    private Vector2 gestureStart;
    private float gestureLastY;
    private float gestureSeconds;
    private int dragColumns;
    private int dragRows;
    private bool gestureActive;
    private bool gestureMoved;
    private bool gestureConsumed;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Mode == ModernMode ? TetrisRuleset.Modern : TetrisRuleset.Classic, start.Random);
        particles.Clear();
        fx.Clear();
        previousLevel = board.Level;
        clearedMask = 0;
        clearFlash = 0f;
        finished = false;
        gestureActive = false;
        gestureConsumed = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        gestureActive = false;
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        var scale = UiScale.Current;
        Layout(context.Safe, scale);
        SyncLabels();
        TetrisRenderer.DrawField(ImGui.GetWindowDrawList(), board, grid, Accent, context.Backdrop.Ink,
            levelLabel.Get(L.Stage.LevelShort, board.Level), Loc.T(L.Games.Lines), linesValue, 0, 0f, false, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        clearFlash = MathF.Max(0f, clearFlash - context.RawDeltaSeconds);
        Layout(Grow(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale)), scale);
        var playing = context.Session.State == StageFlow.Playing && !finished;
        if (!finished)
        {
            Step(simDelta, playing, context, scale);
        }

        SyncLabels();
        var showActive = board.HasActivePiece && !board.GameOver;
        TetrisRenderer.DrawField(drawList, board, grid, Accent, context.Backdrop.Ink,
            levelLabel.Get(L.Stage.LevelShort, board.Level), Loc.T(L.Games.Lines), linesValue, clearedMask,
            clearFlash / TetrisRenderer.ClearFlashSeconds, showActive, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        DrawHud(context, drawList, playing, scale);
        context.Session.Report(board.Score);
    }

    private void Layout(Rect area, float scale)
    {
        var inner = area.Inset(BoardPlate.Padding * scale);
        var well = new Rect(new Vector2(inner.Min.X, inner.Min.Y + TetrisRenderer.BandHeight * scale), inner.Max);
        grid = GameGrid.Centered(well, TetrisBoard.Columns, TetrisBoard.Rows, TetrisRenderer.GapFraction);
    }

    private void Step(float deltaSeconds, bool playing, in GameContext context, float scale)
    {
        board.Update(deltaSeconds);
        if (playing)
        {
            HandleKeyboard(scale);
            HandleGestures(context.RawDeltaSeconds, scale);
        }

        if (board.LockedThisFrame)
        {
            OnLock(context, scale);
        }

        if (board.GameOver)
        {
            OnTopOut(context);
            return;
        }

        if (board.Level > previousLevel)
        {
            OnLevelUp(context);
        }

        if (StackIsHigh())
        {
            context.Fx.Vignette(Danger, 0.10f + 0.10f * Pulse.Wave(Pulse.Fast), 0.3f);
        }
    }

    private void HandleKeyboard(float scale)
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

        if (GameInput.Pressed(ImGuiKey.UpArrow, ImGuiKey.W) || GameInput.Pressed(ImGuiKey.X))
        {
            board.Rotate(1);
        }

        if (GameInput.Pressed(ImGuiKey.Z))
        {
            board.Rotate(-1);
        }

        if (GameInput.Pressed(ImGuiKey.DownArrow, ImGuiKey.S, true))
        {
            board.SoftDrop();
        }

        if (GameInput.Pressed(ImGuiKey.Space))
        {
            HardDrop(scale);
        }

        if (GameInput.Pressed(ImGuiKey.C))
        {
            Hold();
        }
    }

    private void HandleGestures(float rawDelta, float scale)
    {
        var area = grid.Bounds;
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(area.Min);
        ImGui.InvisibleButton(GestureSurfaceId, area.Size);
        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem) &&
                      UiInteract.Hover(area.Min, area.Max);
        var activated = hovered && ImGui.IsItemActivated();
        if (hovered)
        {
            UiInteract.ReportGestureSurface();
        }

        ImGui.SetCursorScreenPos(cursor);
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            Hold();
        }

        var mouse = ImGui.GetMousePos();
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            ReleaseGesture();
            return;
        }

        if (!gestureActive)
        {
            if (activated)
            {
                BeginGesture(mouse);
            }

            return;
        }

        AdvanceGesture(mouse, rawDelta, scale);
    }

    private void BeginGesture(Vector2 mouse)
    {
        gestureActive = true;
        gestureStart = mouse;
        gestureLastY = mouse.Y;
        gestureSeconds = 0f;
        dragColumns = 0;
        dragRows = 0;
        gestureMoved = false;
        gestureConsumed = false;
    }

    private void ReleaseGesture()
    {
        if (!gestureActive)
        {
            return;
        }

        gestureActive = false;
        if (gestureConsumed || gestureMoved || gestureSeconds >= TapSeconds)
        {
            return;
        }

        if (board.Rotate(1))
        {
            UiFeedback.Play(UiSound.GameTick);
        }
    }

    private void AdvanceGesture(Vector2 mouse, float rawDelta, float scale)
    {
        gestureSeconds += rawDelta;
        var verticalSpeed = (mouse.Y - gestureLastY) / MathF.Max(rawDelta, 0.0001f);
        gestureLastY = mouse.Y;
        if (gestureConsumed)
        {
            return;
        }

        var delta = mouse - gestureStart;
        var pitch = grid.Pitch;
        var deadZone = pitch * DeadZoneFraction;
        var targetColumns = StepsFor(delta.X, pitch, deadZone);
        while (dragColumns != targetColumns)
        {
            var step = Math.Sign(targetColumns - dragColumns);
            if (!board.Move(step))
            {
                dragColumns = targetColumns;
                break;
            }

            dragColumns += step;
            gestureMoved = true;
        }

        if (delta.Y > FlickMinPitches * pitch && delta.Y > MathF.Abs(delta.X) &&
            verticalSpeed > FlickPitchesPerSecond * pitch)
        {
            HardDrop(scale);
            gestureMoved = true;
            gestureConsumed = true;
            return;
        }

        var targetRows = delta.Y > 0f ? StepsFor(delta.Y, pitch, deadZone) : 0;
        while (dragRows < targetRows)
        {
            if (!board.SoftDrop() || board.LockedThisFrame)
            {
                dragRows = targetRows;
                gestureConsumed = board.LockedThisFrame;
                break;
            }

            dragRows++;
            gestureMoved = true;
        }

        if (!gestureMoved && gestureSeconds >= LongPressSeconds)
        {
            Hold();
            gestureConsumed = true;
        }
    }

    private static int StepsFor(float distance, float pitch, float deadZone)
    {
        var magnitude = MathF.Abs(distance);
        if (magnitude <= deadZone)
        {
            return 0;
        }

        var steps = (int)((magnitude - deadZone) / pitch) + 1;
        return distance < 0f ? -steps : steps;
    }

    private void Hold()
    {
        if (board.HoldPiece())
        {
            UiFeedback.Play(UiSound.GamePiece);
        }
    }

    private void HardDrop(float scale)
    {
        if (!board.HasActivePiece || board.GameOver)
        {
            return;
        }

        var landing = grid.Origin + new Vector2((board.ActiveX + 2f) * grid.Pitch, (board.GetGhostY() + 2.5f) * grid.Pitch);
        var before = board.LockedThisFrame;
        board.HardDrop();
        if (board.LockedThisFrame == before)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameHitWood);
        fx.AddTrauma(0.14f);
        particles.Burst(landing, 8, GamePalette.Lighten(Accent, 0.1f) with { W = 0.8f }, 120f * scale, 2.4f, 0.35f, 240f);
    }

    private void OnLock(in GameContext context, float scale)
    {
        var lines = board.ClearedLinesThisFrame;
        if (lines > 0)
        {
            OnLinesCleared(lines, context, scale);
        }

        AnnounceLock(lines, scale);
    }

    private void OnLinesCleared(int lines, in GameContext context, float scale)
    {
        clearedMask = board.ClearedRowsMask;
        clearFlash = TetrisRenderer.ClearFlashSeconds;
        UiFeedback.Play(lines >= TetrisLines ? UiSound.GamePowerUp : UiSound.GameClear);
        fx.AddTrauma(MathF.Min(0.45f, 0.08f * lines));
        fx.HitStop(0.03f + 0.02f * lines);
        context.Fx.Flash(ClearFlashColor, 0.12f + 0.03f * lines);
        var burstTint = GamePalette.Lighten(Accent, 0.2f);
        for (var row = 0; row < TetrisBoard.Rows; row++)
        {
            if ((clearedMask & (1 << row)) == 0)
            {
                continue;
            }

            var center = new Vector2(grid.Center.X, grid.Origin.Y + (row + 0.5f) * grid.Pitch);
            particles.Burst(center, 10, burstTint, 170f * scale, 2.8f, 0.5f, 320f);
            particles.Streaks(center, 5, StreakInk, 380f * scale, 2.4f, 0.45f);
        }

        fx.Shockwave(grid.Center, grid.Width * (0.3f + 0.1f * lines), GamePalette.Lighten(Accent, 0.3f), 0.5f, 3f);
        fx.AddText(GainLabel(board.LastLockScore), new Vector2(grid.Center.X, grid.Origin.Y + grid.Height * 0.3f),
            Accent, 1.2f);
        if (board.LastCombo >= 1)
        {
            context.Fx.EdgeGlow(MathF.Min(1f, (board.LastCombo + 1) * ComboGlowPerLink));
        }

        if (lines >= TetrisLines)
        {
            context.Fx.SlowMo(TetrisSlowMoFactor, TetrisSlowMoSeconds);
            context.Fx.Sweep();
            context.Fx.Punch(0.06f);
            particles.Confetti(new Vector2(grid.Center.X, grid.Origin.Y + grid.Height * 0.25f), 60, TetrisPalette,
                280f * scale, 4f, 1.4f);
            return;
        }

        if (lines >= PunchLines)
        {
            context.Fx.Punch(0.03f);
        }
    }

    private void AnnounceLock(int lines, float scale)
    {
        var position = new Vector2(grid.Center.X, grid.Origin.Y + grid.Height * 0.42f);
        if (board.LastSpin != TetrisSpin.None)
        {
            var mini = board.LastSpin == TetrisSpin.Mini;
            var text = board.LastBackToBack
                ? mini ? backToBackMini : backToBackSpin
                : Loc.T(mini ? L.Games.TSpinMini : L.Games.TSpin);
            fx.AddText(text, position, GamePalette.Lighten(Accent, 0.35f), 1.3f);
            fx.Shockwave(position, grid.Width * 0.5f, GamePalette.Lighten(Accent, 0.4f), 0.5f, 3f);
            particles.Sparkle(position, 14, SparkleInk, 160f * scale, 2.6f, 0.7f);
            return;
        }

        if (lines >= TetrisLines && board.LastBackToBack)
        {
            fx.AddText(Loc.T(L.Games.BackToBack), position, GamePalette.Lighten(Accent, 0.35f), 1.3f);
            return;
        }

        if (lines > 0 && board.LastCombo >= 1)
        {
            fx.AddText(ComboLabel(board.LastCombo + 1), position, GamePalette.Lighten(Accent, 0.3f), 1.1f);
        }
    }

    private void OnLevelUp(in GameContext context)
    {
        previousLevel = board.Level;
        GameSfx.LevelClear();
        fx.AddText(levelLabel.Get(L.Stage.LevelShort, board.Level),
            new Vector2(grid.Center.X, grid.Origin.Y + grid.Height * 0.2f), GamePalette.Lighten(Accent, 0.3f), 1.35f);
        fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.14f);
        context.Fx.Sweep();
    }

    private void OnTopOut(in GameContext context)
    {
        finished = true;
        gestureActive = false;
        UiFeedback.Play(UiSound.GameBreak);
        fx.AddTrauma(0.6f);
        context.Fx.Flash(Danger, 0.35f);
        context.Fx.Vignette(Danger, 0.4f, 0.8f);
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, context.Session.StatId)
            .WithStat(L.Games.Lines, GameNumber.Label(board.Lines))
            .WithStat(L.Games.Level, GameNumber.Label(board.Level))
            .WithStat(L.Games.Combo, GameNumber.Label(board.BestCombo)));
    }

    private bool StackIsHigh()
    {
        for (var row = 0; row < DangerRows; row++)
        {
            for (var column = 0; column < TetrisBoard.Columns; column++)
            {
                if (board.CellColor(column, row) != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void DrawHud(in GameContext context, ImDrawListPtr drawList, bool playing, float scale)
    {
        context.Hud.Score(board.Score);
        context.Hud.Custom(TetrisRenderer.PreviewCapsuleWidth);
        var rect = context.Hud.CustomRect(0);
        if (rect.Width <= 0f)
        {
            return;
        }

        var holdRect = TetrisRenderer.HoldRect(rect, scale);
        var holdHovered = playing && UiInteract.Hover(holdRect.Min, holdRect.Max);
        TetrisRenderer.DrawPreviewCapsule(drawList, board, rect, Accent, holdHovered, scale);
        if (!holdHovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (UiInteract.Click(holdRect.Min, holdRect.Max, true))
        {
            Hold();
        }
    }

    private void SyncLabels()
    {
        if (!ReferenceEquals(labelLanguage, Loc.Current))
        {
            labelLanguage = Loc.Current;
            var backToBack = Loc.T(L.Games.BackToBack);
            backToBackSpin = string.Concat(backToBack, " ", Loc.T(L.Games.TSpin));
            backToBackMini = string.Concat(backToBack, " ", Loc.T(L.Games.TSpinMini));
            Array.Clear(comboLabels);
        }

        if (linesShown == board.Lines)
        {
            return;
        }

        linesShown = board.Lines;
        linesValue = GameNumber.Label(board.Lines);
    }

    private string ComboLabel(int count)
    {
        var index = Math.Clamp(count, 0, MaxComboLabel);
        return comboLabels[index] ??= string.Concat(Loc.T(L.Stage.Times, GameNumber.Label(index)), " ", Loc.T(L.Games.Combo));
    }

    private static string GainLabel(int points)
    {
        if (GainLabels.TryGetValue(points, out var label))
        {
            return label;
        }

        label = string.Concat("+", GameNumber.Label(points));
        GainLabels[points] = label;
        return label;
    }

    private static Rect Grow(Rect rect, float factor)
    {
        var half = rect.Size * 0.5f * factor;
        return new Rect(rect.Center - half, rect.Center + half);
    }
}
