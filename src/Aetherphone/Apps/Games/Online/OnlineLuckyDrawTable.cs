using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Cards;
using Aetherphone.Apps.Games.LuckyDraw;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Online;

internal enum LuckyReplay : byte
{
    Keep,
    Replay,
    Snap,
}

internal sealed class OnlineLuckyDrawTable
{
    public const string AccentId = "luckydraw";
    internal const int NotObserved = int.MinValue;
    private const int MaxSeats = OnlineLuckyDrawModel.MaxSeats;
    private const int RowCapacity = OnlineLuckyDrawModel.RowCapacity;
    private const int SlotCount = MaxSeats * RowCapacity;
    private const int FlightCapacity = 96;
    private const int DiscardTag = 16;
    private const float DealSeconds = 0.26f;
    private const float DrawSeconds = 0.34f;
    private const float TravelSeconds = 0.32f;
    private const float FlightArc = 0.16f;
    private const float SweepStagger = 0.045f;
    private const float CollectStagger = 0.02f;
    private const float OpeningHold = 0.35f;
    private const float BustHold = 0.85f;
    private const float SevenHold = 1.7f;
    private const float StayHold = 0.4f;
    private const float ActionHold = 0.6f;
    private const float SavedHold = 0.35f;
    private const float TurnHold = 0.18f;
    private const float TargetHold = 0.25f;
    private const float SheetDelaySeconds = 0.5f;
    private const float SheetSpeed = 2.6f;
    private const float SlotSmooth = 0.11f;
    private const float PopDecay = 4f;
    private const float FlashDecay = 1.15f;
    private const float BannerSeconds = 1.4f;
    private const float TextRise = 30f;
    private const float TableInset = 10f;
    private const float AvatarFraction = 0.34f;
    private const float TimerGap = 3f;
    private const float StatusPad = 40f;

    private readonly GameRoomsStore store;
    private readonly OnlineLuckyDrawModel model = new();
    private readonly LuckyDrawLayout layout = new();
    private readonly CardFlight flights = new(FlightCapacity);
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly StageBackdrop backdrop = new();
    private readonly ScreenFx screen;
    private readonly CardDesign[] designs = new CardDesign[LuckyCards.FaceCount];
    private readonly Spring[] slotX = new Spring[SlotCount];
    private readonly Spring[] slotY = new Spring[SlotCount];
    private readonly Spring[] slotAngle = new Spring[SlotCount];
    private readonly bool[] slotPlaced = new bool[SlotCount];
    private readonly int[] rowInAir = new int[MaxSeats];
    private readonly float[] bustFlash = new float[MaxSeats];
    private readonly int[] bustSlots = new int[MaxSeats * 2];
    private readonly float[] seatPop = new float[MaxSeats];
    private readonly int[] spots = new int[MaxSeats];
    private readonly string[] names = new string[MaxSeats];
    private readonly string[] toPlay = new string[MaxSeats];
    private readonly string[] choosing = new string[MaxSeats];
    private readonly string[] winLines = new string[MaxSeats];
    private readonly int[] order = new int[MaxSeats];
    private readonly Vector4[] confetti;
    private LuckyDrawRoomStateDto? final;
    private LuckyDrawEventDto[] events = Array.Empty<LuckyDrawEventDto>();
    private LuckyDrawPlayerDto[]? labeledPlayers;
    private LanguageInfo? textLanguage;
    private LabelSlot roundTitle;
    private LabelSlot riskLabel;
    private int cursor;
    private int seenStep = NotObserved;
    private int mySeat = -1;
    private int bottomSeat = -1;
    private int spotSeats;
    private int discardInAir;
    private float holdTimer;
    private float sheetDelay;
    private float sheetProgress;
    private float bannerProgress = 1f;
    private float entrance = 1f;
    private float pulseClock;
    private string bannerText = string.Empty;
    private Vector4 bannerColor;
    private bool replaying;
    private bool sheetOpen;

    public OnlineLuckyDrawTable(GameRoomsStore store)
    {
        this.store = store;
        screen = new ScreenFx(backdrop);
        backdrop.Set(Backdrop.Felt);
        confetti = new[] { Accent, LuckyDrawRenderer.Gold, LuckyDrawRenderer.Mint, LuckyDrawRenderer.Ice };
    }

    public static Vector4 Accent => AppAccents.For(AccentId);

    internal static LuckyReplay ReplayFor(int seenStep, int step)
    {
        if (seenStep == NotObserved)
        {
            return LuckyReplay.Snap;
        }

        if (step == seenStep)
        {
            return LuckyReplay.Keep;
        }

        return step == seenStep + 1 ? LuckyReplay.Replay : LuckyReplay.Snap;
    }

    public void Reset()
    {
        final = null;
        events = Array.Empty<LuckyDrawEventDto>();
        cursor = 0;
        seenStep = NotObserved;
        mySeat = -1;
        bottomSeat = -1;
        labeledPlayers = null;
        replaying = false;
        entrance = 1f;
        bannerProgress = 1f;
        ClearMotion();
        screen.Clear();
    }

    public void Draw(Rect body, PhoneTheme theme, float scale, GameRoomSnapshotDto snapshot,
        LuckyDrawRoomStateDto board, string notice, OnlineFinishHold hold)
    {
        using var surface = AppSurface.Begin(body, true);
        ImGui.Dummy(new Vector2(MathF.Max(1f, body.Width - 32f * scale), body.Height - 16f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var accent = Accent;
        var raw = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        backdrop.Update(raw, body, ImGui.GetMousePos(), UiInteract.Hover(body.Min, body.Max));
        backdrop.Draw(drawList, body, accent, scale);
        screen.Update(raw);
        var tick = raw * screen.TimeScale;
        Observe(board);
        SyncText(board);
        particles.Update(raw);
        fx.Update(raw);
        pulseClock += raw;
        entrance = GameJuice.Advance(entrance, raw);
        bannerProgress = GameBanner.Advance(bannerProgress, raw, BannerSeconds);
        Decay(tick, raw);
        var area = StageLayout.Punched(body.Inset(TableInset * scale), screen.PlateScale);
        layout.Build(area, Math.Max(LuckyDrawBoard.MinSeats, model.Seats), scale);
        flights.Advance(tick);
        TakeLandings(scale);
        StepSlots(tick);
        Pump(tick, scale);
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var remaining = store.Room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, nowMs);
        var shake = fx.ShakeOffset(scale);
        DrawTable(drawList, board, shake, remaining, accent, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, layout.Center + shake, bannerText, bannerColor, theme, bannerProgress,
            TextStyles.Title1);
        DrawSheet(drawList, body, area, theme, accent, scale);
        HandleControls(drawList, board, shake, remaining, notice, theme, scale);
        if (hold.Holding)
        {
            hold.Draw(drawList, layout.Controls.Center, body.Width - 32f * scale, theme, scale,
                !replaying && sheetOpen);
        }

        screen.Draw(drawList, body, accent);
    }

    private bool Settled => !replaying && !flights.Busy && holdTimer <= 0f;

    private void Observe(LuckyDrawRoomStateDto board)
    {
        if (ReferenceEquals(board, final))
        {
            return;
        }

        var previous = final;
        final = board;
        var mode = ReplayFor(seenStep, board.Step);
        seenStep = board.Step;
        mySeat = SeatOf(board.Players, store.AccountId);
        if (mode == LuckyReplay.Keep)
        {
            if (!replaying)
            {
                model.Load(board);
            }

            RefreshSpots();
            return;
        }

        if (mode == LuckyReplay.Snap || previous is null)
        {
            SnapTo(board);
            return;
        }

        if (replaying)
        {
            model.Load(previous);
            ClearMotion();
        }

        events = board.Events ?? Array.Empty<LuckyDrawEventDto>();
        cursor = 0;
        replaying = true;
        RefreshSpots();
    }

    private void SnapTo(LuckyDrawRoomStateDto board)
    {
        model.Load(board);
        ClearMotion();
        events = Array.Empty<LuckyDrawEventDto>();
        cursor = 0;
        replaying = false;
        RefreshSpots();
        if (model.Phase is LuckyDrawWire.PhaseRoundOver or LuckyDrawWire.PhaseMatchOver)
        {
            OpenSheet();
        }
    }

    private void ClearMotion()
    {
        flights.Clear();
        particles.Clear();
        fx.Clear();
        Array.Clear(rowInAir);
        Array.Clear(bustFlash);
        Array.Clear(seatPop);
        Array.Clear(slotPlaced);
        discardInAir = 0;
        holdTimer = 0f;
        sheetOpen = false;
        sheetDelay = 0f;
        sheetProgress = 0f;
    }

    private void RefreshSpots()
    {
        var seats = model.Seats;
        var bottom = mySeat >= 0 && mySeat < seats ? mySeat : 0;
        if (bottom == bottomSeat && seats == spotSeats)
        {
            return;
        }

        bottomSeat = bottom;
        spotSeats = seats;
        for (var seat = 0; seat < seats; seat++)
        {
            spots[seat] = (seat - bottom + seats) % seats;
        }

        Array.Clear(slotPlaced);
    }

    private int Spot(int seat) => seat >= 0 && seat < spotSeats ? spots[seat] : 0;

    private void SyncText(LuckyDrawRoomStateDto board)
    {
        var players = board.Players ?? Array.Empty<LuckyDrawPlayerDto>();
        if (ReferenceEquals(players, labeledPlayers) && ReferenceEquals(textLanguage, Loc.Current))
        {
            return;
        }

        if (!ReferenceEquals(textLanguage, Loc.Current))
        {
            LuckyDrawRenderer.BuildDesigns(designs);
            roundTitle.Reset();
            riskLabel.Reset();
        }

        labeledPlayers = players;
        textLanguage = Loc.Current;
        for (var seat = 0; seat < MaxSeats; seat++)
        {
            var name = seat < players.Length ? players[seat].DisplayName : GameSeats.Name(seat);
            var mine = seat == mySeat;
            names[seat] = mine ? Loc.T(L.Games.You) : name;
            toPlay[seat] = mine ? Loc.T(L.Games.OnlineYourTurn) : Loc.T(L.Games.OnlineTheirTurn, name);
            choosing[seat] = Loc.T(L.Games.OnlineLuckyDrawChoosing, name);
            winLines[seat] = mine ? Loc.T(L.Games.YouWin) : Loc.T(L.Stage.SeatWins, name);
        }
    }

    private void Decay(float tick, float raw)
    {
        for (var seat = 0; seat < MaxSeats; seat++)
        {
            seatPop[seat] = MathF.Max(0f, seatPop[seat] - raw * PopDecay);
            if (bustFlash[seat] > 0f)
            {
                bustFlash[seat] = MathF.Max(0.01f, bustFlash[seat] - tick * FlashDecay);
            }
        }
    }

    private void Pump(float tick, float scale)
    {
        holdTimer = MathF.Max(0f, holdTimer - tick);
        if (flights.Busy || holdTimer > 0f)
        {
            return;
        }

        while (cursor < events.Length)
        {
            React(events[cursor++], scale);
            if (flights.Busy || holdTimer > 0f)
            {
                return;
            }
        }

        if (replaying)
        {
            replaying = false;
            if (final is not null && !model.Matches(final))
            {
                model.Load(final);
                Array.Clear(slotPlaced);
            }

            RefreshSpots();
        }

        StepSheet(tick, scale);
    }

    private void StepSheet(float tick, float scale)
    {
        if (model.Phase is not (LuckyDrawWire.PhaseRoundOver or LuckyDrawWire.PhaseMatchOver))
        {
            sheetOpen = false;
            sheetDelay = 0f;
            sheetProgress = 0f;
            return;
        }

        if (sheetOpen)
        {
            sheetProgress = MathF.Min(1f, sheetProgress + tick * SheetSpeed);
            return;
        }

        sheetDelay += tick;
        if (sheetDelay < SheetDelaySeconds)
        {
            return;
        }

        OpenSheet();
        if (model.Phase == LuckyDrawWire.PhaseMatchOver)
        {
            CelebrateMatch(scale);
        }
    }

    private void OpenSheet()
    {
        sheetOpen = true;
        sheetProgress = 0f;
        var count = model.Seats;
        for (var seat = 0; seat < count; seat++)
        {
            order[seat] = seat;
        }

        for (var index = 1; index < count; index++)
        {
            var current = order[index];
            var probe = index - 1;
            while (probe >= 0 && model.Total(order[probe]) < model.Total(current))
            {
                order[probe + 1] = order[probe];
                probe--;
            }

            order[probe + 1] = current;
        }
    }

    private void CelebrateMatch(float scale)
    {
        var winner = model.Winner;
        if (winner < 0 || winner >= model.Seats)
        {
            return;
        }

        var mine = winner == mySeat;
        Banner(winLines[winner], mine ? LuckyDrawRenderer.Gold : GameSeats.Color(winner));
        if (!mine)
        {
            if (mySeat >= 0)
            {
                UiFeedback.Play(UiSound.GameWrong);
                screen.Vignette(LuckyDrawRenderer.Danger, 0.3f, 0.9f);
            }

            return;
        }

        UiFeedback.Play(UiSound.GameClear);
        particles.Confetti(new Vector2(layout.Center.X, layout.Ring.Min.Y), 90, confetti, 300f * scale, 4f, 1.6f);
        screen.Sweep();
        screen.Punch(0.05f);
        screen.Flash(LuckyDrawRenderer.Gold, 0.2f);
    }

    private void React(LuckyDrawEventDto change, float scale)
    {
        if (final is not { } board)
        {
            return;
        }

        switch (change.Kind)
        {
            case LuckyDrawWire.EventRound:
                OnRound(change, board);
                return;
            case LuckyDrawWire.EventDrew:
                model.Apply(change, board);
                OnDrew(change, scale);
                return;
            case LuckyDrawWire.EventKept:
                OnKept(change, scale);
                return;
            case LuckyDrawWire.EventBust:
                model.Apply(change, board);
                OnBust(change, scale);
                return;
            case LuckyDrawWire.EventSwept:
                LaunchSweep(change, board);
                return;
            case LuckyDrawWire.EventSaved:
                model.Apply(change, board);
                OnSaved(change, scale);
                return;
            case LuckyDrawWire.EventSeven:
                model.Apply(change, board);
                OnSeven(change, scale);
                return;
            case LuckyDrawWire.EventChanceKept:
                Pop(change.Seat);
                Float(change.Seat, Loc.T(L.LuckyDraw.SecondChance), LuckyDrawRenderer.Mint, scale);
                particles.Emit(LuckyDrawRenderer.ChanceGlow, SlotPose(change.Seat, change.Slot).Center, 10);
                UiFeedback.Play(UiSound.GamePowerUp);
                holdTimer = SavedHold;
                return;
            case LuckyDrawWire.EventChanceGiven:
                model.Apply(change, board);
                OnGiven(change, scale);
                return;
            case LuckyDrawWire.EventChanceDiscarded:
            case LuckyDrawWire.EventDiscarded:
                model.Apply(change, board);
                ToDiscard(change.Seat, change.Slot, change.Face);
                holdTimer = TargetHold;
                return;
            case LuckyDrawWire.EventChooseTarget:
                model.Apply(change, board);
                Pop(change.Seat);
                holdTimer = TargetHold;
                if (change.Seat == mySeat)
                {
                    UiFeedback.Play(UiSound.GameTick);
                }

                return;
            case LuckyDrawWire.EventFrozen:
                model.Apply(change, board);
                OnFrozen(change, scale);
                return;
            case LuckyDrawWire.EventFlipThree:
                model.Apply(change, board);
                OnFlipThree(change, scale);
                return;
            case LuckyDrawWire.EventDeferred:
                Pop(change.Seat);
                UiFeedback.Play(UiSound.GameTick);
                holdTimer = TargetHold;
                return;
            case LuckyDrawWire.EventStayed:
                model.Apply(change, board);
                OnStayed(change.Seat, scale);
                return;
            case LuckyDrawWire.EventTurn:
                model.Apply(change, board);
                OnTurn(change.Seat);
                return;
            case LuckyDrawWire.EventTimeout:
                Pop(change.Seat);
                Float(change.Seat, Loc.T(L.Games.OnlineTimedOut), LuckyDrawRenderer.Danger, scale);
                return;
            case LuckyDrawWire.EventLeft:
                Pop(change.Seat);
                Float(change.Seat, Loc.T(L.Games.OnlineAway), LuckyDrawRenderer.Muted, scale);
                return;
            case LuckyDrawWire.EventRoundOver:
            case LuckyDrawWire.EventMatchOver:
                model.Apply(change, board);
                sheetOpen = false;
                sheetDelay = 0f;
                return;
            default:
                return;
        }
    }

    private void OnRound(LuckyDrawEventDto change, LuckyDrawRoomStateDto board)
    {
        if (change.Target <= 1)
        {
            ClearMotion();
            entrance = 0f;
        }
        else
        {
            LaunchCollect();
        }

        model.Apply(change, board);
        RefreshSpots();
        Array.Clear(slotPlaced);
        sheetOpen = false;
        sheetDelay = 0f;
        sheetProgress = 0f;
        holdTimer = OpeningHold;
        UiFeedback.Play(UiSound.GameShuffle);
    }

    private void OnDrew(LuckyDrawEventDto change, float scale)
    {
        var seat = change.Seat;
        var count = model.RowCount(seat);
        var to = layout.RowPose(Spot(seat), change.Slot, count);
        var from = PileLayout.Top(layout.Deck, layout.PileWidth, model.DeckCount + 1, scale);
        flights.Launch(change.Face, from, to, 0f, true, seat, model.Dealing ? DealSeconds : DrawSeconds, FlightArc);
        SnapSlot(seat, change.Slot, to);
        rowInAir[seat]++;
        UiFeedback.Play(UiSound.GameCardFlip);
        if (!change.Reshuffled)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameShuffle);
        fx.Shockwave(layout.Deck, layout.PileWidth * 1.4f, LuckyDrawRenderer.Gold, 0.5f, 2.6f);
        fx.AddText(Loc.T(L.LuckyDraw.Shuffled), layout.Deck - new Vector2(0f, layout.PileWidth),
            LuckyDrawRenderer.Gold, 1f, TextRise * scale);
    }

    private void OnKept(LuckyDrawEventDto change, float scale)
    {
        if (!LuckyCards.IsModifier(change.Face))
        {
            if (model.UniqueNumbers(change.Seat) == LuckyDrawBoard.SevenNumbers - 1)
            {
                Pop(change.Seat);
                UiFeedback.Play(UiSound.GameTick);
            }

            return;
        }

        var center = SlotPose(change.Seat, change.Slot).Center;
        particles.Sparkle(center, 10, LuckyDrawRenderer.Gold, 120f * scale, 2.6f, 0.7f);
        fx.Shockwave(center, layout.CardWidth(Spot(change.Seat)) * 1.2f, LuckyDrawRenderer.Gold, 0.4f, 2f);
        Float(change.Seat, designs[Math.Clamp(change.Face, 0, designs.Length - 1)].Label, LuckyDrawRenderer.Gold,
            scale);
        Pop(change.Seat);
        UiFeedback.Play(UiSound.GameCollect);
    }

    private void OnBust(LuckyDrawEventDto change, float scale)
    {
        var seat = change.Seat;
        bustFlash[seat] = 1f;
        bustSlots[seat * 2] = change.Slot;
        bustSlots[seat * 2 + 1] = change.OtherSlot;
        var center = SlotPose(seat, change.Slot).Center;
        particles.Emit(LuckyDrawRenderer.BustShards, center, 16);
        particles.Burst(center, 10, LuckyDrawRenderer.Ember, 180f * scale, 3f, 0.5f, 260f);
        fx.Shockwave(center, layout.CardWidth(Spot(seat)) * 2f, LuckyDrawRenderer.Danger, 0.45f, 3f);
        Float(seat, Loc.T(L.LuckyDraw.Bust), LuckyDrawRenderer.Danger, scale, 1.35f);
        Pop(seat);
        var mine = seat == mySeat;
        fx.AddTrauma(mine ? 0.45f : 0.25f);
        UiFeedback.Play(UiSound.GameWrong);
        if (mine)
        {
            screen.Flash(LuckyDrawRenderer.Danger, 0.2f);
            screen.Vignette(LuckyDrawRenderer.Danger, 0.35f, 0.8f);
        }

        holdTimer = BustHold;
    }

    private void OnSaved(LuckyDrawEventDto change, float scale)
    {
        var seat = change.Seat;
        var countBefore = model.RowCount(seat) + 2;
        var duplicate = SlotPose(seat, change.Slot);
        var chance = SlotPose(seat, change.OtherSlot);
        var first = model.DiscardCount - 2;
        flights.Launch(change.Face, duplicate, PileLayout.Scatter(layout.DiscardPile, layout.PileWidth, first), 0.18f,
            false, DiscardTag, TravelSeconds, FlightArc);
        flights.Launch(LuckyCards.SecondChance, chance,
            PileLayout.Scatter(layout.DiscardPile, layout.PileWidth, first + 1), 0.26f, false, DiscardTag,
            TravelSeconds, FlightArc);
        discardInAir += 2;
        RemoveViewSlot(seat, Math.Max(change.Slot, change.OtherSlot), countBefore);
        RemoveViewSlot(seat, Math.Min(change.Slot, change.OtherSlot), countBefore - 1);
        particles.Emit(LuckyDrawRenderer.ChanceGlow, chance.Center, 14);
        particles.Sparkle(chance.Center, 12, LuckyDrawRenderer.Mint, 150f * scale, 2.8f, 0.8f);
        fx.Shockwave(chance.Center, layout.CardWidth(Spot(seat)) * 2.2f, LuckyDrawRenderer.Mint, 0.5f, 3f);
        Float(seat, Loc.T(L.LuckyDraw.Saved), LuckyDrawRenderer.Mint, scale, 1.25f);
        Pop(seat);
        fx.AddTrauma(0.12f);
        UiFeedback.Play(UiSound.GamePowerUp);
        holdTimer = SavedHold;
    }

    private void OnSeven(LuckyDrawEventDto change, float scale)
    {
        var seat = change.Seat;
        Banner(Loc.T(L.LuckyDraw.Seven), LuckyDrawRenderer.Gold);
        var row = layout.Row(Spot(seat));
        particles.Confetti(row, 60, confetti, 260f * scale, 3.6f, 1.3f);
        particles.Sparkle(row, 20, LuckyDrawRenderer.Gold, 200f * scale, 3f, 1f);
        fx.Shockwave(row, layout.Ring.Width * 0.4f, LuckyDrawRenderer.Gold, 0.7f, 3.4f);
        Float(seat, GameNumber.Signed(LuckyDrawBoard.SevenBonus), LuckyDrawRenderer.Gold, scale, 1.5f);
        Pop(seat);
        fx.AddTrauma(0.2f);
        UiFeedback.Play(UiSound.GameClear);
        screen.Sweep();
        screen.Punch(0.06f);
        screen.Flash(LuckyDrawRenderer.Gold, 0.16f);
        screen.SlowMo(0.5f, 0.35f);
        holdTimer = SevenHold;
    }

    private void OnGiven(LuckyDrawEventDto change, float scale)
    {
        var from = SlotPose(change.Seat, change.Slot);
        RemoveViewSlot(change.Seat, change.Slot, model.RowCount(change.Seat) + 1);
        var target = change.Target;
        var to = layout.RowPose(Spot(target), change.OtherSlot, model.RowCount(target));
        flights.Launch(LuckyCards.SecondChance, from, to, 0f, false, target, TravelSeconds, 0.25f);
        SnapSlot(target, change.OtherSlot, to);
        rowInAir[target]++;
        Float(target, Loc.T(L.LuckyDraw.SecondChance), LuckyDrawRenderer.Mint, scale);
        Pop(target);
        UiFeedback.Play(UiSound.GameCollect);
        holdTimer = SavedHold;
    }

    private void OnFrozen(LuckyDrawEventDto change, float scale)
    {
        ToDiscard(change.Seat, change.Slot, change.Face);
        var plate = layout.Plate(Spot(change.Target));
        particles.Emit(LuckyDrawRenderer.IceShards, plate, 20);
        particles.Sparkle(plate, 10, LuckyDrawRenderer.Ice, 140f * scale, 2.6f, 0.8f);
        fx.Shockwave(plate, 70f * scale, LuckyDrawRenderer.Ice, 0.5f, 3f);
        Float(change.Target, Loc.T(L.LuckyDraw.Frozen), LuckyDrawRenderer.Ice, scale, 1.2f);
        Pop(change.Target);
        fx.AddTrauma(0.1f);
        UiFeedback.Play(UiSound.GameMatch);
        holdTimer = ActionHold;
    }

    private void OnFlipThree(LuckyDrawEventDto change, float scale)
    {
        ToDiscard(change.Seat, change.Slot, change.Face);
        var plate = layout.Plate(Spot(change.Target));
        Banner(Loc.T(L.LuckyDraw.FlipThree), LuckyDrawRenderer.FlipTint);
        particles.Emit(LuckyDrawRenderer.FlipSparks, plate, 18);
        fx.Shockwave(plate, 80f * scale, LuckyDrawRenderer.FlipTint, 0.5f, 3f);
        Pop(change.Target);
        fx.AddTrauma(0.12f);
        UiFeedback.Play(UiSound.GamePowerUp);
        holdTimer = ActionHold;
    }

    private void OnStayed(int seat, float scale)
    {
        var plate = layout.Plate(Spot(seat));
        particles.Burst(plate, 12, LuckyDrawRenderer.Mint, 140f * scale, 2.6f, 0.5f, 0f);
        fx.Shockwave(plate, 56f * scale, LuckyDrawRenderer.Mint, 0.4f, 2.4f);
        Float(seat, Loc.T(L.LuckyDraw.Banked), LuckyDrawRenderer.Mint, scale);
        Pop(seat);
        UiFeedback.Play(UiSound.GameCollect);
        holdTimer = StayHold;
    }

    private void OnTurn(int seat)
    {
        Pop(seat);
        holdTimer = TurnHold;
        if (seat != mySeat)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameTick);
        Banner(toPlay[seat], GameSeats.Color(seat));
    }

    private void ToDiscard(int seat, int slot, int face)
    {
        var from = SlotPose(seat, slot);
        RemoveViewSlot(seat, slot, model.RowCount(seat) + 1);
        flights.Launch(face, from, PileLayout.Scatter(layout.DiscardPile, layout.PileWidth, model.DiscardCount - 1),
            0f, false, DiscardTag, TravelSeconds, FlightArc);
        discardInAir++;
        UiFeedback.Play(UiSound.GameCardPlace);
    }

    private void LaunchSweep(LuckyDrawEventDto change, LuckyDrawRoomStateDto board)
    {
        var seat = change.Seat;
        var count = seat >= 0 && seat < model.Seats ? model.RowCount(seat) : 0;
        Span<int> faces = stackalloc int[RowCapacity];
        Span<CardPose> poses = stackalloc CardPose[RowCapacity];
        for (var slot = 0; slot < count; slot++)
        {
            faces[slot] = model.RowFace(seat, slot);
            poses[slot] = SlotPose(seat, slot);
        }

        model.Apply(change, board);
        var first = model.DiscardCount - count;
        for (var slot = 0; slot < count; slot++)
        {
            flights.Launch(faces[slot], poses[slot],
                PileLayout.Scatter(layout.DiscardPile, layout.PileWidth, first + slot), slot * SweepStagger, false,
                DiscardTag, TravelSeconds, FlightArc);
            slotPlaced[seat * RowCapacity + slot] = false;
        }

        discardInAir += count;
        if (seat >= 0 && seat < MaxSeats)
        {
            bustFlash[seat] = 0f;
        }

        if (count > 0)
        {
            UiFeedback.Play(UiSound.GameCardPlace);
        }
    }

    private void LaunchCollect()
    {
        var index = model.DiscardCount;
        var launched = 0;
        for (var seat = 0; seat < model.Seats; seat++)
        {
            var count = model.RowCount(seat);
            for (var slot = 0; slot < count; slot++)
            {
                flights.Launch(model.RowFace(seat, slot), SlotPose(seat, slot),
                    PileLayout.Scatter(layout.DiscardPile, layout.PileWidth, index), launched * CollectStagger, false,
                    DiscardTag, TravelSeconds, FlightArc);
                index++;
                launched++;
            }
        }

        discardInAir += launched;
    }

    private void TakeLandings(float scale)
    {
        while (flights.TryTakeLanded(out var landing))
        {
            if (landing.Tag == DiscardTag)
            {
                discardInAir = Math.Max(0, discardInAir - 1);
                continue;
            }

            var seat = landing.Tag;
            if (seat < 0 || seat >= MaxSeats)
            {
                continue;
            }

            rowInAir[seat] = Math.Max(0, rowInAir[seat] - 1);
            seatPop[seat] = MathF.Max(seatPop[seat], 0.5f);
            particles.Burst(landing.Pose.Center, 5, LuckyDrawRenderer.Dust, 70f * scale, 1.8f, 0.3f, 0f);
            UiFeedback.Play(UiSound.GameCardPlace);
        }
    }

    private void StepSlots(float tick)
    {
        for (var seat = 0; seat < model.Seats; seat++)
        {
            var count = model.RowCount(seat);
            for (var slot = 0; slot < count; slot++)
            {
                var index = seat * RowCapacity + slot;
                var target = layout.RowPose(Spot(seat), slot, count);
                if (!slotPlaced[index])
                {
                    SnapSlot(seat, slot, target);
                    continue;
                }

                slotX[index].Step(target.Center.X, SlotSmooth, tick);
                slotY[index].Step(target.Center.Y, SlotSmooth, tick);
                slotAngle[index].Step(target.Angle, SlotSmooth, tick);
            }
        }
    }

    private void SnapSlot(int seat, int slot, in CardPose pose)
    {
        if (seat < 0 || seat >= MaxSeats || slot < 0 || slot >= RowCapacity)
        {
            return;
        }

        var index = seat * RowCapacity + slot;
        slotX[index].SnapTo(pose.Center.X);
        slotY[index].SnapTo(pose.Center.Y);
        slotAngle[index].SnapTo(pose.Angle);
        slotPlaced[index] = true;
    }

    private CardPose SlotPose(int seat, int slot)
    {
        if (seat < 0 || seat >= MaxSeats || slot < 0 || slot >= RowCapacity)
        {
            return new CardPose(layout.Center, layout.PileWidth, 0f);
        }

        var index = seat * RowCapacity + slot;
        return new CardPose(new Vector2(slotX[index].Value, slotY[index].Value), layout.CardWidth(Spot(seat)),
            slotAngle[index].Value);
    }

    private void RemoveViewSlot(int seat, int slot, int countBefore)
    {
        if (seat < 0 || seat >= MaxSeats || slot < 0)
        {
            return;
        }

        var start = seat * RowCapacity;
        for (var index = slot; index < countBefore - 1 && index + 1 < RowCapacity; index++)
        {
            slotX[start + index] = slotX[start + index + 1];
            slotY[start + index] = slotY[start + index + 1];
            slotAngle[start + index] = slotAngle[start + index + 1];
            slotPlaced[start + index] = slotPlaced[start + index + 1];
        }

        var last = Math.Clamp(countBefore - 1, 0, RowCapacity - 1);
        slotPlaced[start + last] = false;
    }

    private void Pop(int seat)
    {
        if (seat >= 0 && seat < MaxSeats)
        {
            seatPop[seat] = 1f;
        }
    }

    private void Float(int seat, string text, Vector4 color, float scale, float size = 1.1f)
    {
        if (seat < 0 || seat >= model.Seats)
        {
            return;
        }

        var spot = Spot(seat);
        var lift = LuckyDrawLayout.PlateHeight * scale * (layout.RowAbove(spot) ? 0.9f : -0.9f);
        fx.AddText(text, layout.Plate(spot) - new Vector2(0f, lift), color, size, TextRise * scale);
    }

    private void Banner(string text, Vector4 color)
    {
        bannerText = text;
        bannerColor = color;
        bannerProgress = 0f;
    }

    private void DrawTable(ImDrawListPtr drawList, LuckyDrawRoomStateDto board, Vector2 shake, long remaining,
        Vector4 accent, float scale)
    {
        LuckyDrawRenderer.DrawTable(drawList, layout.Ring.Translate(shake), accent, scale);
        PileDraw.Deck(drawList, layout.Deck + shake, layout.PileWidth, model.DeckCount, accent, scale);
        var deckLabelY = layout.Deck.Y + layout.PileWidth * CardPose.Aspect * 0.5f + 10f * scale;
        Typography.DrawCentered(drawList, new Vector2(layout.Deck.X, deckLabelY) + shake,
            GameNumber.Label(model.DeckCount), LuckyDrawRenderer.Muted, TextStyles.Caption2);
        var visibleDiscard = Math.Clamp(model.DiscardCount - discardInAir, 0, model.DiscardCount);
        PileDraw.Discard(drawList, layout.DiscardPile + shake, layout.PileWidth, model.Discard[..visibleDiscard],
            designs, accent, scale);
        var pulse = Pulse.Wave(Pulse.Calm);
        var players = board.Players ?? Array.Empty<LuckyDrawPlayerDto>();
        for (var seat = 0; seat < model.Seats; seat++)
        {
            DrawPlate(drawList, players, seat, shake, pulse, remaining, board, scale);
        }

        for (var seat = model.Seats - 1; seat >= 0; seat--)
        {
            DrawRow(drawList, seat, shake, accent, scale);
        }

        for (var index = 0; index < flights.Count; index++)
        {
            var card = flights.Card(index);
            CardFace.Draw(drawList, flights.Pose(index).Moved(shake), designs[Math.Clamp(card, 0, designs.Length - 1)],
                accent, scale);
        }

        DrawThinkingDots(drawList, shake, scale);
    }

    private void DrawRow(ImDrawListPtr drawList, int seat, Vector2 shake, Vector4 accent, float scale)
    {
        var visible = model.RowCount(seat) - rowInAir[seat];
        var flash = bustFlash[seat];
        for (var slot = 0; slot < visible; slot++)
        {
            var face = model.RowFace(seat, slot);
            var flashing = flash > 0f && (slot == bustSlots[seat * 2] || slot == bustSlots[seat * 2 + 1]);
            var pose = SlotPose(seat, slot);
            if (flashing)
            {
                pose = pose.Moved(new Vector2(MathF.Sin(pulseClock * 60f + slot) * 2f * scale * flash, 0f))
                    .Sized(pose.Width * (1f + 0.08f * flash));
            }

            LuckyDrawRenderer.DrawCard(drawList, pose.Moved(shake), designs[face], accent, scale,
                flashing ? flash : 0f, LuckyCards.IsTargeted(face));
        }
    }

    private void DrawPlate(ImDrawListPtr drawList, LuckyDrawPlayerDto[] players, int seat, Vector2 shake,
        float pulse, long remaining, LuckyDrawRoomStateDto board, float scale)
    {
        var state = model.State(seat);
        var onClock = OnClock(seat);
        var targetable = !replaying && model.Actor == mySeat && mySeat >= 0 && model.IsValidTarget(seat);
        var away = seat < players.Length && players[seat].Away;
        var detail = DetailFor(seat, away, out var detailInk);
        var view = new LuckyPlateView(names[seat], detail, detailInk, model.Total(seat), model.HandScore(seat), state,
            GameSeats.Color(seat), onClock, false, targetable, model.HasSecondChance(seat),
            model.ForcedSeat == seat ? model.ForcedLeft : 0, seatPop[seat]);
        var spot = Spot(seat);
        var rect = layout.PlateRect(spot).Translate(shake);
        var appear = GameJuice.PopIn(GameJuice.Stagger(entrance, spot, model.Seats));
        if (appear < 1f)
        {
            rect = rect.Scaled(MathF.Max(0.05f, appear));
        }

        LuckyDrawRenderer.DrawPlate(drawList, rect, view, pulse, scale);
        if (!onClock || replaying)
        {
            return;
        }

        var avatarRadius = rect.Height * AvatarFraction;
        var avatar = new Vector2(rect.Min.X + rect.Height * 0.5f, rect.Center.Y);
        TurnTimerRing.Draw(drawList, avatar, avatarRadius + TimerGap * scale, remaining, board.TurnSeconds,
            GameSeats.Color(seat), scale);
    }

    private bool OnClock(int seat)
    {
        if (model.Phase == LuckyDrawWire.PhaseTarget)
        {
            return model.Actor == seat;
        }

        return model.Phase == LuckyDrawWire.PhaseTurn && model.TurnSeat == seat && !model.Dealing
               && model.SeatState(seat) == LuckyDrawWire.SeatActive;
    }

    private string DetailFor(int seat, bool away, out Vector4 ink)
    {
        switch (model.SeatState(seat))
        {
            case LuckyDrawWire.SeatBusted:
                ink = LuckyDrawRenderer.Danger;
                return Loc.T(L.LuckyDraw.Bust);
            case LuckyDrawWire.SeatFrozen:
                ink = LuckyDrawRenderer.Ice;
                return Loc.T(L.LuckyDraw.Frozen);
            case LuckyDrawWire.SeatStayed:
                ink = LuckyDrawRenderer.Mint;
                return away ? Loc.T(L.Games.OnlineAway) : Loc.T(L.LuckyDraw.Banked);
            case LuckyDrawWire.SeatOut:
                ink = LuckyDrawRenderer.Muted;
                return away ? Loc.T(L.Games.OnlineAway) : Loc.T(L.Games.OnlineLuckyDrawNextDeal);
            default:
                ink = LuckyDrawRenderer.Muted;
                return away ? Loc.T(L.Games.OnlineAway) : string.Empty;
        }
    }

    private void DrawThinkingDots(ImDrawListPtr drawList, Vector2 shake, float scale)
    {
        if (!Settled || sheetOpen)
        {
            return;
        }

        var seat = model.Phase == LuckyDrawWire.PhaseTarget
            ? model.Actor
            : model.Phase == LuckyDrawWire.PhaseTurn ? model.TurnSeat : -1;
        if (seat < 0 || seat >= model.Seats || seat == mySeat)
        {
            return;
        }

        var spot = Spot(seat);
        var plate = layout.PlateRect(spot);
        var y = layout.RowAbove(spot) ? plate.Max.Y + 10f * scale : plate.Min.Y - 10f * scale;
        LuckyDrawRenderer.DrawThinking(drawList, new Vector2(plate.Center.X, y) + shake, pulseClock,
            GameSeats.Color(seat), scale);
    }

    private void DrawSheet(ImDrawListPtr drawList, Rect body, Rect area, PhoneTheme theme, Vector4 accent,
        float scale)
    {
        if (!sheetOpen)
        {
            return;
        }

        var matchOver = model.Phase == LuckyDrawWire.PhaseMatchOver;
        var winner = model.Winner;
        var title = !matchOver
            ? roundTitle.Get(L.LuckyDraw.RoundNumber, model.Round)
            : winner >= 0 && winner < model.Seats
                ? winLines[winner]
                : Loc.T(L.Games.OnlineRoundVoid);
        LuckyDrawRenderer.DrawSheet(drawList, body, area, model, order.AsSpan(0, model.Seats), names, title,
            string.Empty, sheetProgress, accent, theme, scale, false);
    }

    private void HandleControls(ImDrawListPtr drawList, LuckyDrawRoomStateDto board, Vector2 shake, long remaining,
        string notice, PhoneTheme theme, float scale)
    {
        if (sheetOpen)
        {
            if (model.Phase == LuckyDrawWire.PhaseRoundOver)
            {
                DrawTimedStatus(drawList, Loc.T(L.LuckyDraw.NextRound), remaining, board.BreakSeconds, scale);
            }

            return;
        }

        if (Settled && model.Phase == LuckyDrawWire.PhaseTurn && model.TurnSeat == mySeat && mySeat >= 0)
        {
            var command = DrawHitStay(drawList, notice, theme, !store.ActInFlight);
            if (command == 1)
            {
                store.SendHit();
            }
            else if (command == 2)
            {
                store.SendStay();
            }

            return;
        }

        if (Settled && model.Phase == LuckyDrawWire.PhaseTarget && model.Actor == mySeat && mySeat >= 0)
        {
            LuckyDrawRenderer.DrawStatus(drawList, layout.Controls, Loc.T(PromptFor(model.PendingFace)),
                LuckyDrawRenderer.Gold, scale, 1f);
            if (store.ActInFlight)
            {
                return;
            }

            for (var seat = 0; seat < model.Seats; seat++)
            {
                if (!model.IsValidTarget(seat))
                {
                    continue;
                }

                var zone = layout.Zone(Spot(seat)).Translate(shake);
                if (UiInteract.HoverClick(zone.Min, zone.Max))
                {
                    store.SendTarget(seat);
                    return;
                }
            }

            return;
        }

        var text = !store.Room.Attached
            ? Loc.T(L.Games.OnlineReconnecting)
            : notice.Length > 0 ? notice : StatusText();
        if (text.Length > 0)
        {
            LuckyDrawRenderer.DrawStatus(drawList, layout.Controls, text, LuckyDrawRenderer.Muted, scale, 1f);
        }
    }

    private string StatusText()
    {
        if (model.Phase == LuckyDrawWire.PhaseTarget && model.Actor >= 0 && model.Actor < model.Seats)
        {
            return choosing[model.Actor];
        }

        var turn = model.TurnSeat;
        if (model.Phase == LuckyDrawWire.PhaseTurn && !model.Dealing && turn >= 0 && turn < model.Seats)
        {
            return toPlay[turn];
        }

        return model.Phase is LuckyDrawWire.PhaseRoundOver or LuckyDrawWire.PhaseMatchOver
            ? string.Empty
            : Loc.T(L.LuckyDraw.Dealing);
    }

    private void DrawTimedStatus(ImDrawListPtr drawList, string text, long remaining, int windowSeconds, float scale)
    {
        LuckyDrawRenderer.DrawStatus(drawList, layout.Controls, text, LuckyDrawRenderer.Muted, scale, 1f);
        var controls = layout.Controls;
        var width = MathF.Min(controls.Width,
            Typography.Measure(text, TextStyles.SubheadlineEmphasized).X + StatusPad * scale);
        var radius = controls.Height * 0.24f;
        var center = new Vector2(controls.Center.X - width * 0.5f - radius - TimerGap * 2f * scale,
            controls.Center.Y);
        if (center.X - radius < controls.Min.X)
        {
            return;
        }

        TurnTimerRing.Draw(drawList, center, radius, remaining, windowSeconds, LuckyDrawRenderer.Gold, scale);
    }

    private int DrawHitStay(ImDrawListPtr drawList, string notice, PhoneTheme theme, bool interactive)
    {
        var seat = mySeat;
        var accent = Accent;
        var pulse = Pulse.Wave(Pulse.Calm);
        ProgressRing.Glow(layout.HitCenter, layout.ButtonSize.X * 0.55f, accent, 0.25f + 0.25f * pulse);
        var hit = GameHud.Button(layout.HitCenter, layout.ButtonSize, Loc.T(L.LuckyDraw.Hit), accent, theme);
        var stay = GameHud.Button(layout.StayCenter, layout.ButtonSize, Loc.T(L.LuckyDraw.Stay),
            LuckyDrawRenderer.StayInk, theme);
        var chance = model.HasSecondChance(seat);
        var caption = notice.Length > 0
            ? notice
            : chance
                ? Loc.T(L.LuckyDraw.ChanceReady)
                : riskLabel.Get(L.LuckyDraw.Risk, (int)MathF.Round(model.BustChance(seat) * 100f));
        var captionInk = notice.Length > 0
            ? LuckyDrawRenderer.Danger
            : chance ? LuckyDrawRenderer.Mint : LuckyDrawRenderer.Muted;
        Typography.DrawCentered(drawList, new Vector2(layout.HitCenter.X, layout.CaptionY), caption, captionInk,
            TextStyles.Caption1);
        if (!interactive)
        {
            return 0;
        }

        if (hit || GameInput.Pressed(ImGuiKey.Space, ImGuiKey.H))
        {
            return 1;
        }

        return stay || GameInput.Pressed(ImGuiKey.Enter, ImGuiKey.S) ? 2 : 0;
    }

    private static LocString PromptFor(int face) => face switch
    {
        LuckyCards.Freeze => L.LuckyDraw.PickFreeze,
        LuckyCards.FlipThree => L.LuckyDraw.PickFlipThree,
        _ => L.LuckyDraw.PickChance,
    };

    private static int SeatOf(LuckyDrawPlayerDto[]? players, string userId)
    {
        if (players is null || userId.Length == 0)
        {
            return -1;
        }

        for (var index = 0; index < players.Length; index++)
        {
            if (string.Equals(players[index].UserId, userId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
