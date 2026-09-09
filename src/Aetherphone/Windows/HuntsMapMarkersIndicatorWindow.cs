using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherphone.Windows;

internal sealed unsafe class HuntsMapMarkersIndicatorWindow : Window
{
    private const ImGuiWindowFlags ChipFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar |
                                               ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
                                               ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground |
                                               ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing |
                                               ImGuiWindowFlags.NoNavInputs | ImGuiWindowFlags.NoNavFocus |
                                               ImGuiWindowFlags.NoSavedSettings;

    private const string AreaMapAddonName = "AreaMap";
    private const float CornerInset = 10f;
    private const float ChipHeight = 28f;
    private const float SidePadding = 12f;
    private const float IconGap = 7f;
    private const float IconScale = 0.66f;
    private const float IconWidth = 11f;
    private const float TextScale = 0.8f;
    private const float InstanceTextScale = 0.7f;
    private const float InstanceRowGap = 3f;
    private const float ToggleRowHeight = 20f;
    private const float ToggleGap = 8f;
    private const float ToggleTextScale = 0.72f;
    private const float ToggleChevronWidth = 9f;
    private const float ToggleChevronGap = 6f;
    private const float ToggleChevronScale = 0.5f;
    private const float LegendTopGap = 6f;
    private const float LegendRowHeight = 30f;
    private const float LegendIconSize = 25f;
    private const float LegendIconBoost = 1.3f;
    private const float LegendIconGap = 6f;
    private const float LegendTextScale = 0.9f;
    private const int LegendCount = 7;
    private const float YOffset = 18f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private readonly HuntsMapMarkers markers;
    private readonly ThemeProvider themes;
    private string? instanceLabel;
    private int? cachedInstance;
    private string cachedInstanceLanguage = string.Empty;
    private bool legendExpanded;

    public HuntsMapMarkersIndicatorWindow(HuntsMapMarkers markers, ThemeProvider themes)
        : base($"{AepConstants.Name}##HuntsMapMarkersIndicator", ChipFlags)
    {
        this.markers = markers;
        this.themes = themes;
        IsOpen = true;
        RespectCloseHotkey = false;
    }

    public override bool DrawConditions() => markers.HasActiveMarkers && TryGetAreaMapBounds(out _, out _);

    public override void PreDraw()
    {
        TryGetAreaMapBounds(out var mapPosition, out var mapSize);
        var scale = UiScale.Global;
        var label = Loc.T(L.Hunts.NativeMapMarkersIndicator);
        var labelSize = Typography.Measure(label, TextScale, FontWeight.SemiBold);
        var headerRowWidth = (IconWidth + IconGap) * scale + labelSize.X;

        var shownInstance = markers.ShownInstance;
        if (shownInstance != cachedInstance || !string.Equals(Loc.Current.Code, cachedInstanceLanguage,
                StringComparison.Ordinal))
        {
            cachedInstance = shownInstance;
            cachedInstanceLanguage = Loc.Current.Code;
            instanceLabel = shownInstance is { } instance
                ? string.Format(Loc.T(L.Hunts.NativeMapMarkersInstanceIndicator), instance)
                : null;
        }

        var instanceSize = instanceLabel is { Length: > 0 }
            ? Typography.Measure(instanceLabel, InstanceTextScale, FontWeight.Regular)
            : Vector2.Zero;

        var toggleLabel = Loc.T(L.Hunts.NativeMapLegendToggle);
        var toggleTextSize = Typography.Measure(toggleLabel, ToggleTextScale, FontWeight.SemiBold);
        var toggleRowWidth = toggleTextSize.X + (ToggleChevronGap + ToggleChevronWidth) * scale;

        var legendRowWidth = 0f;
        if (legendExpanded)
        {
            legendRowWidth = MathF.Max(legendRowWidth, LegendRowWidth(Loc.T(L.Hunts.NativeMapLegendCandidate), scale));
            legendRowWidth = MathF.Max(legendRowWidth, LegendRowWidth(Loc.T(L.Hunts.NativeMapLegendSighted), scale));
            legendRowWidth = MathF.Max(legendRowWidth, LegendRowWidth(Loc.T(L.Hunts.NativeMapLegendConfirmed), scale));
            legendRowWidth =
                MathF.Max(legendRowWidth, LegendRowWidth(Loc.T(L.Hunts.NativeMapLegendActiveMinion), scale));
            legendRowWidth = MathF.Max(legendRowWidth, LegendRowWidth(Loc.T(L.Hunts.NativeMapLegendSsSpawn), scale));
            legendRowWidth =
                MathF.Max(legendRowWidth, LegendRowWidth(Loc.T(L.Hunts.NativeMapLegendFateInactive), scale));
            legendRowWidth = MathF.Max(legendRowWidth, LegendRowWidth(Loc.T(L.Hunts.NativeMapLegendFateActive), scale));
        }

        var contentWidth = MathF.Max(headerRowWidth, MathF.Max(instanceSize.X, MathF.Max(toggleRowWidth, legendRowWidth)));
        var pixelWidth = contentWidth + SidePadding * 2f * scale;
        var pixelHeight = ChipHeight * scale;
        if (instanceLabel is { Length: > 0 })
        {
            pixelHeight += InstanceRowGap * scale + instanceSize.Y;
        }

        pixelHeight += ToggleGap * scale + ToggleRowHeight * scale;

        if (legendExpanded)
        {
            pixelHeight += LegendTopGap * scale + LegendRowHeight * scale * LegendCount;
        }

        Size = new Vector2(pixelWidth / scale, pixelHeight / scale);
        SizeCondition = ImGuiCond.Always;
        Position = new Vector2(mapPosition.X + mapSize.X - pixelWidth - CornerInset * scale,
            mapPosition.Y + (CornerInset + YOffset) * scale);
        PositionCondition = ImGuiCond.Always;
    }

    private static float LegendRowWidth(string label, float scale) =>
        (LegendIconSize + LegendIconGap) * scale + Typography.Measure(label, LegendTextScale, FontWeight.Regular).X;

    public override void Draw()
    {
        var scale = UiScale.Global;
        var theme = themes.Chrome;
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        DrawChip(theme, min, max, scale);
    }

    private void DrawChip(PhoneTheme theme, Vector2 min, Vector2 max, float scale)
    {
        var drawList = ImGui.GetForegroundDrawList();
        var rounding = ChipHeight * scale * 0.5f;
        Elevation.Floating(drawList, min, max, rounding, scale, 1f);
        var surface = IconTile.Surface(theme.Accent);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(surface));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.18f)), scale);
        var ink = White;

        var headerMin = min;
        var headerMax = new Vector2(max.X, min.Y + ChipHeight * scale);
        var label = Loc.T(L.Hunts.NativeMapMarkersIndicator);
        var labelSize = Typography.Measure(label, TextScale, FontWeight.SemiBold);
        var contentLeft = min.X + SidePadding * scale;
        var headerCenterY = (headerMin.Y + headerMax.Y) * 0.5f;
        AppSkin.Icon(drawList, new Vector2(contentLeft + IconWidth * 0.5f * scale, headerCenterY),
            FontAwesomeIcon.MapMarkerAlt.ToIconString(), ink, IconScale);
        Typography.Draw(drawList,
            new Vector2(contentLeft + (IconWidth + IconGap) * scale, headerCenterY - labelSize.Y * 0.5f), label, ink,
            TextScale, FontWeight.SemiBold);

        var top = headerMax.Y;
        if (instanceLabel is { Length: > 0 })
        {
            var instanceSize = Typography.Measure(instanceLabel, InstanceTextScale, FontWeight.Regular);
            var instanceLeft = (min.X + max.X) * 0.5f - instanceSize.X * 0.5f;
            top += InstanceRowGap * scale;
            Typography.Draw(drawList, new Vector2(instanceLeft, top), instanceLabel,
                new Vector4(ink.X, ink.Y, ink.Z, 0.75f), InstanceTextScale, FontWeight.Regular);
            top += instanceSize.Y;
        }

        top += ToggleGap * scale;
        var toggleMin = new Vector2(min.X, top);
        var toggleMax = new Vector2(max.X, top + ToggleRowHeight * scale);
        var toggleHovered = ImGui.IsMouseHoveringRect(toggleMin, toggleMax);
        if (toggleHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                legendExpanded = !legendExpanded;
            }
        }

        DrawToggleRow(drawList, toggleMin, toggleMax, scale, ink, legendExpanded);
        top = toggleMax.Y;

        if (!legendExpanded)
        {
            return;
        }

        top += LegendTopGap * scale;
        DrawLegendRow(drawList, contentLeft, ref top, scale, ink, HuntsMapMarkerIcons.CandidateIconId,
            Loc.T(L.Hunts.NativeMapLegendCandidate), boosted: true);
        DrawLegendRow(drawList, contentLeft, ref top, scale, ink, HuntsMapMarkerIcons.SightedIconId,
            Loc.T(L.Hunts.NativeMapLegendSighted), boosted: true);
        DrawLegendRow(drawList, contentLeft, ref top, scale, ink, HuntsMapMarkerIcons.ConfirmedIconId,
            Loc.T(L.Hunts.NativeMapLegendConfirmed), boosted: true);
        DrawLegendRow(drawList, contentLeft, ref top, scale, ink, HuntsMapMarkerIcons.ActiveMinionIconId,
            Loc.T(L.Hunts.NativeMapLegendActiveMinion), boosted: true);
        DrawLegendRow(drawList, contentLeft, ref top, scale, ink, HuntsMapMarkerIcons.SsSpawnIconId,
            Loc.T(L.Hunts.NativeMapLegendSsSpawn), boosted: true);
        DrawLegendRow(drawList, contentLeft, ref top, scale, ink, HuntsMapMarkerIcons.FateInactiveIconId,
            Loc.T(L.Hunts.NativeMapLegendFateInactive), boosted: false);
        DrawLegendRow(drawList, contentLeft, ref top, scale, ink, HuntsMapMarkerIcons.FateActiveIconId,
            Loc.T(L.Hunts.NativeMapLegendFateActive), boosted: false);
    }

    private static void DrawToggleRow(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale, Vector4 ink,
        bool expanded)
    {
        var label = Loc.T(L.Hunts.NativeMapLegendToggle);
        var labelSize = Typography.Measure(label, ToggleTextScale, FontWeight.SemiBold);
        var rowWidth = labelSize.X + (ToggleChevronGap + ToggleChevronWidth) * scale;
        var rowLeft = (min.X + max.X) * 0.5f - rowWidth * 0.5f;
        var rowCenterY = (min.Y + max.Y) * 0.5f;
        var toggleInk = new Vector4(ink.X, ink.Y, ink.Z, 0.85f);
        Typography.Draw(drawList, new Vector2(rowLeft, rowCenterY - labelSize.Y * 0.5f), label, toggleInk,
            ToggleTextScale, FontWeight.SemiBold);
        var chevronIcon = expanded ? FontAwesomeIcon.ChevronUp : FontAwesomeIcon.ChevronDown;
        var chevronCenterX = rowLeft + labelSize.X + ToggleChevronGap * scale + ToggleChevronWidth * 0.5f * scale;
        AppSkin.Icon(drawList, new Vector2(chevronCenterX, rowCenterY), chevronIcon.ToIconString(), toggleInk,
            ToggleChevronScale);
    }

    private static void DrawLegendRow(ImDrawListPtr drawList, float contentLeft, ref float top, float scale,
        Vector4 ink, uint iconId, string label, bool boosted)
    {
        var rowCenterY = top + LegendRowHeight * scale * 0.5f;
        var slotCenterX = contentLeft + LegendIconSize * 0.5f * scale;
        var drawnSize = boosted ? LegendIconSize * LegendIconBoost : LegendIconSize;
        var iconMin = new Vector2(slotCenterX - drawnSize * 0.5f * scale, rowCenterY - drawnSize * 0.5f * scale);
        var iconMax = iconMin + new Vector2(drawnSize * scale, drawnSize * scale);
        GameIconTile.Draw(drawList, Plugin.TextureProvider, iconId, iconMin, iconMax, 3f * scale, scale);
        var labelSize = Typography.Measure(label, LegendTextScale, FontWeight.Regular);
        Typography.Draw(drawList,
            new Vector2(contentLeft + (LegendIconSize + LegendIconGap) * scale, rowCenterY - labelSize.Y * 0.5f),
            label, new Vector4(ink.X, ink.Y, ink.Z, 0.9f), LegendTextScale, FontWeight.Regular);
        top += LegendRowHeight * scale;
    }

    private static bool TryGetAreaMapBounds(out Vector2 position, out Vector2 size)
    {
        position = default;
        size = default;
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AreaMapAddonName).Address;
        if (addon == null || !addon->IsVisible)
        {
            return false;
        }

        position = new Vector2(addon->X, addon->Y);
        size = new Vector2(addon->GetScaledWidth(true), addon->GetScaledHeight(true));
        return true;
    }
}
