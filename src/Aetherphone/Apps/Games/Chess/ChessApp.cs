using System.Threading.Tasks;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Chess;

internal sealed class ChessApp : IMiniGame
{
    private const string GameId = "chess";
    private const float MoveDuration = 0.20f;
    private const float AiDelay = 0.35f;
    private const float DifficultyRowY = 22f;
    private const float StatusRowY = 54f;
    private const float StripHeight = 22f;
    private const int CapturedCapacity = 16;

    private readonly struct LogEntry
    {
        public readonly ChessMove Move;
        public readonly ChessUndo Undo;

        public LogEntry(in ChessMove move, in ChessUndo undo)
        {
            Move = move;
            Undo = undo;
        }
    }

    private static readonly int[] DisplayValues = { 0, 1, 3, 3, 5, 9, 0 };

    private readonly ChessBoard board = new();
    private readonly ChessEngine engine = new();
    private readonly ChessRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly List<LogEntry> moveLog = new(256);
    private readonly byte[] whiteCaptured = new byte[CapturedCapacity];
    private readonly byte[] blackCaptured = new byte[CapturedCapacity];
    private readonly string[] difficultyLabels = new string[3];
    private ChessLayout layout;
    private Task<ChessMove>? searchTask;
    private ulong targets;
    private ulong captureTargets;
    private int searchToken;
    private int taskToken;
    private int whiteCapturedCount;
    private int blackCapturedCount;
    private int difficulty = 1;
    private int selected = -1;
    private int lastFrom = -1;
    private int lastTo = -1;
    private int movingFrom = -1;
    private int movingTo = -1;
    private int promotionFrom = -1;
    private int promotionTo = -1;
    private float movingPhase = 1f;
    private float promotionProgress;
    private float thinkDelay;
    private float resultAppear;
    private ChessOutcome outcome;
    private bool finished;
    private bool playerWon;
    private bool pendingResult;
    private bool newBest;
    private bool statsLoaded;
    private int streak;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Chess);
    public string Genre => Loc.T(L.Games.GenreStrategy);

    public void Open()
    {
        statsLoaded = false;
        StartNewGame(difficulty);
    }

    public void Close()
    {
        searchToken++;
    }

    public void Dispose()
    {
        searchToken++;
    }

    private void StartNewGame(int target)
    {
        difficulty = target;
        searchToken++;
        board.Reset();
        moveLog.Clear();
        particles.Clear();
        fx.Clear();
        whiteCapturedCount = 0;
        blackCapturedCount = 0;
        targets = 0;
        captureTargets = 0;
        selected = -1;
        lastFrom = -1;
        lastTo = -1;
        movingFrom = -1;
        movingTo = -1;
        movingPhase = 1f;
        promotionFrom = -1;
        promotionTo = -1;
        promotionProgress = 0f;
        thinkDelay = 0f;
        resultAppear = 0f;
        outcome = ChessOutcome.Ongoing;
        finished = false;
        playerWon = false;
        pendingResult = false;
        newBest = false;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = UiScale.Current;
        var theme = context.Theme;
        var body = context.Body;
        if (!statsLoaded)
        {
            streak = context.Stats.Get(GameId).Streak;
            statsLoaded = true;
        }

        if (pendingResult)
        {
            ResolveResult(context, scale);
        }

        movingPhase = MathF.Min(1f, movingPhase + deltaSeconds / MoveDuration);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        CollectSearchResult();
        StartSearchIfNeeded(deltaSeconds);
        GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
        DrawDifficultyRow(body, theme, scale);
        DrawStatusRow(body, theme, scale);
        var topStrip = new Rect(new Vector2(body.Min.X + 10f * scale, body.Min.Y + 72f * scale),
            new Vector2(body.Max.X - 10f * scale, body.Min.Y + (72f + StripHeight) * scale));
        var bottomStrip = new Rect(new Vector2(body.Min.X + 10f * scale, body.Max.Y - (StripHeight + 8f) * scale),
            new Vector2(body.Max.X - 10f * scale, body.Max.Y - 8f * scale));
        var boardArea = new Rect(new Vector2(body.Min.X + 6f * scale, topStrip.Max.Y + 6f * scale),
            new Vector2(body.Max.X - 6f * scale, bottomStrip.Min.Y - 6f * scale));
        layout = ChessRenderer.Layout(boardArea);
        var hovered = ResolveHover();
        if (CanAcceptInput())
        {
            HandleInput(hovered);
        }

        var checkSquare = board.InCheck(board.BlackToMove) ? board.FindKing(board.BlackToMove) : -1;
        var state = new ChessRenderState(selected, hovered, targets, captureTargets, lastFrom, lastTo, checkSquare,
            movingFrom, movingTo, movingPhase);
        renderer.Draw(board, layout, state, Accent, scale);
        var drawList = ImGui.GetWindowDrawList();
        renderer.DrawCaptured(drawList, topStrip, blackCaptured, blackCapturedCount, MaterialLead(true), theme, scale);
        renderer.DrawCaptured(drawList, bottomStrip, whiteCaptured, whiteCapturedCount, MaterialLead(false), theme,
            scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        if (promotionFrom >= 0)
        {
            DrawPromotion(body, theme, deltaSeconds, scale);
            return;
        }

        if (finished)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private void DrawDifficultyRow(Rect body, PhoneTheme theme, float scale)
    {
        difficultyLabels[0] = Loc.T(L.Games.Easy);
        difficultyLabels[1] = Loc.T(L.Games.Medium);
        difficultyLabels[2] = Loc.T(L.Games.Hard);
        var rowY = body.Min.Y + DifficultyRowY * scale;
        var segmentRow = new Rect(new Vector2(body.Min.X + 4f * scale, rowY - 13f * scale),
            new Vector2(body.Max.X - 76f * scale, rowY + 13f * scale));
        var selection = SegmentStrip.Draw("chess.difficulty", segmentRow, difficultyLabels, difficulty, theme);
        if (selection != difficulty)
        {
            StartNewGame(selection);
            return;
        }

        if (IconButton(new Vector2(body.Max.X - 52f * scale, rowY), 15f * scale, FontAwesomeIcon.Undo, theme,
                CanUndo()))
        {
            UndoMoves();
        }

        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 15f * scale, theme))
        {
            StartNewGame(difficulty);
        }
    }

    private void DrawStatusRow(Rect body, PhoneTheme theme, float scale)
    {
        var status = StatusText();
        var color = board.InCheck(false) && !finished ? theme.Danger : theme.TextMuted;
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + StatusRowY * scale), status, color,
            TextStyles.FootnoteEmphasized);
    }

    private string StatusText()
    {
        if (finished)
        {
            return OutcomeLabel();
        }

        if (searchTask is not null || (board.BlackToMove && thinkDelay > 0f))
        {
            return Loc.T(L.Games.Thinking);
        }

        return board.InCheck(false) ? Loc.T(L.Games.Check) : Loc.T(L.Games.YourTurn);
    }

    private string OutcomeLabel()
    {
        return outcome switch
        {
            ChessOutcome.Checkmate => Loc.T(L.Games.Checkmate),
            ChessOutcome.Stalemate => Loc.T(L.Games.Stalemate),
            _ => Loc.T(L.Games.Draw),
        };
    }

    private bool CanAcceptInput() =>
        !finished && promotionFrom < 0 && searchTask is null && !board.BlackToMove && movingPhase >= 1f;

    private bool CanUndo() => searchTask is null && moveLog.Count > 0 && promotionFrom < 0;

    private int ResolveHover()
    {
        if (!UiInteract.Hover(layout.Bounds.Min, layout.Bounds.Max))
        {
            return -1;
        }

        var square = layout.HitTest(ImGui.GetMousePos());
        if (square >= 0)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return square;
    }

    private void HandleInput(int hovered)
    {
        if (hovered < 0 || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        if (selected >= 0 && (targets & (1UL << hovered)) != 0)
        {
            PlayPlayerMove(selected, hovered);
            return;
        }

        selected = ChessPiece.IsColor(board.PieceAt(hovered), false) ? hovered : -1;
        RefreshTargets();
    }

    private void PlayPlayerMove(int from, int to)
    {
        if (board.NeedsPromotion(from, to))
        {
            promotionFrom = from;
            promotionTo = to;
            promotionProgress = 0f;
            return;
        }

        if (board.TryFindMove(from, to, ChessPieceType.None, out var move))
        {
            ApplyMove(move);
        }
    }

    private void RefreshTargets()
    {
        targets = 0;
        captureTargets = 0;
        if (selected < 0)
        {
            return;
        }

        Span<ChessMove> moves = stackalloc ChessMove[ChessBoard.MaxMoves];
        var count = board.GenerateMoves(moves);
        for (var index = 0; index < count; index++)
        {
            var move = moves[index];
            if (move.From != selected)
            {
                continue;
            }

            targets |= 1UL << move.To;
            if ((move.Flags & ChessMoveFlags.Capture) != 0)
            {
                captureTargets |= 1UL << move.To;
            }
        }
    }

    private void ApplyMove(in ChessMove move)
    {
        var moverIsBlack = board.BlackToMove;
        board.Make(move, out var undo);
        moveLog.Add(new LogEntry(move, undo));
        RecordCapture(move, undo, moverIsBlack);
        lastFrom = move.From;
        lastTo = move.To;
        movingFrom = move.From;
        movingTo = move.To;
        movingPhase = 0f;
        selected = -1;
        targets = 0;
        captureTargets = 0;
        var scale = UiScale.Current;
        var center = layout.CellSize > 0f ? layout.SquareCenter(move.To) : Vector2.Zero;
        if (layout.CellSize > 0f)
        {
            if ((move.Flags & ChessMoveFlags.Capture) != 0)
            {
                particles.Burst(center, 12, Accent, 150f * scale, 2.8f, 0.45f, 240f);
                fx.Shockwave(center, layout.CellSize * 1.3f, GamePalette.Lighten(Accent, 0.3f) with { W = 0.7f },
                    0.4f, 2.4f);
                fx.AddTrauma(0.16f);
            }
            else
            {
                particles.Sparkle(center, 5, GamePalette.Lighten(Accent, 0.3f), 80f * scale, 1.6f, 0.35f);
            }
        }

        UpdateOutcome();
    }

    private void RecordCapture(in ChessMove move, in ChessUndo undo, bool moverIsBlack)
    {
        var captured = undo.Captured;
        if ((move.Flags & ChessMoveFlags.EnPassant) != 0)
        {
            captured = ChessPiece.Make(ChessPieceType.Pawn, !moverIsBlack);
        }

        if (captured == 0)
        {
            return;
        }

        if (moverIsBlack)
        {
            if (blackCapturedCount < CapturedCapacity)
            {
                blackCaptured[blackCapturedCount++] = captured;
            }

            return;
        }

        if (whiteCapturedCount < CapturedCapacity)
        {
            whiteCaptured[whiteCapturedCount++] = captured;
        }
    }

    private void UpdateOutcome()
    {
        outcome = board.Evaluate(out _);
        if (outcome == ChessOutcome.Ongoing)
        {
            thinkDelay = board.BlackToMove ? AiDelay : 0f;
            return;
        }

        finished = true;
        pendingResult = true;
        resultAppear = 0f;
        playerWon = outcome == ChessOutcome.Checkmate && board.BlackToMove;
    }

    private void StartSearchIfNeeded(float deltaSeconds)
    {
        if (finished || searchTask is not null || !board.BlackToMove || promotionFrom >= 0)
        {
            return;
        }

        thinkDelay -= deltaSeconds;
        if (thinkDelay > 0f)
        {
            return;
        }

        engine.Prepare(board);
        var depth = DepthFor(difficulty);
        var budget = BudgetFor(difficulty);
        var slack = SlackFor(difficulty);
        taskToken = searchToken;
        searchTask = Task.Run(() => engine.Search(depth, budget, slack));
    }

    private void CollectSearchResult()
    {
        if (searchTask is null || !searchTask.IsCompleted)
        {
            return;
        }

        var completed = searchTask;
        searchTask = null;
        if (taskToken != searchToken || finished || !board.BlackToMove)
        {
            return;
        }

        var move = completed.IsCompletedSuccessfully ? completed.Result : default;
        if (move.IsNone && !TryPickFallback(out move))
        {
            return;
        }

        ApplyMove(move);
    }

    private bool TryPickFallback(out ChessMove move)
    {
        Span<ChessMove> moves = stackalloc ChessMove[ChessBoard.MaxMoves];
        var count = board.GenerateMoves(moves);
        if (count == 0)
        {
            move = default;
            return false;
        }

        move = moves[0];
        return true;
    }

    private void UndoMoves()
    {
        if (!CanUndo())
        {
            return;
        }

        PopMove();
        while (board.BlackToMove && moveLog.Count > 0)
        {
            PopMove();
        }

        finished = false;
        pendingResult = false;
        outcome = ChessOutcome.Ongoing;
        selected = -1;
        targets = 0;
        captureTargets = 0;
        movingPhase = 1f;
        movingFrom = -1;
        movingTo = -1;
        thinkDelay = 0f;
        resultAppear = 0f;
        var last = moveLog.Count - 1;
        lastFrom = last >= 0 ? moveLog[last].Move.From : -1;
        lastTo = last >= 0 ? moveLog[last].Move.To : -1;
    }

    private void PopMove()
    {
        var index = moveLog.Count - 1;
        var entry = moveLog[index];
        moveLog.RemoveAt(index);
        var captured = entry.Undo.Captured;
        var enPassant = (entry.Move.Flags & ChessMoveFlags.EnPassant) != 0;
        board.Unmake(entry.Move, entry.Undo);
        if (captured == 0 && !enPassant)
        {
            return;
        }

        if (board.BlackToMove)
        {
            blackCapturedCount = Math.Max(0, blackCapturedCount - 1);
            return;
        }

        whiteCapturedCount = Math.Max(0, whiteCapturedCount - 1);
    }

    private int MaterialLead(bool black)
    {
        var white = 0;
        for (var index = 0; index < whiteCapturedCount; index++)
        {
            white += DisplayValues[(int)ChessPiece.Type(whiteCaptured[index])];
        }

        var dark = 0;
        for (var index = 0; index < blackCapturedCount; index++)
        {
            dark += DisplayValues[(int)ChessPiece.Type(blackCaptured[index])];
        }

        return black ? dark - white : white - dark;
    }

    private void DrawPromotion(Rect body, PhoneTheme theme, float deltaSeconds, float scale)
    {
        promotionProgress = MathF.Min(1f, promotionProgress + deltaSeconds * 4.5f);
        var choice = ChessRenderer.DrawPromotionPicker(body, theme, Accent, false, promotionProgress, scale);
        if (choice == ChessPieceType.None)
        {
            return;
        }

        if (board.TryFindMove(promotionFrom, promotionTo, choice, out var move))
        {
            promotionFrom = -1;
            promotionTo = -1;
            promotionProgress = 0f;
            ApplyMove(move);
            return;
        }

        promotionFrom = -1;
        promotionTo = -1;
        promotionProgress = 0f;
    }

    private void ResolveResult(in GameContext context, float scale)
    {
        pendingResult = false;
        if (playerWon)
        {
            var previous = streak;
            streak = context.Stats.RecordWin(GameId);
            newBest = streak > previous;
            fx.AddTrauma(0.4f);
            fx.Flash(Accent, 0.35f);
            ReadOnlySpan<Vector4> palette = new[]
            {
                Accent, Core.Theme.Accent.Amber, Core.Theme.Accent.Blue, Core.Theme.Accent.Pink,
            };
            if (layout.CellSize > 0f)
            {
                particles.Confetti(new Vector2(layout.Center.X, layout.Origin.Y), 80, palette, 260f * scale, 4f, 1.4f);
                particles.Sparkle(layout.Center, 16, new Vector4(1f, 0.95f, 0.7f, 1f), 200f * scale, 2.6f, 0.9f);
            }

            return;
        }

        newBest = false;
        if (outcome != ChessOutcome.Checkmate)
        {
            return;
        }

        context.Stats.ResetStreak(GameId);
        streak = 0;
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var drawn = outcome != ChessOutcome.Checkmate;
        var title = drawn ? Loc.T(L.Games.Draw) :
            playerWon ? Loc.T(L.Games.YouWin) : Loc.T(L.Games.Lose);
        var titleColor = drawn ? theme.TextStrong :
            playerWon ? Accent : theme.Danger;
        var secondary = streak > 1 ? $"{Loc.T(L.Games.Streak)} {GameNumber.Label(streak)}" : OutcomeLabel();
        var result = new GameResult(title, titleColor, Loc.T(L.Games.Moves),
            GameNumber.Label((moveLog.Count + 1) / 2), secondary, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame(difficulty);
        }
    }

    private static bool IconButton(Vector2 center, float radius, FontAwesomeIcon icon, PhoneTheme theme, bool enabled)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = enabled && UiInteract.Hover(min, max);
        Material.Frosted(drawList, min, max, radius, scale, enabled ? 0.92f : 0.55f);
        if (hovered)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(theme.Accent with { W = 0.16f }));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var ink = !enabled ? theme.TextMuted with { W = 0.45f } : hovered ? theme.TextStrong : theme.Accent;
        ProgressRing.CenterIcon(drawList, center, icon, ink, radius * 0.95f);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static int DepthFor(int difficulty)
    {
        return difficulty switch
        {
            1 => 4,
            2 => 5,
            _ => 2,
        };
    }

    private static long BudgetFor(int difficulty)
    {
        return difficulty switch
        {
            1 => 1_500_000,
            2 => 3_500_000,
            _ => 200_000,
        };
    }

    private static int SlackFor(int difficulty)
    {
        return difficulty switch
        {
            1 => 30,
            2 => 0,
            _ => 130,
        };
    }
}
