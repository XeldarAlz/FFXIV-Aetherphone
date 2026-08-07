using Aetherphone.Core;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private void DrawDetailsRoute(Rect area, HousingPlotKey key)
    {
        var scale = UiScale.Current;
        var body = DrawSubHeader(area, "housing.header.details", Loc.T(L.Housing.Details));
        var plot = FindPlot(key);
        var watchRecord = housing.Watch.Find(key);
        if (plot is null && watchRecord is null)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.Home, Loc.T(L.Housing.NoLongerReported, "-"),
                Loc.T(L.Housing.NoScansHint));
            return;
        }

        var now = DateTime.UtcNow;
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            var origin = ImGui.GetCursorScreenPos();
            var y = origin.Y + 6f * scale;
            var left = origin.X;
            var right = origin.X + width;

            var size = plot?.Size ?? (HousingPlotSize)(watchRecord?.Size ?? 0);
            var phase = plot?.Phase ?? (HousingLotteryPhase)(watchRecord?.Phase ?? 0);
            var title = Loc.T(L.Housing.PlotTitle, key.Plot, HousingFormat.SizeLabel(size));
            Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(title, width, TextStyles.Title2),
                ui.TitleInk, TextStyles.Title2);
            y += Typography.LineHeight(TextStyles.Title2) + 2f * scale;
            var place = HousingFormat.Place(HousingDistricts.DisplayName(key.DistrictId), key.Ward);
            Typography.Draw(drawList, new Vector2(left, y),
                Typography.FitText(string.Concat(housing.WorldNameOf(key.WorldId), " · ", place), width,
                    TextStyles.Subheadline),
                ui.MutedInk, TextStyles.Subheadline);
            y += Typography.LineHeight(TextStyles.Subheadline) + 10f * scale;

            var lastSeen = plot?.LastSeenUtc ?? FromUnix(watchRecord?.LastSeenUnix ?? 0L);
            var freshness = housing.Thresholds.Classify(lastSeen, now, housing.ActiveSource);
            var chipX = left;
            var freshLabel = HousingFormat.FreshnessLabel(freshness);
            HousingChrome.Chip(drawList, new Vector2(chipX, y), freshLabel,
                HousingChrome.FreshnessHue(freshness, ui.Accent), false);
            chipX += HousingChrome.MeasureChip(freshLabel) + 6f * scale;
            var phaseLabel = HousingFormat.PhaseLabel(phase);
            HousingChrome.Chip(drawList, new Vector2(chipX, y), phaseLabel,
                HousingMarkers.PhaseColor(phase, ui.Accent), false);
            chipX += HousingChrome.MeasureChip(phaseLabel) + 6f * scale;
            if (plot is null)
            {
                HousingChrome.Chip(drawList, new Vector2(chipX, y), Loc.T(L.Housing.LastKnown),
                    AppPalettes.HousingClosed, false);
            }

            y += HousingChrome.ChipHeight * scale + 14f * scale;

            var phaseEnd = plot?.PhaseEndsUtc ?? OptionalUnix(watchRecord?.PhaseEndUnix ?? 0L);
            HousingChrome.SectionLabel(drawList, new Vector2(left, y), width, Loc.T(L.Housing.PhaseEndsLabel), ui);
            y += Typography.LineHeight(TextStyles.Caption1) + 10f * scale;
            var countdownText = phaseEnd is null
                ? Loc.T(L.Housing.TimeUnknown)
                : HousingFormat.HasExpired(phaseEnd, now)
                    ? Loc.T(L.Housing.PhaseExpired)
                    : HousingFormat.Countdown(HousingFormat.Remaining(phaseEnd, now) ?? TimeSpan.Zero);
            Typography.Draw(drawList, new Vector2(left, y),
                Typography.FitText(countdownText, width, TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
            y += Typography.LineHeight(TextStyles.Title3) + 2f * scale;
            Typography.Draw(drawList, new Vector2(left, y),
                phaseEnd is null ? Loc.T(L.Housing.NotReported) : HousingFormat.ExactLocalTime(phaseEnd.Value),
                ui.MutedInk, TextStyles.Footnote);
            y += Typography.LineHeight(TextStyles.Footnote) + 14f * scale;

            var rowHeight = HousingChrome.StatRowHeight * scale;
            HousingChrome.SectionLabel(drawList, new Vector2(left, y), width, Loc.T(L.Housing.ModeLottery), ui);
            y += Typography.LineHeight(TextStyles.Caption1) + 8f * scale;
            var entries = plot?.Entries ?? watchRecord?.Entries;
            y = StatRow(drawList, left, right, y, rowHeight, Loc.T(L.Housing.EntriesLabel),
                HousingFormat.Entries(entries), true);
            if (HousingFormat.Odds(entries) is { } odds)
            {
                y = StatRow(drawList, left, right, y, rowHeight, Loc.T(L.Housing.ModeLottery), odds);
            }

            y = StatRow(drawList, left, right, y, rowHeight, Loc.T(L.Housing.PriceLabel),
                HousingFormat.Price(plot?.Price ?? watchRecord?.Price ?? 0L));
            y = StatRow(drawList, left, right, y, rowHeight, Loc.T(L.Housing.EligibilityLabel),
                HousingFormat.EligibilityLabel(plot?.Eligibility ??
                                               (HousingPurchaseEligibility)(watchRecord?.Eligibility ?? 0)));
            y = StatRow(drawList, left, right, y, rowHeight, Loc.T(L.Housing.PurchaseLabel),
                HousingFormat.ModeLabel(plot?.Mode ?? HousingPurchaseMode.Unknown));
            y = StatRow(drawList, left, right, y, rowHeight, Loc.T(L.Housing.DivisionLabel),
                HousingFormat.DivisionLabel(HousingDistricts.IsSubdivision(key.Plot)));
            y += 6f * scale;
            Typography.DrawWrappedLeft(new Vector2(left, y), Loc.T(L.Housing.EntriesCaveat), ui.MutedInk,
                TextStyles.Caption1, width);
            y += Typography.MeasureWrappedBlock(Loc.T(L.Housing.EntriesCaveat), TextStyles.Caption1, width).Y
                 + 14f * scale;

            HousingChrome.SectionLabel(drawList, new Vector2(left, y), width, Loc.T(L.Housing.SettingsData), ui);
            y += Typography.LineHeight(TextStyles.Caption1) + 8f * scale;
            y = StatRow(drawList, left, right, y, rowHeight, Loc.T(L.Housing.RegionLabel),
                Blank(housing.RegionName(key.WorldId)));
            y = StatRow(drawList, left, right, y, rowHeight, Loc.T(L.Housing.DataCenterLabel),
                Blank(housing.DataCenterName(key.WorldId)));
            y = StatRow(drawList, left, right, y, rowHeight, Loc.T(L.Housing.ScannedLabel),
                HousingFormat.ScanAge(lastSeen, now), true);
            y = StatRow(drawList, left, right, y, rowHeight, Loc.T(L.Housing.ExactTime),
                lastSeen == default ? Loc.T(L.Housing.NotReported) : HousingFormat.ExactLocalTime(lastSeen));
            var firstSeen = plot?.FirstSeenUtc ?? FromUnix(watchRecord?.FirstSeenUnix ?? 0L);
            y = StatRow(drawList, left, right, y, rowHeight, Loc.T(L.Housing.FirstReported),
                firstSeen == default ? Loc.T(L.Housing.NotReported) : HousingFormat.ExactLocalTime(firstSeen));
            y = StatRow(drawList, left, right, y, rowHeight, Loc.T(L.Housing.ProviderLabel), housing.ProviderName);
            y += 12f * scale;

            var buttonHeight = 34f * scale;
            var gap = 8f * scale;
            var watched = housing.Watch.IsWatched(key);
            var watchLabel = Loc.T(plot is null || watched ? L.Housing.Unwatch : L.Housing.Watch);
            var mapLabel = Loc.T(L.Housing.BackToMap);
            Span<string> actionLabels = [watchLabel, mapLabel];
            Span<Rect> actionRects = stackalloc Rect[2];
            HousingChrome.LayoutPills(new Rect(new Vector2(left, y), new Vector2(right, y + buttonHeight)),
                actionLabels, gap, actionRects);
            var watchRect = actionRects[0];
            var mapRect = actionRects[1];
            if (plot is not null && HousingChrome.PillButton(watchRect, watchLabel, watched, ui))
            {
                housing.Watch.ToggleWatch(plot, housing.WorldNameOf(plot.Key.WorldId));
                InvalidateCache();
            }
            else if (plot is null && HousingChrome.PillButton(watchRect, watchLabel, true, ui))
            {
                housing.Watch.CancelReminder(key);
                housing.Watch.Unwatch(key);
                InvalidateCache();
                router.Pop();
                return;
            }

            if (HousingChrome.PillButton(mapRect, mapLabel, false, ui))
            {
                Back();
            }

            y += buttonHeight;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, y - origin.Y + 24f * scale));
        }
    }

    private float StatRow(ImDrawListPtr drawList, float left, float right, float y, float height, string label,
        string value, bool emphasise = false)
    {
        HousingChrome.StatRow(drawList, new Rect(new Vector2(left, y), new Vector2(right, y + height)), label, value,
            ui, emphasise);
        return y + height;
    }

    private static string Blank(string value) => value.Length > 0 ? value : Loc.T(L.Housing.NotReported);

    private static DateTime? OptionalUnix(long unixSeconds) =>
        unixSeconds <= 0L ? null : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
}
