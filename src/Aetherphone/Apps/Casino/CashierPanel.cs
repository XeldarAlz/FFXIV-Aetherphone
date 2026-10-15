using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal readonly record struct CashierView(
    CashierMode Mode,
    string Heading,
    CashierField Field,
    bool Editable,
    CashierAmountState State,
    int TileCount,
    string Status,
    CashierTone Tone,
    string Note,
    long Stack,
    long Wallet,
    long Rate,
    long BankrollRate,
    bool Dust,
    CashOutPlan Plan);

internal sealed class CashierPanel
{
    private const float ExchangeFillAlpha = 0.08f;
    private const float ExchangeRimAlpha = 0.10f;
    private const float ExchangeGlyphScale = 0.7f;
    private const float RatePillAlpha = 0.25f;
    private const float ErrorFillAlpha = 0.10f;
    private const float ErrorStrokeAlpha = 0.35f;
    private const float BobLift = 4f;
    private const double BobPeriod = 3200.0;
    private const float SegmentTrackAlpha = 0.3f;
    private const string SegmentId = "casino.cashier.mode";
    private const string SegmentOverlayId = "casino.cashier.mode.overlay";
    private const string ChipsSlot = "{0}";
    private const string CoinsSlot = "{1}";

    private static readonly TextStyle CaptionStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle AmountStyle = TextStyles.Title2;
    private static readonly TextStyle SubStyle = TextStyles.Footnote;
    private static readonly TextStyle RateStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle HeadingStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle StatusStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle NoteStyle = TextStyles.Footnote;
    private static readonly TextStyle TileLabelFloor = TextStyles.FootnoteEmphasized;
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    private readonly CasinoStore store;
    private readonly CoinStore coins;
    private readonly CashierBuyIn buyIn;
    private readonly CashierCashOut cashOut;
    private readonly CashierExtras extras;
    private readonly CashierConfirmSheet sheet = new();
    private readonly CashierRain rain = new();
    private readonly CasinoTextCache texts = new();
    private readonly string[] modeLabels = new string[2];

    private CashierMode mode;
    private string error = string.Empty;
    private int drawnFrame = int.MinValue;
    private int rolledFrame = int.MinValue;
    private RollingAmount walletRoll;
    private RollingAmount stackRoll;
    private LanguageInfo? rateLanguage;
    private long rateValue = -1;
    private string ratePrefix = string.Empty;
    private string rateMiddle = string.Empty;
    private string rateSuffix = string.Empty;
    private bool rateChipsFirst = true;

    public CashierPanel(CasinoStore store, CoinStore coins)
    {
        this.store = store;
        this.coins = coins;
        buyIn = new CashierBuyIn(store);
        cashOut = new CashierCashOut(store, coins);
        extras = new CashierExtras(store);
    }

    public bool Visible => ImGui.GetFrameCount() - drawnFrame <= 1;

    public bool SheetCapturesPointer => sheet.CapturesPointer;

    public void Reset(long suggestedChips)
    {
        ClearNote();
        mode = CashierMode.Buy;
        cashOut.Cancel();
        buyIn.Reset(suggestedChips);
    }

    public void ClearBuy()
    {
        buyIn.Clear();
        rain.Start(CurrencyKind.Chips);
    }

    public void ShowNote(string text, bool good)
    {
        if (good)
        {
            ClearNote();
            ShellToast.Show(text);
            return;
        }

        error = text;
    }

    public void ClearNote()
    {
        error = string.Empty;
    }

    public void CashedOut(string line, long coinsBack)
    {
        ClearNote();
        cashOut.Clear();
        if (coinsBack > 0)
        {
            rain.Start(CurrencyKind.Coins);
        }

        ShellToast.Show(line);
    }

    public void CloseSheet()
    {
        cashOut.Cancel();
        sheet.Close();
    }

    public bool TakeLimitsRequest() => extras.TakeLimitsRequest();

    public string CashOutLine(long convertedCoins) => cashOut.ResultLine(convertedCoins);

    public float Height(float width, float scale) => Compute(0f, 0f, width, scale, out _, out _, out _).Bottom;

    public float Draw(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale,
        bool interactive, bool overlay)
    {
        drawnFrame = ImGui.GetFrameCount();
        var layout = Compute(left, top, width, scale, out var view, out var noticeTitle, out var noticeHint);
        var live = interactive && !sheet.CapturesPointer && !cashOut.Checking;
        Roll(view);
        if (layout.HasNotice)
        {
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Card, noticeTitle, noticeHint, layout.Notice.Min.X,
                layout.Notice.Min.Y, layout.Notice.Width, scale);
        }

        DrawBalances(drawList, ui, layout.Balances, view, scale);
        if (layout.HasSegment)
        {
            DrawSegment(ui, layout.Segment, live, overlay);
        }

        DrawAmount(drawList, ui, layout.Amount, view, live, overlay, scale);
        extras.Draw(drawList, ui, layout.Extras, live, overlay && live, scale);
        return layout.Bottom;
    }

    public void DrawOverlays(Rect screen, AppSkin ui)
    {
        ResolveCheck();
        sheet.Draw(screen, ui, store.MovingMoney);
        if (sheet.TakeConfirmed(out var request))
        {
            Confirm(request);
        }

        rain.Draw(screen, MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
    }

    private long WalletCoins => Math.Max(0, coins.Wallet?.Balance ?? 0);

    private CashierLayout Compute(float left, float top, float width, float scale, out CashierView view,
        out string noticeTitle, out string noticeHint)
    {
        NoticeFor(out noticeTitle, out noticeHint);
        var blocked = noticeTitle.Length > 0;
        view = ViewFor(blocked ? CashierMode.CashOut : mode);
        var notice = blocked
            ? CasinoNotice.Height(CasinoNoticeKind.Card, noticeTitle, noticeHint, width, scale)
            : 0f;
        var panelText = CashierLayout.PanelTextWidth(width, scale);
        var blocks = new CashierBlocks(notice, Typography.LineHeight(CaptionStyle),
            Typography.LineHeight(AmountStyle), Typography.LineHeight(SubStyle), Typography.LineHeight(RateStyle),
            Typography.LineHeight(HeadingStyle),
            CashierLayout.FieldHeightFor(Typography.LineHeight(CashierField.NumberStyle), scale),
            Typography.MeasureWrappedBlock(view.Status, StatusStyle, panelText).Y,
            view.Note.Length > 0 ? Typography.MeasureWrappedBlock(view.Note, NoteStyle, panelText).Y : 0f,
            error.Length > 0
                ? Typography.MeasureWrappedBlock(error, NoteStyle, CashierLayout.ErrorTextWidth(width, scale)).Y
                : 0f,
            CashierExtras.TextHeight(Loc.T(L.Chips.AutoTopUpHint), CashierLayout.AutoTextWidth(width, scale)),
            CashierExtras.TextHeight(Loc.T(L.CashierDesk.LimitsHint), CashierLayout.LimitsTextWidth(width, scale)));
        return CashierLayout.Compute(left, top, width, blocks, !blocked, view.TileCount, scale);
    }

    private CashierView ViewFor(CashierMode shown)
    {
        var stack = cashOut.Stack;
        var rate = store.Rate;
        var bankrollRate = cashOut.Rate;
        var wallet = WalletCoins;
        if (shown == CashierMode.Buy)
        {
            var bounds = new BuyInBounds(wallet, rate);
            var state = buyIn.State(bounds);
            var status = buyIn.Status(bounds, state, out var tone);
            return new CashierView(CashierMode.Buy, Loc.T(L.Strip.CoinsFieldHint), buyIn.Field, true, state,
                buyIn.Tiles(bounds), status, tone, string.Empty, stack, wallet, rate, bankrollRate, false, default);
        }

        var dust = CashierAmounts.IsDust(stack, bankrollRate);
        var partial = cashOut.Partial && !dust;
        cashOut.Sync(stack, bankrollRate, partial);
        var tiles = cashOut.Tiles(ChipValue.Coins(stack, bankrollRate), partial);
        var plan = cashOut.Plan(stack, bankrollRate, partial);
        var line = cashOut.Status(plan, stack, bankrollRate, out var lineTone);
        return new CashierView(CashierMode.CashOut, Loc.T(L.CashierDesk.CashOutHeading), cashOut.Field, partial,
            plan.State, tiles, line, lineTone, Loc.T(L.CashierDesk.CashOutNote), stack, wallet, rate, bankrollRate,
            dust, plan);
    }

    private void NoticeFor(out string title, out string hint)
    {
        title = string.Empty;
        hint = string.Empty;
        var state = store.State;
        if (coins.Wallet?.FrozenUntilUnix is not null)
        {
            title = Loc.T(L.Coin.FrozenTitle);
            hint = Loc.T(L.Coin.FrozenHint);
            return;
        }

        if (state?.StakesPaused == true)
        {
            title = Loc.T(L.Casino.PausedTitle);
            hint = Loc.T(L.Casino.PausedHint);
            return;
        }

        if (state?.Draining == true)
        {
            title = Loc.T(L.Casino.DrainingTitle);
            hint = Loc.T(L.Casino.DrainingHint);
        }
    }

    private void Roll(in CashierView view)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == rolledFrame)
        {
            return;
        }

        rolledFrame = frame;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        walletRoll.Update(view.Wallet, delta);
        stackRoll.Update(view.Stack, delta);
    }

    private void DrawBalances(ImDrawListPtr drawList, AppSkin ui, in CashierBalancesLayout layout,
        in CashierView view, float scale)
    {
        var card = layout.Card;
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        var wave = Pulse.Wave(BobPeriod);
        var lift = BobLift * scale;
        DrawColumn(drawList, ui, layout.WalletColumn, CurrencyKind.Coins, Loc.T(L.Casino.WalletRow),
            walletRoll.Display, ui.TitleInk, Loc.T(L.CashierDesk.CoinsUnit), -lift * wave, scale);
        var worth = WorthText(view.Stack, view.BankrollRate, layout.ChipsColumn.Width);
        DrawColumn(drawList, ui, layout.ChipsColumn, CurrencyKind.Chips, Loc.T(L.Casino.ChipsRow),
            stackRoll.Display, view.Stack > 0 ? CasinoColors.Money : ui.MutedInk, worth, -lift * (1f - wave), scale);
        var exchange = layout.Exchange;
        var radius = exchange.Width * 0.5f;
        drawList.AddCircleFilled(exchange.Center, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, ExchangeFillAlpha)), 32);
        drawList.AddCircle(exchange.Center, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, ExchangeRimAlpha)),
            32, Metrics.Stroke.Hairline);
        AppSkin.Icon(drawList, exchange.Center, IconGlyph.Of(FontAwesomeIcon.ExchangeAlt), ui.BodyInk,
            ExchangeGlyphScale);
        DrawRate(drawList, ui, layout.RateRow, view.Rate, scale);
    }

    private static void DrawColumn(ImDrawListPtr drawList, AppSkin ui, Rect column, CurrencyKind kind, string label,
        long value, Vector4 amountInk, string sub, float bob, float scale)
    {
        var disc = CashierLayout.Disc * scale;
        var gap = CashierLayout.ColumnGap * scale;
        CurrencyGlyph.Draw(drawList, kind, new Vector2(column.Center.X, column.Min.Y + disc * 0.5f + bob), disc);
        var captionTop = column.Min.Y + disc + gap;
        DrawCentered(drawList, column, captionTop, label, ui.BodyInk, CaptionStyle);
        var amountTop = captionTop + Typography.LineHeight(CaptionStyle) + gap;
        var amountHeight = Typography.LineHeight(AmountStyle);
        var amount = NumberText.Group(value);
        var amountScale = Typography.FitScale(amount, column.Width, AmountStyle.Scale, TextStyles.Headline.Scale,
            AmountStyle.Weight);
        if (Typography.Measure(amount, amountScale, AmountStyle.Weight).X > column.Width)
        {
            amount = NumberText.Compact(value);
            amountScale = Typography.FitScale(amount, column.Width, AmountStyle.Scale, TextStyles.Headline.Scale,
                AmountStyle.Weight);
        }

        var amountSize = Typography.Measure(amount, amountScale, AmountStyle.Weight);
        Typography.Draw(drawList, new Vector2(column.Center.X - amountSize.X * 0.5f,
            amountTop + (amountHeight - amountSize.Y) * 0.5f), amount, amountInk, amountScale, AmountStyle.Weight);
        DrawCentered(drawList, column, amountTop + amountHeight + gap, sub, ui.MutedInk, SubStyle);
    }

    private static void DrawCentered(ImDrawListPtr drawList, Rect column, float top, string text, Vector4 ink,
        in TextStyle style)
    {
        var shown = Typography.FitText(text, column.Width, style);
        var size = Typography.Measure(shown, style);
        Typography.Draw(drawList, new Vector2(column.Center.X - size.X * 0.5f, top), shown, ink, style);
    }

    private string WorthText(long stack, long rate, float width)
    {
        var value = ChipValue.Coins(stack, rate);
        var full = texts.Number(L.CashierDesk.ChipsWorth, value);
        return Typography.Measure(full, SubStyle).X <= width ? full : texts.Compact(L.CashierDesk.ChipsWorth, value);
    }

    private void RefreshRate(long rate)
    {
        if (rate == rateValue && ReferenceEquals(rateLanguage, Loc.Current))
        {
            return;
        }

        rateValue = rate;
        rateLanguage = Loc.Current;
        var template = Loc.T(L.CashierDesk.RateLine);
        var chipsAt = template.IndexOf(ChipsSlot, StringComparison.Ordinal);
        var coinsAt = template.IndexOf(CoinsSlot, StringComparison.Ordinal);
        if (chipsAt < 0 || coinsAt < 0)
        {
            ratePrefix = string.Empty;
            rateMiddle = " = ";
            rateSuffix = string.Empty;
            rateChipsFirst = true;
            return;
        }

        rateChipsFirst = chipsAt < coinsAt;
        var first = Math.Min(chipsAt, coinsAt);
        var second = Math.Max(chipsAt, coinsAt);
        ratePrefix = template[..first];
        rateMiddle = template.Substring(first + ChipsSlot.Length, second - first - ChipsSlot.Length);
        rateSuffix = template[(second + CoinsSlot.Length)..];
    }

    private void DrawRate(ImDrawListPtr drawList, AppSkin ui, Rect row, long rate, float scale)
    {
        RefreshRate(rate);
        var chips = NumberText.Group(rate);
        var one = NumberText.Group(1L);
        var firstAmount = rateChipsFirst ? chips : one;
        var secondAmount = rateChipsFirst ? one : chips;
        var firstKind = rateChipsFirst ? CurrencyKind.Chips : CurrencyKind.Coins;
        var secondKind = rateChipsFirst ? CurrencyKind.Coins : CurrencyKind.Chips;
        var padX = CashierLayout.RatePadX * scale;
        var style = RateStyle;
        var width = RateWidth(firstAmount, secondAmount, style);
        if (width + padX * 2f > row.Width)
        {
            style = TextStyles.Caption1;
            width = RateWidth(firstAmount, secondAmount, style);
        }

        var pillWidth = MathF.Min(row.Width, width + padX * 2f);
        var pillMin = new Vector2(row.Center.X - pillWidth * 0.5f, row.Min.Y);
        var pillMax = new Vector2(row.Center.X + pillWidth * 0.5f, row.Max.Y);
        Squircle.Fill(drawList, pillMin, pillMax, row.Height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(Black, RatePillAlpha)));
        var lineTop = row.Center.Y - Typography.Measure(firstAmount, style).Y * 0.5f;
        var x = row.Center.X - MathF.Min(width, row.Width - padX * 2f) * 0.5f;
        x += DrawRatePart(drawList, x, lineTop, ratePrefix, ui.BodyInk, style);
        x += CurrencyGlyph.DrawAmount(drawList, new Vector2(x, lineTop), firstAmount, firstKind, ui.BodyInk, style).X;
        x += DrawRatePart(drawList, x, lineTop, rateMiddle, ui.BodyInk, style);
        x += CurrencyGlyph.DrawAmount(drawList, new Vector2(x, lineTop), secondAmount, secondKind, ui.BodyInk,
            style).X;
        DrawRatePart(drawList, x, lineTop, rateSuffix, ui.BodyInk, style);
    }

    private float RateWidth(string firstAmount, string secondAmount, in TextStyle style) =>
        Typography.Measure(ratePrefix, style).X + CurrencyGlyph.MeasureAmount(firstAmount, style).X +
        Typography.Measure(rateMiddle, style).X + CurrencyGlyph.MeasureAmount(secondAmount, style).X +
        Typography.Measure(rateSuffix, style).X;

    private static float DrawRatePart(ImDrawListPtr drawList, float x, float top, string text, Vector4 ink,
        in TextStyle style)
    {
        if (text.Length == 0)
        {
            return 0f;
        }

        Typography.Draw(drawList, new Vector2(x, top), text, ink, style);
        return Typography.Measure(text, style).X;
    }

    private void DrawSegment(AppSkin ui, Rect row, bool interactive, bool overlay)
    {
        modeLabels[0] = Loc.T(L.Chips.BuyHeading);
        modeLabels[1] = Loc.T(L.Casino.CashOut);
        var picked = SegmentStrip.Draw(overlay ? SegmentOverlayId : SegmentId, row, modeLabels, (int)mode,
            Palette.WithAlpha(Black, SegmentTrackAlpha), ui.Accent, ui.MutedInk, CasinoArt.White,
            CashierLayout.SegmentHeight, HeadingStyle.Scale, overlay && interactive);
        if (!interactive || picked == (int)mode)
        {
            return;
        }

        mode = (CashierMode)picked;
        ClearNote();
    }

    private void DrawAmount(ImDrawListPtr drawList, AppSkin ui, in CashierAmountLayout layout, in CashierView view,
        bool interactive, bool overlay, float scale)
    {
        ui.Card(drawList, layout.Card.Min, layout.Card.Max, Metrics.Radius.Grouped * scale);
        Typography.Draw(drawList, layout.Heading.Min,
            Typography.FitText(view.Heading, layout.Heading.Width, HeadingStyle), ui.TitleInk, HeadingStyle);
        var enabled = interactive && !store.MovingMoney;
        view.Field.Draw(drawList, ui, layout.Field, view.State, view.Editable, enabled);
        if (layout.HasTiles)
        {
            DrawTiles(drawList, ui, layout, view, enabled, overlay);
        }

        Typography.DrawWrappedLeft(layout.Status.Min, view.Status, ToneInk(ui, view.Tone), StatusStyle,
            layout.Status.Width);
        if (layout.HasNote)
        {
            Typography.DrawWrappedLeft(layout.Note.Min, view.Note, ui.MutedInk, NoteStyle, layout.Note.Width);
        }

        if (layout.HasError)
        {
            DrawError(drawList, ui, layout.Error, scale);
        }

        var ready = view.State == CashierAmountState.Ready;
        var label = PrimaryLabel(view, layout.Primary);
        if (Button.Draw(drawList, layout.Primary, label, ui.Ink, ButtonStyle.Prominent,
                enabled: enabled && ready && !cashOut.Checking, overlay: overlay, id: "cashier.primary"))
        {
            Submit(view);
        }
    }

    private void DrawTiles(ImDrawListPtr drawList, AppSkin ui, in CashierAmountLayout layout, in CashierView view,
        bool enabled, bool overlay)
    {
        var style = Button.LabelStyle(layout.TileRow.Height);
        var labelScale = TileLabelScale(layout, view.Mode, style);
        var fieldCoins = view.Field.Coins;
        for (var index = 0; index < layout.TileCount; index++)
        {
            var rect = layout.Tile(index);
            var tileCoins = view.Mode == CashierMode.Buy ? buyIn.Tile(index) : cashOut.Tile(index);
            var live = enabled && (tileCoins > 0 || view.Dust);
            var on = CashierAmounts.TileOn(tileCoins, fieldCoins) || view.Dust;
            var hovered = live && (overlay
                ? UiInteract.HoverWindowOnly(rect.Min, rect.Max)
                : UiInteract.Hover(rect.Min, rect.Max));
            var face = Button.Surface(drawList, rect, ui.Ink, on ? ButtonStyle.Tinted : ButtonStyle.Gray,
                ButtonRole.Normal, live, hovered, ImGui.GetID(TileId(view.Mode, index)));
            if (on)
            {
                Squircle.Stroke(drawList, face.Face.Min, face.Face.Max, face.Face.Height * 0.5f,
                    ImGui.GetColorU32(ui.Accent), Metrics.Stroke.Hairline * 1.5f);
            }

            var shown = Typography.FitText(TileLabel(view.Mode, index), TileLabelRoom(rect), labelScale, style.Weight);
            var size = Typography.Measure(shown, labelScale, style.Weight);
            Typography.Draw(drawList, face.Face.Center - size * 0.5f, shown, on ? ui.TitleInk : face.LabelInk,
                labelScale, style.Weight);
            if (!live || !UiInteract.Click(rect.Min, rect.Max, hovered))
            {
                continue;
            }

            ClearNote();
            if (view.Mode == CashierMode.Buy)
            {
                buyIn.Pick(index);
            }
            else
            {
                cashOut.Pick(index);
            }
        }
    }

    private float TileLabelScale(in CashierAmountLayout layout, CashierMode shown, in TextStyle style)
    {
        var labelScale = style.Scale;
        for (var index = 0; index < layout.TileCount; index++)
        {
            labelScale = MathF.Min(labelScale, Typography.FitScale(TileLabel(shown, index),
                TileLabelRoom(layout.Tile(index)), style.Scale, TileLabelFloor.Scale, style.Weight));
        }

        return labelScale;
    }

    private string TileLabel(CashierMode shown, int index) =>
        shown == CashierMode.Buy ? buyIn.TileLabel(index) : cashOut.TileLabel(index);

    private static float TileLabelRoom(Rect tile) => MathF.Max(1f, tile.Width - tile.Height * 0.5f);

    private static string TileId(CashierMode shown, int index) => shown == CashierMode.Buy
        ? index switch
        {
            0 => "cashier.buy.tile0",
            1 => "cashier.buy.tile1",
            2 => "cashier.buy.tile2",
            _ => "cashier.buy.tile3",
        }
        : index switch
        {
            0 => "cashier.out.tile0",
            1 => "cashier.out.tile1",
            2 => "cashier.out.tile2",
            _ => "cashier.out.tile3",
        };

    private string PrimaryLabel(in CashierView view, Rect button)
    {
        var room = MathF.Max(1f, button.Width - button.Height);
        var style = Button.LabelStyle(button.Height);
        if (view.Mode == CashierMode.Buy)
        {
            var bounds = new BuyInBounds(view.Wallet, view.Rate);
            var full = buyIn.Primary(bounds, view.State, false);
            return Typography.Measure(full, style).X <= room ? full : buyIn.Primary(bounds, view.State, true);
        }

        var whole = cashOut.Primary(view.Plan, false);
        return Typography.Measure(whole, style).X <= room ? whole : cashOut.Primary(view.Plan, true);
    }

    private static Vector4 ToneInk(AppSkin ui, CashierTone tone) => tone switch
    {
        CashierTone.Ready => CasinoColors.Money,
        CashierTone.Warn => ui.Ink.DangerInk,
        _ => ui.MutedInk,
    };

    private void DrawError(ImDrawListPtr drawList, AppSkin ui, Rect rect, float scale)
    {
        var tint = ui.Accent;
        var radius = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, ErrorFillAlpha)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(tint, ErrorStrokeAlpha)), Metrics.Stroke.Hairline);
        var pad = CashierLayout.ErrorPad * scale;
        Typography.DrawWrappedLeft(new Vector2(rect.Min.X + pad, rect.Min.Y + pad), error, ui.TitleInk, NoteStyle,
            rect.Width - pad * 2f);
    }

    private void Submit(in CashierView view)
    {
        ClearNote();
        if (view.Mode == CashierMode.Buy)
        {
            var bounds = new BuyInBounds(view.Wallet, view.Rate);
            sheet.Open(new CashierSheetRequest(CashierMode.Buy, buyIn.Coins, CashOutRoute.None, false),
                buyIn.Preview(bounds, view.Stack), false);
            return;
        }

        cashOut.BeginCheck(view.Plan);
    }

    private void ResolveCheck()
    {
        if (cashOut.Checking && !Visible)
        {
            cashOut.Cancel();
            return;
        }

        if (!cashOut.ResolveCheck(out var route, out var coinsBack, out var atRisk))
        {
            return;
        }

        var outcome = ReplanCashOut(route, coinsBack, out var plan, out var preview);
        if (outcome == CashierConfirmOutcome.Gone)
        {
            return;
        }

        sheet.Open(new CashierSheetRequest(CashierMode.CashOut, plan.Coins, plan.Route, atRisk), preview,
            outcome == CashierConfirmOutcome.Changed);
    }

    private CashierConfirmOutcome ReplanCashOut(CashOutRoute route, long coinsBack, out CashOutPlan plan,
        out CashierPreview preview)
    {
        var stack = cashOut.Stack;
        var rate = cashOut.Rate;
        var partial = cashOut.Partial && !CashierAmounts.IsDust(stack, rate);
        var outcome = CashierAmounts.Reconfirm(route, coinsBack, stack, rate, partial, out plan);
        preview = CashierAmounts.CashOutPreview(plan, rate, WalletCoins, stack);
        return outcome;
    }

    private void Confirm(in CashierSheetRequest request)
    {
        if (request.Mode == CashierMode.Buy)
        {
            ConfirmBuy(request.Coins);
            return;
        }

        var outcome = ReplanCashOut(request.Route, request.Coins, out var plan, out var preview);
        if (outcome == CashierConfirmOutcome.Changed)
        {
            var atRisk = (store.State?.AtRisk ?? 0) > 0;
            sheet.Open(new CashierSheetRequest(CashierMode.CashOut, plan.Coins, plan.Route, atRisk), preview, true);
            return;
        }

        sheet.Close();
        if (outcome == CashierConfirmOutcome.Gone || !cashOut.Execute(plan))
        {
            ShowNote(Loc.T(L.CashierDesk.NothingMoved), false);
        }
    }

    private void ConfirmBuy(long coinsSpent)
    {
        sheet.Close();
        var ready = CashierAmounts.BuyState(coinsSpent, WalletCoins) == CashierAmountState.Ready;
        if (!ready || !store.BuyChips(coinsSpent))
        {
            ShowNote(Loc.T(L.CashierDesk.NothingMoved), false);
        }
    }
}
