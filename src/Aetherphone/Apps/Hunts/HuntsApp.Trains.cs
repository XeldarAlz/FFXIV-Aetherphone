using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private const float TrainSummaryHeight = 86f;
    private const float TrainRowHeight = 58f;
    private const float TrainGoWidth = 56f;
    private const float TrainGoHeight = 30f;

    private readonly ChipRail trainExpansionRail = new();
    private readonly ChipRail trainWorldRail = new();
    private readonly string[] trainExpansionLabels = new string[HuntExpansions.Ids.Length];
    private readonly bool[] trainExpansionActive = new bool[HuntExpansions.Ids.Length];
    private readonly List<HuntTrainStop> trainStops = new();
    private readonly List<HuntRow> trainRows = new();
    private readonly List<HuntRow> trainPool = new();
    private readonly List<string> trainLegTitles = new();
    private string[] trainWorldIds = Array.Empty<string>();
    private string[] trainWorldLabels = Array.Empty<string>();
    private bool[] trainWorldActive = Array.Empty<bool>();
    private string trainDataCenter = string.Empty;
    private string trainWorld = string.Empty;
    private int trainExpansion = HuntExpansions.Ids.Length - 1;
    private HuntWindowDto[]? trainSource;
    private int trainSpawnVersion = -1;
    private string trainBuiltWorld = string.Empty;
    private int trainBuiltExpansion = -1;
    private string trainLanguage = string.Empty;
    private DateTimeOffset trainRefreshAt;
    private int trainLiveCount;
    private int trainOpenCount;
    private int trainClosedCount;
    private string trainLiveText = string.Empty;
    private string trainOpenText = string.Empty;
    private string trainClosedText = string.Empty;

    private void DrawTrains(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (ImRaii.PushId("hunts.trains"))
        using (AppSurface.Begin(navBar.Body))
        {
            DrawTrainsBody(navBar.Body, scale);
            BottomSpacer(scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "hunts.trains.nav", Loc.T(L.Hunts.TrainsTab),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawTrainsBody(Rect body, float scale)
    {
        if (hunts.CurrentDataCenter is not { Length: > 0 } dataCenter)
        {
            if (DrawEmpty(body, FontAwesomeIcon.Globe, Loc.T(L.Hunts.NoDataCenterTitle),
                    Loc.T(L.Hunts.NoDataCenterHint), Loc.T(L.Hunts.ChooseDataCenter)))
            {
                OpenFilters();
            }

            return;
        }

        if (DrawFailed(body))
        {
            return;
        }

        if (!hunts.Loaded)
        {
            DrawSkeleton(scale);
            return;
        }

        EnsureTrainWorlds(dataCenter);
        DrawTrainPickers();
        EnsureTrainRoute(DateTimeOffset.UtcNow);
        if (trainRows.Count == 0)
        {
            DrawEmpty(body, FontAwesomeIcon.Route, Loc.T(L.Hunts.TrainEmptyTitle), Loc.T(L.Hunts.TrainEmptyHint),
                string.Empty);
            return;
        }

        DrawTrainSummary(scale);
        var start = 0;
        var leg = 0;
        for (var index = 1; index <= trainRows.Count; index++)
        {
            if (index < trainRows.Count && !HuntTrainRoute.StartsLeg(trainStops, index))
            {
                continue;
            }

            DrawTrainLeg(leg < trainLegTitles.Count ? trainLegTitles[leg] : string.Empty, start, index - start, scale);
            leg++;
            start = index;
        }
    }

    private void EnsureTrainWorlds(string dataCenter)
    {
        if (string.Equals(dataCenter, trainDataCenter, StringComparison.Ordinal))
        {
            return;
        }

        trainDataCenter = dataCenter;
        trainWorldIds = HuntDataCenterWorlds.WorldsFor(dataCenter);
        trainWorldLabels = new string[trainWorldIds.Length];
        trainWorldActive = new bool[trainWorldIds.Length];
        for (var index = 0; index < trainWorldIds.Length; index++)
        {
            trainWorldLabels[index] = ResolveWorldLabel(trainWorldIds[index]);
        }

        var currentWorld = HuntDataCenterWorlds.SlugFor(LocationShare.CurrentWorldId());
        trainWorld = trainWorldIds.Length == 0 ? string.Empty : trainWorldIds[0];
        for (var index = 0; index < trainWorldIds.Length; index++)
        {
            if (string.Equals(trainWorldIds[index], currentWorld, StringComparison.OrdinalIgnoreCase))
            {
                trainWorld = trainWorldIds[index];
            }
        }
    }

    private void DrawTrainPickers()
    {
        for (var index = 0; index < trainExpansionLabels.Length; index++)
        {
            trainExpansionLabels[index] = ExpansionName(index);
            trainExpansionActive[index] = index == trainExpansion;
        }

        var tappedExpansion = trainExpansionRail.Draw(ui, trainExpansionLabels, trainExpansionActive,
            "hunts.trains.expansion");
        if (tappedExpansion >= 0 && tappedExpansion != trainExpansion)
        {
            trainExpansion = tappedExpansion;
            UiFeedback.Play(UiSound.Tap);
        }

        if (trainWorldIds.Length == 0)
        {
            return;
        }

        for (var index = 0; index < trainWorldIds.Length; index++)
        {
            trainWorldActive[index] = string.Equals(trainWorldIds[index], trainWorld, StringComparison.OrdinalIgnoreCase);
        }

        var tappedWorld = trainWorldRail.Draw(ui, trainWorldLabels, trainWorldActive, "hunts.trains.world");
        if (tappedWorld >= 0 && !trainWorldActive[tappedWorld])
        {
            trainWorld = trainWorldIds[tappedWorld];
            UiFeedback.Play(UiSound.Tap);
        }

        Gap(HuntsArt.TileGap);
    }

    private void EnsureTrainRoute(DateTimeOffset now)
    {
        var windows = hunts.Windows;
        var current = ReferenceEquals(windows, trainSource) && hunts.ActiveSpawnVersion == trainSpawnVersion &&
                      string.Equals(trainWorld, trainBuiltWorld, StringComparison.Ordinal) &&
                      trainExpansion == trainBuiltExpansion &&
                      string.Equals(configuration.Language, trainLanguage, StringComparison.Ordinal) &&
                      now < trainRefreshAt;
        if (current)
        {
            return;
        }

        trainSource = windows;
        trainSpawnVersion = hunts.ActiveSpawnVersion;
        trainBuiltWorld = trainWorld;
        trainBuiltExpansion = trainExpansion;
        trainLanguage = configuration.Language;
        trainRefreshAt = now + BoardRefreshInterval;
        HuntTrainRoute.Build(mobCatalog.ById, windows, trainWorld, HuntExpansions.Ids[trainExpansion], trainStops);
        trainRows.Clear();
        trainLegTitles.Clear();
        trainLiveCount = 0;
        trainOpenCount = 0;
        trainClosedCount = 0;
        for (var index = 0; index < trainStops.Count; index++)
        {
            var stop = trainStops[index];
            var window = windows[stop.WindowIndex];
            var mob = mobCatalog.Find(stop.MobId);
            while (trainPool.Count <= trainRows.Count)
            {
                trainPool.Add(new HuntRow());
            }

            var row = trainPool[trainRows.Count];
            FillRow(row, window, mob, ResolveDisplayStatus(window, mob, now), ResolveMobLabel(mob, stop.MobId), now);
            trainRows.Add(row);
            CountTrainRow(row.Section);
            if (HuntTrainRoute.StartsLeg(trainStops, index))
            {
                var zone = ResolveZoneLabel(stop.ZoneId);
                trainLegTitles.Add(stop.ZoneInstance > 0 && mob is not null && hunts.ZoneInstanceCountFor(mob) > 1
                    ? Loc.T(L.Hunts.PlaceInstance, zone, stop.ZoneInstance)
                    : zone);
            }
        }

        trainLiveText = trainLiveCount.ToString(Loc.Culture);
        trainOpenText = trainOpenCount.ToString(Loc.Culture);
        trainClosedText = trainClosedCount.ToString(Loc.Culture);
    }

    private void CountTrainRow(HuntBoardSection section)
    {
        switch (section)
        {
            case HuntBoardSection.Live:
                trainLiveCount++;
                break;
            case HuntBoardSection.Open:
                trainOpenCount++;
                break;
            case HuntBoardSection.Soon:
                trainClosedCount++;
                break;
        }
    }

    private void DrawTrainSummary(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = TrainSummaryHeight * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
        UiAnchors.Report("hunts.trains.summary", card);
        ui.Card(drawList, card.Min, card.Max, HuntsArt.CardRadius * scale, elevated: true);
        var columnWidth = width / 3f;
        DrawTrainStat(drawList, card, 0, columnWidth, trainLiveText, Loc.T(L.Hunts.StatusLive),
            trainLiveCount > 0 ? HuntsArt.LiveColor : ui.TitleInk);
        DrawTrainStat(drawList, card, 1, columnWidth, trainOpenText, Loc.T(L.Hunts.Open),
            trainOpenCount > 0 ? HuntsArt.OpenColor : ui.TitleInk);
        DrawTrainStat(drawList, card, 2, columnWidth, trainClosedText, Loc.T(L.Hunts.Closed), ui.TitleInk);
        for (var index = 1; index < 3; index++)
        {
            var x = card.Min.X + columnWidth * index;
            drawList.AddLine(new Vector2(x, card.Min.Y + HuntsArt.CardPadding * scale),
                new Vector2(x, card.Max.Y - HuntsArt.CardPadding * scale), ImGui.GetColorU32(ui.Hairline),
                Metrics.Stroke.Hairline);
        }

        ImGui.Dummy(new Vector2(width, height + HuntsArt.CardGap * scale));
    }

    private void DrawTrainStat(ImDrawListPtr drawList, Rect card, int column, float columnWidth, string value,
        string label, Vector4 valueInk)
    {
        var valueStyle = TextStyles.Title1;
        var valueHeight = Typography.LineHeight(valueStyle);
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = card.Center.Y - (valueHeight + labelHeight) * 0.5f;
        var centerX = card.Min.X + columnWidth * (column + 0.5f);
        var valueWidth = Typography.Measure(value, valueStyle).X;
        Typography.Draw(drawList, new Vector2(centerX - valueWidth * 0.5f, top), value, valueInk, valueStyle);
        var fitted = Typography.FitText(label, columnWidth - HuntsArt.TileGap * UiScale.Current, TextStyles.Footnote);
        var labelWidth = Typography.Measure(fitted, TextStyles.Footnote).X;
        Typography.Draw(drawList, new Vector2(centerX - labelWidth * 0.5f, top + valueHeight), fitted, ui.MutedInk,
            TextStyles.Footnote);
    }

    private void DrawTrainLeg(string title, int start, int count, float scale)
    {
        ui.SectionLabel(title, TextStyles.FootnoteEmphasized, 6f);
        var card = GroupCard.Begin(ui, count, TrainRowHeight);
        card.SeparatorInset = RingSize + HuntsArt.RowGap;
        for (var offset = 0; offset < count; offset++)
        {
            var row = card.NextRow();
            var bounds = new Rect(new Vector2(card.Bounds.Min.X, row.Min.Y), new Vector2(card.Bounds.Max.X, row.Max.Y));
            if (start + offset == 0)
            {
                UiAnchors.Report("hunts.trains.first", bounds);
            }

            if (ImGui.IsRectVisible(bounds.Min, bounds.Max))
            {
                DrawTrainRow(row, bounds, trainRows[start + offset], scale);
            }
        }

        card.End();
        Gap(HuntsArt.CardGap * 0.5f);
    }

    private void DrawTrainRow(Rect row, Rect bounds, HuntRow model, float scale)
    {
        if (model.Section != HuntBoardSection.Live || model.TerritoryId == 0 ||
            HuntDataCenterWorlds.WorldRowId(model.Window.WorldId) == 0)
        {
            DrawBoardRow(row, bounds, model, scale, Loc.T(L.Hunts.TrainsTab));
            return;
        }

        var size = new Vector2(TrainGoWidth * scale, TrainGoHeight * scale);
        var goMin = new Vector2(row.Max.X - size.X, row.Center.Y - size.Y * 0.5f);
        var goRect = new Rect(goMin, goMin + size);
        var overGo = UiInteract.Hover(goRect.Min, goRect.Max);
        var inner = new Rect(row.Min, new Vector2(goRect.Min.X - HuntsArt.RowGap * scale, row.Max.Y));
        DrawBoardRow(inner, bounds, model, scale, Loc.T(L.Hunts.TrainsTab), overGo);
        if (ui.PillButton(goRect, Loc.T(L.Hunts.Go), true, model.GoId))
        {
            UiFeedback.Play(UiSound.Tap);
            TravelToRow(model);
        }
    }
}
