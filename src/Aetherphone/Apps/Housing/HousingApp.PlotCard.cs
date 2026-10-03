using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private const float CardPad = 16f;
    private const float CardGap = 12f;
    private const float CardStatGap = 14f;
    private const float CardActionHeight = 44f;
    private const float CardActionGap = 8f;
    private const float CardSwatch = 12f;
    private const float CardCloseRadius = 13f;
    private const float CardRadius = 28f;

    private HousingPlotKey selectedPlot;
    private bool plotCardOpen;
    private Spring plotCardShown;
    private CachedText cardCountdown;
    private CachedText cardSubtitle;
    private CachedText cardTitle;
    private CachedText cardEntries;
    private CachedText cardPrice;

    private void SelectPlot(HousingPlotKey key)
    {
        if (selectedPlot == key && plotCardOpen)
        {
            return;
        }

        selectedPlot = key;
        plotCardOpen = true;
        legendOpen = false;
        UiFeedback.Play(UiSound.SheetPresent);
    }

    private void ClosePlotCard(bool immediate = false)
    {
        if (plotCardOpen && !immediate)
        {
            UiFeedback.Play(UiSound.SheetDismiss);
        }

        plotCardOpen = false;
        if (!immediate)
        {
            return;
        }

        selectedPlot = default;
        plotCardShown.SnapTo(0f);
    }

    private void StepPlotCard(float delta)
    {
        plotCardShown.Step(plotCardOpen ? 1f : 0f, Motion.Sheet, delta);
        if (!plotCardOpen && plotCardShown.Value <= 0.001f)
        {
            selectedPlot = default;
        }
    }

    private static float PlotCardHeight(float scale) =>
        CardPad * 2f * scale + Typography.LineHeight(TextStyles.Title3) + HousingArt.LineGap * scale +
        Typography.LineHeight(TextStyles.Footnote) + CardGap * scale + Typography.LineHeight(TextStyles.Subheadline) +
        HousingArt.LineGap * 3f * scale + HousingArt.BarHeight * scale + CardStatGap * scale +
        Typography.LineHeight(TextStyles.Headline) + Typography.LineHeight(TextStyles.Caption1) +
        CardStatGap * scale + CardActionHeight * scale;

    private Rect PlotCardRect(Rect area, float bottom, float scale)
    {
        var height = PlotCardHeight(scale);
        var travel = (height + ControlInset * scale + TabBar.ContentInset(scale)) * (1f - plotCardShown.Value);
        var min = new Vector2(area.Min.X, bottom - height + travel);
        var max = new Vector2(area.Max.X, bottom + travel);
        return new Rect(min, max);
    }

    private void DrawPlotCard(Rect area, float bottom, float scale)
    {
        if (plotCardShown.Value <= 0.005f || !selectedPlot.IsValid)
        {
            return;
        }

        if (FindPlot(selectedPlot) is not { } plot)
        {
            ClosePlotCard(true);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var rect = PlotCardRect(area, bottom, scale);
        var alpha = Math.Clamp(plotCardShown.Value, 0f, 1f);
        var radius = CardRadius * scale;
        Elevation.Floating(drawList, rect.Min, rect.Max, radius, scale, alpha);
        Material.ThemedGlass(drawList, rect.Min, rect.Max, radius, scale, ui.Palette.BackdropTop);
        if (alpha < 0.6f)
        {
            return;
        }

        UiAnchors.Report("housing.sheet", rect);
        var pad = CardPad * scale;
        var left = rect.Min.X + pad;
        var right = rect.Max.X - pad;
        var now = DateTime.UtcNow;
        var key = (long)plot.Key.GetHashCode() ^ ((long)plot.Size << 40);
        var y = rect.Min.Y + pad;

        var closeRadius = CardCloseRadius * scale;
        var closeCenter = new Vector2(right - closeRadius, y + closeRadius);
        if (HousingArt.CircleAction(drawList, ImGui.GetID("housing.card.close"), closeCenter, closeRadius, PhoneIcons.X, ui,
                Loc.T(L.Common.Close), false))
        {
            ClosePlotCard();
        }

        var swatch = CardSwatch * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        HousingMarkers.DrawSwatch(drawList, new Vector2(left + swatch * 0.5f, y + titleHeight * 0.5f), plot.Size,
            HousingMarkers.PhaseColor(plot.Phase, ui.Accent), scale);
        var titleLeft = left + swatch + Metrics.Space.Sm * scale;
        var title = cardTitle.IsCurrent(key)
            ? cardTitle.Value
            : cardTitle.Store(key, HousingFormat.PlotLabel(plot.Key.Plot));
        Typography.Draw(drawList, new Vector2(titleLeft, y),
            Typography.FitText(title, MathF.Max(1f, closeCenter.X - closeRadius - titleLeft), TextStyles.Title3),
            ui.TitleInk, TextStyles.Title3);
        y += titleHeight + HousingArt.LineGap * scale;
        var subtitle = cardSubtitle.IsCurrent(key)
            ? cardSubtitle.Value
            : cardSubtitle.Store(key, string.Concat(HousingFormat.SizeLabel(plot.Size), " · ",
                HousingFormat.Place(HousingDistricts.DisplayName(plot.Key.DistrictId), plot.Key.Ward)));
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(subtitle, right - left, TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
        y += Typography.LineHeight(TextStyles.Footnote) + CardGap * scale;

        var phaseHue = HousingMarkers.PhaseColor(plot.Phase, ui.Accent);
        var countdown = HousingText.Countdown(ref cardCountdown, plot.PhaseEndsUtc, now);
        var countdownWidth = WidgetText.TabularWidth(countdown, TextStyles.SubheadlineEmphasized);
        WidgetText.Tabular(drawList, new Vector2(right - countdownWidth, y), countdown, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left, y),
            Typography.FitText(HousingFormat.PhaseLabel(plot.Phase),
                MathF.Max(1f, right - left - countdownWidth - HousingArt.TextGap * scale),
                TextStyles.SubheadlineEmphasized), phaseHue, TextStyles.SubheadlineEmphasized);
        y += Typography.LineHeight(TextStyles.Subheadline) + HousingArt.LineGap * 3f * scale;
        var progress = HousingLottery.Progress(plot.Phase, plot.PhaseEndsUtc, now);
        HousingArt.Bar(drawList, new Vector2(left, y), new Vector2(right, y + HousingArt.BarHeight * scale),
            MathF.Max(0f, progress), phaseHue);
        y += HousingArt.BarHeight * scale + CardStatGap * scale;

        var entries = cardEntries.IsCurrent(key ^ (plot.Entries ?? -1))
            ? cardEntries.Value
            : cardEntries.Store(key ^ (plot.Entries ?? -1), HousingFormat.Entries(plot.Entries));
        var price = cardPrice.IsCurrent(key ^ plot.Price)
            ? cardPrice.Value
            : cardPrice.Store(key ^ plot.Price, HousingFormat.Price(plot.Price));
        var column = (right - left) / 3f;
        DrawStatColumn(drawList, new Vector2(left, y), column, entries, Loc.T(L.Housing.EntriesLabel));
        DrawStatColumn(drawList, new Vector2(left + column, y), column, price, Loc.T(L.Housing.PriceLabel));
        DrawStatColumn(drawList, new Vector2(left + column * 2f, y), column,
            HousingFormat.EligibilityLabel(plot.Eligibility), Loc.T(L.Housing.EligibilityLabel));
        y += Typography.LineHeight(TextStyles.Headline) + Typography.LineHeight(TextStyles.Caption1) +
             CardStatGap * scale;

        DrawCardActions(drawList, plot, left, right, y, scale);
    }

    private void DrawStatColumn(ImDrawListPtr drawList, Vector2 topLeft, float width, string value, string label)
    {
        var scale = UiScale.Current;
        var maxWidth = MathF.Max(1f, width - Metrics.Space.Sm * scale);
        Typography.Draw(drawList, topLeft, Typography.FitText(value, maxWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(topLeft.X, topLeft.Y + Typography.LineHeight(TextStyles.Headline)),
            Typography.FitText(label, maxWidth, TextStyles.Caption1), ui.MutedInk, TextStyles.Caption1);
    }

    private void DrawCardActions(ImDrawListPtr drawList, HousingPlot plot, float left, float right, float top,
        float scale)
    {
        var height = CardActionHeight * scale;
        var radius = height * 0.5f;
        var gap = CardActionGap * scale;
        var detailsCenter = new Vector2(right - radius, top + radius);
        var remindCenter = new Vector2(detailsCenter.X - height - gap, detailsCenter.Y);
        var watchCenter = new Vector2(remindCenter.X - height - gap, detailsCenter.Y);
        var travel = new Rect(new Vector2(left, top), new Vector2(watchCenter.X - radius - gap, top + height));
        if (HousingChrome.PillButton(travel, Loc.T(L.Housing.TravelHere), true, ui))
        {
            TravelTo(plot.Key);
        }

        var watched = housing.Watch.IsWatched(plot.Key);
        var hit = new Vector2(radius, radius);
        UiAnchors.Report("housing.sheet.watch", new Rect(watchCenter - hit, watchCenter + hit));
        if (HousingArt.CircleAction(drawList, ImGui.GetID("housing.card.watch"), watchCenter, radius,
                watched ? PhoneIcons.BookmarkFilled : PhoneIcons.Bookmark, ui,
                Loc.T(watched ? L.Housing.Unwatch : L.Housing.Watch), watched))
        {
            ToggleWatch(plot);
        }

        var reminder = housing.Watch.FindReminder(plot.Key);
        var hasReminder = reminder is { Notified: false };
        var hasDeadline = plot.PhaseEndsUtc is not null;
        UiAnchors.Report("housing.sheet.remind", new Rect(remindCenter - hit, remindCenter + hit));
        if (HousingArt.CircleAction(drawList, ImGui.GetID("housing.card.remind"), remindCenter, radius,
                hasReminder ? PhoneIcons.BellFilled : PhoneIcons.Bell, ui,
                Loc.T(hasDeadline ? L.Housing.RemindMe : L.Housing.ReminderUnavailable), hasReminder, hasDeadline))
        {
            OpenReminderSheet(plot.Key);
        }

        if (HousingArt.CircleAction(drawList, ImGui.GetID("housing.card.details"), detailsCenter, radius, PhoneIcons.InfoCircle,
                ui, Loc.T(L.Housing.DetailsAction), false))
        {
            PushDetails(plot.Key, Loc.T(L.Housing.Map));
        }
    }
}
