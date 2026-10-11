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

internal sealed class WheelCabinet : ICabinetIdle
{
    public const float MaxRingRadius = 170f;

    private const float NoticeGap = 6f;
    private const float BannerPad = 12f;
    private const float BannerRadius = 18f;
    private const float BannerTextShare = 0.92f;
    private const float RimInset = 16f;
    private const float LockFlashSeconds = 0.45f;
    private const float IdleTurnRate = 0.25f;
    private const float LandPopSeconds = 0.35f;
    private const float MinimumPop = 0.05f;
    private const string RangeJoin = "-";

    private readonly CasinoStore chips;
    private readonly CasinoRoomsStore rooms;
    private readonly Action leaveRoom;
    private readonly WheelRoundPlayback playback = new();
    private readonly BetComposer composer = new("##wheelBet");
    private readonly long[] spotStakes = new long[WheelRules.SpotCount];
    private readonly LabelSlot[] bettors = new LabelSlot[WheelRules.SpotCount];
    private readonly WheelRecentRail recent = new();
    private readonly WheelStatusLine statusLine = new();

    private string inlineReason = string.Empty;
    private string resultText = string.Empty;
    private string resultKey = string.Empty;
    private LanguageInfo? resultLanguage;
    private long resultStaked;
    private long resultReturned;
    private string celebratedRoundId = string.Empty;
    private string returnText = string.Empty;
    private int returnLow = -1;
    private int returnHigh = -1;
    private LanguageInfo? returnLanguage;
    private int selectedSpot;
    private int lastTickSegment = int.MinValue;
    private WheelPointer pointer;
    private float lastAngle;
    private float idleAngle;
    private bool entered;
    private Vector2 ringCenter;
    private float ringRadius;

    public WheelCabinet(CasinoStore chips, CasinoRoomsStore rooms, Action leaveRoom)
    {
        this.chips = chips;
        this.rooms = rooms;
        this.leaveRoom = leaveRoom;
    }

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public static float DeckHeight => BetComposer.DeckHeightFor(false, false);

    public void Enter()
    {
        entered = true;
        inlineReason = string.Empty;
        composer.Reset(WheelRules.MinStakePerSpot);
        rooms.Enter(CasinoRoomIds.WheelFloor);
    }

    public void Reset()
    {
        if (entered)
        {
            rooms.Leave();
        }

        entered = false;
        playback.Reset();
        inlineReason = string.Empty;
        celebratedRoundId = string.Empty;
        lastTickSegment = int.MinValue;
        recent.Reset();
        pointer.Reset();
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
        var wheel = held?.Wheel;
        var remaining = snapshot is null
            ? 0
            : room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        playback.Update(snapshot, wheel, remaining, frame.SnapToTruth ? WheelChoreography.SpinSeconds : delta);
        if (frame.SnapToTruth)
        {
            pointer.Reset();
        }

        pointer.Step(delta);
        recent.Sync(wheel?.Recent);
        if (playback.Stage == WheelStage.Settling && WheelRules.IsSegment(playback.Segment))
        {
            recent.Land(WheelRules.SpotAt(playback.Segment));
        }
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

        CollectStakes(snapshot);
        CelebrateSettledRound(stage, snapshot, frame);
        var status = WheelStatus.Of(room.Unreachable(Environment.TickCount64), snapshot.Phase, playback.Stage,
            remaining);
        statusLine.Draw(drawList, frame.Layout.Ribbon, status, snapshot.Occupancy, scale);
        var board = WheelBoardLayout.Compute(safe, scale);
        WheelHistoryStrip.Draw(drawList, board.History, recent,
            LandingPop(playback.SpunLive, playback.Stage, playback.StageSeconds), scale);
        DrawWheel(drawList, board.Wheel, frame.Phase, delta, scale);
        var choosing = snapshot.Phase == CasinoRoomPhases.Open && !frame.Blocked;
        WheelSpotCaption.Draw(drawList, board.Caption, choosing ? Loc.T(L.Casino.WheelPickSpot) : string.Empty,
            ReturnCaption(wheel), scale);
        DrawCards(drawList, ui, wheel, board, choosing, scale);
        DrawResultBanner(drawList, stage, snapshot, board.Wheel, frame.Phase, scale);
        if (inlineReason.Length > 0)
        {
            var message = CasinoReasons.Text(inlineReason, chips.Ceiling.MaxBet);
            var height = CasinoNotice.Height(CasinoNoticeKind.Reason, string.Empty, message, safe.Width, scale);
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty, message, safe.Min.X,
                board.Caption.Min.Y - height - NoticeGap * scale, safe.Width, scale);
        }

        DrawDeck(stage, frame, ui, state, state.Sitting ?? CasinoWire.NoBankroll, snapshot);
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        var scale = UiScale.Current;
        idleAngle += deltaSeconds * IdleTurnRate;
        var radius = MathF.Min(rect.Width, rect.Height) * 0.42f;
        var center = rect.Center;
        DrawRim(drawList, center, radius, idleAngle * 4f, 0.7f, scale);
        WheelRingArt.Draw(drawList, center, radius, idleAngle, -1, 0f, scale);
        WheelRingArt.DrawPointer(drawList, center, radius, scale);
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
            }
        }

        if (rooms.TakeStakeFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }
    }

    private void CollectStakes(CasinoRoomSnapshotDto snapshot)
    {
        Array.Clear(spotStakes);
        var bets = rooms.WheelBetsFor(snapshot.RoomId, snapshot.RoundIndex)?.Bets;
        if (bets is null)
        {
            return;
        }

        for (var index = 0; index < bets.Length; index++)
        {
            var spot = bets[index].Spot;
            if (WheelRules.IsSpot(spot))
            {
                spotStakes[spot] += bets[index].Amount;
            }
        }
    }

    private long StakedThisRound()
    {
        var total = 0L;
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            total += spotStakes[spot];
        }

        return total;
    }

    private long ReturnOn(int segment)
    {
        var total = 0L;
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            total += WheelRules.Returned(segment, spot, spotStakes[spot]);
        }

        return total;
    }

    private void CelebrateSettledRound(CasinoStage stage, CasinoRoomSnapshotDto snapshot, in CasinoStageFrame frame)
    {
        var roundKey = WheelRoundPlayback.RoundKeyOf(snapshot);
        if (playback.Stage != WheelStage.Settling
            || string.Equals(celebratedRoundId, roundKey, StringComparison.Ordinal))
        {
            return;
        }

        celebratedRoundId = roundKey;
        var staked = StakedThisRound();
        if (staked <= 0)
        {
            return;
        }

        var mine = rooms.WheelBetsFor(snapshot.RoomId, snapshot.RoundIndex);
        var capped = mine is { Capped: true };
        var returned = capped ? mine!.Payout : ReturnOn(playback.Segment);
        stage.Settle(new CasinoBetRecord(L.Casino.GameWheel, staked, returned, string.Empty,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), capped));
        if (playback.SpunLive)
        {
            stage.Celebration.Celebrate(staked, returned, ringCenter, frame.Instant);
        }
    }

    private void DrawWheel(ImDrawListPtr drawList, Rect area, float phase, float delta, float scale)
    {
        var radius = MathF.Min(MathF.Min(area.Width, area.Height) * 0.5f - RimInset * scale, MaxRingRadius * scale);
        radius = MathF.Max(radius, 1f);
        ringRadius = radius;
        ringCenter = new Vector2(area.Center.X, area.Center.Y + RimInset * 0.5f * scale);
        TickPegs(delta);
        var settling = playback.Stage == WheelStage.Settling;
        var flash = playback.Stage == WheelStage.Locking && playback.StageSeconds < LockFlashSeconds
            ? 1f - playback.StageSeconds / LockFlashSeconds
            : 0f;
        var glow = settling ? 0.55f + 0.45f * Pulse.Wave(Pulse.Breath) : 0f;
        DrawRim(drawList, ringCenter, radius, phase * (settling ? 2f : 1f), settling ? 1f : 0.6f + flash * 0.4f, scale);
        WheelRingArt.Draw(drawList, ringCenter, radius, playback.Angle, settling ? playback.Segment : -1, glow, scale);
        WheelRingArt.DrawPointer(drawList, ringCenter, radius, scale, pointer.Deflection);
        DrawHub(drawList);
    }

    private static void DrawRim(ImDrawListPtr drawList, Vector2 center, float radius, float phase, float lit,
        float scale)
    {
        var rim = radius + RimInset * scale;
        var rect = new Rect(center - new Vector2(rim, rim), center + new Vector2(rim, rim));
        CasinoLights.BulbChase(drawList, rect, rim, scale, phase, CasinoLights.BulbPitch, CasinoColors.Money,
            CasinoColors.LightA, lit);
    }

    private void TickPegs(float delta)
    {
        var spinning = playback.Stage is WheelStage.Spinning or WheelStage.Locking;
        var angle = playback.Angle;
        var speed = delta > 0f ? (angle - lastAngle) / delta : 0f;
        lastAngle = angle;
        var segment = (int)MathF.Floor(angle / WheelChoreography.SegmentSpan);
        if (segment == lastTickSegment)
        {
            return;
        }

        var first = lastTickSegment == int.MinValue;
        lastTickSegment = segment;
        if (!spinning || first)
        {
            return;
        }

        pointer.Kick(speed);
        CasinoSfx.Play(UiSound.WheelTick);
    }

    private void DrawHub(ImDrawListPtr drawList)
    {
        if (playback.Stage == WheelStage.Settling && WheelRules.IsSegment(playback.Segment))
        {
            var spot = WheelRules.SpotAt(playback.Segment);
            var pop = LandingPop(playback.SpunLive, playback.Stage, playback.StageSeconds);
            Typography.DrawCenteredExact(drawList, ringCenter, MultiplierLabel(spot), WheelRingArt.SpotTextInks[spot],
                TextStyles.Title2.Scale * pop, TextStyles.Title2.Weight);
            return;
        }

        drawList.AddCircleFilled(ringCenter, ringRadius * 0.20f * (0.55f + 0.25f * Pulse.Wave(Pulse.Breath)),
            ImGui.GetColorU32(CasinoColors.Money with { W = 0.55f }), 24);
    }

    private static string MultiplierLabel(int spot) => WheelRingArt.SpotLabel(spot);

    internal static float LandingPop(bool spunLive, WheelStage stage, float stageSeconds)
    {
        if (!spunLive || stage != WheelStage.Settling || stageSeconds >= LandPopSeconds)
        {
            return 1f;
        }

        return MathF.Max(MinimumPop, GameJuice.PopIn(stageSeconds / LandPopSeconds));
    }

    private void DrawCards(ImDrawListPtr drawList, AppSkin ui, CasinoWheelRoomStateDto? wheel,
        in WheelBoardLayout board, bool selectable, float scale)
    {
        var settled = playback.Stage == WheelStage.Settling && WheelRules.IsSegment(playback.Segment);
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            var rect = board.Card(spot);
            var hovered = selectable && UiInteract.Hover(rect.Min, rect.Max);
            var row = SpotOf(wheel, spot);
            var view = new WheelSpotView(spot, spotStakes[spot], row?.Bettors ?? 0, row?.Amount ?? 0L,
                spot == selectedSpot, hovered, settled && WheelRules.Wins(playback.Segment, spot), settled,
                selectable);
            WheelSpotCard.Draw(drawList, ui, rect, view, scale);
            ShowTableHint(rect, view, scale);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(rect.Min, rect.Max, hovered))
            {
                selectedSpot = spot;
                inlineReason = string.Empty;
                CasinoSfx.Play(UiSound.ChipSlide);
            }
        }
    }

    private void ShowTableHint(Rect rect, in WheelSpotView view, float scale)
    {
        if (!view.ShowsTable)
        {
            return;
        }

        var rows = WheelPodiumLayout.Compute(rect, scale);
        var table = new Rect(rows.Crowd.Min, rows.Total.Max);
        if (UiInteract.Hover(table.Min, table.Max))
        {
            HoverTooltip.Show(table, bettors[view.Spot].Get(L.Casino.WheelBettors, view.Bettors));
        }
    }

    private void DrawResultBanner(ImDrawListPtr drawList, CasinoStage stage, CasinoRoomSnapshotDto snapshot,
        Rect area, float phase, float scale)
    {
        if (playback.Stage != WheelStage.Settling || !WheelRules.IsSegment(playback.Segment))
        {
            return;
        }

        var staked = StakedThisRound();
        if (staked <= 0)
        {
            return;
        }

        var returned = ReturnOn(playback.Segment);
        var text = ResultText(WheelRoundPlayback.RoundKeyOf(snapshot), staked, returned);
        var won = returned > staked;
        var pad = BannerPad * scale;
        var maxWidth = MathF.Max(1f, area.Width * BannerTextShare - pad * 2f);
        var style = BannerStyle(text, won, maxWidth);
        var size = Typography.MeasureWrappedBlock(text, style, maxWidth);
        var half = new Vector2(size.X * 0.5f + pad, size.Y * 0.5f + pad * 0.75f);
        var center = new Vector2(area.Center.X, area.Max.Y - half.Y);
        var min = center - half;
        var max = center + half;
        var radius = BannerRadius * scale;
        Material.ThemedGlass(drawList, min, max, radius, scale, stage.Backdrop.Ground, 0.96f);
        if (won)
        {
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(CasinoColors.Money), 1.6f * scale);
            if (WinLadder.TierFor(staked, returned) >= WinTier.Nice)
            {
                CasinoLights.BulbChase(drawList, new Rect(min, max), radius, scale, phase, CasinoLights.BulbPitch,
                    CasinoColors.Money, CasinoColors.LightA, 1f);
            }
        }

        Typography.DrawWrappedCentered(drawList, center, text, won ? CasinoColors.Money : StageInks.Strong, style,
            maxWidth);
    }

    private static TextStyle BannerStyle(string text, bool won, float maxWidth)
    {
        var style = won ? TextStyles.Title2 : TextStyles.Headline;
        var floor = won ? TextStyles.Title3.Scale : TextStyles.Footnote.Scale;
        return style with { Scale = Typography.FitScale(text, maxWidth, style.Scale, floor, style.Weight) };
    }

    private string ResultText(string roundKey, long staked, long returned)
    {
        if (string.Equals(roundKey, resultKey, StringComparison.Ordinal) && staked == resultStaked
            && returned == resultReturned && ReferenceEquals(resultLanguage, Loc.Current))
        {
            return resultText;
        }

        resultKey = roundKey;
        resultStaked = staked;
        resultReturned = returned;
        resultLanguage = Loc.Current;
        var spot = MultiplierLabel(WheelRules.SpotAt(playback.Segment));
        if (returned > staked)
        {
            resultText = Loc.T(L.Casino.WheelWonOn, NumberText.Group(returned), spot);
        }
        else if (returned > 0)
        {
            resultText = Loc.T(L.Casino.WheelLandedBack, spot, NumberText.Group(returned));
        }
        else
        {
            resultText = Loc.T(L.Casino.WheelLandedNoWin, spot);
        }

        return resultText;
    }

    private string ReturnCaption(CasinoWheelRoomStateDto? wheel)
    {
        ReturnRange(wheel, out var low, out var high);
        if (low == returnLow && high == returnHigh && ReferenceEquals(returnLanguage, Loc.Current))
        {
            return returnText;
        }

        returnLow = low;
        returnHigh = high;
        returnLanguage = Loc.Current;
        var percent = low == high
            ? Percent(low)
            : string.Concat(Percent(low), RangeJoin, Percent(high));
        returnText = Loc.T(L.Strip.PaysBackShort, percent);
        return returnText;
    }

    private static string Percent(int tenths) => (tenths / 10m).ToString("0.#", Loc.Culture);

    internal static void ReturnRange(CasinoWheelRoomStateDto? wheel, out int low, out int high)
    {
        low = int.MaxValue;
        high = 0;
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            var tenths = ReturnTenths(wheel, spot);
            low = Math.Min(low, tenths);
            high = Math.Max(high, tenths);
        }
    }

    internal static int ReturnTenths(CasinoWheelRoomStateDto? board, int spot)
    {
        var row = SpotOf(board, spot);
        if (row is not null && row.ReturnBasisPoints > 0)
        {
            return row.ReturnBasisPoints / 10;
        }

        return WheelRules.ReturnBasisPointsFor(spot) / 10;
    }

    private static CasinoWheelSpotDto? SpotOf(CasinoWheelRoomStateDto? board, int spot)
    {
        var spots = board?.Spots;
        if (spots is null)
        {
            return null;
        }

        for (var index = 0; index < spots.Length; index++)
        {
            if (spots[index].Spot == spot)
            {
                return spots[index];
            }
        }

        return null;
    }

    private void DrawDeck(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, CasinoStateDto state,
        CasinoSittingDto sitting, CasinoRoomSnapshotDto snapshot)
    {
        var staked = StakedThisRound();
        var headroom = WheelRules.HeadroomOn(staked, spotStakes[selectedSpot]);
        var ceiling = chips.Ceiling.MaxBet;
        var maximum = Math.Min(headroom, ceiling);
        var open = snapshot.Phase == CasinoRoomPhases.Open;
        var enabled = open && !state.StakesPaused && !state.Draining && !rooms.StakeInFlight && !frame.Blocked
            && maximum >= WheelRules.MinStakePerSpot;
        var model = new BetComposerModel(WheelRules.MinStakePerSpot, maximum, sitting.Stack, L.Strip.BetFor, enabled,
            Repeat: stage.RepeatPressed(), Busy: rooms.StakeInFlight);
        if (composer.Draw(stage, ui, frame.Deck, model, frame.DeltaSeconds) != BetComposerAction.Confirm)
        {
            return;
        }

        inlineReason = string.Empty;
        rooms.PlaceWheelBet(selectedSpot, composer.Amount);
    }

    private void DrawClosed(ImDrawListPtr drawList, AppSkin ui, string reason, Rect safe, float scale)
    {
        var title = Loc.T(L.Casino.WheelClosedTitle);
        var hint = Loc.T(CasinoReasons.TryMessage(reason, out var known) ? known : L.Casino.WheelClosedHint);
        CasinoNotice.DrawWithAction(drawList, ui, CasinoNoticeKind.Card, title, hint, Loc.T(L.Casino.WheelBackToFloor),
            safe.Min.X, safe.Min.Y + Metrics.Space.Lg * scale, safe.Width, scale, out var pressed);
        if (pressed)
        {
            leaveRoom();
        }
    }
}
