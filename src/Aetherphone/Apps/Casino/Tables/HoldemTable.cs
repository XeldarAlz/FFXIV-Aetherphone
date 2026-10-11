using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Cards;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal enum HoldemDeckMode : byte
{
    Status,
    Raise,
    BuyIn,
    TopUp,
    Reactions,
}

internal sealed partial class HoldemTable : ICabinetIdle
{
    public const int FlightCapacity = 48;

    private const float ChipFlightSeconds = 0.55f;
    private const float PayoutDelaySeconds = 0.35f;
    private const float ReactionSeconds = 2.6f;
    private const float PeelSmoothing = 0.08f;
    private const long TimerWarnMilliseconds = 5_000;
    private const float SettledClock = 99f;

    private static readonly int[] IdleCards = { 0, 12, 11, 10, 9 };

    private readonly CasinoStore chips;
    private readonly CasinoRoomsStore rooms;
    private readonly CasinoTablesStore tables;
    private readonly HoldemStore store;
    private readonly CasinoTurnNotifier turns;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly Action openCashier;
    private readonly Action leaveRoom;
    private readonly HoldemPlayback playback = new();
    private readonly HoldemTableLayout layout = new();
    private readonly HoldemSeatFlow seatFlow;
    private readonly CasinoTextCache texts = new();
    private readonly HoldemRaiseComposer composer;
    private readonly HoldemHistorySheet historySheet;
    private readonly CardFlight flights = new(FlightCapacity);
    private readonly long[] sweepAmounts = new long[HoldemRules.MaxSeats];
    private readonly long[] payoutAmounts = new long[HoldemRules.MaxSeats];
    private readonly bool[] winningCards = new bool[PlayingCards.DeckSize];
    private readonly Vector2[] scatter = new Vector2[HoldemPotScatter.Count];

    private string roomId = string.Empty;
    private string inlineReason = string.Empty;
    private string turnKey = string.Empty;
    private string settledHand = string.Empty;
    private string winnersHand = string.Empty;
    private string scatterHand = string.Empty;
    private string ribbonText = string.Empty;
    private string ribbonHand = string.Empty;
    private int ribbonPhase = -1;
    private int ribbonCursor = int.MinValue;
    private int ribbonSeconds = -1;
    private LanguageInfo? ribbonLanguage;
    private HoldemDeckMode deckMode;
    private int mySeat = -1;
    private int pickedSeat = -1;
    private int reaction = -1;
    private long buyIn;
    private bool postNow;
    private bool entered;
    private bool potsOpen;
    private bool practice;
    private bool timerWarned;
    private float sweepClock = SettledClock;
    private float payoutClock = SettledClock;
    private float reactionClock = SettledClock;
    private float idleClock;
    private long heroStack = -1;
    private Spring peel = new(0f);

    public HoldemTable(CasinoStore chips, CasinoRoomsStore rooms, CasinoTablesStore tables, HoldemStore store,
        CasinoTurnNotifier turns, RemoteImageCache images, LodestoneService lodestone, Action openCashier,
        Action leaveRoom)
    {
        this.chips = chips;
        this.rooms = rooms;
        this.tables = tables;
        this.store = store;
        this.turns = turns;
        this.images = images;
        this.lodestone = lodestone;
        this.openCashier = openCashier;
        this.leaveRoom = leaveRoom;
        seatFlow = new HoldemSeatFlow(store);
        composer = new HoldemRaiseComposer(texts);
        historySheet = new HoldemHistorySheet(store, texts);
    }

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public string RoomId => roomId;

    public bool Practice => practice;

    public long HeroStack => heroStack;

    public float DeckHeight => deckMode switch
    {
        HoldemDeckMode.Raise => HoldemRaiseComposer.DeckHeight,
        HoldemDeckMode.BuyIn or HoldemDeckMode.TopUp => BuyInDeckHeight,
        _ => CasinoStageLayout.DeckHeight,
    };

    public void Enter(string tableId)
    {
        var next = tableId.Length > 0 ? tableId : HoldemRules.LowRoom;
        if (entered && !string.Equals(roomId, next, StringComparison.Ordinal))
        {
            AbandonSeatIfHeld();
        }

        entered = true;
        roomId = next;
        ClearTable();
        _ = store.TakeSeatOutcome();
        _ = store.TakeActionOutcome();
        _ = store.TakeIntentFailure();
        rooms.Enter(roomId);
    }

    public void Exit()
    {
        AbandonSeatIfHeld();
        Reset();
    }

    public void Reset()
    {
        if (entered)
        {
            rooms.Leave();
            seatFlow.Left();
        }

        entered = false;
        historySheet.Close();
        ClearTable();
    }

    public void Gate()
    {
        historySheet.Gate();
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        historySheet.Draw(screen, ui);
    }

    private void ClearTable()
    {
        mySeat = -1;
        pickedSeat = -1;
        reaction = -1;
        inlineReason = string.Empty;
        turnKey = string.Empty;
        settledHand = string.Empty;
        winnersHand = string.Empty;
        deckMode = HoldemDeckMode.Status;
        potsOpen = false;
        practice = false;
        timerWarned = false;
        heroStack = -1;
        sweepClock = SettledClock;
        payoutClock = SettledClock;
        reactionClock = SettledClock;
        playback.Reset();
        seatFlow.Reset();
        flights.Clear();
        composer.Close();
        turns.Forget();
        peel.SnapTo(0f);
    }

    private void AbandonSeatIfHeld()
    {
        if (!entered || seatFlow.StandQueued)
        {
            return;
        }

        if (mySeat < 0 && !CasinoSeatMachine.Holds(seatFlow.Stage))
        {
            return;
        }

        store.Abandon(roomId);
    }

    public void Draw(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        turns.StampAttention();
        ConsumeOutcomes();
        var room = rooms.Room;
        var safe = frame.Safe;
        if (room.ClosedReason.Length > 0)
        {
            DrawClosed(drawList, ui, room.ClosedReason, safe, scale);
            return;
        }

        var board = room.State?.Holdem;
        var snapshot = room.State?.Snapshot;
        if (board is null || snapshot is null)
        {
            LoadingPulse.Draw(safe.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        var me = rooms.AccountId;
        var mine = MineFor(room.Private?.Holdem, board);
        mySeat = SeatOf(board, me);
        var hero = SeatAt(board, mySeat);
        var nowTick = Environment.TickCount64;
        seatFlow.Observe(hero is not null, room.HandedOff, hero?.Leaving ?? false, nowTick);
        if (seatFlow.Reason.Length > 0)
        {
            inlineReason = seatFlow.Reason;
            seatFlow.ClearReason();
        }

        if (hero is null && deckMode is HoldemDeckMode.Raise or HoldemDeckMode.TopUp or HoldemDeckMode.Reactions)
        {
            deckMode = HoldemDeckMode.Status;
        }

        store.SyncHand(room, me);
        practice = board.Practice;
        heroStack = hero?.Stack ?? -1;
        var delta = frame.DeltaSeconds;
        playback.Advance(board, frame.SnapToTruth);
        if (frame.SnapToTruth)
        {
            flights.Clear();
        }
        else
        {
            flights.Advance(delta);
        }

        layout.Compute(safe, SeatCountOf(board), mySeat >= 0 ? mySeat : 0, mySeat >= 0, scale);
        DrainLaunches(board, scale);
        DrainCues(stage, frame, board, scale);
        AdvanceClocks(delta);
        var unreachable = room.Unreachable(nowTick);
        var veiled = unreachable && CasinoSeatMachine.Holds(seatFlow.Stage);
        if (veiled || seatFlow.Elsewhere)
        {
            UiInteract.BlockThisFrame();
        }

        var localNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var remaining = room.RemainingMilliseconds(board.DeadlineUnixMs, localNow);
        HoldemArt.DrawFeltStage(drawList, FeltArea(frame), layout.Ring, practice, frame.Phase, scale);
        DrawRibbon(drawList, frame, board, snapshot, remaining, room.Attached, scale);
        DrawBoard(drawList, board, scale);
        DrawPot(drawList, board, scale);
        DrawStateLine(drawList, board, mine, hero, scale);
        DrawSeats(drawList, ui, board, remaining, frame.Phase, scale);
        DrawHero(drawList, board, mine, hero, delta, scale);
        DrawChipFlights(drawList, board, scale);
        DrawCardFlights(drawList, scale);
        DrawReaction(drawList, scale);
        DrawShelf(drawList, ui, board, mine, hero, scale);
        if (potsOpen)
        {
            DrawPotDetails(drawList, ui, board, scale);
        }

        TrackTurn(board, mine, remaining);
        DrawDeck(stage, frame, ui, board, mine, hero, remaining, scale);
        if (veiled)
        {
            ReconnectVeil.Draw(drawList, frame.Body, ui, 0, scale);
            return;
        }

        if (seatFlow.Elsewhere)
        {
            DrawElsewhere(drawList, ui, room, frame.Body, scale);
        }
    }

    private static Rect FeltArea(in CasinoStageFrame frame)
    {
        var full = frame.Full;
        return frame.Layout.HasDeck ? new Rect(full.Min, new Vector2(full.Max.X, frame.Deck.Min.Y)) : full;
    }

    private void DrawStateLine(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, CasinoHoldemYouDto? mine,
        CasinoHoldemSeatDto? hero, float scale)
    {
        var text = StateText(board, mine, hero);
        if (text.Key is null)
        {
            return;
        }

        var band = layout.StateBand;
        var ink = text.Key == L.Holdem.StateYourTurn.Key ? CasinoColors.MoneyHighlight : CasinoColors.InkTitle;
        StageText.StateLine(drawList, band.Center, Loc.T(text), band.Width, ink);
    }

    internal static LocString StateText(CasinoHoldemRoomStateDto board, CasinoHoldemYouDto? mine,
        CasinoHoldemSeatDto? hero)
    {
        if (LivePrompt(board, mine) is { } prompt && !HoldemActions.OnlyShowOrMuck(prompt.Actions))
        {
            return L.Holdem.StateYourTurn;
        }

        return board.Phase switch
        {
            HoldemPhases.Waiting when hero is null => L.Holdem.StatePickSeat,
            HoldemPhases.Waiting => Seated(board) < HoldemRules.MinSeats ? L.Holdem.RibbonWaiting
                : L.Holdem.RibbonNextHand,
            HoldemPhases.Showdown => L.Holdem.RibbonShowdown,
            HoldemPhases.Voided => L.Holdem.HandVoided,
            _ => default,
        };
    }

    private void ConsumeOutcomes()
    {
        var outcome = store.TakeActionOutcome();
        if (outcome is not null)
        {
            inlineReason = outcome.Granted ? string.Empty : outcome.Reason;
        }
    }

    private static CasinoHoldemYouDto? MineFor(CasinoHoldemYouDto? mine, CasinoHoldemRoomStateDto board)
    {
        if (mine is null || board.HandId.Length == 0
            || !string.Equals(mine.HandId, board.HandId, StringComparison.Ordinal))
        {
            return null;
        }

        return mine;
    }

    internal static int SeatOf(CasinoHoldemRoomStateDto board, string userId)
    {
        var seats = board.Seats;
        if (seats is null || userId.Length == 0)
        {
            return -1;
        }

        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].State != HoldemSeatStates.Empty
                && string.Equals(seats[index].UserId, userId, StringComparison.Ordinal))
            {
                return seats[index].SeatIndex;
            }
        }

        return -1;
    }

    internal static CasinoHoldemSeatDto? SeatAt(CasinoHoldemRoomStateDto board, int seatIndex)
    {
        var seats = board.Seats;
        if (seats is null || seatIndex < 0)
        {
            return null;
        }

        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].SeatIndex == seatIndex && seats[index].State != HoldemSeatStates.Empty)
            {
                return seats[index];
            }
        }

        return null;
    }

    internal static int SeatCountOf(CasinoHoldemRoomStateDto board)
    {
        if (board.MaxSeats > 0)
        {
            return HoldemSeatRing.Clamp(board.MaxSeats);
        }

        return HoldemSeatRing.Clamp(board.Seats?.Length ?? HoldemRules.HouseSeats);
    }

    internal static int Seated(CasinoHoldemRoomStateDto board)
    {
        var seats = board.Seats;
        if (seats is null)
        {
            return 0;
        }

        var count = 0;
        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].State != HoldemSeatStates.Empty)
            {
                count++;
            }
        }

        return count;
    }

    internal static CasinoHoldemPromptDto? LivePrompt(CasinoHoldemRoomStateDto board, CasinoHoldemYouDto? mine)
    {
        var prompt = mine?.Prompt;
        if (prompt is null || prompt.Actions == 0)
        {
            return null;
        }

        if (HoldemActions.OnlyShowOrMuck(prompt.Actions))
        {
            return prompt;
        }

        return board.CursorSeat == mine!.SeatIndex && mine.ActionCount == board.ActionCount ? prompt : null;
    }

    private void DrainLaunches(CasinoHoldemRoomStateDto board, float scale)
    {
        var deck = DeckPose(scale);
        while (playback.TryTakeLaunch(out var launch))
        {
            if (launch.Kind == HoldemLaunchKind.Board)
            {
                var slot = layout.BoardSlot(launch.Slot);
                flights.Launch(BoardCard(board, launch.Slot), deck, new CardPose(slot, layout.BoardCardWidth),
                    launch.Delay, true, launch.Tag, HoldemPlayback.FlightSeconds, CardFlight.DefaultArc, true);
                continue;
            }

            var hero = launch.Seat == mySeat;
            var target = hero ? HeroCardCenter(launch.Slot) : SeatCardCenter(launch.Seat, launch.Slot);
            var width = hero ? layout.HeroCardPixels : layout.SeatCardPixels;
            flights.Launch(HoldemRules.FaceDown, deck, new CardPose(target, width, 0f, false), launch.Delay, false,
                launch.Tag, HoldemPlayback.FlightSeconds, CardFlight.DefaultArc, true);
        }

        while (flights.TryTakeLanded(out var landing))
        {
            if (landing.Tag >= HoldemPlayback.BoardTagBase && landing.Tag < HoldemPlayback.HoleTagBase)
            {
                CasinoSfx.Play(UiSound.CardSnap);
            }
        }
    }

    private CardPose DeckPose(float scale)
    {
        var felt = layout.Felt;
        return new CardPose(new Vector2(felt.Center.X, felt.Min.Y + HoldemArt.RailWidth * scale * 3f),
            HoldemTableLayout.SeatCardWidth * scale, 0f, false);
    }

    private static int BoardCard(CasinoHoldemRoomStateDto board, int index)
    {
        var cards = board.Board;
        return cards is not null && index < cards.Length ? cards[index] : HoldemRules.FaceDown;
    }

    private void DrainCues(CasinoStage stage, in CasinoStageFrame frame, CasinoHoldemRoomStateDto board, float scale)
    {
        if (playback.TakeHandStarted())
        {
            inlineReason = string.Empty;
            potsOpen = false;
            CasinoSfx.Play(UiSound.CardSnap);
        }

        if (playback.TryTakeSweep(sweepAmounts))
        {
            sweepClock = 0f;
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        while (playback.TryTakeReveal(out _))
        {
            CasinoSfx.Play(UiSound.CardSnap);
        }

        if (playback.TakeVoided())
        {
            ShellToast.Show(Loc.T(L.Holdem.HandVoided));
        }

        if (!playback.TryTakeResult(out var live))
        {
            return;
        }

        CollectPayouts(board);
        if (live && SumPayouts() > 0)
        {
            payoutClock = -PayoutDelaySeconds;
        }

        SettleHero(stage, frame, board, live, scale);
    }

    private void CollectPayouts(CasinoHoldemRoomStateDto board)
    {
        Array.Clear(payoutAmounts);
        Array.Clear(winningCards);
        winnersHand = board.HandId;
        var winners = board.Winners;
        if (winners is null)
        {
            return;
        }

        for (var index = 0; index < winners.Length; index++)
        {
            var winner = winners[index];
            if (winner.SeatIndex >= 0 && winner.SeatIndex < HoldemRules.MaxSeats)
            {
                payoutAmounts[winner.SeatIndex] += winner.Amount;
            }

            var best = winner.Best5;
            if (best is null || winner.Strength <= 0)
            {
                continue;
            }

            for (var card = 0; card < best.Length; card++)
            {
                if (PlayingCards.IsCard(best[card]))
                {
                    winningCards[best[card]] = true;
                }
            }
        }
    }

    private long SumPayouts()
    {
        var total = 0L;
        for (var seat = 0; seat < payoutAmounts.Length; seat++)
        {
            total += payoutAmounts[seat];
        }

        return total;
    }

    private void SettleHero(CasinoStage stage, in CasinoStageFrame frame, CasinoHoldemRoomStateDto board, bool live,
        float scale)
    {
        if (mySeat < 0 || string.Equals(settledHand, board.HandId, StringComparison.Ordinal))
        {
            return;
        }

        var hero = SeatAt(board, mySeat);
        if (hero is null || hero.Committed <= 0)
        {
            return;
        }

        settledHand = board.HandId;
        var won = payoutAmounts[mySeat];
        if (!practice)
        {
            stage.Settle(new CasinoBetRecord(L.Casino.GameHoldem, hero.Committed, won,
                string.Concat(board.HandId, "_", GameNumber.Label(mySeat)),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }

        if (!live)
        {
            return;
        }

        stage.Celebration.Celebrate(hero.Committed, won, layout.HeroCardsCenter, frame.Instant);
        if (won > hero.Committed)
        {
            stage.Particles.Emit(CasinoLights.Sparkle(scale), layout.SeatCenter(mySeat), 14);
        }
    }

    private void AdvanceClocks(float delta)
    {
        sweepClock = MathF.Min(SettledClock, sweepClock + delta);
        payoutClock = MathF.Min(SettledClock, payoutClock + delta);
        reactionClock = MathF.Min(SettledClock, reactionClock + delta);
    }

    private void TrackTurn(CasinoHoldemRoomStateDto board, CasinoHoldemYouDto? mine, long remaining)
    {
        var key = CasinoTurnNotifier.HoldemTurnKey(board, mine);
        if (key is null)
        {
            if (turnKey.Length > 0)
            {
                turnKey = string.Empty;
                deckMode = deckMode == HoldemDeckMode.Raise ? HoldemDeckMode.Status : deckMode;
                composer.Close();
            }

            return;
        }

        if (!string.Equals(key, turnKey, StringComparison.Ordinal))
        {
            turnKey = key;
            timerWarned = false;
            CasinoSfx.Play(UiSound.TurnChime);
        }

        if (timerWarned || remaining <= 0 || remaining > TimerWarnMilliseconds)
        {
            return;
        }

        timerWarned = true;
        CasinoSfx.Play(UiSound.TimerLow);
    }

    private string RibbonLabel(CasinoHoldemRoomStateDto board, long remaining)
    {
        var seconds = (int)((Math.Max(0, remaining) + 999) / 1000);
        var shownSeconds = board.Phase is HoldemPhases.Intermission or HoldemPhases.Settled ? seconds : -1;
        if (board.Phase == ribbonPhase && board.CursorSeat == ribbonCursor && shownSeconds == ribbonSeconds
            && ReferenceEquals(ribbonLanguage, Loc.Current)
            && string.Equals(ribbonHand, board.HandId, StringComparison.Ordinal))
        {
            return ribbonText;
        }

        ribbonPhase = board.Phase;
        ribbonCursor = board.CursorSeat;
        ribbonSeconds = shownSeconds;
        ribbonLanguage = Loc.Current;
        ribbonHand = board.HandId;
        ribbonText = BuildRibbon(board, shownSeconds);
        return ribbonText;
    }

    private string BuildRibbon(CasinoHoldemRoomStateDto board, int seconds)
    {
        switch (board.Phase)
        {
            case HoldemPhases.Waiting:
                return Loc.T(Seated(board) < HoldemRules.MinSeats ? L.Holdem.RibbonWaiting : L.Holdem.RibbonNextHand);
            case HoldemPhases.Showdown:
                return Loc.T(L.Holdem.RibbonShowdown);
            case HoldemPhases.Intermission or HoldemPhases.Settled:
                return Loc.T(L.Holdem.RibbonNextIn, TimeText.Duration(Math.Max(0, seconds)));
            case HoldemPhases.Voided:
                return Loc.T(L.Holdem.HandVoided);
        }

        var street = Loc.T(StreetName(board.Phase));
        if (board.CursorSeat < 0)
        {
            return Loc.T(L.Holdem.RibbonDealing, street);
        }

        if (board.CursorSeat == mySeat)
        {
            return Loc.T(L.Holdem.RibbonYourTurn, street);
        }

        var name = SeatAt(board, board.CursorSeat)?.DisplayName ?? string.Empty;
        return Loc.T(L.Holdem.RibbonToAct, street, name);
    }

    internal static LocString StreetName(int phase) => phase switch
    {
        HoldemPhases.Flop => L.Holdem.StepFlop,
        HoldemPhases.Turn => L.Holdem.StepTurn,
        HoldemPhases.River => L.Holdem.StepRiver,
        HoldemPhases.Showdown => L.Holdem.StepShowdown,
        _ => L.Holdem.StepPreflop,
    };

    private void DrawRibbon(ImDrawListPtr drawList, in CasinoStageFrame frame, CasinoHoldemRoomStateDto board,
        CasinoRoomSnapshotDto snapshot, long remaining, bool attached, float scale)
    {
        var window = board.Phase switch
        {
            HoldemPhases.Intermission or HoldemPhases.Settled => HoldemRules.IntermissionSeconds,
            _ when board.CursorSeat >= 0 => board.WindowSeconds > 0 ? board.WindowSeconds : HoldemRules.TurnSeconds,
            _ => 0,
        };
        var watching = Math.Max(0, snapshot.Occupancy - Seated(board));
        PhaseRibbon.Draw(drawList, frame.Layout.Ribbon, RibbonLabel(board, remaining), window > 0 ? remaining : 0,
            window, watching, attached ? CasinoColors.LightA : CasinoColors.InkMuted, scale);
    }

    private void DrawClosed(ImDrawListPtr drawList, AppSkin ui, string reason, Rect safe, float scale)
    {
        var asks = BlackjackTable.AsksToJoin(reason);
        var title = Loc.T(asks ? L.Casino.BlackjackDoorTitle : L.Holdem.ClosedTitle);
        var hint = Loc.T(CasinoReasons.TryMessage(reason, out var known) ? known : L.Holdem.ClosedHint);
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

    private void DrawElsewhere(ImDrawListPtr drawList, AppSkin ui, CasinoRoomSession room, Rect body, float scale)
    {
        Material.Veil(drawList, body.Min, body.Max, 0.55f);
        var width = body.Width * 0.82f;
        var title = Loc.T(L.Holdem.ElsewhereTitle);
        var hint = Loc.T(L.Holdem.ElsewhereHint);
        var height = CasinoNotice.Height(CasinoNoticeKind.Card, title, hint, width, scale);
        var top = body.Center.Y - (height + Button.LargeHeight * scale) * 0.5f;
        CasinoNotice.DrawWithAction(drawList, ui, CasinoNoticeKind.Card, title, hint, Loc.T(L.Holdem.PlayHere),
            body.Center.X - width * 0.5f, top, width, scale, out var pressed);
        if (pressed)
        {
            seatFlow.Claim(room);
        }
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        var scale = UiScale.Current;
        idleClock += deltaSeconds;
        var felt = rect;
        var ring = felt.Inset(MathF.Min(felt.Width, felt.Height) * 0.14f);
        HoldemArt.DrawFeltStage(drawList, felt, ring, false, idleClock, scale);
        for (var seat = 0; seat < HoldemRules.HouseSeats; seat++)
        {
            var center = HoldemSeatRing.Seat(seat, HoldemRules.HouseSeats, ring, 0);
            var acting = seat == (int)(idleClock * 0.8f) % HoldemRules.HouseSeats;
            drawList.AddCircleFilled(center, 9f * scale,
                ImGui.GetColorU32((acting ? CasinoColors.LightA : CasinoColors.InkMuted) with { W = 0.85f }), 20);
        }

        var cardWidth = MathF.Min(felt.Width * 0.12f, 26f * scale);
        var shown = (int)(idleClock * 1.25f) % (HoldemRules.BoardSize + 3);
        for (var index = 0; index < HoldemRules.BoardSize; index++)
        {
            var center = new Vector2(felt.Center.X + (index - 2) * (cardWidth + 3f * scale), felt.Center.Y);
            HoldemArt.DrawCard(drawList, center, cardWidth, IdleCards[index], index < shown, 1f, scale);
        }
    }
}
