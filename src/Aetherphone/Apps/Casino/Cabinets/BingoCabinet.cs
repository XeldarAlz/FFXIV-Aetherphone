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

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class BingoCabinet : ICabinetIdle
{
    private const float RailGap = 8f;
    private const float RailLabelHeight = 18f;
    private const float DragThreshold = 6f;
    private const float RailSettleSeconds = 0.12f;
    private const float WheelStep = 48f;
    private const float CallerBallShare = 0.34f;
    private const float FlightLift = 34f;
    private const float PopOutShare = 0.22f;
    private const float HeartbeatGapSeconds = 0.2f;
    private const float HeartbeatLubRate = 0.62f;
    private const float HeartbeatDubRate = 0.55f;
    private const double HeartbeatPeriod = 900.0;
    private const float PodiumGap = 6f;
    private const float PodiumPad = 5f;
    private const float AvatarRadius = 7f;
    private const float AvatarStep = 10f;
    private const int MaxAvatars = 6;
    private const float OverlayPad = 14f;
    private const float RowHeight = 26f;
    private const float IdlePopSeconds = 1.4f;
    private const float KnobPillGap = 6f;
    private const float VeilAlpha = 0.72f;

    private static readonly MarqueeId CallerMarquee = new("casino.bingo.caller", 0L);

    private static readonly LocString[] StageNames =
    {
        L.Casino.BingoStageLine,
        L.Casino.BingoStageTwoLines,
        L.Casino.BingoStageFullHouse,
    };

    private static readonly Vector4[] AvatarTints =
    {
        CasinoColors.LightA,
        CasinoColors.LightB,
        CasinoColors.Money,
        new(0.55f, 0.85f, 0.45f, 1f),
        new(0.80f, 0.58f, 0.98f, 1f),
        new(1f, 0.55f, 0.35f, 1f),
    };

    private readonly CasinoStore chips;
    private readonly CasinoRoomsStore rooms;
    private readonly Action leaveRoom;
    private readonly BingoRoundPlayback playback = new();
    private readonly BingoTumbler tumbler = new();
    private readonly BetComposer composer = new("##bingoBuy");
    private readonly long[] ladder = new long[BingoRules.StageCount];
    private readonly LabelSlot[] cardLabels = new LabelSlot[BingoRules.MaxCards];
    private readonly LabelSlot[] stageBalls = new LabelSlot[BingoRules.StageCount];
    private readonly LabelSlot[] stageWinners = new LabelSlot[BingoRules.StageCount];

    private BingoLabel ribbonLabel;
    private BingoLabel callerLabel;
    private BingoLabel captionLabel;
    private LabelSlot earlyBirdLabel;
    private LabelSlot holdingLabel;
    private LabelSlot outcomeLabel;
    private Spring railOffset;
    private BingoHallLayout layout;
    private string inlineReason = string.Empty;
    private string celebratedRoundId = string.Empty;
    private int requestedCards = 1;
    private string priceText = string.Empty;
    private LanguageInfo? priceLanguage;
    private float lightPhase;
    private float heartbeatDub;
    private float railDragFrom;
    private float railDragStart;
    private float idlePop;
    private bool railDragging;
    private bool railDragMoved;
    private bool entered;

    public BingoCabinet(CasinoStore chips, CasinoRoomsStore rooms, Action leaveRoom)
    {
        this.chips = chips;
        this.rooms = rooms;
        this.leaveRoom = leaveRoom;
    }

    public Backdrop IdleBackdrop => Backdrop.Arena;

    public static float DeckHeight => BetComposer.DeckHeightFor(true, true);

    public bool ManualDaub { get; private set; }

    public LocString DaubLabel => ManualDaub ? L.Bingo.DaubManual : L.Bingo.DaubAuto;

    public void ToggleDaub()
    {
        ManualDaub = !ManualDaub;
        CasinoSfx.Play(ManualDaub ? UiSound.ToggleOn : UiSound.ToggleOff);
    }

    public void Enter()
    {
        entered = true;
        inlineReason = string.Empty;
        requestedCards = 1;
        rooms.Enter(CasinoRoomIds.BingoHall);
    }

    public void Reset()
    {
        if (entered)
        {
            rooms.Leave();
        }

        entered = false;
        playback.Reset();
        tumbler.Reset();
        inlineReason = string.Empty;
        celebratedRoundId = string.Empty;
        requestedCards = 1;
        heartbeatDub = 0f;
        railDragging = false;
        railOffset.SnapTo(0f);
    }

    public void Gate()
    {
        composer.Gate();
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        composer.DrawOverlay(screen, ui, false);
    }

    public void Draw(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var delta = frame.DeltaSeconds;
        ConsumeStakeResults();
        var room = rooms.Room;
        var held = room.State;
        var snapshot = held?.Snapshot;
        var board = held?.Bingo;
        var mine = snapshot is null ? null : rooms.BingoCardsFor(snapshot.RoomId, snapshot.RoundIndex);
        playback.Update(snapshot, board, mine, delta, ManualDaub);
        if (frame.SnapToTruth)
        {
            playback.Snap();
        }

        tumbler.Update(delta);
        lightPhase = frame.Phase;
        var drawList = ImGui.GetWindowDrawList();
        var safe = frame.Safe;
        var closedReason = room.ClosedReason;
        if (closedReason.Length > 0)
        {
            DrawClosed(drawList, ui, closedReason, safe, scale);
            return;
        }

        var state = chips.State;
        if (state is null || snapshot is null)
        {
            LoadingPulse.Draw(safe.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        var remaining = room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var holding = HeldCards(mine);
        layout = BingoHallLayout.Compute(safe, holding, scale);
        PlayCues(playback.TakeCues(), frame.Instant);
        TickHeartbeat(delta);
        SettleRound(stage, snapshot, board, mine, frame);
        PhaseRibbon.Draw(drawList, frame.Layout.Ribbon, RibbonLabel(snapshot.Phase, remaining), remaining,
            CasinoRoomCadence.BingoWindow(snapshot.Phase), snapshot.Occupancy,
            room.Attached ? CasinoColors.LightA : CasinoColors.InkMuted, scale);
        var calledOff = CalledOff(board, mine);
        BingoCardArt.DrawTumbler(drawList, layout.Tumbler, tumbler,
            playback.Stage == BingoStage.Calling ? 1f : 0.4f, scale);
        DrawCaller(drawList, layout.Caller, snapshot.Phase, remaining, scale);
        BingoCardArt.DrawBoard(drawList, layout.Board, layout.Cell, playback, scale);
        DrawCards(drawList, ui, mine, holding, calledOff, frame.DeltaSeconds, scale);
        DrawPodiums(drawList, board, scale);
        if (playback.Stage == BingoStage.Wrapped || calledOff)
        {
            DrawResult(drawList, board, mine, holding, calledOff, scale);
        }

        DrawFlight(drawList, scale);
        if (inlineReason.Length > 0)
        {
            var message = CasinoReasons.Text(inlineReason, chips.Ceiling.MaxBet);
            var height = CasinoNotice.Height(CasinoNoticeKind.Reason, string.Empty, message, safe.Width, scale);
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty, message, safe.Min.X,
                layout.Podiums.Min.Y - height - PodiumGap * scale, safe.Width, scale);
        }

        DrawDeck(stage, frame, ui, state, state.Sitting ?? CasinoWire.NoBankroll, holding, calledOff, scale);
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        var scale = UiScale.Current;
        tumbler.Update(deltaSeconds);
        idlePop += deltaSeconds;
        if (idlePop >= IdlePopSeconds)
        {
            idlePop = 0f;
            tumbler.Pop(IdlePopSeconds * 0.5f);
        }

        var side = MathF.Min(rect.Width, rect.Height) * 0.84f;
        var drum = new Rect(rect.Center - new Vector2(side * 0.5f, side * 0.5f),
            rect.Center + new Vector2(side * 0.5f, side * 0.5f));
        var rim = drum.Inset(-CasinoLights.BulbPitch * 0.5f * scale);
        CasinoLights.BulbChase(drawList, rim, rim.Width * 0.5f, scale, tumbler.CageAngle * 2f, CasinoLights.BulbPitch,
            CasinoColors.Money, CasinoColors.LightB, 0.8f);
        BingoCardArt.DrawTumbler(drawList, drum, tumbler, 0.8f, scale);
    }

    internal static int HeldCards(CasinoBingoCardsDto? mine)
    {
        var held = mine?.Cards?.Length ?? 0;
        return held > BingoRules.MaxCards ? BingoRules.MaxCards : held;
    }

    internal static bool Settled(CasinoBingoCardsDto? mine)
    {
        return mine is not null && mine.RoundState == CasinoRoundStates.Settled;
    }

    internal static bool CalledOff(CasinoBingoRoomStateDto? board, CasinoBingoCardsDto? mine)
    {
        return (board is not null && board.Cancelled)
            || (mine is not null && mine.RoundState == CasinoRoundStates.Voided);
    }

    internal static long PrizeAt(CasinoBingoRoomStateDto? board, int stage)
    {
        if (!BingoRules.IsStage(stage))
        {
            return 0;
        }

        var published = board?.Prizes;
        if (published is not null && published.Length >= BingoRules.StageCount)
        {
            return published[stage];
        }

        return BingoRules.PrizeFor(stage, CardsInPlay(board));
    }

    internal static void LadderFor(CasinoBingoRoomStateDto? board, Span<long> ladder)
    {
        for (var stage = 0; stage < BingoRules.StageCount && stage < ladder.Length; stage++)
        {
            ladder[stage] = PrizeAt(board, stage);
        }
    }

    internal static void DisplayLadder(CasinoBingoRoomStateDto? board, Span<long> ladder)
    {
        LadderFor(board, ladder);
        if (CardsInPlay(board) > 0)
        {
            return;
        }

        for (var stage = 0; stage < BingoRules.StageCount && stage < ladder.Length; stage++)
        {
            if (ladder[stage] <= 0)
            {
                ladder[stage] = BingoRules.PrizeFor(stage, 1);
            }
        }
    }

    internal static int PrizeCardCapOf(CasinoBingoRoomStateDto? board)
    {
        var cap = board?.PrizeCardCap ?? 0;
        return cap > 0 ? cap : BingoRules.PrizeCardCap;
    }

    internal static int CardsInPlay(CasinoBingoRoomStateDto? board)
    {
        return board?.Cards ?? 0;
    }

    internal static int BallIntervalOf(CasinoBingoRoomStateDto? board)
    {
        var interval = board?.BallIntervalMs ?? 0;
        return interval > 0 ? interval : BingoRules.BallIntervalMs;
    }

    internal static int NextRoomSeconds(int phase, long remainingMs)
    {
        var estimate = phase switch
        {
            CasinoRoomPhases.Locked => remainingMs + CasinoRoomCadence.BingoResultSeconds * 1000L,
            CasinoRoomPhases.Result => remainingMs,
            _ => 0L,
        };

        return (int)((estimate + 999) / 1000);
    }

    internal static CasinoBingoStageDto? StageAwarded(CasinoBingoRoomStateDto? board, int stage)
    {
        var stages = board?.Stages;
        if (stages is null)
        {
            return null;
        }

        for (var index = 0; index < stages.Length; index++)
        {
            if (stages[index].Stage == stage)
            {
                return stages[index];
            }
        }

        return null;
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

    private void PlayCues(BingoCue cues, bool instant)
    {
        if ((cues & BingoCue.BallPopped) != 0)
        {
            tumbler.Pop(BingoRoundPlayback.FlightSeconds);
            CasinoSfx.Play(UiSound.GamePop);
        }

        if ((cues & BingoCue.BallLanded) != 0)
        {
            CasinoSfx.Play(UiSound.PegTick);
        }

        if ((cues & BingoCue.Daubed) != 0)
        {
            CasinoSfx.Play(UiSound.Daub);
        }

        if ((cues & BingoCue.StageWon) != 0)
        {
            CasinoSfx.Play(UiSound.TurnChime);
        }

        if ((cues & BingoCue.OneAway) != 0 && !instant && CasinoSfx.Audible)
        {
            UiFeedback.PlayPitched(UiSound.GameHitSoft, HeartbeatLubRate);
            heartbeatDub = HeartbeatGapSeconds;
        }
    }

    private void TickHeartbeat(float deltaSeconds)
    {
        if (heartbeatDub <= 0f)
        {
            return;
        }

        heartbeatDub -= deltaSeconds;
        if (heartbeatDub > 0f || !CasinoSfx.Audible)
        {
            return;
        }

        UiFeedback.PlayPitched(UiSound.Daub, HeartbeatDubRate);
    }

    private void SettleRound(CasinoStage stage, CasinoRoomSnapshotDto snapshot, CasinoBingoRoomStateDto? board,
        CasinoBingoCardsDto? mine, in CasinoStageFrame frame)
    {
        var roundKey = BingoRoundPlayback.RoundKeyOf(snapshot);
        if (playback.Stage != BingoStage.Wrapped || !Settled(mine)
            || string.Equals(celebratedRoundId, roundKey, StringComparison.Ordinal))
        {
            return;
        }

        celebratedRoundId = roundKey;
        var stake = mine!.Stake;
        var payout = mine.Payout;
        if (stake > 0)
        {
            stage.Settle(new CasinoBetRecord(L.Casino.GameBingo, stake, payout, mine.RoundId,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), mine.Capped));
        }

        if (!playback.CalledLive)
        {
            return;
        }

        var origin = layout.Hero.Center;
        var tier = WinLadder.TierFor(stake, payout);
        if (tier == WinTier.None)
        {
            return;
        }

        if (board is not null && board.EarlyBird && playback.WonStage(BingoRules.StageFullHouse)
            && tier < WinTier.Epic)
        {
            stage.Celebration.Start(WinTier.Epic, payout, origin, frame.Instant, false);
            return;
        }

        stage.Celebration.Celebrate(stake, payout, origin, frame.Instant);
    }

    private string RibbonLabel(int phase, long remainingMs)
    {
        var seconds = (int)((Math.Max(0, remainingMs) + 999) / 1000);
        return phase switch
        {
            CasinoRoomPhases.Open => ribbonLabel.Duration(L.Casino.BingoCardsClose, seconds),
            CasinoRoomPhases.Locked => playback.BallCount > 0
                ? ribbonLabel.Numbers(L.Casino.BingoCalledCount, playback.BallCount, BingoRules.Balls)
                : Loc.T(L.Casino.BingoProgressWaiting),
            _ => ribbonLabel.Duration(L.Casino.BingoNextRoom, NextRoomSeconds(phase, remainingMs)),
        };
    }

    private void DrawCaller(ImDrawListPtr drawList, Rect rect, int phase, long remainingMs, float scale)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var seconds = (int)((Math.Max(0, remainingMs) + 999) / 1000);
        var center = rect.Center;
        if (playback.Stage == BingoStage.Selling)
        {
            StageText.State(drawList, center - new Vector2(0f, Typography.LineHeight(TextStyles.Title2) * 0.5f),
                callerLabel.Duration(L.Casino.BingoFirstBall, seconds), rect.Width, CallerMarquee);
            var price = Typography.FitText(PriceText(), rect.Width, TextStyles.FootnoteEmphasized);
            Typography.DrawCentered(drawList,
                center + new Vector2(0f, Typography.LineHeight(TextStyles.FootnoteEmphasized)), price,
                CasinoColors.Money, TextStyles.FootnoteEmphasized);
            return;
        }

        var radius = MathF.Min(rect.Height * CallerBallShare, rect.Width * 0.3f);
        var ballCenter = new Vector2(center.X, rect.Min.Y + radius + Metrics.Space.Xs * scale);
        var shown = playback.Flying ? PreviousBall() : playback.LatestBall;
        if (shown > 0)
        {
            var settle = playback.Flying ? 1f : MathF.Min(1f, (playback.SinceBall - BingoRoundPlayback.FlightSeconds) / 0.3f);
            var pop = 0.85f + 0.15f * Easing.EaseOutBack(MathF.Max(0.01f, settle));
            drawList.AddCircleFilled(ballCenter, radius * 1.35f,
                ImGui.GetColorU32(CasinoColors.Money with { W = 0.10f + 0.12f * Pulse.Wave(Pulse.Breath) }), 32);
            BingoCardArt.DrawBall(drawList, ballCenter, radius * pop, shown, 1f);
        }
        else
        {
            drawList.AddCircle(ballCenter, radius, ImGui.GetColorU32(CasinoColors.InkMuted with { W = 0.5f }), 32,
                1.5f * scale);
        }

        var caption = playback.BallCount > 0
            ? captionLabel.Numbers(L.Casino.BingoCalledCount, playback.BallCount, BingoRules.Balls)
            : Loc.T(L.Casino.BingoProgressWaiting);
        caption = Typography.FitText(caption, rect.Width, TextStyles.FootnoteEmphasized);
        var captionTop = ballCenter.Y + radius * 1.35f + Metrics.Space.Xxs * scale;
        Typography.DrawCentered(drawList,
            new Vector2(center.X, captionTop + Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f), caption,
            CasinoColors.InkTitle, TextStyles.FootnoteEmphasized);
    }

    private string PriceText()
    {
        if (ReferenceEquals(priceLanguage, Loc.Current) && priceText.Length > 0)
        {
            return priceText;
        }

        priceLanguage = Loc.Current;
        priceText = Loc.T(L.Casino.BingoCardPrice, NumberText.Compact(BingoRules.CardPrice),
            GameNumber.Label(BingoRules.MaxCards));
        return priceText;
    }

    private int PreviousBall()
    {
        var count = playback.BallCount;
        if (count < 2)
        {
            return 0;
        }

        var balls = BingoRoundPlayback.CalledBalls(rooms.Room.State?.Bingo);
        return count - 2 < balls.Length ? balls[count - 2] : 0;
    }

    private void DrawFlight(ImDrawListPtr drawList, float scale)
    {
        if (!playback.Flying)
        {
            return;
        }

        var progress = playback.FlightProgress;
        var exit = BingoCardArt.TumblerPoint(layout.Tumbler, BingoTumbler.Exit);
        var drumRadius = layout.Tumbler.Width * 0.5f;
        var smallRadius = BingoTumbler.BallRadius * drumRadius / BingoTumbler.DrumRadius;
        var bigRadius = MathF.Max(smallRadius * 2.2f, layout.Cell * 0.9f);
        var lifted = exit - new Vector2(0f, FlightLift * scale);
        Vector2 at;
        float radius;
        if (progress < PopOutShare)
        {
            var rise = Easing.EaseOutCubic(progress / PopOutShare);
            at = Vector2.Lerp(exit, lifted, rise);
            radius = smallRadius + (bigRadius - smallRadius) * rise;
        }
        else
        {
            var travel = Easing.EaseInOutCubic((progress - PopOutShare) / (1f - PopOutShare));
            var target = BingoHallLayout.BoardCellCenter(layout.Board, layout.Cell, playback.LatestBall);
            var control = new Vector2((lifted.X + target.X) * 0.5f, MathF.Min(lifted.Y, target.Y) - FlightLift * scale);
            var inverse = 1f - travel;
            at = inverse * inverse * lifted + 2f * inverse * travel * control + travel * travel * target;
            radius = bigRadius + (layout.Cell * 0.42f - bigRadius) * travel;
        }

        drawList.AddCircleFilled(at, radius * 1.5f, ImGui.GetColorU32(CasinoColors.Money with { W = 0.18f }), 24);
        BingoCardArt.DrawBall(drawList, at, radius, playback.LatestBall, 1f);
    }

    private void DrawCards(ImDrawListPtr drawList, AppSkin ui, CasinoBingoCardsDto? mine, int holding, bool calledOff,
        float deltaSeconds, float scale)
    {
        if (holding == 0)
        {
            if (calledOff || playback.Stage == BingoStage.Wrapped)
            {
                return;
            }

            var hint = Loc.T(playback.Stage == BingoStage.Selling ? L.Casino.BingoNoCardsHint : L.Casino.BingoRoomRolling);
            var width = MathF.Min(layout.Cards.Width, layout.Hero.Width + OverlayPad * 2f * scale);
            var height = CasinoNotice.Height(CasinoNoticeKind.Info, string.Empty, hint, width, scale);
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Info, string.Empty, hint, layout.Cards.Center.X - width * 0.5f,
                layout.Cards.Center.Y - height * 0.5f, width, scale);
            return;
        }

        var hero = playback.HeroIndex;
        DrawHero(drawList, mine, hero, scale);
        if (layout.HasRail)
        {
            DrawRail(drawList, mine, holding, hero, deltaSeconds, scale);
        }
    }

    private void DrawHero(ImDrawListPtr drawList, CasinoBingoCardsDto? mine, int hero, float scale)
    {
        var card = layout.Hero;
        if (card.Width <= 0f)
        {
            return;
        }

        var oneAway = playback.Stage == BingoStage.Calling && playback.OneAway(hero);
        var glow = oneAway ? 0.35f + 0.65f * Pulse.Heartbeat(HeartbeatPeriod) : 0f;
        BingoCardArt.DrawPlate(drawList, card, true, glow, scale);
        var visible = playback.VisibleMaskOf(hero);
        var stamped = playback.StampedMaskOf(hero);
        BingoCardArt.DrawCard(drawList, card, BingoRoundPlayback.CardAt(mine, hero), visible, stamped, playback, hero,
            scale);
        var labelTop = layout.Cards.Min.Y;
        var label = cardLabels[hero].Get(L.Casino.BingoCardLabel, hero + 1);
        var labelWidth = Typography.Measure(label, TextStyles.FootnoteEmphasized).X;
        Typography.Draw(drawList, new Vector2(card.Min.X, labelTop), label, CasinoColors.InkTitle,
            TextStyles.FootnoteEmphasized);
        var spare = card.Width - labelWidth - Metrics.Space.Sm * scale;
        if (oneAway)
        {
            var chip = Typography.FitText(Loc.T(L.Casino.BingoOneAway), spare, TextStyles.FootnoteEmphasized);
            var chipWidth = Typography.Measure(chip, TextStyles.FootnoteEmphasized).X;
            Typography.Draw(drawList, new Vector2(card.Max.X - chipWidth, labelTop), chip,
                CasinoColors.Money with { W = 0.6f + 0.4f * glow }, TextStyles.FootnoteEmphasized);
        }
        else if (ManualDaub && playback.Stage == BingoStage.Calling)
        {
            var hint = Typography.FitText(Loc.T(L.Bingo.DaubHint), spare, TextStyles.Footnote);
            var hintWidth = Typography.Measure(hint, TextStyles.Footnote).X;
            Typography.Draw(drawList, new Vector2(card.Max.X - hintWidth, labelTop), hint, CasinoColors.InkBody,
                TextStyles.Footnote);
        }

        if (ManualDaub && playback.Stage == BingoStage.Calling)
        {
            HandleDaubTaps(card, hero, visible & ~stamped);
        }
    }

    private void HandleDaubTaps(Rect card, int cardIndex, int pending)
    {
        if (pending == 0 || !UiInteract.Hover(card.Min, card.Max))
        {
            return;
        }

        for (var cellIndex = 0; cellIndex < BingoRules.Cells; cellIndex++)
        {
            if ((pending & (1 << cellIndex)) == 0)
            {
                continue;
            }

            var cell = BingoCardArt.CellRect(card, cellIndex);
            var hovered = UiInteract.Hover(cell.Min, cell.Max);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(cell.Min, cell.Max, hovered, false))
            {
                playback.Stamp(cardIndex, cellIndex);
                return;
            }
        }
    }

    private void DrawRail(ImDrawListPtr drawList, CasinoBingoCardsDto? mine, int holding, int hero,
        float deltaSeconds, float scale)
    {
        var rail = layout.Rail;
        var side = rail.Width;
        var step = side + (RailLabelHeight + RailGap) * scale;
        var content = (holding - 1) * step - RailGap * scale;
        var lowest = MathF.Min(0f, rail.Height - content);
        PressSurface.Claim("##bingoRail", rail, out _);
        var hovered = UiInteract.Hover(rail.Min, rail.Max);
        var mouse = ImGui.GetMousePos();
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            railDragging = true;
            railDragMoved = false;
            railDragFrom = mouse.Y;
            railDragStart = railOffset.Value;
        }

        if (railDragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var travel = mouse.Y - railDragFrom;
                if (MathF.Abs(travel) > DragThreshold * scale)
                {
                    railDragMoved = true;
                    UiInteract.CancelPendingTap();
                }

                railOffset.SnapTo(railDragStart + travel);
            }
            else
            {
                railDragging = false;
            }
        }

        if (hovered)
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                railOffset.SnapTo(railOffset.Value + wheel * WheelStep * scale);
            }
        }

        if (!railDragging)
        {
            railOffset.Step(Math.Clamp(railOffset.Value, lowest, 0f), RailSettleSeconds, deltaSeconds);
        }

        drawList.PushClipRect(rail.Min, rail.Max, true);
        var y = rail.Min.Y + railOffset.Value;
        for (var cardIndex = 0; cardIndex < holding; cardIndex++)
        {
            if (cardIndex == hero)
            {
                continue;
            }

            DrawMini(drawList, new Rect(new Vector2(rail.Min.X, y), new Vector2(rail.Max.X, y + step)), cardIndex,
                side, scale);
            y += step;
        }

        drawList.PopClipRect();
    }

    private void DrawMini(ImDrawListPtr drawList, Rect slot, int cardIndex, float side, float scale)
    {
        var label = Typography.FitText(cardLabels[cardIndex].Get(L.Casino.BingoCardLabel, cardIndex + 1), side,
            TextStyles.Footnote);
        Typography.Draw(drawList, slot.Min, label, CasinoColors.InkBody, TextStyles.Footnote);
        var top = slot.Min.Y + RailLabelHeight * scale;
        var inset = 4f * scale;
        var card = new Rect(new Vector2(slot.Min.X + inset, top + inset), new Vector2(slot.Max.X - inset, top + side - inset));
        var oneAway = playback.Stage == BingoStage.Calling && playback.OneAway(cardIndex);
        BingoCardArt.DrawPlate(drawList, card, false,
            oneAway ? 0.35f + 0.65f * Pulse.Heartbeat(HeartbeatPeriod) : 0f, scale);
        BingoCardArt.DrawMini(drawList, card, playback.VisibleMaskOf(cardIndex), playback.StampedMaskOf(cardIndex),
            scale);
        var hit = new Rect(slot.Min, new Vector2(slot.Max.X, top + side));
        var hovered = UiInteract.Hover(hit.Min, hit.Max) && UiInteract.Hover(layout.Rail.Min, layout.Rail.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(hit.Min, hit.Max, hovered, false) || railDragMoved)
        {
            return;
        }

        playback.Promote(cardIndex);
        CasinoSfx.Play(UiSound.CardSnap);
    }

    private void DrawPodiums(ImDrawListPtr drawList, CasinoBingoRoomStateDto? board, float scale)
    {
        var row = layout.Podiums;
        var gap = PodiumGap * scale;
        var width = (row.Width - gap * (BingoRules.StageCount - 1)) / BingoRules.StageCount;
        DisplayLadder(board, ladder);
        playback.BestCard(out var gapToGoal, out var goal);
        for (var stageIndex = 0; stageIndex < BingoRules.StageCount; stageIndex++)
        {
            var min = new Vector2(row.Min.X + stageIndex * (width + gap), row.Min.Y);
            var rect = new Rect(min, new Vector2(min.X + width, row.Max.Y));
            var chasing = playback.CardCount > 0 && playback.Stage == BingoStage.Calling && goal == stageIndex
                && gapToGoal > 0;
            DrawPodium(drawList, board, stageIndex, rect, chasing, scale);
        }
    }

    private void DrawPodium(ImDrawListPtr drawList, CasinoBingoRoomStateDto? board, int stageIndex, Rect rect,
        bool chasing, float scale)
    {
        var awarded = StageAwarded(board, stageIndex);
        var mine = playback.WonStage(stageIndex);
        var rounding = Metrics.Radius.Md * scale;
        var lit = mine ? 1f : awarded is not null ? 0.55f : chasing ? 0.3f + 0.2f * Pulse.Wave(Pulse.Breath) : 0.15f;
        var tint = mine ? CasinoColors.Money : CasinoColors.LightB;
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(Vector4.Lerp(CasinoColors.FeltBottom, tint, 0.25f * lit) with { W = 0.92f }),
            ImGui.GetColorU32(new Vector4(0.03f, 0.02f, 0.06f, 0.92f)));
        Squircle.Fill(drawList, rect.Min, new Vector2(rect.Max.X, rect.Min.Y + 3f * scale), rounding * 0.4f,
            ImGui.GetColorU32(tint with { W = 0.4f + 0.6f * lit }));
        if (mine)
        {
            CasinoLights.BulbChase(drawList, rect, rounding, scale, lightPhase, CasinoLights.BulbPitch, CasinoColors.Money, CasinoColors.LightA, 1f);
        }

        var pad = PodiumPad * scale;
        var inner = rect.Width - pad * 2f;
        var y = rect.Min.Y + pad;
        var name = Typography.FitText(Loc.T(StageNames[stageIndex]), inner, TextStyles.FootnoteEmphasized);
        var nameWidth = Typography.Measure(name, TextStyles.FootnoteEmphasized).X;
        Typography.Draw(drawList, new Vector2(rect.Center.X - nameWidth * 0.5f, y), name,
            awarded is null ? CasinoColors.InkBody : CasinoColors.InkTitle, TextStyles.FootnoteEmphasized);
        y += Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var prize = NumberText.Compact(awarded?.Prize ?? ladder[stageIndex]);
        var prizeSize = CurrencyGlyph.MeasureAmount(prize, TextStyles.Title3);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(rect.Center.X - prizeSize.X * 0.5f, y), prize,
            CurrencyKind.Chips, CasinoColors.Money, TextStyles.Title3);
        y += prizeSize.Y;
        if (awarded is null)
        {
            return;
        }

        var status = mine
            ? Loc.T(L.Bingo.You)
            : stageBalls[stageIndex].Get(L.Casino.BingoLadderGone, awarded.Ball);
        status = Typography.FitText(status, inner, TextStyles.Footnote);
        var statusWidth = Typography.Measure(status, TextStyles.Footnote).X;
        Typography.Draw(drawList, new Vector2(rect.Center.X - statusWidth * 0.5f, y), status,
            mine ? CasinoColors.Money : CasinoColors.InkBody, TextStyles.Footnote);
    }

    private void DrawResult(ImDrawListPtr drawList, CasinoBingoRoomStateDto? board, CasinoBingoCardsDto? mine,
        int holding, bool calledOff, float scale)
    {
        var area = layout.Cards;
        if (area.Height <= 0f)
        {
            return;
        }

        var rounding = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, area.Min, area.Max, rounding,
            ImGui.GetColorU32(new Vector4(0.02f, 0.015f, 0.05f, VeilAlpha)));
        var pad = OverlayPad * scale;
        var inner = area.Width - pad * 2f;
        var left = area.Min.X + pad;
        var y = area.Min.Y + pad;
        drawList.PushClipRect(area.Min, area.Max, true);
        var title = Typography.FitText(Loc.T(L.Casino.BingoRoomWrapped), inner, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left, y), title, CasinoColors.InkTitle, TextStyles.SubheadlineEmphasized);
        y += Typography.LineHeight(TextStyles.SubheadlineEmphasized) + Metrics.Space.Xxs * scale;
        var won = !calledOff && Settled(mine) && mine!.Payout > 0;
        var outcome = OutcomeText(mine, holding, calledOff, won);
        if (won)
        {
            var fitted = Typography.FitText(outcome, inner, TextStyles.Title3);
            Typography.Draw(drawList, new Vector2(left, y), fitted, CasinoColors.Money, TextStyles.Title3);
            y += Typography.LineHeight(TextStyles.Title3);
        }
        else
        {
            y += Typography.DrawWrappedLeft(new Vector2(left, y), outcome, CasinoColors.InkBody, TextStyles.Subheadline,
                inner);
        }

        if (calledOff)
        {
            drawList.PopClipRect();
            return;
        }

        y += Metrics.Space.Sm * scale;
        var fullHouse = StageAwarded(board, BingoRules.StageFullHouse);
        if (board is not null && board.EarlyBird && fullHouse is not null)
        {
            y = DrawEarlyBird(drawList, fullHouse.Ball, left, y, inner, scale);
        }

        for (var stageIndex = 0; stageIndex < BingoRules.StageCount; stageIndex++)
        {
            var awarded = StageAwarded(board, stageIndex);
            if (awarded is null)
            {
                continue;
            }

            DrawWinnersRow(drawList, awarded, stageIndex, left, y, inner, scale);
            y += RowHeight * scale;
        }

        drawList.PopClipRect();
    }

    private string OutcomeText(CasinoBingoCardsDto? mine, int holding, bool calledOff, bool won)
    {
        if (calledOff)
        {
            return Loc.T(L.Casino.BingoCalledOff);
        }

        if (holding == 0)
        {
            return Loc.T(L.Casino.BingoWatchedRoom);
        }

        if (!Settled(mine))
        {
            return Loc.T(L.Casino.BingoCardsPending);
        }

        if (!won)
        {
            return Loc.T(L.Casino.BingoNoWin);
        }

        var payout = mine!.Payout;
        return outcomeLabel.Get(L.Casino.BingoYouWon, (int)Math.Min(payout, int.MaxValue));
    }

    private float DrawEarlyBird(ImDrawListPtr drawList, int ball, float left, float y, float width, float scale)
    {
        var banner = Typography.FitText(Loc.T(L.Bingo.EarlyBird), width * 0.5f, TextStyles.Headline);
        var bannerSize = Typography.Measure(banner, TextStyles.Headline);
        var height = bannerSize.Y + Metrics.Space.Xs * 2f * scale;
        var pillMax = new Vector2(left + bannerSize.X + Metrics.Space.Md * 2f * scale, y + height);
        var flicker = 0.75f + 0.25f * Pulse.Wave(Pulse.Fast);
        Squircle.Fill(drawList, new Vector2(left, y), pillMax, height * 0.5f,
            ImGui.GetColorU32(CasinoColors.LightA with { W = 0.18f * flicker }));
        Squircle.Stroke(drawList, new Vector2(left, y), pillMax, height * 0.5f,
            ImGui.GetColorU32(CasinoColors.LightA with { W = flicker }), 1.6f * scale);
        Typography.Draw(drawList, new Vector2(left + Metrics.Space.Md * scale, y + Metrics.Space.Xs * scale), banner,
            CasinoColors.InkTitle, TextStyles.Headline);
        var hintLeft = pillMax.X + Metrics.Space.Sm * scale;
        var hint = Typography.FitText(earlyBirdLabel.Get(L.Bingo.EarlyBirdHint, ball), left + width - hintLeft,
            TextStyles.Footnote);
        Typography.Draw(drawList,
            new Vector2(hintLeft, y + (height - Typography.LineHeight(TextStyles.Footnote)) * 0.5f), hint,
            CasinoColors.InkTitle, TextStyles.Footnote);
        return y + height + Metrics.Space.Sm * scale;
    }

    private void DrawWinnersRow(ImDrawListPtr drawList, CasinoBingoStageDto awarded, int stageIndex, float left,
        float y, float width, float scale)
    {
        var centerY = y + RowHeight * scale * 0.5f;
        var nameWidth = width * 0.32f;
        var name = Typography.FitText(Loc.T(StageNames[stageIndex]), nameWidth, TextStyles.Footnote);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left, centerY - lineHeight * 0.5f), name, CasinoColors.InkTitle,
            TextStyles.Footnote);
        var winners = Math.Max(1, awarded.Winners);
        var shown = Math.Min(MaxAvatars, winners);
        var avatarLeft = left + nameWidth + Metrics.Space.Xs * scale;
        var mine = playback.WonStage(stageIndex);
        for (var avatar = 0; avatar < shown; avatar++)
        {
            var center = new Vector2(avatarLeft + AvatarRadius * scale + avatar * AvatarStep * scale, centerY);
            var tint = mine && avatar == 0 ? CasinoColors.Money : AvatarTints[avatar % AvatarTints.Length];
            drawList.AddCircleFilled(center, AvatarRadius * scale, ImGui.GetColorU32(tint with { W = 0.9f }), 16);
            drawList.AddCircle(center, AvatarRadius * scale, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)), 16,
                MathF.Max(1f, scale));
        }

        var labelLeft = avatarLeft + AvatarRadius * 2f * scale + (shown - 1) * AvatarStep * scale
            + Metrics.Space.Sm * scale;
        var label = winners > 1
            ? stageWinners[stageIndex].Get(L.Bingo.Winners, winners)
            : Loc.T(L.Bingo.OneWinner);
        if (mine)
        {
            label = Loc.T(L.Bingo.You);
        }

        label = Typography.FitText(label, MathF.Max(0f, left + width - labelLeft), TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
            label, mine ? CasinoColors.Money : CasinoColors.InkTitle, TextStyles.Footnote);
    }

    private void DrawDeck(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, CasinoStateDto state,
        CasinoSittingDto sitting, int holding, bool calledOff, float scale)
    {
        var headroom = BingoRules.MaxCards - holding;
        var selling = playback.Stage == BingoStage.Selling && !calledOff;
        requestedCards = Math.Clamp(requestedCards, 1, Math.Max(1, headroom));
        var stake = BingoRules.StakeFor(requestedCards);
        composer.Reset(stake);
        var enabled = selling && headroom > 0 && !state.StakesPaused && !state.Draining && !rooms.StakeInFlight
            && !frame.Blocked;
        var model = new BetComposerModel(stake, stake, sitting.Stack, L.Bingo.BuyAction, enabled, FixedAmount: true,
            Knob: true, Repeat: enabled && stage.RepeatPressed(), Busy: rooms.StakeInFlight);
        var action = composer.Draw(stage, ui, frame.Deck, model, frame.DeltaSeconds);
        DrawKnob(ImGui.GetWindowDrawList(), composer.KnobRect, selling, headroom, holding, calledOff, enabled, scale);
        if (action != BetComposerAction.Confirm)
        {
            return;
        }

        inlineReason = string.Empty;
        rooms.BuyBingoCards(requestedCards);
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    private void DrawKnob(ImDrawListPtr drawList, Rect knob, bool selling, int headroom, int holding, bool calledOff,
        bool enabled, float scale)
    {
        if (knob.Width <= 0f || knob.Height <= 0f)
        {
            return;
        }

        if (!selling || headroom <= 0)
        {
            var message = calledOff
                ? Loc.T(L.Casino.BingoCalledOff)
                : headroom <= 0 && selling
                    ? holdingLabel.Get(L.Casino.BingoHoldingFull, holding)
                    : Loc.T(playback.Stage == BingoStage.Calling ? L.Casino.BingoCardsFinal : L.Casino.BingoNextRoomSale);
            var fitted = Typography.FitText(message, knob.Width, TextStyles.FootnoteEmphasized);
            Typography.DrawCentered(drawList, knob.Center, fitted, CasinoColors.InkTitle, TextStyles.FootnoteEmphasized);
            return;
        }

        var label = Typography.FitText(Loc.T(L.Bingo.KnobCards), knob.Width * 0.25f, TextStyles.Footnote);
        var labelWidth = Typography.Measure(label, TextStyles.Footnote).X;
        Typography.Draw(drawList,
            new Vector2(knob.Min.X, knob.Center.Y - Typography.LineHeight(TextStyles.Footnote) * 0.5f), label,
            CasinoColors.InkBody, TextStyles.Footnote);
        var gap = KnobPillGap * scale;
        var left = knob.Min.X + labelWidth + Metrics.Space.Sm * scale;
        var pill = (knob.Max.X - left - gap * (BingoRules.MaxCards - 1)) / BingoRules.MaxCards;
        for (var count = 1; count <= BingoRules.MaxCards; count++)
        {
            var min = new Vector2(left + (count - 1) * (pill + gap), knob.Min.Y);
            var max = new Vector2(min.X + pill, knob.Max.Y);
            var available = enabled && count <= headroom;
            var selected = count == requestedCards;
            var hovered = available && UiInteract.Hover(min, max);
            var rounding = (max.Y - min.Y) * 0.5f;
            var fill = selected ? CasinoColors.LightA with { W = 0.85f } : new Vector4(1f, 1f, 1f, hovered ? 0.14f : 0.07f);
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(fill));
            if (selected)
            {
                Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(CasinoColors.MoneyHighlight),
                    MathF.Max(1f, scale));
            }

            var ink = selected ? CasinoColors.InkTitle : available ? CasinoColors.InkBody : CasinoColors.InkMuted;
            Typography.DrawCentered(drawList, (min + max) * 0.5f, GameNumber.Label(count),
                ink with { W = available || selected ? 1f : 0.45f }, TextStyles.SubheadlineEmphasized);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (available && UiInteract.Click(min, max, hovered, false))
            {
                requestedCards = count;
                inlineReason = string.Empty;
                CasinoSfx.Play(UiSound.ChipSlide);
            }
        }
    }

    private void DrawClosed(ImDrawListPtr drawList, AppSkin ui, string reason, Rect safe, float scale)
    {
        var title = Loc.T(L.Casino.BingoClosedTitle);
        var hint = Loc.T(CasinoReasons.TryMessage(reason, out var known) ? known : L.Casino.BingoClosedHint);
        CasinoNotice.DrawWithAction(drawList, ui, CasinoNoticeKind.Card, title, hint, Loc.T(L.Casino.WheelBackToFloor),
            safe.Min.X, safe.Min.Y + Metrics.Space.Lg * scale, safe.Width, scale, out var pressed);
        if (pressed)
        {
            leaveRoom();
        }
    }
}
