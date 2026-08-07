using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private const float WatchRowHeight = 76f;

    private void DrawWatchlistRoute(Rect area)
    {
        var scale = UiScale.Current;
        var watched = housing.Watch.Watched;
        var body = DrawSubHeader(area, "housing.header.watchlist", Loc.T(L.Housing.Watchlist),
            watched.Count > 0 ? Loc.T(L.Housing.ClearWatchlist) : null,
            watched.Count > 0 ? AskClearWatchlist : null);
        if (watched.Count == 0)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.Bookmark, Loc.T(L.Housing.WatchlistEmpty),
                Loc.T(L.Housing.WatchlistEmptyHint));
            return;
        }

        using (AppSurface.Begin(body))
        {
            var now = DateTime.UtcNow;
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var card = GroupCard.Begin(frameTheme, watched.Count, WatchRowHeight);
            for (var index = 0; index < watched.Count; index++)
            {
                DrawWatchRow(card.NextRow(), watched[index], now, scale);
            }

            card.End();
            HousingChrome.SectionLabel(ImGui.GetWindowDrawList(),
                ImGui.GetCursorScreenPos() + new Vector2(0f, 12f * scale),
                ScrollLayout.StableContentWidth(), Loc.T(L.Housing.EntriesLabel), ui);
            ImGui.Dummy(new Vector2(0f, 26f * scale));
            Typography.DrawWrappedLeft(ImGui.GetCursorScreenPos(), Loc.T(L.Housing.EntriesCaveat), ui.MutedInk,
                TextStyles.Footnote, ScrollLayout.StableContentWidth());
            ImGui.Dummy(new Vector2(0f, 44f * scale));
        }
    }

    private void AskClearWatchlist()
    {
        var count = housing.Watch.Watched.Count;
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Housing.Watchlist),
            Message = Loc.T(L.Housing.ClearWatchlistConfirm, count),
            ConfirmLabel = Loc.T(L.Housing.ClearWatchlist),
            CancelLabel = Loc.T(L.Common.Cancel),
            Confirm = () =>
            {
                var watched = housing.Watch.Watched;
                for (var index = watched.Count - 1; index >= 0; index--)
                {
                    housing.Watch.CancelReminder(watched[index].Key);
                }

                housing.Watch.ClearWatched();
                InvalidateCache();
            },
        });
    }

    private void DrawWatchRow(Rect row, HousingWatchRecord record, DateTime now, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var live = FindPlot(record.Key);
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, new Vector2(row.Min.X - 8f * scale, row.Min.Y + 2f * scale),
                new Vector2(row.Max.X + 8f * scale, row.Max.Y - 2f * scale), Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(Palette.WithAlpha(frameTheme.Accent, 0.12f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var size = (HousingPlotSize)record.Size;
        var phase = (HousingLotteryPhase)record.Phase;
        var lastSeen = FromUnix(record.LastSeenUnix);
        var markerCenter = new Vector2(row.Min.X + 15f * scale, row.Min.Y + 22f * scale);
        var style = new HousingMarkerStyle(size, phase, true, false,
            housing.Thresholds.Classify(lastSeen, now, HousingProviderKind.Service) == HousingDataFreshness.Stale,
            false);
        HousingMarkers.Draw(drawList, markerCenter, style, frameTheme.Accent, scale * 0.86f, 0f);
        var textLeft = markerCenter.X + 22f * scale;
        var textRight = row.Max.X - 14f * scale;
        var title = Loc.T(L.Housing.PlotTitle, record.Plot, HousingFormat.SizeLabel(size));
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 8f * scale),
            Typography.FitText(title, textRight - textLeft, TextStyles.BodyEmphasized), frameTheme.TextStrong,
            TextStyles.BodyEmphasized);
        var worldName = record.WorldName.Length > 0 ? record.WorldName : housing.WorldNameOf(record.WorldId);
        var place = string.Concat(worldName, " · ",
            HousingFormat.Place(HousingDistricts.DisplayName(record.DistrictId), record.Ward));
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 26f * scale),
            Typography.FitText(place, textRight - textLeft, TextStyles.Footnote), frameTheme.TextMuted,
            TextStyles.Footnote);
        var phaseEnd = record.PhaseEndUnix > 0L
            ? DateTimeOffset.FromUnixTimeSeconds(record.PhaseEndUnix).UtcDateTime
            : (DateTime?)null;
        var stateLine = record.StillReported
            ? string.Concat(HousingFormat.PhaseLabel(phase), " · ",
                phaseEnd is null
                    ? Loc.T(L.Housing.TimeUnknown)
                    : HousingFormat.Countdown(HousingFormat.Remaining(phaseEnd, now) ?? TimeSpan.Zero), " · ",
                Loc.T(L.Housing.EntriesLabel), ": ", HousingFormat.Entries(record.Entries))
            : Loc.T(L.Housing.NoLongerReported, HousingFormat.ScanAgeShort(lastSeen, now));
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 43f * scale),
            Typography.FitText(stateLine, textRight - textLeft, TextStyles.Caption1),
            record.StillReported ? frameTheme.TextMuted : AppPalettes.HousingResults, TextStyles.Caption1);
        var chipTop = row.Min.Y + 56f * scale;
        var chipX = textLeft;
        if (!record.StillReported)
        {
            var label = Loc.T(L.Housing.LastKnown);
            HousingChrome.Chip(drawList, new Vector2(chipX, chipTop), label, AppPalettes.HousingClosed, false);
            chipX += HousingChrome.MeasureChip(label) + 6f * scale;
        }

        var reminder = housing.Watch.FindReminder(record.Key);
        if (reminder is { Notified: false })
        {
            HousingChrome.Chip(drawList, new Vector2(chipX, chipTop),
                HousingFormat.LeadTime(reminder.OffsetMinutes), AppPalettes.HousingBrass, false);
        }

        var removeRadius = 11f * scale;
        var removeCenter = new Vector2(textRight - removeRadius, row.Min.Y + 20f * scale);
        if (AppSkin.IconButton(removeCenter, removeRadius, FontAwesomeIcon.Times.ToIconString(), frameTheme.TextMuted,
                frameTheme.SurfaceMuted, 0.64f, frameTheme, Loc.T(L.Housing.Unwatch)))
        {
            housing.Watch.CancelReminder(record.Key);
            housing.Watch.Unwatch(record.Key);
            InvalidateCache();
            return;
        }

        if (!UiInteract.Click(row.Min, row.Max, hovered))
        {
            return;
        }

        if (live is not null)
        {
            OpenFromList(live);
            return;
        }

        Push(HousingRoute.Details, record.Key);
    }

    private static DateTime FromUnix(long unixSeconds) =>
        unixSeconds <= 0L ? default : DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
}
