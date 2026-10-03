using Aetherphone.Apps.Skywatcher.Sky;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Skywatcher;

internal enum SkywatcherTab : byte
{
    Forecast,
    Zones,
    Control,
}

internal sealed partial class SkywatcherApp : IPhoneApp
{
    private const int WindowCount = 10;
    private const float RefreshIntervalSeconds = 5f;
    private const float MaxFrameSeconds = 0.1f;
    private const float ChevronUnits = 40f;
    private const float SidePaddingUnits = 14f;
    private const float SectionGapUnits = 12f;
    public string Id => "skywatcher";
    public string DisplayName => Loc.T(L.Apps.Skywatcher);
    public string Glyph => "W";
    public int BadgeCount => 0;
    private readonly WeatherService weather;
    private readonly WeatherControl control;
    private readonly Configuration configuration;
    private readonly AppSkin ui = new(AppPalettes.Neutral(AppAccents.For("skywatcher")));
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[3];
    private readonly SkyTransition sky = new();
    private readonly List<WeatherWindow> forecast = new();
    private string zone = string.Empty;
    private uint viewedTerritory;
    private float sinceRefresh;
    private SkywatcherTab activeTab;
    private bool scrubbing;
    private bool resetScroll;
    private float scrollY;

    public SkywatcherApp(WeatherService weather, WeatherControl control, Configuration configuration)
    {
        this.weather = weather;
        this.control = control;
        this.configuration = configuration;
    }

    private bool ViewingCurrentZone => viewedTerritory == 0 || viewedTerritory == weather.CurrentTerritory;

    public void OnOpened()
    {
        activeTab = SkywatcherTab.Forecast;
        viewedTerritory = 0;
        scrubbing = false;
        editingZones = false;
        zoneQuery = string.Empty;
        resetScroll = true;
        Refresh();
        sky.Snap();
    }

    public void OnClosed()
    {
    }

    private void Refresh()
    {
        if (ViewingCurrentZone)
        {
            viewedTerritory = 0;
            zone = weather.CurrentZone();
            weather.Forecast(forecast, WindowCount);
        }
        else
        {
            zone = weather.ZoneName(viewedTerritory);
            weather.Forecast(viewedTerritory, forecast, WindowCount);
        }

        RefreshForecastText();
        RefreshDetails();
        RefreshZoneCards();
        sinceRefresh = 0f;
    }

    private void View(uint territoryId)
    {
        viewedTerritory = territoryId;
        activeTab = SkywatcherTab.Forecast;
        resetScroll = true;
        Refresh();
    }

    public void Draw(in PhoneContext context)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        sinceRefresh += delta;
        if (sinceRefresh >= RefreshIntervalSeconds)
        {
            Refresh();
        }

        var scale = UiScale.Current;
        var theme = context.Theme;
        var content = context.Content;
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        var rounding = theme.ScreenRounding * scale;
        var bell = EorzeaTime.Now();
        var daylight = WeatherSky.Daylight(bell.Hour + bell.Minute / 60f);
        var hasData = forecast.Count > 0;
        if (hasData)
        {
            TourHolds.Release(Id);
        }
        else
        {
            TourHolds.Hold(Id);
        }

        var kind = hasData ? WeatherSky.Classify(forecast[0].Weather.EnglishKey) : WeatherKind.Clouds;
        sky.Step(kind, hasData ? daylight : 0f, delta);
        var palette = sky.Palette;
        var drawList = ImGui.GetWindowDrawList();
        WeatherSky.Paint(drawList, screen, rounding, palette);
        sky.Draw(drawList, screen, rounding, hasData ? daylight : 0f, scale);
        var glassBase = WeatherCard.GlassBase(palette);
        ui.Palette = ui.Palette with { BackdropTop = glassBase, BackdropBottom = glassBase, Accent = palette.Ink };
        WallpaperBackdrop.RecordFlat(glassBase);
        AppSurface.ScrollbarInk = palette.Ink;
        SceneChrome.BackChevron(content, context.Navigation, palette.Ink, scale);
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + ChevronUnits * scale), content.Max);
        var showHero = activeTab == SkywatcherTab.Forecast && hasData;
        DrawBody(body, screen, palette, daylight, hasData, showHero, scale);
        if (showHero)
        {
            DrawHero(body, palette, scale);
        }

        DrawTabBar(content, scale);
    }

    private void DrawBody(Rect body, Rect screen, in SkyPalette palette, float daylight, bool hasData, bool showHero,
        float scale)
    {
        var skyKey = ImGui.GetID("##sky");
        ImGui.SetCursorScreenPos(body.Min);
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding,
            new Vector2(SidePaddingUnits * scale, 0f));
        using var child = ImRaii.Child("##sky", body.Size, false,
            DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground));
        if (!child)
        {
            return;
        }

        AppSurface.ResetScrollOnNewVisit();
        if (resetScroll)
        {
            ImGui.SetScrollY(0f);
            resetScroll = false;
        }

        var surface = DragScrollHost.Begin(skyKey);
        scrollY = ImGui.GetScrollY();
        var clipTop = showHero ? body.Min.Y + HeroVisibleHeight(scale) : body.Min.Y;
        ImGui.PushClipRect(new Vector2(body.Min.X, clipTop), body.Max, true);
        DrawTab(screen, palette, daylight, hasData, showHero, scale);
        ImGui.PopClipRect();
        ImGui.Dummy(new Vector2(0f, TabBar.ContentInset(scale) + SectionGapUnits * scale));
        if (scrubbing)
        {
            surface.CancelDrag();
        }
    }

    private void DrawTab(Rect screen, in SkyPalette palette, float daylight, bool hasData, bool showHero, float scale)
    {
        switch (activeTab)
        {
            case SkywatcherTab.Control:
                DrawControl(palette, scale);
                return;
            case SkywatcherTab.Zones:
                DrawZones(palette, daylight, scale);
                return;
        }

        if (!hasData)
        {
            DrawEmpty(screen, palette, scale);
            return;
        }

        if (showHero)
        {
            ImGui.Dummy(new Vector2(0f, HeroExpandedUnits * scale));
        }

        DrawForecast(palette, scale);
    }

    private void DrawTabBar(Rect content, float scale)
    {
        tabItems[0] = new TabItem(Loc.T(L.Skywatcher.Forecast), IconGlyph.Of(FontAwesomeIcon.CloudSun));
        tabItems[1] = new TabItem(Loc.T(L.Skywatcher.Zones), IconGlyph.Of(FontAwesomeIcon.List),
            AnchorKey: "skywatcher.tab.zones");
        tabItems[2] = new TabItem(Loc.T(L.Skywatcher.Control), IconGlyph.Of(FontAwesomeIcon.SlidersH),
            AnchorKey: "skywatcher.tab.control");
        var result = tabBar.Draw(content, ui, tabItems, (int)activeTab);
        if (result.Tapped < 0 || result.Tapped == (int)activeTab)
        {
            return;
        }

        activeTab = (SkywatcherTab)result.Tapped;
        scrubbing = false;
        resetScroll = true;
        if (activeTab == SkywatcherTab.Zones)
        {
            RefreshZoneCards();
        }
    }

    private static void DrawEmpty(Rect screen, in SkyPalette palette, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + ImGui.GetContentRegionAvail().Y * 0.4f);
        WeatherGlyph.Draw(WeatherKind.Clouds, center - new Vector2(0f, 28f * scale), 46f * scale, palette, false,
            palette.Horizon);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 48f * scale), Loc.T(L.Skywatcher.NoData),
            palette.InkSoft, TextStyles.Subheadline);
    }

    private static void SectionLabel(string title, in SkyPalette palette, float scale)
    {
        ImGui.Dummy(new Vector2(0f, SectionGapUnits * scale));
        var text = Loc.Upper(title);
        var style = TextStyles.FootnoteEmphasized;
        var origin = ImGui.GetCursorScreenPos() + new Vector2(4f * scale, 0f);
        var drawList = ImGui.GetWindowDrawList();
        ShadowText(drawList, origin, text, palette.InkSoft, style, palette, scale);
        var size = Typography.Measure(text, style);
        ImGui.Dummy(new Vector2(size.X + 8f * scale, size.Y + 6f * scale));
    }

    private static void ShadowText(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color,
        in TextStyle style, in SkyPalette palette, float scale)
    {
        var shadow = new Vector4(0f, 0f, 0f, palette.LightSky ? 0.14f : 0.30f);
        Typography.Draw(drawList, position + new Vector2(0f, 1f * scale), text, shadow, style);
        Typography.Draw(drawList, position, text, color, style);
    }

    private static void ShadowCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        in TextStyle style, in SkyPalette palette, float scale)
    {
        var shadow = new Vector4(0f, 0f, 0f, palette.LightSky ? 0.18f : 0.36f);
        Typography.DrawCentered(drawList, center + new Vector2(0f, 1.2f * scale), text, shadow, style.Scale,
            style.Weight);
        Typography.DrawCentered(drawList, center, text, color, style.Scale, style.Weight);
    }

    private static bool IsDayWindow(WeatherWindow window)
    {
        float bell;
        if (window.IsCurrent)
        {
            var now = EorzeaTime.Now();
            bell = now.Hour + now.Minute / 60f;
        }
        else
        {
            bell = window.StartBell;
        }

        return WeatherSky.Daylight(bell) >= 0.5f;
    }

    public void Dispose()
    {
    }
}
