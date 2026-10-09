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
    private readonly RaceFieldList field = new();
    private readonly RaceTicketStrip strip = new();
    private readonly RaceDeck deck = new();
    private readonly bool[] mine = new bool[RaceRules.FieldSize];
    private readonly long[] stakes = new long[RaceRules.FieldSize];

    private string inlineReason = string.Empty;
    private string settledKey = string.Empty;
    private long roundSeen = -1;
    private int phaseSeen = -1;
    private int timerSecond = -1;
    private bool planSeen;
    private bool entered;
    private float gallopClock;
    private float idleClock;
    private Rect deckRect;

    public RaceCabinet(CasinoStore chips, CasinoRoomsStore rooms, Action openCashier, Action leaveRoom)
    {
        this.chips = chips;
        this.rooms = rooms;
        this.openCashier = openCashier;
        this.leaveRoom = leaveRoom;
    }

    public static float DeckHeight => RaceDeckLayout.Height;

    public Backdrop IdleBackdrop => Backdrop.Arena;

    public bool WantsLandscape =>
        entered && (rooms.Room.State?.Snapshot?.Phase ?? CasinoRoomPhases.Open) == CasinoRoomPhases.Locked;

    public CasinoStageSpec Spec(bool landscape)
    {
        var phase = rooms.Room.State?.Snapshot?.Phase ?? CasinoRoomPhases.Open;
        var deckHeight = !landscape && phase == CasinoRoomPhases.Open ? DeckHeight : 0f;
        return new CasinoStageSpec(CasinoGames.Race, L.Race.Title, Backdrop.Arena, DeckHeight: deckHeight,
            InstantAvailable: true, ReturnTenths: RaceRules.ReturnTenths);
    }

    public void Enter()
    {
        entered = true;
        inlineReason = string.Empty;
        deck.Reset(RaceRules.MinBet);
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
        if (!racing)
        {
            DrawCountdown(drawList, frame, snapshot.Phase, remaining, room.Attached, state, scale);
        }

        if (racing)
        {
            DrawRace(drawList, frame, runners, landscape, delta, stretch, scale);
            return;
        }

        if (runners is not { Length: RaceRules.FieldSize })
        {
            DrawCenterPill(drawList, safe, Loc.T(L.Race.WaitingField), 1f, scale);
            return;
        }

        if (open)
        {
            DrawOpen(stage, frame, ui, drawList, landscape, snapshot, runners, bets, state, scale);
            return;
        }

        var layout = RaceLayout.Compute(safe, frame.Deck, landscape, false, false, 0f, scale);
        if (layout.Landscape)
        {
            DrawRail(drawList, frame.Full, new Vector2(layout.Side.Min.X, layout.Header.Max.Y), scale);
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
        field.Reset();
        strip.Reset();
        settledKey = string.Empty;
        roundSeen = -1;
        phaseSeen = -1;
        timerSecond = -1;
        planSeen = false;
        gallopClock = 0f;
        Array.Clear(mine);
        Array.Clear(stakes);
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
        Array.Clear(stakes);
        var tickets = bets?.Tickets;
        if (tickets is null)
        {
            return;
        }

        for (var index = 0; index < tickets.Length; index++)
        {
            var ticket = tickets[index];
            if (RaceRules.IsRunner(ticket.Runner))
            {
                mine[ticket.Runner] = true;
                stakes[ticket.Runner] += ticket.Amount;
            }

            if (RaceRules.IsRunner(ticket.RunnerB))
            {
                mine[ticket.RunnerB] = true;
                stakes[ticket.RunnerB] += ticket.Amount;
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
                stage.Particles.Sparkle(deckRect.Center, 14, CasinoColors.Money, 90f * UiScale.Current,
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

    private void DrawCountdown(ImDrawListPtr drawList, in CasinoStageFrame frame, int phase, long remainingMs,
        bool attached, CasinoStateDto state, float scale)
    {
        var stageLayout = frame.Layout;
        var balance = RaceAmounts.Text(state.Sitting?.Stack ?? 0);
        var capsuleWidth = RaceCountdownLayout.CapsuleWidth(Typography.Measure(balance, TextStyles.Headline).X,
            stageLayout.CapsuleMaxWidth, scale);
        var chip = RaceCountdownLayout.Compute(stageLayout.CapsuleCenter, capsuleWidth, stageLayout.InfoCenter,
            stageLayout.ChipRadiusPixels, scale, out var wide);
        RaceCountdownChip.Draw(drawList, chip, wide, remainingMs, CasinoRoomCadence.RaceWindow(phase),
            attached ? CasinoColors.LightA : CasinoColors.InkMuted, scale);
    }

    private static void DrawRail(ImDrawListPtr drawList, Rect full, Vector2 corner, float scale)
    {
        var inset = RaceOpenLayout.RailInset * scale;
        drawList.PushClipRect(full.Min, full.Max, true);
        Material.Frosted(drawList, corner - new Vector2(inset, inset),
            new Vector2(full.Max.X + RailRadius * scale, full.Max.Y + RailRadius * scale), RailRadius * scale, scale);
        drawList.PopClipRect();
    }

    private void DrawRace(ImDrawListPtr drawList, in CasinoStageFrame frame, CasinoRaceRunnerDto[]? runners,
        bool landscape, float deltaSeconds, float stretch, float scale)
    {
        var layout = RaceTrackLayout.Compute(frame.Full, frame.Layout.Band.Max.Y, scale);
        if (landscape)
        {
            view.DrawSide(drawList, layout.Stand, layout.Track, playback, runners, mine, deltaSeconds, stretch, scale);
        }
        else
        {
            view.DrawVertical(drawList, layout.Stand, layout.Track, playback, runners, mine, deltaSeconds, stretch,
                scale);
        }

        RaceHud.DrawProgress(drawList, layout.Progress, playback, runners, mine, scale);
        RaceHud.DrawTopThree(drawList, layout.TopThree, playback, mine, scale);
        if (playback.Stage == RaceStage.Gates || !playback.HasOrder)
        {
            RaceHud.DrawCaption(drawList, layout.Caption, Loc.T(L.Race.Gates), 1f, false, scale);
            return;
        }

        if (commentary.Showing)
        {
            RaceHud.DrawCaption(drawList, layout.Caption, commentary.Line,
                RaceHud.CaptionAlpha(commentary.Age, RaceCommentary.LineSeconds), false, scale);
            return;
        }

        if (playback.Stage == RaceStage.Replay)
        {
            RaceHud.DrawCaption(drawList, layout.Caption, Loc.T(L.Race.Replay), 1f, true, scale);
        }
    }

    private void DrawOpen(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, ImDrawListPtr drawList,
        bool landscape, CasinoRoomSnapshotDto snapshot, CasinoRaceRunnerDto[] runners, CasinoRaceBetsDto? bets,
        CasinoStateDto state, float scale)
    {
        var sitting = state.Sitting;
        var tickets = bets?.Tickets ?? Array.Empty<CasinoRaceTicketDto>();
        var full = tickets.Length >= RaceRules.MaxTickets;
        var selectable = sitting is not null && !frame.Blocked && !full;
        var safe = frame.Safe;
        var message = inlineReason.Length > 0 ? Loc.T(CasinoReasons.MessageFor(inlineReason)) : string.Empty;
        var noticeHeight = message.Length > 0
            ? CasinoNotice.Height(CasinoNoticeKind.Reason, string.Empty, message,
                RaceOpenLayout.ColumnWidth(safe, landscape, scale), scale)
            : 0f;
        var layout = RaceOpenLayout.Compute(safe, frame.Deck, landscape, tickets.Length, strip.Expanded, noticeHeight,
            scale);
        if (layout.HasRail)
        {
            DrawRail(drawList, frame.Full, layout.Rail.Min, scale);
        }

        var tapped = field.Draw(ui, layout.List, runners, builder, texts, stakes, selectable, snapshot.RoundIndex,
            frame.DeltaSeconds, scale);
        if (tapped >= 0)
        {
            builder.Tap(tapped);
            inlineReason = string.Empty;
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        strip.Draw(drawList, layout.Tickets, layout.TicketRows, tickets, runners, texts, snapshot.RoundIndex,
            frame.DeltaSeconds, scale);
        if (layout.HasNotice)
        {
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty, message, layout.Notice.Min.X,
                layout.Notice.Min.Y, layout.Notice.Width, scale);
        }

        deckRect = layout.Deck;
        if (layout.Deck.Height <= 0f)
        {
            return;
        }

        if (sitting is null)
        {
            DrawSeatMissing(drawList, ui, layout.Deck, scale);
            return;
        }

        DrawDeck(stage, frame, ui, layout.Deck, state, sitting, runners, bets, full);
    }

    private long RideAmount(CasinoRaceBetsDto? bets, CasinoSittingDto? sitting)
    {
        if (bets is null || sitting is null || builder.RiddenRound == bets.RoundIndex)
        {
            return 0;
        }

        return RaceTicketBuilder.RideAmount(bets.PreviousPayout, chips.Ceiling.MaxBet, sitting.Stack);
    }

    private string PrimaryLabel(CasinoRaceRunnerDto[] runners, bool full)
    {
        if (full)
        {
            return Loc.T(L.Race.TicketsFull);
        }

        if (builder.Ready)
        {
            return texts.BetLine(deck.Amount, builder.Kind, builder.First, builder.RunnerB, runners);
        }

        return Loc.T(builder.WantsSecond ? L.Race.PromptSecond : L.Race.PromptPick);
    }

    private void DrawDeck(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, Rect deckArea,
        CasinoStateDto state, CasinoSittingDto sitting, CasinoRaceRunnerDto[] runners, CasinoRaceBetsDto? bets,
        bool full)
    {
        var enabled = !state.StakesPaused && !state.Draining && !rooms.StakeInFlight && !frame.Blocked && !full;
        var ride = RideAmount(bets, sitting);
        var model = new RaceDeckModel(chips.Ceiling.MaxBet, sitting.Stack, builder.Kind, PrimaryLabel(runners, full),
            builder.Ready, enabled, ride > 0 ? texts.Ride(ride) : string.Empty, stage.RepeatPressed());
        var action = deck.Draw(ui, deckArea, texts.KindLabels, model, frame.DeltaSeconds);
        switch (action)
        {
            case RaceDeckAction.Kind:
                builder.SetKind(deck.PickedKind);
                CasinoSfx.Play(UiSound.ChipSlide);
                break;
            case RaceDeckAction.Bet:
                inlineReason = string.Empty;
                rooms.PlaceRaceBet(builder.Kind, builder.First, builder.RunnerB, deck.Amount);
                break;
            case RaceDeckAction.Ride:
                builder.MarkRidden(bets?.RoundIndex ?? -1);
                deck.Reset(ride);
                inlineReason = string.Empty;
                rooms.PlaceRaceBet(builder.Kind, builder.First, builder.RunnerB, ride);
                break;
        }
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

    private void DrawSeatMissing(ImDrawListPtr drawList, AppSkin ui, Rect deckArea, float scale)
    {
        var layout = RaceDeckLayout.Compute(deckArea, 0f, scale);
        var title = Loc.T(L.Casino.CabinetNoChipsTitle);
        var style = TextStyles.SubheadlineEmphasized;
        var message = layout.Message;
        Typography.Draw(drawList, new Vector2(message.Min.X, message.Center.Y - Typography.LineHeight(style) * 0.5f),
            Typography.FitText(title, message.Width, style), ui.TitleInk, style);
        if (Button.Draw(layout.Primary, Loc.T(L.Casino.Cashier), ui.Ink, ButtonStyle.Prominent))
        {
            openCashier();
        }
    }
}
