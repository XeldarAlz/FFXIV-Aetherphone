using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class ScratchCabinet : ICabinetIdle
{
    public const int RunLength = 5;

    private const float RubHoldPerSecond = 0.9f;
    private const float RubDragFactor = 0.05f;
    private const float TicketMaxWidth = 300f;
    private const float KioskInset = 12f;
    private const float CellGap = 6f;
    private const float AutoPauseSeconds = 0.8f;
    private const float RubTickSeconds = 0.06f;
    private const float StatusGap = 10f;
    private const float IdleThemeSeconds = 3f;
    private const int FlakesPerTick = 3;
    private const int RevealSparkles = 6;
    private const string MalformedReason = "malformed";

    private readonly CasinoStore store;
    private readonly CasinoPlayStore play;
    private readonly Action openCashier;
    private readonly ScratchCardPlayback playback = new();
    private readonly ScratchOddsSheet oddsSheet = new();
    private readonly BetComposer composer = new("##scratchBuy");

    private int tierIndex;
    private ScratchPhase seenPhase;
    private string inlineReason = string.Empty;
    private string roundId = string.Empty;
    private string stampLabel = string.Empty;
    private Rect ticketArea;
    private float autoPause;
    private float rubTick;
    private float idleTime;
    private float stampSeconds = -1f;
    private int rasp;
    private bool stroked;

    public ScratchCabinet(CasinoStore store, CasinoPlayStore play, Action openCashier)
    {
        this.store = store;
        this.play = play;
        this.openCashier = openCashier;
        composer.Auto.Rounds = RunLength;
        composer.Auto.StopOnBonus = false;
    }

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public int CurrentTier => tierIndex;

    public long CurrentPrice => ScratchRules.Prices[tierIndex];

    public bool OddsOpen => oddsSheet.IsOpen;

    public static float DeckHeight => BetComposer.DeckHeightFor(true, true);

    public void OpenOdds()
    {
        oddsSheet.Open();
    }

    public void CloseOdds()
    {
        oddsSheet.Close();
    }

    public void Gate()
    {
        oddsSheet.Gate();
        composer.Gate();
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        oddsSheet.Draw(screen, ui, tierIndex);
        composer.DrawOverlay(screen, ui, false);
    }

    public void Enter()
    {
        inlineReason = string.Empty;
        play.RecoverPendingRound();
    }

    public void Reset()
    {
        playback.Clear();
        oddsSheet.Close();
        composer.Auto.Stop(AutoStop.Manual);
        composer.Auto.Acknowledge();
        inlineReason = string.Empty;
        roundId = string.Empty;
        seenPhase = ScratchPhase.Idle;
        autoPause = 0f;
        stampSeconds = -1f;
        stroked = false;
    }

    public void Draw(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var state = store.State;
        if (state is null)
        {
            LoadingPulse.Draw(frame.Safe.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk,
                LoadingPulse.SafeLabel());
            return;
        }

        if (frame.SnapToTruth)
        {
            playback.RevealAll();
        }

        ConsumeResults(stage, frame);
        var sitting = state.Sitting;
        var safe = frame.Safe;
        var statusHeight = Typography.LineHeight(TextStyles.Subheadline) + Button.SmallHeight * scale
            + StatusGap * 2f * scale;
        LayoutTicket(safe, safe.Max.Y - statusHeight, scale);
        DrawKiosk(drawList, stage, frame, scale);
        DrawTicket(drawList, stage, ui, frame, scale);
        var status = new Rect(new Vector2(safe.Min.X, ticketArea.Max.Y + StatusGap * scale), safe.Max);
        DrawStatus(drawList, stage, ui, state, sitting, status, scale);
        if (sitting is null)
        {
            DrawSeatMissing(drawList, ui, safe, scale);
            return;
        }

        DrawDeck(stage, frame, ui, state, sitting, scale);
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        var scale = UiScale.Current;
        idleTime += deltaSeconds;
        var tier = (int)(idleTime / IdleThemeSeconds) % ScratchRules.TierCount;
        var height = MathF.Min(rect.Height * 0.86f, rect.Width * 0.7f * (1f + ScratchTicketArt.HeaderShare));
        var width = height / (1f + ScratchTicketArt.HeaderShare);
        var ticket = new Rect(rect.Center - new Vector2(width * 0.5f, height * 0.5f),
            rect.Center + new Vector2(width * 0.5f, height * 0.5f));
        CasinoLights.BulbChase(drawList, ticket.Inset(-KioskInset * 0.5f * scale), Metrics.Radius.Grouped * scale, scale,
            idleTime, CasinoLights.BulbPitch, CasinoColors.Money, ScratchTicketArt.For(tier).Accent, 0.8f);
        ScratchTicketArt.Shell(drawList, ticket, tier, idleTime, scale);
        var grid = ScratchTicketArt.GridRect(ticket);
        var cell = CellSize(grid, scale);
        for (var cellIndex = 0; cellIndex < ScratchRules.CellCount; cellIndex++)
        {
            var cellMin = CellMin(grid, cellIndex, cell, scale);
            var shimmer = 0.5f + 0.5f * MathF.Sin(idleTime * 1.6f + cellIndex * 0.7f);
            ScratchTicketArt.Foil(drawList, cellMin, cellMin + new Vector2(cell, cell), cell * 0.18f, tier, 1f, shimmer,
                scale);
        }
    }

    private void ConsumeResults(CasinoStage stage, in CasinoStageFrame frame)
    {
        var card = play.TakeScratchResult();
        if (card is not null)
        {
            if (card.Granted && playback.Begin(card))
            {
                inlineReason = string.Empty;
                roundId = card.RoundId;
                stroked = false;
                stampSeconds = -1f;
                stage.Celebration.Clear();
                if (frame.Instant || composer.Auto.Running)
                {
                    playback.RevealAll();
                }
            }
            else
            {
                inlineReason = card.Granted
                    ? MalformedReason
                    : card.Reason.Length > 0 ? card.Reason : CasinoReasons.Unreachable;
                composer.Auto.Stop(AutoStop.Refused);
            }
        }

        if (play.TakeRoundFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
            composer.Auto.Stop(AutoStop.Refused);
        }

        if (stampSeconds >= 0f)
        {
            stampSeconds += frame.DeltaSeconds;
        }

        if (playback.TakeWinCelebration(out var prize))
        {
            var price = ScratchRules.Prices[playback.Tier];
            stage.Celebration.Celebrate(price, prize, ticketArea.Center, frame.Instant);
            stage.Particles.Emit(CasinoLights.Sparkle(UiScale.Current), ticketArea.Center, 18);
            stampLabel = CasinoMultiples.Label(ScratchRules.MultipleOf(playback.Tier, prize) * 100);
            stampSeconds = 0f;
        }

        var settledNow = seenPhase == ScratchPhase.Scratching && playback.Phase == ScratchPhase.Settled;
        seenPhase = playback.Phase;
        if (!settledNow)
        {
            return;
        }

        var stake = ScratchRules.Prices[playback.Tier];
        var payout = playback.PrizeOnceRevealed;
        stage.Settle(new CasinoBetRecord(L.Casino.GameScratch, stake, payout, roundId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        var stack = store.State?.Sitting?.Stack ?? 0;
        if (composer.Auto.Running)
        {
            composer.Auto.Settle(stake, payout, false, stake, stake, stack);
            autoPause = AutoPauseSeconds;
        }
    }

    private void LayoutTicket(Rect safe, float bottom, float scale)
    {
        var available = MathF.Max(0f, bottom - safe.Min.Y);
        var width = MathF.Min(MathF.Min(safe.Width - KioskInset * 2f * scale, TicketMaxWidth * scale),
            available / (1f + ScratchTicketArt.HeaderShare) - KioskInset * scale);
        width = MathF.Max(0f, width);
        var height = width * (1f + ScratchTicketArt.HeaderShare);
        var left = safe.Center.X - width * 0.5f;
        var top = safe.Min.Y + MathF.Max(0f, (available - height) * 0.5f);
        ticketArea = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
    }

    private int DisplayTier => playback.Phase == ScratchPhase.Idle ? tierIndex : playback.Tier;

    private void DrawKiosk(ImDrawListPtr drawList, CasinoStage stage, in CasinoStageFrame frame, float scale)
    {
        var celebrating = stage.Celebration.Active;
        var kiosk = ticketArea.Inset(-KioskInset * scale);
        var accent = ScratchTicketArt.For(DisplayTier).Accent;
        CasinoLights.Spotlight(drawList, new Vector2(kiosk.Center.X, frame.Full.Min.Y), MathF.PI * 0.5f,
            kiosk.Max.Y - frame.Full.Min.Y, 0.32f, Vector4.Lerp(CasinoColors.MoneyHighlight, accent, 0.35f), 0.06f);
        CasinoLights.BulbChase(drawList, kiosk, Metrics.Radius.Grouped * scale, scale, frame.Phase * (celebrating ? 2f : 1f),
            CasinoLights.BulbPitch, CasinoColors.Money, accent, celebrating ? 1f : 0.55f);
    }

    private void DrawTicket(ImDrawListPtr drawList, CasinoStage stage, AppSkin ui, in CasinoStageFrame frame,
        float scale)
    {
        if (ticketArea.Width <= 0f)
        {
            return;
        }

        var tier = DisplayTier;
        ScratchTicketArt.Shell(drawList, ticketArea, tier, frame.Phase, scale);
        var grid = ScratchTicketArt.GridRect(ticketArea);
        var cell = CellSize(grid, scale);
        var scratching = playback.Phase == ScratchPhase.Scratching && !frame.Blocked;
        if (scratching && UiInteract.HoverWindowOnly(grid.Min, grid.Max))
        {
            UiInteract.ReportGestureSurface();
        }

        var rubbing = scratching && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var drag = ImGui.GetIO().MouseDelta.Length();
        rubTick = MathF.Max(0f, rubTick - frame.DeltaSeconds);
        for (var cellIndex = 0; cellIndex < ScratchRules.CellCount; cellIndex++)
        {
            var cellMin = CellMin(grid, cellIndex, cell, scale);
            var cellMax = cellMin + new Vector2(cell, cell);
            var center = (cellMin + cellMax) * 0.5f;
            var rounding = cell * 0.18f;
            Squircle.Fill(drawList, cellMin, cellMax, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.FieldSurface, 0.95f)));
            if (playback.Phase == ScratchPhase.Idle)
            {
                ScratchTicketArt.Foil(drawList, cellMin, cellMax, rounding, tier, 1f, 0.5f, scale);
                continue;
            }

            var foilLeft = playback.FoilRemaining(cellIndex);
            if (foilLeft < 1f)
            {
                DrawSymbol(drawList, cellIndex, center, cell);
            }

            if (foilLeft > 0f)
            {
                var shimmer = 0.5f + 0.5f * MathF.Sin(frame.Phase * 3f + cellIndex);
                ScratchTicketArt.Foil(drawList, cellMin, cellMax, rounding, tier, foilLeft, shimmer, scale);
            }

            if (!rubbing || playback.IsRevealed(cellIndex) || !UiInteract.Hover(cellMin, cellMax))
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            stroked = true;
            playback.Rub(cellIndex, frame.DeltaSeconds * RubHoldPerSecond + drag / MathF.Max(cell, 1f) * RubDragFactor);
            if (rubTick <= 0f)
            {
                Rasp(stage, tier, scale);
                rubTick = RubTickSeconds;
            }

            if (playback.IsRevealed(cellIndex))
            {
                CasinoSfx.Play(UiSound.TileSafe);
                stage.Particles.Emit(CasinoLights.Sparkle(scale), center, RevealSparkles);
            }
        }

        if (stampSeconds >= 0f && playback.PrizeOnceRevealed > 0)
        {
            ScratchTicketArt.Stamp(drawList, ticketArea, stampLabel, stampSeconds, scale);
        }
    }

    private void Rasp(CasinoStage stage, int tier, float scale)
    {
        var finger = ImGui.GetIO().MousePos;
        var theme = ScratchTicketArt.For(tier);
        stage.Particles.Emit(FoilFlake(theme, scale), finger, FlakesPerTick);
        stage.Particles.Emit(CasinoLights.Sparkle(scale), finger, 1);
        rasp = (rasp + 3) % 7;
        CasinoSfx.Pitched(UiSound.ReelTick, rasp);
    }

    private static ParticleSpec FoilFlake(in ScratchTheme theme, float scale) =>
        new(theme.FoilTop, theme.FoilBottom, 2.4f * scale, 90f * scale, 0.55f, 320f * scale, 1.4f, 9f, MathF.PI * 0.8f,
            -MathF.PI * 0.5f, ParticleShape.Shard, SizeCurve.Shrink);

    private void DrawSymbol(ImDrawListPtr drawList, int cellIndex, Vector2 center, float cell)
    {
        var matched = playback.IsMatchedCell(cellIndex);
        if (matched)
        {
            var glow = 0.14f + 0.08f * MathF.Sin((float)ImGui.GetTime() * 5f);
            drawList.AddCircleFilled(center, cell * 0.48f, ImGui.GetColorU32(CasinoColors.Money with { W = glow }), 24);
        }

        var settledLoss = playback.RevealComplete && playback.PrizeOnceRevealed <= 0;
        var dimmed = (playback.WinningSymbolOnceMatched >= 0 && !matched) || settledLoss;
        ScratchSymbolArt.Draw(drawList, playback.CellSymbol(cellIndex), center, cell * 0.3f, dimmed ? 0.45f : 1f);
    }

    private static float CellSize(Rect grid, float scale) =>
        (grid.Width - CellGap * scale * (ScratchRules.GridSide - 1)) / ScratchRules.GridSide;

    private static Vector2 CellMin(Rect grid, int cellIndex, float cell, float scale)
    {
        var column = cellIndex % ScratchRules.GridSide;
        var row = cellIndex / ScratchRules.GridSide;
        var gap = CellGap * scale;
        return new Vector2(grid.Min.X + column * (cell + gap), grid.Min.Y + row * (cell + gap));
    }

    private void DrawStatus(ImDrawListPtr drawList, CasinoStage stage, AppSkin ui, CasinoStateDto state,
        CasinoSittingDto? sitting, Rect area, float scale)
    {
        var centerX = area.Center.X;
        var top = area.Min.Y;
        if (inlineReason.Length > 0)
        {
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty,
                Loc.T(CasinoReasons.MessageFor(inlineReason)), area.Min.X, top, area.Width, scale);
            return;
        }

        if (state.StakesPaused || state.Draining)
        {
            DrawLine(drawList, Loc.T(state.StakesPaused ? L.Casino.PausedTitle : L.Casino.DrainingTitle), centerX, top,
                area.Width, ui.MutedInk);
            return;
        }

        if (playback.Phase == ScratchPhase.Scratching && stroked && !composer.Auto.Running)
        {
            if (SmallButton(drawList, ui, Loc.T(L.Casino.ScratchRevealAll), centerX, top, area.Width, scale))
            {
                playback.RevealAll();
                CasinoSfx.Play(UiSound.TileSafe);
            }

            return;
        }

        if (sitting is not null && sitting.Stack < CurrentPrice && playback.Phase != ScratchPhase.Scratching)
        {
            DrawLine(drawList, Loc.T(L.Casino.ScratchLowStack), centerX, top, area.Width, ui.MutedInk);
            return;
        }

        var bottom = top + Typography.LineHeight(TextStyles.Subheadline);
        if (playback.RevealComplete && playback.PrizeOnceRevealed <= 0)
        {
            bottom = DrawLine(drawList, Loc.T(L.Casino.ScratchNoWin), centerX, top, area.Width, CasinoColors.Loss);
        }
        else if (playback.Phase is ScratchPhase.Idle or ScratchPhase.Scratching)
        {
            bottom = DrawLine(drawList, Loc.T(L.Casino.ScratchHint), centerX, top, area.Width, ui.MutedInk);
        }

        if (!CanStartRun(stage, state, sitting))
        {
            return;
        }

        var buttonTop = bottom + StatusGap * 0.5f * scale;
        if (!SmallButton(drawList, ui, Loc.T(L.Casino.ScratchFiveInARow), centerX, buttonTop, area.Width, scale))
        {
            return;
        }

        composer.Auto.Rounds = RunLength;
        composer.Auto.Start(CurrentPrice);
        Buy(stage);
    }

    private bool CanStartRun(CasinoStage stage, CasinoStateDto state, CasinoSittingDto? sitting)
    {
        return sitting is not null && playback.Phase != ScratchPhase.Scratching && !composer.Auto.Running
            && !play.RoundInFlight && !stage.Celebration.Blocking && sitting.Stack >= CurrentPrice
            && !state.StakesPaused && !state.Draining;
    }

    private static bool SmallButton(ImDrawListPtr drawList, AppSkin ui, string label, float centerX, float top,
        float maxWidth, float scale)
    {
        var width = MathF.Min(Button.WidthFor(label, ButtonSize.Small), maxWidth);
        var rect = new Rect(new Vector2(centerX - width * 0.5f, top),
            new Vector2(centerX + width * 0.5f, top + Button.SmallHeight * scale));
        return Button.Draw(drawList, rect, label, ui.Ink, ButtonStyle.Tinted);
    }

    private static float DrawLine(ImDrawListPtr drawList, string text, float centerX, float top, float width,
        Vector4 ink)
    {
        return Typography.DrawWrappedCentered(drawList, text, TextStyles.Subheadline, ink, new Vector2(centerX, top),
            width);
    }

    private void DrawDeck(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, CasinoStateDto state,
        CasinoSittingDto sitting, float scale)
    {
        var price = CurrentPrice;
        composer.Reset(price);
        var blocked = state.StakesPaused || state.Draining || frame.Blocked;
        var scratching = playback.Phase == ScratchPhase.Scratching;
        if (autoPause > 0f)
        {
            autoPause -= frame.DeltaSeconds;
        }

        var enabled = !play.RoundInFlight && !scratching && !blocked && sitting.Stack >= price;
        var model = new BetComposerModel(price, price, sitting.Stack, L.Strip.BuyFor, enabled, AutoAvailable: true,
            FixedAmount: true, Knob: true, Repeat: stage.RepeatPressed(), Busy: play.RoundInFlight || scratching);
        var action = composer.Draw(ui, frame.Deck, model, frame.DeltaSeconds);
        DrawTierKnob(ui, composer.KnobRect, !scratching && !play.RoundInFlight && !composer.Auto.Running, scale);
        if (blocked && composer.Auto.Running)
        {
            composer.Auto.Stop(AutoStop.Manual);
        }

        var autoReady = composer.Auto.Running && autoPause <= 0f && enabled && !stage.Celebration.Blocking;
        if (action is BetComposerAction.Confirm or BetComposerAction.StartAuto || autoReady)
        {
            Buy(stage);
        }

        if (sitting.Stack < price && !scratching && !play.RoundInFlight && composer.Auto.Running)
        {
            composer.Auto.Stop(AutoStop.Chips);
        }
    }

    private void Buy(CasinoStage stage)
    {
        inlineReason = string.Empty;
        stage.Celebration.Clear();
        playback.Clear();
        seenPhase = ScratchPhase.Idle;
        stampSeconds = -1f;
        play.BuyScratch(tierIndex);
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    private void DrawTierKnob(AppSkin ui, Rect rect, bool changeable, float scale)
    {
        if (rect.Width <= 0f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var gap = Metrics.Space.Sm * scale;
        var chipWidth = (rect.Width - gap * (ScratchRules.TierCount - 1)) / ScratchRules.TierCount;
        for (var tier = 0; tier < ScratchRules.TierCount; tier++)
        {
            var chipMin = new Vector2(rect.Min.X + tier * (chipWidth + gap), rect.Min.Y);
            var chipRect = new Rect(chipMin, new Vector2(chipMin.X + chipWidth, rect.Max.Y));
            var hovered = changeable && UiInteract.Hover(chipRect.Min, chipRect.Max);
            ChipRail.PaintChip(drawList, chipRect, NumberText.Compact(ScratchRules.Prices[tier]), tier == tierIndex,
                hovered, ui.Ink);
            var underline = chipRect.Height * 0.5f;
            drawList.AddLine(new Vector2(chipRect.Min.X + underline, chipRect.Max.Y - scale),
                new Vector2(chipRect.Max.X - underline, chipRect.Max.Y - scale),
                ImGui.GetColorU32(ScratchTicketArt.For(tier).Accent with { W = tier == tierIndex ? 1f : 0.45f }),
                MathF.Max(1f, 2f * scale));
            if (UiInteract.Click(chipRect.Min, chipRect.Max, hovered) && tier != tierIndex)
            {
                tierIndex = tier;
                playback.Clear();
                seenPhase = ScratchPhase.Idle;
                stampSeconds = -1f;
                CasinoSfx.Play(UiSound.ChipSlide);
            }
        }
    }

    private void DrawSeatMissing(ImDrawListPtr drawList, AppSkin ui, Rect safe, float scale)
    {
        var title = Loc.T(L.Casino.CabinetNoChipsTitle);
        var hint = Loc.T(L.Casino.CabinetNoChipsHint);
        var width = safe.Width;
        var height = CasinoNotice.Height(CasinoNoticeKind.Card, title, hint, width, scale);
        var top = safe.Max.Y - height - Button.LargeHeight * scale - Metrics.Space.Md * scale;
        CasinoNotice.DrawWithAction(drawList, ui, CasinoNoticeKind.Card, title, hint, Loc.T(L.Casino.Cashier),
            safe.Min.X, top, width, scale, out var pressed);
        if (pressed)
        {
            openCashier();
        }
    }
}
