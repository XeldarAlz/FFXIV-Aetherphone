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

internal sealed class ScratchCabinet
{
    private const float PadX = 16f;
    private const float InfoRowHeight = 48f;
    private const float BannerHeight = 58f;
    private const float TierChipHeight = 36f;
    private const float BuyPillHeight = 50f;
    private const float RubHoldPerSecond = 0.9f;
    private const float RubDragFactor = 0.05f;
    private const long BigWinMultiple = 5;
    private const long SmallWinMultiple = 2;
    private const string MalformedReason = "malformed";

    private static readonly Vector4 Gold = new(1f, 0.84f, 0.42f, 1f);
    private static readonly Vector4 FoilTop = new(0.42f, 0.46f, 0.55f, 1f);
    private static readonly Vector4 FoilBottom = new(0.28f, 0.31f, 0.39f, 1f);

    private static readonly Vector4[] ConfettiPalette =
    {
        new(1.00f, 0.84f, 0.42f, 1f),
        new(1.00f, 0.95f, 0.75f, 1f),
        new(0.55f, 0.92f, 0.88f, 1f),
        new(0.80f, 0.58f, 0.98f, 1f),
    };

    private readonly CasinoStore store;
    private readonly CasinoPlayStore play;
    private readonly Action openCashier;
    private readonly ScratchCardPlayback playback = new();
    private readonly ParticleSystem particles = new(192);
    private readonly ScratchOddsSheet oddsSheet = new();

    private RollingValue prizeRoll;
    private int tierIndex;
    private ScratchPhase soundedPhase;
    private long celebrationPrize;
    private string inlineReason = string.Empty;
    private Rect cardArea;

    public ScratchCabinet(CasinoStore store, CasinoPlayStore play, Action openCashier)
    {
        this.store = store;
        this.play = play;
        this.openCashier = openCashier;
    }

    public int CurrentTier => tierIndex;

    public long CurrentPrice => ScratchRules.Prices[tierIndex];

    public bool OddsOpen => oddsSheet.IsOpen;

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
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        oddsSheet.Draw(screen, ui, tierIndex);
    }

    public void Enter()
    {
        inlineReason = string.Empty;
        play.RecoverPendingRound();
    }

    public void Reset()
    {
        playback.Clear();
        particles.Clear();
        oddsSheet.Close();
        inlineReason = string.Empty;
        celebrationPrize = 0;
        prizeRoll.Snap(0);
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        ConsumeResults(scale);
        particles.Update(delta);

        var state = store.State;
        if (state is null)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var pad = PadX * scale;
        var left = body.Min.X + pad;
        var width = body.Width - pad * 2f;
        var y = body.Min.Y + Metrics.Space.Sm * scale;

        y = DrawInfoRow(drawList, ui, state, left, y, width, scale);
        y += Metrics.Space.Sm * scale;

        var cellExtent = MathF.Min((width - 16f * scale) / ScratchRules.GridSide, 96f * scale);
        var cardWidth = cellExtent * ScratchRules.GridSide + 16f * scale;
        var cardHeight = cardWidth;
        var cardLeft = left + (width - cardWidth) * 0.5f;
        cardArea = new Rect(new Vector2(cardLeft, y), new Vector2(cardLeft + cardWidth, y + cardHeight));
        DrawCard(drawList, ui, cardArea, cellExtent, scale, delta);
        y = cardArea.Max.Y + Metrics.Space.Sm * scale;

        y = DrawBanner(drawList, ui, left, y, width, scale, delta);
        y += Metrics.Space.Sm * scale;

        var sitting = state.Sitting;
        var scratchSeated = sitting is not null;
        if (!scratchSeated)
        {
            DrawSeatMissing(drawList, ui, left, y, width, scale);
        }
        else
        {
            y = DrawTierRow(drawList, ui, left, y, width, scale);
            y += Metrics.Space.Md * scale;
            DrawBuyControls(drawList, ui, state, sitting!, left, y, width, scale);
        }

        particles.Draw(drawList, scale);
    }

    private void ConsumeResults(float scale)
    {
        var card = play.TakeScratchResult();
        if (card is not null)
        {
            if (card.Granted && playback.Begin(card))
            {
                inlineReason = string.Empty;
                celebrationPrize = 0;
                prizeRoll.Snap(0);
            }
            else if (card.Granted)
            {
                inlineReason = MalformedReason;
            }
            else
            {
                inlineReason = card.Reason.Length > 0 ? card.Reason : CasinoReasons.Unreachable;
            }
        }

        if (play.TakeRoundFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }

        if (playback.TakeWinCelebration(out var prize))
        {
            celebrationPrize = prize;
            prizeRoll.Snap(0);
            Celebrate(prize, scale);
        }

        var settledNow = soundedPhase == ScratchPhase.Scratching && playback.Phase == ScratchPhase.Settled;
        soundedPhase = playback.Phase;
        if (settledNow && playback.PrizeOnceRevealed <= 0)
        {
            UiFeedback.Play(UiSound.GameWrong);
        }
    }

    private void Celebrate(long prize, float scale)
    {
        var origin = new Vector2(cardArea.Center.X, cardArea.Min.Y + cardArea.Height * 0.35f);
        var price = ScratchRules.Prices[playback.Tier];
        UiFeedback.Play(prize >= price * BigWinMultiple ? UiSound.GamePowerUp : UiSound.GameWin);
        if (prize >= price * BigWinMultiple)
        {
            particles.Confetti(origin, 80, ConfettiPalette, 320f * scale, 5f, 1.5f);
            particles.Sparkle(origin, 20, Gold, 180f * scale, 4f, 1.0f);
        }
        else if (prize >= price * SmallWinMultiple)
        {
            particles.Confetti(origin, 32, ConfettiPalette, 240f * scale, 4f, 1.1f);
        }
        else
        {
            particles.Sparkle(origin, 10, Gold, 130f * scale, 3f, 0.8f);
        }
    }

    private float DrawInfoRow(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state, float left, float y,
        float width, float scale)
    {
        var height = InfoRowHeight * scale;
        var pillWidth = width * 0.46f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + pillWidth, y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(ui.FieldSurface));
        Squircle.Stroke(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.25f)), Metrics.Stroke.Hairline);
        Typography.Draw(drawList, new Vector2(min.X + 16f * scale, y + 7f * scale), Loc.T(L.Casino.SlotsChips),
            ui.MutedInk, TextStyles.Caption1);
        var stackText = NumberText.Group(DisplayStack(state));
        CurrencyGlyph.DrawAmount(drawList, new Vector2(min.X + 16f * scale, y + 21f * scale), stackText,
            CurrencyKind.Chips, ui.TitleInk, TextStyles.SubheadlineEmphasized);
        return y + height;
    }

    private long DisplayStack(CasinoStateDto state)
    {
        var stack = state.Sitting?.Stack ?? 0;
        return Math.Max(0, stack - playback.PrizeStillUnderFoil);
    }

    private void DrawCard(ImDrawListPtr drawList, AppSkin ui, Rect card, float cellExtent, float scale, float delta)
    {
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, card.Min, card.Max, rounding);
        Squircle.Stroke(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.28f)), Metrics.Stroke.Thin * scale);

        var inset = 8f * scale;
        var inner = new Rect(card.Min + new Vector2(inset, inset), card.Max - new Vector2(inset, inset));
        var cellGap = 4f * scale;
        var cellSize = (inner.Width - cellGap * (ScratchRules.GridSide - 1)) / ScratchRules.GridSide;
        var scratching = playback.Phase == ScratchPhase.Scratching;

        if (scratching && UiInteract.HoverWindowOnly(card.Min, card.Max))
        {
            UiInteract.ReportGestureSurface();
        }

        var rubbing = scratching && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var mouseDrag = ImGui.GetIO().MouseDelta.Length();

        for (var cellIndex = 0; cellIndex < ScratchRules.CellCount; cellIndex++)
        {
            var column = cellIndex % ScratchRules.GridSide;
            var row = cellIndex / ScratchRules.GridSide;
            var cellMin = new Vector2(inner.Min.X + column * (cellSize + cellGap),
                inner.Min.Y + row * (cellSize + cellGap));
            var cellMax = cellMin + new Vector2(cellSize, cellSize);
            var center = (cellMin + cellMax) * 0.5f;
            var cellRounding = 10f * scale;
            Squircle.Fill(drawList, cellMin, cellMax, cellRounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.FieldSurface, 0.9f)));

            if (playback.Phase == ScratchPhase.Idle)
            {
                DrawFoil(drawList, cellMin, cellMax, cellRounding, center, 1f, scale);
                continue;
            }

            var foilLeft = playback.FoilRemaining(cellIndex);
            if (foilLeft < 1f)
            {
                var matched = playback.IsMatchedCell(cellIndex);
                if (matched)
                {
                    var glow = 0.12f + 0.06f * MathF.Sin((float)ImGui.GetTime() * 5f);
                    drawList.AddCircleFilled(center, cellSize * 0.48f, ImGui.GetColorU32(Gold with { W = glow }), 24);
                }

                var settledLoss = playback.RevealComplete && playback.PrizeOnceRevealed <= 0;
                var dimmed = (playback.WinningSymbolOnceMatched >= 0 && !matched) || settledLoss;
                ScratchSymbolArt.Draw(drawList, playback.CellSymbol(cellIndex), center, cellSize * 0.3f,
                    dimmed ? 0.45f : 1f);
            }

            if (foilLeft > 0f)
            {
                DrawFoil(drawList, cellMin, cellMax, cellRounding, center, foilLeft, scale);
            }

            if (!scratching || playback.IsRevealed(cellIndex))
            {
                continue;
            }

            var hovered = UiInteract.Hover(cellMin, cellMax);
            if (!hovered)
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (!rubbing)
            {
                continue;
            }

            var rub = delta * RubHoldPerSecond + mouseDrag / MathF.Max(cellSize, 1f) * RubDragFactor;
            playback.Rub(cellIndex, rub);
            if (playback.IsRevealed(cellIndex))
            {
                UiFeedback.Play(UiSound.GameCollect);
            }

            particles.Sparkle(ImGui.GetMousePos(), 1, Palette.WithAlpha(FoilTop, 0.8f), 60f * scale, 2f, 0.35f);
        }
    }

    private static void DrawFoil(ImDrawListPtr drawList, Vector2 cellMin, Vector2 cellMax, float rounding,
        Vector2 center, float amount, float scale)
    {
        var alpha = Math.Clamp(amount, 0f, 1f);
        Squircle.FillVerticalGradient(drawList, cellMin, cellMax, rounding,
            ImGui.GetColorU32(FoilTop with { W = alpha }), ImGui.GetColorU32(FoilBottom with { W = alpha }));
        var sheen = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f * alpha));
        var size = cellMax.X - cellMin.X;
        drawList.AddLine(new Vector2(cellMin.X + size * 0.2f, cellMax.Y - size * 0.15f),
            new Vector2(cellMax.X - size * 0.15f, cellMin.Y + size * 0.2f), sheen, 3f * scale);
        SlotsSymbolArt.DrawSparkle(drawList, center, size * 0.14f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.30f * alpha)));
    }

    private float DrawBanner(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width, float scale,
        float delta)
    {
        var height = BannerHeight * scale;
        var center = new Vector2(left + width * 0.5f, y + height * 0.5f);
        if (celebrationPrize > 0)
        {
            prizeRoll.Update((int)celebrationPrize, delta);
            Typography.DrawCentered(drawList, center with { Y = center.Y - 14f * scale },
                Loc.T(L.Casino.ScratchWinBanner), Gold, TextStyles.FootnoteEmphasized);
            var amount = "+" + NumberText.Group((long)prizeRoll.Display);
            Typography.DrawCentered(drawList, center with { Y = center.Y + 8f * scale }, amount, Gold,
                TextStyles.Title2.Scale * prizeRoll.PopScale, TextStyles.Title2.Weight);
            return y + height;
        }

        if (playback.RevealComplete)
        {
            Typography.DrawCentered(drawList, center, Loc.T(L.Casino.ScratchNoWin), ui.MutedInk,
                TextStyles.Subheadline);
            return y + height;
        }

        var hint = Loc.T(L.Casino.ScratchHint);
        var hintTop = y + Metrics.Space.Xs * scale;
        var hintHeight = Typography.DrawWrappedCentered(new Vector2(center.X, hintTop), hint, ui.MutedInk,
            TextStyles.Footnote, width);
        var bottom = hintTop + hintHeight;
        if (playback.Phase == ScratchPhase.Scratching)
        {
            bottom = DrawRevealAll(drawList, ui, center.X, bottom + Metrics.Space.Xs * scale, scale);
        }

        return MathF.Max(bottom, y + height);
    }

    private float DrawRevealAll(ImDrawListPtr drawList, AppSkin ui, float centerX, float top, float scale)
    {
        var label = Loc.T(L.Casino.ScratchRevealAll);
        var labelSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var chipHeight = 26f * scale;
        var chipMin = new Vector2(centerX - labelSize.X * 0.5f - 12f * scale, top);
        var chipMax = new Vector2(centerX + labelSize.X * 0.5f + 12f * scale, top + chipHeight);
        var hovered = UiInteract.Hover(chipMin, chipMax);
        Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(ui.FieldSurface));
        Squircle.Stroke(drawList, chipMin, chipMax, chipHeight * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.3f)), 1f * scale);
        if (hovered)
        {
            Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, label, ui.BodyInk,
            TextStyles.FootnoteEmphasized);
        if (UiInteract.Click(chipMin, chipMax, hovered))
        {
            playback.RevealAll();
            UiFeedback.Play(UiSound.GameCollect);
        }

        return chipMax.Y;
    }

    private float DrawTierRow(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width, float scale)
    {
        Typography.Draw(drawList, new Vector2(left, y), Loc.T(L.Casino.ScratchPrice), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        y += 20f * scale;
        var gap = 8f * scale;
        var chipWidth = (width - gap * 2f) / ScratchRules.TierCount;
        var chipHeight = TierChipHeight * scale;
        var changeable = !play.RoundInFlight && playback.Phase != ScratchPhase.Scratching;
        for (var tier = 0; tier < ScratchRules.TierCount; tier++)
        {
            var chipMin = new Vector2(left + tier * (chipWidth + gap), y);
            var chipMax = new Vector2(chipMin.X + chipWidth, y + chipHeight);
            var active = tier == tierIndex;
            var hovered = changeable && UiInteract.Hover(chipMin, chipMax);
            var fill = active ? Palette.WithAlpha(ui.Accent, 0.9f) : ui.FieldSurface;
            Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(fill));
            if (hovered)
            {
                Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(ui.HoverTint));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var ink = active ? ui.Palette.HeaderInk : ui.BodyInk;
            Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f,
                GameNumber.Label((int)ScratchRules.Prices[tier]), ink, TextStyles.SubheadlineEmphasized);
            if (UiInteract.Click(chipMin, chipMax, hovered))
            {
                tierIndex = tier;
            }
        }

        return y + chipHeight;
    }

    private void DrawBuyControls(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state,
        CasinoSittingDto sitting, float left, float y, float width, float scale)
    {
        if (inlineReason.Length > 0)
        {
            y = DrawReasonCard(drawList, ui, Loc.T(CasinoReasons.MessageFor(inlineReason)), left, y, width, scale);
            y += Metrics.Space.Sm * scale;
        }

        var price = CurrentPrice;
        var lowStack = sitting.Stack < price;
        var blocked = state.StakesPaused || state.Draining;
        var scratching = playback.Phase == ScratchPhase.Scratching;
        var canBuy = !play.RoundInFlight && !scratching && !blocked && !lowStack;
        var priceText = NumberText.Group(price);
        var label = playback.RevealComplete
            ? Loc.T(L.Casino.ScratchAnotherFor, priceText)
            : Loc.T(L.Casino.ScratchBuyFor, priceText);
        var pillRect = new Rect(new Vector2(left, y), new Vector2(left + width, y + BuyPillHeight * scale));
        if (ui.ActionPill(pillRect, label, canBuy, TextStyles.Headline))
        {
            inlineReason = string.Empty;
            celebrationPrize = 0;
            prizeRoll.Snap(0);
            play.BuyScratch(tierIndex);
            UiFeedback.Play(UiSound.CasinoChips);
        }

        y = pillRect.Max.Y + Metrics.Space.Sm * scale;
        if (blocked)
        {
            var notice = state.StakesPaused ? Loc.T(L.Casino.PausedTitle) : Loc.T(L.Casino.DrainingTitle);
            Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y + 8f * scale), notice,
                ui.MutedInk, TextStyles.Footnote);
            return;
        }

        if (!lowStack || play.RoundInFlight || scratching)
        {
            return;
        }

        var hint = Loc.T(L.Casino.ScratchLowStack);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(new Vector2(left, y), hint, ui.MutedInk, TextStyles.Footnote, width);
        y += hintBlock.Y + Metrics.Space.Sm * scale;
        var cashierRect = new Rect(new Vector2(left + width * 0.25f, y),
            new Vector2(left + width * 0.75f, y + 38f * scale));
        if (ui.GhostButton(cashierRect, Loc.T(L.Casino.Cashier)))
        {
            openCashier();
        }
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

    private void DrawSeatMissing(ImDrawListPtr drawList, AppSkin ui, float left, float y, float width, float scale)
    {
        var title = Loc.T(L.Casino.CabinetNoChipsTitle);
        var hint = Loc.T(L.Casino.CabinetNoChipsHint);
        var pad = 14f * scale;
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - pad * 2f);
        var cardHeight = titleSize.Y + hintBlock.Y + pad * 2f + 6f * scale;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + cardHeight);
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
