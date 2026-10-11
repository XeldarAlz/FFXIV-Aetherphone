using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
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
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly BlackjackSeatFlow seatFlow;
    private readonly Action leaveRoom;
    private readonly Action<string> openLedger;
    private readonly BlackjackHostedText hostedText = new();
    private readonly CasinoTextCache text = new();
    private readonly BlackjackProjection projection = new();
    private readonly BlackjackDealPlayback playback = new();
    private readonly BlackjackDealer dealer = new();
    private readonly BlackjackIdleScript idle = new();
    private readonly BlackjackTableLayout layout = new();
    private readonly SeatView[] seatViews = new SeatView[BlackjackRules.SeatCount];
    private readonly SeatMotion[] motions = new SeatMotion[BlackjackRules.SeatCount];
    private readonly BlackjackSeatRecap[] recaps = new BlackjackSeatRecap[BlackjackRules.SeatCount];
    private readonly Spring[] heroRaise = new Spring[BlackjackRules.MaxHandsPerSeat];

    private int mySeat = -1;
    private string roomId = CasinoRoomIds.BlackjackPit;
    private string inlineReason = string.Empty;
    private string shownReason = string.Empty;
    private float reasonClock;
    private bool entered;
    private bool motionsPrimed;

    public BlackjackTable(CasinoStore chips, CasinoRoomsStore rooms, CasinoTablesStore tables,
        CasinoHistoryStore history, CasinoTurnNotifier turns, RemoteImageCache images, LodestoneService lodestone,
        Action leaveRoom, Action<string> openLedger)
    {
        this.chips = chips;
        this.rooms = rooms;
        this.tables = tables;
        this.history = history;
        this.turns = turns;
        this.images = images;
        this.lodestone = lodestone;
        this.leaveRoom = leaveRoom;
        this.openLedger = openLedger;
        seatFlow = new BlackjackSeatFlow(tables);
        composer = new BetComposer("##blackjackBet");
        gilComposer = new ClassicBetComposer("##blackjackGilBet");
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

    public void Enter(string tableId)
    {
        var nextRoomId = tableId.Length > 0 ? tableId : CasinoRoomIds.BlackjackPit;
        if (entered && !string.Equals(roomId, nextRoomId, StringComparison.Ordinal))
        {
            AbandonSeatIfHeld();
        }

        entered = true;
        roomId = nextRoomId;
        inlineReason = string.Empty;
        mySeat = -1;
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
        if (!entered || seatFlow.StandQueued)
        {
            return;
        }

        if (!BlackjackRules.IsSeat(mySeat) && !CasinoSeatMachine.Holds(seatFlow.Stage))
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
        }

        entered = false;
        mySeat = -1;
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
        var top = Venue.TournamentOverlay.Shows(board)
            ? safe.Min.Y + Venue.TournamentOverlay.Reserve(scale)
            : safe.Min.Y;
        var feltTop = DrawHostedRow(drawList, ui, board, safe.Min.X, top, safe.Width, scale);
        ExpireReason(delta);
        var reason = ReasonText();
        var reasonHeight = reason.Length > 0
            ? CasinoNotice.Height(CasinoNoticeKind.Reason, string.Empty, reason, safe.Width, scale)
            : 0f;
        var feltBottom = reasonHeight > 0f ? safe.Max.Y - reasonHeight - Metrics.Space.Sm * scale : safe.Max.Y;
        var felt = new Rect(new Vector2(safe.Min.X, feltTop), new Vector2(safe.Max.X, feltBottom));
        if (felt.Height <= 0f)
        {
            return;
        }

        BuildSeatViews(board);
        BuildRecaps(board);
        UpdateMotions(delta);
        layout.Compute(felt, BlackjackTableLayout.RailSeatCount(mySeat, SeatLimit(board)),
            BlackjackRules.IsSeat(mySeat), scale);
        DrawFelt(stage, drawList, ui, board, deadlineRemaining, delta, frame.Phase, scale);
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
        var hint = Loc.T(CasinoReasons.TryMessage(closedReason, out var known) ? known : L.Casino.BlackjackClosedHint);
        var asks = AsksToJoin(closedReason);
        var title = Loc.T(asks ? L.Casino.BlackjackDoorTitle : L.Casino.BlackjackClosedTitle);
        var action = Loc.T(asks ? L.Casino.BlackjackAskToJoin : L.Casino.WheelBackToFloor);
        CasinoNotice.DrawWithAction(drawList, ui, CasinoNoticeKind.Card, title, hint, action, safe.Min.X,
            safe.Min.Y + Metrics.Space.Lg * scale, safe.Width, scale, out var pressed);
        if (!pressed)
        {
            return;
        }

        if (asks)
        {
            tables.Knock(roomId);
            return;
        }

        leaveRoom();
    }

    internal static bool AsksToJoin(string reason)
    {
        return string.Equals(reason, CasinoReasons.InviteOnly, StringComparison.Ordinal)
            || string.Equals(reason, CasinoReasons.NotMember, StringComparison.Ordinal)
            || string.Equals(reason, CasinoReasons.Denied, StringComparison.Ordinal);
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
        float y, float width, float scale)
    {
        if (!BlackjackHosting.SeatBanked(board))
        {
            return y;
        }

        var owner = tables.CardFor(roomId)?.OwnerUserId ?? string.Empty;
        var dealerPowers = BlackjackHosting.DealerPowers(board, rooms.AccountId, owner);
        var gil = CasinoCurrencies.Of(board) == CasinoCurrencies.Gil;
        var height = Button.LargeHeight * scale;
        var right = left + width;
        if (dealerPowers)
        {
            var pauseLabel = board.Paused ? Loc.T(L.Tables.Resume) : Loc.T(L.Tables.Pause);
            var pauseWidth = Button.WidthFor(pauseLabel, ButtonSize.Large);
            var pauseRect = new Rect(new Vector2(right - pauseWidth, y), new Vector2(right, y + height));
            if (Button.Draw(drawList, pauseRect, pauseLabel, ui.Ink, ButtonStyle.Gray,
                    enabled: !tables.IntentInFlight, id: "table.pause"))
            {
                inlineReason = string.Empty;
                tables.Pause(roomId, !board.Paused);
            }

            right = pauseRect.Min.X - Metrics.Space.Sm * scale;
            if (BlackjackHosting.HostDeals(board) && board.Phase == BlackjackPhases.Betting)
            {
                var dealLabel = Loc.T(L.Tables.Deal);
                var dealWidth = Button.WidthFor(dealLabel, ButtonSize.Large);
                var dealRect = new Rect(new Vector2(right - dealWidth, y), new Vector2(right, y + height));
                if (Button.Draw(drawList, dealRect, dealLabel, ui.Ink, ButtonStyle.Prominent,
                        enabled: !tables.IntentInFlight && !board.Paused, id: "table.deal"))
                {
                    inlineReason = string.Empty;
                    tables.Deal(roomId);
                    CasinoSfx.Play(UiSound.CardSnap);
                }

                right = dealRect.Min.X - Metrics.Space.Sm * scale;
            }
        }

        if (gil)
        {
            var ledgerLabel = Loc.T(L.Tables.OpenLedger);
            var ledgerWidth = Button.WidthFor(ledgerLabel, ButtonSize.Large);
            var ledgerRect = new Rect(new Vector2(right - ledgerWidth, y), new Vector2(right, y + height));
            if (Button.Draw(drawList, ledgerRect, ledgerLabel, ui.Ink, ButtonStyle.Tinted, id: "table.ledger"))
            {
                openLedger(roomId);
            }

            right = ledgerRect.Min.X - Metrics.Space.Sm * scale;
        }

        var info = gil ? hostedText.GilLimits(board) : hostedText.Rules(board);
        var ink = gil ? TableRow.CurrencyTint(CasinoCurrencies.Gil, ui.Accent) : StageText.Strong;
        var gap = HostedRowGap * scale;
        var bottom = y;
        if (right < left + width)
        {
            bottom = y + height;
            var free = right - left;
            if (HostedInfoLayout.FitsLine(Typography.Measure(info, TextStyles.Footnote).X, free, scale))
            {
                StageText.Plate(drawList, new Vector2(left + free * 0.5f, y + height * 0.5f), info, free, ink,
                    TextStyles.Footnote, scale);
                info = string.Empty;
            }
        }

        if (info.Length > 0)
        {
            bottom = DrawInfoBlock(drawList, info, left, bottom > y ? bottom + gap : bottom, width, ink, scale);
        }

        if (gil && board.Rules is not null)
        {
            bottom = DrawInfoBlock(drawList, hostedText.Rules(board), left, bottom + gap, width, StageText.Strong,
                scale);
        }

        return bottom + gap;
    }

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
                motion.SettleSign = recaps[seatIndex].Sign;
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
