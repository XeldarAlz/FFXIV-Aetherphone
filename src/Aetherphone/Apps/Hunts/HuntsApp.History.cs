using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private sealed class HistoryRow
    {
        public HuntLogEntryDto Entry = null!;
        public string Rank = string.Empty;
        public string Name = string.Empty;
        public string Subtitle = string.Empty;
        public string Time = string.Empty;
        public string Lifetime = string.Empty;
        public string MarqueeKey = string.Empty;
    }

    private const float HistoryRowHeight = 58f;
    private const float HistoryTileSize = 36f;

    private static readonly Func<DateTimeOffset, DateTime> LocalDate = static moment => moment.ToLocalTime().DateTime;

    private readonly List<HuntLogEntryDto> historySorted = new();
    private readonly List<HuntHistoryDay> historyDays = new();
    private readonly List<string> historyDayTitles = new();
    private readonly List<HistoryRow> historyRows = new();
    private HuntLogEntryDto[]? historySource;
    private DateTime historyBuiltDate;
    private string historyLanguage = string.Empty;

    private void DrawHistory(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (ImRaii.PushId("hunts.history"))
        using (AppSurface.Begin(navBar.Body))
        {
            DrawHistoryBody(navBar.Body, scale);
            BottomSpacer(scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "hunts.history.nav", Loc.T(L.Hunts.HistoryTab),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawHistoryBody(Rect body, float scale)
    {
        if (!hunts.IsAuthenticated)
        {
            if (DrawEmpty(body, FontAwesomeIcon.Lock, Loc.T(L.Hunts.HistorySignedOutTitle),
                    Loc.T(L.Hunts.HistoryRequiresLoginTooltip), Loc.T(L.Hunts.SignupLoginButton)))
            {
                OpenAccount();
            }

            return;
        }

        if (hunts.CurrentDataCenter is null)
        {
            if (DrawEmpty(body, FontAwesomeIcon.Globe, Loc.T(L.Hunts.NoDataCenterTitle),
                    Loc.T(L.Hunts.NoDataCenterHint), Loc.T(L.Hunts.ChooseDataCenter)))
            {
                OpenFilters();
            }

            return;
        }

        hunts.EnsureHistoryLoaded();
        if (!hunts.HistoryLoaded)
        {
            DrawSkeleton(scale);
            return;
        }

        EnsureHistoryModel();
        if (historyRows.Count == 0)
        {
            DrawEmpty(body, FontAwesomeIcon.History, Loc.T(L.Hunts.HistoryEmpty), Loc.T(L.Hunts.HistoryEmptyHint),
                string.Empty);
            return;
        }

        for (var dayIndex = 0; dayIndex < historyDays.Count; dayIndex++)
        {
            var day = historyDays[dayIndex];
            ui.SectionLabel(historyDayTitles[dayIndex], TextStyles.FootnoteEmphasized, 6f);
            var card = GroupCard.Begin(ui, day.Count, HistoryRowHeight);
            card.SeparatorInset = HistoryTileSize + HuntsArt.RowGap;
            for (var offset = 0; offset < day.Count; offset++)
            {
                var row = card.NextRow();
                var bounds = new Rect(new Vector2(card.Bounds.Min.X, row.Min.Y),
                    new Vector2(card.Bounds.Max.X, row.Max.Y));
                if (ImGui.IsRectVisible(bounds.Min, bounds.Max))
                {
                    DrawHistoryRow(row, bounds, historyRows[day.Start + offset], scale);
                }
            }

            card.End();
            Gap(HuntsArt.CardGap * 0.5f);
        }
    }

    private void EnsureHistoryModel()
    {
        var entries = hunts.History;
        var today = DateTime.Now.Date;
        if (ReferenceEquals(entries, historySource) && today == historyBuiltDate &&
            string.Equals(configuration.Language, historyLanguage, StringComparison.Ordinal))
        {
            return;
        }

        historySource = entries;
        historyBuiltDate = today;
        historyLanguage = configuration.Language;
        HuntHistoryDays.Group(entries, LocalDate, historySorted, historyDays);
        historyDayTitles.Clear();
        for (var index = 0; index < historyDays.Count; index++)
        {
            var moment = HuntHistoryDays.MomentOf(historySorted[historyDays[index].Start])!.Value;
            historyDayTitles.Add(TimeText.DayLabel(moment.ToUnixTimeSeconds()));
        }

        while (historyRows.Count < historySorted.Count)
        {
            historyRows.Add(new HistoryRow());
        }

        if (historyRows.Count > historySorted.Count)
        {
            historyRows.RemoveRange(historySorted.Count, historyRows.Count - historySorted.Count);
        }

        for (var index = 0; index < historySorted.Count; index++)
        {
            FillHistoryRow(historyRows[index], historySorted[index]);
        }
    }

    private void FillHistoryRow(HistoryRow row, HuntLogEntryDto entry)
    {
        var mob = mobCatalog.Find(entry.MobId);
        var zoneInstance = entry.ZoneInstance ?? 0;
        row.Entry = entry;
        row.Rank = mob?.Rank ?? string.Empty;
        row.Name = ResolveMobLabel(mob, entry.MobId);
        row.MarqueeKey = entry.Id.ToString(Loc.Culture);
        var place = ResolvePlace(entry.WorldId, zoneInstance, mob);
        var zoneId = entry.ZoneId is { Length: > 0 } loggedZone
            ? loggedZone
            : mob is { ZoneIds.Length: > 0 } ? mob.ZoneIds[0] : string.Empty;
        var zone = ResolveZoneLabel(zoneId);
        row.Subtitle = zone.Length > 0 ? Loc.T(L.Hunts.PlaceInZone, place, zone) : place;
        row.Time = HuntHistoryDays.MomentOf(entry) is { } moment ? TimeText.Clock(moment.ToLocalTime()) : string.Empty;
        row.Lifetime = entry.IsFailed
            ? Loc.T(L.Hunts.HistoryFailed)
            : HuntHistoryDays.Lifetime(entry) is { } lifetime
                ? Loc.T(L.Hunts.HistoryLifetime, HuntsText.Span(lifetime))
                : string.Empty;
    }

    private void DrawHistoryRow(Rect row, Rect bounds, HistoryRow model, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        if (hovered)
        {
            drawList.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tile = HistoryTileSize * scale;
        HuntsArt.RankTile(drawList, new Vector2(row.Min.X, row.Center.Y - tile * 0.5f), tile, model.Rank,
            HuntsArt.RankColor(model.Rank, ui.Accent));
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subHeight = Typography.LineHeight(TextStyles.Footnote);
        var lineGap = HuntsArt.LineGap * scale;
        var top = row.Center.Y - (titleHeight + lineGap + subHeight) * 0.5f;
        var timeWidth = Typography.Measure(model.Time, TextStyles.SubheadlineEmphasized).X;
        var lifetimeWidth = model.Lifetime.Length > 0 ? Typography.Measure(model.Lifetime, TextStyles.Footnote).X : 0f;
        Typography.Draw(drawList, new Vector2(row.Max.X - timeWidth,
                top + (titleHeight - Typography.LineHeight(TextStyles.SubheadlineEmphasized)) * 0.5f), model.Time,
            ui.TitleInk, TextStyles.SubheadlineEmphasized);
        if (lifetimeWidth > 0f)
        {
            Typography.Draw(drawList, new Vector2(row.Max.X - lifetimeWidth, top + titleHeight + lineGap),
                model.Lifetime, model.Entry.IsFailed ? frameTheme.Danger : ui.MutedInk, TextStyles.Footnote);
        }

        var left = row.Min.X + tile + HuntsArt.RowGap * scale;
        Marquee.DrawLeftAuto(drawList, new MarqueeId("hunts.history.name.", model.MarqueeKey), model.Name, left, top,
            MathF.Max(1f, row.Max.X - timeWidth - HuntsArt.RowGap * scale - left), TextStyles.Headline, ui.TitleInk);
        Typography.Draw(drawList, new Vector2(left, top + titleHeight + lineGap),
            Typography.FitText(model.Subtitle, MathF.Max(1f, row.Max.X - lifetimeWidth - HuntsArt.RowGap * scale - left),
                TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        if (UiInteract.Click(bounds.Min, bounds.Max, hovered))
        {
            OpenDetailFor(model.Entry.MobId, model.Entry.WorldId, model.Entry.ZoneInstance ?? 0,
                Loc.T(L.Hunts.HistoryTab));
        }
    }
}
