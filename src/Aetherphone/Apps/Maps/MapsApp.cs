using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Maps;

internal sealed partial class MapsApp : IPhoneApp
{
    private enum MapsPage : byte
    {
        Home,
        Expansion,
        Place,
    }

    private enum StageMode : byte
    {
        Hero,
        Live,
        Plot,
    }

    private const int PageCapacity = 4;
    private const int RecentsCapacity = 8;
    private const float CanvasUnits = MapPixelMath.FullCanvasSize;
    private const float DefaultZoom = 0.7f;
    private const float MaximumZoom = 2.5f;
    private const float FocusZoom = 0.9f;
    private const float ControlsTopInset = 8f;
    private const float ChipClearance = 140f;
    private const float PinLabelWidth = 150f;

    public string Id => "maps";
    public string DisplayName => Loc.T(L.Apps.Maps);
    public string Glyph => "Ma";
    public int BadgeCount => 0;
    public bool WantsSystemTheme => true;

    private readonly MapData maps;
    private readonly Configuration configuration;
    private readonly ZoneMapTextures zoneMapTextures;
    private readonly MinimapReader reader;
    private readonly ZoneMapLadder ladder;
    private readonly MapCamera camera = new();
    private readonly MapDrawer drawer = new();
    private readonly MapsPage[] pages = new MapsPage[PageCapacity];
    private readonly HashSet<uint> favorites = new();
    private readonly Vector4 accent = AppAccents.For("maps");
    private int pageDepth;
    private byte openExpansion;
    private MapAetheryte? openPlace;
    private bool lifestreamAvailable;
    private PhoneTheme theme = PhoneTheme.Default;
    private MapLocation location;
    private StageMode mode;
    private uint stageMapId;
    private IReadOnlyList<MapAetheryte> stagePins = Array.Empty<MapAetheryte>();
    private MapLocation plotSource;
    private bool hasPlot;
    private uint plotMapId;
    private float plotU;
    private float plotV;
    private string plotLabel = string.Empty;
    private Rect lastHeader;
    private Rect lastField;
    private Rect contentClip;

    public MapsApp(MapData maps, Configuration configuration, ZoneMapTextures zoneMapTextures)
    {
        this.maps = maps;
        this.configuration = configuration;
        this.zoneMapTextures = zoneMapTextures;
        reader = new MinimapReader(zoneMapTextures);
        ladder = new ZoneMapLadder(zoneMapTextures, Plugin.DataManager, Plugin.TextureProvider);
    }

    private MapsPage Page => pageDepth == 0 ? MapsPage.Home : pages[pageDepth - 1];

    public void OnOpened()
    {
        search = string.Empty;
        searchQuery = string.Empty;
        pageDepth = 0;
        openPlace = null;
        lifestreamAvailable = LifestreamBridge.IsAvailable();
        drawer.Reset(MapDrawerDetent.Medium);
        camera.Reset();
        stageMapId = 0;
        plotSource = default;
        SyncFavorites();
        favoritesRail.Reset();
        browseRail.Reset();
    }

    public void OnClosed()
    {
        search = string.Empty;
        searchQuery = string.Empty;
        openPlace = null;
        pageDepth = 0;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        var drawList = ImGui.GetWindowDrawList();
        ladder.BeginFrame();
        reader.Update(delta);
        location = maps.CurrentLocation();
        var headerHeight = HeaderHeight(scale);
        var peek = headerHeight + theme.BottomZoneHeight * scale;
        var panel = drawer.Update(screen, peek, lastHeader, lastField, delta);
        var visibleBottom = MathF.Max(panel.Min.Y, screen.Max.Y - drawer.MediumHeight);
        var visible = new Rect(new Vector2(screen.Min.X, context.Content.Min.Y),
            new Vector2(screen.Max.X, MathF.Max(context.Content.Min.Y + 1f, visibleBottom)));
        var stage = new Rect(screen.Min, new Vector2(screen.Max.X, MathF.Max(screen.Min.Y + 1f, panel.Min.Y)));
        UiAnchors.Report("maps.map", visible);
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(theme.AppBackground with { W = 1f }),
            theme.ScreenRounding * scale);
        ResolveStage();
        DrawStage(drawList, screen, stage, visible, context.Content, scale, delta);
        DrawDrawer(drawList, screen, panel, headerHeight, scale);
    }

    private void ResolveStage()
    {
        if (location.Kind == MapLocationKind.House)
        {
            RefreshPlot();
            SetStage(hasPlot ? StageMode.Plot : StageMode.Hero, hasPlot ? plotMapId : 0u);
            return;
        }

        var live = reader.HasMap && reader.HasPlayer && reader.MapRowId != 0 && !LocationShare.IsIndoors() &&
                   zoneMapTextures.ForMap(reader.MapRowId) is not null;
        SetStage(live ? StageMode.Live : StageMode.Hero, live ? reader.MapRowId : 0u);
    }

    private void SetStage(StageMode nextMode, uint mapId)
    {
        mode = nextMode;
        if (mapId == stageMapId)
        {
            return;
        }

        stageMapId = mapId;
        stagePins = maps.AetherytesOnMap(mapId);
        camera.Reset();
    }

    private void RefreshPlot()
    {
        if (plotSource == location)
        {
            return;
        }

        plotSource = location;
        hasPlot = false;
        if (LocationShare.Capture() is not { MapId: not 0 } captured ||
            !maps.TryMapMetrics(captured.MapId, out var sizeFactor))
        {
            return;
        }

        hasPlot = true;
        plotMapId = captured.MapId;
        plotU = (captured.MapX - 1f) * (sizeFactor / 100f) / 41f;
        plotV = (captured.MapY - 1f) * (sizeFactor / 100f) / 41f;
        plotLabel = captured.Plot > 0 ? Loc.T(L.Maps.PlotLine, captured.Plot) : string.Empty;
    }

    private void DrawStage(ImDrawListPtr drawList, Rect screen, Rect stage, Rect visible, Rect content, float scale,
        float delta)
    {
        var controls = MapChrome.ControlsRect(screen, content.Min.Y + ControlsTopInset * scale, scale);
        var controlsShown = mode != StageMode.Hero && controls.Max.Y + ControlsTopInset * scale < stage.Max.Y;
        var overControls = controlsShown && UiInteract.Hover(controls.Min, controls.Max);
        var input = StageInput(stage, overControls);
        if (mode == StageMode.Hero)
        {
            var note = location.IsKnown ? Loc.T(L.Maps.NoMapHere) : string.Empty;
            LocationHero.Draw(drawList, screen, visible, theme, accent, in location, note, scale);
            return;
        }

        var cover = MathF.Max(screen.Width, screen.Height);
        var unit = CanvasUnits * scale;
        var minimum = cover;
        var maximum = MathF.Max(cover, unit * MaximumZoom);
        var preferred = Math.Clamp(unit * DefaultZoom, minimum, maximum);
        var followU = mode == StageMode.Live ? reader.PlayerU : plotU;
        var followV = mode == StageMode.Live ? reader.PlayerV : plotV;
        var tapped = camera.Update(screen, visible, in input, minimum, maximum, preferred, true, followU, followV,
            delta);
        if (ladder.Get(stageMapId, camera.Size) is not { } texture)
        {
            return;
        }

        MapCanvas.Map(drawList, screen, texture, camera, theme.ScreenRounding * scale);
        var hoveredPin = DrawPins(drawList, stage, input.Hovered && !camera.Dragging, scale);
        if (mode == StageMode.Live)
        {
            MapCanvas.Player(drawList, camera.ToScreen(reader.PlayerU, reader.PlayerV), reader.Facing, accent, scale);
        }
        else
        {
            DrawPlotPin(drawList, scale);
        }

        if (tapped && hoveredPin is not null)
        {
            UiFeedback.Play(UiSound.Tap);
            OpenPlace(hoveredPin);
        }

        if (!controlsShown)
        {
            return;
        }

        DrawControls(drawList, controls, minimum, scale);
        DrawCoordinateChip(drawList, stage, controls, scale);
    }

    private MapCameraInput StageInput(Rect stage, bool overControls)
    {
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(stage.Min);
        ImGui.InvisibleButton("##mapsStage", stage.Size, ImGuiButtonFlags.MouseButtonLeft);
        var hovered = ImGui.IsItemHovered() && UiInteract.Hover(stage.Min, stage.Max) && !overControls;
        var activated = hovered && ImGui.IsItemActivated();
        var active = ImGui.IsItemActive();
        ImGui.SetCursorScreenPos(cursor);
        var doubleClicked = hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
        return new MapCameraInput(hovered, activated, active, doubleClicked);
    }

    private MapAetheryte? DrawPins(ImDrawListPtr drawList, Rect stage, bool pointerFree, float scale)
    {
        var mouse = ImGui.GetMousePos();
        var hitRadius = MapCanvas.PinHitRadius * scale;
        var bestDistance = hitRadius * hitRadius;
        MapAetheryte? hovered = null;
        var selectedId = Page == MapsPage.Place && openPlace is not null ? openPlace.RowId : 0u;
        for (var index = 0; index < stagePins.Count; index++)
        {
            var pin = stagePins[index];
            var center = camera.ToScreen(pin.U, pin.V);
            if (!stage.Contains(center))
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(center, mouse);
            if (pointerFree && distance <= bestDistance)
            {
                bestDistance = distance;
                hovered = pin;
            }
        }

        var baseKey = ImGui.GetID("maps.pin");
        for (var index = 0; index < stagePins.Count; index++)
        {
            var pin = stagePins[index];
            var center = camera.ToScreen(pin.U, pin.V);
            if (!stage.Contains(center))
            {
                continue;
            }

            var grow = PressFx.Toward(unchecked(baseKey + pin.RowId), ReferenceEquals(pin, hovered) ? 1.12f : 1f);
            MapCanvas.Pin(drawList, center, scale, grow, pin.RowId == selectedId, accent);
            MapCanvas.PinLabel(center, pin.Name, scale, PinLabelWidth * scale);
        }

        if (hovered is not null)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered;
    }

    private void DrawPlotPin(ImDrawListPtr drawList, float scale)
    {
        var center = camera.ToScreen(plotU, plotV);
        MapCanvas.PlotPin(drawList, center, accent, scale);
        if (plotLabel.Length > 0)
        {
            MapCanvas.PinLabel(center + new Vector2(0f, 4f * scale), plotLabel, scale, PinLabelWidth * scale);
        }
    }

    private void DrawControls(ImDrawListPtr drawList, Rect controls, float overviewSize, float scale)
    {
        var pressed = MapChrome.Controls(drawList, controls, camera.Following, theme, scale,
            Loc.T(L.Maps.CurrentLocation), Loc.T(L.Maps.WholeZone));
        switch (pressed)
        {
            case MapControl.Recenter:
                UiFeedback.Play(UiSound.Tap);
                camera.Recenter();
                break;
            case MapControl.Overview:
                UiFeedback.Play(UiSound.Tap);
                camera.Overview(overviewSize);
                break;
        }
    }

    private void DrawCoordinateChip(ImDrawListPtr drawList, Rect stage, Rect controls, float scale)
    {
        if (mode != StageMode.Live || reader.Coordinates.Length == 0 ||
            stage.Max.Y - controls.Max.Y < ChipClearance * scale)
        {
            return;
        }

        var bottomLeft = new Vector2(stage.Min.X + Metrics.Space.GlassInset * scale,
            stage.Max.Y - Metrics.Space.GlassInset * scale);
        MapChrome.Chip(drawList, bottomLeft, reader.Coordinates, theme, scale);
    }

    private void PushPage(MapsPage page)
    {
        if (pageDepth >= PageCapacity)
        {
            pageDepth = PageCapacity - 1;
        }

        pages[pageDepth] = page;
        pageDepth++;
    }

    private void PopPage()
    {
        if (pageDepth > 0)
        {
            pageDepth--;
        }

        UiFeedback.Play(UiSound.Tap);
        if (Page != MapsPage.Place)
        {
            openPlace = null;
        }
    }

    private void OpenExpansion(MapExpansion expansion)
    {
        UiFeedback.Play(UiSound.Tap);
        openExpansion = expansion.Order;
        PushPage(MapsPage.Expansion);
        if (drawer.Detent == MapDrawerDetent.Peek)
        {
            drawer.SetDetent(MapDrawerDetent.Medium);
        }
    }

    private void OpenPlace(MapAetheryte aetheryte)
    {
        openPlace = aetheryte;
        ReadTeleportInfo(aetheryte);
        if (Page == MapsPage.Place)
        {
            pageDepth--;
        }

        PushPage(MapsPage.Place);
        if (drawer.Detent != MapDrawerDetent.Medium)
        {
            drawer.SetDetent(MapDrawerDetent.Medium);
        }
    }

    private bool IsOnStage(MapAetheryte aetheryte) =>
        mode != StageMode.Hero && aetheryte.HasPosition && aetheryte.MapId == stageMapId;

    private void ShowOnMap(MapAetheryte aetheryte)
    {
        var scale = UiScale.Current;
        camera.FocusOn(aetheryte.U, aetheryte.V, CanvasUnits * scale * FocusZoom);
        drawer.SetDetent(MapDrawerDetent.Peek);
    }

    private void SyncFavorites()
    {
        favorites.Clear();
        var stored = configuration.MapFavorites;
        for (var index = 0; index < stored.Count; index++)
        {
            favorites.Add(stored[index]);
        }

        favoritesDirty = true;
    }

    private void ToggleFavorite(uint rowId)
    {
        if (favorites.Remove(rowId))
        {
            configuration.MapFavorites.Remove(rowId);
            UiFeedback.Play(UiSound.ToggleOff);
        }
        else
        {
            favorites.Add(rowId);
            configuration.MapFavorites.Add(rowId);
            UiFeedback.Play(UiSound.ToggleOn);
        }

        favoritesDirty = true;
        configuration.Save();
    }

    private void RecordRecent(uint rowId)
    {
        var recents = configuration.MapRecents;
        recents.Remove(rowId);
        recents.Insert(0, rowId);
        if (recents.Count > RecentsCapacity)
        {
            recents.RemoveRange(RecentsCapacity, recents.Count - RecentsCapacity);
        }

        recentsDirty = true;
        configuration.Save();
    }

    private void Teleport(MapAetheryte aetheryte)
    {
        var outcome = LifestreamBridge.TeleportToAetheryte(aetheryte.RowId);
        var available = outcome != LifestreamOutcome.NotInstalled;
        if (available != lifestreamAvailable)
        {
            lifestreamAvailable = available;
            ReadTeleportInfo(aetheryte);
        }

        switch (outcome)
        {
            case LifestreamOutcome.Started:
                RecordRecent(aetheryte.RowId);
                UiFeedback.Play(UiSound.Success);
                ShellToast.Show(Loc.T(L.Maps.Teleporting, aetheryte.Name));
                drawer.SetDetent(MapDrawerDetent.Peek);
                return;
            case LifestreamOutcome.NotInstalled:
                RecordRecent(aetheryte.RowId);
                ImGui.SetClipboardText(LifestreamBridge.AetheryteCommand(aetheryte.Name));
                UiFeedback.Play(UiSound.Tap);
                ShellToast.Show();
                return;
            case LifestreamOutcome.Busy:
                UiFeedback.Play(UiSound.Caution);
                ShellToast.Show(Loc.T(L.Travel.Busy));
                return;
            case LifestreamOutcome.NotAttuned:
                UiFeedback.Play(UiSound.Caution);
                ShellToast.Show(Loc.T(L.Travel.NotAttuned, aetheryte.Name));
                return;
            default:
                UiFeedback.Play(UiSound.Caution);
                ShellToast.Show(Loc.T(L.Travel.Blocked));
                return;
        }
    }

    private static void OpenGameMap(uint territoryId, uint mapId, float gameX, float gameY)
    {
        if (territoryId == 0 || mapId == 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        LocationShare.OpenMap(new SharedLocation(territoryId, mapId, gameX, gameY, 0, 0, 0, 0));
    }

    private void ReportVisible(string key, Rect rect)
    {
        if (rect.Max.Y <= contentClip.Min.Y || rect.Min.Y >= contentClip.Max.Y)
        {
            return;
        }

        UiAnchors.Report(key, new Rect(new Vector2(rect.Min.X, MathF.Max(rect.Min.Y, contentClip.Min.Y)),
            new Vector2(rect.Max.X, MathF.Min(rect.Max.Y, contentClip.Max.Y))));
    }

    public void Dispose()
    {
        ladder.Dispose();
    }
}
