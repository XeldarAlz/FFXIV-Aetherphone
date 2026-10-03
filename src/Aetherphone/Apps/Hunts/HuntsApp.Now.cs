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
    private sealed class HuntRow
    {
        public HuntWindowDto Window = null!;
        public HuntMobDefinition? Mob;
        public HuntWindowStatus Status;
        public HuntBoardSection Section;
        public double SortKey;
        public float Ring;
        public string Rank = string.Empty;
        public string Name = string.Empty;
        public string Subtitle = string.Empty;
        public string Primary = string.Empty;
        public string Secondary = string.Empty;
        public string Reporters = string.Empty;
        public string MarqueeKey = string.Empty;
        public string PressId = string.Empty;
        public string GoId = string.Empty;
        public string ZoneId = string.Empty;
        public uint TerritoryId;
        public Vector2 Focus;
        public bool HasFocus;
        public (float X, float Y)? Coordinate;
    }

    private const int SearchMaxLength = 48;
    private const float SearchGap = 10f;
    private const float BoardRowHeight = 62f;
    private const float RingSize = 40f;
    private const float RingThickness = 4f;
    private const float LiveCardHeight = 92f;
    private const float LiveThumbSize = 64f;
    private const float GoButtonWidth = 64f;
    private const float GoButtonHeight = 32f;
    private const float StatusCapsuleHeight = 30f;
    private const float StatusCapsulePad = 12f;
    private const float StatusDotGap = 8f;
    private const float StatusRowGap = 10f;
    private const float SkeletonRowUnits = 56f;
    private const float SkeletonGapUnits = 10f;
    private const float SkeletonAreaUnits = 340f;
    private const float EmptyAreaFraction = 0.6f;

    private static readonly TimeSpan BoardRefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly Comparison<HuntRow> CompareRows = static (left, right) => left.SortKey.CompareTo(right.SortKey);

    private readonly List<HuntRow> rowPool = new();
    private readonly List<HuntRow> liveRows = new();
    private readonly List<HuntRow> openRows = new();
    private readonly List<HuntRow> soonRows = new();
    private readonly List<HuntRow> waitingRows = new();
    private HuntWindowDto[]? boardSource;
    private int boardSpawnVersion = -1;
    private int boardFilterRevision = -1;
    private string boardSearch = string.Empty;
    private string boardLanguage = string.Empty;
    private DateTimeOffset boardRefreshAt;
    private bool boardDirty = true;
    private int boardRowCount;
    private string searchQuery = string.Empty;
    private bool listReadyForTour;
    private int statusLabelState = -1;
    private object? statusLabelCulture;
    private string statusLabelDataCenter = string.Empty;
    private string statusLabel = string.Empty;

    private void DrawNow(in PhoneContext context)
    {
        listReadyForTour = false;
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (ImRaii.PushId("hunts.now"))
        using (AppSurface.Begin(navBar.Body))
        {
            DrawStatusCapsule(scale);
            DrawNowBody(navBar.Body, scale);
            BottomSpacer(scale);
        }

        navButtons[0] = new NavBarButton(PhoneIcons.AdjustmentsHorizontal, Loc.T(L.Hunts.FiltersTitle));
        UiAnchors.Report("hunts.filters", AppHeader.LargeTitleButtonRect(in navBar, 0, 1));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "hunts.nav", DisplayName, NavBarStyle.From(ui),
            navButtons.AsSpan(0, 1));
        if (pressed == 0)
        {
            OpenFilters();
        }
    }

    private void DrawStatusCapsule(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var authenticated = hunts.IsAuthenticated;
        var connected = authenticated && hunts.RealtimeConnected;
        var dotInk = connected ? HuntsArt.OpenColor : authenticated ? HuntsArt.LiveColor : ui.MutedInk;
        var label = StatusCapsuleLabel(connected ? 2 : authenticated ? 1 : 0, hunts.CurrentDataCenter ?? string.Empty);
        var dot = 8f * scale;
        var pad = StatusCapsulePad * scale;
        var maxLabel = MathF.Max(1f, width - pad * 2f - dot - StatusDotGap * scale);
        var fitted = Typography.FitText(label, maxLabel, TextStyles.FootnoteEmphasized);
        var labelSize = Typography.Measure(fitted, TextStyles.FootnoteEmphasized);
        var capsuleWidth = pad * 2f + dot + StatusDotGap * scale + labelSize.X;
        var rect = new Rect(origin, origin + new Vector2(capsuleWidth, StatusCapsuleHeight * scale));
        UiAnchors.Report("hunts.auth", rect);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var press = PressFx.Scale("hunts.status", hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left),
            Core.Animation.Motion.PressScaleControl);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        Material.ThemedGlass(drawList, min, max, (max.Y - min.Y) * 0.5f, scale, frameTheme);
        var dotCenter = new Vector2(min.X + pad + dot * 0.5f, rect.Center.Y);
        HuntsArt.HaloDot(drawList, dotCenter, dotInk, connected, scale);
        Typography.Draw(drawList, new Vector2(dotCenter.X + dot * 0.5f + StatusDotGap * scale,
            rect.Center.Y - labelSize.Y * 0.5f), fitted, ui.TitleInk, TextStyles.FootnoteEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            OpenAccount();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (StatusCapsuleHeight + StatusRowGap) * scale));
    }

    private string StatusCapsuleLabel(int state, string dataCenter)
    {
        if (state == statusLabelState && ReferenceEquals(Loc.Culture, statusLabelCulture) &&
            string.Equals(dataCenter, statusLabelDataCenter, StringComparison.Ordinal) && statusLabel.Length > 0)
        {
            return statusLabel;
        }

        statusLabelState = state;
        statusLabelCulture = Loc.Culture;
        statusLabelDataCenter = dataCenter;
        var stateText = state switch
        {
            2 => Loc.T(L.Hunts.StatusLive),
            1 => Loc.T(L.Hunts.StatusReconnecting),
            _ => Loc.T(L.Hunts.StatusSignIn),
        };
        statusLabel = dataCenter.Length > 0 ? Loc.T(L.Hunts.StatusOnDataCenter, dataCenter, stateText) : stateText;
        return statusLabel;
    }

    private void DrawNowBody(Rect body, float scale)
    {
        if (hunts.CurrentDataCenter is null)
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

        DrawSearch(scale);
        var now = DateTimeOffset.UtcNow;
        EnsureBoard(now);
        if (boardRowCount == 0)
        {
            DrawBoardEmpty(body);
            return;
        }

        DrawLiveSection(scale);
        DrawBoardSection(Loc.T(L.Hunts.SectionOpen), openRows, scale);
        DrawBoardSection(Loc.T(L.Hunts.SectionSoon), soonRows, scale);
        DrawBoardSection(Loc.T(L.Hunts.SectionWaiting), waitingRows, scale);
    }

    private void DrawSearch(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var field = new Rect(origin, origin + new Vector2(width, GlassField.HeightUnits * scale));
        GlassField.Search(ImGui.GetWindowDrawList(), field, "##huntsSearch", Loc.T(L.Hunts.SearchHint),
            ref searchQuery, frameTheme, scale, SearchMaxLength, false);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (GlassField.HeightUnits + SearchGap) * scale));
    }

    private void DrawSkeleton(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var area = new Rect(origin, origin + new Vector2(width, SkeletonAreaUnits * scale));
        Skeleton.Rows(ImGui.GetWindowDrawList(), area, SkeletonRowUnits, SkeletonGapUnits, scale);
        ImGui.Dummy(area.Size);
    }

    private void DrawBoardEmpty(Rect body)
    {
        if (searchQuery.Trim().Length > 0)
        {
            DrawEmpty(body, FontAwesomeIcon.Search, Loc.T(L.Hunts.NoMatchTitle), Loc.T(L.Hunts.NoMatchHint),
                string.Empty);
            return;
        }

        if (hunts.Windows.Length == 0)
        {
            DrawEmpty(body, FontAwesomeIcon.Crosshairs, Loc.T(L.Hunts.Empty), Loc.T(L.Hunts.EmptyHint), string.Empty);
            return;
        }

        if (DrawEmpty(body, FontAwesomeIcon.Filter, Loc.T(L.Hunts.FilteredOutTitle), Loc.T(L.Hunts.FilteredOutHint),
                Loc.T(L.Hunts.ClearFilters)))
        {
            UiFeedback.Play(UiSound.Refresh);
            filter.Reset();
            filterStore.Save(filter.ToSnapshot());
        }
    }

    private bool DrawFailed(Rect body)
    {
        if (!hunts.Failed)
        {
            return false;
        }

        if (DrawEmpty(body, FontAwesomeIcon.CloudDownloadAlt, Loc.T(L.Hunts.Failed), Loc.T(L.Hunts.FailedHint),
                Loc.T(L.Hunts.TryAgain)))
        {
            UiFeedback.Play(UiSound.Refresh);
            hunts.Retry();
        }

        return true;
    }

    private bool DrawEmpty(Rect body, FontAwesomeIcon icon, string title, string hint, string action)
    {
        var origin = ImGui.GetCursorScreenPos();
        var area = new Rect(origin,
            new Vector2(body.Max.X, MathF.Max(body.Max.Y, origin.Y + body.Height * EmptyAreaFraction)));
        return action.Length == 0
            ? DrawEmptyBody(area, icon, title, hint)
            : EmptyState.Draw(area, ui, icon, title, hint, action);
    }

    private bool DrawEmptyBody(Rect area, FontAwesomeIcon icon, string title, string hint)
    {
        EmptyState.Draw(area, ui, icon, title, hint);
        return false;
    }

    private void EnsureBoard(DateTimeOffset now)
    {
        var windows = hunts.Windows;
        var search = searchQuery.Trim();
        var language = configuration.Language;
        var current = !boardDirty
                      && ReferenceEquals(windows, boardSource)
                      && hunts.ActiveSpawnVersion == boardSpawnVersion
                      && filter.Revision == boardFilterRevision
                      && string.Equals(search, boardSearch, StringComparison.Ordinal)
                      && string.Equals(language, boardLanguage, StringComparison.Ordinal)
                      && now < boardRefreshAt;
        if (current)
        {
            return;
        }

        boardDirty = false;
        boardSource = windows;
        boardSpawnVersion = hunts.ActiveSpawnVersion;
        boardFilterRevision = filter.Revision;
        boardSearch = search;
        boardLanguage = language;
        boardRefreshAt = now + BoardRefreshInterval;
        RebuildBoard(windows, search, now);
    }

    private void RebuildBoard(HuntWindowDto[] windows, string search, DateTimeOffset now)
    {
        liveRows.Clear();
        openRows.Clear();
        soonRows.Clear();
        waitingRows.Clear();
        boardRowCount = 0;
        for (var index = 0; index < windows.Length; index++)
        {
            var window = windows[index];
            var mob = mobCatalog.Find(window.MobId);
            var spawned = hunts.IsSpawned(window.MobId, window.WorldId, window.ZoneInstance);
            if (mob is { Rank: "SS" } && !spawned)
            {
                continue;
            }

            var status = ResolveDisplayStatus(window, mob, now);
            if (!filter.Matches(window, mob, status))
            {
                continue;
            }

            var name = ResolveMobLabel(mob, window.MobId);
            if (search.Length > 0 && !name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var row = AcquireRow(boardRowCount++);
            FillRow(row, window, mob, status, name, now);
            SectionList(row.Section).Add(row);
        }

        liveRows.Sort(CompareRows);
        openRows.Sort(CompareRows);
        soonRows.Sort(CompareRows);
        waitingRows.Sort(CompareRows);
    }

    private HuntRow AcquireRow(int index)
    {
        while (rowPool.Count <= index)
        {
            rowPool.Add(new HuntRow());
        }

        return rowPool[index];
    }

    private List<HuntRow> SectionList(HuntBoardSection section) => section switch
    {
        HuntBoardSection.Live => liveRows,
        HuntBoardSection.Open => openRows,
        HuntBoardSection.Waiting => waitingRows,
        _ => soonRows,
    };

    private void FillRow(HuntRow row, HuntWindowDto window, HuntMobDefinition? mob, HuntWindowStatus status,
        string name, DateTimeOffset now)
    {
        var spawnedSince = hunts.SpawnedSince(window.MobId, window.WorldId, window.ZoneInstance);
        var hasGate = TryResolveConditionGate(mob, now, out var untilGate);
        row.Window = window;
        row.Mob = mob;
        row.Status = status;
        row.Section = HuntBoard.SectionFor(status);
        row.SortKey = HuntBoard.SortKey(row.Section, status, window, mob, now, spawnedSince,
            hasGate ? untilGate : null);
        row.Ring = HuntBoard.RingFraction(status, window, mob, now);
        row.Rank = mob?.Rank ?? string.Empty;
        row.Name = name;
        var key = window.MobId + window.WorldId + window.ZoneInstance.ToString(Loc.Culture);
        if (!string.Equals(key, row.MarqueeKey, StringComparison.Ordinal))
        {
            row.MarqueeKey = key;
            row.PressId = "hunts.live." + key;
            row.GoId = "hunts.live.go." + key;
        }
        row.ZoneId = ResolveMobZoneId(mob, window.MobId, window.WorldId, window.ZoneInstance);
        row.TerritoryId = zoneCatalog.ResolveTerritoryId(row.ZoneId);
        var place = ResolvePlace(window.WorldId, window.ZoneInstance, mob);
        var zone = ResolveZoneLabel(row.ZoneId);
        row.Subtitle = zone.Length > 0 ? Loc.T(L.Hunts.PlaceInZone, place, zone) : place;
        row.Reporters = string.Empty;
        row.HasFocus = false;
        row.Coordinate = null;
        FillRowText(row, window, mob, status, now, spawnedSince, hasGate ? untilGate : null);
        if (row.Section != HuntBoardSection.Live)
        {
            return;
        }

        row.Reporters = ResolveReporterLabel(window.MobId, window.WorldId, window.ZoneInstance);
        if (hunts.ConfirmedPoiIdFor(window.MobId, window.WorldId, window.ZoneInstance) is not { } poiId ||
            zoneCatalog.FindPoi(poiId) is not { } found)
        {
            return;
        }

        var (rawX, rawY) = found.Poi.ParsedLocation();
        var (normalizedX, normalizedY) = MapPixelMath.NormalizeToFullCanvas(rawX, rawY);
        row.Focus = new Vector2(normalizedX, normalizedY);
        row.HasFocus = true;
        row.Coordinate = zoneCatalog.ResolveCoordinate(poiId);
    }

    private void FillRowText(HuntRow row, HuntWindowDto window, HuntMobDefinition? mob, HuntWindowStatus status,
        DateTimeOffset now, DateTimeOffset? spawnedSince, TimeSpan? untilGate)
    {
        var percentage = HuntWindowMath.Percentage(window, mob, now);
        switch (status)
        {
            case HuntWindowStatus.Spawned:
            case HuntWindowStatus.Scheduled:
                var phase = ResolvePhaseLabel(window.MobId, window.WorldId, window.ZoneInstance, mob);
                var headline = status == HuntWindowStatus.Scheduled
                    ? Loc.T(L.Hunts.Scheduled)
                    : spawnedSince is { } since
                        ? Loc.T(L.Hunts.UpFor, HuntsText.Span(now - since))
                        : Loc.T(L.Hunts.Spawned);
                row.Primary = phase.Length > 0 ? Loc.T(L.Hunts.StatusWithPhase, headline, phase) : headline;
                row.Secondary = string.Empty;
                return;
            case HuntWindowStatus.Open:
                row.Primary = percentage is { } openPercentage ? HuntsText.Percent(openPercentage) : Loc.T(L.Hunts.Open);
                row.Secondary = HuntWindowMath.MinimumReachedAt(window, mob) is { } openedAt
                    ? Loc.T(L.Hunts.OpenedAgo, TimeText.Ago(openedAt))
                    : string.Empty;
                return;
            case HuntWindowStatus.Capped:
                row.Primary = Loc.T(L.Hunts.Capped);
                row.Secondary = percentage is { } cappedPercentage ? HuntsText.Percent(cappedPercentage) : string.Empty;
                return;
            case HuntWindowStatus.Closed:
                var untilMinimum = HuntWindowMath.TimeUntilMinimum(window, mob, now);
                row.Primary = untilMinimum is { } remaining ? HuntsText.Span(remaining) : Loc.T(L.Hunts.Closed);
                row.Secondary = HuntWindowMath.MinimumReachedAt(window, mob) is { } opensAt
                    ? Loc.T(L.Hunts.OpensAt, HuntsText.Moment(opensAt))
                    : string.Empty;
                return;
            case HuntWindowStatus.Unmet:
                row.Primary = Loc.T(L.Hunts.Unmet);
                row.Secondary = untilGate is { } gate ? TimeText.Until(gate) : string.Empty;
                return;
            default:
                row.Primary = Loc.T(L.Hunts.Unknown);
                row.Secondary = string.Empty;
                return;
        }
    }

    private void DrawLiveSection(float scale)
    {
        if (liveRows.Count == 0)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Hunts.SectionLive), TextStyles.FootnoteEmphasized, 6f);
        for (var index = 0; index < liveRows.Count; index++)
        {
            DrawLiveCard(liveRows[index], index == 0, scale);
        }

        Gap(HuntsArt.CardGap - HuntsArt.TileGap);
    }

    private void DrawLiveCard(HuntRow row, bool first, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = LiveCardHeight * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
        ImGui.Dummy(new Vector2(width, height + HuntsArt.TileGap * scale));
        if (first)
        {
            listReadyForTour = true;
            UiAnchors.Report("hunts.row.first", card);
        }

        if (!ImGui.IsRectVisible(card.Min, card.Max))
        {
            return;
        }

        var pad = HuntsArt.CardPadding * scale * 0.75f;
        var thumb = LiveThumbSize * scale;
        var goRect = GoButtonRect(card, pad, scale);
        var canTravel = row.TerritoryId != 0 && HuntDataCenterWorlds.WorldRowId(row.Window.WorldId) != 0;
        var overGo = canTravel && UiInteract.Hover(goRect.Min, goRect.Max);
        var hovered = !overGo && UiInteract.Hover(card.Min, card.Max);
        var press = PressFx.Scale(row.PressId, hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left),
            Core.Animation.Motion.PressScaleCard);
        var half = card.Size * 0.5f * press;
        var min = card.Center - half;
        var max = card.Center + half;
        ui.Card(drawList, min, max, HuntsArt.CardRadius * scale, elevated: true);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, HuntsArt.CardRadius * scale, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var thumbMin = new Vector2(min.X + pad, min.Y + (max.Y - min.Y - thumb) * 0.5f);
        var thumbMax = thumbMin + new Vector2(thumb, thumb);
        var texture = row.TerritoryId != 0 ? zoneMapTextures.ForTerritory(row.TerritoryId) : null;
        if (!HuntsArt.MapThumbnail(drawList, texture, thumbMin, thumbMax, thumb * Metrics.Radius.TileFactor, row.Focus,
                row.HasFocus, HuntsArt.LiveColor, scale))
        {
            HuntsArt.RankTile(drawList, thumbMin, thumb, row.Rank, HuntsArt.RankColor(row.Rank, ui.Accent));
        }

        var left = thumbMax.X + HuntsArt.RowGap * scale;
        var right = (canTravel ? goRect.Min.X : max.X - pad) - HuntsArt.RowGap * scale;
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var primaryHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var lineGap = HuntsArt.LineGap * scale;
        var blockHeight = nameHeight + lineGap + primaryHeight + lineGap + lineHeight +
                          (row.Reporters.Length > 0 ? lineGap + lineHeight : 0f);
        var top = (min.Y + max.Y - blockHeight) * 0.5f;
        var nameLeft = left;
        if (row.Rank.Length > 0)
        {
            nameLeft += InlineBadge.Draw(drawList, left, top + nameHeight * 0.5f, row.Rank,
                HuntsArt.RankColor(row.Rank, ui.MutedInk), scale) + 6f * scale;
        }

        Marquee.DrawLeftAuto(drawList, new MarqueeId("hunts.live.name.", row.MarqueeKey), row.Name, nameLeft, top,
            MathF.Max(1f, right - nameLeft), TextStyles.Headline, ui.TitleInk);
        top += nameHeight + lineGap;
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(row.Primary, MathF.Max(1f, right - left), TextStyles.SubheadlineEmphasized),
            HuntsArt.LiveColor, TextStyles.SubheadlineEmphasized);
        top += primaryHeight + lineGap;
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(row.Subtitle, MathF.Max(1f, right - left), TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        if (row.Reporters.Length > 0)
        {
            top += lineHeight + lineGap;
            Typography.Draw(drawList, new Vector2(left, top),
                Typography.FitText(row.Reporters, MathF.Max(1f, right - left), TextStyles.Footnote), ui.MutedInk,
                TextStyles.Footnote);
        }

        if (canTravel && ui.PillButton(goRect, Loc.T(L.Hunts.Go), true, row.GoId))
        {
            UiFeedback.Play(UiSound.Tap);
            TravelToRow(row);
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            OpenDetail(row.Window, DisplayName);
        }
    }

    private static Rect GoButtonRect(Rect card, float pad, float scale)
    {
        var size = new Vector2(GoButtonWidth * scale, GoButtonHeight * scale);
        var min = new Vector2(card.Max.X - pad - size.X, card.Center.Y - size.Y * 0.5f);
        return new Rect(min, min + size);
    }

    private void TravelToRow(HuntRow row)
    {
        var worldId = HuntDataCenterWorlds.WorldRowId(row.Window.WorldId);
        if (row.TerritoryId == 0 || worldId == 0)
        {
            return;
        }

        NavigateToCoordinate(row.TerritoryId, worldId, ResolveMapId(row.TerritoryId), row.Coordinate, row.Coordinate,
            row.Window.ZoneInstance);
    }

    private void DrawBoardSection(string title, List<HuntRow> rows, float scale)
    {
        if (rows.Count == 0)
        {
            return;
        }

        ui.SectionLabel(title, TextStyles.FootnoteEmphasized, 6f);
        var card = GroupCard.Begin(ui, rows.Count, BoardRowHeight);
        card.SeparatorInset = RingSize + HuntsArt.RowGap;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = card.NextRow();
            var bounds = new Rect(new Vector2(card.Bounds.Min.X, row.Min.Y), new Vector2(card.Bounds.Max.X, row.Max.Y));
            if (!listReadyForTour && index == 0)
            {
                listReadyForTour = true;
                UiAnchors.Report("hunts.row.first", bounds);
            }

            if (!ImGui.IsRectVisible(bounds.Min, bounds.Max))
            {
                continue;
            }

            DrawBoardRow(row, bounds, rows[index], scale, DisplayName);
        }

        card.End();
        Gap(HuntsArt.CardGap * 0.5f);
    }

    private void DrawBoardRow(Rect row, Rect bounds, HuntRow model, float scale, string backTitle,
        bool overChild = false)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = !overChild && UiInteract.Hover(bounds.Min, bounds.Max);
        if (hovered)
        {
            drawList.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var ink = HuntsArt.StatusColor(model.Status, ui.MutedInk);
        var ring = RingSize * scale;
        var ringCenter = new Vector2(row.Min.X + ring * 0.5f, row.Center.Y);
        HuntsArt.StatusRing(drawList, ringCenter, ring * 0.5f, RingThickness * scale, model.Ring, ink, model.Rank,
            HuntsArt.RankColor(model.Rank, ui.TitleInk));
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subHeight = Typography.LineHeight(TextStyles.Footnote);
        var lineGap = HuntsArt.LineGap * scale;
        var top = row.Center.Y - (titleHeight + lineGap + subHeight) * 0.5f;
        var primaryStyle = TextStyles.SubheadlineEmphasized;
        var primaryWidth = Typography.Measure(model.Primary, primaryStyle).X;
        var secondaryWidth = model.Secondary.Length > 0 ? Typography.Measure(model.Secondary, TextStyles.Footnote).X : 0f;
        var primaryInk = model.Status is HuntWindowStatus.Closed or HuntWindowStatus.Unknown ? ui.TitleInk : ink;
        Typography.Draw(drawList, new Vector2(row.Max.X - primaryWidth,
            top + (titleHeight - Typography.LineHeight(primaryStyle)) * 0.5f), model.Primary, primaryInk, primaryStyle);
        if (secondaryWidth > 0f)
        {
            Typography.Draw(drawList, new Vector2(row.Max.X - secondaryWidth, top + titleHeight + lineGap),
                model.Secondary, ui.MutedInk, TextStyles.Footnote);
        }

        var textLeft = row.Min.X + ring + HuntsArt.RowGap * scale;
        var nameRight = row.Max.X - primaryWidth - HuntsArt.RowGap * scale;
        var subRight = row.Max.X - secondaryWidth - HuntsArt.RowGap * scale;
        Marquee.DrawLeftAuto(drawList, new MarqueeId("hunts.row.name.", model.MarqueeKey), model.Name, textLeft, top,
            MathF.Max(1f, nameRight - textLeft), TextStyles.Headline, ui.TitleInk);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight + lineGap),
            Typography.FitText(model.Subtitle, MathF.Max(1f, subRight - textLeft), TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        if (UiInteract.Click(bounds.Min, bounds.Max, hovered))
        {
            OpenDetail(model.Window, backTitle);
        }
    }
}
