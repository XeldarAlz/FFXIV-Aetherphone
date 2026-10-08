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

internal sealed class BingoCabinet
{
    private const float StatusRowHeight = 22f;
    private const float CallerHeight = 162f;
    private const float BallRailHeight = 48f;
    private const float ProgressHeight = 62f;
    private const float LadderRowHeight = 34f;
    private const float SegmentHeight = 32f;
    private const float PillHeight = Button.LargeHeight;
    private const float CardGap = 10f;
    private const float CardPlatePadding = 5f;
    private const float CardLabelHeight = 22f;
    private const int RailCalls = 8;

    private static readonly Vector4 Gold = new(1f, 0.84f, 0.42f, 1f);
    private static readonly Vector4 CountThumbInk = new(1f, 1f, 1f, 1f);

    private static readonly Vector4[] ConfettiPalette =
    {
        new(1.00f, 0.84f, 0.42f, 1f),
        new(1.00f, 0.95f, 0.75f, 1f),
        new(0.55f, 0.92f, 0.88f, 1f),
        new(0.80f, 0.58f, 0.98f, 1f),
    };

    private static readonly LocString[] StageNames =
    {
        L.Casino.BingoStageLine,
        L.Casino.BingoStageTwoLines,
        L.Casino.BingoStageFullHouse,
    };

    private readonly CasinoStore chips;
    private readonly CasinoRoomsStore rooms;
    private readonly Action openCashier;
    private readonly Action leaveRoom;
    private readonly BingoRoundPlayback playback = new();
    private readonly ParticleSystem particles = new(160);
    private readonly long[] ladder = new long[BingoRules.StageCount];

    private RollingValue winRoll;
    private string inlineReason = string.Empty;
    private string celebratedRoundId = string.Empty;
    private long settledPayout;
    private int requestedCards = 1;
    private bool entered;
    private Vector2 celebrationAnchor;

    public BingoCabinet(CasinoStore chips, CasinoRoomsStore rooms, Action openCashier, Action leaveRoom)
    {
        this.chips = chips;
        this.rooms = rooms;
        this.openCashier = openCashier;
        this.leaveRoom = leaveRoom;
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
        particles.Clear();
        inlineReason = string.Empty;
        celebratedRoundId = string.Empty;
        settledPayout = 0;
        requestedCards = 1;
        winRoll.Snap(0);
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        ConsumeStakeResults();

        var room = rooms.Room;
        var held = room.State;
        var snapshot = held?.Snapshot;
        var board = held?.Bingo;
        var mine = snapshot is null ? null : rooms.BingoCardsFor(snapshot.RoomId, snapshot.RoundIndex);
        playback.Update(snapshot, board, mine, delta);
        particles.Update(delta);

        var closedReason = room.ClosedReason;
        if (closedReason.Length > 0)
        {
            DrawClosed(body, ui, closedReason, scale);
            return;
        }

        var state = chips.State;
        if (state is null || snapshot is null)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        var remaining = room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        CelebrateSettledRoom(snapshot, board, mine, scale);

        using var surface = AppSurface.Begin(body);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
        var width = ScrollLayout.StableContentWidth();
        var calledOff = CalledOff(board, mine);
        DrawStatusRow(ui, snapshot, board, room.Attached, width, scale);
        DrawCaller(ui, snapshot, board, mine, remaining, width, scale);
        if (snapshot.Phase != CasinoRoomPhases.Open)
        {
            DrawProgress(ui, mine, width, scale);
        }

        DrawLadder(ui, board, snapshot.Phase, width, scale);
        if (snapshot.Phase == CasinoRoomPhases.Result)
        {
            DrawRoomSummary(ui, mine, calledOff, width, scale, delta);
        }

        var sitting = state.Sitting;
        var seated = sitting is not null;
        if (calledOff)
        {
            if (snapshot.Phase != CasinoRoomPhases.Result)
            {
                DrawNote(ui, Loc.T(L.Casino.BingoCalledOff), width, scale);
            }
        }
        else if (!seated)
        {
            DrawSeatMissing(ui, width, scale);
        }
        else if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            DrawComposer(ui, state, sitting!, mine, width, scale);
        }

        DrawCards(ui, mine, calledOff, snapshot.Phase, width, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        particles.Draw(ImGui.GetWindowDrawList(), scale);
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

    private void CelebrateSettledRoom(CasinoRoomSnapshotDto snapshot, CasinoBingoRoomStateDto? board,
        CasinoBingoCardsDto? mine, float scale)
    {
        var roundKey = BingoRoundPlayback.RoundKeyOf(snapshot);
        settledPayout = Settled(mine) ? mine!.Payout : 0;
        if (snapshot.Phase != CasinoRoomPhases.Result || !Settled(mine)
            || string.Equals(celebratedRoundId, roundKey, StringComparison.Ordinal))
        {
            return;
        }

        celebratedRoundId = roundKey;
        winRoll.Snap(0);
        var fullHouse = settledPayout >= PrizeAt(board, BingoRules.StageFullHouse);
        if (playback.CalledLive && playback.CardCount > 0)
        {
            UiFeedback.Play(settledPayout <= 0 ? UiSound.GameWrong
                : fullHouse ? UiSound.GamePowerUp : UiSound.GameWin);
        }

        if (settledPayout <= 0)
        {
            return;
        }

        if (fullHouse)
        {
            particles.Confetti(celebrationAnchor, 90, ConfettiPalette, 330f * scale, 5f, 1.6f);
            particles.Sparkle(celebrationAnchor, 24, Gold, 190f * scale, 4f, 1.0f);
            return;
        }

        particles.Confetti(celebrationAnchor, 40, ConfettiPalette, 250f * scale, 4f, 1.2f);
    }

    internal static int CardsInPlay(CasinoBingoRoomStateDto? board)
    {
        return board?.Cards ?? 0;
    }

    private void DrawStatusRow(AppSkin ui, CasinoRoomSnapshotDto snapshot, CasinoBingoRoomStateDto? board,
        bool attached, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = StatusRowHeight * scale;
        var hall = Loc.T(L.Casino.BingoInTheHall, GameNumber.Label(snapshot.Occupancy));
        var holders = board?.Players ?? 0;
        if (holders > 0)
        {
            hall = string.Concat(hall, "  ", Loc.T(L.Casino.BingoHoldingCards, GameNumber.Label(holders)));
        }

        Typography.Draw(drawList, new Vector2(origin.X, origin.Y + 4f * scale), hall, ui.MutedInk,
            TextStyles.Caption1);

        if (!attached)
        {
            DrawPulseChip(drawList, ui, new Vector2(origin.X + width, origin.Y),
                Loc.T(L.Casino.WheelReconnecting), scale);
        }

        ImGui.Dummy(new Vector2(width, height));
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

    private void DrawCaller(AppSkin ui, CasinoRoomSnapshotDto snapshot, CasinoBingoRoomStateDto? board,
        CasinoBingoCardsDto? mine, long remainingMs, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = CallerHeight * scale;
        var min = origin;
        var max = new Vector2(min.X + width, min.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        celebrationAnchor = new Vector2((min.X + max.X) * 0.5f, min.Y + 48f * scale);

        var centerX = (min.X + max.X) * 0.5f;
        var seconds = (int)((remainingMs + 999) / 1000);
        var ballsRolling = snapshot.Phase == CasinoRoomPhases.Locked && CardsInPlay(board) > 0;
        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 34f * scale),
                Loc.T(L.Casino.BingoCardsClose, TimeText.Duration(seconds)), ui.TitleInk, TextStyles.Title3);
            Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 64f * scale),
                Loc.T(L.Casino.BingoFirstBall, TimeText.Duration(seconds)), ui.MutedInk, TextStyles.Caption1);
        }
        else if (ballsRolling)
        {
            if (playback.LatestBall > 0)
            {
                DrawBallShow(drawList, ui, centerX, min.Y, scale);
            }
            else
            {
                Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 48f * scale),
                    Loc.T(L.Casino.BingoProgressWaiting), ui.MutedInk, TextStyles.Subheadline);
            }

            if (HeldCards(mine) == 0)
            {
                DrawPulseChip(drawList, ui, new Vector2(max.X - 10f * scale, min.Y + 10f * scale),
                    Loc.T(L.Casino.BingoNextRoom,
                        TimeText.Duration(NextRoomSeconds(snapshot.Phase, remainingMs))), scale);
            }
        }
        else
        {
            DrawNextRoomHero(drawList, ui, centerX, min.Y,
                NextRoomSeconds(snapshot.Phase, remainingMs), scale);
        }

        DrawBallRail(drawList, ui, board, min, max, scale);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawBallShow(ImDrawListPtr drawList, AppSkin ui, float centerX, float top, float scale)
    {
        var ballCenter = new Vector2(centerX, top + 52f * scale);
        if (playback.Rolling)
        {
            var roll = playback.RollProgress;
            var wobble = (1f - roll) * 3.5f * scale;
            var jitter = new Vector2(MathF.Sin(playback.SinceBall * 41f) * wobble,
                MathF.Cos(playback.SinceBall * 37f) * wobble);
            drawList.AddCircleFilled(ballCenter, 44f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.Accent, 0.10f + 0.14f * (1f - roll))), 40);
            BingoCardArt.DrawBallChip(drawList, ballCenter + jitter, 38f * scale,
                playback.RollingFace(playback.BallCount), 0.55f + 0.35f * roll, ui.Palette.HeaderInk);
        }
        else
        {
            var settle = MathF.Min(1f,
                (playback.SinceBall - BingoRoundPlayback.BallRollSeconds)
                / BingoRoundPlayback.BallEntrySeconds);
            var radius = 38f * scale * (0.90f + 0.10f * Easing.EaseOutBack(MathF.Max(0.01f, settle)));
            drawList.AddCircleFilled(ballCenter, radius * 1.42f,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.Accent, 0.18f * (1f - settle * 0.5f))), 40);
            BingoCardArt.DrawBallChip(drawList, ballCenter, radius, playback.LatestBall, 1f,
                ui.Palette.HeaderInk);
        }

        Typography.DrawCentered(drawList, new Vector2(centerX, top + 104f * scale),
            Loc.T(L.Casino.BingoCalledCount, GameNumber.Label(playback.BallCount),
                GameNumber.Label(BingoRules.Balls)), ui.MutedInk, TextStyles.Subheadline);
    }

    private static void DrawNextRoomHero(ImDrawListPtr drawList, AppSkin ui, float centerX, float top,
        int nextRoomSeconds, float scale)
    {
        var caption = Loc.T(L.Casino.BingoWaitingRoom);
        var captionCenter = new Vector2(centerX, top + 30f * scale);
        Typography.DrawCentered(drawList, captionCenter, caption, ui.MutedInk, TextStyles.Subheadline);
        var captionSize = Typography.Measure(caption, TextStyles.Subheadline);
        DrawBreathingDot(drawList, ui,
            new Vector2(captionCenter.X - captionSize.X * 0.5f - 10f * scale, captionCenter.Y), scale);
        Typography.DrawCentered(drawList, new Vector2(centerX, top + 62f * scale),
            TimeText.Duration(nextRoomSeconds), ui.TitleInk, TextStyles.Title1);
        Typography.DrawCentered(drawList, new Vector2(centerX, top + 94f * scale),
            Loc.T(L.Casino.BingoNextRoomSale), ui.MutedInk, TextStyles.Caption1);
    }

    private static void DrawPulseChip(ImDrawListPtr drawList, AppSkin ui, Vector2 topRight, string label,
        float scale)
    {
        var labelSize = Typography.Measure(label, TextStyles.Caption2);
        var chipMin = new Vector2(topRight.X - labelSize.X - 18f * scale, topRight.Y);
        var chipMax = new Vector2(topRight.X, topRight.Y + labelSize.Y + 7f * scale);
        Squircle.Fill(drawList, chipMin, chipMax, (chipMax.Y - chipMin.Y) * 0.5f,
            ImGui.GetColorU32(ui.FieldSurface));
        DrawBreathingDot(drawList, ui, new Vector2(chipMin.X + 8f * scale, (chipMin.Y + chipMax.Y) * 0.5f),
            scale);
        Typography.Draw(drawList, new Vector2(chipMin.X + 14f * scale, chipMin.Y + 3.5f * scale), label,
            ui.MutedInk, TextStyles.Caption2);
    }

    private static void DrawBreathingDot(ImDrawListPtr drawList, AppSkin ui, Vector2 center, float scale)
    {
        drawList.AddCircleFilled(center, 2.6f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f + 0.45f * Pulse.Wave(Pulse.Breath))), 12);
    }

    private readonly record struct CardProgress(int CardIndex, int Gap, int GoalStage);

    private CardProgress BestProgress(CasinoBingoCardsDto? mine)
    {
        var holding = HeldCards(mine);
        var best = new CardProgress(-1, int.MaxValue, BingoRules.StageLine);
        for (var cardIndex = 0; cardIndex < holding; cardIndex++)
        {
            var mask = playback.AutoMaskOf(cardIndex);
            var gap = BingoRules.NextGoalGap(mask, out var goalStage);
            if (gap < best.Gap)
            {
                best = new CardProgress(cardIndex, gap, goalStage);
            }
        }

        return best;
    }

    private void DrawProgress(AppSkin ui, CasinoBingoCardsDto? mine, float width, float scale)
    {
        if (HeldCards(mine) == 0)
        {
            return;
        }

        var best = BestProgress(mine);
        if (best.CardIndex < 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = ProgressHeight * scale;
        var min = origin;
        var max = new Vector2(min.X + width, min.Y + height);
        var hot = best.Gap <= 1;
        var rounding = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, min, max, rounding);
        if (hot)
        {
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Gold), 1.4f * scale);
        }

        var headline = HeadlineFor(best);
        var ink = hot ? Gold : ui.TitleInk;
        var centerY = (min.Y + max.Y) * 0.5f;
        var pad = Metrics.Space.Lg * scale;
        Typography.Draw(drawList, new Vector2(min.X + pad, centerY - 15f * scale), headline, ink,
            TextStyles.Title3);

        var onCard = Loc.T(L.Casino.BingoProgressOn,
            Loc.T(L.Casino.BingoCardLabel, GameNumber.Label(best.CardIndex + 1)));
        Typography.Draw(drawList, new Vector2(min.X + pad, centerY + 6f * scale), onCard, ui.MutedInk,
            TextStyles.Subheadline);

        if (hot)
        {
            var pulse = 0.5f + 0.5f * Pulse.Wave(Pulse.Fast);
            var radius = 16f * scale;
            var center = new Vector2(max.X - pad - radius, centerY);
            drawList.AddCircleFilled(center, radius * (0.94f + 0.10f * pulse),
                ImGui.GetColorU32(Palette.WithAlpha(Gold, 0.22f + 0.18f * pulse)), 28);
            Typography.DrawCentered(drawList, center, GameNumber.Label(best.Gap), Gold,
                TextStyles.Title3);
        }

        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private static string HeadlineFor(CardProgress best)
    {
        if (best.Gap <= 0)
        {
            return Loc.T(L.Casino.BingoProgressAllDone);
        }

        if (best.Gap == 1)
        {
            return best.GoalStage switch
            {
                BingoRules.StageTwoLines => Loc.T(L.Casino.BingoAwayReadyTwoLines),
                BingoRules.StageFullHouse => Loc.T(L.Casino.BingoAwayReadyFullHouse),
                _ => Loc.T(L.Casino.BingoAwayReadyLine),
            };
        }

        var count = GameNumber.Label(best.Gap);
        return best.GoalStage switch
        {
            BingoRules.StageTwoLines => Loc.T(L.Casino.BingoAwayTwoLines, count),
            BingoRules.StageFullHouse => Loc.T(L.Casino.BingoAwayFullHouse, count),
            _ => Loc.T(L.Casino.BingoAwayLine, count),
        };
    }

    private void DrawBallRail(ImDrawListPtr drawList, AppSkin ui, CasinoBingoRoomStateDto? board, Vector2 min,
        Vector2 max, float scale)
    {
        var balls = BingoRoundPlayback.CalledBalls(board);
        if (balls.Length == 0)
        {
            return;
        }

        var radius = 11f * scale;
        var gap = 6f * scale;
        var shown = balls.Length < RailCalls ? balls.Length : RailCalls;
        var step = radius * 2f + gap;
        var railWidth = shown * step - gap;
        var startX = (min.X + max.X) * 0.5f - railWidth * 0.5f + radius;
        var label = Loc.T(L.Casino.BingoRecentCalls);
        Typography.Draw(drawList, new Vector2(min.X + 14f * scale, max.Y - BallRailHeight * scale), label,
            ui.MutedInk, TextStyles.Footnote);
        var railY = max.Y - radius - 8f * scale;
        for (var index = 0; index < shown; index++)
        {
            var ball = balls[balls.Length - shown + index];
            var newest = index == shown - 1;
            var alpha = 0.40f + 0.60f * ((index + 1f) / shown);
            var center = new Vector2(startX + index * step, railY);
            if (newest)
            {
                var breath = 0.45f + 0.55f * Pulse.Wave(Pulse.Breath);
                drawList.AddCircle(center, radius + 4f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(Gold, 0.30f + 0.35f * breath)), 28, 1.8f * scale);
            }

            BingoCardArt.DrawBallChip(drawList, center, radius, ball, alpha, ui.Palette.HeaderInk);
        }
    }

    private void DrawLadder(AppSkin ui, CasinoBingoRoomStateDto? board, int phase, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var cardsInPlay = CardsInPlay(board);
        var seeded = cardsInPlay == 0;
        DisplayLadder(board, ladder);
        var prizeCardCap = PrizeCardCapOf(board);
        var capNote = seeded
            ? Loc.T(L.Casino.BingoLadderSeeds)
            : cardsInPlay >= prizeCardCap
                ? Loc.T(L.Casino.BingoLadderCapped, GameNumber.Label(prizeCardCap))
                : Loc.T(L.Casino.BingoLadderGrows, GameNumber.Label(prizeCardCap));
        var inset = 14f * scale;
        var innerWidth = width - inset * 2f;
        var heading = seeded && phase != CasinoRoomPhases.Open
            ? Loc.T(L.Casino.BingoLadderNextHeading)
            : Loc.T(L.Casino.BingoLadderHeading);
        var headingSize = Typography.Measure(heading, TextStyles.FootnoteEmphasized);
        var noteBlock = Typography.MeasureWrappedBlock(capNote, TextStyles.Caption2, innerWidth);
        var height = inset * 2f + headingSize.Y + 6f * scale + BingoRules.StageCount * LadderRowHeight * scale
            + noteBlock.Y;
        var min = origin;
        var max = new Vector2(min.X + width, min.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);

        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + inset), heading, ui.TitleInk,
            TextStyles.FootnoteEmphasized);
        if (!seeded)
        {
            var inPlay = Loc.T(L.Casino.BingoCardsInPlay, GameNumber.Label(cardsInPlay));
            var inPlaySize = Typography.Measure(inPlay, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(max.X - inset - inPlaySize.X, min.Y + inset), inPlay,
                ui.MutedInk, TextStyles.Caption1);
        }

        var rowY = min.Y + inset + headingSize.Y + 6f * scale;
        for (var stage = 0; stage < BingoRules.StageCount; stage++)
        {
            DrawLadderRow(drawList, ui, board, stage, min.X + inset, rowY, innerWidth, scale);
            rowY += LadderRowHeight * scale;
        }

        Typography.DrawWrappedLeft(new Vector2(min.X + inset, rowY + 2f * scale), capNote, ui.MutedInk,
            TextStyles.Caption2, innerWidth);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawLadderRow(ImDrawListPtr drawList, AppSkin ui, CasinoBingoRoomStateDto? board, int stage,
        float left, float y, float width, float scale)
    {
        var awarded = StageAwarded(board, stage);
        var name = Loc.T(StageNames[stage]);
        var ink = awarded is null ? ui.TitleInk : Gold;
        var rowCenter = y + LadderRowHeight * scale * 0.42f;

        var prize = awarded?.Prize ?? ladder[stage];
        var prizeText = NumberText.Group(prize);
        var prizeSize = CurrencyGlyph.MeasureAmount(prizeText, TextStyles.Title3);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(left + width - prizeSize.X, rowCenter - prizeSize.Y * 0.5f),
            prizeText, CurrencyKind.Chips, ink, TextStyles.Title3);

        var nameSize = Typography.Measure(name, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(left, rowCenter - nameSize.Y * 0.5f), name, ink,
            TextStyles.Subheadline);

        if (awarded is null)
        {
            return;
        }

        var chip = awarded.Winners > 1
            ? Loc.T(L.Casino.BingoLadderShared, GameNumber.Label(awarded.Winners),
                GameNumber.Label(awarded.Ball))
            : Loc.T(L.Casino.BingoLadderGone, GameNumber.Label(awarded.Ball));
        var chipSize = Typography.Measure(chip, TextStyles.Caption1);
        var chipPad = 7f * scale;
        var chipMin = new Vector2(left + nameSize.X + 10f * scale, rowCenter - chipSize.Y * 0.5f - 2f * scale);
        var chipMax = new Vector2(chipMin.X + chipSize.X + chipPad * 2f, chipMin.Y + chipSize.Y + 4f * scale);
        if (chipMax.X < left + width - prizeSize.X - 8f * scale)
        {
            Squircle.Fill(drawList, chipMin, chipMax, (chipMax.Y - chipMin.Y) * 0.5f,
                ImGui.GetColorU32(new Vector4(Gold.X, Gold.Y, Gold.Z, 0.16f)));
            Typography.Draw(drawList, new Vector2(chipMin.X + chipPad, chipMin.Y + 2f * scale), chip, Gold,
                TextStyles.Caption1);
        }
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

    private void DrawRoomSummary(AppSkin ui, CasinoBingoCardsDto? mine, bool calledOff, float width, float scale,
        float delta)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var innerWidth = width - inset * 2f;
        var title = Loc.T(L.Casino.BingoRoomWrapped);
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var won = !calledOff && Settled(mine) && settledPayout > 0;

        string outcome;
        if (calledOff)
        {
            outcome = Loc.T(L.Casino.BingoCalledOff);
        }
        else if (HeldCards(mine) == 0)
        {
            outcome = Loc.T(L.Casino.BingoWatchedRoom);
        }
        else if (!Settled(mine))
        {
            outcome = Loc.T(L.Casino.BingoCardsPending);
        }
        else if (won)
        {
            winRoll.Update((int)Math.Min(settledPayout, int.MaxValue), delta);
            outcome = Loc.T(L.Casino.BingoYouWon, NumberText.Signed(winRoll.Display));
        }
        else
        {
            outcome = Loc.T(L.Casino.BingoNoWin);
        }

        var outcomeBlock = won
            ? Typography.Measure(outcome, TextStyles.Title3)
            : Typography.MeasureWrappedBlock(outcome, TextStyles.Subheadline, innerWidth);
        var height = inset * 2f + titleSize.Y + 8f * scale + MathF.Max(outcomeBlock.Y, 26f * scale);
        var min = origin;
        var max = new Vector2(min.X + width, min.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        Squircle.Stroke(drawList, min, max, Metrics.Radius.Grouped * scale,
            ImGui.GetColorU32(Palette.WithAlpha(won ? Gold : ui.Accent, 0.35f)), 1f * scale);

        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + inset), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var outcomeY = min.Y + inset + titleSize.Y + 8f * scale;
        if (won)
        {
            Typography.Draw(drawList, new Vector2(min.X + inset, outcomeY), outcome, Gold,
                TextStyles.Title3.Scale * winRoll.PopScale, TextStyles.Title3.Weight);
        }
        else
        {
            Typography.DrawWrappedLeft(new Vector2(min.X + inset, outcomeY), outcome, ui.MutedInk,
                TextStyles.Subheadline, innerWidth);
        }

        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawComposer(AppSkin ui, CasinoStateDto state, CasinoSittingDto sitting,
        CasinoBingoCardsDto? mine, float width, float scale)
    {
        if (inlineReason.Length > 0)
        {
            DrawNote(ui, CasinoReasons.Text(inlineReason, chips.Ceiling.MaxBet), width, scale);
        }

        var holding = HeldCards(mine);
        var room = BingoRules.MaxCards - holding;
        if (room <= 0)
        {
            DrawNote(ui, Loc.T(L.Casino.BingoHoldingFull, CardCountLabel(holding)), width, scale);
            return;
        }

        if (requestedCards > room)
        {
            requestedCards = room;
        }

        if (requestedCards < 1)
        {
            requestedCards = 1;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var heading = holding > 0
            ? Loc.T(L.Casino.BingoBuyMoreHeading, CardCountLabel(holding))
            : Loc.T(L.Casino.BingoBuyHeading);
        Typography.Draw(drawList, origin, heading, ui.MutedInk, TextStyles.FootnoteEmphasized);
        var price = Loc.T(L.Casino.BingoCardPrice, NumberText.Group(BingoRules.CardPrice),
            GameNumber.Label(BingoRules.MaxCards));
        var priceSize = Typography.Measure(price, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(origin.X + width - priceSize.X, origin.Y), price, ui.MutedInk,
            TextStyles.Caption1);

        var segmentY = origin.Y + 20f * scale;
        DrawCountSegments(drawList, ui, origin.X, segmentY, width, room, scale);

        var stake = BingoRules.StakeFor(requestedCards);
        var thin = sitting.Stack < stake;
        var blocked = state.StakesPaused || state.Draining;
        var canBuy = !blocked && !thin && !rooms.StakeInFlight;
        var label = Loc.T(L.Casino.BingoBuyFor, CardCountLabel(requestedCards),
            NumberText.Group(stake));
        var pillY = segmentY + SegmentHeight * scale + Metrics.Space.Sm * scale;
        var pillRect = new Rect(new Vector2(origin.X, pillY),
            new Vector2(origin.X + width, pillY + PillHeight * scale));
        if (ui.ActionPill(pillRect, label, canBuy, TextStyles.Subheadline))
        {
            inlineReason = string.Empty;
            rooms.BuyBingoCards(requestedCards);
            UiFeedback.Play(UiSound.CasinoChips);
        }

        var noteY = pillRect.Max.Y + 6f * scale;
        var note = blocked
            ? Loc.T(state.StakesPaused ? L.Casino.PausedTitle : L.Casino.DrainingTitle)
            : thin ? Loc.T(L.Casino.SlotsLowStack) : Loc.T(L.Casino.BingoBuyAgainNote);
        var noteBlock = Typography.MeasureWrappedBlock(note, TextStyles.Caption2, width);
        Typography.DrawWrappedLeft(new Vector2(origin.X, noteY), note, ui.MutedInk, TextStyles.Caption2, width);
        var finalNote = Loc.T(L.Casino.BingoCardsFinal);
        var finalY = noteY + noteBlock.Y + 2f * scale;
        var finalBlock = Typography.MeasureWrappedBlock(finalNote, TextStyles.Caption2, width);
        Typography.DrawWrappedLeft(new Vector2(origin.X, finalY), finalNote, ui.MutedInk, TextStyles.Caption2,
            width);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, finalY + finalBlock.Y - origin.Y + Metrics.Space.Md * scale));
        if (thin)
        {
            DrawCashierPill(ui, width, scale);
        }
    }

    private void DrawCashierPill(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var pillRect = new Rect(new Vector2(origin.X + width * 0.25f, origin.Y),
            new Vector2(origin.X + width * 0.75f, origin.Y + 38f * scale));
        if (ui.GhostButton(pillRect, Loc.T(L.Casino.Cashier)))
        {
            openCashier();
        }

        ImGui.Dummy(new Vector2(width, 38f * scale + Metrics.Space.Sm * scale));
    }

    private void DrawCountSegments(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width,
        int headroom, float scale)
    {
        var height = SegmentHeight * scale;
        var rounding = height * 0.5f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Surfaces.Fill(ui.Ink, FillLevel.Tertiary)));
        var slotWidth = width / BingoRules.MaxCards;
        for (var count = 1; count <= BingoRules.MaxCards; count++)
        {
            var slotMin = new Vector2(left + (count - 1) * slotWidth, y);
            var slotMax = new Vector2(slotMin.X + slotWidth, max.Y);
            var enabled = count <= headroom;
            var selected = count == requestedCards;
            var hovered = enabled && UiInteract.Hover(slotMin, slotMax);
            if (selected)
            {
                var padding = new Vector2(3f * scale, 3f * scale);
                Squircle.Fill(drawList, slotMin + padding, slotMax - padding, rounding - padding.Y,
                    ImGui.GetColorU32(ui.Accent with { W = 1f }));
            }
            else if (hovered)
            {
                Squircle.Fill(drawList, slotMin, slotMax, rounding,
                    ImGui.GetColorU32(Surfaces.Fill(ui.Ink, FillLevel.Quaternary)));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var ink = selected ? CountThumbInk : enabled ? ui.BodyInk : ui.MutedInk;
            Typography.DrawCentered(drawList, (slotMin + slotMax) * 0.5f, GameNumber.Label(count),
                Palette.WithAlpha(ink, enabled ? 1f : 0.4f), TextStyles.Subheadline);
            if (enabled && UiInteract.Click(slotMin, slotMax, hovered))
            {
                requestedCards = count;
                inlineReason = string.Empty;
            }
        }
    }

    private static string CardCountLabel(int cards)
    {
        return cards == 1 ? Loc.T(L.Casino.BingoOneCard) : Loc.T(L.Casino.BingoCardCount, GameNumber.Label(cards));
    }

    private void DrawCards(AppSkin ui, CasinoBingoCardsDto? mine, bool calledOff, int phase, float width,
        float scale)
    {
        var holding = HeldCards(mine);
        if (holding == 0)
        {
            if (calledOff || phase == CasinoRoomPhases.Result)
            {
                return;
            }

            DrawNote(ui,
                Loc.T(phase == CasinoRoomPhases.Open ? L.Casino.BingoNoCardsHint : L.Casino.BingoRoomRolling),
                width, scale);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var gap = CardGap * scale;
        var labelHeight = CardLabelHeight * scale;
        var hero = HeroIndex(mine, holding);

        var plate = CardPlatePadding * scale;
        var columns = holding > 1 ? 2 : 1;
        var usable = width - plate * 2f;
        var cardWidth = columns == 2 ? (usable - gap - plate * 2f) * 0.5f : usable * 0.56f;
        var cardHeight = BingoCardArt.HeightFor(cardWidth);
        var rowStep = cardHeight + labelHeight + gap;
        var rows = (holding + columns - 1) / columns;
        for (var cardIndex = 0; cardIndex < holding; cardIndex++)
        {
            var column = cardIndex % columns;
            var row = cardIndex / columns;
            var left = columns == 1
                ? origin.X + (width - cardWidth) * 0.5f
                : origin.X + plate + column * (cardWidth + gap + plate * 2f);
            var cardMin = new Vector2(left, origin.Y + row * rowStep + labelHeight);
            var card = new Rect(cardMin, new Vector2(cardMin.X + cardWidth, cardMin.Y + cardHeight));
            DrawCard(drawList, ui, card, mine, cardIndex, labelHeight, cardIndex == hero && holding > 1, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * rowStep - gap + Metrics.Space.Sm * scale));

        var footnote = Loc.T(L.Casino.BingoMarksAuto);
        var footBlock = Typography.MeasureWrappedBlock(footnote, TextStyles.Caption2, width);
        Typography.DrawWrappedLeft(ImGui.GetCursorScreenPos(), footnote, ui.MutedInk, TextStyles.Caption2, width);
        ImGui.Dummy(new Vector2(width, footBlock.Y));
    }

    private int HeroIndex(CasinoBingoCardsDto? mine, int holding)
    {
        var best = BestProgress(mine);
        return best.CardIndex >= 0 && best.CardIndex < holding ? best.CardIndex : 0;
    }

    private void DrawCard(ImDrawListPtr drawList, AppSkin ui, Rect card, CasinoBingoCardsDto? mine, int cardIndex,
        float labelHeight, bool hero, float scale)
    {
        var rounding = Metrics.Radius.Sm * scale;
        var padding = new Vector2(CardPlatePadding * scale, CardPlatePadding * scale);
        var plateMin = card.Min - padding;
        var plateMax = card.Max + padding;
        Elevation.Card(drawList, plateMin, plateMax, rounding, scale, hero ? 1f : 0.6f);
        Squircle.Fill(drawList, plateMin, plateMax, rounding, ImGui.GetColorU32(ui.FieldSurface));
        if (hero)
        {
            Squircle.Stroke(drawList, plateMin, plateMax, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(Gold, 0.55f)), 1.6f * scale);
        }

        var autoMask = playback.AutoMaskOf(cardIndex);
        var stampedMask = playback.StampedMaskOf(cardIndex);
        var label = Loc.T(L.Casino.BingoCardLabel, GameNumber.Label(cardIndex + 1));
        var labelStyle = hero ? TextStyles.SubheadlineEmphasized : TextStyles.Footnote;
        Typography.Draw(drawList, new Vector2(card.Min.X, card.Min.Y - labelHeight), label,
            hero ? ui.TitleInk : ui.MutedInk, labelStyle);

        var gap = BingoRules.NextGoalGap(autoMask, out _);
        if (gap <= 2 && gap > 0)
        {
            var chip = gap == 1 ? Loc.T(L.Casino.BingoOneAway) : GameNumber.Label(gap);
            var chipStyle = hero ? TextStyles.FootnoteEmphasized : TextStyles.Caption1;
            var chipSize = Typography.Measure(chip, chipStyle);
            var chipPad = 7f * scale;
            var chipMax = new Vector2(card.Max.X, card.Min.Y - labelHeight * 0.12f);
            var chipMin = new Vector2(chipMax.X - chipSize.X - chipPad * 2f, chipMax.Y - chipSize.Y - 3f * scale);
            Squircle.Fill(drawList, chipMin, chipMax, (chipMax.Y - chipMin.Y) * 0.5f,
                ImGui.GetColorU32(new Vector4(Gold.X, Gold.Y, Gold.Z, gap == 1 ? 0.26f : 0.14f)));
            Typography.Draw(drawList, new Vector2(chipMin.X + chipPad, chipMin.Y + 1.5f * scale), chip, Gold,
                chipStyle);
        }

        if (Settled(mine) && mine!.Payout > 0)
        {
            Squircle.Stroke(drawList, plateMin, plateMax, rounding, ImGui.GetColorU32(Gold), 1.6f * scale);
        }

        var numbers = BingoRoundPlayback.CardAt(mine, cardIndex);
        BingoCardArt.Draw(drawList, ui, card, numbers, autoMask, stampedMask, playback, cardIndex, scale);
        HandleCardTaps(card, cardIndex, autoMask, stampedMask);
    }

    private void HandleCardTaps(Rect card, int cardIndex, int autoMask, int stampedMask)
    {
        var pending = autoMask & ~stampedMask;
        if (pending == 0 || !UiInteract.Hover(card.Min, card.Max))
        {
            return;
        }

        var cellSize = card.Width / BingoCardArt.Columns;
        for (var cellIndex = 0; cellIndex < BingoRules.Cells; cellIndex++)
        {
            if ((pending & (1 << cellIndex)) == 0)
            {
                continue;
            }

            var column = BingoRules.ColumnOfCell(cellIndex);
            var row = BingoRules.RowOfCell(cellIndex);
            var min = new Vector2(card.Min.X + column * cellSize, card.Min.Y + row * cellSize);
            var max = new Vector2(min.X + cellSize, min.Y + cellSize);
            var hovered = UiInteract.Hover(min, max);
            if (hovered && UiInteract.Click(min, max, hovered))
            {
                playback.Stamp(cardIndex, cellIndex);
                return;
            }
        }
    }

    private void DrawNote(AppSkin ui, string message, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 12f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, width - inset * 2f);
        var height = block.Y + inset * 2f;
        var min = origin;
        var max = new Vector2(min.X + width, min.Y + height);
        Squircle.Fill(drawList, min, max, Metrics.Radius.Grouped * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Squircle.Stroke(drawList, min, max, Metrics.Radius.Grouped * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + inset), message, ui.TitleInk,
            TextStyles.Footnote, width - inset * 2f);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawSeatMissing(AppSkin ui, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var inset = 14f * scale;
        var title = Loc.T(L.Casino.CabinetNoChipsTitle);
        var hint = Loc.T(L.Casino.CabinetNoChipsHint);
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var block = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - inset * 2f);
        var height = titleSize.Y + block.Y + inset * 2f + 6f * scale + 44f * scale + Metrics.Space.Sm * scale;
        var min = origin;
        var max = new Vector2(min.X + width, min.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + inset), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + inset + titleSize.Y + 6f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - inset * 2f);

        var pillRect = new Rect(new Vector2(min.X + width * 0.2f, max.Y - inset - 44f * scale),
            new Vector2(min.X + width * 0.8f, max.Y - inset));
        if (ui.PillButton(pillRect, Loc.T(L.Casino.Cashier), true, true))
        {
            openCashier();
        }

        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawClosed(Rect body, AppSkin ui, string reason, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pad = Metrics.Space.Md * scale;
        var left = body.Min.X + pad;
        var width = body.Width - pad * 2f;
        var inset = 14f * scale;
        var title = Loc.T(L.Casino.BingoClosedTitle);
        var hint = Loc.T(CasinoReasons.TryMessage(reason, out var known) ? known : L.Casino.BingoClosedHint);
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var block = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - inset * 2f);
        var height = titleSize.Y + block.Y + inset * 2f + 6f * scale;
        var min = new Vector2(left, body.Min.Y + Metrics.Space.Lg * scale);
        var max = new Vector2(left + width, min.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + inset), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + inset, min.Y + inset + titleSize.Y + 6f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - inset * 2f);

        var pillY = max.Y + Metrics.Space.Md * scale;
        var pillRect = new Rect(new Vector2(left + width * 0.2f, pillY),
            new Vector2(left + width * 0.8f, pillY + 44f * scale));
        if (ui.PillButton(pillRect, Loc.T(L.Casino.WheelBackToFloor), true, true))
        {
            leaveRoom();
        }
    }

}
