using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Cards;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.LuckyDraw;

internal sealed class LuckyDrawApp : IMiniGame
{
    private const string GameId = "luckydraw";
    private const int MaxSeats = LuckyDrawBoard.MaxSeats;
    private const int RowCapacity = LuckyDrawBoard.RowCapacity;
    private const int SlotCount = MaxSeats * RowCapacity;
    private const int FlightCapacity = 96;
    private const int DiscardTag = 16;
    private const int DemoSeats = 4;
    private const int PumpGuard = 24;
    private const ulong IdleSeed = 11;
    private const ulong ThinkSalt = 0x5EED5EEDUL;
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
    private const float ThinkMin = 0.45f;
    private const float ThinkMax = 0.95f;
    private const float SheetDelaySeconds = 0.5f;
    private const float SheetSpeed = 2.6f;
    private const float DemoSheetSeconds = 2.4f;
    private const float SlotSmooth = 0.11f;
    private const float PopDecay = 4f;
    private const float FlashDecay = 1.15f;
    private const float BannerSeconds = 1.4f;
    private const float TextRise = 30f;
    private static readonly LocString[] Modes = { L.LuckyDraw.OneBot, L.LuckyDraw.ThreeBots, L.LuckyDraw.FiveBots };
    private static readonly int[] BotCounts = { 1, 3, 5 };
    private static readonly GameSpec StageSpec = new(GameId, L.LuckyDraw.Title, GameGenre.Tabletop, L.LuckyDraw.Hook,
        Backdrop.Felt, HudStyle.Standard, ScoreKind.Streak, Modes, keyboard: true, seats: GameSeats.Max);
    private static readonly Vector4 StayInk = LuckyDrawRenderer.StayInk;
    private static readonly Vector4 Muted = LuckyDrawRenderer.Muted;
    private static readonly Vector4 Dust = LuckyDrawRenderer.Dust;
    private static readonly ParticleSpec BustShards = LuckyDrawRenderer.BustShards;
    private static readonly ParticleSpec IceShards = LuckyDrawRenderer.IceShards;
    private static readonly ParticleSpec ChanceGlow = LuckyDrawRenderer.ChanceGlow;
    private static readonly ParticleSpec FlipSparks = LuckyDrawRenderer.FlipSparks;

    private readonly LuckyDrawBoard board = new();
    private readonly LuckyDrawLayout layout = new();
    private readonly CardFlight flights = new(FlightCapacity);
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly CardDesign[] designs = new CardDesign[LuckyCards.FaceCount];
    private readonly Spring[] slotX = new Spring[SlotCount];
    private readonly Spring[] slotY = new Spring[SlotCount];
    private readonly Spring[] slotAngle = new Spring[SlotCount];
    private readonly bool[] slotPlaced = new bool[SlotCount];
    private readonly int[] rowInAir = new int[MaxSeats];
    private readonly float[] bustFlash = new float[MaxSeats];
    private readonly int[] bustSlots = new int[MaxSeats * 2];
    private readonly float[] seatPop = new float[MaxSeats];
    private readonly bool[] bots = new bool[MaxSeats];
    private readonly LuckyAppetite[] appetites = new LuckyAppetite[MaxSeats];
    private readonly string[] names = new string[MaxSeats];
    private readonly string[] toPlay = new string[MaxSeats];
    private readonly string[] winLines = new string[MaxSeats];
    private readonly int[] order = new int[MaxSeats];
    private readonly Vector4[] confetti;
    private GameRandom thinkRandom = GameRandom.FromSeed(IdleSeed);
    private LanguageInfo? textLanguage;
    private LabelSlot roundTitle;
    private LabelSlot riskLabel;
    private ulong demoSeed = IdleSeed;
    private int seatCount = DemoSeats;
    private int discardInAir;
    private int sweepSeat = -1;
    private float holdTimer;
    private float thinkTimer = -1f;
    private float sheetDelay;
    private float sheetProgress;
    private float sheetTimer;
    private float bannerProgress = 1f;
    private float entrance;
    private float pulseClock;
    private string bannerText = string.Empty;
    private Vector4 bannerColor;
    private bool sheetOpen;
    private bool hotSeat;
    private bool demo;
    private bool finished;
    private bool namesDirty = true;

    public LuckyDrawApp()
    {
        confetti = new[] { AppAccents.For(GameId), LuckyDrawRenderer.Gold, LuckyDrawRenderer.Mint, LuckyDrawRenderer.Ice };
        SetupDemo();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        demo = false;
        finished = false;
        hotSeat = start.HotSeat;
        seatCount = hotSeat
            ? Math.Clamp(start.Seats, LuckyDrawBoard.MinSeats, MaxSeats)
            : 1 + BotCounts[StageSpec.ClampMode(start.Mode)];
        for (var seat = 0; seat < MaxSeats; seat++)
        {
            bots[seat] = !hotSeat && seat > 0;
            appetites[seat] = LuckyDrawBot.AppetiteFor(seat - 1);
        }

        thinkRandom = GameRandom.FromSeed(start.Seed ^ ThinkSalt);
        board.NewMatch(seatCount, start.Random);
        ResetView();
        entrance = 0f;
        namesDirty = true;
    }

    public void Close()
    {
        SetupDemo();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        Frame(context, context.RawDeltaSeconds, false);
    }

    public void Draw(in GameContext context)
    {
        Frame(context, demo ? context.RawDeltaSeconds : context.DeltaSeconds,
            !demo && context.Session.State == StageFlow.Playing);
        if (demo)
        {
            return;
        }

        context.Hud.Score(board.Round, L.LuckyDraw.Round);
        if (!hotSeat)
        {
            context.Hud.Best(context.Session.Best);
        }

        context.Session.Report(board.Total(0));
    }

    private void SetupDemo()
    {
        demo = true;
        hotSeat = false;
        finished = false;
        seatCount = DemoSeats;
        for (var seat = 0; seat < MaxSeats; seat++)
        {
            bots[seat] = true;
            appetites[seat] = LuckyDrawBot.AppetiteFor(seat);
        }

        thinkRandom = GameRandom.FromSeed(demoSeed ^ ThinkSalt);
        board.NewMatch(seatCount, GameRandom.FromSeed(demoSeed));
        ResetView();
        entrance = 1f;
        namesDirty = true;
    }

    private void ResetView()
    {
        flights.Clear();
        particles.Clear();
        fx.Clear();
        Array.Clear(rowInAir);
        Array.Clear(bustFlash);
        Array.Clear(seatPop);
        Array.Clear(slotPlaced);
        discardInAir = 0;
        sweepSeat = -1;
        holdTimer = OpeningHold;
        thinkTimer = -1f;
        sheetOpen = false;
        sheetDelay = 0f;
        sheetProgress = 0f;
        sheetTimer = 0f;
        bannerProgress = 1f;
    }

    private void Frame(in GameContext context, float tick, bool interactive)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        SyncText();
        particles.Update(raw);
        fx.Update(raw);
        pulseClock += raw;
        entrance = GameJuice.Advance(entrance, raw);
        bannerProgress = GameBanner.Advance(bannerProgress, raw, BannerSeconds);
        Decay(tick, raw);
        var area = demo ? context.Safe : StageLayout.Punched(context.Safe, context.Fx.PlateScale);
        layout.Build(area, board.Seats, scale);
        flights.Advance(tick);
        TakeLandings(scale);
        StepSlots(tick);
        if (!finished)
        {
            Pump(context, tick, scale);
        }

        var shake = fx.ShakeOffset(scale);
        DrawTable(drawList, shake, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, layout.Center + shake, bannerText, bannerColor, context.Theme, bannerProgress,
            TextStyles.Title1);
        if (!demo)
        {
            HandleControls(drawList, context, shake, scale, interactive);
        }

        DrawSheet(drawList, context, scale, interactive);
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

    private void SyncText()
    {
        if (!namesDirty && ReferenceEquals(textLanguage, Loc.Current))
        {
            return;
        }

        namesDirty = false;
        textLanguage = Loc.Current;
        LuckyDrawRenderer.BuildDesigns(designs);
        for (var seat = 0; seat < MaxSeats; seat++)
        {
            names[seat] = hotSeat
                ? GameSeats.Name(seat)
                : !demo && seat == 0
                    ? Loc.T(L.Games.You)
                    : Loc.T(L.LuckyDraw.BotName, GameNumber.Label(demo ? seat + 1 : seat));
            var you = !hotSeat && !demo && seat == 0;
            toPlay[seat] = you ? Loc.T(L.Games.YourTurn) : Loc.T(L.LuckyDraw.ToPlay, names[seat]);
            winLines[seat] = you ? Loc.T(L.Games.YouWin) : Loc.T(L.Stage.SeatWins, names[seat]);
        }
    }

    private void Pump(in GameContext context, float tick, float scale)
    {
        holdTimer = MathF.Max(0f, holdTimer - tick);
        if (flights.Busy || holdTimer > 0f)
        {
            return;
        }

        if (sweepSeat >= 0)
        {
            LaunchSweep(sweepSeat);
            sweepSeat = -1;
            return;
        }

        for (var guard = 0; guard < PumpGuard; guard++)
        {
            switch (board.Phase)
            {
                case LuckyPhase.Auto:
                    React(board.Advance(), context, scale);
                    break;
                case LuckyPhase.Turn:
                {
                    var seat = board.TurnSeat;
                    if (!bots[seat] || !Think(tick))
                    {
                        return;
                    }

                    React(LuckyDrawBot.WantsHit(board, seat, appetites[seat]) ? board.Hit() : board.Stay(), context,
                        scale);
                    break;
                }
                case LuckyPhase.Target:
                {
                    var actor = board.Actor;
                    if (actor < 0 || !bots[actor] || !Think(tick))
                    {
                        return;
                    }

                    React(board.Target(BotTarget(actor)), context, scale);
                    break;
                }
                default:
                    StepSheet(context, tick, scale);
                    return;
            }

            if (flights.Busy || holdTimer > 0f || sweepSeat >= 0)
            {
                return;
            }
        }
    }

    private int BotTarget(int actor)
    {
        var chosen = LuckyDrawBot.ChooseTarget(board, actor, appetites[actor]);
        if (chosen >= 0)
        {
            return chosen;
        }

        for (var seat = 0; seat < board.Seats; seat++)
        {
            if (board.IsValidTarget(seat))
            {
                return seat;
            }
        }

        return -1;
    }

    private bool Think(float tick)
    {
        if (thinkTimer < 0f)
        {
            thinkTimer = thinkRandom.Range(ThinkMin, ThinkMax);
            return false;
        }

        thinkTimer -= tick;
        if (thinkTimer > 0f)
        {
            return false;
        }

        thinkTimer = -1f;
        return true;
    }

    private void StepSheet(in GameContext context, float tick, float scale)
    {
        if (!sheetOpen)
        {
            sheetDelay += tick;
            if (sheetDelay < SheetDelaySeconds)
            {
                return;
            }

            sheetOpen = true;
            sheetProgress = 0f;
            sheetTimer = 0f;
            SortStandings();
            if (board.Phase == LuckyPhase.MatchOver)
            {
                CelebrateMatch(context, scale);
            }

            return;
        }

        sheetProgress = MathF.Min(1f, sheetProgress + tick * SheetSpeed);
        if (!demo)
        {
            return;
        }

        sheetTimer += tick;
        if (sheetTimer >= DemoSheetSeconds)
        {
            Continue(context);
        }
    }

    private void Continue(in GameContext context)
    {
        if (board.Phase == LuckyPhase.MatchOver)
        {
            if (demo)
            {
                demoSeed++;
                SetupDemo();
                return;
            }

            FinishMatch(context);
            return;
        }

        LaunchCollect();
        board.StartRound();
        Array.Clear(slotPlaced);
        sheetOpen = false;
        sheetDelay = 0f;
        sheetProgress = 0f;
        holdTimer = OpeningHold;
        Sound(UiSound.GameShuffle);
    }

    private void FinishMatch(in GameContext context)
    {
        finished = true;
        var winner = Math.Max(0, board.Winner);
        var focus = hotSeat ? winner : 0;
        var outcome = hotSeat
            ? GameOutcome.Unranked().WithWinner(winner)
            : new GameOutcome(0, ScoreKind.Streak, GameId, winner == 0);
        context.Session.Finish(outcome
            .WithStat(L.LuckyDraw.Rounds, GameNumber.Label(board.Round))
            .WithStat(hotSeat ? L.LuckyDraw.WinningTotal : L.LuckyDraw.YourTotal, GameNumber.Label(board.Total(focus)))
            .WithStat(L.LuckyDraw.BestRound, GameNumber.Label(board.BestRound(focus)))
            .WithStat(L.LuckyDraw.Sevens, GameNumber.Label(board.Sevens(focus))));
    }

    private void CelebrateMatch(in GameContext context, float scale)
    {
        var winner = board.Winner;
        var humanWon = winner >= 0 && !bots[winner];
        Banner(winLines[Math.Max(0, winner)], humanWon ? LuckyDrawRenderer.Gold : GameSeats.Color(Math.Max(0, winner)));
        if (demo)
        {
            return;
        }

        if (!humanWon)
        {
            Sound(UiSound.GameWrong);
            context.Fx.Vignette(LuckyDrawRenderer.Danger, 0.3f, 0.9f);
            return;
        }

        Sound(UiSound.GameClear);
        particles.Confetti(new Vector2(layout.Center.X, layout.Ring.Min.Y), 90, confetti, 300f * scale, 4f, 1.6f);
        context.Fx.Sweep();
        context.Fx.Punch(0.05f);
        context.Fx.Flash(LuckyDrawRenderer.Gold, 0.2f);
    }

    private void SortStandings()
    {
        var count = board.Seats;
        for (var seat = 0; seat < count; seat++)
        {
            order[seat] = seat;
        }

        for (var index = 1; index < count; index++)
        {
            var current = order[index];
            var probe = index - 1;
            while (probe >= 0 && board.Total(order[probe]) < board.Total(current))
            {
                order[probe + 1] = order[probe];
                probe--;
            }

            order[probe + 1] = current;
        }
    }

    private void React(in LuckyEvent update, in GameContext context, float scale)
    {
        switch (update.Kind)
        {
            case LuckyEventKind.Drew:
                OnDrew(update, scale);
                return;
            case LuckyEventKind.Kept:
                OnKept(update, scale);
                return;
            case LuckyEventKind.Bust:
                OnBust(update, context, scale);
                return;
            case LuckyEventKind.Saved:
                OnSaved(update, scale);
                return;
            case LuckyEventKind.Seven:
                OnSeven(update, context, scale);
                return;
            case LuckyEventKind.SecondChanceKept:
                Pop(update.Seat);
                Float(update.Seat, Loc.T(L.LuckyDraw.SecondChance), LuckyDrawRenderer.Mint, scale);
                particles.Emit(ChanceGlow, SlotPose(update.Seat, update.Slot).Center, 10);
                Sound(UiSound.GamePowerUp);
                holdTimer = SavedHold;
                return;
            case LuckyEventKind.SecondChanceGiven:
                OnGiven(update, scale);
                return;
            case LuckyEventKind.SecondChanceDiscarded:
            case LuckyEventKind.ActionDiscarded:
                ToDiscard(update.Seat, update.Slot, update.Face);
                holdTimer = TargetHold;
                return;
            case LuckyEventKind.ChooseTarget:
                Pop(update.Seat);
                thinkTimer = -1f;
                holdTimer = TargetHold;
                if (!bots[update.Seat])
                {
                    Sound(UiSound.GameTick);
                }

                return;
            case LuckyEventKind.Frozen:
                OnFrozen(update, scale);
                return;
            case LuckyEventKind.FlipThree:
                OnFlipThree(update, scale);
                return;
            case LuckyEventKind.ActionDeferred:
                Pop(update.Seat);
                Sound(UiSound.GameTick);
                holdTimer = TargetHold;
                return;
            case LuckyEventKind.Stayed:
                OnStayed(update, scale);
                return;
            case LuckyEventKind.Turn:
                OnTurn(update);
                return;
            case LuckyEventKind.RoundOver:
            case LuckyEventKind.MatchOver:
                sheetOpen = false;
                sheetDelay = 0f;
                return;
            default:
                return;
        }
    }

    private void OnDrew(in LuckyEvent update, float scale)
    {
        var seat = update.Seat;
        var count = board.RowCount(seat);
        var to = layout.RowPose(seat, update.Slot, count);
        var from = PileLayout.Top(layout.Deck, layout.PileWidth, board.DeckCount + 1, scale);
        flights.Launch(update.Face, from, to, 0f, true, seat, board.Dealing ? DealSeconds : DrawSeconds, FlightArc);
        SnapSlot(seat, update.Slot, to);
        rowInAir[seat]++;
        Sound(UiSound.GameCardFlip);
        if (!update.Reshuffled)
        {
            return;
        }

        Sound(UiSound.GameShuffle);
        fx.Shockwave(layout.Deck, layout.PileWidth * 1.4f, LuckyDrawRenderer.Gold, 0.5f, 2.6f);
        fx.AddText(Loc.T(L.LuckyDraw.Shuffled), layout.Deck - new Vector2(0f, layout.PileWidth), LuckyDrawRenderer.Gold,
            1f, TextRise * scale);
    }

    private void OnKept(in LuckyEvent update, float scale)
    {
        if (!LuckyCards.IsModifier(update.Face))
        {
            if (board.UniqueNumbers(update.Seat) == LuckyDrawBoard.SevenNumbers - 1)
            {
                Pop(update.Seat);
                Sound(UiSound.GameTick);
            }

            return;
        }

        var center = SlotPose(update.Seat, update.Slot).Center;
        particles.Sparkle(center, 10, LuckyDrawRenderer.Gold, 120f * scale, 2.6f, 0.7f);
        fx.Shockwave(center, layout.CardWidth(update.Seat) * 1.2f, LuckyDrawRenderer.Gold, 0.4f, 2f);
        Float(update.Seat, designs[update.Face].Label, LuckyDrawRenderer.Gold, scale);
        Pop(update.Seat);
        Sound(UiSound.GameCollect);
    }

    private void OnBust(in LuckyEvent update, in GameContext context, float scale)
    {
        var seat = update.Seat;
        bustFlash[seat] = 1f;
        bustSlots[seat * 2] = update.Slot;
        bustSlots[seat * 2 + 1] = update.OtherSlot;
        var center = SlotPose(seat, update.Slot).Center;
        particles.Emit(BustShards, center, 16);
        particles.Burst(center, 10, LuckyDrawRenderer.Ember, 180f * scale, 3f, 0.5f, 260f);
        fx.Shockwave(center, layout.CardWidth(seat) * 2f, LuckyDrawRenderer.Danger, 0.45f, 3f);
        Float(seat, Loc.T(L.LuckyDraw.Bust), LuckyDrawRenderer.Danger, scale, 1.35f);
        Pop(seat);
        var human = !bots[seat];
        fx.AddTrauma(human ? 0.45f : 0.25f);
        Sound(UiSound.GameWrong);
        if (!demo && human)
        {
            context.Fx.Flash(LuckyDrawRenderer.Danger, 0.2f);
            context.Fx.Vignette(LuckyDrawRenderer.Danger, 0.35f, 0.8f);
        }

        holdTimer = BustHold;
        sweepSeat = seat;
    }

    private void OnSaved(in LuckyEvent update, float scale)
    {
        var seat = update.Seat;
        var countBefore = board.RowCount(seat) + 2;
        var duplicate = SlotPose(seat, update.Slot);
        var chance = SlotPose(seat, update.OtherSlot);
        var first = board.DiscardCount - 2;
        flights.Launch(update.Face, duplicate, PileLayout.Scatter(layout.DiscardPile, layout.PileWidth, first), 0.18f,
            false, DiscardTag, TravelSeconds, FlightArc);
        flights.Launch(LuckyCards.SecondChance, chance,
            PileLayout.Scatter(layout.DiscardPile, layout.PileWidth, first + 1), 0.26f, false, DiscardTag,
            TravelSeconds, FlightArc);
        discardInAir += 2;
        RemoveViewSlot(seat, Math.Max(update.Slot, update.OtherSlot), countBefore);
        RemoveViewSlot(seat, Math.Min(update.Slot, update.OtherSlot), countBefore - 1);
        particles.Emit(ChanceGlow, chance.Center, 14);
        particles.Sparkle(chance.Center, 12, LuckyDrawRenderer.Mint, 150f * scale, 2.8f, 0.8f);
        fx.Shockwave(chance.Center, layout.CardWidth(seat) * 2.2f, LuckyDrawRenderer.Mint, 0.5f, 3f);
        Float(seat, Loc.T(L.LuckyDraw.Saved), LuckyDrawRenderer.Mint, scale, 1.25f);
        Pop(seat);
        fx.AddTrauma(0.12f);
        Sound(UiSound.GamePowerUp);
        holdTimer = SavedHold;
    }

    private void OnSeven(in LuckyEvent update, in GameContext context, float scale)
    {
        var seat = update.Seat;
        Banner(Loc.T(L.LuckyDraw.Seven), LuckyDrawRenderer.Gold);
        var row = layout.Row(seat);
        particles.Confetti(row, 60, confetti, 260f * scale, 3.6f, 1.3f);
        particles.Sparkle(row, 20, LuckyDrawRenderer.Gold, 200f * scale, 3f, 1f);
        fx.Shockwave(row, layout.Ring.Width * 0.4f, LuckyDrawRenderer.Gold, 0.7f, 3.4f);
        Float(seat, GameNumber.Signed(LuckyDrawBoard.SevenBonus), LuckyDrawRenderer.Gold, scale, 1.5f);
        Pop(seat);
        fx.AddTrauma(0.2f);
        Sound(UiSound.GameClear);
        if (!demo)
        {
            context.Fx.Sweep();
            context.Fx.Punch(0.06f);
            context.Fx.Flash(LuckyDrawRenderer.Gold, 0.16f);
            context.Fx.SlowMo(0.5f, 0.35f);
        }

        holdTimer = SevenHold;
    }

    private void OnGiven(in LuckyEvent update, float scale)
    {
        var from = SlotPose(update.Seat, update.Slot);
        RemoveViewSlot(update.Seat, update.Slot, board.RowCount(update.Seat) + 1);
        var to = layout.RowPose(update.Target, update.OtherSlot, board.RowCount(update.Target));
        flights.Launch(LuckyCards.SecondChance, from, to, 0f, false, update.Target, TravelSeconds, 0.25f);
        SnapSlot(update.Target, update.OtherSlot, to);
        rowInAir[update.Target]++;
        Float(update.Target, Loc.T(L.LuckyDraw.SecondChance), LuckyDrawRenderer.Mint, scale);
        Pop(update.Target);
        Sound(UiSound.GameCollect);
        holdTimer = SavedHold;
    }

    private void OnFrozen(in LuckyEvent update, float scale)
    {
        ToDiscard(update.Seat, update.Slot, update.Face);
        var plate = layout.Plate(update.Target);
        particles.Emit(IceShards, plate, 20);
        particles.Sparkle(plate, 10, LuckyDrawRenderer.Ice, 140f * scale, 2.6f, 0.8f);
        fx.Shockwave(plate, 70f * scale, LuckyDrawRenderer.Ice, 0.5f, 3f);
        Float(update.Target, Loc.T(L.LuckyDraw.Frozen), LuckyDrawRenderer.Ice, scale, 1.2f);
        Pop(update.Target);
        fx.AddTrauma(0.1f);
        Sound(UiSound.GameMatch);
        holdTimer = ActionHold;
    }

    private void OnFlipThree(in LuckyEvent update, float scale)
    {
        ToDiscard(update.Seat, update.Slot, update.Face);
        var plate = layout.Plate(update.Target);
        Banner(Loc.T(L.LuckyDraw.FlipThree), LuckyDrawRenderer.FlipTint);
        particles.Emit(FlipSparks, plate, 18);
        fx.Shockwave(plate, 80f * scale, LuckyDrawRenderer.FlipTint, 0.5f, 3f);
        Pop(update.Target);
        fx.AddTrauma(0.12f);
        Sound(UiSound.GamePowerUp);
        holdTimer = ActionHold;
    }

    private void OnStayed(in LuckyEvent update, float scale)
    {
        var seat = update.Seat;
        var plate = layout.Plate(seat);
        particles.Burst(plate, 12, LuckyDrawRenderer.Mint, 140f * scale, 2.6f, 0.5f, 0f);
        fx.Shockwave(plate, 56f * scale, LuckyDrawRenderer.Mint, 0.4f, 2.4f);
        Float(seat, Loc.T(L.LuckyDraw.Banked), LuckyDrawRenderer.Mint, scale);
        Pop(seat);
        Sound(UiSound.GameCollect);
        holdTimer = StayHold;
    }

    private void OnTurn(in LuckyEvent update)
    {
        var seat = update.Seat;
        Pop(seat);
        thinkTimer = -1f;
        holdTimer = TurnHold;
        if (bots[seat])
        {
            return;
        }

        Sound(UiSound.GameTick);
        if (hotSeat)
        {
            Banner(names[seat], GameSeats.Color(seat));
        }
    }

    private void ToDiscard(int seat, int slot, int face)
    {
        var from = SlotPose(seat, slot);
        RemoveViewSlot(seat, slot, board.RowCount(seat) + 1);
        flights.Launch(face, from, PileLayout.Scatter(layout.DiscardPile, layout.PileWidth, board.DiscardCount - 1),
            0f, false, DiscardTag, TravelSeconds, FlightArc);
        discardInAir++;
        Sound(UiSound.GameCardPlace);
    }

    private void LaunchSweep(int seat)
    {
        var count = board.RowCount(seat);
        Span<int> faces = stackalloc int[RowCapacity];
        Span<CardPose> poses = stackalloc CardPose[RowCapacity];
        for (var slot = 0; slot < count; slot++)
        {
            faces[slot] = board.RowFace(seat, slot);
            poses[slot] = SlotPose(seat, slot);
        }

        board.SweepBusted(seat);
        var first = board.DiscardCount - count;
        for (var slot = 0; slot < count; slot++)
        {
            flights.Launch(faces[slot], poses[slot],
                PileLayout.Scatter(layout.DiscardPile, layout.PileWidth, first + slot), slot * SweepStagger, false,
                DiscardTag, TravelSeconds, FlightArc);
            slotPlaced[seat * RowCapacity + slot] = false;
        }

        discardInAir += count;
        bustFlash[seat] = 0f;
        if (count > 0)
        {
            Sound(UiSound.GameCardPlace);
        }
    }

    private void LaunchCollect()
    {
        var index = board.DiscardCount;
        var launched = 0;
        for (var seat = 0; seat < MaxSeats; seat++)
        {
            var count = seat < board.Seats ? board.RowCount(seat) : 0;
            for (var slot = 0; slot < count; slot++)
            {
                flights.Launch(board.RowFace(seat, slot), SlotPose(seat, slot),
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
            particles.Burst(landing.Pose.Center, 5, Dust, 70f * scale, 1.8f, 0.3f, 0f);
            Sound(UiSound.GameCardPlace);
        }
    }

    private void StepSlots(float tick)
    {
        for (var seat = 0; seat < board.Seats; seat++)
        {
            var count = board.RowCount(seat);
            for (var slot = 0; slot < count; slot++)
            {
                var index = seat * RowCapacity + slot;
                var target = layout.RowPose(seat, slot, count);
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
        var index = seat * RowCapacity + slot;
        slotX[index].SnapTo(pose.Center.X);
        slotY[index].SnapTo(pose.Center.Y);
        slotAngle[index].SnapTo(pose.Angle);
        slotPlaced[index] = true;
    }

    private CardPose SlotPose(int seat, int slot)
    {
        var index = seat * RowCapacity + slot;
        return new CardPose(new Vector2(slotX[index].Value, slotY[index].Value), layout.CardWidth(seat),
            slotAngle[index].Value);
    }

    private void RemoveViewSlot(int seat, int slot, int countBefore)
    {
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
        var plate = layout.Plate(seat);
        var lift = LuckyDrawLayout.PlateHeight * scale * (layout.RowAbove(seat) ? 0.9f : -0.9f);
        fx.AddText(text, plate - new Vector2(0f, lift), color, size, TextRise * scale);
    }

    private void Banner(string text, Vector4 color)
    {
        bannerText = text;
        bannerColor = color;
        bannerProgress = 0f;
    }

    private void Sound(UiSound sound)
    {
        if (!demo)
        {
            UiFeedback.Play(sound);
        }
    }

    private void DrawTable(ImDrawListPtr drawList, Vector2 shake, float scale)
    {
        var accent = Accent;
        LuckyDrawRenderer.DrawTable(drawList, layout.Ring.Translate(shake), accent, scale);
        PileDraw.Deck(drawList, layout.Deck + shake, layout.PileWidth, board.DeckCount, accent, scale);
        var deckLabelY = layout.Deck.Y + layout.PileWidth * CardPose.Aspect * 0.5f + 10f * scale;
        Typography.DrawCentered(drawList, new Vector2(layout.Deck.X, deckLabelY) + shake,
            GameNumber.Label(board.DeckCount), Muted, TextStyles.Caption2);
        var visibleDiscard = Math.Max(0, board.DiscardCount - discardInAir);
        PileDraw.Discard(drawList, layout.DiscardPile + shake, layout.PileWidth, board.Discard[..visibleDiscard],
            designs, accent, scale);
        var pulse = Pulse.Wave(Pulse.Calm);
        for (var seat = 0; seat < board.Seats; seat++)
        {
            DrawPlate(drawList, seat, shake, pulse, scale);
        }

        for (var seat = board.Seats - 1; seat >= 0; seat--)
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
        var visible = board.RowCount(seat) - rowInAir[seat];
        var flash = bustFlash[seat];
        for (var slot = 0; slot < visible; slot++)
        {
            var face = board.RowFace(seat, slot);
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

    private void DrawPlate(ImDrawListPtr drawList, int seat, Vector2 shake, float pulse, float scale)
    {
        var state = board.State(seat);
        var active = board.Playing && ((board.Phase == LuckyPhase.Target && board.Actor == seat) ||
                                       (board.Phase != LuckyPhase.Target && board.TurnSeat == seat &&
                                        !board.Dealing && state == LuckySeatState.Active));
        var targetable = board.Phase == LuckyPhase.Target && board.Actor >= 0 && !bots[board.Actor] &&
                         board.IsValidTarget(seat);
        var detail = DetailFor(seat, state, out var detailInk);
        var view = new LuckyPlateView(names[seat], detail, detailInk, board.Total(seat), board.HandScore(seat), state,
            GameSeats.Color(seat), active, bots[seat], targetable, board.HasSecondChance(seat),
            board.ForcedSeat == seat ? board.ForcedLeft : 0, seatPop[seat]);
        var rect = layout.PlateRect(seat).Translate(shake);
        var appear = GameJuice.PopIn(GameJuice.Stagger(entrance, seat, board.Seats));
        if (appear < 1f)
        {
            rect = rect.Scaled(MathF.Max(0.05f, appear));
        }

        LuckyDrawRenderer.DrawPlate(drawList, rect, view, pulse, scale);
    }

    private string DetailFor(int seat, LuckySeatState state, out Vector4 ink)
    {
        switch (state)
        {
            case LuckySeatState.Busted:
                ink = LuckyDrawRenderer.Danger;
                return Loc.T(L.LuckyDraw.Bust);
            case LuckySeatState.Frozen:
                ink = LuckyDrawRenderer.Ice;
                return Loc.T(L.LuckyDraw.Frozen);
            case LuckySeatState.Stayed:
                ink = LuckyDrawRenderer.Mint;
                return Loc.T(L.LuckyDraw.Banked);
            default:
                ink = Muted;
                if (!bots[seat])
                {
                    return string.Empty;
                }

                return appetites[seat].Temper switch
                {
                    LuckyTemper.Careful => Loc.T(L.LuckyDraw.Careful),
                    LuckyTemper.Bold => Loc.T(L.LuckyDraw.Bold),
                    _ => Loc.T(L.LuckyDraw.Steady),
                };
        }
    }

    private void DrawThinkingDots(ImDrawListPtr drawList, Vector2 shake, float scale)
    {
        if (thinkTimer < 0f || flights.Busy || holdTimer > 0f)
        {
            return;
        }

        var seat = board.Phase == LuckyPhase.Target ? board.Actor : board.Phase == LuckyPhase.Turn ? board.TurnSeat : -1;
        if (seat < 0 || !bots[seat])
        {
            return;
        }

        var plate = layout.PlateRect(seat);
        var y = layout.RowAbove(seat) ? plate.Max.Y + 10f * scale : plate.Min.Y - 10f * scale;
        LuckyDrawRenderer.DrawThinking(drawList, new Vector2(plate.Center.X, y) + shake, pulseClock,
            GameSeats.Color(seat), scale);
    }

    private void HandleControls(ImDrawListPtr drawList, in GameContext context, Vector2 shake, float scale,
        bool interactive)
    {
        if (finished || sheetOpen)
        {
            return;
        }

        var theme = context.Theme;
        var phase = board.Phase;
        if (phase == LuckyPhase.Turn && !bots[board.TurnSeat])
        {
            var command = DrawHitStay(drawList, theme, interactive && !flights.Busy && holdTimer <= 0f);
            if (command == 1)
            {
                React(board.Hit(), context, scale);
            }
            else if (command == 2)
            {
                React(board.Stay(), context, scale);
            }

            return;
        }

        if (phase == LuckyPhase.Target && board.Actor >= 0 && !bots[board.Actor])
        {
            LuckyDrawRenderer.DrawStatus(drawList, layout.Controls, Loc.T(PromptFor(board.PendingFace)),
                LuckyDrawRenderer.Gold, scale, 1f);
            if (!interactive || flights.Busy || holdTimer > 0f)
            {
                return;
            }

            for (var seat = 0; seat < board.Seats; seat++)
            {
                if (!board.IsValidTarget(seat))
                {
                    continue;
                }

                var zone = layout.Zone(seat).Translate(shake);
                if (UiInteract.HoverClick(zone.Min, zone.Max))
                {
                    React(board.Target(seat), context, scale);
                    return;
                }
            }

            return;
        }

        if (!board.Playing)
        {
            return;
        }

        var turn = board.Phase == LuckyPhase.Target ? board.Actor : board.TurnSeat;
        var text = board.Dealing || turn < 0 ? Loc.T(L.LuckyDraw.Dealing) : toPlay[turn];
        LuckyDrawRenderer.DrawStatus(drawList, layout.Controls, text, Muted, scale, 1f);
    }

    private int DrawHitStay(ImDrawListPtr drawList, Core.Theme.PhoneTheme theme, bool interactive)
    {
        var seat = board.TurnSeat;
        var pulse = Pulse.Wave(Pulse.Calm);
        ProgressRing.Glow(layout.HitCenter, layout.ButtonSize.X * 0.55f, Accent, 0.25f + 0.25f * pulse);
        var hit = GameHud.Button(layout.HitCenter, layout.ButtonSize, Loc.T(L.LuckyDraw.Hit), Accent, theme);
        var stay = GameHud.Button(layout.StayCenter, layout.ButtonSize, Loc.T(L.LuckyDraw.Stay), StayInk, theme);
        var captionY = layout.CaptionY;
        var caption = board.HasSecondChance(seat)
            ? Loc.T(L.LuckyDraw.ChanceReady)
            : riskLabel.Get(L.LuckyDraw.Risk, (int)MathF.Round(board.BustChance(seat) * 100f));
        var captionInk = board.HasSecondChance(seat) ? LuckyDrawRenderer.Mint : Muted;
        Typography.DrawCentered(drawList, new Vector2(layout.HitCenter.X, captionY), caption, captionInk,
            TextStyles.Caption1);
        if (hotSeat)
        {
            Typography.DrawCentered(drawList, new Vector2(layout.StayCenter.X, captionY), toPlay[seat],
                GameSeats.Color(seat), TextStyles.Caption1);
        }

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

    private void DrawSheet(ImDrawListPtr drawList, in GameContext context, float scale, bool interactive)
    {
        if (!sheetOpen || finished)
        {
            return;
        }

        var matchOver = board.Phase == LuckyPhase.MatchOver;
        var title = matchOver ? winLines[Math.Max(0, board.Winner)] : roundTitle.Get(L.LuckyDraw.RoundNumber, board.Round);
        var button = Loc.T(matchOver ? L.LuckyDraw.SeeResults : L.LuckyDraw.NextRound);
        var clicked = LuckyDrawRenderer.DrawSheet(drawList, context.Full, context.Safe, board,
            order.AsSpan(0, board.Seats), names, title, button, sheetProgress, Accent, context.Theme, scale,
            interactive && !demo);
        if (demo || !interactive)
        {
            return;
        }

        if (clicked || (sheetProgress >= 0.6f && GameInput.Pressed(ImGuiKey.Space, ImGuiKey.Enter)))
        {
            Continue(context);
        }
    }
}
