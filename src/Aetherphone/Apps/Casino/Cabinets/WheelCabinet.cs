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

    private const float PodiumGap = 6f;
    private const float PodiumCap = 3f;
    private const float BetDiscRadius = 8f;
    private const float LosingDim = 0.55f;
    private const float BannerPad = 12f;
    private const float BannerRadius = 18f;
    private const float BannerTextShare = 0.92f;
    private const float BannerMinFit = 0.7f;
    private const float RailHeight = 26f;
    private const float RailChipGap = 4f;
    private const float RimInset = 16f;
    private const float LockFlashSeconds = 0.45f;
    private const float IdleTurnRate = 0.25f;
    private const float RailPopSeconds = 0.35f;

    private readonly CasinoStore chips;
    private readonly CasinoRoomsStore rooms;
    private readonly Action leaveRoom;
    private readonly WheelRoundPlayback playback = new();
    private readonly BetComposer composer = new("##wheelBet");
    private readonly long[] spotStakes = new long[WheelRules.SpotCount];
    private readonly LabelSlot[] multipliers = new LabelSlot[WheelRules.SpotCount];
    private readonly LabelSlot[] bettors = new LabelSlot[WheelRules.SpotCount];
    private readonly WheelRecentRail recent = new();
    private readonly string[] returns = new string[WheelRules.SpotCount];

    private string inlineReason = string.Empty;
    private string resultText = string.Empty;
    private string resultKey = string.Empty;
    private LanguageInfo? resultLanguage;
    private long resultStaked;
    private long resultReturned;
    private string celebratedRoundId = string.Empty;
    private string ribbonText = string.Empty;
    private int ribbonSeconds = -1;
    private int ribbonStage = -1;
    private LanguageInfo? ribbonLanguage;
    private LanguageInfo? returnsLanguage;
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
        var board = held?.Wheel;
        var remaining = snapshot is null
            ? 0
            : room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        playback.Update(snapshot, board, remaining, frame.SnapToTruth ? WheelChoreography.SpinSeconds : delta);
        if (frame.SnapToTruth)
        {
            pointer.Reset();
        }

        pointer.Step(delta);
        recent.Sync(board?.Recent);
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
        PhaseRibbon.Draw(drawList, frame.Layout.Ribbon, RibbonLabel(snapshot.Phase, remaining), remaining,
            CasinoRoomCadence.WheelWindow(snapshot.Phase), snapshot.Occupancy,
            room.Attached ? CasinoColors.LightA : CasinoColors.InkMuted, scale);
        var podiumTop = safe.Max.Y - WheelPodiumLayout.Height(scale);
        var railTop = safe.Min.Y;
        DrawRecentRail(drawList, safe.Min.X, railTop, safe.Width, scale);
        var wheelArea = new Rect(new Vector2(safe.Min.X, railTop + (RailHeight + RailChipGap) * scale),
            new Vector2(safe.Max.X, podiumTop - PodiumGap * scale));
        DrawWheel(drawList, snapshot, wheelArea, remaining, frame.Phase, delta, scale);
        var sitting = state.Sitting;
        var betting = snapshot.Phase == CasinoRoomPhases.Open && sitting is not null && !frame.Blocked;
        DrawPodiums(drawList, ui, board, new Rect(new Vector2(safe.Min.X, podiumTop), safe.Max), betting, scale);
        DrawResultBanner(drawList, stage, snapshot, wheelArea, frame.Phase, scale);
        if (inlineReason.Length > 0)
        {
            var message = CasinoReasons.Text(inlineReason, chips.Ceiling.MaxBet);
            var height = CasinoNotice.Height(CasinoNoticeKind.Reason, string.Empty, message, safe.Width, scale);
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty, message, safe.Min.X,
                podiumTop - height - PodiumGap * scale, safe.Width, scale);
        }

        DrawDeck(stage, frame, ui, state, sitting ?? CasinoWire.NoBankroll, snapshot);
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
        float phase, float delta, float scale)
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
        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            WheelRingArt.DrawCountdown(drawList, ringCenter, radius + RimInset * 0.5f * scale,
                TurnTimerRing.Fraction(remainingMs, CasinoRoomCadence.WheelOpenSeconds), CasinoColors.LightA, scale);
        }

        WheelRingArt.DrawPointer(drawList, ringCenter, radius, scale, pointer.Deflection);
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

    private void DrawHub(ImDrawListPtr drawList, CasinoRoomSnapshotDto snapshot, long remainingMs)
    {
        if (playback.Stage == WheelStage.Settling && WheelRules.IsSegment(playback.Segment))
        {
            var spot = WheelRules.SpotAt(playback.Segment);
            Typography.DrawCentered(drawList, ringCenter, MultiplierLabel(spot), WheelRingArt.SpotTextInks[spot],
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

    private void DrawRecentRail(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var count = recent.Count;
        if (count == 0)
        {
            return;
        }

        var height = RailHeight * scale;
        var x = left;
        for (var index = 0; index < count; index++)
        {
            var spot = recent.SpotAt(index);
            var text = MultiplierLabel(spot);
            var chipWidth = Typography.Measure(text, TextStyles.FootnoteEmphasized).X + height * 0.6f;
            if (x + chipWidth > left + width)
            {
                break;
            }

            var min = new Vector2(x, top);
            var max = new Vector2(x + chipWidth, top + height);
            var color = WheelRingArt.SpotColors[spot];
            var newest = index == 0;
            var pop = newest && recent.HasLanded && playback.StageSeconds < RailPopSeconds
                ? GameJuice.PopIn(playback.StageSeconds / RailPopSeconds)
                : 1f;
            var chipCenter = (min + max) * 0.5f;
            var half = (max - min) * 0.5f * pop;
            Squircle.Fill(drawList, chipCenter - half, chipCenter + half, half.Y,
                ImGui.GetColorU32(Palette.WithAlpha(color, newest ? 0.32f : 0.14f)));
            if (newest)
            {
                Squircle.Stroke(drawList, chipCenter - half, chipCenter + half, half.Y, ImGui.GetColorU32(color),
                    MathF.Max(1f, scale));
            }

            Typography.DrawCentered(drawList, chipCenter, text, WheelRingArt.SpotTextInks[spot],
                TextStyles.FootnoteEmphasized);
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
        var settling = playback.Stage == WheelStage.Settling && WheelRules.IsSegment(playback.Segment);
        var winning = settling && WheelRules.Wins(playback.Segment, spot);
        var mine = spotStakes[spot];
        var losing = settling && !winning && mine > 0;
        var lit = winning ? 1f : selected && !settling ? 0.6f : 0.25f;
        drawList.AddCircleFilled(new Vector2(rect.Center.X, rect.Min.Y), rect.Width * 0.6f,
            ImGui.GetColorU32(color with { W = 0.10f * lit }), 24);
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(Palette.Mix(ui.Palette.CardFill with { W = 1f }, color, 0.22f * lit + 0.08f)),
            ImGui.GetColorU32(ui.Palette.CardFill with { W = 1f }));
        Squircle.Fill(drawList, rect.Min, new Vector2(rect.Max.X, rect.Min.Y + PodiumCap * scale), rounding * 0.4f,
            ImGui.GetColorU32(color));
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var rows = WheelPodiumLayout.Compute(rect, scale);
        PodiumText(drawList, rows.Header, MultiplierLabel(spot), WheelRingArt.SpotTextInks[spot], TextStyles.Title3);
        DrawPodiumBet(drawList, rows.Bet, mine, winning, scale);
        DrawCrowd(drawList, board, spot, rows.Crowd, scale);
        PodiumOptionalText(drawList, rows.Back, returns[spot], StageInks.Strong, TextStyles.Footnote);
        if (losing)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, LosingDim)));
        }

        if (winning || (selected && !settling))
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(winning ? CasinoColors.Money : color), (winning ? 2f : 1.4f) * scale);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            selectedSpot = spot;
            inlineReason = string.Empty;
            CasinoSfx.Play(UiSound.ChipSlide);
        }
    }

    private static void DrawPodiumBet(ImDrawListPtr drawList, Rect row, long mine, bool winning, float scale)
    {
        if (mine <= 0)
        {
            return;
        }

        var radius = MathF.Min(BetDiscRadius * scale, row.Height * 0.5f);
        var gap = Metrics.Space.Xxs * scale;
        var style = FitStyle(TextStyles.FootnoteEmphasized, row.Height);
        var available = MathF.Max(1f, row.Width - radius * 2f - gap);
        var fitted = Typography.FitText(NumberText.Compact(mine), available, style);
        var size = Typography.Measure(fitted, style);
        var left = row.Center.X - (radius * 2f + gap + size.X) * 0.5f;
        ChipStack.DrawDisc(drawList, new Vector2(left + radius, row.Center.Y), radius, TopDenomination(mine));
        Typography.Draw(drawList, new Vector2(left + radius * 2f + gap, row.Center.Y - size.Y * 0.5f), fitted,
            winning ? CasinoColors.MoneyHighlight : CasinoColors.Money, style);
    }

    private static long TopDenomination(long amount)
    {
        var denominations = ChipStack.Denominations;
        for (var index = 0; index < denominations.Length; index++)
        {
            if (denominations[index] <= amount)
            {
                return denominations[index];
            }
        }

        return denominations[denominations.Length - 1];
    }

    private static void PodiumText(ImDrawListPtr drawList, Rect row, string text, Vector4 ink, in TextStyle style)
    {
        var fittedStyle = FitStyle(style, row.Height);
        var fitted = Typography.FitText(text, row.Width, fittedStyle);
        Typography.DrawCentered(drawList, row.Center, fitted, ink, fittedStyle);
    }

    private static void PodiumOptionalText(ImDrawListPtr drawList, Rect row, string text, Vector4 ink,
        in TextStyle style)
    {
        var fittedStyle = FitStyle(style, row.Height);
        if (Typography.Measure(text, fittedStyle).X > row.Width)
        {
            return;
        }

        Typography.DrawCentered(drawList, row.Center, text, ink, fittedStyle);
    }

    private static TextStyle FitStyle(in TextStyle style, float height)
    {
        var lineHeight = Typography.LineHeight(style);
        if (lineHeight <= height || lineHeight <= 0f)
        {
            return style;
        }

        return style with { Scale = style.Scale * height / lineHeight };
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
        var style = won ? TextStyles.Title2 : TextStyles.Headline;
        var pad = BannerPad * scale;
        var maxWidth = MathF.Max(1f, area.Width * BannerTextShare - pad * 2f);
        var measured = Typography.Measure(text, style).X;
        if (measured > maxWidth)
        {
            style = style with { Scale = style.Scale * MathF.Max(BannerMinFit, maxWidth / measured) };
        }

        var fitted = Typography.FitText(text, maxWidth, style);
        var size = Typography.Measure(fitted, style);
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

        Typography.DrawCentered(drawList, center, fitted, won ? CasinoColors.Money : StageInks.Strong, style);
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

    private void DrawCrowd(ImDrawListPtr drawList, CasinoWheelRoomStateDto? board, int spot, Rect row, float scale)
    {
        var count = BettorsOf(board, spot);
        WheelCrowdArt.Draw(drawList, row.Center, row.Width, spot, count, spotStakes[spot] > 0, scale);
        if (count > 0 && UiInteract.Hover(row.Min, row.Max))
        {
            HoverTooltip.Show(row, bettors[spot].Get(L.Casino.WheelBettors, count));
        }
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
            returns[spot] = Loc.T(L.Strip.PaysBackShort, (tenths / 10m).ToString("0.#", Loc.Culture));
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
