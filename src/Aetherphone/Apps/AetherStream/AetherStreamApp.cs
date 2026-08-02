using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.AetherStream;

internal enum AetherStreamScreen : byte
{
    Main,
    Settings,
    Join,
}

internal enum AetherStreamTab : byte
{
    Player,
    Queue,
    Casting,
}

internal sealed partial class AetherStreamApp : IPhoneApp
{
    public string Id => "aetherstream";
    public string DisplayName => Loc.T(L.Apps.AetherStream);
    public string Glyph => "V";
    public Vector4 Accent => AppAccents.For(Id);
    public int BadgeCount => 0;

    private readonly VideoPlayer video;
    private readonly ScreenController screen;
    private readonly AetherStreamQueue queue;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly RemoteImageCache remoteImages;
    private readonly HttpService http;
    private readonly LodestoneService lodestone;
    private readonly WatchAlongSession watchAlong;
    private readonly AetherStreamScreenWindow screenWindow;
    private readonly AccountClient joinAccount;
    private readonly StoreWork joinWork = new("aetherstream.join");
    private readonly StoreWork dependencyWork = new("aetherstream.dependencies");
    private bool mpvDownloading;
    private bool ytdlpDownloading;
    private readonly AppSkin ui = new(AppPalettes.AetherStream);
    private readonly ViewRouter<AetherStreamScreen> router = new(AetherStreamScreen.Main);
    private readonly string[] tabOptions = new string[3];
    private AetherStreamTab activeTab;
    private PhoneTheme theme = PhoneTheme.Default;

    public AetherStreamApp(VideoPlayer video, ScreenController screen, AetherStreamQueue queue,
        Configuration configuration, ConfirmService confirm, RemoteImageCache remoteImages, HttpService http,
        AethernetSession aethernetSession, LodestoneService lodestone, WatchAlongSession watchAlong,
        AetherStreamScreenWindow screenWindow)
    {
        this.video = video;
        this.screen = screen;
        this.queue = queue;
        this.configuration = configuration;
        this.confirm = confirm;
        this.remoteImages = remoteImages;
        this.http = http;
        this.lodestone = lodestone;
        this.watchAlong = watchAlong;
        this.screenWindow = screenWindow;
        joinAccount = new AethernetApi(http, aethernetSession, "aetherstream").Account;
        video.SetVolume((int)(configuration.VideoVolume * 100));
        video.HardwareDecoding = configuration.VideoHardwareDecoding;
        video.AllowInsecureDirectUrls = configuration.VideoAllowInsecureDirectUrls;
        video.MaxQualityHeight = configuration.VideoMaxQualityHeight;
    }

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        ui.Theme = context.Theme;
        var scale = ImGuiHelpers.GlobalScale;
        var screenRect = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screenRect);
        var localContext = context;
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, (screenState, area, _) =>
        {
            switch (screenState)
            {
                case AetherStreamScreen.Settings:
                    DrawSettings(localContext, area, scale);
                    return;
                case AetherStreamScreen.Join:
                    DrawJoinScreen(localContext, area, scale);
                    return;
                default:
                    DrawMain(localContext, area, scale);
                    return;
            }
        });
    }

    private void DrawMain(PhoneContext context, Rect area, float scale)
    {
        DrawMainHeader(context, area, scale);

        var tabTop = area.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var tabMargin = Metrics.Space.Lg * scale;
        var tabRow = new Rect(new Vector2(area.Min.X + tabMargin, tabTop),
            new Vector2(area.Max.X - tabMargin, tabTop + 30f * scale));
        tabOptions[0] = Loc.T(L.AetherStream.TabPlayer);
        tabOptions[1] = Loc.T(L.AetherStream.TabQueue);
        tabOptions[2] = Loc.T(L.AetherStream.TabCasting);
        var selected = SegmentStrip.Draw("aetherstream.tabs", tabRow, tabOptions, (int)activeTab, theme);
        activeTab = (AetherStreamTab)selected;

        var body = new Rect(new Vector2(area.Min.X, tabRow.Max.Y + 10f * scale), area.Max);
        switch (activeTab)
        {
            case AetherStreamTab.Queue:
                DrawQueueTab(body, scale);
                break;
            case AetherStreamTab.Casting:
                DrawCastingTab(body, scale);
                break;
            default:
                DrawPlayerTab(body, scale);
                break;
        }
    }

    private void DrawMainHeader(PhoneContext context, Rect area, float scale)
    {
        AppHeader.Draw(context, DisplayName);
        var radius = 13f * scale;
        var center = new Vector2(area.Max.X - Metrics.Space.Lg * scale - radius,
            area.Min.Y + AppHeader.Height * scale * 0.5f);
        if (ui.IconButton(center, radius, FontAwesomeIcon.Cog.ToIconString(), ui.TitleInk,
                Palette.WithAlpha(ui.TitleInk, 0.12f), 0.55f))
        {
            router.Push(AetherStreamScreen.Settings);
        }
    }

    public void Dispose()
    {
        video.Stop();
        joinWork.Dispose();
        dependencyWork.Dispose();
    }
}
