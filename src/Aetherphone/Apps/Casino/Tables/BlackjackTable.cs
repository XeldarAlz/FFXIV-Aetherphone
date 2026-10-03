using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class BlackjackTable
{
    private const float PadX = 16f;
    private const float StatusRowHeight = 22f;
    private const float BannerHeight = 30f;
    private const float ActionBarHeight = 46f;
    private const float BetFlightSeconds = 0.4f;
    private const float PlatePopSeconds = 0.2f;
    private const float SettleFlightSeconds = 0.55f;
    private const float BadgePopSeconds = 0.25f;
    private const float HeroRaiseSmoothing = 0.12f;
    private const float HeroRaiseUnits = 8f;
    private const float SnappedClock = 10f;

    private static readonly Vector4 Gold = new(1f, 0.84f, 0.42f, 1f);
    private static readonly Vector4 PillFill = new(0f, 0f, 0f, 0.35f);

    private static readonly Vector4[] ConfettiPalette =
    {
        new(1.00f, 0.84f, 0.42f, 1f),
        new(1.00f, 0.95f, 0.75f, 1f),
        new(0.55f, 0.92f, 0.88f, 1f),
        new(0.80f, 0.58f, 0.98f, 1f),
    };

    private static readonly int[] ActionBits =
    {
        BlackjackRules.ActionHit,
        BlackjackRules.ActionStand,
        BlackjackRules.ActionDouble,
        BlackjackRules.ActionSplit,
    };

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
    private readonly CasinoTurnNotifier turns;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly BlackjackSeatFlow seatFlow;
    private readonly Action openCashier;
    private readonly Action leaveRoom;
    private readonly Action knock;
    private readonly BlackjackProjection projection = new();
    private readonly BlackjackDealPlayback playback = new();
    private readonly DealerBubble dealer = new();
    private readonly BetComposer composer = new("##blackjackBet");
    private readonly ParticleSystem particles = new(160);
    private readonly SeatView[] seatViews = new SeatView[BlackjackRules.SeatCount];
    private readonly SeatMotion[] motions = new SeatMotion[BlackjackRules.SeatCount];
    private readonly Spring[] heroRaise = new Spring[BlackjackRules.MaxHandsPerSeat];

    private RollingValue winRoll;
    private RollingValue stackRoll;
    private int mySeat = -1;
    private string roomId = CasinoRoomIds.BlackjackPit;
    private string inlineReason = string.Empty;
    private string celebratedHandId = string.Empty;
    private string spokenHandId = string.Empty;
    private long settledDelta;
    private int spokenPhase = -1;
    private int spokenSeat = int.MinValue;
    private bool entered;
    private bool motionsPrimed;

    public BlackjackTable(CasinoStore chips, CasinoRoomsStore rooms, CasinoTablesStore tables,
        CasinoTurnNotifier turns, RemoteImageCache images, LodestoneService lodestone, Action openCashier,
        Action leaveRoom)
    {
        this.chips = chips;
        this.rooms = rooms;
        this.tables = tables;
        this.turns = turns;
        this.images = images;
        this.lodestone = lodestone;
        this.openCashier = openCashier;
        this.leaveRoom = leaveRoom;
        knock = Knock;
        seatFlow = new BlackjackSeatFlow(tables);
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
        composer.Reset(BlackjackRules.MinBet);
        motionsPrimed = false;
        Array.Clear(motions);
        _ = tables.TakeSeatOutcome();
        _ = tables.TakeIntentFailure();
        rooms.Enter(roomId);
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
        particles.Clear();
        seatFlow.Reset();
        turns.Forget();
        inlineReason = string.Empty;
        celebratedHandId = string.Empty;
        spokenHandId = string.Empty;
        spokenPhase = -1;
        spokenSeat = int.MinValue;
        settledDelta = 0;
        motionsPrimed = false;
        Array.Clear(motions);
        winRoll.Snap(0);
        stackRoll.Snap(0);
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        turns.StampAttention();
        ConsumeStakeResults();

        var room = rooms.Room;
        var closedReason = room.ClosedReason;
        var pad = PadX * scale;
        var left = body.Min.X + pad;
        var width = body.Width - pad * 2f;
        var drawList = ImGui.GetWindowDrawList();

        if (closedReason.Length > 0)
        {
            DrawClosedNotice(drawList, ui, closedReason, left, body.Min.Y + Metrics.Space.Lg * scale, width, scale);
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
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        playback.Update(board, delta);
        particles.Update(delta);
        dealer.Update(delta);

        var unreachable = room.Unreachable(nowTick);
        var veiled = unreachable && CasinoSeatMachine.Holds(seatFlow.Stage);
        if (veiled)
        {
            UiInteract.BlockThisFrame();
        }

        var localNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var deadlineRemaining = room.RemainingMilliseconds(board.DeadlineUnixMs, localNow);

        var y = body.Min.Y + Metrics.Space.Xs * scale;
        y = DrawStatusRow(drawList, ui, snapshot, board, !unreachable, left, y, width, scale);

        var footerHeight = FooterHeightFor(board.Phase, scale);
        var feltMin = new Vector2(left, y + Metrics.Space.Xs * scale);
        var feltMax = new Vector2(left + width, body.Max.Y - footerHeight - Metrics.Space.Md * scale);
        if (feltMax.Y <= feltMin.Y)
        {
            return;
        }

        var felt = new Rect(feltMin, feltMax);
        FeltPanel.Draw(drawList, felt, ui.Accent, scale);
        BlackjackTableArt.DrawShoe(drawList, BlackjackTableLayout.ShoeAnchor(felt, scale), scale);
        BuildSeatViews(board);
        UpdateMotions(delta);
        DrawDealer(drawList, ui, board, felt, scale);
        var tapped = DrawRail(drawList, ui, board, felt, deadlineRemaining, scale);
        if (BlackjackRules.IsSeat(tapped) && seatViews[tapped].Phase == SeatPhase.Empty)
        {
            TapEmptySeat(tapped, state, board);
        }

        DrawHero(drawList, ui, board, felt, deadlineRemaining, delta, scale);
        SpeakForPhase(board);
        dealer.Draw(drawList, BlackjackTableLayout.BubbleAnchor(felt), ui, scale);
        CelebrateSettledHand(board, felt, scale);

        var footerTop = felt.Max.Y + Metrics.Space.Md * scale;
        DrawFooter(drawList, ui, state, board, snapshot, deadlineRemaining, delta, left, footerTop, width, scale,
            veiled);
        particles.Draw(drawList, scale);

        if (veiled)
        {
            ReconnectVeil.Draw(drawList, body, ui,
                room.RemainingMilliseconds(projection.SeatAt(mySeat)?.HeldUntilUnixMs ?? 0, localNow), scale);
        }
    }

    private void ConsumeStakeResults()
    {
        var result = rooms.TakeStakeResult();
        if (result is not null)
        {
            inlineReason = result.Granted
                ? string.Empty
                : result.Reason.Length > 0 ? result.Reason : CasinoReasons.Unreachable;
        }

        if (rooms.TakeStakeFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }
    }

    private void DrawClosedNotice(ImDrawListPtr drawList, AppSkin ui, string closedReason, float left, float y,
        float width, float scale)
    {
        var hint = Loc.T(CasinoReasons.TryMessage(closedReason, out var known) ? known : L.Casino.BlackjackClosedHint);
        if (AsksToJoin(closedReason))
        {
            DrawNotice(drawList, ui, Loc.T(L.Casino.BlackjackDoorTitle), hint, Loc.T(L.Casino.BlackjackAskToJoin),
                knock, left, y, width, scale);
            return;
        }

        DrawNotice(drawList, ui, Loc.T(L.Casino.BlackjackClosedTitle), hint, Loc.T(L.Casino.WheelBackToFloor),
            leaveRoom, left, y, width, scale);
    }

    internal static bool AsksToJoin(string reason)
    {
        return string.Equals(reason, CasinoReasons.InviteOnly, StringComparison.Ordinal)
            || string.Equals(reason, CasinoReasons.NotMember, StringComparison.Ordinal)
            || string.Equals(reason, CasinoReasons.Denied, StringComparison.Ordinal);
    }

    private void Knock()
    {
        tables.Knock(roomId);
    }

    private static float FooterHeightFor(int phase, float scale)
    {
        return phase == BlackjackPhases.Betting
            ? BannerHeight * scale + BetComposer.HeightFor(scale)
            : BannerHeight * scale + ActionBarHeight * scale;
    }

    private float DrawStatusRow(ImDrawListPtr drawList, AppSkin ui, CasinoRoomSnapshotDto snapshot,
        CasinoBlackjackRoomStateDto board, bool reachable, float left, float y, float width, float scale)
    {
        var height = StatusRowHeight * scale;
        var players = SeatedCount(board);
        var watching = snapshot.Occupancy > players ? snapshot.Occupancy - players : 0;
        var seated = watching > 0
            ? Loc.T(L.Casino.BlackjackAtTheTableWatching, GameNumber.Label(players),
                GameNumber.Label(watching))
            : Loc.T(L.Casino.BlackjackAtTheTable, GameNumber.Label(players));
        Typography.Draw(drawList, new Vector2(left, y + 4f * scale), seated, ui.MutedInk, TextStyles.Caption1);
        if (reachable && AwayAtMySeat())
        {
            AwayBadge.Draw(drawList, new Vector2(left + width, y), ui, Loc.T(L.Casino.AwayBadge), scale);
            return y + height;
        }

        if (reachable)
        {
            var rules = Loc.T(L.Casino.BlackjackRules);
            var rulesWidth = Typography.Measure(rules, TextStyles.Caption2).X;
            var seatedWidth = Typography.Measure(seated, TextStyles.Caption1).X;
            var room = width - seatedWidth - Metrics.Space.Md * scale;
            if (rulesWidth <= room)
            {
                Typography.Draw(drawList, new Vector2(left + width - rulesWidth, y + 5f * scale), rules,
                    ui.MutedInk, TextStyles.Caption2);
                return y + height;
            }

            var rulesHeight = Typography.DrawWrappedLeft(new Vector2(left, y + height), rules, ui.MutedInk,
                TextStyles.Caption2, width);
            return y + height + rulesHeight;
        }

        var label = Loc.T(L.Casino.WheelReconnecting);
        var labelSize = Typography.Measure(label, TextStyles.Caption2);
        var chipMax = new Vector2(left + width, y + height - 2f * scale);
        var chipMin = new Vector2(chipMax.X - labelSize.X - 18f * scale, y);
        var chipRounding = (chipMax.Y - chipMin.Y) * 0.5f;
        Squircle.Fill(drawList, chipMin, chipMax, chipRounding, ImGui.GetColorU32(ui.FieldSurface));
        var dotCenter = new Vector2(chipMin.X + 8f * scale, (chipMin.Y + chipMax.Y) * 0.5f);
        drawList.AddCircleFilled(dotCenter, 2.6f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f + 0.45f * Pulse.Wave(Pulse.Breath))), 12);
        Typography.Draw(drawList, new Vector2(dotCenter.X + 6f * scale, chipMin.Y + 3f * scale), label, ui.MutedInk,
            TextStyles.Caption2);
        return y + height;
    }

    private void TapEmptySeat(int seatIndex, CasinoStateDto state, CasinoBlackjackRoomStateDto board)
    {
        var rack = CasinoWire.SittingFor(state, CasinoWire.BlackjackKind);
        if (rack is not null)
        {
            inlineReason = string.Empty;
            seatFlow.Sit(roomId, seatIndex, rack.Stack, board.Phase);
            return;
        }

        var buyIn = RackFor(state, board);
        if (buyIn <= 0)
        {
            openCashier();
            return;
        }

        inlineReason = string.Empty;
        seatFlow.Sit(roomId, seatIndex, buyIn, board.Phase);
    }

    private static long RackFor(CasinoStateDto state, CasinoBlackjackRoomStateDto board)
    {
        return BlackjackRules.RackFor(board.MaxBet, state.MinBuyIn, state.MaxBuyIn,
            state.Sitting?.Stack ?? 0);
    }

    private bool AwayAtMySeat()
    {
        var seat = projection.SeatAt(mySeat);
        return seat is not null && !seat.Connected;
    }

    private static int SeatedCount(CasinoBlackjackRoomStateDto board)
    {
        var seats = board.Seats;
        if (seats is null)
        {
            return 0;
        }

        var taken = 0;
        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].State != BlackjackSeatStates.Empty)
            {
                taken++;
            }
        }

        return taken;
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

            var hands = projection.HandsAt(seatIndex);
            var settled = hands.Length > 0 && Settled(hands);
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
                var total = 0L;
                for (var handIndex = 0; handIndex < hands.Length; handIndex++)
                {
                    total += hands[handIndex].Delta;
                }

                motion.SettleSign = total > 0 ? 1 : total < 0 ? -1 : 0;
                continue;
            }

            motion.SettleClock += delta;
        }
    }

    private void DrawDealer(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, in Rect felt,
        float scale)
    {
        var fanCenter = BlackjackTableLayout.DealerFanCenter(felt);
        var cards = board.DealerCards;
        var count = cards?.Length ?? 0;
        if (count == 0)
        {
            return;
        }

        var cardWidth = BlackjackTableLayout.DealerCardWidth * scale;
        var step = BlackjackTableLayout.FanStep(cardWidth, count, felt.Width * 0.6f);
        var start = fanCenter.X - (count - 1) * step * 0.5f;
        var shoe = BlackjackTableLayout.ShoeAnchor(felt, scale);
        var rounding = PlayingCards.RoundingFor(cardWidth);
        var reveal = playback.HoleReveal();
        for (var index = 0; index < count; index++)
        {
            var travel = playback.TravelOf(BlackjackDealPlayback.DealerSlot, index);
            if (travel <= 0f)
            {
                continue;
            }

            var target = new Vector2(start + index * step, fanCenter.Y);
            var card = cards![index];
            if (index == 1 && playback.HoleRevealing())
            {
                var squashed = SquashedRect(target, cardWidth, BlackjackDealChoreography.RevealScaleX(reveal));
                if (BlackjackDealChoreography.RevealFaceUp(reveal) && PlayingCards.IsCard(card))
                {
                    PlayingCards.DrawFace(drawList, squashed, card, rounding, scale, true);
                }
                else
                {
                    PlayingCards.DrawBack(drawList, squashed, rounding, scale, true);
                }

                continue;
            }

            var center = BlackjackDealChoreography.Position(shoe, target, travel, scale);
            var rect = BlackjackDealChoreography.CardRect(center, cardWidth, travel);
            if (BlackjackDealChoreography.FaceUp(travel) && PlayingCards.IsCard(card))
            {
                PlayingCards.DrawFace(drawList, rect, card, rounding, scale, true);
            }
            else
            {
                PlayingCards.DrawBack(drawList, rect, rounding, scale, true);
            }
        }

        if (board.DealerTotal <= 0)
        {
            return;
        }

        var pillCenter = new Vector2(fanCenter.X,
            fanCenter.Y + PlayingCards.HeightFor(cardWidth) * 0.5f + BlackjackTableLayout.DealerTotalDrop * scale);
        BlackjackTableArt.DrawTotalPill(drawList, pillCenter, GameNumber.Label(board.DealerTotal), PillFill,
            ui.TitleInk, scale);
    }

    private static Rect SquashedRect(Vector2 center, float width, float scaleX)
    {
        var halfWidth = width * 0.5f * scaleX;
        var halfHeight = PlayingCards.HeightFor(width) * 0.5f;
        return new Rect(new Vector2(center.X - halfWidth, center.Y - halfHeight),
            new Vector2(center.X + halfWidth, center.Y + halfHeight));
    }

    private int DrawRail(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, in Rect felt,
        long turnRemaining, float scale)
    {
        var railCount = BlackjackTableLayout.RailSeatCount(mySeat);
        var columnWidth = BlackjackTableLayout.RailColumnWidth(felt, railCount, scale);
        var puckRadius = BlackjackTableLayout.RailPuckRadius * scale;
        var shoe = BlackjackTableLayout.ShoeAnchor(felt, scale);
        var dealerAnchor = BlackjackTableLayout.DealerFanCenter(felt);
        var seated = BlackjackRules.IsSeat(mySeat);
        var corner = new Vector2(puckRadius, puckRadius);
        var tapped = -1;
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            if (seated && seatIndex == mySeat)
            {
                continue;
            }

            var slot = BlackjackTableLayout.RailSlotOf(seatIndex, mySeat);
            var puck = BlackjackTableLayout.RailPuckCenter(felt, slot, railCount, scale);
            var view = seatViews[seatIndex];
            var hovered = CircleHovered(puck, puckRadius);
            if (view.Phase == SeatPhase.Empty)
            {
                BlackjackTableArt.DrawGhostSeat(drawList, puck, puckRadius, ui.FieldSurface, ui.MutedInk, hovered,
                    scale);
                if (UiInteract.Click(puck - corner, puck + corner, hovered))
                {
                    tapped = seatIndex;
                }

                continue;
            }

            var acting = view.Phase == SeatPhase.Acting;
            if (acting)
            {
                BlackjackTableArt.DrawActingGlow(drawList, puck, puckRadius * 2.2f, ui.Accent);
            }

            var dimmed = view.Phase == SeatPhase.Away || view.Phase == SeatPhase.Out || !view.Connected;
            AvatarView.DrawRemote(drawList, puck, puckRadius, ui.Theme, view.DisplayName, string.Empty,
                view.AvatarUrl, images, lodestone, 1f, 32, dimmed ? 0.45f : 1f, Frames.Of(view.FrameId));
            if (acting)
            {
                TurnTimerRing.Draw(drawList, puck, puckRadius + 4f * scale, turnRemaining, board.WindowSeconds,
                    ui.Accent, scale);
            }

            if (!view.Connected)
            {
                drawList.AddCircleFilled(new Vector2(puck.X + puckRadius * 0.7f, puck.Y - puckRadius * 0.7f),
                    3.4f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.45f + 0.4f * Pulse.Wave(Pulse.Breath))), 12);
            }

            var textWidth = columnWidth - 4f * scale;
            var name = Typography.FitText(view.DisplayName, textWidth, TextStyles.Caption2);
            Typography.DrawCentered(drawList,
                new Vector2(puck.X, puck.Y + BlackjackTableLayout.RailNameDrop * scale), name,
                dimmed ? ui.MutedInk : ui.BodyInk, TextStyles.Caption2);
            Typography.DrawCentered(drawList,
                new Vector2(puck.X, puck.Y + BlackjackTableLayout.RailStackDrop * scale),
                NumberText.Group(view.Stack), dimmed ? ui.MutedInk : Gold, TextStyles.Caption2);

            DrawBetDisplay(drawList, ui, seatIndex, puck,
                new Vector2(puck.X, puck.Y - BlackjackTableLayout.RailBetLift * scale), dealerAnchor, false, scale);
            DrawRailHands(drawList, ui, board, seatIndex, puck, columnWidth, shoe, scale);
        }

        return tapped;
    }

    private void DrawRailHands(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, int seatIndex,
        Vector2 puck, float columnWidth, Vector2 shoe, float scale)
    {
        var hands = projection.HandsAt(seatIndex);
        if (hands.Length == 0)
        {
            return;
        }

        var count = hands.Length < BlackjackRules.MaxHandsPerSeat ? hands.Length : BlackjackRules.MaxHandsPerSeat;
        var cardWidth = (count > 1 ? BlackjackTableLayout.RailSplitCardWidth : BlackjackTableLayout.RailCardWidth)
            * scale;
        var handWidth = columnWidth / count;
        var fanY = puck.Y - BlackjackTableLayout.RailCardsLift * scale;
        var activeHand = board.ActiveSeat == seatIndex ? board.ActiveHand : -1;
        for (var handIndex = 0; handIndex < count; handIndex++)
        {
            var hand = hands[handIndex];
            var fanCenter = new Vector2(puck.X + (handIndex - (count - 1) * 0.5f) * handWidth, fanY);
            DrawHandFan(drawList, seatIndex, handIndex, hand, fanCenter, cardWidth, handWidth - 3f * scale, shoe,
                scale);
            var outcomeText = OutcomeLabel(hand);
            var badgeEntrance = BadgeEntrance(seatIndex);
            var badgeShown = hand.Outcome != BlackjackOutcomes.Pending && outcomeText.Length > 0
                && badgeEntrance > 0f;
            if (badgeShown)
            {
                var won = hand.Delta > 0;
                BlackjackTableArt.DrawOutcomeBadge(drawList,
                    new Vector2(fanCenter.X, puck.Y - BlackjackTableLayout.RailBadgeLift * scale), outcomeText,
                    won ? Gold : ui.TitleInk, won ? Gold : ui.BodyInk, badgeEntrance, scale);
            }
            else if (hand.Total > 0)
            {
                var ink = hand.Outcome == BlackjackOutcomes.Bust
                    ? ui.MutedInk
                    : handIndex == activeHand ? ui.Accent : ui.TitleInk;
                BlackjackTableArt.DrawTotalPill(drawList,
                    new Vector2(fanCenter.X, puck.Y - BlackjackTableLayout.RailTotalLift * scale),
                    GameNumber.Label(hand.Total), PillFill, ink, scale);
            }
        }
    }

    private void DrawHandFan(ImDrawListPtr drawList, int seatIndex, int handIndex, CasinoBlackjackHandDto hand,
        Vector2 fanCenter, float cardWidth, float maxWidth, Vector2 shoe, float scale)
    {
        var cards = hand.Cards;
        var count = cards?.Length ?? 0;
        if (count == 0)
        {
            return;
        }

        var step = BlackjackTableLayout.FanStep(cardWidth, count, maxWidth);
        var start = fanCenter.X - (count - 1) * step * 0.5f;
        var slot = BlackjackDealPlayback.SlotOf(seatIndex, handIndex);
        var rounding = PlayingCards.RoundingFor(cardWidth);
        for (var index = 0; index < count; index++)
        {
            var travel = playback.TravelOf(slot, index);
            if (travel <= 0f)
            {
                continue;
            }

            var target = new Vector2(start + index * step, fanCenter.Y);
            var center = BlackjackDealChoreography.Position(shoe, target, travel, scale);
            var rect = BlackjackDealChoreography.CardRect(center, cardWidth, travel);
            var card = projection.CardAt(seatIndex, handIndex, index, cards![index]);
            if (BlackjackDealChoreography.FaceUp(travel) && PlayingCards.IsCard(card))
            {
                PlayingCards.DrawFace(drawList, rect, card, rounding, scale, true);
            }
            else
            {
                PlayingCards.DrawBack(drawList, rect, rounding, scale, true);
            }
        }
    }

    private void DrawHero(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board, in Rect felt,
        long turnRemaining, float delta, float scale)
    {
        var fanY = BlackjackTableLayout.HeroFanY(felt);
        if (!BlackjackRules.IsSeat(mySeat))
        {
            DrawHeroGhostSlots(drawList, felt, fanY, scale);
            return;
        }

        var puckCenter = DrawCapsule(drawList, ui, board, BlackjackTableLayout.CapsuleCenter(felt, scale),
            turnRemaining, delta, scale);
        var hands = projection.HandsAt(mySeat);
        var shoe = BlackjackTableLayout.ShoeAnchor(felt, scale);
        var myTurn = board.ActiveSeat == mySeat && board.Phase == BlackjackPhases.PlayerTurns;
        var cardWidth = BlackjackTableLayout.HeroCardWidth(hands.Length) * scale;
        var slotWidth = BlackjackTableLayout.HeroSlotWidth(felt, hands.Length, scale);
        var fanHalfHeight = PlayingCards.HeightFor(cardWidth) * 0.5f;
        if (hands.Length == 0)
        {
            DrawHeroGhostSlots(drawList, felt, fanY, scale);
        }

        for (var handIndex = 0; handIndex < hands.Length && handIndex < heroRaise.Length; handIndex++)
        {
            var hand = hands[handIndex];
            var active = myTurn && board.ActiveHand == handIndex;
            var raise = heroRaise[handIndex].Step(active ? -HeroRaiseUnits * scale : 0f, HeroRaiseSmoothing, delta);
            var fanCenter = BlackjackTableLayout.HeroHandCenter(felt, hands.Length, handIndex, scale);
            fanCenter.Y += raise;
            if (active)
            {
                BlackjackTableArt.DrawActingGlow(drawList, fanCenter, cardWidth * 1.5f, ui.Accent);
            }

            DrawHandFan(drawList, mySeat, handIndex, hand, fanCenter, cardWidth,
                slotWidth - BlackjackTableLayout.HeroSlotPad * scale, shoe, scale);
            if (hand.Total > 0)
            {
                var fill = active ? Palette.WithAlpha(ui.Accent, 0.30f) : PillFill;
                var ink = hand.Outcome == BlackjackOutcomes.Bust
                    ? ui.MutedInk
                    : hand.Total == BlackjackRules.TargetTotal ? Gold : ui.TitleInk;
                BlackjackTableArt.DrawTotalPill(drawList,
                    new Vector2(fanCenter.X,
                        fanCenter.Y + fanHalfHeight + BlackjackTableLayout.HeroTotalDrop * scale),
                    GameNumber.Label(hand.Total), fill, ink, scale);
            }

            var outcomeText = OutcomeLabel(hand);
            if (hand.Outcome != BlackjackOutcomes.Pending && outcomeText.Length > 0)
            {
                var won = hand.Delta > 0;
                BlackjackTableArt.DrawOutcomeBadge(drawList, fanCenter, outcomeText, won ? Gold : ui.TitleInk,
                    won ? Gold : ui.BodyInk, BadgeEntrance(mySeat), scale);
            }
        }

        var chipsBase = new Vector2(felt.Center.X,
            fanY + fanHalfHeight + BlackjackTableLayout.HeroChipsDrop * scale);
        DrawBetDisplay(drawList, ui, mySeat, puckCenter, chipsBase, BlackjackTableLayout.DealerFanCenter(felt),
            true, scale);
    }

    private void DrawBetDisplay(ImDrawListPtr drawList, AppSkin ui, int seatIndex, Vector2 origin, Vector2 anchor,
        Vector2 dealerAnchor, bool heroChips, float scale)
    {
        ref readonly var motion = ref motions[seatIndex];
        if (motion.ShownBet > 0)
        {
            var flight = motion.BetClock / BetFlightSeconds;
            BlackjackTableArt.DrawFlightDisc(drawList, origin, anchor, flight,
                BlackjackTableArt.TopChipColor(motion.ShownBet), scale);
            var entrance = (motion.BetClock - BetFlightSeconds) / PlatePopSeconds;
            if (entrance > 0f)
            {
                if (heroChips)
                {
                    BlackjackTableArt.DrawChipColumn(drawList, anchor, motion.ShownBet, scale);
                    BlackjackTableArt.DrawBetPlate(drawList, new Vector2(anchor.X, anchor.Y + 16f * scale),
                        motion.ShownBet, ui.TitleInk, entrance, scale);
                }
                else
                {
                    BlackjackTableArt.DrawBetPlate(drawList, anchor, motion.ShownBet, ui.TitleInk, entrance, scale);
                }
            }
        }

        if (motion.SettleStarted && motion.SettleSign != 0 && motion.SettleClock < SettleFlightSeconds)
        {
            var progress = motion.SettleClock / SettleFlightSeconds;
            if (motion.SettleSign > 0)
            {
                BlackjackTableArt.DrawFlightDisc(drawList, dealerAnchor, anchor, progress, Gold, scale);
            }
            else
            {
                BlackjackTableArt.DrawFlightDisc(drawList, anchor, dealerAnchor, progress,
                    BlackjackTableArt.TopChipColor(motion.ShownBet > 0 ? motion.ShownBet : BlackjackRules.MinBet),
                    scale);
            }
        }
    }

    private float BadgeEntrance(int seatIndex)
    {
        ref readonly var motion = ref motions[seatIndex];
        if (!motion.SettleStarted)
        {
            return 1f;
        }

        var entrance = (motion.SettleClock - SettleFlightSeconds * 0.5f) / BadgePopSeconds;
        return Math.Clamp(entrance, 0f, 1f);
    }

    private Vector2 DrawCapsule(ImDrawListPtr drawList, AppSkin ui, CasinoBlackjackRoomStateDto board,
        Vector2 center, long turnRemaining, float delta, float scale)
    {
        var view = seatViews[mySeat];
        var height = BlackjackTableLayout.CapsuleHeight * scale;
        var puckRadius = BlackjackTableLayout.CapsulePuckRadius * scale;
        stackRoll.Update((int)Math.Clamp(view.Stack, 0, int.MaxValue), delta);
        var stackLabel = NumberText.Group(stackRoll.Display);
        var name = Typography.FitText(view.DisplayName, 120f * scale, TextStyles.Caption1);
        var nameSize = Typography.Measure(name, TextStyles.Caption1);
        var stackSize = Typography.Measure(stackLabel, TextStyles.FootnoteEmphasized);
        var stackReserve = CurrencyGlyph.Reserve(stackSize.Y);
        var textWidth = MathF.Max(nameSize.X, stackReserve + stackSize.X);
        var pad = 10f * scale;
        var halfWidth = (puckRadius * 2f + pad * 2.75f + textWidth) * 0.5f;
        var min = new Vector2(center.X - halfWidth, center.Y - height * 0.5f);
        var max = new Vector2(center.X + halfWidth, center.Y + height * 0.5f);
        ui.Card(drawList, min, max, height * 0.5f);
        var puck = new Vector2(min.X + pad + puckRadius, center.Y);
        var dimmed = !view.Connected;
        AvatarView.DrawRemote(drawList, puck, puckRadius, ui.Theme, view.DisplayName, string.Empty, view.AvatarUrl,
            images, lodestone, 1f, 32, dimmed ? 0.45f : 1f, Frames.Of(view.FrameId));
        if (board.ActiveSeat == mySeat && board.Phase == BlackjackPhases.PlayerTurns)
        {
            TurnTimerRing.Draw(drawList, puck, puckRadius + 3.5f * scale, turnRemaining, board.WindowSeconds,
                ui.Accent, scale);
        }

        var textX = puck.X + puckRadius + pad * 0.75f;
        Typography.Draw(drawList, new Vector2(textX, center.Y - nameSize.Y - 1.5f * scale), name, ui.TitleInk,
            TextStyles.Caption1);
        var glyphSize = stackSize.Y * CurrencyGlyph.GlyphFraction;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Chips,
            new Vector2(textX + glyphSize * 0.5f, center.Y + 1.5f * scale + stackSize.Y * 0.5f), glyphSize);
        Typography.Draw(drawList, new Vector2(textX + stackReserve, center.Y + 1.5f * scale), stackLabel, Gold,
            TextStyles.FootnoteEmphasized.Scale * stackRoll.PopScale, TextStyles.FootnoteEmphasized.Weight);
        return puck;
    }

    private static void DrawHeroGhostSlots(ImDrawListPtr drawList, in Rect felt, float fanY, float scale)
    {
        var cardWidth = BlackjackTableLayout.HeroCardWidth(1) * scale;
        var height = PlayingCards.HeightFor(cardWidth);
        var step = cardWidth * 0.45f;
        var rounding = PlayingCards.RoundingFor(cardWidth);
        for (var index = 0; index < 2; index++)
        {
            var centerX = felt.Center.X + (index - 0.5f) * step;
            var min = new Vector2(centerX - cardWidth * 0.5f, fanY - height * 0.5f);
            PlayingCards.DrawSlot(drawList, new Rect(min, min + new Vector2(cardWidth, height)), rounding, scale);
        }
    }

    private static bool CircleHovered(Vector2 center, float radius)
    {
        var offset = ImGui.GetMousePos() - center;
        if (offset.LengthSquared() > radius * radius)
        {
            return false;
        }

        var corner = new Vector2(radius, radius);
        return UiInteract.Hover(center - corner, center + corner);
    }

    private static string OutcomeLabel(CasinoBlackjackHandDto hand)
    {
        if (hand.Outcome == BlackjackOutcomes.Blackjack)
        {
            return Loc.T(L.Casino.BlackjackSeatNatural);
        }

        if (hand.Outcome == BlackjackOutcomes.Push)
        {
            return Loc.T(L.Casino.BlackjackSeatPush);
        }

        if (hand.Outcome == BlackjackOutcomes.Bust)
        {
            return Loc.T(L.Casino.BlackjackSeatBust);
        }

        return hand.Delta > 0
            ? string.Concat("+", NumberText.Group(hand.Delta))
            : string.Empty;
    }

    private void SpeakForPhase(CasinoBlackjackRoomStateDto board)
    {
        if (board.Phase == spokenPhase && board.ActiveSeat == spokenSeat
            && string.Equals(spokenHandId, board.HandId, StringComparison.Ordinal))
        {
            return;
        }

        spokenPhase = board.Phase;
        spokenSeat = board.ActiveSeat;
        spokenHandId = board.HandId;
        if (board.Phase == BlackjackPhases.Betting)
        {
            dealer.Show(Loc.T(L.Casino.BlackjackWaitingForBets));
            return;
        }

        if (BlackjackPhases.Over(board.Phase) && board.DealerTotal > 0)
        {
            dealer.Show(Loc.T(L.Casino.BlackjackDealerHas, GameNumber.Label(board.DealerTotal)));
            return;
        }

        if (board.ActiveSeat >= 0 && board.ActiveSeat == mySeat)
        {
            dealer.Show(Loc.T(L.Casino.BlackjackYourTurn));
        }
    }

    private void CelebrateSettledHand(CasinoBlackjackRoomStateDto board, in Rect felt, float scale)
    {
        if (string.Equals(celebratedHandId, board.HandId, StringComparison.Ordinal))
        {
            return;
        }

        settledDelta = 0;
        if (board.HandId.Length == 0 || !BlackjackRules.IsSeat(mySeat))
        {
            return;
        }

        var hands = projection.HandsAt(mySeat);
        if (hands.Length == 0 || !Settled(hands))
        {
            return;
        }

        celebratedHandId = board.HandId;
        settledDelta = 0;
        for (var index = 0; index < hands.Length; index++)
        {
            settledDelta += hands[index].Delta;
        }

        winRoll.Snap(0);
        var witnessed = motions[mySeat].SettleClock < SnappedClock;
        var stake = TotalBet(hands);
        var bigWin = stake > 0 && settledDelta >= stake * 10;
        if (witnessed)
        {
            SoundSettledHand(settledDelta, bigWin);
        }

        if (settledDelta <= 0)
        {
            return;
        }

        var origin = new Vector2(felt.Center.X, BlackjackTableLayout.HeroFanY(felt));
        if (bigWin)
        {
            particles.Confetti(origin, 90, ConfettiPalette, 330f * scale, 5f, 1.6f);
            particles.Sparkle(origin, 24, Gold, 190f * scale, 4f, 1.0f);
            return;
        }

        particles.Confetti(origin, 40, ConfettiPalette, 250f * scale, 4f, 1.2f);
    }

    private static void SoundSettledHand(long delta, bool bigWin)
    {
        if (bigWin)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            return;
        }

        if (delta > 0)
        {
            UiFeedback.Play(UiSound.GameWin);
            return;
        }

        if (delta < 0)
        {
            UiFeedback.Play(UiSound.GameWrong);
        }
    }

    private static bool Settled(CasinoBlackjackHandDto[] hands)
    {
        for (var index = 0; index < hands.Length; index++)
        {
            if (hands[index].Outcome == BlackjackOutcomes.Pending)
            {
                return false;
            }
        }

        return true;
    }

    private static long TotalBet(CasinoBlackjackHandDto[] hands)
    {
        var total = 0L;
        for (var index = 0; index < hands.Length; index++)
        {
            total += hands[index].Bet;
        }

        return total;
    }

    private void DrawFooter(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state,
        CasinoBlackjackRoomStateDto board, CasinoRoomSnapshotDto snapshot, long deadlineRemaining, float delta,
        float left, float y, float width, float scale, bool veiled)
    {
        var draining = snapshot.State != CasinoRoomStates.Live;
        y = DrawBanner(drawList, ui, state, board, draining, deadlineRemaining, delta, left, y, width, scale);
        var sitting = CasinoWire.SittingFor(state, CasinoWire.BlackjackKind);
        var bought = sitting is not null;
        var onBoard = BlackjackRules.IsSeat(mySeat);
        if (!bought || (!onBoard && !CasinoSeatMachine.Holds(seatFlow.Stage)))
        {
            DrawSitAction(ui, state, board, bought, left, y, width, scale);
            return;
        }

        var stakeBlocked = draining || state.Draining || state.StakesPaused;
        var seatStack = SeatStackOf(sitting!);
        if (onBoard && CasinoJoinGate.CanPlaceBet(board.Phase, true, seatFlow.Waiting, draining,
                state.StakesPaused || state.Draining))
        {
            DrawComposer(ui, state, seatStack, board, left, y, width, scale, delta, veiled);
            return;
        }

        if (onBoard && CasinoJoinGate.CanAct(true, seatFlow.Waiting, projection.ActionsMask != 0))
        {
            DrawActionBar(ui, board, seatStack, left, y, width, scale);
            return;
        }

        var standLabel = seatFlow.StandQueued
            ? Loc.T(L.Casino.StandQueued)
            : stakeBlocked ? Loc.T(L.Casino.CashOut) : Loc.T(L.Casino.StandAction);
        if (DrawSingleAction(ui, standLabel, !seatFlow.Busy && !seatFlow.StandQueued, left, y, width, scale))
        {
            inlineReason = string.Empty;
            seatFlow.Stand(roomId);
        }
    }

    private long SeatStackOf(CasinoSittingDto sitting)
    {
        var seat = projection.SeatAt(mySeat);
        return seat is not null && seat.Chips > 0 ? seat.Chips : sitting.Stack;
    }

    private float DrawBanner(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state,
        CasinoBlackjackRoomStateDto board, bool draining, long deadlineRemaining, float delta,
        float left, float y, float width, float scale)
    {
        var center = new Vector2(left + width * 0.5f, y + BannerHeight * scale * 0.4f);
        if (BlackjackPhases.Over(board.Phase) && settledDelta > 0)
        {
            winRoll.Update((int)Math.Min(settledDelta, int.MaxValue), delta);
            var amount = "+" + NumberText.Group((long)winRoll.Display);
            Typography.DrawCentered(drawList, center, Loc.T(L.Casino.BlackjackYouWon, amount), Gold,
                TextStyles.Title3.Scale * winRoll.PopScale, TextStyles.Title3.Weight);
            return y + BannerHeight * scale;
        }

        var message = inlineReason.Length > 0
            ? Loc.T(CasinoReasons.MessageFor(inlineReason))
            : SeatTextFor(state, draining) ?? BannerTextFor(board, deadlineRemaining);
        Typography.DrawCentered(drawList, center, Typography.FitText(message, width, TextStyles.Caption1),
            ui.MutedInk, TextStyles.Caption1);
        return y + BannerHeight * scale;
    }

    private string? SeatTextFor(CasinoStateDto state, bool draining)
    {
        if (seatFlow.StandQueued)
        {
            return Loc.T(L.Casino.StandAtHandEnd);
        }

        if (seatFlow.Waiting)
        {
            return Loc.T(L.Casino.DealtNextHand);
        }

        if (draining || state.Draining)
        {
            return Loc.T(L.Casino.TableDrainingLine);
        }

        return state.StakesPaused ? Loc.T(L.Casino.PausedTitle) : null;
    }

    private string BannerTextFor(CasinoBlackjackRoomStateDto board, long deadlineRemaining)
    {
        if (board.Phase == BlackjackPhases.Betting)
        {
            var seconds = (int)((deadlineRemaining + 999) / 1000);
            return board.HandId.Length == 0 || deadlineRemaining <= 0
                ? Loc.T(L.Casino.BlackjackWaitingForBets)
                : Loc.T(L.Casino.BlackjackBetsCloseIn, TimeText.Duration(seconds));
        }

        if (BlackjackPhases.Over(board.Phase))
        {
            return Loc.T(L.Casino.BlackjackHandOver);
        }

        if (board.ActiveSeat < 0)
        {
            return Loc.T(L.Casino.BlackjackDealing);
        }

        return board.ActiveSeat == mySeat
            ? Loc.T(L.Casino.BlackjackYourTurn)
            : Loc.T(L.Casino.BlackjackDealerPlays);
    }

    private void DrawComposer(AppSkin ui, CasinoStateDto state, long seatStack,
        CasinoBlackjackRoomStateDto board, float left, float y, float width, float scale, float delta, bool veiled)
    {
        var minimum = board.MinBet > 0 ? board.MinBet : BlackjackRules.MinBet;
        var maximum = board.MaxBet > 0 ? board.MaxBet : BlackjackRules.MaxBet;
        composer.Prefill(minimum);
        var blocked = veiled || state.StakesPaused || state.Draining || rooms.StakeInFlight;
        var bounds = new Rect(new Vector2(left, y),
            new Vector2(left + width, y + BetComposer.HeightFor(scale)));
        var label = Loc.T(L.Casino.BlackjackBetConfirm, NumberText.Group(composer.Amount),
            NumberText.Group(BlackjackRules.BlackjackPayout(composer.Amount)));
        if (composer.Draw(ui, bounds, minimum, maximum, seatStack, BlackjackRules.BetStep, !blocked, label,
                delta))
        {
            inlineReason = string.Empty;
            rooms.PlaceBlackjackBet(composer.Amount);
            UiFeedback.Play(UiSound.CasinoChips);
        }
    }

    private void DrawActionBar(AppSkin ui, CasinoBlackjackRoomStateDto board, long seatStack, float left,
        float y, float width, float scale)
    {
        var mask = projection.ActionsMask;
        var offered = 0;
        for (var index = 0; index < ActionBits.Length; index++)
        {
            if (BlackjackRules.Allows(mask, ActionBits[index]))
            {
                offered++;
            }
        }

        if (offered == 0)
        {
            return;
        }

        var gap = Metrics.Space.Xs * scale;
        var buttonWidth = (width - gap * (offered - 1)) / offered;
        var height = ActionBarHeight * scale;
        var cost = ActiveBetOf(board);
        var affordable = seatStack >= cost;
        var drawn = 0;
        for (var index = 0; index < ActionBits.Length; index++)
        {
            var bit = ActionBits[index];
            if (!BlackjackRules.Allows(mask, bit))
            {
                continue;
            }

            var min = new Vector2(left + drawn * (buttonWidth + gap), y);
            var rect = new Rect(min, new Vector2(min.X + buttonWidth, min.Y + height));
            drawn++;
            var wagered = bit == BlackjackRules.ActionDouble || bit == BlackjackRules.ActionSplit;
            var legal = !rooms.StakeInFlight && (!wagered || affordable);
            var costLine = wagered ? NumberText.Group(cost) : string.Empty;
            if (AppSkin.StackedPillButton(rect, LabelFor(bit), costLine, bit == BlackjackRules.ActionStand,
                    legal, ui.Theme) && legal)
            {
                inlineReason = string.Empty;
                rooms.SendBlackjackAction(bit);
                if (wagered)
                {
                    UiFeedback.Play(UiSound.CasinoChips);
                }
            }
        }
    }

    private long ActiveBetOf(CasinoBlackjackRoomStateDto board)
    {
        var hands = projection.HandsAt(mySeat);
        var active = projection.ActiveHand;
        return active >= 0 && active < hands.Length ? hands[active].Bet : 0;
    }

    private static string LabelFor(int action)
    {
        return action switch
        {
            BlackjackRules.ActionHit => Loc.T(L.Casino.BlackjackActionHit),
            BlackjackRules.ActionStand => Loc.T(L.Casino.BlackjackActionStand),
            BlackjackRules.ActionDouble => Loc.T(L.Casino.BlackjackActionDouble),
            _ => Loc.T(L.Casino.BlackjackActionSplit),
        };
    }

    private void DrawSitAction(AppSkin ui, CasinoStateDto state, CasinoBlackjackRoomStateDto board, bool bought,
        float left, float y, float width, float scale)
    {
        var buyIn = bought
            ? CasinoWire.SittingFor(state, CasinoWire.BlackjackKind)?.Stack ?? 0
            : RackFor(state, board);
        if (!bought && buyIn <= 0)
        {
            if (DrawSingleAction(ui, Loc.T(L.Casino.BlackjackTakeSeat), true, left, y, width, scale))
            {
                openCashier();
            }

            return;
        }

        var seatIndex = FirstOpenSeat();
        var label = seatIndex < 0 ? Loc.T(L.Casino.TableFullBadge) : Loc.T(L.Casino.SitDownAction);
        if (DrawSingleAction(ui, label, seatIndex >= 0 && !seatFlow.Busy, left, y, width, scale))
        {
            inlineReason = string.Empty;
            seatFlow.Sit(roomId, seatIndex, buyIn, board.Phase);
        }
    }

    private int FirstOpenSeat()
    {
        for (var seatIndex = 0; seatIndex < BlackjackRules.SeatCount; seatIndex++)
        {
            var seat = projection.SeatAt(seatIndex);
            if (seat is null || seat.State == BlackjackSeatStates.Empty)
            {
                return seatIndex;
            }
        }

        return -1;
    }

    private static bool DrawSingleAction(AppSkin ui, string label, bool enabled, float left, float y, float width,
        float scale)
    {
        var rect = new Rect(new Vector2(left + width * 0.2f, y),
            new Vector2(left + width * 0.8f, y + ActionBarHeight * scale));
        return AppSkin.PillButton(rect, label, true, enabled, ui.Theme) && enabled;
    }

    private static void DrawNotice(ImDrawListPtr drawList, AppSkin ui, string title, string hint, string action,
        Action onAction, float left, float y, float width, float scale)
    {
        var pad = 14f * scale;
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var block = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - pad * 2f);
        var height = titleSize.Y + block.Y + pad * 2f + 6f * scale;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad + titleSize.Y + 6f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - pad * 2f);

        var pillY = max.Y + Metrics.Space.Md * scale;
        var pillRect = new Rect(new Vector2(left + width * 0.2f, pillY),
            new Vector2(left + width * 0.8f, pillY + 44f * scale));
        if (AppSkin.PillButton(pillRect, action, true, true, ui.Theme))
        {
            onAction();
        }
    }
}
