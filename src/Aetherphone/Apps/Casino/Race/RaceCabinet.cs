using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Race;

internal sealed class RaceCabinet : ICabinetIdle
{
    private const float GallopSeconds = 0.22f;
    private const float GallopStretchSeconds = 0.08f;
    private const float ReplayGallopSeconds = 0.5f;
    private const int TimerLowSeconds = 5;
    private const float SurgePunch = 0.035f;
    private const float LeadPunch = 0.06f;
    private const float OffPunch = 0.05f;
    private const float PhotoShake = 0.3f;
    private const float PillHeight = 28f;
    private const float PillFade = 0.4f;
    private const float RailRadius = 24f;
    private const float IdleBirds = 4f;
    private const float IdleSpeed = 0.18f;
    private const int FinishSparkles = 26;

    private static readonly Vector4 FlashWhite = new(1f, 1f, 1f, 1f);

    private readonly CasinoStore chips;
    private readonly CasinoRoomsStore rooms;
    private readonly Action openCashier;
    private readonly Action leaveRoom;
    private readonly RaceRoundPlayback playback = new();
    private readonly RaceCommentary commentary = new();
    private readonly RaceTrackView view = new();
    private readonly RaceBoards boards = new();
    private readonly RaceTicketBuilder builder = new();
    private readonly RaceTexts texts = new();
    private readonly BetComposer composer = new("##raceBet");
    private readonly bool[] mine = new bool[RaceRules.FieldSize];

    private string inlineReason = string.Empty;
    private string settledKey = string.Empty;
    private string ribbonText = string.Empty;
    private int ribbonSeconds = -1;
    private int ribbonStage = -1;
    private LanguageInfo? ribbonLanguage;
    private long roundSeen = -1;
    private int phaseSeen = -1;
    private int timerSecond = -1;
    private bool planSeen;
    private bool entered;
    private float gallopClock;
    private float idleClock;

    public RaceCabinet(CasinoStore chips, CasinoRoomsStore rooms, Action openCashier, Action leaveRoom)
    {
        this.chips = chips;
        this.rooms = rooms;
        this.openCashier = openCashier;
        this.leaveRoom = leaveRoom;
    }

    public static float DeckHeight => BetComposer.DeckHeightFor(true, false);

    public Backdrop IdleBackdrop => Backdrop.Arena;

    public bool WantsLandscape => entered;

    public CasinoStageSpec Spec(bool landscape)
    {
        var phase = rooms.Room.State?.Snapshot?.Phase ?? CasinoRoomPhases.Open;
        var deck = !landscape && phase == CasinoRoomPhases.Open ? DeckHeight : 0f;
        return new CasinoStageSpec(CasinoGames.Race, L.Race.Title, Backdrop.Arena, Room: !landscape,
            DeckHeight: deck, InstantAvailable: true, ReturnTenths: RaceRules.ReturnTenths);
    }

    public void Enter()
    {
        entered = true;
        inlineReason = string.Empty;
        composer.Reset(RaceRules.MinBet);
        builder.Reset();
        ResetShow();
        rooms.Enter(CasinoRoomIds.RaceTrack);
    }

    public void Reset()
    {
        if (entered)
        {
            rooms.Leave();
        }

        entered = false;
        inlineReason = string.Empty;
        builder.Reset();
        ResetShow();
    }

    public void Gate()
    {
        composer.Gate();
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        composer.DrawOverlay(screen, ui, false);
    }

    public void Draw(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, bool landscape)
    {
        var scale = UiScale.Current;
        var delta = frame.DeltaSeconds;
        texts.Sync();
        ConsumeStakeResults(stage);
        var room = rooms.Room;
        var held = room.State;
        var snapshot = held?.Snapshot;
        var board = held?.Race;
        var localNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var remaining = snapshot is null ? 0 : room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, localNow);
        playback.Update(snapshot, board, room.ServerNowUnixMs(localNow), frame.Instant);
        var drawList = ImGui.GetWindowDrawList();
        var safe = frame.Safe;
        var closedReason = room.ClosedReason;
        if (closedReason.Length > 0)
        {
            DrawClosed(drawList, ui, closedReason, safe, scale);
            return;
        }

        var state = chips.State;
        var runners = board?.Runners;
        if (state is null || snapshot is null)
        {
            LoadingPulse.Draw(safe.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        TrackRound(stage, snapshot, remaining);
        var bets = rooms.RaceBetsFor(snapshot.RoomId, snapshot.RoundIndex);
        CollectMine(bets);
        var stretch = playback.FinalStretch;
        view.Advance(delta, stretch);
        commentary.Advance(delta);
        HandleCues(stage, runners);
        Gallop(delta, stretch);
        Settle(stage, frame, snapshot, bets);
        var racing = snapshot.Phase == CasinoRoomPhases.Locked;
        var open = snapshot.Phase == CasinoRoomPhases.Open;
        var layout = RaceLayout.Compute(safe, frame.Deck, landscape, racing, open, DeckHeight, scale);
        var ribbonRect = landscape ? layout.Header : frame.Layout.Ribbon;
        PhaseRibbon.Draw(drawList, ribbonRect, RibbonLabel(snapshot.Phase, remaining), remaining,
            CasinoRoomCadence.RaceWindow(snapshot.Phase), snapshot.Occupancy,
            room.Attached ? CasinoColors.LightA : CasinoColors.InkMuted, scale);
        if (racing)
        {
            DrawRace(drawList, layout, runners, landscape, delta, stretch, scale);
            return;
        }

        if (runners is not { Length: RaceRules.FieldSize })
        {
            DrawCenterPill(drawList, layout.Main, Loc.T(L.Race.WaitingField), 1f, scale);
            return;
        }

        if (layout.Landscape)
        {
            var railMin = new Vector2(layout.Side.Min.X - 6f * scale, layout.Header.Max.Y);
            drawList.PushClipRect(frame.Full.Min, frame.Full.Max, true);
            Material.Frosted(drawList, railMin, new Vector2(frame.Full.Max.X + RailRadius * scale,
                frame.Full.Max.Y + RailRadius * scale), RailRadius * scale, scale);
            drawList.PopClipRect();
        }

        if (open)
        {
            DrawOpen(stage, frame, ui, drawList, layout, snapshot, runners, bets, state, scale);
            return;
        }

        DrawResult(drawList, layout, board!, runners, bets, stage.Phase, delta, scale);
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        var scale = UiScale.Current;
        idleClock += deltaSeconds;
        var ground = rect.Min.Y + rect.Height * 0.78f;
        drawList.AddRectFilledMultiColor(new Vector2(rect.Min.X, rect.Min.Y + rect.Height * 0.52f), rect.Max,
            ImGui.GetColorU32(new Vector4(0.27f, 0.19f, 0.13f, 0.0f)), ImGui.GetColorU32(new Vector4(0.27f, 0.19f, 0.13f, 0f)),
            ImGui.GetColorU32(new Vector4(0.42f, 0.30f, 0.19f, 0.9f)), ImGui.GetColorU32(new Vector4(0.42f, 0.30f, 0.19f, 0.9f)));
        var height = MathF.Min(rect.Height * 0.32f, 64f * scale);
        for (var bird = 0; bird < (int)IdleBirds; bird++)
        {
            var lane = bird / IdleBirds;
            var travel = (idleClock * IdleSpeed * (1f + 0.07f * bird) + bird * 0.27f) % 1.2f - 0.1f;
            var foot = new Vector2(rect.Min.X + rect.Width * travel, ground - lane * height * 0.35f);
            var frame = (int)(idleClock * 7f + bird) & 1;
            RaceBirdArt.DrawSide(drawList, foot, height * (0.8f + 0.2f * (1f - lane)), bird * 3, bird, bird * 2, frame,
                1f);
        }

        CasinoSigns.Draw(drawList, CasinoSign.Race, new Vector2(rect.Center.X, rect.Min.Y + rect.Height * 0.22f),
            rect.Height * 0.16f, CasinoColors.LightB, 0.8f + 0.2f * MathF.Sin(idleClock * 3f));
    }

    private void ResetShow()
    {
        playback.Reset();
        commentary.Clear();
        view.Reset();
        boards.Reset();
        settledKey = string.Empty;
        roundSeen = -1;
        phaseSeen = -1;
        timerSecond = -1;
        planSeen = false;
        gallopClock = 0f;
        Array.Clear(mine);
    }

    private void TrackRound(CasinoStage stage, CasinoRoomSnapshotDto snapshot, long remainingMs)
    {
        if (snapshot.RoundIndex != roundSeen)
        {
            roundSeen = snapshot.RoundIndex;
            builder.Follow(snapshot.RoundIndex);
            view.Reset();
            commentary.Clear();
            planSeen = false;
            timerSecond = -1;
        }

        if (!planSeen && playback.HasOrder)
        {
            planSeen = true;
            commentary.Begin(playback.Seed);
        }

        if (snapshot.Phase != phaseSeen)
        {
            var first = phaseSeen < 0;
            phaseSeen = snapshot.Phase;
            if (!first)
            {
                AnnouncePhase(stage, snapshot.Phase);
            }
        }

        if (snapshot.Phase != CasinoRoomPhases.Open)
        {
            return;
        }

        var seconds = (int)((Math.Max(0, remainingMs) + 999) / 1000);
        if (seconds == timerSecond)
        {
            return;
        }

        timerSecond = seconds;
        if (seconds is > 0 and <= TimerLowSeconds)
        {
            CasinoSfx.Play(UiSound.TimerLow);
        }
    }

    private static void AnnouncePhase(CasinoStage stage, int phase)
    {
        if (phase == CasinoRoomPhases.Open)
        {
            CasinoSfx.Play(UiSound.TurnChime);
            return;
        }

        if (phase == CasinoRoomPhases.Locked)
        {
            stage.Fx.Flash(CasinoColors.LightB, 0.18f);
        }
    }

    private void CollectMine(CasinoRaceBetsDto? bets)
    {
        Array.Clear(mine);
        var tickets = bets?.Tickets;
        if (tickets is null)
        {
            return;
        }

        for (var index = 0; index < tickets.Length; index++)
        {
            if (RaceRules.IsRunner(tickets[index].Runner))
            {
                mine[tickets[index].Runner] = true;
            }

            if (RaceRules.IsRunner(tickets[index].RunnerB))
            {
                mine[tickets[index].RunnerB] = true;
            }
        }
    }

    private void ConsumeStakeResults(CasinoStage stage)
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
                stage.Particles.Sparkle(composer.KnobRect.Center, 14, CasinoColors.Money, 90f * UiScale.Current,
                    3f * UiScale.Current, 0.6f);
                builder.Clear();
            }
        }

        if (rooms.TakeStakeFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }
    }

    private void HandleCues(CasinoStage stage, CasinoRaceRunnerDto[]? runners)
    {
        while (playback.TryTakeCue(out var cue))
        {
            var name = NameOf(runners, cue.Slot);
            switch (cue.Cue)
            {
                case RaceCue.Off:
                    CasinoSfx.Play(UiSound.RaceHorn);
                    stage.Fx.Flash(FlashWhite, 0.22f);
                    view.Punch(OffPunch);
                    commentary.Speak(cue.Cue, name);
                    break;
                case RaceCue.Surge:
                    view.Punch(SurgePunch);
                    stage.Fx.Flash(CasinoColors.LightB, 0.07f);
                    commentary.Speak(cue.Cue, name);
                    break;
                case RaceCue.LeadChange:
                    view.Punch(LeadPunch);
                    commentary.Speak(cue.Cue, name);
                    break;
                case RaceCue.Fade:
                case RaceCue.FinalStretch:
                    commentary.Speak(cue.Cue, name);
                    break;
                case RaceCue.PhotoFinish:
                    stage.Fx.Flash(FlashWhite, 0.9f);
                    view.Shake(PhotoShake);
                    commentary.Speak(cue.Cue, name);
                    break;
                case RaceCue.Winner:
                    stage.Particles.Sparkle(view.FinishScreen, FinishSparkles, CasinoColors.MoneyHighlight,
                        140f * UiScale.Current, 3f * UiScale.Current, 0.8f);
                    CasinoSfx.Play(UiSound.RaceHorn);
                    if (!playback.PhotoFinish)
                    {
                        commentary.Speak(cue.Cue, name);
                    }

                    break;
            }
        }
    }

    private static string NameOf(CasinoRaceRunnerDto[]? runners, int slot)
    {
        if (runners is not { Length: RaceRules.FieldSize } || !RaceRules.IsRunner(slot))
        {
            return string.Empty;
        }

        return runners[slot].Name;
    }

    private void Gallop(float deltaSeconds, float stretch)
    {
        var interval = playback.Stage switch
        {
            RaceStage.Running => GallopSeconds - GallopStretchSeconds * stretch,
            RaceStage.Replay => ReplayGallopSeconds,
            _ => 0f,
        };
        if (interval <= 0f)
        {
            gallopClock = 0f;
            return;
        }

        gallopClock += deltaSeconds;
        if (gallopClock < interval)
        {
            return;
        }

        gallopClock = 0f;
        CasinoSfx.Play(UiSound.Gallop);
    }

    private void Settle(CasinoStage stage, in CasinoStageFrame frame, CasinoRoomSnapshotDto snapshot,
        CasinoRaceBetsDto? bets)
    {
        if (playback.Stage is not (RaceStage.Finished or RaceStage.Result) || bets is null
            || bets.Phase == CasinoRoomPhases.Open
            || string.Equals(settledKey, playback.RoundKey, StringComparison.Ordinal))
        {
            return;
        }

        settledKey = playback.RoundKey;
        var staked = bets.MyStake;
        if (staked <= 0)
        {
            return;
        }

        stage.Settle(new CasinoBetRecord(L.Race.Title, staked, bets.MyPayout, bets.RoundId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        chips.RefreshNow();
        var live = playback.WatchedLive || (frame.Instant && snapshot.Phase == CasinoRoomPhases.Locked);
        if (live)
        {
            stage.Celebration.Celebrate(staked, bets.MyPayout, frame.Safe.Center, frame.Instant);
        }
    }

    private string RibbonLabel(int phase, long remainingMs)
    {
        var stageIndex = (int)playback.Stage;
        var seconds = (int)((Math.Max(0, remainingMs) + 999) / 1000);
        if (stageIndex == ribbonStage && seconds == ribbonSeconds && ReferenceEquals(ribbonLanguage, Loc.Current))
        {
            return ribbonText;
        }

        ribbonStage = stageIndex;
        ribbonSeconds = seconds;
        ribbonLanguage = Loc.Current;
        ribbonText = playback.Stage switch
        {
            RaceStage.Paddock or RaceStage.Waiting when phase == CasinoRoomPhases.Open =>
                Loc.T(L.Casino.WheelBetsCloseIn, TimeText.Duration(seconds)),
            RaceStage.Running => Loc.T(L.Race.Running),
            RaceStage.Replay => Loc.T(L.Race.Replay),
            RaceStage.Gates => Loc.T(L.Casino.WheelBetsClosed),
            _ => Loc.T(L.Casino.RoomNextIn, TimeText.Duration(phase == CasinoRoomPhases.Result
                ? seconds
                : seconds + CasinoRoomCadence.RaceResultSeconds)),
        };
        return ribbonText;
    }

    private void DrawRace(ImDrawListPtr drawList, in RaceLayout layout, CasinoRaceRunnerDto[]? runners,
        bool landscape, float deltaSeconds, float stretch, float scale)
    {
        if (landscape)
        {
            view.DrawSide(drawList, layout.Main, playback, runners, deltaSeconds, stretch, scale);
        }
        else
        {
            view.DrawVertical(drawList, layout.Main, playback, runners, deltaSeconds, stretch, scale);
        }

        if (playback.HasOrder)
        {
            view.DrawTicker(drawList, layout.Ticker, playback, runners, mine, deltaSeconds, scale);
        }

        if (playback.Stage == RaceStage.Gates || !playback.HasOrder)
        {
            DrawCenterPill(drawList, layout.Main, Loc.T(L.Race.Gates), 1f, scale);
            return;
        }

        if (playback.Stage == RaceStage.Replay)
        {
            DrawReplayTag(drawList, layout.Main, scale);
        }

        if (commentary.Showing)
        {
            var alpha = Math.Clamp((RaceCommentary.LineSeconds - commentary.Age) / PillFade, 0f, 1f);
            DrawTopPill(drawList, layout.Main, commentary.Line, alpha, scale);
        }
    }

    private void DrawOpen(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, ImDrawListPtr drawList,
        in RaceLayout layout, CasinoRoomSnapshotDto snapshot, CasinoRaceRunnerDto[] runners, CasinoRaceBetsDto? bets,
        CasinoStateDto state, float scale)
    {
        var sitting = state.Sitting;
        var tickets = bets?.Tickets;
        var ticketCount = tickets?.Length ?? 0;
        var full = ticketCount >= RaceRules.MaxTickets;
        var selectable = sitting is not null && !frame.Blocked && !full;
        var tapped = boards.DrawTote(drawList, ui, layout.Main, runners, builder, texts, selectable,
            snapshot.RoundIndex, frame.DeltaSeconds, scale);
        if (tapped >= 0)
        {
            builder.Tap(tapped);
            inlineReason = string.Empty;
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        var ride = RideAmount(bets, sitting);
        var hint = full ? Loc.T(L.Race.TicketsFull) : HintLine(runners);
        if (layout.Landscape)
        {
            DrawLandscapeRail(drawList, ui, layout, bets, ride, hint, snapshot.RoundIndex, frame.DeltaSeconds, scale);
        }
        else
        {
            DrawPortraitStrip(drawList, ui, layout.Side, bets, ride, hint, snapshot.RoundIndex, frame.DeltaSeconds,
                scale);
        }

        DrawReason(drawList, ui, layout, scale);
        if (!layout.HasDeck)
        {
            return;
        }

        if (sitting is null)
        {
            DrawSeatMissing(drawList, ui, layout.Deck, scale);
            return;
        }

        DrawDeck(stage, frame, ui, layout.Deck, state, sitting, full);
    }

    private long RideAmount(CasinoRaceBetsDto? bets, CasinoSittingDto? sitting)
    {
        if (bets is null || sitting is null || builder.RiddenRound == bets.RoundIndex)
        {
            return 0;
        }

        return RaceTicketBuilder.RideAmount(bets.PreviousPayout, chips.Ceiling.MaxBet, sitting.Stack);
    }

    private string HintLine(CasinoRaceRunnerDto[] runners)
    {
        if (!RaceRules.IsRunner(builder.First))
        {
            return Loc.T(L.Race.PromptPick);
        }

        if (builder.WantsSecond)
        {
            return Loc.T(L.Race.PromptSecond);
        }

        var first = runners[builder.First];
        var partner = RaceRules.IsRunner(builder.RunnerB) ? runners[builder.RunnerB].OddsHundredths : 0;
        var pay = RaceRules.PotentialPayHundredths(builder.Kind, first.OddsHundredths, first.PlaceOddsHundredths,
            partner);
        return texts.PayLine(builder.Kind, pay);
    }

    private void DrawLandscapeRail(ImDrawListPtr drawList, AppSkin ui, in RaceLayout layout, CasinoRaceBetsDto? bets,
        long ride, string hint, long roundIndex, float deltaSeconds, float scale)
    {
        var side = layout.Side;
        var rideHeight = RaceLayout.RideHeight * scale;
        var hintHeight = Typography.LineHeight(TextStyles.Footnote);
        var gap = RaceLayout.Gap * scale;
        var bottom = side.Max.Y;
        if (ride > 0)
        {
            var rideRect = new Rect(new Vector2(side.Min.X, bottom - rideHeight), new Vector2(side.Max.X, bottom));
            DrawRide(ui, rideRect, ride, bets!.RoundIndex);
            bottom = rideRect.Min.Y - gap * 0.5f;
        }

        var hintTop = bottom - hintHeight;
        Marquee.DrawLeftAuto(drawList, "casino.race.hint", hint, side.Min.X, hintTop, side.Width, TextStyles.Footnote,
            builder.Ready ? CasinoColors.MoneyHighlight : CasinoColors.InkMuted);
        var list = new Rect(side.Min, new Vector2(side.Max.X, MathF.Max(side.Min.Y, hintTop - gap * 0.5f)));
        boards.DrawTickets(drawList, list, bets?.Tickets, texts, false, false, roundIndex, deltaSeconds, scale);
    }

    private void DrawPortraitStrip(ImDrawListPtr drawList, AppSkin ui, Rect strip, CasinoRaceBetsDto? bets,
        long ride, string hint, long roundIndex, float deltaSeconds, float scale)
    {
        var rowHeight = RaceLayout.RideHeight * scale * 0.8f;
        var rideWidth = 0f;
        if (ride > 0)
        {
            var label = texts.Ride(ride);
            rideWidth = MathF.Min(strip.Width * 0.5f, Button.WidthFor(label, ButtonSize.Small));
            DrawRide(ui, new Rect(new Vector2(strip.Max.X - rideWidth, strip.Min.Y),
                new Vector2(strip.Max.X, strip.Min.Y + rowHeight)), ride, bets!.RoundIndex);
            rideWidth += RaceLayout.Gap * scale;
        }

        var hintTop = strip.Min.Y + (rowHeight - Typography.LineHeight(TextStyles.Footnote)) * 0.5f;
        Marquee.DrawLeftAuto(drawList, "casino.race.hint", hint, strip.Min.X, hintTop,
            MathF.Max(1f, strip.Width - rideWidth), TextStyles.Footnote,
            builder.Ready ? CasinoColors.MoneyHighlight : CasinoColors.InkMuted);
        var list = new Rect(new Vector2(strip.Min.X, strip.Min.Y + rowHeight + 4f * scale), strip.Max);
        boards.DrawTickets(drawList, list, bets?.Tickets, texts, false, true, roundIndex, deltaSeconds, scale);
    }

    private void DrawRide(AppSkin ui, Rect rect, long ride, long roundIndex)
    {
        var enabled = builder.Ready && !rooms.StakeInFlight;
        if (!Button.Draw(rect, texts.Ride(ride), ui.Ink, ButtonStyle.Tinted, enabled: enabled))
        {
            return;
        }

        builder.MarkRidden(roundIndex);
        composer.Reset(ride);
        inlineReason = string.Empty;
        rooms.PlaceRaceBet(builder.Kind, builder.First, builder.RunnerB, ride);
    }

    private void DrawReason(ImDrawListPtr drawList, AppSkin ui, in RaceLayout layout, float scale)
    {
        if (inlineReason.Length == 0)
        {
            return;
        }

        var host = layout.Main;
        var message = Loc.T(CasinoReasons.MessageFor(inlineReason));
        var height = CasinoNotice.Height(CasinoNoticeKind.Reason, string.Empty, message, host.Width, scale);
        CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty, message, host.Min.X,
            host.Max.Y - height, host.Width, scale);
    }

    private void DrawDeck(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, Rect deck, CasinoStateDto state,
        CasinoSittingDto sitting, bool full)
    {
        var enabled = !state.StakesPaused && !state.Draining && !rooms.StakeInFlight && !frame.Blocked
                      && builder.Ready && !full;
        var model = new BetComposerModel(RaceRules.MinBet, chips.Ceiling.MaxBet, sitting.Stack, L.Strip.BetFor,
            enabled, Knob: true, Repeat: stage.RepeatPressed(), Busy: rooms.StakeInFlight);
        var action = composer.Draw(ui, deck, model, frame.DeltaSeconds);
        var knob = composer.KnobRect;
        if (knob.Width > 0f)
        {
            var picked = SegmentStrip.Draw("##raceKind", knob, texts.KindLabels, builder.Kind,
                Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary), ui.Accent, ui.MutedInk, CasinoColors.InkTitle);
            if (picked != builder.Kind)
            {
                builder.SetKind(picked);
                CasinoSfx.Play(UiSound.ChipSlide);
            }
        }

        if (action != BetComposerAction.Confirm)
        {
            return;
        }

        inlineReason = string.Empty;
        rooms.PlaceRaceBet(builder.Kind, builder.First, builder.RunnerB, composer.Amount);
    }

    private void DrawResult(ImDrawListPtr drawList, in RaceLayout layout, CasinoRaceRoomStateDto board,
        CasinoRaceRunnerDto[] runners, CasinoRaceBetsDto? bets, float phase, float deltaSeconds, float scale)
    {
        boards.DrawResult(drawList, layout.Main, board, runners, texts, board.RoundIndex, phase, deltaSeconds, scale);
        var side = layout.Side;
        var totalHeight = RaceLayout.RideHeight * scale;
        var compact = !layout.Landscape;
        if (compact)
        {
            RaceBoards.DrawTotal(drawList, new Rect(side.Min, new Vector2(side.Max.X, side.Min.Y + totalHeight * 0.8f)),
                bets, texts, scale);
            boards.DrawTickets(drawList, new Rect(
                    new Vector2(side.Min.X, side.Min.Y + totalHeight * 0.8f + 4f * scale), side.Max), bets?.Tickets,
                texts, true, true, board.RoundIndex, deltaSeconds, scale);
            return;
        }

        RaceBoards.DrawTotal(drawList, new Rect(new Vector2(side.Min.X, side.Max.Y - totalHeight), side.Max), bets,
            texts, scale);
        boards.DrawTickets(drawList,
            new Rect(side.Min, new Vector2(side.Max.X, side.Max.Y - totalHeight - 4f * scale)), bets?.Tickets, texts,
            true, false, board.RoundIndex, deltaSeconds, scale);
    }

    private void DrawCenterPill(ImDrawListPtr drawList, Rect area, string text, float alpha, float scale)
    {
        var height = PillHeight * scale;
        var style = TextStyles.SubheadlineEmphasized;
        var maxWidth = area.Width * 0.86f;
        var fitted = Typography.FitText(text, maxWidth - height, style);
        var width = Typography.Measure(fitted, style).X + height;
        var center = area.Center;
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        Material.FrostedGlass(drawList, min, min + new Vector2(width, height), height * 0.5f, scale, alpha);
        Typography.DrawCentered(drawList, center, fitted, CasinoColors.InkTitle with { W = alpha }, style);
    }

    private static void DrawTopPill(ImDrawListPtr drawList, Rect area, string text, float alpha, float scale)
    {
        if (alpha <= 0f || text.Length == 0)
        {
            return;
        }

        var height = PillHeight * scale;
        var style = TextStyles.SubheadlineEmphasized;
        var fitted = Typography.FitText(text, area.Width * 0.86f - height, style);
        var width = Typography.Measure(fitted, style).X + height;
        var center = new Vector2(area.Center.X, area.Min.Y + height * 0.5f + 6f * scale);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        Material.FrostedGlass(drawList, min, min + new Vector2(width, height), height * 0.5f, scale, alpha);
        Typography.DrawCentered(drawList, center, fitted, CasinoColors.InkTitle with { W = alpha }, style);
    }

    private static void DrawReplayTag(ImDrawListPtr drawList, Rect area, float scale)
    {
        var style = TextStyles.FootnoteEmphasized;
        var label = Loc.T(L.Race.Replay);
        var height = 22f * scale;
        var dot = 4f * scale;
        var fitted = Typography.FitText(label, area.Width * 0.4f, style);
        var width = Typography.Measure(fitted, style).X + height + dot * 2f;
        var min = new Vector2(area.Min.X + 8f * scale, area.Max.Y - height - 8f * scale);
        Material.FrostedGlass(drawList, min, min + new Vector2(width, height), height * 0.5f, scale);
        var blink = 0.4f + 0.6f * Pulse.Wave(Pulse.Fast);
        drawList.AddCircleFilled(new Vector2(min.X + height * 0.5f, min.Y + height * 0.5f), dot,
            ImGui.GetColorU32(CasinoColors.LightA with { W = blink }), 12);
        Typography.Draw(drawList,
            new Vector2(min.X + height * 0.5f + dot * 2f, min.Y + (height - Typography.LineHeight(style)) * 0.5f),
            fitted, CasinoColors.InkTitle, style);
    }

    private void DrawClosed(ImDrawListPtr drawList, AppSkin ui, string reason, Rect safe, float scale)
    {
        var title = Loc.T(L.Race.ClosedTitle);
        var hint = Loc.T(CasinoReasons.TryMessage(reason, out var known) ? known : L.Race.ClosedHint);
        CasinoNotice.DrawWithAction(drawList, ui, CasinoNoticeKind.Card, title, hint, Loc.T(L.Casino.WheelBackToFloor),
            safe.Min.X, safe.Min.Y + Metrics.Space.Lg * scale, safe.Width, scale, out var pressed);
        if (pressed)
        {
            leaveRoom();
        }
    }

    private void DrawSeatMissing(ImDrawListPtr drawList, AppSkin ui, Rect deck, float scale)
    {
        var inset = BetComposer.Pad * scale;
        var title = Loc.T(L.Casino.CabinetNoChipsTitle);
        var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(deck.Min.X + inset, deck.Min.Y + inset),
            Typography.FitText(title, deck.Width - inset * 2f, TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var top = deck.Min.Y + inset + titleHeight + Metrics.Space.Sm * scale;
        var rect = new Rect(new Vector2(deck.Min.X + inset, top),
            new Vector2(deck.Max.X - inset, top + Button.LargeHeight * scale));
        if (Button.Draw(drawList, rect, Loc.T(L.Casino.Cashier), ui.Ink))
        {
            openCashier();
        }
    }
}
