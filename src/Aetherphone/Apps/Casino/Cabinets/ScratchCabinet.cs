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
    public const int AutoRounds = 5;

    private const float RubHoldPerSecond = 0.9f;
    private const float RubDragFactor = 0.05f;
    private const float TicketMaxWidth = 300f;
    private const float TicketHeaderShare = 0.24f;
    private const float KioskInset = 12f;
    private const float CellGap = 6f;
    private const float AutoPauseSeconds = 0.8f;
    private const float RubTickSeconds = 0.09f;
    private const float StatusGap = 10f;
    private const string MalformedReason = "malformed";

    private static readonly Vector4 FoilTop = new(0.62f, 0.64f, 0.72f, 1f);
    private static readonly Vector4 FoilBottom = new(0.36f, 0.38f, 0.46f, 1f);
    private static readonly Vector4 TicketPaper = new(0.10f, 0.07f, 0.16f, 1f);

    private static readonly Vector4[] TierTints =
    {
        CasinoColors.LightB,
        new(0.30f, 0.86f, 0.55f, 1f),
        CasinoColors.LightA,
        CasinoColors.Money,
    };

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
    private Rect ticketArea;
    private float autoPause;
    private float rubTick;
    private float idleTime;
    private bool stroked;

    public ScratchCabinet(CasinoStore store, CasinoPlayStore play, Action openCashier)
    {
        this.store = store;
        this.play = play;
        this.openCashier = openCashier;
        composer.Auto.Rounds = AutoRounds;
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
        var statusHeight = Typography.LineHeight(TextStyles.Subheadline) * 2f + StatusGap * scale;
        var ticketBottom = safe.Max.Y - statusHeight;
        LayoutTicket(safe, ticketBottom, scale);
        DrawKiosk(drawList, stage, frame, scale);
        DrawTicket(drawList, ui, frame.DeltaSeconds, scale, frame.Blocked);
        DrawStatus(drawList, ui, state, sitting, new Rect(new Vector2(safe.Min.X, ticketArea.Max.Y + StatusGap * scale),
            safe.Max), scale);
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
        var side = MathF.Min(rect.Width, rect.Height) * 0.78f;
        var ticket = new Rect(rect.Center - new Vector2(side * 0.5f, side * 0.5f),
            rect.Center + new Vector2(side * 0.5f, side * 0.5f));
        CasinoLights.BulbChase(drawList, ticket.Inset(-KioskInset * 0.5f * scale), Metrics.Radius.Grouped * scale, scale,
            idleTime, CasinoLights.BulbPitch, CasinoColors.Money, CasinoColors.LightA, 0.8f);
        PaintTicketShell(drawList, ticket, 1, scale);
        var grid = GridRect(ticket);
        var cell = (grid.Width - CellGap * scale * (ScratchRules.GridSide - 1)) / ScratchRules.GridSide;
        for (var cellIndex = 0; cellIndex < ScratchRules.CellCount; cellIndex++)
        {
            var cellMin = CellMin(grid, cellIndex, cell, scale);
            var shimmer = 0.5f + 0.5f * MathF.Sin(idleTime * 1.6f + cellIndex * 0.7f);
            DrawFoil(drawList, cellMin, cellMin + new Vector2(cell, cell), cell * 0.18f, 1f, shimmer, scale);
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

        if (playback.TakeWinCelebration(out var prize))
        {
            var price = ScratchRules.Prices[playback.Tier];
            stage.Celebration.Celebrate(price, prize, ticketArea.Center, frame.Instant);
            stage.Particles.Emit(CasinoLights.Sparkle(UiScale.Current), ticketArea.Center, 18);
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
            available / (1f + TicketHeaderShare) - KioskInset * scale);
        width = MathF.Max(0f, width);
        var height = width * (1f + TicketHeaderShare);
        var left = safe.Center.X - width * 0.5f;
        var top = safe.Min.Y + MathF.Max(0f, (available - height) * 0.5f);
        ticketArea = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
    }

    private void DrawKiosk(ImDrawListPtr drawList, CasinoStage stage, in CasinoStageFrame frame, float scale)
    {
        var celebrating = stage.Celebration.Active;
        var kiosk = ticketArea.Inset(-KioskInset * scale);
        CasinoLights.Spotlight(drawList, new Vector2(kiosk.Center.X, frame.Full.Min.Y), MathF.PI * 0.5f,
            kiosk.Max.Y - frame.Full.Min.Y, 0.32f, CasinoColors.MoneyHighlight, 0.05f);
        CasinoLights.BulbChase(drawList, kiosk, Metrics.Radius.Grouped * scale, scale, frame.Phase * (celebrating ? 2f : 1f),
            CasinoLights.BulbPitch, CasinoColors.Money, TierTints[Math.Clamp(DisplayTier, 0, TierTints.Length - 1)],
            celebrating ? 1f : 0.55f);
    }

    private int DisplayTier => playback.Phase == ScratchPhase.Idle ? tierIndex : playback.Tier;

    private void DrawTicket(ImDrawListPtr drawList, AppSkin ui, float delta, float scale, bool blocked)
    {
        if (ticketArea.Width <= 0f)
        {
            return;
        }

        PaintTicketShell(drawList, ticketArea, DisplayTier, scale);
        var grid = GridRect(ticketArea);
        var cell = (grid.Width - CellGap * scale * (ScratchRules.GridSide - 1)) / ScratchRules.GridSide;
        var scratching = playback.Phase == ScratchPhase.Scratching && !blocked;
        if (scratching && UiInteract.HoverWindowOnly(grid.Min, grid.Max))
        {
            UiInteract.ReportGestureSurface();
        }

        var rubbing = scratching && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var drag = ImGui.GetIO().MouseDelta.Length();
        rubTick = MathF.Max(0f, rubTick - delta);
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
                DrawFoil(drawList, cellMin, cellMax, rounding, 1f, 0.5f, scale);
                continue;
            }

            var foilLeft = playback.FoilRemaining(cellIndex);
            if (foilLeft < 1f)
            {
                DrawSymbol(drawList, cellIndex, center, cell);
            }

            if (foilLeft > 0f)
            {
                DrawFoil(drawList, cellMin, cellMax, rounding, foilLeft, 0.5f, scale);
            }

            if (!rubbing || playback.IsRevealed(cellIndex) || !UiInteract.Hover(cellMin, cellMax))
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            stroked = true;
            playback.Rub(cellIndex, delta * RubHoldPerSecond + drag / MathF.Max(cell, 1f) * RubDragFactor);
            if (rubTick <= 0f)
            {
                CasinoSfx.Pitched(UiSound.PegTick, cellIndex);
                rubTick = RubTickSeconds;
            }

            if (playback.IsRevealed(cellIndex))
            {
                CasinoSfx.Play(UiSound.TileSafe);
            }
        }
    }

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

    private static Rect GridRect(Rect ticket)
    {
        var header = ticket.Height - ticket.Width;
        var inset = ticket.Width * 0.07f;
        return new Rect(new Vector2(ticket.Min.X + inset, ticket.Min.Y + header + inset * 0.2f),
            new Vector2(ticket.Max.X - inset, ticket.Max.Y - inset));
    }

    private static Vector2 CellMin(Rect grid, int cellIndex, float cell, float scale)
    {
        var column = cellIndex % ScratchRules.GridSide;
        var row = cellIndex / ScratchRules.GridSide;
        var gap = CellGap * scale;
        return new Vector2(grid.Min.X + column * (cell + gap), grid.Min.Y + row * (cell + gap));
    }

    private static void PaintTicketShell(ImDrawListPtr drawList, Rect ticket, int tier, float scale)
    {
        var tint = TierTints[Math.Clamp(tier, 0, TierTints.Length - 1)];
        var rounding = Metrics.Radius.Grouped * scale;
        Elevation.Card(drawList, ticket.Min, ticket.Max, rounding, scale, 1f);
        Squircle.FillVerticalGradient(drawList, ticket.Min, ticket.Max, rounding,
            ImGui.GetColorU32(Palette.Mix(TicketPaper, tint, 0.18f)), ImGui.GetColorU32(TicketPaper));
        Squircle.Stroke(drawList, ticket.Min, ticket.Max, rounding, ImGui.GetColorU32(tint with { W = 0.55f }),
            MathF.Max(1f, 1.4f * scale));
        var header = ticket.Height - ticket.Width;
        var signHeight = CasinoSigns.HeightToFit(CasinoSign.Scratch, ticket.Width * 0.72f, header * 0.42f);
        CasinoSigns.Draw(drawList, CasinoSign.Scratch, new Vector2(ticket.Center.X, ticket.Min.Y + header * 0.42f),
            signHeight, tint, 1f);
        var price = NumberText.Compact(ScratchRules.Prices[Math.Clamp(tier, 0, ScratchRules.TierCount - 1)]);
        var priceSize = CurrencyGlyph.MeasureAmount(price, TextStyles.FootnoteEmphasized);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(ticket.Center.X - priceSize.X * 0.5f,
            ticket.Min.Y + header * 0.72f), price, CurrencyKind.Chips, CasinoColors.Money, TextStyles.FootnoteEmphasized);
    }

    private static void DrawFoil(ImDrawListPtr drawList, Vector2 cellMin, Vector2 cellMax, float rounding,
        float amount, float shimmer, float scale)
    {
        var alpha = Math.Clamp(amount, 0f, 1f);
        Squircle.FillVerticalGradient(drawList, cellMin, cellMax, rounding,
            ImGui.GetColorU32(FoilTop with { W = alpha }), ImGui.GetColorU32(FoilBottom with { W = alpha }));
        var size = cellMax.X - cellMin.X;
        var sheen = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (0.06f + 0.10f * shimmer) * alpha));
        drawList.AddLine(new Vector2(cellMin.X + size * 0.2f, cellMax.Y - size * 0.15f),
            new Vector2(cellMax.X - size * 0.15f, cellMin.Y + size * 0.2f), sheen, 3f * scale);
        SlotsSymbolArt.DrawSparkle(drawList, (cellMin + cellMax) * 0.5f, size * 0.14f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.30f * alpha)));
    }

    private void DrawStatus(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state, CasinoSittingDto? sitting,
        Rect area, float scale)
    {
        var centerX = area.Center.X;
        var top = area.Min.Y;
        if (inlineReason.Length > 0)
        {
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty,
                CasinoReasons.Text(inlineReason, store.Ceiling.MaxBet), area.Min.X, top, area.Width, scale);
            return;
        }

        if (state.StakesPaused || state.Draining)
        {
            DrawLine(drawList, Loc.T(state.StakesPaused ? L.Casino.PausedTitle : L.Casino.DrainingTitle), centerX, top,
                area.Width, ui.MutedInk);
            return;
        }

        if (playback.RevealComplete && playback.PrizeOnceRevealed <= 0)
        {
            DrawLine(drawList, Loc.T(L.Casino.ScratchNoWin), centerX, top, area.Width, CasinoColors.Loss);
            return;
        }

        if (playback.Phase == ScratchPhase.Scratching && stroked)
        {
            var label = Loc.T(L.Casino.ScratchRevealAll);
            var width = Button.WidthFor(label, ButtonSize.Small);
            var rect = new Rect(new Vector2(centerX - width * 0.5f, top),
                new Vector2(centerX + width * 0.5f, top + Button.SmallHeight * scale));
            if (Button.Draw(drawList, rect, label, ui.Ink, ButtonStyle.Tinted))
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

        if (playback.Phase != ScratchPhase.Settled)
        {
            DrawLine(drawList, Loc.T(L.Casino.ScratchHint), centerX, top, area.Width, ui.MutedInk);
        }
    }

    private static void DrawLine(ImDrawListPtr drawList, string text, float centerX, float top, float width,
        Vector4 ink)
    {
        Typography.DrawWrappedCentered(drawList, text, TextStyles.Subheadline, ink, new Vector2(centerX, top), width);
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
            if (UiInteract.Click(chipRect.Min, chipRect.Max, hovered) && tier != tierIndex)
            {
                tierIndex = tier;
                playback.Clear();
                seenPhase = ScratchPhase.Idle;
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
