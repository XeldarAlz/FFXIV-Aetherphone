using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Coin;
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

    private const float MaxDim = 0.45f;
    private const float GrabberTop = 8f;
    private const float PadX = 18f;
    private const float SectionGap = 12f;
    private const float SummaryRowHeight = 22f;
    private const float CardPad = 12f;
    private const float BottomPad = 18f;

    private readonly CasinoStore store;
    private readonly CoinStore coins;
    private readonly ConfirmService confirm;
    private readonly CashierBonusShelf bonuses;
    private readonly CashierCashOut cashOut;
    private readonly CashierBuyIn buyIn;
    private readonly CasinoTextCache texts = new();

    private Spring reveal;
    private bool open;
    private int openedFrame;
    private string inlineReason = string.Empty;
    private string inlineNote = string.Empty;

    public CashierDrawer(CasinoStore store, CoinStore coins, ConfirmService confirm, CashierBonusShelf bonuses,
        CashierCashOut cashOut)
    {
        this.store = store;
        this.coins = coins;
        this.confirm = confirm;
        this.bonuses = bonuses;
        this.cashOut = cashOut;
        buyIn = new CashierBuyIn(store, confirm);
    }

    public bool IsOpen => open;

    public void Open()
    {
        if (open)
        {
            return;
        }

        open = true;
        openedFrame = ImGui.GetFrameCount();
        UiFeedback.Play(UiSound.SheetPresent);
        inlineReason = string.Empty;
        inlineNote = string.Empty;
        bonuses.ClearNote();
        buyIn.Reset(0);
        store.RefreshNow();
        coins.EnsureFresh();
    }

    public void Open(long suggestedAmount)
    {
        Open();
        buyIn.Reset(suggestedAmount);
    }

    public void Close()
    {
        if (!open)
        {
            return;
        }

        open = false;
        UiFeedback.Play(UiSound.SheetDismiss);
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
        reveal.Step(open ? 1f : 0f, Motion.Sheet, delta);
        if (!open && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            return;
        }

        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##cashierDrawer", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            Material.Veil(drawList, screen.Min, screen.Max, MaxDim * opacity);
            var interactive = open && confirm.Active is null && opacity > 0.5f;
            var panel = DrawPanel(screen, ui, drawList, opacity, interactive);
            bonuses.DrawShower(drawList, UiScale.Current);
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
                buyIn.Clear();
            }

            HandleOutcome(sitting.Reason, openLimits);
        }

        var closed = store.TakeCloseResult();
        if (closed is not null)
        {
            if (closed.Granted)
            {
                UiFeedback.Play(UiSound.Payout);
                inlineNote = cashOut.ResultLine(closed);
            }

            HandleOutcome(closed.Reason, openLimits);
        }

        if (store.TakeMoneyMoveFailure())
        {
            HandleOutcome(CasinoReasons.Unreachable, openLimits);
        }
    }

    private void HandleOutcome(string reason, Action openLimits)
    {
        if (reason.Length == 0)
        {
            inlineReason = string.Empty;
            return;
        }

        inlineNote = string.Empty;
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
        var split = CashOutSplit.Of(state);
        var bounds = BuyInBounds.Of(state, wallet?.Balance ?? 0);

        NoticeFor(frozen, paused, draining, out var noticeTitle, out var noticeHint);
        var note = NoteText();
        var titleHeight = Typography.Measure(Loc.T(L.Casino.Cashier), TextStyles.Headline).Y;
        var summaryHeight = (sittingOpen ? 3 : 2) * SummaryRowHeight * scale + CardPad * 2f * scale;
        var noticeHeight = noticeTitle.Length > 0
            ? NoticeHeight(noticeTitle, noticeHint, innerWidth, scale) + SectionGap * scale
            : 0f;
        var noteHeight = note.Length > 0
            ? Typography.MeasureWrappedBlock(note, TextStyles.Footnote, innerWidth - CardPad * 2f * scale).Y
              + CardPad * 2f * scale + SectionGap * scale
            : 0f;
        var bonusRows = store.HasFeature(CasinoFeatures.Bonus) ? bonuses.RowCount(true) : 0;
        var bonusHeight = bonusRows > 0 ? bonuses.Height(true, scale) + SectionGap * scale : 0f;
        var stakeHeight = stakeBlocked ? 0f : buyIn.Height(scale);
        var cashOutHeight = sittingOpen ? SectionGap * 2f * scale + cashOut.Height(split, innerWidth, scale) : 0f;
        var grabberBlock = (GrabberTop + Metrics.Size.GrabberHeight + Metrics.Space.Md) * scale;
        var panelHeight = grabberBlock + titleHeight + SectionGap * scale + summaryHeight + SectionGap * scale
            + noticeHeight + noteHeight + bonusHeight + stakeHeight + cashOutHeight + BottomPad * scale;
        panelHeight = MathF.Min(panelHeight, screen.Height - Metrics.Space.Xl * scale);

        var panelBottom = screen.Max.Y + panelHeight * (1f - slide);
        var panelTop = panelBottom - panelHeight;
        var panelMin = new Vector2(screen.Min.X, panelTop);
        var panelMax = new Vector2(screen.Max.X, panelBottom);
        var rounding = ui.Theme.ScreenRounding * scale;
        var skin = CasinoArt.Sheet(ui);
        Squircle.Fill(drawList, panelMin, panelMax, rounding, ImGui.GetColorU32(skin.Panel));
        Squircle.Stroke(drawList, panelMin, panelMax, rounding, ImGui.GetColorU32(skin.Stroke),
            Metrics.Stroke.Hairline);
        var grabberWidth = Metrics.Size.GrabberWidth * scale;
        var grabberHeight = Metrics.Size.GrabberHeight * scale;
        var grabberMin = new Vector2(screen.Center.X - grabberWidth * 0.5f, panelTop + GrabberTop * scale);
        drawList.AddRectFilled(grabberMin, grabberMin + new Vector2(grabberWidth, grabberHeight),
            ImGui.GetColorU32(skin.Grabber), grabberHeight * 0.5f);

        var left = panelMin.X + PadX * scale;
        var y = panelTop + grabberBlock;
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, y + titleHeight * 0.5f),
            Loc.T(L.Casino.Cashier), ui.TitleInk, TextStyles.Headline);
        y += titleHeight + SectionGap * scale;

        y = DrawSummary(drawList, ui, state, wallet, sittingOpen, left, y, innerWidth, scale);
        y += SectionGap * scale;

        if (noticeTitle.Length > 0)
        {
            y = DrawNotice(drawList, ui, noticeTitle, noticeHint, left, y, innerWidth, scale) + SectionGap * scale;
        }

        if (note.Length > 0)
        {
            y = DrawNote(drawList, ui, note, NoteIsGood(), left, y, innerWidth, scale) + SectionGap * scale;
        }

        if (bonusRows > 0)
        {
            y = bonuses.Draw(drawList, ui, left, y, innerWidth, scale, true, true, interactive) + SectionGap * scale;
        }

        if (!stakeBlocked)
        {
            y = buyIn.Draw(drawList, ui, bounds, left, y, innerWidth, scale, interactive);
        }

        if (sittingOpen)
        {
            y += SectionGap * scale;
            CoinArt.Hairline(drawList, ui, left, left + innerWidth, y);
            y += SectionGap * scale;
            cashOut.Draw(drawList, ui, split, left, y, innerWidth, scale, true, interactive);
        }

        return new Rect(panelMin, panelMax);
    }

    private string NoteText()
    {
        if (inlineReason.Length > 0)
        {
            return CasinoReasons.Text(inlineReason, store.Ceiling.MaxBet);
        }

        if (bonuses.Note.Length > 0)
        {
            return bonuses.Note;
        }

        return inlineNote;
    }

    private bool NoteIsGood()
    {
        return inlineReason.Length == 0 && (bonuses.Note.Length == 0 || bonuses.NoteIsGrant);
    }

    private static void NoticeFor(bool frozen, bool paused, bool draining, out string title, out string hint)
    {
        title = string.Empty;
        hint = string.Empty;
        if (frozen)
        {
            title = Loc.T(L.Coin.FrozenTitle);
            hint = Loc.T(L.Coin.FrozenHint);
            return;
        }

        if (paused)
        {
            title = Loc.T(L.Casino.PausedTitle);
            hint = Loc.T(L.Casino.PausedHint);
            return;
        }

        if (draining)
        {
            title = Loc.T(L.Casino.DrainingTitle);
            hint = Loc.T(L.Casino.DrainingHint);
        }
    }

    private float DrawSummary(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto? state, CoinWalletDto? wallet,
        bool sittingOpen, float left, float y, float innerWidth, float scale)
    {
        var rows = sittingOpen ? 3 : 2;
        var height = rows * SummaryRowHeight * scale + CardPad * 2f * scale;
        var min = new Vector2(left, y);
        var max = new Vector2(left + innerWidth, y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);

        var rowY = min.Y + CardPad * scale;
        DrawSummaryRow(drawList, ui, Loc.T(L.Casino.WalletRow), NumberText.Group(wallet?.Balance ?? 0),
            CurrencyKind.Coins, left, rowY, innerWidth, scale, ui.TitleInk);
        rowY += SummaryRowHeight * scale;

        if (sittingOpen)
        {
            DrawSummaryRow(drawList, ui, Loc.T(L.Casino.ChipsRow), NumberText.Group(state?.Sitting?.Stack ?? 0),
                CurrencyKind.Chips, left, rowY, innerWidth, scale, CasinoColors.Money);
            rowY += SummaryRowHeight * scale;
        }

        var net = state?.NetLossToday ?? 0;
        var tonight = net switch
        {
            > 0 => texts.Number(L.Casino.TonightDown, net),
            < 0 => texts.Number(L.Casino.TonightUp, -net),
            _ => Loc.T(L.Casino.TonightEven),
        };
        Typography.Draw(drawList, new Vector2(left + CardPad * scale, rowY + 2f * scale),
            Typography.FitText(tonight, innerWidth - CardPad * 2f * scale, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        return max.Y;
    }

    private static void DrawSummaryRow(ImDrawListPtr drawList, AppSkin ui, string label, string value,
        CurrencyKind kind, float left, float rowY, float innerWidth, float scale, Vector4 valueInk)
    {
        var valueSize = CurrencyGlyph.MeasureAmount(value, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left + CardPad * scale, rowY),
            Typography.FitText(label, innerWidth - CardPad * 3f * scale - valueSize.X, TextStyles.Subheadline),
            ui.BodyInk, TextStyles.Subheadline);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(left + innerWidth - CardPad * scale - valueSize.X, rowY),
            value, kind, valueInk, TextStyles.SubheadlineEmphasized);
    }

    private static float NoticeHeight(string title, string hint, float innerWidth, float scale)
    {
        var pad = CardPad * scale;
        return Typography.Measure(title, TextStyles.FootnoteEmphasized).Y
               + Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, innerWidth - pad * 2f).Y + pad * 2f
               + 6f * scale;
    }

    private static float DrawNotice(ImDrawListPtr drawList, AppSkin ui, string title, string hint, float left,
        float y, float innerWidth, float scale)
    {
        var pad = CardPad * scale;
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        var min = new Vector2(left, y);
        var max = new Vector2(left + innerWidth, y + NoticeHeight(title, hint, innerWidth, scale));
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), title, ui.Accent,
            TextStyles.FootnoteEmphasized);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad + titleSize.Y + 6f * scale), hint,
            ui.MutedInk, TextStyles.Footnote, innerWidth - pad * 2f);
        return max.Y;
    }

    private static float DrawNote(ImDrawListPtr drawList, AppSkin ui, string message, bool good, float left, float y,
        float innerWidth, float scale)
    {
        var pad = CardPad * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, innerWidth - pad * 2f);
        var min = new Vector2(left, y);
        var max = new Vector2(left + innerWidth, y + block.Y + pad * 2f);
        var tint = good ? CasinoColors.Money : ui.Accent;
        Squircle.Fill(drawList, min, max, Metrics.Radius.Grouped * scale,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.10f)));
        Squircle.Stroke(drawList, min, max, Metrics.Radius.Grouped * scale,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.35f)), 1f * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), message, ui.TitleInk, TextStyles.Footnote,
            innerWidth - pad * 2f);
        return max.Y;
    }
}
