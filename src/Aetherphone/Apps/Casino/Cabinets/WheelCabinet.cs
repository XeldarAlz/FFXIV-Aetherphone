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

    private const float PodiumHeight = 86f;
    private const float PodiumGap = 6f;
    private const float PodiumPad = 6f;
    private const float PodiumCap = 3f;
    private const float StakeChipScale = 0.7f;
    private const float RailHeight = 22f;
    private const float RailChipGap = 4f;
    private const float RimInset = 16f;
    private const float LockFlashSeconds = 0.45f;
    private const float PointerKickDecay = 8f;
    private const float PointerKickLift = 4f;
    private const float IdleTurnRate = 0.25f;
    private const int RecentShown = 12;

    private readonly CasinoStore chips;
    private readonly CasinoRoomsStore rooms;
    private readonly Action openCashier;
    private readonly Action leaveRoom;
    private readonly WheelRoundPlayback playback = new();
    private readonly BetComposer composer = new("##wheelBet");
    private readonly long[] spotStakes = new long[WheelRules.SpotCount];
    private readonly LabelSlot[] multipliers = new LabelSlot[WheelRules.SpotCount];
    private readonly LabelSlot[] bettors = new LabelSlot[WheelRules.SpotCount];
    private readonly string[] returns = new string[WheelRules.SpotCount];

    private string inlineReason = string.Empty;
    private string celebratedRoundId = string.Empty;
    private string ribbonText = string.Empty;
    private int ribbonSeconds = -1;
    private int ribbonStage = -1;
    private LanguageInfo? ribbonLanguage;
    private LanguageInfo? returnsLanguage;
    private int selectedSpot;
    private int lastTickSegment = int.MinValue;
    private float pointerKick;
    private float idleAngle;
    private bool entered;
    private Vector2 ringCenter;
    private float ringRadius;

    public WheelCabinet(CasinoStore chips, CasinoRoomsStore rooms, Action openCashier, Action leaveRoom)
    {
        this.chips = chips;
        this.rooms = rooms;
        this.openCashier = openCashier;
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
        var board = held?.Wheel;
        var remaining = snapshot is null
            ? 0
            : room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        playback.Update(snapshot, board, remaining, frame.SnapToTruth ? WheelChoreography.SpinSeconds : delta);
        pointerKick = MathF.Max(0f, pointerKick - delta * PointerKickDecay);
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
        PhaseRibbon.Draw(drawList, frame.Layout.Ribbon, RibbonLabel(snapshot.Phase, remaining), remaining,
            CasinoRoomCadence.WheelWindow(snapshot.Phase), snapshot.Occupancy,
            room.Attached ? CasinoColors.LightA : CasinoColors.InkMuted, scale);
        var podiumTop = safe.Max.Y - PodiumHeight * scale;
        var railTop = safe.Min.Y;
        DrawRecentRail(drawList, board, safe.Min.X, railTop, safe.Width, scale);
        var wheelArea = new Rect(new Vector2(safe.Min.X, railTop + (RailHeight + RailChipGap) * scale),
            new Vector2(safe.Max.X, podiumTop - PodiumGap * scale));
        DrawWheel(drawList, snapshot, wheelArea, remaining, frame.Phase, scale);
        var sitting = state.Sitting;
        var betting = snapshot.Phase == CasinoRoomPhases.Open && sitting is not null && !frame.Blocked;
        DrawPodiums(drawList, ui, board, new Rect(new Vector2(safe.Min.X, podiumTop), safe.Max), betting, scale);
        if (inlineReason.Length > 0)
        {
            var message = Loc.T(CasinoReasons.MessageFor(inlineReason));
            var height = CasinoNotice.Height(CasinoNoticeKind.Reason, string.Empty, message, safe.Width, scale);
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty, message, safe.Min.X,
                podiumTop - height - PodiumGap * scale, safe.Width, scale);
        }

        if (sitting is null)
        {
            DrawSeatMissing(drawList, ui, frame.Deck, scale);
            return;
        }

        DrawDeck(stage, frame, ui, state, sitting, snapshot);
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

        var returned = ReturnOn(playback.Segment);
        stage.Settle(new CasinoBetRecord(L.Casino.GameWheel, staked, returned, string.Empty,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        if (playback.SpunLive)
        {
            stage.Celebration.Celebrate(staked, returned, ringCenter, frame.Instant);
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
        if (playback.Stage == WheelStage.Settling)
        {
            var next = phase == CasinoRoomPhases.Result ? seconds : seconds + CasinoRoomCadence.WheelResultSeconds;
            ribbonText = Loc.T(L.Casino.RoomNextIn, TimeText.Duration(next));
        }
        else if (phase == CasinoRoomPhases.Open)
        {
            ribbonText = Loc.T(L.Casino.WheelBetsCloseIn, TimeText.Duration(seconds));
        }
        else
        {
            ribbonText = Loc.T(playback.Stage == WheelStage.Locking ? L.Casino.WheelBetsClosed : L.Casino.WheelSpinning);
        }

        return ribbonText;
    }

    private void DrawWheel(ImDrawListPtr drawList, CasinoRoomSnapshotDto snapshot, Rect area, long remainingMs,
        float phase, float scale)
    {
        var radius = MathF.Min(MathF.Min(area.Width, area.Height) * 0.5f - RimInset * scale, MaxRingRadius * scale);
        radius = MathF.Max(radius, 1f);
        ringRadius = radius;
        ringCenter = new Vector2(area.Center.X, area.Center.Y + RimInset * 0.5f * scale);
        TickPegs();
        var settling = playback.Stage == WheelStage.Settling;
        var flash = playback.Stage == WheelStage.Locking && playback.StageSeconds < LockFlashSeconds
            ? 1f - playback.StageSeconds / LockFlashSeconds
            : 0f;
        var glow = settling ? 0.55f + 0.45f * Pulse.Wave(Pulse.Breath) : 0f;
        DrawRim(drawList, ringCenter, radius, phase * (settling ? 2f : 1f), settling ? 1f : 0.6f + flash * 0.4f, scale);
        WheelRingArt.Draw(drawList, ringCenter, radius, playback.Angle, settling ? playback.Segment : -1, glow, scale);
        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            WheelRingArt.DrawCountdown(drawList, ringCenter, radius + RimInset * 0.5f * scale,
                TurnTimerRing.Fraction(remainingMs, CasinoRoomCadence.WheelOpenSeconds), CasinoColors.LightA, scale);
        }

        WheelRingArt.DrawPointer(drawList, ringCenter - new Vector2(0f, pointerKick * PointerKickLift * scale), radius,
            scale);
        DrawHub(drawList, snapshot, remainingMs);
    }

    private static void DrawRim(ImDrawListPtr drawList, Vector2 center, float radius, float phase, float lit,
        float scale)
    {
        var rim = radius + RimInset * scale;
        var rect = new Rect(center - new Vector2(rim, rim), center + new Vector2(rim, rim));
        CasinoLights.BulbChase(drawList, rect, rim, scale, phase, CasinoLights.BulbPitch, CasinoColors.Money,
            CasinoColors.LightA, lit);
    }

    private void TickPegs()
    {
        var spinning = playback.Stage is WheelStage.Spinning or WheelStage.Locking;
        var segment = (int)MathF.Floor(playback.Angle / WheelChoreography.SegmentSpan);
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

        pointerKick = 1f;
        CasinoSfx.Play(UiSound.WheelTick);
    }

    private void DrawHub(ImDrawListPtr drawList, CasinoRoomSnapshotDto snapshot, long remainingMs)
    {
        if (playback.Stage == WheelStage.Settling && WheelRules.IsSegment(playback.Segment))
        {
            var spot = WheelRules.SpotAt(playback.Segment);
            Typography.DrawCentered(drawList, ringCenter, MultiplierLabel(spot), WheelRingArt.SpotColors[spot],
                TextStyles.Title2);
            return;
        }

        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            var seconds = (int)((Math.Max(0, remainingMs) + 999) / 1000);
            Typography.DrawCentered(drawList, ringCenter, GameNumber.Label(seconds), CasinoColors.InkTitle,
                TextStyles.Title3);
            return;
        }

        drawList.AddCircleFilled(ringCenter, ringRadius * 0.20f * (0.55f + 0.25f * Pulse.Wave(Pulse.Breath)),
            ImGui.GetColorU32(CasinoColors.Money with { W = 0.55f }), 24);
    }

    private string MultiplierLabel(int spot) => multipliers[spot].Get(L.Casino.WheelMultiplier,
        WheelRules.Multipliers[spot]);

    private void DrawRecentRail(ImDrawListPtr drawList, CasinoWheelRoomStateDto? board, float left, float top,
        float width, float scale)
    {
        var recent = board?.Recent;
        if (recent is null || recent.Length == 0)
        {
            return;
        }

        var height = RailHeight * scale;
        var x = left;
        var limit = Math.Min(recent.Length, RecentShown);
        for (var index = 0; index < limit; index++)
        {
            var spot = recent[index];
            if (!WheelRules.IsSpot(spot))
            {
                continue;
            }

            var text = MultiplierLabel(spot);
            var chipWidth = Typography.Measure(text, TextStyles.Caption1).X + height * 0.6f;
            if (x + chipWidth > left + width)
            {
                break;
            }

            var min = new Vector2(x, top);
            var max = new Vector2(x + chipWidth, top + height);
            var color = WheelRingArt.SpotColors[spot];
            var newest = index == 0;
            Squircle.Fill(drawList, min, max, height * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(color, newest ? 0.32f : 0.14f)));
            if (newest)
            {
                Squircle.Stroke(drawList, min, max, height * 0.5f, ImGui.GetColorU32(color), MathF.Max(1f, scale));
            }

            Typography.DrawCentered(drawList, (min + max) * 0.5f, text, Palette.WithAlpha(color, newest ? 1f : 0.75f),
                TextStyles.Caption1);
            x = max.X + RailChipGap * scale;
        }
    }

    private void DrawPodiums(ImDrawListPtr drawList, AppSkin ui, CasinoWheelRoomStateDto? board, Rect row,
        bool selectable, float scale)
    {
        var gap = PodiumGap * scale;
        var width = (row.Width - gap * (WheelRules.SpotCount - 1)) / WheelRules.SpotCount;
        RefreshReturns(board);
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            var min = new Vector2(row.Min.X + spot * (width + gap), row.Min.Y);
            DrawPodium(drawList, ui, board, spot, new Rect(min, new Vector2(min.X + width, row.Max.Y)), selectable,
                scale);
        }
    }

    private void DrawPodium(ImDrawListPtr drawList, AppSkin ui, CasinoWheelRoomStateDto? board, int spot, Rect rect,
        bool selectable, float scale)
    {
        var rounding = Metrics.Radius.Md * scale;
        var color = WheelRingArt.SpotColors[spot];
        var selected = spot == selectedSpot;
        var hovered = selectable && UiInteract.Hover(rect.Min, rect.Max);
        var winning = playback.Stage == WheelStage.Settling && WheelRules.Wins(playback.Segment, spot);
        var lit = winning ? 1f : selected ? 0.6f : 0.25f;
        drawList.AddCircleFilled(new Vector2(rect.Center.X, rect.Min.Y), rect.Width * 0.6f,
            ImGui.GetColorU32(color with { W = 0.10f * lit }), 24);
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(Palette.Mix(ui.Palette.CardFill with { W = 1f }, color, 0.22f * lit + 0.08f)),
            ImGui.GetColorU32(ui.Palette.CardFill with { W = 1f }));
        Squircle.Fill(drawList, rect.Min, new Vector2(rect.Max.X, rect.Min.Y + PodiumCap * scale), rounding * 0.4f,
            ImGui.GetColorU32(color));
        if (winning || selected)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(winning ? CasinoColors.Money : color), (winning ? 2f : 1.4f) * scale);
        }

        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = PodiumPad * scale;
        var inner = rect.Width - pad;
        var centerX = rect.Center.X;
        var y = rect.Min.Y + pad;
        y = PodiumLine(drawList, MultiplierLabel(spot), centerX, y, inner, color, TextStyles.Title3);
        y = PodiumLine(drawList, NumberText.Compact(PoolOf(board, spot)), centerX, y, inner, ui.BodyInk,
            TextStyles.Caption1);
        y = PodiumLine(drawList, bettors[spot].Get(L.Casino.WheelBettors, BettorsOf(board, spot)), centerX, y, inner,
            ui.MutedInk, TextStyles.Caption2);
        PodiumLine(drawList, returns[spot], centerX, y, inner, Palette.WithAlpha(CasinoColors.Money, 0.8f),
            TextStyles.Caption2);
        var mine = spotStakes[spot];
        if (mine > 0)
        {
            ChipStack.Draw(drawList, new Vector2(centerX, rect.Min.Y - pad), mine, scale * StakeChipScale,
                CasinoColors.Money);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            selectedSpot = spot;
            inlineReason = string.Empty;
            CasinoSfx.Play(UiSound.ChipSlide);
        }
    }

    private static float PodiumLine(ImDrawListPtr drawList, string text, float centerX, float top, float width,
        Vector4 ink, in TextStyle style)
    {
        var fitted = Typography.FitText(text, width, style);
        var size = Typography.Measure(fitted, style);
        Typography.Draw(drawList, new Vector2(centerX - size.X * 0.5f, top), fitted, ink, style);
        return top + Typography.LineHeight(style);
    }

    private void RefreshReturns(CasinoWheelRoomStateDto? board)
    {
        if (ReferenceEquals(returnsLanguage, Loc.Current) && returns[0] is not null)
        {
            return;
        }

        returnsLanguage = Loc.Current;
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            var tenths = ReturnTenths(board, spot);
            returns[spot] = Loc.T(L.Strip.ReturnValue, (tenths / 10m).ToString("0.#", Loc.Culture));
        }
    }

    internal static int ReturnTenths(CasinoWheelRoomStateDto? board, int spot)
    {
        var row = SpotOf(board, spot);
        if (row is not null && row.ReturnBasisPoints > 0)
        {
            return row.ReturnBasisPoints / 10;
        }

        return (WheelRules.Multipliers[spot] + 1) * WheelRules.SegmentCounts[spot] * 1000 / WheelRules.SegmentCount;
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

    private static long PoolOf(CasinoWheelRoomStateDto? board, int spot) => SpotOf(board, spot)?.Amount ?? 0;

    private static int BettorsOf(CasinoWheelRoomStateDto? board, int spot) => SpotOf(board, spot)?.Bettors ?? 0;

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
        if (composer.Draw(ui, frame.Deck, model, frame.DeltaSeconds) != BetComposerAction.Confirm)
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

    private void DrawSeatMissing(ImDrawListPtr drawList, AppSkin ui, Rect deck, float scale)
    {
        var inset = BetComposer.Pad * scale;
        var title = Loc.T(L.Casino.CabinetNoChipsTitle);
        var label = Loc.T(L.Casino.Cashier);
        var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(deck.Min.X + inset, deck.Min.Y + inset),
            Typography.FitText(title, deck.Width - inset * 2f, TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var top = deck.Min.Y + inset + titleHeight + Metrics.Space.Sm * scale;
        var rect = new Rect(new Vector2(deck.Min.X + inset, top),
            new Vector2(deck.Max.X - inset, top + Button.LargeHeight * scale));
        if (Button.Draw(drawList, rect, label, ui.Ink))
        {
            openCashier();
        }
    }
}
