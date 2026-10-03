using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed class CashierDrawer
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float RevealSmoothTime = 0.16f;
    private const float MaxDim = 0.45f;
    private const float PanelRounding = 26f;
    private const float PadX = 18f;
    private const float SectionGap = 10f;
    private const float SummaryRowHeight = 22f;
    private const float CardPad = 12f;
    private const float PillHeight = 44f;
    private const float LotHeight = 42f;
    private const float LotGap = 8f;
    private const int LotColumns = 2;
    private const long FallbackMinBuyIn = 2_000;
    private const long FallbackMaxBuyIn = 200_000;
    private const long MinTopUp = CasinoChipLots.ChipPerCoin;

    private readonly CasinoStore store;
    private readonly CoinStore coins;
    private readonly ConfirmService confirm;

    private Spring reveal;
    private bool open;
    private int openedFrame;
    private long selectedLot;
    private bool lotPinned;
    private string inlineReason = string.Empty;

    public CashierDrawer(CasinoStore store, CoinStore coins, ConfirmService confirm)
    {
        this.store = store;
        this.coins = coins;
        this.confirm = confirm;
    }

    public void Open()
    {
        if (open)
        {
            return;
        }

        open = true;
        openedFrame = ImGui.GetFrameCount();
        selectedLot = 0;
        lotPinned = false;
        inlineReason = string.Empty;
        store.RefreshNow();
        coins.EnsureFresh();
    }

    public void Open(long suggestedAmount)
    {
        Open();
        var suggested = CasinoChipLots.ToWholeCoins(suggestedAmount);
        if (suggested > 0)
        {
            selectedLot = suggested;
            lotPinned = true;
        }
    }

    public void Close()
    {
        open = false;
    }

    public void Gate()
    {
        if (open && confirm.Active is null)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, AppSkin ui, Action openLimits)
    {
        ConsumeResults(openLimits);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, RevealSmoothTime, delta);
        if (!open && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            return;
        }

        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        var slide = Easing.EaseOutQuint(opacity);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##cashierDrawer", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
            var interactive = open && confirm.Active is null && opacity > 0.5f;
            var panel = DrawPanel(screen, ui, drawList, slide, interactive);
            if (!interactive)
            {
                return;
            }

            if (ImGui.GetFrameCount() != openedFrame && UiInteract.ClickedOutside(panel.Min, panel.Max))
            {
                Close();
            }
        }
    }

    private void ConsumeResults(Action openLimits)
    {
        var sitting = store.TakeSittingResult();
        if (sitting is not null)
        {
            if (sitting.Granted)
            {
                UiFeedback.Play(UiSound.CasinoChips);
            }

            HandleOutcome(sitting.Reason, openLimits, true);
        }

        var closed = store.TakeCloseResult();
        if (closed is not null)
        {
            if (closed.Granted)
            {
                UiFeedback.Play(UiSound.Payout);
            }

            HandleOutcome(closed.Reason, openLimits, false);
        }

        if (store.TakeMoneyMoveFailure())
        {
            HandleOutcome(CasinoReasons.Unreachable, openLimits, false);
        }
    }

    private void HandleOutcome(string reason, Action openLimits, bool clearAmount)
    {
        if (reason.Length == 0)
        {
            inlineReason = string.Empty;
            if (clearAmount)
            {
                selectedLot = 0;
                lotPinned = false;
            }

            return;
        }

        if (string.Equals(reason, CasinoReasons.LossLimit, StringComparison.Ordinal))
        {
            Close();
            openLimits();
            return;
        }

        if (open)
        {
            inlineReason = reason;
            return;
        }

        confirm.Alert(null, Loc.T(CasinoReasons.MessageFor(reason)), Loc.T(L.Common.Close));
    }

    private Rect DrawPanel(Rect screen, AppSkin ui, ImDrawListPtr drawList, float slide, bool interactive)
    {
        var scale = UiScale.Current;
        var state = store.State;
        var wallet = coins.Wallet;
        var sittingOpen = state?.Sitting is not null;
        var frozen = wallet?.FrozenUntilUnix is not null;
        var paused = state?.StakesPaused == true;
        var draining = state?.Draining == true;
        var stakeBlocked = frozen || paused || draining;
        var innerWidth = screen.Width - PadX * 2f * scale;

        var noticeTitle = string.Empty;
        var noticeHint = string.Empty;
        if (frozen)
        {
            noticeTitle = Loc.T(L.Coin.FrozenTitle);
            noticeHint = Loc.T(L.Coin.FrozenHint);
        }
        else if (paused)
        {
            noticeTitle = Loc.T(L.Casino.PausedTitle);
            noticeHint = Loc.T(L.Casino.PausedHint);
        }
        else if (draining)
        {
            noticeTitle = Loc.T(L.Casino.DrainingTitle);
            noticeHint = Loc.T(L.Casino.DrainingHint);
        }

        var reasonText = inlineReason.Length > 0 ? Loc.T(CasinoReasons.MessageFor(inlineReason)) : string.Empty;

        var titleHeight = Typography.Measure(Loc.T(L.Casino.Cashier), TextStyles.Headline).Y;
        var summaryRows = sittingOpen ? 3 : 2;
        var summaryHeight = summaryRows * SummaryRowHeight * scale + CardPad * 2f * scale;
        var noticeHeight = 0f;
        if (noticeTitle.Length > 0)
        {
            var hintBlock = Typography.MeasureWrappedBlock(noticeHint, TextStyles.Footnote, innerWidth - CardPad * 2f * scale);
            noticeHeight = Typography.Measure(noticeTitle, TextStyles.FootnoteEmphasized).Y + hintBlock.Y
                + CardPad * 2f * scale + 6f * scale + SectionGap * scale;
        }

        var reasonHeight = 0f;
        if (reasonText.Length > 0)
        {
            var reasonBlock = Typography.MeasureWrappedBlock(reasonText, TextStyles.Footnote, innerWidth - CardPad * 2f * scale);
            reasonHeight = reasonBlock.Y + CardPad * 2f * scale + SectionGap * scale;
        }

        var lotRows = (CasinoChipLots.Chips.Length + LotColumns) / LotColumns;
        var stakeHeight = stakeBlocked
            ? 0f
            : (18f + 6f + lotRows * LotHeight + (lotRows - 1) * LotGap + SectionGap + PillHeight) * scale;
        var cashOutHeight = sittingOpen ? (SectionGap + PillHeight + 20f) * scale : 0f;
        var panelHeight = 14f * scale + titleHeight + SectionGap * scale + summaryHeight + SectionGap * scale
            + noticeHeight + reasonHeight + stakeHeight + cashOutHeight + 18f * scale;

        var panelBottom = screen.Max.Y + panelHeight * (1f - slide);
        var panelTop = panelBottom - panelHeight;
        var panelMin = new Vector2(screen.Min.X, panelTop);
        var panelMax = new Vector2(screen.Max.X, panelBottom);
        var rounding = PanelRounding * scale;
        Squircle.Fill(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.Lighten(ui.Palette.BackdropTop, 0.10f) with { W = 1f }));
        Squircle.Stroke(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.08f)), Metrics.Stroke.Hairline);

        var left = panelMin.X + PadX * scale;
        var y = panelTop + 14f * scale;
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, y + titleHeight * 0.5f),
            Loc.T(L.Casino.Cashier), ui.TitleInk, TextStyles.Headline);
        y += titleHeight + SectionGap * scale;

        y = DrawSummary(drawList, ui, state, wallet, sittingOpen, left, y, innerWidth, scale);
        y += SectionGap * scale;

        if (noticeTitle.Length > 0)
        {
            y = DrawNotice(drawList, ui, noticeTitle, noticeHint, left, y, innerWidth, scale);
            y += SectionGap * scale;
        }

        if (reasonText.Length > 0)
        {
            y = DrawReason(drawList, ui, reasonText, left, y, innerWidth, scale);
            y += SectionGap * scale;
        }

        if (!stakeBlocked)
        {
            y = DrawStakeEntry(drawList, ui, state, wallet, sittingOpen, left, y, innerWidth, scale,
                interactive);
        }

        if (sittingOpen)
        {
            y += SectionGap * scale;
            DrawCashOut(drawList, ui, state!.Sitting!, left, y, innerWidth, scale, interactive);
        }

        return new Rect(panelMin, panelMax);
    }

    private static float DrawSummary(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto? state,
        CoinWalletDto? wallet, bool sittingOpen, float left, float y, float innerWidth, float scale)
    {
        var rows = sittingOpen ? 3 : 2;
        var height = rows * SummaryRowHeight * scale + CardPad * 2f * scale;
        var min = new Vector2(left, y);
        var max = new Vector2(left + innerWidth, y + height);
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(ui.Palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, 16f * scale, scale);

        var rowY = min.Y + CardPad * scale;
        var balanceText = NumberText.Group(wallet?.Balance ?? 0);
        DrawSummaryRow(drawList, ui, Loc.T(L.Casino.WalletRow), balanceText, CurrencyKind.Coins, left, rowY,
            innerWidth, scale, ui.TitleInk);
        rowY += SummaryRowHeight * scale;

        if (sittingOpen)
        {
            var stackText = NumberText.Group(state?.Sitting?.Stack ?? 0);
            DrawSummaryRow(drawList, ui, Loc.T(L.Casino.ChipsRow), stackText, CurrencyKind.Chips, left, rowY,
                innerWidth, scale, ui.Accent);
            rowY += SummaryRowHeight * scale;
        }

        var net = state?.NetLossToday ?? 0;
        var tonight = net switch
        {
            > 0 => Loc.T(L.Casino.TonightDown, NumberText.Group(net)),
            < 0 => Loc.T(L.Casino.TonightUp, NumberText.Group(-net)),
            _ => Loc.T(L.Casino.TonightEven),
        };
        Typography.Draw(drawList, new Vector2(left + CardPad * scale, rowY + 2f * scale), tonight, ui.MutedInk,
            TextStyles.Footnote);
        return max.Y;
    }

    private static void DrawSummaryRow(ImDrawListPtr drawList, AppSkin ui, string label, string value,
        CurrencyKind kind, float left, float rowY, float innerWidth, float scale, Vector4 valueInk)
    {
        Typography.Draw(drawList, new Vector2(left + CardPad * scale, rowY), label, ui.BodyInk,
            TextStyles.Subheadline);
        var valueSize = CurrencyGlyph.MeasureAmount(value, TextStyles.SubheadlineEmphasized);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(left + innerWidth - CardPad * scale - valueSize.X, rowY),
            value, kind, valueInk, TextStyles.SubheadlineEmphasized);
    }

    private static float DrawNotice(ImDrawListPtr drawList, AppSkin ui, string title, string hint, float left,
        float y, float innerWidth, float scale)
    {
        var pad = CardPad * scale;
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, innerWidth - pad * 2f);
        var height = titleSize.Y + hintBlock.Y + pad * 2f + 6f * scale;
        var min = new Vector2(left, y);
        var max = new Vector2(left + innerWidth, y + height);
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(ui.Palette.CardFill));
        Material.EdgeSquircle(drawList, min, max, 16f * scale, scale);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), title, ui.Accent,
            TextStyles.FootnoteEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad + titleSize.Y + 6f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, innerWidth - pad * 2f);
        return max.Y;
    }

    private static float DrawReason(ImDrawListPtr drawList, AppSkin ui, string message, float left, float y,
        float innerWidth, float scale)
    {
        var pad = CardPad * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, innerWidth - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + innerWidth, y + height);
        Squircle.Fill(drawList, min, max, 16f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Squircle.Stroke(drawList, min, max, 16f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), message, ui.TitleInk,
            TextStyles.Footnote, innerWidth - pad * 2f);
        return max.Y;
    }

    private float DrawStakeEntry(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto? state,
        CoinWalletDto? wallet, bool sittingOpen, float left, float y, float innerWidth, float scale,
        bool interactive)
    {
        var minBuyIn = state is { MinBuyIn: > 0 } ? state.MinBuyIn : FallbackMinBuyIn;
        var maxBuyIn = state is { MaxBuyIn: > 0 } ? state.MaxBuyIn : FallbackMaxBuyIn;
        var minAmount = sittingOpen ? MinTopUp : minBuyIn;
        var room = sittingOpen ? Math.Max(0, maxBuyIn - (state?.Sitting?.ChipsIn ?? 0)) : maxBuyIn;

        var walletChips = (wallet?.Balance ?? 0) * CasinoChipLots.ChipPerCoin;
        var effectiveMax = CasinoChipLots.ToWholeCoins(Math.Min(room, walletChips));

        var heading = sittingOpen ? Loc.T(L.Casino.TopUp) : Loc.T(L.Casino.BuyIn);
        Typography.Draw(drawList, new Vector2(left, y), heading, ui.MutedInk, TextStyles.FootnoteEmphasized);
        var rate = Loc.T(L.Casino.ChipRate);
        var rateSize = Typography.Measure(rate, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left + innerWidth - rateSize.X, y), rate, ui.MutedInk,
            TextStyles.Footnote);
        y += (18f + 6f) * scale;

        var remainder = CasinoChipLots.RemainderFor(minAmount, effectiveMax);
        if (!lotPinned || !CasinoChipLots.IsAffordable(selectedLot, minAmount, effectiveMax))
        {
            var preselect = CasinoChipLots.PreselectFor(minAmount, effectiveMax);
            selectedLot = preselect > 0 ? preselect : remainder;
        }

        var lotWidth = (innerWidth - (LotColumns - 1) * LotGap * scale) / LotColumns;
        var lots = CasinoChipLots.Chips;
        var gridTop = y;
        for (var index = 0; index < lots.Length; index++)
        {
            var lot = lots[index];
            var column = index % LotColumns;
            var row = index / LotColumns;
            var shown = lot;
            var lotMin = new Vector2(left + column * (lotWidth + LotGap * scale),
                gridTop + row * (LotHeight + LotGap) * scale);
            var lotMax = new Vector2(lotMin.X + lotWidth, lotMin.Y + LotHeight * scale);
            var lotAffordable = CasinoChipLots.IsAffordable(shown, minAmount, effectiveMax);
            if (DrawLot(drawList, ui, new Rect(lotMin, lotMax), shown, shown == selectedLot, lotAffordable,
                    interactive, scale))
            {
                selectedLot = shown;
                lotPinned = true;
            }
        }

        if (remainder > 0)
        {
            var column = lots.Length % LotColumns;
            var row = lots.Length / LotColumns;
            var lotMin = new Vector2(left + column * (lotWidth + LotGap * scale),
                gridTop + row * (LotHeight + LotGap) * scale);
            var lotMax = new Vector2(lotMin.X + lotWidth, lotMin.Y + LotHeight * scale);
            if (DrawLot(drawList, ui, new Rect(lotMin, lotMax), remainder, remainder == selectedLot, true,
                    interactive, scale))
            {
                selectedLot = remainder;
                lotPinned = true;
            }
        }

        var lotRows = (lots.Length + LotColumns) / LotColumns;
        y += (lotRows * LotHeight + (lotRows - 1) * LotGap) * scale + SectionGap * scale;

        var amount = selectedLot;
        var amountValid = CasinoChipLots.IsAffordable(amount, minAmount, effectiveMax);
        var busy = store.MovingMoney;
        var label = amountValid
            ? Loc.T(sittingOpen ? L.Casino.TopUpFor : L.Casino.BuyInFor,
                NumberText.Group(amount))
            : Loc.T(L.Casino.NotEnoughCoins);
        var confirmRect = new Rect(new Vector2(left, y), new Vector2(left + innerWidth, y + PillHeight * scale));
        var canConfirm = interactive && amountValid && !busy;
        if (RawPill(drawList, confirmRect, label, true, canConfirm, ui, scale))
        {
            AskStake(sittingOpen, amount);
        }

        return y + PillHeight * scale;
    }

    private static bool DrawLot(ImDrawListPtr drawList, AppSkin ui, Rect rect, long chips, bool selected,
        bool affordable, bool interactive, float scale)
    {
        var live = interactive && affordable;
        var rounding = Metrics.Radius.Sm * scale;
        var hovered = live && UiInteract.HoverWindowOnly(rect.Min, rect.Max);
        var fill = selected && affordable
            ? Palette.WithAlpha(ui.Accent, 0.16f)
            : Palette.WithAlpha(ui.FieldSurface, affordable ? 1f : 0.4f);
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(fill));
        if (selected && affordable)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.65f)), 1.5f * scale);
        }

        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var chipInk = affordable ? (selected ? ui.Accent : ui.TitleInk) : Palette.WithAlpha(ui.MutedInk, 0.6f);
        var costInk = Palette.WithAlpha(ui.MutedInk, affordable ? 1f : 0.6f);
        var glyphAlpha = affordable ? 1f : 0.45f;
        var chipText = NumberText.Group(chips);
        var costText = Loc.T(L.Casino.LotCost, NumberText.Group(CasinoChipLots.CoinsFor(chips)));
        var chipSize = CurrencyGlyph.MeasureAmount(chipText, TextStyles.SubheadlineEmphasized);
        var costSize = CurrencyGlyph.MeasureAmount(costText, TextStyles.Caption1);
        var stackHeight = chipSize.Y + costSize.Y;
        var top = rect.Center.Y - stackHeight * 0.5f;
        CurrencyGlyph.DrawAmount(drawList, new Vector2(rect.Center.X - chipSize.X * 0.5f, top), chipText,
            CurrencyKind.Chips, chipInk, TextStyles.SubheadlineEmphasized, glyphAlpha);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(rect.Center.X - costSize.X * 0.5f, top + chipSize.Y),
            costText, CurrencyKind.Coins, costInk, TextStyles.Caption1, glyphAlpha);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void AskStake(bool sittingOpen, long amount)
    {
        var amountText = NumberText.Group(amount);
        if (sittingOpen)
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = Loc.T(L.Casino.TopUpConfirmTitle, amountText),
                Message = Loc.T(L.Casino.TopUpConfirmBody, amountText),
                ConfirmLabel = Loc.T(L.Casino.TopUp),
                CancelLabel = Loc.T(L.Common.Cancel),
                Danger = false,
                Confirm = () => store.TopUp(amount),
            });
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Casino.BuyInConfirmTitle, amountText),
            Message = Loc.T(L.Casino.BuyInConfirmBody, amountText),
            ConfirmLabel = Loc.T(L.Casino.BuyIn),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Confirm = () => store.OpenSitting(amount),
        });
    }

    private void DrawCashOut(ImDrawListPtr drawList, AppSkin ui, CasinoSittingDto sitting,
        float left, float y, float innerWidth, float scale, bool interactive)
    {
        var stackText = NumberText.Group(sitting.Stack);
        var rect = new Rect(new Vector2(left, y), new Vector2(left + innerWidth, y + PillHeight * scale));
        var canCashOut = interactive && !store.MovingMoney;
        if (RawPill(drawList, rect, Loc.T(L.Casino.CashOutFor, stackText), false, canCashOut, ui, scale))
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = Loc.T(L.Casino.CashOutConfirmTitle, stackText),
                Message = Loc.T(L.Casino.CashOutConfirmBody),
                ConfirmLabel = Loc.T(L.Casino.CashOut),
                CancelLabel = Loc.T(L.Common.Cancel),
                Danger = false,
                Confirm = store.CloseSitting,
            });
        }

        Typography.Draw(drawList, new Vector2(left, y + PillHeight * scale + 4f * scale),
            Loc.T(L.Casino.CashOutHint), ui.MutedInk, TextStyles.Caption1);
    }

    private static bool RawPill(ImDrawListPtr drawList, Rect rect, string label, bool filled, bool enabled,
        AppSkin ui, float scale)
    {
        var rounding = rect.Height * 0.5f;
        var hovered = enabled && UiInteract.HoverWindowOnly(rect.Min, rect.Max);
        var fill = filled
            ? Palette.WithAlpha(ui.Accent, enabled ? 1f : 0.4f)
            : Palette.WithAlpha(ui.FieldSurface, enabled ? 1f : 0.5f);
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(fill));
        if (!filled)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.30f)), 1f * scale);
        }

        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var ink = filled ? ui.Palette.HeaderInk : ui.TitleInk;
        var fitted = Typography.FitText(label, rect.Width - rect.Height, 0.9f, FontWeight.SemiBold);
        var textSize = Typography.Measure(fitted, 0.9f, FontWeight.SemiBold);
        Typography.Draw(drawList, rect.Center - textSize * 0.5f, fitted,
            enabled ? ink : Palette.WithAlpha(ink, 0.6f), 0.9f, FontWeight.SemiBold);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
