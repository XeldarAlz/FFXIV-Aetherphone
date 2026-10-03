using System.Globalization;
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
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class WheelCabinet
{
    private const float PadX = 16f;
    private const float StatusRowHeight = 22f;
    private const float MaxRingRadius = 108f;
    private const float BannerHeight = 54f;
    private const float SpotCardHeight = 92f;
    private const float SpotGap = 6f;
    private const float FieldHeight = 40f;
    private const float PillHeight = 46f;
    private const float LockFlashSeconds = 0.45f;
    private const float RecentChipHeight = 20f;
    private const float RecentChipGap = 5f;
    private const int RecentShown = 8;
    private const int AmountBufferLength = 4;

    private static readonly Vector4 Gold = new(1f, 0.84f, 0.42f, 1f);

    private static readonly Vector4[] ConfettiPalette =
    {
        new(1.00f, 0.84f, 0.42f, 1f),
        new(1.00f, 0.95f, 0.75f, 1f),
        new(0.55f, 0.92f, 0.88f, 1f),
        new(0.80f, 0.58f, 0.98f, 1f),
    };

    private readonly CasinoStore chips;
    private readonly CasinoRoomsStore rooms;
    private readonly Action openCashier;
    private readonly Action leaveRoom;
    private readonly WheelRoundPlayback playback = new();
    private readonly ParticleSystem particles = new(192);
    private readonly long[] spotStakes = new long[WheelRules.SpotCount];

    private RollingValue winRoll;
    private string amountBuffer = string.Empty;
    private string inlineReason = string.Empty;
    private string celebratedRoundId = string.Empty;
    private int selectedSpot;
    private long settledReturn;
    private bool entered;
    private Vector2 ringCenter;

    public WheelCabinet(CasinoStore chips, CasinoRoomsStore rooms, Action openCashier, Action leaveRoom)
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
        amountBuffer = WheelRules.MinStakePerSpot.ToString(Loc.Culture);
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
        particles.Clear();
        inlineReason = string.Empty;
        celebratedRoundId = string.Empty;
        settledReturn = 0;
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
        var board = held?.Wheel;
        var remaining = snapshot is null
            ? 0
            : room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        playback.Update(snapshot, board, remaining, delta);
        particles.Update(delta);

        var drawList = ImGui.GetWindowDrawList();
        var pad = PadX * scale;
        var left = body.Min.X + pad;
        var width = body.Width - pad * 2f;

        var closedReason = room.ClosedReason;
        if (closedReason.Length > 0)
        {
            DrawClosed(drawList, ui, closedReason, left, body.Min.Y + Metrics.Space.Lg * scale, width, scale);
            return;
        }

        var state = chips.State;
        if (state is null || snapshot is null)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        CollectStakes(snapshot);
        CelebrateSettledRound(snapshot, scale);

        var y = body.Min.Y + Metrics.Space.Xs * scale;
        y = DrawStatusRow(drawList, ui, snapshot, room.Attached, left, y, width, scale);
        y = DrawWheel(drawList, ui, snapshot, remaining, left, y, width, scale);
        y = DrawBanner(drawList, ui, snapshot, remaining, left, y, width, scale, delta);
        y = DrawRecentRail(drawList, ui, board, left, y, width, scale);
        y += Metrics.Space.Xs * scale;

        var sitting = state.Sitting;
        var seated = sitting is not null;
        y = DrawSpotRow(drawList, ui, board, snapshot, seated, left, y, width, scale);
        if (!seated)
        {
            DrawSeatMissing(drawList, ui, left, y + Metrics.Space.Sm * scale, width, scale);
            particles.Draw(drawList, scale);
            return;
        }

        DrawComposer(drawList, ui, state, sitting!, snapshot, left, y + Metrics.Space.Sm * scale, width, scale);
        particles.Draw(drawList, scale);
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

    private void CollectStakes(CasinoRoomSnapshotDto snapshot)
    {
        Array.Clear(spotStakes);
        var mine = rooms.WheelBetsFor(snapshot.RoomId, snapshot.RoundIndex);
        var bets = mine?.Bets;
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

    private long StakedOn(int spot)
    {
        return WheelRules.IsSpot(spot) ? spotStakes[spot] : 0;
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

    private void CelebrateSettledRound(CasinoRoomSnapshotDto snapshot, float scale)
    {
        var roundKey = WheelRoundPlayback.RoundKeyOf(snapshot);
        if (playback.Stage != WheelStage.Settling
            || string.Equals(celebratedRoundId, roundKey, StringComparison.Ordinal))
        {
            return;
        }

        celebratedRoundId = roundKey;
        settledReturn = ReturnOn(playback.Segment);
        winRoll.Snap(0);
        var multiplier = WheelRules.MultiplierOf(WheelRules.SpotAt(playback.Segment));
        if (playback.SpunLive)
        {
            SoundSettledRound(multiplier);
        }

        if (settledReturn <= 0)
        {
            return;
        }

        var origin = ringCenter;
        if (multiplier >= 10)
        {
            particles.Confetti(origin, 90, ConfettiPalette, 330f * scale, 5f, 1.6f);
            particles.Sparkle(origin, 24, Gold, 190f * scale, 4f, 1.0f);
            return;
        }

        if (multiplier >= 3)
        {
            particles.Confetti(origin, 40, ConfettiPalette, 250f * scale, 4f, 1.2f);
            return;
        }

        particles.Sparkle(origin, 12, Gold, 140f * scale, 3f, 0.8f);
    }

    private void SoundSettledRound(int multiplier)
    {
        if (settledReturn > 0)
        {
            UiFeedback.Play(multiplier >= 10 ? UiSound.GamePowerUp : UiSound.GameWin);
            return;
        }

        if (StakedThisRound() > 0)
        {
            UiFeedback.Play(UiSound.GameWrong);
        }
    }

    private float DrawStatusRow(ImDrawListPtr drawList, AppSkin ui, CasinoRoomSnapshotDto snapshot, bool attached,
        float left, float y, float width, float scale)
    {
        var height = StatusRowHeight * scale;
        var rail = Loc.T(L.Casino.WheelAtTheRail, GameNumber.Label(snapshot.Occupancy));
        Typography.Draw(drawList, new Vector2(left, y + 4f * scale), rail, ui.MutedInk, TextStyles.Caption1);

        if (attached)
        {
            return y + height;
        }

        var label = Loc.T(L.Casino.WheelReconnecting);
        var labelSize = Typography.Measure(label, TextStyles.Caption2);
        var chipMax = new Vector2(left + width, y + height - 2f * scale);
        var chipMin = new Vector2(chipMax.X - labelSize.X - 18f * scale, y);
        var chipRounding = (chipMax.Y - chipMin.Y) * 0.5f;
        Squircle.Fill(drawList, chipMin, chipMax, chipRounding, ImGui.GetColorU32(ui.FieldSurface));
        var dotCenter = new Vector2(chipMin.X + 8f * scale, (chipMin.Y + chipMax.Y) * 0.5f);
        drawList.AddCircleFilled(dotCenter, 2.6f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f + 0.45f * Pulse.Wave(Pulse.Breath))), 12);
        Typography.Draw(drawList, new Vector2(dotCenter.X + 6f * scale, chipMin.Y + 3f * scale), label,
            ui.MutedInk, TextStyles.Caption2);
        return y + height;
    }

    private float DrawWheel(ImDrawListPtr drawList, AppSkin ui, CasinoRoomSnapshotDto snapshot, long remainingMs,
        float left, float y, float width, float scale)
    {
        var radius = MathF.Min(width * 0.40f, MaxRingRadius * scale);
        var center = new Vector2(left + width * 0.5f, y + radius + 16f * scale);
        ringCenter = center;

        var flash = playback.Stage == WheelStage.Locking && playback.StageSeconds < LockFlashSeconds
            ? 1f - playback.StageSeconds / LockFlashSeconds
            : 0f;
        var glow = playback.Stage == WheelStage.Settling
            ? 0.55f + 0.45f * Pulse.Wave(Pulse.Breath)
            : 0f;
        var highlight = playback.Stage == WheelStage.Settling ? playback.Segment : -1;

        WheelRingArt.Draw(drawList, center, radius, playback.Angle, highlight, glow, scale);
        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            WheelRingArt.DrawCountdown(drawList, center, radius + 13f * scale, CountdownFraction(remainingMs),
                ui.Accent, scale);
        }
        else if (playback.Stage == WheelStage.Settling)
        {
            WheelRingArt.DrawCountdown(drawList, center, radius + 13f * scale,
                SettleFraction(NextOpenMilliseconds(snapshot.Phase, remainingMs)), Gold, scale);
        }
        else if (flash > 0f)
        {
            drawList.AddCircle(center, radius + 13f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(Gold, flash)), 96, 4f * scale);
        }

        WheelRingArt.DrawPointer(drawList, center, radius, scale);
        DrawHub(drawList, ui, center, radius, snapshot, remainingMs, scale);
        return center.Y + radius + 22f * scale;
    }

    private void DrawHub(ImDrawListPtr drawList, AppSkin ui, Vector2 center, float radius,
        CasinoRoomSnapshotDto snapshot, long remainingMs, float scale)
    {
        if (playback.Stage == WheelStage.Settling && WheelRules.IsSegment(playback.Segment))
        {
            var spot = WheelRules.SpotAt(playback.Segment);
            var text = Loc.T(L.Casino.WheelMultiplier, GameNumber.Label(WheelRules.Multipliers[spot]));
            Typography.DrawCentered(drawList, center, text, WheelRingArt.SpotColors[spot], TextStyles.Title2);
            return;
        }

        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            var seconds = (int)((remainingMs + 999) / 1000);
            Typography.DrawCentered(drawList, center, TimeText.Duration(seconds), ui.TitleInk, TextStyles.Title3);
            return;
        }

        var span = radius * 0.20f;
        drawList.AddCircleFilled(center, span * (0.55f + 0.25f * Pulse.Wave(Pulse.Breath)),
            ImGui.GetColorU32(Palette.WithAlpha(Gold, 0.55f)), 24);
    }

    private static float CountdownFraction(long remainingMs)
    {
        if (remainingMs <= 0)
        {
            return 0f;
        }

        var seconds = remainingMs / 1000f;
        return seconds >= 60f ? 1f : seconds / 60f;
    }

    private static long NextOpenMilliseconds(int phase, long remainingMs)
    {
        return phase == CasinoRoomPhases.Result
            ? remainingMs
            : remainingMs + CasinoRoomCadence.WheelResultSeconds * 1000L;
    }

    private static float SettleFraction(long nextOpenMs)
    {
        if (nextOpenMs <= 0)
        {
            return 0f;
        }

        return nextOpenMs / (CasinoRoomCadence.WheelResultSeconds * 1000f);
    }

    private float DrawBanner(ImDrawListPtr drawList, AppSkin ui, CasinoRoomSnapshotDto snapshot, long remainingMs,
        float left, float y, float width, float scale, float delta)
    {
        var center = new Vector2(left + width * 0.5f, y + BannerHeight * scale * 0.4f);
        if (playback.Stage == WheelStage.Settling)
        {
            winRoll.Update((int)Math.Min(settledReturn, int.MaxValue), delta);
            if (settledReturn > 0)
            {
                var amount = "+" + NumberText.Group((long)winRoll.Display);
                Typography.DrawCentered(drawList, center, Loc.T(L.Casino.WheelYouWon, amount), Gold,
                    TextStyles.Title2.Scale * winRoll.PopScale, TextStyles.Title2.Weight);
            }
            else
            {
                var landed = Loc.T(L.Casino.WheelMultiplier,
                    GameNumber.Label(WheelRules.MultiplierOf(WheelRules.SpotAt(playback.Segment))));
                Typography.DrawCentered(drawList, center, Loc.T(L.Casino.WheelLanded, landed), ui.MutedInk,
                    TextStyles.Subheadline);
            }

            var nextSeconds = (int)((NextOpenMilliseconds(snapshot.Phase, remainingMs) + 999) / 1000);
            Typography.DrawCentered(drawList, new Vector2(center.X, y + BannerHeight * scale * 0.82f),
                Loc.T(L.Casino.RoomNextIn, TimeText.Duration(nextSeconds)), ui.MutedInk, TextStyles.Caption1);
            return y + BannerHeight * scale;
        }

        if (snapshot.Phase == CasinoRoomPhases.Open)
        {
            var seconds = (int)((remainingMs + 999) / 1000);
            var window = CasinoRoomCadence.WheelWindow(snapshot.Phase);
            var urgent = TurnTimerRing.IsUrgent(remainingMs, window);
            var ringRadius = 17f * scale;
            var ringCenter = new Vector2(left + width * 0.5f - 74f * scale, center.Y);
            TurnTimerRing.Draw(drawList, ringCenter, ringRadius, remainingMs, window,
                urgent ? Gold : ui.Palette.Accent, scale);
            Typography.DrawCentered(drawList, ringCenter, GameNumber.Label(seconds),
                urgent ? Gold : ui.TitleInk, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(ringCenter.X + ringRadius + 12f * scale,
                    center.Y - 10f * scale), Loc.T(L.Casino.WheelBetsCloseIn, TimeText.Duration(seconds)),
                urgent ? Gold : ui.TitleInk, TextStyles.Title3);
            return y + BannerHeight * scale;
        }

        var message = playback.Stage == WheelStage.Locking
            ? Loc.T(L.Casino.WheelBetsClosed)
            : Loc.T(L.Casino.WheelSpinning);
        Typography.DrawCentered(drawList, center, message, Gold, TextStyles.Title3);
        return y + BannerHeight * scale;
    }

    private float DrawSpotRow(ImDrawListPtr drawList, AppSkin ui, CasinoWheelRoomStateDto? board,
        CasinoRoomSnapshotDto snapshot, bool seated, float left, float y, float width, float scale)
    {
        var gap = SpotGap * scale;
        var cardWidth = (width - gap * (WheelRules.SpotCount - 1)) / WheelRules.SpotCount;
        var cardHeight = SpotCardHeight * scale;
        var betting = snapshot.Phase == CasinoRoomPhases.Open;
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            var min = new Vector2(left + spot * (cardWidth + gap), y);
            var max = new Vector2(min.X + cardWidth, y + cardHeight);
            DrawSpotCard(drawList, ui, board, spot, min, max, seated && betting, scale);
        }

        return y + cardHeight;
    }

    private float DrawRecentRail(ImDrawListPtr drawList, AppSkin ui, CasinoWheelRoomStateDto? board, float left,
        float y, float width, float scale)
    {
        var recent = board?.Recent;
        if (recent is null || recent.Length == 0)
        {
            return y;
        }

        var label = Loc.T(L.Casino.WheelRecentHeading);
        var labelSize = Typography.Measure(label, TextStyles.Caption2);
        Typography.Draw(drawList, new Vector2(left, y), label, ui.MutedInk, TextStyles.Caption2);

        var chipHeight = RecentChipHeight * scale;
        var chipY = y + labelSize.Y + 4f * scale;
        var chipX = left;
        var limit = recent.Length < RecentShown ? recent.Length : RecentShown;
        for (var index = 0; index < limit; index++)
        {
            var spot = recent[index];
            if (!WheelRules.IsSpot(spot))
            {
                continue;
            }

            var text = Loc.T(L.Casino.WheelMultiplier, GameNumber.Label(WheelRules.Multipliers[spot]));
            var textSize = Typography.Measure(text, TextStyles.Caption1);
            var chipWidth = textSize.X + 14f * scale;
            if (chipX + chipWidth > left + width)
            {
                break;
            }

            var min = new Vector2(chipX, chipY);
            var max = new Vector2(chipX + chipWidth, chipY + chipHeight);
            var color = WheelRingArt.SpotColors[spot];
            var newest = index == 0;
            Squircle.Fill(drawList, min, max, chipHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(color, newest ? 0.30f : 0.14f)));
            if (newest)
            {
                Squircle.Stroke(drawList, min, max, chipHeight * 0.5f,
                    ImGui.GetColorU32(Palette.WithAlpha(color, 0.85f)), 1f * scale);
            }

            Typography.DrawCentered(drawList, (min + max) * 0.5f, text,
                Palette.WithAlpha(color, newest ? 1f : 0.75f), TextStyles.Caption1);
            chipX = max.X + RecentChipGap * scale;
        }

        return chipY + chipHeight;
    }

    private void DrawSpotCard(ImDrawListPtr drawList, AppSkin ui, CasinoWheelRoomStateDto? board, int spot,
        Vector2 min, Vector2 max, bool selectable, float scale)
    {
        var rounding = Metrics.Radius.Sm * scale;
        var color = WheelRingArt.SpotColors[spot];
        var selected = spot == selectedSpot;
        var hovered = selectable && UiInteract.Hover(min, max);
        var winning = playback.Stage == WheelStage.Settling && WheelRules.Wins(playback.Segment, spot);

        Elevation.Card(drawList, min, max, rounding, scale, selected || winning ? 1f : 0.65f);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.FieldSurface));
        var capMax = new Vector2(max.X, min.Y + 4f * scale);
        Squircle.Fill(drawList, min, capMax, rounding * 0.5f, ImGui.GetColorU32(color));
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (winning)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(Gold, 0.16f)));
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Gold), 2f * scale);
        }
        else if (selected)
        {
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.9f)),
                1.6f * scale);
        }

        var centerX = (min.X + max.X) * 0.5f;
        var multiplier = Loc.T(L.Casino.WheelMultiplier, GameNumber.Label(WheelRules.Multipliers[spot]));
        Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 20f * scale), multiplier, color,
            TextStyles.Title3);

        var row = BoardFor(board, spot);
        Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 42f * scale),
            NumberText.Group(row.Amount), ui.BodyInk, TextStyles.Subheadline);
        Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 58f * scale),
            Loc.T(L.Casino.WheelBettors, GameNumber.Label(row.Bettors)), ui.MutedInk, TextStyles.Footnote);

        var mine = spotStakes[spot];
        if (mine > 0)
        {
            Typography.DrawCentered(drawList, new Vector2(centerX, min.Y + 68f * scale),
                Loc.T(L.Casino.WheelYours, NumberText.Group(mine)), ui.Accent, TextStyles.Caption2);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            selectedSpot = spot;
            inlineReason = string.Empty;
        }
    }

    private static CasinoWheelSpotDto BoardFor(CasinoWheelRoomStateDto? board, int spot)
    {
        var spots = board?.Spots;
        if (spots is null)
        {
            return new CasinoWheelSpotDto(spot);
        }

        for (var index = 0; index < spots.Length; index++)
        {
            if (spots[index].Spot == spot)
            {
                return spots[index];
            }
        }

        return new CasinoWheelSpotDto(spot);
    }

    private void DrawComposer(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state, CasinoSittingDto sitting,
        CasinoRoomSnapshotDto snapshot, float left, float y, float width, float scale)
    {
        var staked = StakedThisRound();
        if (staked == 0)
        {
            y = DrawFinalityCard(drawList, ui, left, y, width, scale);
            y += Metrics.Space.Sm * scale;
        }

        if (inlineReason.Length > 0)
        {
            y = DrawReasonCard(drawList, ui, Loc.T(CasinoReasons.MessageFor(inlineReason)), left, y, width, scale);
            y += Metrics.Space.Sm * scale;
        }

        var onSelected = StakedOn(selectedSpot);
        var ceiling = WheelRules.HeadroomOn(staked, onSelected);
        if (sitting.Stack < ceiling)
        {
            ceiling = sitting.Stack;
        }

        if (ceiling < WheelRules.MinStakePerSpot)
        {
            var note = staked >= WheelRules.MaxStakePerRound
                ? Loc.T(L.Casino.WheelSpinFull, NumberText.Group(WheelRules.MaxStakePerRound))
                : (onSelected >= WheelRules.MaxStakePerSpot
                    ? Loc.T(L.Casino.WheelSpotFull, NumberText.Group(WheelRules.MaxStakePerSpot))
                    : Loc.T(L.Casino.SlotsLowStack));
            var block = Typography.MeasureWrappedBlock(note, TextStyles.Footnote, width);
            Typography.DrawWrappedLeft(new Vector2(left, y), note, ui.MutedInk, TextStyles.Footnote, width);
            if (staked >= WheelRules.MaxStakePerRound || onSelected >= WheelRules.MaxStakePerSpot)
            {
                return;
            }

            var cashierY = y + block.Y + Metrics.Space.Sm * scale;
            var cashierRect = new Rect(new Vector2(left + width * 0.25f, cashierY),
                new Vector2(left + width * 0.75f, cashierY + 38f * scale));
            if (ui.GhostButton(cashierRect, Loc.T(L.Casino.Cashier)))
            {
                openCashier();
            }

            return;
        }

        var heading = onSelected > 0
            ? Loc.T(L.Casino.WheelAddToSpot, NumberText.Group(onSelected))
            : (staked > 0
                ? Loc.T(L.Casino.WheelOnThisSpin, NumberText.Group(staked))
                : Loc.T(L.Casino.WheelBetHeading));
        Typography.Draw(drawList, new Vector2(left, y), heading, ui.MutedInk, TextStyles.FootnoteEmphasized);
        var bounds = Loc.T(L.Casino.WheelBetBounds, NumberText.Group(WheelRules.MinStakePerSpot),
            NumberText.Group(ceiling));
        var boundsSize = Typography.Measure(bounds, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(left + width - boundsSize.X, y), bounds, ui.MutedInk,
            TextStyles.Caption1);
        y += 20f * scale;

        var fieldWidth = width * 0.34f;
        var fieldMin = new Vector2(left, y);
        var fieldMax = new Vector2(left + fieldWidth, y + FieldHeight * scale);
        Squircle.Fill(drawList, fieldMin, fieldMax, Metrics.Radius.Sm * scale, ImGui.GetColorU32(ui.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 10f * scale,
            (fieldMin.Y + fieldMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldWidth - 20f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputText("##wheelAmount", ref amountBuffer, AmountBufferLength,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }

        var amount = ParseAmount();
        var clamped = WheelRules.ClampOn(amount, staked, onSelected, sitting.Stack);
        var blocked = state.StakesPaused || state.Draining || snapshot.Phase != CasinoRoomPhases.Open;
        var canPlace = !blocked && !rooms.StakeInFlight && clamped > 0;
        var multiplier = Loc.T(L.Casino.WheelMultiplier, GameNumber.Label(WheelRules.Multipliers[selectedSpot]));
        var label = canPlace
            ? Loc.T(L.Casino.WheelPlaceOn, NumberText.Group(clamped), multiplier)
            : Loc.T(L.Casino.WheelPlace);
        var pillMin = new Vector2(fieldMax.X + 8f * scale, y + (FieldHeight - PillHeight) * 0.5f * scale);
        var pillRect = new Rect(pillMin, new Vector2(left + width, pillMin.Y + PillHeight * scale));
        if (ui.ActionPill(pillRect, label, canPlace, TextStyles.Subheadline))
        {
            inlineReason = string.Empty;
            rooms.PlaceWheelBet(selectedSpot, clamped);
            UiFeedback.Play(UiSound.CasinoChips);
        }

        y += MathF.Max(FieldHeight, PillHeight) * scale + Metrics.Space.Xs * scale;

        if (state.StakesPaused || state.Draining)
        {
            Typography.DrawWrappedLeft(new Vector2(left, y),
                Loc.T(state.StakesPaused ? L.Casino.PausedTitle : L.Casino.DrainingTitle), ui.MutedInk,
                TextStyles.Caption2, width);
            return;
        }

        if (staked == 0)
        {
            Typography.DrawWrappedLeft(new Vector2(left, y),
                Loc.T(L.Casino.WheelSpinCap, NumberText.Group(WheelRules.MaxStakePerRound)),
                ui.MutedInk, TextStyles.Caption2, width);
            return;
        }

        var final = Loc.T(L.Casino.WheelFinalShort);
        Typography.Draw(drawList, new Vector2(left, y), final, ui.MutedInk, TextStyles.Caption2);
        y += Typography.Measure(final, TextStyles.Caption2).Y + 2f * scale;
        Typography.DrawWrappedLeft(new Vector2(left, y), Loc.T(L.Casino.WheelSpreadHint), ui.MutedInk,
            TextStyles.Caption2, width);
    }

    private long ParseAmount()
    {
        return long.TryParse(amountBuffer, NumberStyles.Integer, Loc.Culture, out var value) ? value : 0;
    }

    private static float DrawFinalityCard(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width,
        float scale)
    {
        var pad = 12f * scale;
        var title = Loc.T(L.Casino.WheelFinalTitle);
        var body = Loc.T(L.Casino.WheelFinalBody);
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        var block = Typography.MeasureWrappedBlock(body, TextStyles.Caption1, width - pad * 2f);
        var height = titleSize.Y + block.Y + pad * 2f + 4f * scale;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Squircle.Stroke(drawList, min, max, 16f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), title, ui.TitleInk,
            TextStyles.FootnoteEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad + titleSize.Y + 4f * scale), body,
            ui.MutedInk, TextStyles.Caption1, width - pad * 2f);
        return max.Y;
    }

    private static float DrawReasonCard(ImDrawListPtr drawList, AppSkin ui, string message, float left, float y,
        float width, float scale)
    {
        var pad = 12f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, width - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Squircle.Stroke(drawList, min, max, 16f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), message, ui.TitleInk,
            TextStyles.Footnote, width - pad * 2f);
        return max.Y;
    }

    private void DrawClosed(ImDrawListPtr drawList, AppSkin ui, string reason, float left, float y, float width,
        float scale)
    {
        var pad = 14f * scale;
        var title = Loc.T(L.Casino.WheelClosedTitle);
        var hint = Loc.T(CasinoReasons.TryMessage(reason, out var known) ? known : L.Casino.WheelClosedHint);
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var block = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - pad * 2f);
        var height = titleSize.Y + block.Y + pad * 2f + 6f * scale;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad + titleSize.Y + 6f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - pad * 2f);

        var pillY = max.Y + Metrics.Space.Md * scale;
        var pillRect = new Rect(new Vector2(left + width * 0.2f, pillY),
            new Vector2(left + width * 0.8f, pillY + 44f * scale));
        if (AppSkin.PillButton(pillRect, Loc.T(L.Casino.WheelBackToFloor), true, true, ui.Theme))
        {
            leaveRoom();
        }
    }

    private void DrawSeatMissing(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width, float scale)
    {
        var pad = 14f * scale;
        var title = Loc.T(L.Casino.CabinetNoChipsTitle);
        var hint = Loc.T(L.Casino.CabinetNoChipsHint);
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var block = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - pad * 2f);
        var height = titleSize.Y + block.Y + pad * 2f + 6f * scale;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad + titleSize.Y + 6f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, width - pad * 2f);

        var pillY = max.Y + Metrics.Space.Md * scale;
        var pillRect = new Rect(new Vector2(left + width * 0.2f, pillY),
            new Vector2(left + width * 0.8f, pillY + 44f * scale));
        if (AppSkin.PillButton(pillRect, Loc.T(L.Casino.Cashier), true, true, ui.Theme))
        {
            openCashier();
        }
    }
}
