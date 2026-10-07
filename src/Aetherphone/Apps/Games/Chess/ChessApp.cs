using System.Threading.Tasks;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Chess;

internal sealed class ChessApp : IMiniGame
{
    private const string GameId = "chess";
    private const float MoveDuration = 0.20f;
    private const float AiDelay = 0.35f;
    private const float PromotionSpeed = 4.5f;
    private const float BottomRowHeight = 32f;
    private const float BottomRowGap = 10f;
    private const float StatusInset = 4f;
    private const float MateSlowMo = 0.45f;
    private const float MateSlowSeconds = 0.4f;
    private const int CapturedCapacity = 16;
    private const int MediumMode = 1;
    private const int HardMode = 2;
    private const int BigCaptureValue = 5;

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

    private static readonly LocString[] Modes = { L.Games.Easy, L.Games.Medium, L.Games.Hard };
    private static readonly string[] ModeStatIds = { "chess.easy", "chess.medium", "chess.hard" };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Chess, GameGenre.Tabletop, L.Chess.Hook,
        Backdrop.Felt, HudStyle.Compact, ScoreKind.Streak, Modes, ModeStatIds);
    private static readonly Vector4 AccentColor = AppAccents.For(GameId);
    private static readonly int[] DisplayValues = { 0, 1, 3, 3, 5, 9, 0 };
    private static readonly Vector4 CheckGlow = new(0.94f, 0.30f, 0.32f, 1f);
    private static readonly Vector4 Cream = new(1f, 0.95f, 0.7f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly TextStyle StatusStyle = TextStyles.FootnoteEmphasized;

    private readonly ChessBoard board = new();
    private readonly ChessEngine engine = new();
    private readonly ChessRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly List<LogEntry> moveLog = new(256);
    private readonly byte[] whiteCaptured = new byte[CapturedCapacity];
    private readonly byte[] blackCaptured = new byte[CapturedCapacity];
    private readonly Vector4[] winPalette;
    private readonly Func<ChessMove> searchWork;
    private ChessLayout layout;
    private Rect plate;
    private Task<ChessMove>? searchTask;
    private ChessMove pendingMove;
    private ulong targets;
    private ulong captureTargets;
    private ulong seed;
    private int searchToken;
    private int taskToken;
    private int searchDepth;
    private long searchBudget;
    private int searchSlack;
    private int whiteCapturedCount;
    private int blackCapturedCount;
    private int mode = MediumMode;
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
    private float entrance = 1f;
    private ChessOutcome outcome;
    private bool seedPending;
    private bool hasPendingMove;
    private bool wasInCheck;
    private bool over;
    private bool finished;

    public ChessApp()
    {
        winPalette = new[] { AccentColor, Core.Theme.Accent.Amber, Core.Theme.Accent.Blue, Core.Theme.Accent.Pink };
        searchWork = () => engine.Search(searchDepth, searchBudget, searchSlack);
        ResetTable();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AccentColor;

    public void Start(in GameStart start)
    {
        mode = start.Mode;
        seed = start.Seed;
        seedPending = true;
        searchToken++;
        ResetTable();
        entrance = 0f;
    }

    public void Close()
    {
        searchToken++;
        ResetTable();
    }

    public void Dispose()
    {
        searchToken++;
    }

    public void DrawIdle(in GameContext context)
    {
        var scale = UiScale.Current;
        ComputeLayout(context.Safe, scale);
        DrawTable(ImGui.GetWindowDrawList(), ChessRenderState.Idle, context, Vector2.Zero, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var theme = context.Theme;
        var simDelta = context.DeltaSeconds;
        var rawDelta = context.RawDeltaSeconds;
        var playing = context.Session.State == StageFlow.Playing;
        entrance = GameJuice.Advance(entrance, rawDelta);
        movingPhase = MathF.Min(1f, movingPhase + simDelta / MoveDuration);
        particles.Update(rawDelta);
        fx.Update(rawDelta);
        ComputeLayout(StageLayout.Punched(context.Safe, context.Fx.PlateScale), scale);
        if (!over && playing)
        {
            CollectSearchResult(context, scale);
            StartSearchIfNeeded(simDelta);
        }

        var hovered = playing && CanAcceptInput() ? ResolveHover() : -1;
        if (hovered >= 0)
        {
            HandleInput(hovered, context, scale);
        }

        var checkSquare = board.InCheck(board.BlackToMove) ? board.FindKing(board.BlackToMove) : -1;
        var state = new ChessRenderState(selected, hovered, targets, captureTargets, lastFrom, lastTo, checkSquare,
            movingFrom, movingTo, movingPhase, entrance);
        DrawTable(drawList, state, context, fx.ShakeOffset(scale), scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        DrawBottomRow(drawList, theme, playing, scale);
        FillHud(context, drawList, theme, scale);
        if (over && !finished && movingPhase >= 1f)
        {
            FinishGame(context, scale);
        }

        if (promotionFrom >= 0 && playing)
        {
            DrawPromotion(context, scale);
        }

        context.Session.Report(PlayerMoves);
    }

    private int PlayerMoves => (moveLog.Count + 1) / 2;

    private void ResetTable()
    {
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
        outcome = ChessOutcome.Ongoing;
        hasPendingMove = false;
        wasInCheck = false;
        over = false;
        finished = false;
    }

    private void ComputeLayout(Rect safe, float scale)
    {
        var reserved = (BottomRowHeight + BottomRowGap) * scale;
        var boardBottom = MathF.Max(safe.Min.Y, safe.Max.Y - reserved);
        var area = new Rect(safe.Min, new Vector2(safe.Max.X, boardBottom)).Inset(BoardPlate.Padding * scale);
        layout = ChessRenderer.Layout(area);
        plate = BoardPlate.Around(layout.Bounds, scale);
    }

    private void DrawTable(ImDrawListPtr drawList, in ChessRenderState state, in GameContext context, Vector2 shake,
        float scale)
    {
        BoardPlate.Draw(drawList, plate.Translate(shake), BoardPlate.Radius * scale, scale, AccentColor,
            context.Backdrop.Ink);
        renderer.Draw(drawList, board, layout.Translate(shake), state, AccentColor, scale);
    }

    private void DrawBottomRow(ImDrawListPtr drawList, PhoneTheme theme, bool playing, float scale)
    {
        var rowTop = plate.Max.Y + BottomRowGap * scale;
        var rowHeight = BottomRowHeight * scale;
        var undoLabel = Loc.T(L.Games.Undo);
        var undoWidth = ChessRenderer.UndoWidth(undoLabel, scale);
        var undoRect = new Rect(new Vector2(plate.Max.X - undoWidth, rowTop), new Vector2(plate.Max.X, rowTop + rowHeight));
        if (ChessRenderer.DrawUndoCapsule(drawList, undoRect, undoLabel, AccentColor, theme, playing && CanUndo(), scale))
        {
            UndoMoves();
        }

        var inCheck = !over && board.InCheck(false);
        var color = inCheck ? theme.Danger : StageInks.Muted;
        Typography.Draw(drawList,
            new Vector2(plate.Min.X + StatusInset * scale, rowTop + rowHeight * 0.5f - Typography.LineHeight(StatusStyle) * 0.5f),
            StatusText(inCheck), color, StatusStyle);
    }

    private string StatusText(bool inCheck)
    {
        if (over)
        {
            return OutcomeLabel();
        }

        if (board.BlackToMove)
        {
            return Loc.T(L.Games.Thinking);
        }

        return inCheck ? Loc.T(L.Games.Check) : Loc.T(L.Games.YourTurn);
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

    private void FillHud(in GameContext context, ImDrawListPtr drawList, PhoneTheme theme, float scale)
    {
        if (whiteCapturedCount + blackCapturedCount > 0)
        {
            var lead = MaterialLead();
            var leadLabel = lead == 0 ? string.Empty : GameNumber.Signed(Math.Abs(lead));
            context.Hud.Custom(ChessRenderer.CapturedCapsuleWidth(whiteCapturedCount, blackCapturedCount, leadLabel, lead,
                scale));
            var rect = context.Hud.CustomRect(0);
            if (rect.Width > 0f)
            {
                renderer.DrawCapturedCapsule(drawList, rect, whiteCaptured, whiteCapturedCount, blackCaptured,
                    blackCapturedCount, leadLabel, lead, theme, scale);
            }
        }

        context.Hud.Best(context.Session.Best);
    }

    private bool CanAcceptInput() =>
        !over && promotionFrom < 0 && searchTask is null && !hasPendingMove && !board.BlackToMove && movingPhase >= 1f;

    private bool CanUndo() => searchTask is null && !hasPendingMove && moveLog.Count > 0 && promotionFrom < 0;

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

    private void HandleInput(int hovered, in GameContext context, float scale)
    {
        var square = layout.SquareRect(hovered);
        if (!UiInteract.Click(square.Min, square.Max, true, false))
        {
            return;
        }

        if (selected >= 0 && (targets & (1UL << hovered)) != 0)
        {
            PlayPlayerMove(selected, hovered, context, scale);
            return;
        }

        selected = ChessPiece.IsColor(board.PieceAt(hovered), false) ? hovered : -1;
        RefreshTargets();
    }

    private void PlayPlayerMove(int from, int to, in GameContext context, float scale)
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
            ApplyMove(move, context, scale);
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

    private void ApplyMove(in ChessMove move, in GameContext context, float scale)
    {
        var moverIsBlack = board.BlackToMove;
        board.Make(move, out var undo);
        moveLog.Add(new LogEntry(move, undo));
        var captured = RecordCapture(move, undo, moverIsBlack);
        lastFrom = move.From;
        lastTo = move.To;
        movingFrom = move.From;
        movingTo = move.To;
        movingPhase = 0f;
        selected = -1;
        targets = 0;
        captureTargets = 0;
        var center = layout.SquareCenter(move.To);
        if (captured != 0)
        {
            UiFeedback.Play(UiSound.GameHitWood);
            var shards = new ParticleSpec(GamePalette.Lighten(AccentColor, 0.25f), AccentColor with { W = 0f }, 3.2f * scale,
                220f * scale, 0.55f, 420f, 1.4f, 10f, shape: ParticleShape.Shard);
            particles.Emit(shards, center, 12);
            fx.Shockwave(center, layout.CellSize * 1.3f, GamePalette.Lighten(AccentColor, 0.3f) with { W = 0.7f }, 0.4f,
                2.4f);
            fx.AddTrauma(0.16f);
            if (DisplayValues[(int)ChessPiece.Type(captured)] >= BigCaptureValue)
            {
                context.Fx.Punch(0.04f);
            }
        }
        else
        {
            UiFeedback.Play(UiSound.GamePiece);
            particles.Sparkle(center, 5, GamePalette.Lighten(AccentColor, 0.3f), 80f * scale, 1.6f, 0.35f);
        }

        UpdateOutcome(context, moverIsBlack);
    }

    private byte RecordCapture(in ChessMove move, in ChessUndo undo, bool moverIsBlack)
    {
        var captured = undo.Captured;
        if ((move.Flags & ChessMoveFlags.EnPassant) != 0)
        {
            captured = ChessPiece.Make(ChessPieceType.Pawn, !moverIsBlack);
        }

        if (captured == 0)
        {
            return 0;
        }

        if (moverIsBlack)
        {
            if (blackCapturedCount < CapturedCapacity)
            {
                blackCaptured[blackCapturedCount++] = captured;
            }

            return captured;
        }

        if (whiteCapturedCount < CapturedCapacity)
        {
            whiteCaptured[whiteCapturedCount++] = captured;
        }

        return captured;
    }

    private void UpdateOutcome(in GameContext context, bool moverIsBlack)
    {
        outcome = board.Evaluate(out _);
        if (outcome == ChessOutcome.Ongoing)
        {
            thinkDelay = board.BlackToMove ? AiDelay : 0f;
            PulseCheck(context, moverIsBlack);
            return;
        }

        over = true;
        wasInCheck = false;
        if (outcome == ChessOutcome.Checkmate)
        {
            context.Fx.SlowMo(MateSlowMo, MateSlowSeconds);
        }
    }

    private void PulseCheck(in GameContext context, bool moverIsBlack)
    {
        var inCheck = moverIsBlack && board.InCheck(false);
        if (inCheck && !wasInCheck)
        {
            context.Fx.Vignette(CheckGlow, 0.28f, 0.8f);
            var king = board.FindKing(false);
            if (king >= 0)
            {
                fx.AddText(Loc.T(L.Games.Check), layout.SquareCenter(king), CheckGlow, 1.2f);
            }
        }

        wasInCheck = inCheck;
    }

    private void StartSearchIfNeeded(float simDelta)
    {
        if (searchTask is not null || hasPendingMove || !board.BlackToMove || promotionFrom >= 0)
        {
            return;
        }

        thinkDelay -= simDelta;
        if (thinkDelay > 0f)
        {
            return;
        }

        if (seedPending)
        {
            engine.Reseed(seed);
            seedPending = false;
        }

        engine.Prepare(board);
        searchDepth = DepthFor(mode);
        searchBudget = BudgetFor(mode);
        searchSlack = SlackFor(mode);
        taskToken = searchToken;
        searchTask = Task.Run(searchWork);
    }

    private void CollectSearchResult(in GameContext context, float scale)
    {
        if (searchTask is not null && searchTask.IsCompleted)
        {
            var completed = searchTask;
            searchTask = null;
            if (taskToken == searchToken && board.BlackToMove)
            {
                pendingMove = completed.IsCompletedSuccessfully ? completed.Result : default;
                hasPendingMove = true;
            }
        }

        if (!hasPendingMove || movingPhase < 1f)
        {
            return;
        }

        hasPendingMove = false;
        var move = pendingMove;
        if (move.IsNone && !TryPickFallback(out move))
        {
            return;
        }

        ApplyMove(move, context, scale);
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

        over = false;
        outcome = ChessOutcome.Ongoing;
        selected = -1;
        targets = 0;
        captureTargets = 0;
        movingPhase = 1f;
        movingFrom = -1;
        movingTo = -1;
        thinkDelay = 0f;
        var last = moveLog.Count - 1;
        lastFrom = last >= 0 ? moveLog[last].Move.From : -1;
        lastTo = last >= 0 ? moveLog[last].Move.To : -1;
        wasInCheck = board.InCheck(false);
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

    private int MaterialLead()
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

        return white - dark;
    }

    private void DrawPromotion(in GameContext context, float scale)
    {
        promotionProgress = MathF.Min(1f, promotionProgress + context.RawDeltaSeconds * PromotionSpeed);
        var choice = ChessRenderer.DrawPromotionPicker(context.Full, context.Theme, AccentColor, false,
            promotionProgress, scale);
        if (choice == ChessPieceType.None)
        {
            return;
        }

        var from = promotionFrom;
        var to = promotionTo;
        promotionFrom = -1;
        promotionTo = -1;
        promotionProgress = 0f;
        if (board.TryFindMove(from, to, choice, out var move))
        {
            ApplyMove(move, context, scale);
        }
    }

    private void FinishGame(in GameContext context, float scale)
    {
        finished = true;
        var playerWon = outcome == ChessOutcome.Checkmate && board.BlackToMove;
        if (playerWon)
        {
            fx.AddTrauma(0.4f);
            fx.Flash(AccentColor, 0.35f);
            particles.Confetti(new Vector2(layout.Center.X, layout.Origin.Y), 80, winPalette, 260f * scale, 4f, 1.4f);
            particles.Sparkle(layout.Center, 16, Cream, 200f * scale, 2.6f, 0.9f);
            context.Fx.Punch(0.05f);
            context.Fx.Sweep();
        }
        else if (outcome == ChessOutcome.Checkmate)
        {
            context.Fx.Vignette(Danger, 0.3f, 0.9f);
        }

        var statId = StageSpec.StatIdFor(mode);
        var result = outcome == ChessOutcome.Checkmate
            ? new GameOutcome(playerWon ? context.Session.Best + 1 : 0, ScoreKind.Streak, statId, playerWon)
            : GameOutcome.Drawn(statId);
        context.Session.Finish(result
            .WithStat(L.Chess.Result, OutcomeLabel())
            .WithStat(L.Games.Moves, GameNumber.Label(PlayerMoves)));
    }

    private static int DepthFor(int mode)
    {
        return mode switch
        {
            MediumMode => 4,
            HardMode => 5,
            _ => 2,
        };
    }

    private static long BudgetFor(int mode)
    {
        return mode switch
        {
            MediumMode => 1_500_000,
            HardMode => 3_500_000,
            _ => 200_000,
        };
    }

    private static int SlackFor(int mode)
    {
        return mode switch
        {
            MediumMode => 30,
            HardMode => 0,
            _ => 130,
        };
    }
}
