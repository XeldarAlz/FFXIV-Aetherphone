using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp : IPhoneApp
{
    private const float CopiedSeconds = 1.6f;

    public string Id => "muster";
    public string DisplayName => Loc.T(L.Apps.Muster);
    public string Glyph => "Mu";
    public int BadgeCount => 0;

    private readonly MusterStore store;
    private readonly MusterLauncher launcher;
    private readonly AethernetApi api;
    private readonly GameData gameData;
    private readonly RemoteImageCache images;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly ReportService report;
    private readonly AppSkin ui = new(AppPalettes.Muster);
    private readonly ViewRouter<MusterRoute> router;
    private readonly RouterDraw<MusterRoute> drawView;
    private readonly Action back;
    private readonly Action decrementMaxAttendees;
    private readonly Action incrementMaxAttendees;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private float copiedTimer;
    private string copiedKey = string.Empty;

    public MusterApp(MusterStore store, MusterLauncher launcher, AethernetApi api, GameData gameData,
        RemoteImageCache images, Configuration configuration, ConfirmService confirm, ReportService report)
    {
        this.store = store;
        this.launcher = launcher;
        this.api = api;
        this.gameData = gameData;
        this.images = images;
        this.configuration = configuration;
        this.confirm = confirm;
        this.report = report;
        router = new ViewRouter<MusterRoute>(MusterRoute.Directory);
        drawView = DrawView;
        back = () => router.Pop();
        decrementMaxAttendees = () => SetMaxAttendees(createMaxAttendees - 1);
        incrementMaxAttendees = () => SetMaxAttendees(createMaxAttendees + 1);
    }

    public void OnOpened()
    {
        router.Reset();
        if (launcher.TryConsumeDetail(out var musterId))
        {
            ResetDetailState();
            router.Push(MusterRoute.Detail(musterId), false);
        }

        store.SyncNow();
        store.RefreshDirectory();
    }

    public void OnClosed()
    {
        router.Reset();
        ResetDetailState();
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
                AppPalettes.Muster.TitleInk, 1.3f, FontWeight.Bold);
            var body = new Rect(new Vector2(context.Content.Min.X, context.Content.Min.Y + AppHeader.Height * scale),
                context.Content.Max);
            Typography.DrawCentered(body.Center, Loc.T(L.Muster.SetUpAccount), AppPalettes.Muster.MutedInk);
            return;
        }

        if (copiedTimer > 0f)
        {
            copiedTimer -= ImGui.GetIO().DeltaTime;
        }

        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(MusterRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case MusterScreen.Detail:
                DrawDetail(area, route.MusterId!);
                break;
            case MusterScreen.Create:
                DrawCreate(area);
                break;
            case MusterScreen.Manage:
                DrawManage(area);
                break;
            default:
                DrawDirectory(area);
                break;
        }
    }

    private void OpenDetail(string musterId)
    {
        ResetDetailState();
        router.Push(MusterRoute.Detail(musterId));
    }

    private void Copy(string key, string text)
    {
        ImGui.SetClipboardText(text);
        copiedKey = key;
        copiedTimer = CopiedSeconds;
    }

    private bool JustCopied(string key) =>
        copiedTimer > 0f && string.Equals(copiedKey, key, StringComparison.Ordinal);

    private void SubmitReport(string musterId, string? reason, Action<bool> done)
    {
        _ = Task.Run(async () =>
        {
            var ok = false;
            try
            {
                ok = await api.Safety.ReportAsync("muster", musterId, reason, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Muster] report failed: {exception.Message}");
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
