using Aetherphone.Core;
using Aetherphone.Core.Geography;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct GeoScopeChoice(bool Picked, GeoScopeKind Kind, string Value);

internal sealed class GeoScopeScreen
{
    private const float RowHeight = 56f;
    private const float WorldRowHeight = 44f;
    private const float WorldIndent = 30f;
    private const float CardInset = 14f;
    private const float CellPadX = SocialChrome.CellPadX;
    private const float RadioSize = 22f;
    private const float ChevronReach = 16f;

    private static readonly TextStyle SectionStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle RowTitleStyle = TextStyles.BodyEmphasized;
    private static readonly TextStyle RowHelpStyle = TextStyles.Footnote;
    private static readonly TextStyle WorldStyle = TextStyles.Body;

    private enum RowAction : byte
    {
        None,
        Pick,
        Expand,
    }

    private readonly SocialInk ink;
    private readonly AppSkin ui;
    private readonly TextStyle titleStyle;
    private readonly Dictionary<string, string> worldCountLabels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, string> allOfLabels = new();
    private LanguageInfo? labelLanguage;
    private string expandedDataCenter = string.Empty;
    private GeoScopeKind kind;
    private string value = string.Empty;
    private bool worlds;
    private GeoScopeChoice choice;

    public GeoScopeScreen(SocialInk ink, AppSkin ui, in TextStyle titleStyle)
    {
        this.ink = ink;
        this.ui = ui;
        this.titleStyle = titleStyle;
    }

    public GeoScopeChoice Draw(Rect area, string title, Action back, string homeWorld, GeoScopeKind currentKind,
        string currentValue, bool pickWorlds)
    {
        kind = currentKind;
        value = currentValue;
        worlds = pickWorlds;
        choice = default;
        var scale = UiScale.Current;
        SocialChrome.DrawScreenHeader(area, title, ink, back, titleStyle, 0f, string.Empty, true, true);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawNearYouCard(homeWorld, scale);
            var regions = WorldGeography.Regions;
            for (var index = 0; index < regions.Length; index++)
            {
                DrawRegionCard(regions[index], scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }

        return choice;
    }

    private void DrawNearYouCard(string homeWorld, float scale)
    {
        SocialChrome.DrawSectionLabel(Loc.T(L.Venues.NearYouSection), ink, SectionStyle);
        var home = WorldGeography.DataCenterOfWorld(homeWorld);
        var region = home is null ? null : WorldGeography.RegionById(home.RegionId);
        var rows = 1;
        if (home is not null)
        {
            rows += worlds ? 2 : 1;
            rows += region is null ? 0 : 1;
        }

        var card = BeginCard(rows * RowHeight * scale, scale);
        var row = 0;
        if (home is not null)
        {
            if (worlds && Row(card, row++, rows, homeWorld, Loc.T(L.Venues.YourWorld), kind == GeoScopeKind.MyWorld,
                    scale))
            {
                Pick(GeoScopeKind.MyWorld, string.Empty);
            }

            if (Row(card, row++, rows, home.Name, Loc.T(L.Venues.YourDataCenter), kind == GeoScopeKind.MyDataCenter,
                    scale))
            {
                Pick(GeoScopeKind.MyDataCenter, string.Empty);
            }

            if (region is not null && Row(card, row++, rows, Loc.T(region.Label), Loc.T(L.Venues.YourRegion),
                    kind == GeoScopeKind.MyRegion, scale))
            {
                Pick(GeoScopeKind.MyRegion, string.Empty);
            }
        }

        if (Row(card, row, rows, Loc.T(L.Venues.Everywhere), Loc.T(L.Venues.EverywhereHint),
                kind == GeoScopeKind.Everywhere, scale))
        {
            Pick(GeoScopeKind.Everywhere, string.Empty);
        }

        EndCard(card, scale);
    }

    private void DrawRegionCard(GeoRegionInfo region, float scale)
    {
        SocialChrome.DrawSectionLabel(Loc.T(region.Label), ink, SectionStyle);
        var dataCenters = region.DataCenters;
        var expanded = worlds ? ExpandedIn(region) : null;
        var rows = 1 + dataCenters.Length;
        var worldRows = expanded is null ? 0 : expanded.Worlds.Length;
        var card = BeginCard(rows * RowHeight * scale + worldRows * WorldRowHeight * scale, scale);
        if (Row(card, 0, rows + worldRows, AllOfLabel(region), string.Empty,
                kind == GeoScopeKind.Region && string.Equals(value, region.Key, StringComparison.Ordinal), scale))
        {
            Pick(GeoScopeKind.Region, region.Key);
        }

        var top = card.Min.Y + RowHeight * scale;
        for (var index = 0; index < dataCenters.Length; index++)
        {
            var dataCenter = dataCenters[index];
            var isExpanded = ReferenceEquals(dataCenter, expanded);
            var rowRect = new Rect(new Vector2(card.Min.X, top), new Vector2(card.Max.X, top + RowHeight * scale));
            var last = index == dataCenters.Length - 1 && !isExpanded;
            var selected = kind == GeoScopeKind.DataCenter &&
                           string.Equals(value, dataCenter.Name, StringComparison.OrdinalIgnoreCase);
            var action = DataCenterRow(rowRect, dataCenter, selected, isExpanded, last, scale);
            if (action == RowAction.Pick)
            {
                Pick(GeoScopeKind.DataCenter, dataCenter.Name);
            }
            else if (action == RowAction.Expand)
            {
                expandedDataCenter = isExpanded ? string.Empty : dataCenter.Name;
            }

            top = rowRect.Max.Y;
            if (!isExpanded)
            {
                continue;
            }

            for (var worldIndex = 0; worldIndex < dataCenter.Worlds.Length; worldIndex++)
            {
                var world = dataCenter.Worlds[worldIndex];
                var worldRect = new Rect(new Vector2(card.Min.X, top),
                    new Vector2(card.Max.X, top + WorldRowHeight * scale));
                var worldSelected = kind == GeoScopeKind.World &&
                                    string.Equals(value, world, StringComparison.OrdinalIgnoreCase);
                var lastWorld = index == dataCenters.Length - 1 && worldIndex == dataCenter.Worlds.Length - 1;
                if (WorldRow(worldRect, world, worldSelected, lastWorld, scale))
                {
                    Pick(GeoScopeKind.World, world);
                }

                top = worldRect.Max.Y;
            }
        }

        EndCard(card, scale);
    }

    private GeoDataCenterInfo? ExpandedIn(GeoRegionInfo region)
    {
        if (expandedDataCenter.Length == 0)
        {
            return null;
        }

        for (var index = 0; index < region.DataCenters.Length; index++)
        {
            if (string.Equals(region.DataCenters[index].Name, expandedDataCenter, StringComparison.OrdinalIgnoreCase))
            {
                return region.DataCenters[index];
            }
        }

        return null;
    }

    private Rect BeginCard(float height, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var card = new Rect(new Vector2(origin.X + pad, origin.Y),
            new Vector2(origin.X + width - pad, origin.Y + height));
        ui.Card(ImGui.GetWindowDrawList(), card.Min, card.Max, Metrics.Radius.Card * scale, elevated: true);
        return card;
    }

    private static void EndCard(Rect card, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        ImGui.SetCursorScreenPos(new Vector2(card.Min.X - CellPadX * scale, card.Min.Y));
        ImGui.Dummy(new Vector2(width, card.Height + Metrics.Space.Sm * scale));
    }

    private bool Row(Rect card, int index, int count, string title, string subtitle, bool selected, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = RowHeight * scale;
        var min = new Vector2(card.Min.X, card.Min.Y + index * rowHeight);
        var max = new Vector2(card.Max.X, min.Y + rowHeight);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            DrawRowHover(drawList, min, max, index == 0, index == count - 1, Metrics.Radius.Card * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var inset = CardInset * scale;
        var centerY = min.Y + rowHeight * 0.5f;
        var radioCenter = new Vector2(max.X - inset - RadioSize * 0.5f * scale, centerY);
        DrawRadio(drawList, radioCenter, selected, scale);
        DrawRowText(drawList, min.X + inset, radioCenter.X - 20f * scale, centerY, title, subtitle, selected);
        if (index < count - 1)
        {
            FeedCell.Hairline(drawList, min.X + inset, max.X, max.Y, ink.Hairline);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private RowAction DataCenterRow(Rect row, GeoDataCenterInfo dataCenter, bool selected, bool expanded, bool last,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            DrawRowHover(drawList, row.Min, row.Max, false, last, Metrics.Radius.Card * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var rowClicked = UiInteract.Click(row.Min, row.Max, hovered);
        var inset = CardInset * scale;
        var centerY = row.Center.Y;
        var radioCenter = new Vector2(row.Max.X - inset - RadioSize * 0.5f * scale, centerY);
        DrawRadio(drawList, radioCenter, selected, scale);
        var textRight = radioCenter.X - 20f * scale;
        var chevronClicked = false;
        if (worlds)
        {
            var chevronCenter = new Vector2(radioCenter.X - 36f * scale, centerY);
            var chevronExtent = new Vector2(ChevronReach * scale, ChevronReach * scale);
            var chevronHovered = UiInteract.Hover(chevronCenter - chevronExtent, chevronCenter + chevronExtent);
            if (chevronHovered)
            {
                drawList.AddCircleFilled(chevronCenter, ChevronReach * scale, ImGui.GetColorU32(ink.FieldFill), 24);
            }

            PhoneIcon.Draw(drawList, chevronCenter, expanded ? PhoneIcons.ChevronUp : PhoneIcons.ChevronDown,
                chevronHovered ? ink.TitleInk : ink.MutedInk, ChevronReach * scale);
            chevronClicked = UiInteract.Click(chevronCenter - chevronExtent, chevronCenter + chevronExtent,
                chevronHovered);
            textRight = chevronCenter.X - 22f * scale;
        }

        DrawRowText(drawList, row.Min.X + inset, textRight, centerY, dataCenter.Name, WorldCountLabel(dataCenter),
            selected);
        if (!last)
        {
            FeedCell.Hairline(drawList, row.Min.X + inset, row.Max.X, row.Max.Y, ink.Hairline);
        }

        if (chevronClicked)
        {
            return RowAction.Expand;
        }

        return rowClicked ? RowAction.Pick : RowAction.None;
    }

    private bool WorldRow(Rect row, string world, bool selected, bool last, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            DrawRowHover(drawList, row.Min, row.Max, false, last, Metrics.Radius.Card * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var inset = CardInset * scale;
        var left = row.Min.X + inset + WorldIndent * scale;
        var radioCenter = new Vector2(row.Max.X - inset - RadioSize * 0.5f * scale, row.Center.Y);
        DrawRadio(drawList, radioCenter, selected, scale);
        drawList.AddCircleFilled(new Vector2(left - 14f * scale, row.Center.Y), 2.5f * scale,
            ImGui.GetColorU32(ink.FaintInk), 12);
        var height = Typography.LineHeight(WorldStyle);
        Typography.Draw(drawList, new Vector2(left, row.Center.Y - height * 0.5f),
            Typography.FitText(world, MathF.Max(1f, radioCenter.X - 20f * scale - left), WorldStyle),
            selected ? ink.TitleInk : ink.BodyInk, WorldStyle);
        if (!last)
        {
            FeedCell.Hairline(drawList, left, row.Max.X, row.Max.Y, ink.Hairline);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private void DrawRowHover(ImDrawListPtr drawList, Vector2 min, Vector2 max, bool first, bool last, float rounding)
    {
        var flags = first && last ? ImDrawFlags.RoundCornersAll
            : first ? ImDrawFlags.RoundCornersTop
            : last ? ImDrawFlags.RoundCornersBottom
            : ImDrawFlags.RoundCornersNone;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(ink.HoverTint),
            flags == ImDrawFlags.RoundCornersNone ? 0f : rounding, flags);
    }

    private void DrawRadio(ImDrawListPtr drawList, Vector2 center, bool selected, float scale) =>
        PhoneIcon.Draw(drawList, center, selected ? PhoneIcons.CircleCheckFilled : PhoneIcons.Circle,
            selected ? ink.Accent : ink.FaintInk, RadioSize * scale);

    private void DrawRowText(ImDrawListPtr drawList, float left, float right, float centerY, string title,
        string subtitle, bool selected)
    {
        var width = MathF.Max(1f, right - left);
        var titleHeight = Typography.LineHeight(RowTitleStyle);
        var subtitleHeight = subtitle.Length > 0 ? Typography.LineHeight(RowHelpStyle) : 0f;
        var top = centerY - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(title, width, RowTitleStyle),
            selected ? ink.AccentLink : ink.TitleInk, RowTitleStyle);
        if (subtitle.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(left, top + titleHeight),
                Typography.FitText(subtitle, width, RowHelpStyle), ink.MutedInk, RowHelpStyle);
        }
    }

    private string WorldCountLabel(GeoDataCenterInfo dataCenter)
    {
        SyncLanguage();
        if (worldCountLabels.TryGetValue(dataCenter.Name, out var label))
        {
            return label;
        }

        label = Loc.T(L.Venues.WorldCount, dataCenter.Worlds.Length.ToString(Loc.Culture));
        worldCountLabels[dataCenter.Name] = label;
        return label;
    }

    private string AllOfLabel(GeoRegionInfo region)
    {
        SyncLanguage();
        if (allOfLabels.TryGetValue(region.Id, out var label))
        {
            return label;
        }

        label = Loc.T(L.Venues.AllOfRegion, Loc.T(region.Label));
        allOfLabels[region.Id] = label;
        return label;
    }

    private void SyncLanguage()
    {
        if (ReferenceEquals(labelLanguage, Loc.Current))
        {
            return;
        }

        worldCountLabels.Clear();
        allOfLabels.Clear();
        labelLanguage = Loc.Current;
    }

    private void Pick(GeoScopeKind pickedKind, string pickedValue) =>
        choice = new GeoScopeChoice(true, pickedKind, pickedValue);
}
