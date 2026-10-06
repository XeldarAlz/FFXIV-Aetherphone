using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Flow;

internal sealed class FlowApp : IMiniGame
{
    private const string GameId = "flow";
    private const int HintsPerLevel = 1;
    private const float RowGap = 6f;
    private const float HintRadius = 16f;
    private const float StripGap = 10f;
    private static readonly LocString[] Modes = { L.Games.Easy, L.Games.Medium, L.Games.Hard };
    private static readonly string[] ModeStatIds = { "flow.easy", "flow.medium", "flow.hard" };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Flow, GameGenre.Puzzle, L.Flow.Hook,
        Backdrop.Paper, HudStyle.Compact, ScoreKind.Level, Modes, ModeStatIds);

    private readonly FlowBoard board = new();
    private readonly FlowRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private RatioLabel flowsLabel;
    private RatioLabel filledLabel;
    private int mode;
    private ulong salt;
    private int boardLevel = -1;
    private int boardMode = -1;
    private ulong boardSalt;
    private bool boardPending;
    private int hintsUsed;
    private bool finished;
    private float entrance;
    private float liquidTime;
    private float fillNudge;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        mode = start.Mode;
        salt = start.Daily ? start.Seed : 0UL;
        boardPending = true;
        hintsUsed = 0;
        finished = false;
        entrance = 0f;
        fillNudge = 0f;
        particles.Clear();
        fx.Clear();
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        var session = context.Session;
        SyncBoard(session.Best + 1, session.Mode, session.Daily ? session.Seed : 0UL, false);
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        Layout(context.Safe, scale, out var header, out _, out var boardArea);
        var grid = GameGrid.Centered(boardArea, board.Columns, board.Rows, FlowRenderer.CellGap);
        var dark = context.Backdrop.Ink == StageInk.Dark;
        var ink = dark ? GamePalette.InkDark : context.Theme.TextStrong;
        var muted = dark ? GamePalette.InkDark with { W = 0.62f } : context.Theme.TextMuted;
        renderer.DrawStrip(drawList, StripRect(header, scale), board.Level, session.Best, Accent, ink, muted, 1f, scale);
        renderer.Draw(drawList, board, grid, Accent, context.Backdrop.Ink, 0f, liquidTime, 1f, Vector2.Zero, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var theme = context.Theme;
        var session = context.Session;
        if (boardPending)
        {
            boardPending = false;
            SyncBoard(session.Best + 1, mode, salt, true);
        }

        var playing = session.State == StageFlow.Playing && !finished;
        var rawSeconds = context.RawDeltaSeconds;
        particles.Update(rawSeconds);
        fx.Update(rawSeconds);
        entrance = GameJuice.Advance(entrance, rawSeconds);
        liquidTime += rawSeconds;
        Layout(context.Safe, scale, out var header, out var progressRow, out var boardArea);
        var grid = GameGrid.Centered(Scaled(boardArea, context.Fx.PlateScale), board.Columns, board.Rows,
            FlowRenderer.CellGap);
        var hovered = playing ? ResolveHover(grid) : -1;
        if (playing)
        {
            HandleInput(hovered, grid, scale, context);
        }

        var dark = context.Backdrop.Ink == StageInk.Dark;
        var ink = dark ? GamePalette.InkDark : theme.TextStrong;
        var muted = dark ? GamePalette.InkDark with { W = 0.62f } : theme.TextMuted;
        renderer.DrawStrip(drawList, StripRect(header, scale), board.Level, session.Best, Accent, ink, muted, entrance,
            scale);
        var hintCenter = new Vector2(header.Max.X - HintRadius * scale, header.Center.Y);
        var hintReady = playing && hintsUsed < HintsPerLevel && board.HintColor() >= 0;
        if (FlowRenderer.HintButton(drawList, hintCenter, HintRadius * scale, hintReady, theme, Accent, scale))
        {
            UseHint(grid, scale, context);
        }

        var filled = board.FilledCells();
        var connected = board.ConnectedColors();
        var awaitingFill = playing && connected == board.ColorCount && filled < board.CellCount;
        fillNudge = Math.Clamp(fillNudge + rawSeconds * (awaitingFill ? 4f : -5f), 0f, 1f);
        renderer.DrawProgress(drawList, progressRow, ink, muted, Accent, scale, flowsLabel.Get(connected, board.ColorCount),
            filledLabel.Get(filled, board.CellCount), filled, board.CellCount, awaitingFill);
        renderer.Draw(drawList, board, grid, Accent, context.Backdrop.Ink, fillNudge, liquidTime, entrance,
            fx.ShakeOffset(scale), scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        context.Hud.Level(board.Level);
        session.Report(board.Level);
    }

    private void SyncBoard(int level, int wantedMode, ulong wantedSalt, bool force)
    {
        if (!force && boardLevel == level && boardMode == wantedMode && boardSalt == wantedSalt && board.Moves == 0)
        {
            return;
        }

        boardLevel = level;
        boardMode = wantedMode;
        boardSalt = wantedSalt;
        board.Reset(level, wantedMode, wantedSalt);
    }

    private static void Layout(Rect safe, float scale, out Rect header, out Rect progressRow, out Rect boardArea)
    {
        var gap = RowGap * scale;
        header = new Rect(safe.Min, new Vector2(safe.Max.X, safe.Min.Y + FlowRenderer.HeaderHeight * scale));
        progressRow = new Rect(new Vector2(safe.Min.X, header.Max.Y + gap),
            new Vector2(safe.Max.X, header.Max.Y + gap + FlowRenderer.ProgressHeight * scale));
        var inset = BoardPlate.Padding * scale;
        boardArea = new Rect(new Vector2(safe.Min.X + inset, progressRow.Max.Y + gap + inset),
            new Vector2(safe.Max.X - inset, safe.Max.Y - inset));
    }

    private static Rect StripRect(Rect header, float scale) =>
        new(header.Min, new Vector2(header.Max.X - (HintRadius * 2f + StripGap) * scale, header.Max.Y));

    private static Rect Scaled(Rect rect, float factor)
    {
        var half = rect.Size * 0.5f * factor;
        return new Rect(rect.Center - half, rect.Center + half);
    }

    private int ResolveHover(in GameGrid grid)
    {
        if (!UiInteract.Hover(grid.Bounds.Min, grid.Bounds.Max))
        {
            return -1;
        }

        var local = ImGui.GetMousePos() - grid.Origin;
        var column = (int)(local.X / grid.Pitch);
        var row = (int)(local.Y / grid.Pitch);
        if (column < 0 || column >= board.Columns || row < 0 || row >= board.Rows)
        {
            return -1;
        }

        return row * board.Columns + column;
    }

    private void HandleInput(int hovered, in GameGrid grid, float scale, in GameContext context)
    {
        if (hovered >= 0 && (board.IsEndpoint(hovered) || board.Owner(hovered) >= 0))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (hovered >= 0)
            {
                board.Press(hovered);
            }
        }
        else if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && board.ActiveColor >= 0)
        {
            DragTo(hovered, grid, scale, context);
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            board.Release();
        }
    }

    private void DragTo(int target, in GameGrid grid, float scale, in GameContext context)
    {
        if (target < 0)
        {
            return;
        }

        for (var guard = 0; guard < FlowBoard.MaxCells; guard++)
        {
            var head = board.Head;
            if (head < 0 || head == target)
            {
                return;
            }

            var result = ExtendToward(head, target);
            if (result == FlowEvent.None)
            {
                return;
            }

            if (result == FlowEvent.Completed)
            {
                OnConnected(grid, scale, context);
                return;
            }
        }
    }

    private FlowEvent ExtendToward(int head, int target)
    {
        var direct = board.StepToward(head, target);
        if (direct >= 0)
        {
            var result = board.Extend(direct);
            if (result != FlowEvent.None)
            {
                return result;
            }
        }

        var aside = board.StepAside(head, target);
        if (aside < 0)
        {
            return FlowEvent.None;
        }

        return board.Extend(aside);
    }

    private void OnConnected(in GameGrid grid, float scale, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameMatch);
        var color = board.ActiveColor;
        if (color < 0)
        {
            return;
        }

        var head = board.PathCell(color, board.PathLength(color) - 1);
        var center = grid.CellCenter(head % board.Columns, head / board.Columns);
        var tint = FlowRenderer.ColorOf(color);
        particles.Burst(center, 14, tint, 150f * scale, 3f, 0.5f, 240f);
        particles.Sparkle(center, 6, GamePalette.Lighten(tint, 0.4f), 120f * scale, 2.2f, 0.6f);
        fx.Shockwave(center, grid.Pitch * 0.9f, GamePalette.Lighten(tint, 0.25f), 0.42f, 2.6f);
        fx.AddTrauma(0.12f);
        context.Fx.Punch(0.03f);
        if (board.IsSolved())
        {
            OnSolved(grid, scale, context);
        }
    }

    private void UseHint(in GameGrid grid, float scale, in GameContext context)
    {
        var color = board.HintColor();
        if (color < 0 || !board.ApplyHint(color))
        {
            return;
        }

        hintsUsed++;
        UiFeedback.Play(UiSound.GamePowerUp);
        var tint = FlowRenderer.ColorOf(color);
        var sparkle = GamePalette.Lighten(tint, 0.4f);
        var length = board.SolutionLength(color);
        for (var index = 0; index < length; index++)
        {
            var cell = board.SolutionCell(color, index);
            particles.Sparkle(grid.CellCenter(cell % board.Columns, cell / board.Columns), 2, sparkle, 90f * scale,
                1.8f, 0.5f);
        }

        var last = board.SolutionCell(color, length - 1);
        fx.Shockwave(grid.CellCenter(last % board.Columns, last / board.Columns), grid.Pitch * 1.2f,
            tint with { W = 0.8f }, 0.45f, 2.4f);
        fx.AddTrauma(0.08f);
        if (board.IsSolved())
        {
            OnSolved(grid, scale, context);
        }
    }

    private void OnSolved(in GameGrid grid, float scale, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameClear);
        finished = true;
        fx.AddTrauma(0.3f);
        context.Fx.Flash(Accent, 0.3f);
        context.Fx.Sweep();
        fx.Shockwave(grid.Center, grid.Width * 0.6f, GamePalette.Lighten(Accent, 0.3f), 0.6f, 3.2f);
        particles.Confetti(new Vector2(grid.Center.X, grid.Bounds.Min.Y), 70, FlowRenderer.ConfettiPalette,
            260f * scale, 4f, 1.3f);
        context.Session.Finish(new GameOutcome(board.Level, ScoreKind.Level, context.Session.StatId)
            .WithStat(L.Games.Moves, GameNumber.Label(board.Moves))
            .WithStat(L.Flow.Hints, GameNumber.Label(hintsUsed)));
    }
}
