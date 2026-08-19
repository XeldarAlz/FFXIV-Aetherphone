using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.VenueSync;

internal sealed partial class VenueSyncApp : IPhoneApp
{
    public string Id => "venue-sync";
    public string DisplayName => Loc.T(L.Apps.VenueSync);
    public string Glyph => "VM";
    public int BadgeCount => 0;

    private readonly VenueSyncApiClient client;
    private readonly VenueSyncState state;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly ViewRouter<VenueSyncRoute> router;
    private readonly RouterDraw<VenueSyncRoute> drawView;
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    private readonly AppSkin ui = new(AppPalettes.VenueSync(PhoneTheme.Default));

    public VenueSyncApp(VenueSyncApiClient client, VenueSyncState state, Configuration configuration,
        GameData gameData)
    {
        this.client = client;
        this.state = state;
        this.configuration = configuration;
        this.gameData = gameData;
        router = new ViewRouter<VenueSyncRoute>(VenueSyncRoute.Dashboard);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        ResetTransientState();
        state.EnsureShiftsFresh(false);
    }

    public void OnClosed()
    {
        router.Reset();
        ResetTransientState();
    }

    private void ResetTransientState()
    {
        shiftsActionError = null;
        shiftsActionNotice = null;
        salesError = null;
        settingsError = null;
        settingsCharacterLinkStatus = null;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        ui.Palette = AppPalettes.VenueSync(theme);
        var screen = SceneChrome.ScreenFrom(context.Content, theme, UiScale.Current);
        ui.Backdrop(screen);
        state.EnsureShiftsFresh(false);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(VenueSyncRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route)
        {
            case VenueSyncRoute.Dashboard:
                DrawDashboard(area);
                break;
            case VenueSyncRoute.Shifts:
                DrawShifts(area);
                break;
            case VenueSyncRoute.Sales:
                DrawSales(area);
                break;
            case VenueSyncRoute.Settings:
                DrawSettings(area);
                break;
            default:
                break;
        }
    }

    public void Dispose()
    {
    }
}
