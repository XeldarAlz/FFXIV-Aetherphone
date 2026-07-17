using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Clock;

internal sealed partial class ClockApp : IPhoneApp
{
    private enum ClockScreen : byte
    {
        Root,
        EditAlarm,
        AddCity,
    }

    private const int TabWorld = 0;
    private const int TabAlarms = 1;
    private const int TabStopwatch = 2;
    private const int TabTimer = 3;

    public string Id => "clock";
    public string DisplayName => Loc.T(L.Apps.Clock);
    public string Glyph => "T";
    public Vector4 Accent => AppAccents.For("clock");
    public int BadgeCount => 0;

    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.Clock);
    private readonly ViewRouter<ClockScreen> router;
    private readonly RouterDraw<ClockScreen> drawView;
    private readonly Action back;
    private readonly string[] tabOptions = new string[4];
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private int activeTab;

    public ClockApp(Configuration configuration, ConfirmService confirm)
    {
        this.configuration = configuration;
        this.confirm = confirm;
        router = new ViewRouter<ClockScreen>(ClockScreen.Root, Id);
        drawView = DrawView;
        back = () => router.Pop();
        swLaps = new List<double>();
    }

    public void OnOpened()
    {
        router.Reset();
    }

    public void OnClosed()
    {
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = context.Theme;
        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(ClockScreen screen, Rect area, int depth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ui.Body(area);
        switch (screen)
        {
            case ClockScreen.EditAlarm:
                DrawAlarmEditor(area, scale);
                return;
            case ClockScreen.AddCity:
                DrawCityPicker(area, scale);
                return;
            default:
                DrawRoot(area, scale);
                return;
        }
    }

    private void DrawRoot(Rect content, float scale)
    {
        if (GuideIntents.Consume("clock.tab.alarms"))
        {
            activeTab = TabAlarms;
        }

        var context = new PhoneContext(content, theme, navigation);
        AppHeader.Draw(context, DisplayName);
        DrawRootAction(content, scale);

        var segMargin = Metrics.Space.Lg * scale;
        var segTop = content.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var segRow = new Rect(new Vector2(content.Min.X + segMargin, segTop),
            new Vector2(content.Max.X - segMargin, segTop + 30f * scale));
        UiAnchors.Report("clock.tabs", segRow);
        tabOptions[TabWorld] = Loc.T(L.Clock.TabWorld);
        tabOptions[TabAlarms] = Loc.T(L.Clock.TabAlarms);
        tabOptions[TabStopwatch] = Loc.T(L.Clock.TabStopwatch);
        tabOptions[TabTimer] = Loc.T(L.Clock.TabTimer);
        activeTab = SegmentStrip.Draw("clock.tabs", segRow, tabOptions, activeTab, theme);

        var body = new Rect(new Vector2(content.Min.X, segRow.Max.Y + 10f * scale), content.Max);
        switch (activeTab)
        {
            case TabAlarms:
                DrawAlarms(body, scale);
                return;
            case TabStopwatch:
                DrawStopwatch(body, scale);
                return;
            case TabTimer:
                DrawTimer(body, scale);
                return;
            default:
                DrawWorld(body, scale);
                return;
        }
    }

    private void DrawRootAction(Rect content, float scale)
    {
        if (activeTab != TabWorld && activeTab != TabAlarms)
        {
            return;
        }

        var radius = 15f * scale;
        var center = new Vector2(content.Max.X - Metrics.Space.Lg * scale - radius,
            content.Min.Y + AppHeader.Height * scale * 0.5f);
        UiAnchors.Report("clock.add",
            new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius)));
        var tooltip = activeTab == TabWorld ? Loc.T(L.Clock.AddCity) : Loc.T(L.Clock.NewAlarm);
        if (ui.IconButton(center, radius, FontAwesomeIcon.Plus.ToIconString(), ui.TitleInk,
                Palette.WithAlpha(ui.TitleInk, 0.12f), 0.6f, tooltip))
        {
            if (activeTab == TabWorld)
            {
                router.Push(ClockScreen.AddCity);
            }
            else
            {
                StartNewAlarm();
            }
        }
    }

    private bool DrawPillButton(Rect rect, string label, Vector4 fill, Vector4 ink)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var shown = hovered ? Palette.Mix(fill, new Vector4(1f, 1f, 1f, 1f), 0.12f) : fill;
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(shown));
        Typography.DrawCentered(drawList, rect.Center, label, ink, TextStyles.Headline.Scale, TextStyles.Headline.Weight);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public void Dispose()
    {
    }
}
