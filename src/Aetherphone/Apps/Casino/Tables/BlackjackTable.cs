using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed partial class BlackjackTable : ICabinetIdle
{
    private const float HostedRowGap = 6f;
    private const string InfoMarquee = "casino.blackjack.hostedInfo";
    private const float SnappedClock = 10f;
    private const float ReasonSeconds = 6f;

    private struct SeatMotion
    {
        public long ShownBet;
        public float BetClock;
        public bool SettleStarted;
        public float SettleClock;
        public int SettleSign;
    }

    private readonly CasinoStore chips;
    private readonly CasinoRoomsStore rooms;
    private readonly CasinoTablesStore tables;
    private readonly CasinoHistoryStore history;
    private readonly CasinoTurnNotifier turns;
    private readonly ConfirmService confirm;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly Venue.TournamentOverlay tournament;
    private readonly BlackjackSeatFlow seatFlow;
    private readonly Action leaveRoom;
    private readonly Action<string> openLedger;
    private readonly Action openGilLedger;
    private readonly Action standUp;
    private readonly BlackjackHostedText hostedText = new();
    private readonly CasinoTextCache text = new();
    private readonly BlackjackHandLog handLog = new();
    private readonly BlackjackHistorySheet historySheet;
    private readonly BlackjackProjection projection = new();
    private readonly BlackjackDealPlayback playback = new();
    private readonly BlackjackDealer dealer = new();
    private readonly BlackjackIdleScript idle = new();
    private readonly BlackjackTableLayout layout = new();
    private readonly SeatView[] seatViews = new SeatView[BlackjackRules.SeatCount];
    private readonly SeatMotion[] motions = new SeatMotion[BlackjackRules.SeatCount];
    private readonly BlackjackSeatRecap[] recaps = new BlackjackSeatRecap[BlackjackRules.SeatCount];
    private readonly Spring[] heroRaise = new Spring[BlackjackRules.MaxHandsPerSeat];
    private readonly string[] railNameSources = new string[BlackjackRules.SeatCount];
    private readonly string[] railForenames = new string[BlackjackRules.SeatCount];

    private int mySeat = -1;
    private string roomId = CasinoRoomIds.BlackjackPit;
    private string inlineReason = string.Empty;
    private string shownReason = string.Empty;
    private float reasonClock;
    private bool entered;
    private bool motionsPrimed;
    private bool dealing;
    private bool banking;

    public BlackjackTable(CasinoStore chips, CasinoRoomsStore rooms, CasinoTablesStore tables,
        CasinoHistoryStore history, CasinoTurnNotifier turns, ConfirmService confirm, RemoteImageCache images,
        LodestoneService lodestone, Venue.TournamentOverlay tournament, Action leaveRoom, Action<string> openLedger,
        Action openGilLedger)
    {
        this.chips = chips;
        this.rooms = rooms;
        this.tables = tables;
        this.history = history;
        this.turns = turns;
        this.confirm = confirm;
        this.images = images;
        this.lodestone = lodestone;
        this.tournament = tournament;
        this.leaveRoom = leaveRoom;
        this.openLedger = openLedger;
        this.openGilLedger = openGilLedger;
        standUp = StandUp;
        seatFlow = new BlackjackSeatFlow(tables);
        composer = new BetComposer("##blackjackBet");
        gilComposer = new ClassicBetComposer("##blackjackGilBet");
        historySheet = new BlackjackHistorySheet(handLog, text);
    }

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public string RoomId => roomId;

    public int Currency
    {
        get
        {
            var board = projection.Board;
            if (board is not null)
            {
                return CasinoCurrencies.Of(board);
            }

            var card = tables.CardFor(roomId);
            return card is null ? CasinoCurrencies.Chips : CasinoCurrencies.Of(card);
        }
    }

    public float DeckHeight => DeckHeightFor(Currency);

    public static float DeckHeightFor(int currency)
    {
        var composerHeight = BetComposer.DeckHeightFor(false, false);
        if (currency != CasinoCurrencies.Gil)
        {
            return BlackjackDeckLayout.DeckHeight(composerHeight);
        }

        return BlackjackDeckLayout.DeckHeight(MathF.Max(composerHeight,
            ClassicBetComposer.HeightFor(1f) + BetComposer.Pad * 2f));
    }

    public bool HoldsSeat => entered && !seatFlow.StandQueued
                             && (BlackjackRules.IsSeat(mySeat) || CasinoSeatMachine.Holds(seatFlow.Stage));

    public bool ForfeitsTournament
    {
        get
        {
            var board = projection.Board;
            return board is not null && HoldsSeat && BlackjackHosting.StillInTournament(board, rooms.AccountId);
        }
    }

    public void ConfirmLeave(Action leave)
    {
        if (ForfeitsTournament)
        {
            ConfirmResign(leave);
            return;
        }

        TableLeaveConfirm.Ask(confirm, leave);
    }

    public void ConfirmResign(Action resign)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Venue.TournamentResignTitle),
            Message = Loc.T(L.Venue.TournamentResignBody),
            ConfirmLabel = Loc.T(L.Venue.TournamentResign),
            CancelLabel = Loc.T(L.Strip.KeepPlaying),
            Sheet = true,
            Danger = true,
            Confirm = resign,
        });
    }

    public void Enter(string tableId)
    {
        var nextRoomId = tableId.Length > 0 ? tableId : CasinoRoomIds.BlackjackPit;
        if (entered && !string.Equals(roomId, nextRoomId, StringComparison.Ordinal))
        {
            AbandonSeatIfHeld();
        }

        entered = true;
        roomId = nextRoomId;
        handLog.Follow(roomId);
        inlineReason = string.Empty;
        mySeat = -1;
        dealing = false;
        banking = false;
        projection.Reset();
        playback.Reset();
        seatFlow.Reset();
        turns.Forget();
        ResetDeck();
        ResetSettlement();
        motionsPrimed = false;
        Array.Clear(motions);
        _ = tables.TakeSeatOutcome();
        _ = tables.TakeIntentFailure();
        rooms.Enter(roomId);
        if (!CasinoRoomIds.IsBlackjackHouse(roomId))
        {
            tables.RefreshCard(roomId);
        }
    }

    public void Exit()
    {
        AbandonSeatIfHeld();
        Reset();
    }

    private void AbandonSeatIfHeld()
    {
        if (!HoldsSeat)
        {
            return;
        }

        tables.Abandon(roomId);
    }

    public void Reset()
    {
        if (entered)
        {
            rooms.Leave();
            seatFlow.Left();
            tables.ForgetKnock();
        }

        entered = false;
        historySheet.Close();
        mySeat = -1;
        dealing = false;
        banking = false;
        projection.Reset();
        playback.Reset();
        dealer.Clear();
        seatFlow.Reset();
        turns.Forget();
        inlineReason = string.Empty;
        ResetDeck();
        ResetSettlement();
        motionsPrimed = false;
        Array.Clear(motions);
    }

    public void OpenHistory()
    {
        historySheet.Open();
    }

    public void Gate()
    {
        historySheet.Gate();
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        historySheet.Draw(screen, ui);
    }

    public void Draw(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var delta = frame.DeltaSeconds;
        turns.StampAttention();
        ConsumeStakeResults();
        var drawList = ImGui.GetWindowDrawList();
        var safe = frame.Safe;
        var room = rooms.Room;
        var closedReason = room.ClosedReason;
        if (closedReason.Length > 0)
        {
            DrawClosed(drawList, ui, closedReason, safe, scale);
            return;
        }

        var held = room.State;
        var snapshot = held?.Snapshot;
        var state = chips.State;
        projection.Watch(rooms.AccountId);
        projection.Apply(held);
        projection.ApplyPersonal(room.Private);
        var board = projection.Board;
        mySeat = projection.MySeat;
        var nowTick = Environment.TickCount64;
        seatFlow.Observe(board, mySeat, board?.Phase ?? BlackjackPhases.Betting, nowTick);
        if (seatFlow.Reason.Length > 0)
        {
            inlineReason = seatFlow.Reason;
            seatFlow.ClearReason();
        }

        if (state is null || snapshot is null || board is null)
        {
            LoadingPulse.Draw(safe.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        var ownerId = OwnerId();
        var holdsSeat = BlackjackRules.IsSeat(mySeat) || CasinoSeatMachine.Holds(seatFlow.Stage);
        dealing = BlackjackHosting.DealsFromDeck(board, rooms.AccountId, ownerId, holdsSeat);
        banking = !dealing && BlackjackHosting.BanksFromDeck(board, rooms.AccountId, ownerId, holdsSeat);

        if (frame.SnapToTruth)
        {
            playback.Reset();
            motionsPrimed = false;
        }

        playback.Update(board, delta);
        dealer.Update(delta);
        UpdateAnnouncements(delta);
        var unreachable = room.Unreachable(nowTick);
        var veiled = unreachable && CasinoSeatMachine.Holds(seatFlow.Stage);
        if (veiled)
        {
            UiInteract.BlockThisFrame();
        }

        var localNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var deadlineRemaining = room.RemainingMilliseconds(board.DeadlineUnixMs, localNow);
        stateLabel = StateLabel(board, unreachable);
        var top = tournament.Showing(board)
            ? safe.Min.Y + Venue.TournamentOverlay.Reserve(scale)
            : safe.Min.Y;
        var seated = BlackjackRules.IsSeat(mySeat);
        var railCount = BlackjackTableLayout.RailSeatCount(mySeat, SeatLimit(board));
        var feltFloor = BlackjackTableLayout.MinimumHeight(safe.Width, railCount, seated, scale);
        var knockReserve = HostKnockBanner.Reserve(tables, roomId, safe.Width, scale);
        var knockTop = DrawHostedRow(drawList, ui, board, safe.Min.X, top, safe.Width,
            safe.Max.Y - knockReserve - feltFloor, scale);
        var feltTop = BlackjackTableLayout.FeltTop(knockTop, knockReserve, safe.Max.Y, feltFloor);
        ExpireReason(delta);
        var reason = ReasonText();
        var reasonHeight = reason.Length > 0
            ? CasinoNotice.Height(CasinoNoticeKind.Reason, string.Empty, reason, safe.Width, scale)
            : 0f;
        var noticeReserve = reasonHeight > 0f ? reasonHeight + Metrics.Space.Sm * scale : 0f;
        var feltBottom = BlackjackTableLayout.FeltBottom(feltTop, safe.Max.Y, noticeReserve, feltFloor);
        var felt = new Rect(new Vector2(safe.Min.X, feltTop), new Vector2(safe.Max.X, feltBottom));
        if (felt.Height <= 0f)
        {
            return;
        }

        BuildSeatViews(board);
        BuildRecaps(board);
        UpdateMotions(delta);
        layout.Compute(felt, railCount, seated, scale);
        DrawFelt(stage, drawList, ui, board, deadlineRemaining, delta, frame.Phase, scale);
        HostKnockBanner.Draw(drawList, ui, tables, roomId, new Vector2(safe.Min.X, knockTop), safe.Width, scale);
        SpeakForPhase(board);
        SettleHand(stage, board, frame);
        if (reasonHeight > 0f)
        {
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty, reason, safe.Min.X,
                safe.Max.Y - reasonHeight, safe.Width, scale);
        }

        DrawDeck(stage, frame, ui, state, board, snapshot, deadlineRemaining, veiled);
        if (veiled)
        {
            ReconnectVeil.Draw(drawList, frame.Body, ui,
                room.RemainingMilliseconds(projection.SeatAt(mySeat)?.HeldUntilUnixMs ?? 0, localNow), scale);
        }
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        BlackjackIdleScene.Draw(drawList, rect, idle, deltaSeconds, UiScale.Current);
    }

    private void ConsumeStakeResults()
    {
        var result = rooms.TakeStakeResult();
        if (result is not null)
        {
            inlineReason = result.Granted
                ? string.Empty
                : result.Reason.Length > 0 ? result.Reason : CasinoReasons.Unreachable;
            if (result.Granted)
            {
                CasinoSfx.Play(UiSound.ChipSlide);
                ClearPendingSides();
            }
        }

        if (rooms.TakeStakeFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }

        var notice = tables.TakeNoticeOutcome();
        if (notice is not null)
        {
            inlineReason = notice.Granted ? string.Empty : notice.Reason;
        }
    }

    private void DrawClosed(ImDrawListPtr drawList, AppSkin ui, string closedReason, Rect safe, float scale)
    {
        if (tables.TakeKnockAdmitted(roomId))
        {
            rooms.Enter(roomId);
            tables.RefreshCard(roomId);
            return;
        }

        var knock = tables.KnockFor(roomId, closedReason);
        tables.EnsureKnockFresh(roomId);
        var action = TableDoorCard.Draw(drawList, ui, closedReason, knock, L.Casino.BlackjackClosedTitle,
            L.Casino.BlackjackClosedHint, safe, scale, Currency == CasinoCurrencies.Gil);
        if (action == TableDoorAction.Knock)
        {
            tables.Knock(roomId);
            return;
        }

        if (action == TableDoorAction.GilLedger)
        {
            openGilLedger();
            return;
        }

        if (action == TableDoorAction.Leave)
        {
            leaveRoom();
        }
    }

    private string StateLabel(CasinoBlackjackRoomStateDto board, bool unreachable)
    {
        if (unreachable)
        {
            return Loc.T(L.Casino.WheelReconnecting);
        }

        var seat = projection.SeatAt(mySeat);
        if (seat is not null && !seat.Connected)
        {
            return Loc.T(L.Casino.AwayBadge);
        }

        if (seatFlow.StandQueued)
        {
            return Loc.T(L.Casino.StandQueued);
        }

        if (seatFlow.Waiting)
        {
            return Loc.T(L.Casino.DealtNextHand);
        }

        if (board.Paused)
        {
            return Loc.T(L.Tables.PausedBadge);
        }

        if (BlackjackHosting.AwaitsDeal(board))
        {
            return hostedText.WaitingDeal(board.DealerName);
        }

        if (board.InsuranceOpen)
        {
            return Loc.T(L.Blackjack.InsuranceQuestion);
        }

        if (board.Phase == BlackjackPhases.Betting)
        {
            return Loc.T(L.Casino.BlackjackWaitingForBets);
        }

        if (BlackjackPhases.Over(board.Phase))
        {
            return Loc.T(L.Casino.BlackjackHandOver);
        }

        if (board.ActiveSeat < 0)
        {
            return Loc.T(L.Casino.BlackjackDealing);
        }

        return board.ActiveSeat == mySeat ? Loc.T(L.Casino.BlackjackYourTurn) : Loc.T(L.Casino.BlackjackDealerPlays);
    }

    private static int TimerWindow(CasinoBlackjackRoomStateDto board)
    {
        if (board.InsuranceOpen)
        {
            return BlackjackRules.InsuranceSeconds;
        }

        return board.WindowSeconds > 0 ? board.WindowSeconds : board.TurnSeconds;
    }

    private float DrawHostedRow(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, float left,
        float y, float width, float limit, float scale)
    {
        if (!BlackjackHosting.SeatBanked(board))
        {
            return y;
        }

        var dealerPowers = BlackjackHosting.DealerPowers(board, rooms.AccountId, OwnerId());
        var showsDeal = dealerPowers && BlackjackHosting.HostDeals(board) && board.Phase == BlackjackPhases.Betting
                        && !dealing;
        var gil = CasinoCurrencies.Of(board) == CasinoCurrencies.Gil;
        var height = Button.LargeHeight * scale;
        var buttonGap = Metrics.Space.Sm * scale;
        var pauseLabel = board.Paused ? Loc.T(L.Tables.Resume) : Loc.T(L.Tables.Pause);
        var dealLabel = Loc.T(BlackjackHosting.DealLabel(board));
        var ledgerLabel = Loc.T(L.Tables.OpenLedger);
        var pauseWidth = dealerPowers ? Button.WidthFor(pauseLabel, ButtonSize.Large) : 0f;
        var dealWidth = showsDeal ? Button.WidthFor(dealLabel, ButtonSize.Large) : 0f;
        var ledgerWidth = gil ? Button.WidthFor(ledgerLabel, ButtonSize.Large) : 0f;
        var buttons = (dealerPowers ? 1 : 0) + (showsDeal ? 1 : 0) + (gil ? 1 : 0);
        var squeeze = HostedInfoLayout.ButtonSqueeze(pauseWidth + dealWidth + ledgerWidth, buttons, width,
            buttonGap);
        var right = left + width;
        if (dealerPowers)
        {
            var pauseRect = new Rect(new Vector2(right - pauseWidth * squeeze, y), new Vector2(right, y + height));
            if (Button.Draw(drawList, pauseRect, pauseLabel, ui.Ink, ButtonStyle.Gray,
                    enabled: !tables.IntentInFlight, id: "table.pause"))
            {
                inlineReason = string.Empty;
                tables.Pause(roomId, !board.Paused);
            }

            right = pauseRect.Min.X - buttonGap;
        }

        if (showsDeal)
        {
            var dealRect = new Rect(new Vector2(right - dealWidth * squeeze, y), new Vector2(right, y + height));
            if (Button.Draw(drawList, dealRect, dealLabel, ui.Ink, ButtonStyle.Prominent,
                    enabled: !tables.IntentInFlight && !board.Paused, id: "table.deal"))
            {
                inlineReason = string.Empty;
                tables.Deal(roomId);
                CasinoSfx.Play(UiSound.CardSnap);
            }

            right = dealRect.Min.X - buttonGap;
        }

        if (gil)
        {
            var ledgerRect = new Rect(new Vector2(right - ledgerWidth * squeeze, y), new Vector2(right, y + height));
            if (Button.Draw(drawList, ledgerRect, ledgerLabel, ui.Ink, ButtonStyle.Tinted, id: "table.ledger"))
            {
                openLedger(roomId);
            }

            right = ledgerRect.Min.X - buttonGap;
        }

        var first = gil ? hostedText.GilLimits(board) : hostedText.Rules(board);
        var second = gil && board.Rules is not null ? hostedText.Rules(board) : string.Empty;
        var gap = HostedRowGap * scale;
        var rowBottom = buttons > 0 ? y + height : y;
        var leadGap = buttons > 0 ? gap : 0f;
        var free = right - left;
        var firstBeside = buttons > 0
                          && HostedInfoLayout.FitsLine(Typography.Measure(first, TextStyles.Footnote).X, free, scale);
        var blocksHeight = (firstBeside ? 0f : leadGap + InfoBlockHeight(first, width, scale))
                           + (second.Length > 0 ? gap + InfoBlockHeight(second, width, scale) : 0f);
        var lineHeight = HostedInfoLayout.BlockHeight(Typography.LineHeight(TextStyles.Footnote), 1, scale);
        var mode = HostedInfoLayout.Mode(blocksHeight, leadGap + lineHeight, limit - gap - rowBottom,
            buttons > 0 ? free : 0f, scale);
        switch (mode)
        {
            case HostedInfoMode.Blocks:
                return DrawInfoBlocks(drawList, ui, first, second, firstBeside, gil, left, y, rowBottom, free,
                    width, height, scale) + gap;
            case HostedInfoMode.Beside:
                StageText.Status(drawList, new Vector2(left + free * 0.5f, y + height * 0.5f),
                    hostedText.Info(first, second), free, InfoMarquee, TextStyles.Footnote, true);
                return rowBottom + gap;
            case HostedInfoMode.Line:
                StageText.Status(drawList, new Vector2(left + width * 0.5f, rowBottom + leadGap + lineHeight * 0.5f),
                    hostedText.Info(first, second), width, InfoMarquee, TextStyles.Footnote, true);
                return rowBottom + leadGap + lineHeight + gap;
            default:
                return rowBottom + gap;
        }
    }

    private float DrawInfoBlocks(ImDrawListPtr drawList, AppSkin ui, string first, string second, bool firstBeside,
        bool gil, float left, float y, float rowBottom, float free, float width, float height, float scale)
    {
        var ink = gil ? TableRow.CurrencyTint(CasinoCurrencies.Gil, ui.Accent) : StageText.Strong;
        var gap = HostedRowGap * scale;
        var bottom = rowBottom;
        if (firstBeside)
        {
            StageText.Plate(drawList, new Vector2(left + free * 0.5f, y + height * 0.5f), first, free, ink,
                TextStyles.Footnote, scale);
        }
        else
        {
            bottom = DrawInfoBlock(drawList, first, left, bottom > y ? bottom + gap : bottom, width, ink, scale);
        }

        if (second.Length > 0)
        {
            bottom = DrawInfoBlock(drawList, second, left, bottom + gap, width, StageText.Strong, scale);
        }

        return bottom;
    }

    private static float InfoBlockHeight(string text, float width, float scale)
    {
        var style = TextStyles.Footnote;
        var padX = StageText.CapsulePadX * scale;
        var count = Math.Min(Typography.WrapText(text, style, MathF.Max(1f, width - padX * 2f)).Length,
            HostedInfoLayout.MaxLines);
        return count == 0 ? 0f : HostedInfoLayout.BlockHeight(Typography.LineHeight(style), count, scale);
    }

    private string OwnerId() => tables.CardFor(roomId)?.OwnerUserId ?? string.Empty;

    private static float DrawInfoBlock(ImDrawListPtr drawList, string text, float left, float top, float width,
        Vector4 ink, float scale)
    {
        var style = TextStyles.Footnote;
        var padX = StageText.CapsulePadX * scale;
        var lines = Typography.WrapText(text, style, MathF.Max(1f, width - padX * 2f));
        var count = Math.Min(lines.Length, HostedInfoLayout.MaxLines);
        if (count == 0)
        {
            return top;
        }

        var lineHeight = Typography.LineHeight(style);
        var textWidth = 0f;
        for (var index = 0; index < count; index++)
        {
            textWidth = MathF.Max(textWidth, Typography.Measure(lines[index], style).X);
        }

        var height = HostedInfoLayout.BlockHeight(lineHeight, count, scale);
        var centerX = left + width * 0.5f;
        var min = new Vector2(centerX - textWidth * 0.5f - padX, top);
        var max = new Vector2(centerX + textWidth * 0.5f + padX, top + height);
        StageText.Capsule(drawList, min, max);
        var textTop = top + (height - lineHeight * count) * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var lineWidth = Typography.Measure(lines[index], style).X;
            Typography.Draw(drawList, new Vector2(centerX - lineWidth * 0.5f, textTop + index * lineHeight),
                lines[index], ink, style);
        }

        return max.Y;
    }

    private string ReasonText() =>
        inlineReason.Length == 0 ? string.Empty : CasinoReasons.Text(inlineReason, chips.Ceiling.MaxBet);

    private void ExpireReason(float delta)
    {
        if (inlineReason.Length == 0)
        {
            shownReason = string.Empty;
            return;
        }

        if (!ReferenceEquals(inlineReason, shownReason))
        {
            shownReason = inlineReason;
            reasonClock = 0f;
            return;
        }

        reasonClock += delta;
        if (reasonClock < ReasonSeconds)
        {
            return;
        }

        inlineReason = string.Empty;
        shownReason = string.Empty;
    }

    private void BuildSeatViews(CasinoBlackjackRoomStateDto board)
    {
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            var seat = projection.SeatAt(seatIndex);
            if (seat is null || seat.State == BlackjackSeatStates.Empty)
            {
                seatViews[seatIndex] = new SeatView(seatIndex, string.Empty, string.Empty, string.Empty, 0, 0,
                    SeatPhase.Empty, false, true);
                continue;
            }

            var phase = board.ActiveSeat == seatIndex
                ? SeatPhase.Acting
                : BlackjackRules.PhaseOf(seat.State, seat.Connected, seat.JoinsNextHand, seat.Committed > 0);
            seatViews[seatIndex] = new SeatView(seatIndex, seat.DisplayName, seat.AvatarUrl, seat.FrameId,
                seat.Chips, seat.Committed, phase, seatIndex == mySeat, seat.Connected);
        }
    }

    private void BuildRecaps(CasinoBlackjackRoomStateDto board)
    {
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            recaps[seatIndex] = BlackjackPhases.Over(board.Phase)
                ? BlackjackRecap.Of(projection.SeatAt(seatIndex), projection.HandsAt(seatIndex))
                : default;
        }
    }

    private void UpdateMotions(float delta)
    {
        var snap = !motionsPrimed;
        motionsPrimed = true;
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            ref var motion = ref motions[seatIndex];
            var view = seatViews[seatIndex];
            if (view.Phase == SeatPhase.Empty)
            {
                motion = default;
                continue;
            }

            var settled = recaps[seatIndex].Settled;
            if (snap)
            {
                motion.ShownBet = view.Bet;
                motion.BetClock = SnappedClock;
                motion.SettleStarted = settled;
                motion.SettleClock = SnappedClock;
                motion.SettleSign = 0;
                continue;
            }

            if (view.Bet > motion.ShownBet)
            {
                motion.ShownBet = view.Bet;
                motion.BetClock = 0f;
            }
            else if (view.Bet < motion.ShownBet)
            {
                motion.ShownBet = view.Bet;
                motion.BetClock = SnappedClock;
            }

            motion.BetClock += delta;
            if (!settled)
            {
                motion.SettleStarted = false;
                motion.SettleClock = 0f;
                motion.SettleSign = 0;
                continue;
            }

            if (!motion.SettleStarted)
            {
                motion.SettleStarted = true;
                motion.SettleClock = 0f;
                motion.SettleSign = recaps[seatIndex].MainSign;
                continue;
            }

            motion.SettleClock += delta;
        }
    }

    internal static int SeatLimit(CasinoBlackjackRoomStateDto board)
    {
        var count = board.Seats?.Length ?? 0;
        return count > 0 ? Math.Min(count, BlackjackRules.SeatCount) : BlackjackRules.SeatCount;
    }

    private int FirstOpenSeat(CasinoBlackjackRoomStateDto board)
    {
        var limit = SeatLimit(board);
        for (var seatIndex = 0; seatIndex < limit; seatIndex++)
        {
            var seat = projection.SeatAt(seatIndex);
            if (seat is null || seat.State == BlackjackSeatStates.Empty)
            {
                return seatIndex;
            }
        }

        return -1;
    }
}
