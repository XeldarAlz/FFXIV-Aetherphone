using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal readonly record struct CashierSheetRequest(CashierMode Mode, long Coins, CashOutRoute Route, bool AtRisk);

internal sealed class CashierConfirmSheet
{
    private const int RowCount = 4;
    private const float MinShare = 0.3f;
    private const float MaxShare = 0.92f;
    private const float GlyphSize = 44f;
    private const float FlowWidth = 44f;
    private const float FlowDot = 8f;
    private const float FlowTravel = 18f;
    private const float FlowFade = 0.3f;
    private const double FlowPeriod = 1200.0;
    private const float SectionGap = 14f;
    private const float RowPadX = 14f;
    private const float RowPadY = 12f;
    private const float RowsRadius = 18f;
    private const float DividerAlpha = 0.07f;
    private const float ConfirmHeight = 52f;
    private const float CancelHeight = 48f;
    private const float ButtonGap = 10f;
    private const float RiskPad = 12f;
    private const float RiskGap = 4f;
    private const float RiskFillAlpha = 0.10f;
    private const float RiskStrokeAlpha = 0.35f;
    private const float BottomPad = 4f;

    private static readonly Vector4 RowsFill = new(0f, 0f, 0f, 0.28f);
    private static readonly TextStyle TitleStyle = TextStyles.Title2;
    private static readonly TextStyle LabelStyle = TextStyles.Subheadline;
    private static readonly TextStyle ValueStyle = TextStyles.Headline;

    private readonly SheetSurface sheet = new("casino.cashier.confirm");
    private readonly Action<Rect> drawBody;
    private readonly CasinoTextCache texts = new();
    private readonly string[] labels = new string[RowCount];
    private readonly string[] values = new string[RowCount];

    private AppSkin skin = null!;
    private CashierSheetRequest request;
    private CashierPreview preview;
    private bool busy;
    private bool confirmed;
    private bool changed;

    public CashierConfirmSheet()
    {
        drawBody = DrawBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public bool CapturesPointer => sheet.CapturesPointer;

    public void Open(in CashierSheetRequest next, in CashierPreview nextPreview, bool amountChanged)
    {
        request = next;
        preview = nextPreview;
        changed = amountChanged;
        confirmed = false;
        sheet.Open();
    }

    public void Close()
    {
        sheet.Close();
    }

    public bool TakeConfirmed(out CashierSheetRequest taken)
    {
        taken = request;
        if (!confirmed)
        {
            return false;
        }

        confirmed = false;
        return true;
    }

    public void Draw(Rect screen, AppSkin ui, bool moving)
    {
        if (!sheet.CapturesPointer)
        {
            return;
        }

        skin = ui;
        busy = moving;
        FillRows();
        var scale = UiScale.Current;
        var width = screen.Width - Metrics.Space.Lg * 2f * scale;
        var share = Math.Clamp(
            (SheetSurface.ChromeHeight() + ContentHeight(width, scale) + Metrics.Space.Lg * 2f * scale) /
            MathF.Max(1f, screen.Height), MinShare, MaxShare);
        sheet.Draw(screen, CasinoArt.Sheet(ui), string.Empty, share, drawBody);
    }

    private bool Buying => request.Mode == CashierMode.Buy;

    private bool ShowsRisk => !Buying && request.AtRisk && request.Route == CashOutRoute.Whole;

    private string Title => Buying
        ? texts.Number(L.CashierDesk.BuyTitle, preview.Get)
        : texts.Number(L.CashierDesk.CashOutTitle, preview.Get);

    private void FillRows()
    {
        if (Buying)
        {
            labels[0] = Loc.T(L.CashierDesk.YouPay);
            values[0] = texts.Number(L.Strip.CoinsAmount, preview.Pay);
            labels[1] = Loc.T(L.Chips.YouGet);
            values[1] = texts.Number(L.CashierDesk.ChipsAmount, preview.Get);
            labels[2] = Loc.T(L.CashierDesk.WalletAfter);
            values[2] = texts.Number(L.Strip.CoinsAmount, preview.WalletAfter);
            labels[3] = Loc.T(L.CashierDesk.ChipsAfter);
            values[3] = NumberText.Group(preview.ChipsAfter);
            return;
        }

        labels[0] = Loc.T(L.CashierDesk.ChipsTaken);
        values[0] = NumberText.Group(preview.Pay);
        labels[1] = Loc.T(L.Chips.YouGet);
        values[1] = texts.Number(L.Strip.CoinsAmount, preview.Get);
        labels[2] = Loc.T(L.CashierDesk.ChipsLeft);
        values[2] = NumberText.Group(preview.ChipsAfter);
        labels[3] = Loc.T(L.CashierDesk.WalletAfter);
        values[3] = texts.Number(L.Strip.CoinsAmount, preview.WalletAfter);
    }

    private static float RowHeight(float scale) =>
        MathF.Max(Typography.LineHeight(LabelStyle), Typography.LineHeight(ValueStyle)) + RowPadY * 2f * scale;

    private float TitleHeight(float width) =>
        Typography.WrapText(Title, TitleStyle, width).Length * Typography.LineHeight(TitleStyle);

    private static float NoticeHeight(string title, string body, float width, float scale)
    {
        var text = width - RiskPad * 2f * scale;
        var titleBlock = title.Length > 0
            ? Typography.LineHeight(TextStyles.SubheadlineEmphasized) + RiskGap * scale
            : 0f;
        return RiskPad * 2f * scale + titleBlock + Typography.MeasureWrappedBlock(body, TextStyles.Footnote, text).Y;
    }

    private float RiskHeight(float width, float scale) => ShowsRisk
        ? NoticeHeight(Loc.T(L.Chips.RoundOpenTitle), Loc.T(L.Chips.RoundOpenBody), width, scale)
        : 0f;

    private float ChangedHeight(float width, float scale) => changed
        ? NoticeHeight(string.Empty, Loc.T(L.CashierDesk.AmountChanged), width, scale)
        : 0f;

    private float ContentHeight(float width, float scale)
    {
        var gap = SectionGap * scale;
        var risk = RiskHeight(width, scale);
        var moved = ChangedHeight(width, scale);
        return GlyphSize * scale + gap + TitleHeight(width) + gap + (moved > 0f ? moved + gap : 0f) +
               RowHeight(scale) * RowCount + (risk > 0f ? risk + gap : 0f) + gap + ConfirmHeight * scale +
               ButtonGap * scale + CancelHeight * scale + BottomPad * scale;
    }

    private void DrawBody(Rect content)
    {
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##cashierConfirmBody", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.NativeScrollContentWidth();
            var gap = SectionGap * scale;
            var y = origin.Y;
            DrawFlow(drawList, new Vector2(origin.X + width * 0.5f, y + GlyphSize * scale * 0.5f), scale);
            y += GlyphSize * scale + gap;
            y = DrawTitle(drawList, origin.X, y, width) + gap;
            var moved = ChangedHeight(width, scale);
            if (moved > 0f)
            {
                DrawNotice(drawList, new Rect(new Vector2(origin.X, y), new Vector2(origin.X + width, y + moved)),
                    string.Empty, Loc.T(L.CashierDesk.AmountChanged), scale);
                y += moved + gap;
            }

            y = DrawRows(drawList, origin.X, y, width, scale) + gap;
            var risk = RiskHeight(width, scale);
            if (risk > 0f)
            {
                DrawNotice(drawList, new Rect(new Vector2(origin.X, y), new Vector2(origin.X + width, y + risk)),
                    Loc.T(L.Chips.RoundOpenTitle), Loc.T(L.Chips.RoundOpenBody), scale);
                y += risk + gap;
            }

            var confirm = new Rect(new Vector2(origin.X, y), new Vector2(origin.X + width, y + ConfirmHeight * scale));
            var action = Buying ? Loc.T(L.Chips.BuyHeading) : Loc.T(L.Casino.CashOut);
            if (Button.Draw(drawList, confirm, action, skin.Ink, ButtonStyle.Prominent,
                    enabled: sheet.IsOpen && !busy && !confirmed, overlay: true, id: "cashier.confirm.go"))
            {
                confirmed = true;
            }

            var cancelTop = confirm.Max.Y + ButtonGap * scale;
            var cancel = new Rect(new Vector2(origin.X, cancelTop),
                new Vector2(origin.X + width, cancelTop + CancelHeight * scale));
            if (Button.Draw(drawList, cancel, Loc.T(L.Common.Cancel), skin.Ink, ButtonStyle.Gray,
                    enabled: sheet.IsOpen, overlay: true, id: "cashier.confirm.cancel"))
            {
                sheet.Close();
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, MathF.Max(1f, cancel.Max.Y + BottomPad * scale - origin.Y)));
        }
    }

    private void DrawFlow(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var glyph = GlyphSize * scale;
        var reach = (FlowWidth * 0.5f + SectionGap) * scale + glyph * 0.5f;
        var from = Buying ? CurrencyKind.Coins : CurrencyKind.Chips;
        var to = Buying ? CurrencyKind.Chips : CurrencyKind.Coins;
        CurrencyGlyph.Draw(drawList, from, new Vector2(center.X - reach, center.Y), glyph);
        CurrencyGlyph.Draw(drawList, to, new Vector2(center.X + reach, center.Y), glyph);
        var phase = Pulse.Phase(FlowPeriod);
        DrawDot(drawList, center, phase, skin.Accent, scale);
        DrawDot(drawList, center, (phase + 0.5f) % 1f, CasinoColors.Money, scale);
    }

    private static void DrawDot(ImDrawListPtr drawList, Vector2 center, float phase, Vector4 ink, float scale)
    {
        var alpha = phase < FlowFade ? phase / FlowFade : phase > 1f - FlowFade ? (1f - phase) / FlowFade : 1f;
        var x = center.X + (phase * 2f - 1f) * FlowTravel * scale;
        drawList.AddCircleFilled(new Vector2(x, center.Y), FlowDot * 0.5f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ink, alpha)), 16);
    }

    private float DrawTitle(ImDrawListPtr drawList, float left, float top, float width)
    {
        var lines = Typography.WrapText(Title, TitleStyle, width);
        var lineHeight = Typography.LineHeight(TitleStyle);
        var y = top;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var size = Typography.Measure(lines[lineIndex], TitleStyle);
            Typography.Draw(drawList, new Vector2(left + (width - size.X) * 0.5f, y + (lineHeight - size.Y) * 0.5f),
                lines[lineIndex], skin.TitleInk, TitleStyle);
            y += lineHeight;
        }

        return y;
    }

    private float DrawRows(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var rowHeight = RowHeight(scale);
        var card = new Rect(new Vector2(left, top), new Vector2(left + width, top + rowHeight * RowCount));
        Squircle.Fill(drawList, card.Min, card.Max, RowsRadius * scale, ImGui.GetColorU32(RowsFill));
        var pad = RowPadX * scale;
        var divider = ImGui.GetColorU32(Palette.WithAlpha(skin.TitleInk, DividerAlpha));
        for (var rowIndex = 0; rowIndex < RowCount; rowIndex++)
        {
            var rowTop = top + rowIndex * rowHeight;
            if (rowIndex > 0)
            {
                drawList.AddLine(new Vector2(left + pad, rowTop), new Vector2(left + width - pad, rowTop), divider,
                    Metrics.Stroke.Hairline);
            }

            var centerY = rowTop + rowHeight * 0.5f;
            var room = width - pad * 2f;
            var value = Typography.FitText(values[rowIndex], room * 0.6f, ValueStyle);
            var valueSize = Typography.Measure(value, ValueStyle);
            Typography.Draw(drawList, new Vector2(left + width - pad - valueSize.X, centerY - valueSize.Y * 0.5f),
                value, InkFor(rowIndex), ValueStyle);
            var label = Typography.FitText(labels[rowIndex], MathF.Max(1f, room - valueSize.X - pad), LabelStyle);
            var labelSize = Typography.Measure(label, LabelStyle);
            Typography.Draw(drawList, new Vector2(left + pad, centerY - labelSize.Y * 0.5f), label, skin.MutedInk,
                LabelStyle);
        }

        return card.Max.Y;
    }

    private Vector4 InkFor(int rowIndex) => rowIndex switch
    {
        0 => skin.TitleInk,
        1 => CasinoColors.Money,
        _ => skin.BodyInk,
    };

    private void DrawNotice(ImDrawListPtr drawList, Rect rect, string title, string body, float scale)
    {
        var radius = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(skin.Accent, RiskFillAlpha)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(skin.Accent, RiskStrokeAlpha)), Metrics.Stroke.Hairline);
        var pad = RiskPad * scale;
        var textWidth = rect.Width - pad * 2f;
        var bodyTop = rect.Min.Y + pad;
        if (title.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(rect.Min.X + pad, bodyTop),
                Typography.FitText(title, textWidth, TextStyles.SubheadlineEmphasized), skin.TitleInk,
                TextStyles.SubheadlineEmphasized);
            bodyTop += Typography.LineHeight(TextStyles.SubheadlineEmphasized) + RiskGap * scale;
        }

        Typography.DrawWrappedLeft(new Vector2(rect.Min.X + pad, bodyTop), body, skin.BodyInk, TextStyles.Footnote,
            textWidth);
    }
}
