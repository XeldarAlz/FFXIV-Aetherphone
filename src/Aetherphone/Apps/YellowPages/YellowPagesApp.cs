using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp : IPhoneApp
{
    private const float CopiedSeconds = 1.6f;

    public string Id => "yellowpages";
    public string DisplayName => Loc.T(L.Apps.YellowPages);
    public string Glyph => "Yp";
    public int BadgeCount => 0;

    private readonly YellowPagesStore store;
    private readonly YellowPagesLauncher launcher;
    private readonly AethernetApi api;
    private readonly GameData gameData;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly ReportService report;
    private readonly AppSkin ui = new(AppPalettes.YellowPages);
    private readonly ViewRouter<YellowPagesRoute> router;
    private readonly RouterDraw<YellowPagesRoute> drawView;
    private readonly Action back;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private float copiedTimer;
    private string copiedKey = string.Empty;
    private bool lifestreamAvailable;

    public YellowPagesApp(YellowPagesStore store, YellowPagesLauncher launcher, AethernetApi api, GameData gameData,
        RemoteImageCache images, LodestoneService lodestone, Configuration configuration, ConfirmService confirm,
        ReportService report)
    {
        this.store = store;
        this.launcher = launcher;
        this.api = api;
        this.gameData = gameData;
        this.images = images;
        this.lodestone = lodestone;
        this.configuration = configuration;
        this.confirm = confirm;
        this.report = report;
        router = new ViewRouter<YellowPagesRoute>(YellowPagesRoute.Browse);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        lifestreamAvailable = LifestreamBridge.IsAvailable();
        if (launcher.TryConsumeDetail(out var adId))
        {
            ResetDetailState();
            router.Push(YellowPagesRoute.Detail(adId), false);
        }

        store.SyncNow();
        RefreshBrowse();
    }

    public void OnClosed()
    {
        router.Reset();
        ResetDetailState();
        ResetComposeForm();
        copiedTimer = 0f;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);
        if (!store.IsSignedIn)
        {
            var rowCenterY = context.Content.Min.Y + AppHeader.Height * scale * 0.5f;
            Typography.DrawCentered(new Vector2(context.Content.Center.X, rowCenterY), DisplayName,
                AppPalettes.YellowPages.TitleInk, 1.3f, FontWeight.Bold);
            var body = new Rect(new Vector2(context.Content.Min.X, context.Content.Min.Y + AppHeader.Height * scale),
                context.Content.Max);
            Typography.DrawCentered(body.Center, Loc.T(L.YellowPages.SetUpAccount),
                AppPalettes.YellowPages.MutedInk);
            return;
        }

        if (copiedTimer > 0f)
        {
            copiedTimer -= ImGui.GetIO().DeltaTime;
        }

        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(YellowPagesRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case YellowPagesScreen.Detail:
                DrawDetail(area, route.AdId!);
                break;
            case YellowPagesScreen.Compose:
                DrawCompose(area);
                break;
            case YellowPagesScreen.Mine:
                DrawMine(area);
                break;
            case YellowPagesScreen.Saved:
                DrawSaved(area);
                break;
            default:
                DrawBrowse(area);
                break;
        }
    }

    private void RefreshBrowse()
    {
        store.RefreshDirectory(configuration.YellowPagesCategoryFilter, browseOpenNow, browseSearch);
    }

    private void OpenDetail(string adId)
    {
        ResetDetailState();
        router.Push(YellowPagesRoute.Detail(adId));
    }

    private void Copy(string key, string text)
    {
        ImGui.SetClipboardText(text);
        copiedKey = key;
        copiedTimer = CopiedSeconds;
    }

    private bool JustCopied(string key) =>
        copiedTimer > 0f && string.Equals(copiedKey, key, StringComparison.Ordinal);

    private void SubmitReport(string adId, string? reason, Action<bool> done)
    {
        _ = Task.Run(async () =>
        {
            var ok = false;
            try
            {
                ok = await api.Safety.ReportAsync("ad", adId, reason, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[YellowPages] report failed: {exception.Message}");
            }

            done(ok);
        });
    }

    private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static int TrimmedLength(string value)
    {
        var start = 0;
        var end = value.Length - 1;
        while (start <= end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        return end - start + 1;
    }

    public void Dispose()
    {
    }
}
