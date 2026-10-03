using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Market;

internal sealed partial class MarketApp
{
    private const long MaxPrice = 999_999_999L;
    private const int PriceMaxLength = 13;
    private const float SheetPadX = 20f;
    private const float SheetTitleHeight = 30f;
    private const float SheetSubtitleHeight = 22f;
    private const float SheetGap = 14f;
    private const float SheetStripHeight = 32f;
    private const float SheetButtonHeight = 48f;
    private const float SheetRowHeight = 48f;
    private const float SheetListHeader = 30f;
    private const float StepperRadius = 18f;
    private const float StepperGap = 10f;
    private const float SheetMutedAlpha = 0.62f;
    private const float SheetHairlineAlpha = 0.12f;
    private const float StepperFillAlpha = 0.10f;
    private const int SheetMaxRows = 4;

    private readonly Sheet alertSheet = new();
    private readonly string[] directionLabels = new string[2];
    private readonly List<MarketAlert> itemAlerts = new();
    private MarketView alertView;
    private MarketScope alertScope;
    private bool alertBelow = true;
    private bool alertHq;
    private long alertCheapest;
    private string alertPrice = string.Empty;
    private CachedText alertExplain;
    private CachedText alertContext;

    private void OpenAlertSheet(MarketView view, MarketScope scope)
    {
        alertView = view;
        alertScope = scope;
        alertBelow = true;
        var snapshot = market.RequestItem(view.ItemId, scope, false).Snapshot;
        alertHq = snapshot is not null && ResolveQuality(snapshot);
        alertCheapest = snapshot?.Min(alertHq) ?? 0;
        var start = alertCheapest > 0 ? alertCheapest : chartMedianWeek;
        alertPrice = start > 0 ? start.ToString(CultureInfo.InvariantCulture) : string.Empty;
        alertExplain.Reset();
        alertSheet.Open();
    }

    private void DrawAlertSheet(Rect screen)
    {
        if (!alertSheet.CapturesPointer)
        {
            return;
        }

        var scale = UiScale.Current;
        alerts.CopyInto(itemAlerts);
        for (var alertIndex = itemAlerts.Count - 1; alertIndex >= 0; alertIndex--)
        {
            if (itemAlerts[alertIndex].ItemId != alertView.ItemId)
            {
                itemAlerts.RemoveAt(alertIndex);
            }
        }

        var listed = Math.Min(itemAlerts.Count, SheetMaxRows);
        var explainWidth = screen.Width - SheetPadX * 2f * scale;
        var explainHeight = MathF.Max(Typography.LineHeight(TextStyles.Footnote) * 2f,
            Typography.MeasureWrappedBlock(AlertExplain(Math.Min(MarketTrend.ParseGil(alertPrice), MaxPrice)),
                TextStyles.Footnote, explainWidth).Y);
        var height = (SheetMetrics.GrabberZone + SheetTitleHeight + SheetSubtitleHeight + SheetGap + SheetStripHeight +
                      SheetGap + GlassField.HeightUnits + SheetGap + SheetGap + SheetButtonHeight +
                      Metrics.Size.HomeIndicatorInset) * scale + explainHeight +
                     (listed > 0 ? (SheetListHeader + listed * SheetRowHeight) * scale : 0f);
        using var layer = ScreenLayer.Begin("market.alertSheet", screen, false);
        var frame = alertSheet.Begin(ImGui.GetWindowDrawList(), screen, theme, SheetDetents.Fitted(height),
            SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        var drawList = frame.DrawList;
        var content = frame.Content;
        var ink = frame.Ink with { W = frame.Ink.W * frame.Opacity };
        var muted = ink with { W = ink.W * SheetMutedAlpha };
        var left = content.Min.X + SheetPadX * scale;
        var right = content.Max.X - SheetPadX * scale;
        var width = right - left;
        var y = content.Min.Y;
        var title = Loc.T(L.Market.NewAlert);
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, y + SheetTitleHeight * scale * 0.5f), title,
            ink, TextStyles.Headline);
        y += SheetTitleHeight * scale;
        var subtitle = Typography.FitText(alertView.Name, width, TextStyles.Subheadline);
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, y + SheetSubtitleHeight * scale * 0.5f),
            subtitle, muted, TextStyles.Subheadline);
        y += (SheetSubtitleHeight + SheetGap) * scale;
        directionLabels[0] = Loc.T(L.Market.AtOrBelow);
        directionLabels[1] = Loc.T(L.Market.AtOrAbove);
        var strip = new Rect(new Vector2(left, y), new Vector2(right, y + SheetStripHeight * scale));
        var direction = SegmentStrip.Draw("market.alertDirection", strip, directionLabels, alertBelow ? 0 : 1,
            ui.Palette, SheetStripHeight);
        if (direction != (alertBelow ? 0 : 1))
        {
            UiFeedback.Play(UiSound.Tap);
            alertBelow = direction == 0;
        }

        y += (SheetStripHeight + SheetGap) * scale;
        var threshold = Math.Min(MarketTrend.ParseGil(alertPrice), MaxPrice);
        y = DrawPriceStepper(drawList, left, right, y, threshold, ink, scale);
        y += SheetGap * scale;
        threshold = Math.Min(MarketTrend.ParseGil(alertPrice), MaxPrice);
        var explain = AlertExplain(threshold);
        var explainBottom = Typography.DrawWrappedCentered(drawList, explain, TextStyles.Footnote, muted,
            new Vector2(content.Center.X, y), width);
        y = MathF.Max(explainBottom, y + explainHeight) + SheetGap * scale;
        var button = new Rect(new Vector2(left, y), new Vector2(right, y + SheetButtonHeight * scale));
        if (ui.AccentPill(button, Loc.T(L.Market.CreateAlert), threshold > 0, TextStyles.Headline))
        {
            CreateAlert(threshold);
        }

        y = button.Max.Y;
        if (listed > 0)
        {
            DrawSheetAlerts(drawList, left, right, y, listed, ink, muted, scale);
        }

        alertSheet.End(in frame);
    }

    private float DrawPriceStepper(ImDrawListPtr drawList, float left, float right, float top, long threshold,
        Vector4 ink, float scale)
    {
        var height = GlassField.HeightUnits * scale;
        var radius = StepperRadius * scale;
        var centerY = top + height * 0.5f;
        var minusCenter = new Vector2(left + radius, centerY);
        var plusCenter = new Vector2(right - radius, centerY);
        var step = MarketTrend.Step(Math.Max(threshold, alertCheapest));
        var fill = Palette.WithAlpha(ink, StepperFillAlpha);
        if (HoverButton.Circle(drawList, "market.alertMinus", minusCenter, radius, FontAwesomeIcon.Minus, fill,
                ink, ImGui.GetIO().DeltaTime, 1f, threshold > step, Loc.T(L.Market.Lower), HoverLabelSide.Above))
        {
            UiFeedback.Play(UiSound.Tap);
            alertPrice = Math.Max(1L, threshold - step).ToString(CultureInfo.InvariantCulture);
        }

        if (HoverButton.Circle(drawList, "market.alertPlus", plusCenter, radius, FontAwesomeIcon.Plus, fill,
                ink, ImGui.GetIO().DeltaTime, 1f, threshold < MaxPrice, Loc.T(L.Market.Higher), HoverLabelSide.Above))
        {
            UiFeedback.Play(UiSound.Tap);
            alertPrice = Math.Min(MaxPrice, threshold + step).ToString(CultureInfo.InvariantCulture);
        }

        var field = new Rect(new Vector2(minusCenter.X + radius + StepperGap * scale, top),
            new Vector2(plusCenter.X - radius - StepperGap * scale, top + height));
        Material.ThemedGlass(drawList, field.Min, field.Max, GlassField.Radius(field), scale, theme);
        GlassField.Text(field, "##marketAlertPrice", Loc.T(L.Market.GilPrice), ref alertPrice, theme, scale,
            PriceMaxLength, false, ImGuiInputTextFlags.CharsDecimal);
        return field.Max.Y;
    }

    private void DrawSheetAlerts(ImDrawListPtr drawList, float left, float right, float top, int listed,
        Vector4 ink, Vector4 muted, float scale)
    {
        var headerHeight = SheetListHeader * scale;
        var headerTextTop = top + headerHeight - Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(left, headerTextTop),
            Loc.T(L.Market.ExistingAlerts), muted, TextStyles.FootnoteEmphasized);
        var rowTop = top + headerHeight;
        var rowHeight = SheetRowHeight * scale;
        var hairline = ImGui.GetColorU32(ink with { W = ink.W * SheetHairlineAlpha });
        MarketAlert? removed = null;
        for (var alertIndex = 0; alertIndex < listed; alertIndex++)
        {
            var alert = itemAlerts[alertIndex];
            var rowMin = new Vector2(left, rowTop + alertIndex * rowHeight);
            drawList.AddLine(rowMin, new Vector2(right, rowMin.Y), hairline, Metrics.Stroke.Hairline);
            var centerY = rowMin.Y + rowHeight * 0.5f;
            var removeCenter = new Vector2(right - RemoveRadius * scale, centerY);
            if (HoverButton.Circle(drawList, AlertId(alertIndex), removeCenter, RemoveGlyphRadius * scale,
                    FontAwesomeIcon.Trash, Palette.WithAlpha(ink, 0f), muted, ImGui.GetIO().DeltaTime, 1f, true,
                    Loc.T(L.Market.RemoveAlert), HoverLabelSide.Above))
            {
                removed = alert;
            }

            var textWidth = MathF.Max(1f, removeCenter.X - RemoveRadius * scale - MarketArt.ValueGap * scale - left);
            var rule = Typography.FitText(MarketText.Rule(alert), textWidth, TextStyles.Body);
            Typography.Draw(drawList, new Vector2(left, centerY - Typography.LineHeight(TextStyles.Body) * 0.5f), rule,
                alert.Enabled ? ink : muted, TextStyles.Body);
        }

        if (removed is null)
        {
            return;
        }

        UiFeedback.Play(UiSound.ToggleOff);
        alerts.Remove(removed);
    }

    private void CreateAlert(long threshold)
    {
        alerts.Add(new MarketAlert
        {
            ItemId = alertView.ItemId,
            ItemName = alertView.Name,
            IconId = alertView.IconId,
            ScopeKind = alertScope.Kind,
            ScopeName = alertScope.ApiName,
            HqOnly = alertHq,
            Threshold = threshold,
            Below = alertBelow,
            Enabled = true,
        });
        UiFeedback.Play(UiSound.Success);
        alertSheet.Close();
    }

    private string AlertExplain(long threshold)
    {
        var key = (threshold << 2) ^ (alertBelow ? 1L : 0L) ^ (alertHq ? 2L : 0L) ^
                  ((long)alertScope.ApiName.GetHashCode() << 40);
        if (alertExplain.IsCurrent(key))
        {
            return alertExplain.Value;
        }

        var price = threshold > 0 ? MarketFormat.Gil(threshold) : "-";
        var entry = alertBelow
            ? alertHq ? L.Market.AlertExplainBelowHq : L.Market.AlertExplainBelow
            : alertHq ? L.Market.AlertExplainAboveHq : L.Market.AlertExplainAbove;
        var text = Loc.T(entry, alertScope.ApiName, price);
        if (alertCheapest > 0)
        {
            text = string.Concat(text, " ", AlertContext());
        }

        return alertExplain.Store(key, text);
    }

    private string AlertContext()
    {
        if (alertContext.IsCurrent(alertCheapest))
        {
            return alertContext.Value;
        }

        return alertContext.Store(alertCheapest, Loc.T(L.Market.CheapestNow, MarketFormat.Gil(alertCheapest)));
    }
}
