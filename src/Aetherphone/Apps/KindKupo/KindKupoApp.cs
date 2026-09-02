using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;


namespace Aetherphone.Apps.KindKupo;

internal sealed partial class KindKupoApp : IPhoneApp
{
    public string Id => "kindkupo";
    public string DisplayName => Loc.T(L.Apps.KindKupo);
    public string Glyph => "KK";
    public int BadgeCount => social.UnseenCount(Id);

    private readonly KindKupoStore store;
    private readonly ViewRouter<KindKupoRoute> router;
    private readonly RouterDraw<KindKupoRoute> drawView;
    private readonly AppSkin ui = new(AppPalettes.KindKupo);
    private readonly AethernetSession session;
    private readonly AethernetApi net;
    private readonly ConductGateService conduct;
    private readonly SocialNotificationService social;
    private readonly ReportService report;
    private PhoneTheme theme = PhoneTheme.Default;
    private string draft = string.Empty;
    private INavigator navigation = null!;
    private readonly Action back;
    public KindKupoApp(AethernetSession session, AethernetApi net, ReportService report,
        ConductGateService conduct, SocialNotificationService social)
    {
        this.session = session;
        this.net = net;
        this.report = report;
        this.conduct = conduct;
        this.social = social;
        store = new KindKupoStore(session, net.Kupo);

        drawView = DrawView;
        router = new ViewRouter<KindKupoRoute>(KindKupoRoute.Home);
        back = () => router.Pop();
    }
    public void Dispose() => store.Dispose();
    public void OnOpened()
    {
        router.Reset();
        draft = string.Empty;
        replyDraft = string.Empty;
        conduct.NotifyAppOpened(Id);
    }

    public void OnClosed()
    {
        router.Reset();
        replyDraft = string.Empty;
        draft = string.Empty;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;

        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);

        if (!session.IsSignedIn)
        {
            TourHolds.Hold(Id);
            ui.Body(context.Content);
            AppHeader.Draw(context, DisplayName, navigation.Back);
            var top = context.Content.Min.Y + AppHeader.Height * scale;
            var body = new Rect(new Vector2(context.Content.Min.X, top), context.Content.Max);
            EmptyState.Draw(body, ui, FontAwesomeIcon.UserLock, Loc.T(L.KindKupo.SignInTitle),
                Loc.T(L.KindKupo.SignInHint));
            return;
        }

        TourHolds.Release(Id);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(KindKupoRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case KindKupoScreen.Write:
                DrawWriteScreen(area);
                break;
            case KindKupoScreen.Inbox:
                DrawInbox(area);
                break;
            case KindKupoScreen.Respond:
                DrawResponseFeed(area);
                break;
            case KindKupoScreen.ResponseList:
            {
                var confession = ResolveConfession(route);
                if (confession is not null)
                {
                    DrawResponseListScreen(area, confession);
                }
                break;
            }
            case KindKupoScreen.ComposeResponse:
            {
                var confession = ResolveConfession(route);
                if (confession is not null)
                {
                    DrawComposeResponse(area, confession);
                }
                break;
            }
            default:
                DrawHome(area);
                break;
        }

    }

    private ConfessionDto? ResolveConfession(KindKupoRoute route) =>
        route.ConfessionId is null ? null : store.FindConfession(route.ConfessionId);

    private void DrawConfessionCard(ConfessionDto confession, KindKupoScreen screen)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var pad = 14f * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var rounding = 16f * scale;
        var cardGap = 12f * scale;

        var contentLeft = origin.X + pad;
        var contentRight = origin.X + width - pad;
        var contentWidth = MathF.Max(1f, contentRight - contentLeft);
        var textTop = origin.Y + pad;

        var textHeight = confession.Text.Length == 0
            ? 0f
            : Typography.MeasureWrapped(confession.Text, contentWidth, 1.05f);

        var textToFooterGap = 10f * scale;
        var footerHeight = 24f * scale;
        var footerCenterY = textTop + textHeight + textToFooterGap + footerHeight * 0.5f;

        var cardHeight = pad + textHeight + textToFooterGap + footerHeight + pad;
        var cardBottom = origin.Y + cardHeight;

        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), rounding);

        if (confession.Text.Length > 0)
        {
            ImGui.SetCursorScreenPos(new Vector2(contentLeft, textTop));
            using (Typography.WrapAt(contentRight))
            using (Plugin.Fonts.Push(1.05f))
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.KindKupo.BodyInk))
            {
                Typography.Wrapped(confession.Text);
            }
        }

        DrawCardFooter(confession, screen, contentLeft, contentWidth, footerCenterY);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + cardGap));
    }

    private void DrawCardFooter(ConfessionDto confession, KindKupoScreen screen, float left, float width,
        float centerY)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var right = left + width;

        var stamp = TimeText.Ago(confession.CreatedAtUnix);
        var stampSize = Typography.Measure(stamp, 0.85f, FontWeight.Regular);
        var stampPos = new Vector2(left, centerY - stampSize.Y * 0.5f);
        Typography.Draw(drawList, stampPos, stamp, AppPalettes.KindKupo.MutedInk, 0.85f, FontWeight.Regular);

        var iconSize = 16f * scale;
        var transparent = Vector4.Zero;

        switch (screen)
        {
            case KindKupoScreen.Respond:
            {
                var respondPos = new Vector2(right - iconSize * 0.8f, centerY);
                if (ui.IconButton(respondPos, iconSize, FontAwesomeIcon.Pen.ToIconString(),
                        AppPalettes.KindKupo.MutedInk, transparent, 1f, Loc.T(L.KindKupo.Respond)))
                {
                    router.Push(KindKupoRoute.ComposeResponse(confession.Id));
                }

                var reportPos = new Vector2(respondPos.X - 24f * scale, centerY);
                if (ui.IconButton(reportPos, iconSize, FontAwesomeIcon.Flag.ToIconString(),
                        AppPalettes.KindKupo.MutedInk, transparent, 0.9f, Loc.T(L.KindKupo.Report)))
                {
                    OpenReportConfession(confession.Id);
                }
                break;
            }

            case KindKupoScreen.Inbox:
            {
                var repliesPos = new Vector2(right - iconSize * 0.8f, centerY);
                if (ui.IconButton(repliesPos, iconSize, FontAwesomeIcon.CommentDots.ToIconString(),
                        AppPalettes.KindKupo.MutedInk, transparent, 1f, Loc.T(L.KindKupo.ViewReplies, confession.ResponseCount)))
                {
                    router.Push(KindKupoRoute.ViewResponse(confession.Id));
                }
                break;
            }
        }
    }

    private void DrawResponseCard(ConfessionResponseDto response)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var pad = 14f * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var rounding = 16f * scale;
        var cardGap = 12f * scale;

        var contentLeft = origin.X + pad;
        var contentRight = origin.X + width - pad;
        var contentWidth = MathF.Max(1f, contentRight - contentLeft);
        var textTop = origin.Y + pad;

        var textHeight = response.Text.Length == 0
            ? 0f
            : Typography.MeasureWrapped(response.Text, contentWidth, 1.05f);
        var textToFooterGap = 10f * scale;
        var footerHeight = 24f * scale;
        var footerCenterY = textTop + textHeight + textToFooterGap + footerHeight * 0.5f;

        var cardHeight = pad + textHeight + textToFooterGap + footerHeight + pad;
        var cardBottom = origin.Y + cardHeight;
        ui.Card(drawList, origin, new Vector2(origin.X + width, cardBottom), rounding);

        if (response.Text.Length > 0)
        {
            ImGui.SetCursorScreenPos(new Vector2(contentLeft, textTop));
            using (Typography.WrapAt(contentRight))
            using (Plugin.Fonts.Push(1.05f))
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.KindKupo.BodyInk))
            {
                Typography.Wrapped(response.Text);
            }
        }
        var stamp = TimeText.Ago(response.CreatedAtUnix);
        var stampSize = Typography.Measure(stamp, 0.85f, FontWeight.Regular);
        var stampPos = new Vector2(contentLeft, footerCenterY - stampSize.Y * 0.5f);
        Typography.Draw(drawList, stampPos, stamp, AppPalettes.KindKupo.MutedInk, 0.85f, FontWeight.Regular);

        var reportIconPos = new Vector2(contentRight - 12f * scale, footerCenterY);
        if (ui.IconButton(reportIconPos, 16f * scale, FontAwesomeIcon.Flag.ToIconString(),
                AppPalettes.KindKupo.MutedInk,
                new Vector4(0f, 0f, 0f, 0f), 0.9f, Loc.T(L.KindKupo.Report)))
        {
            OpenReportResponse(response.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + cardGap));
    }

    private void OpenReportConfession(string confessionId)
    {
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.KindKupo.ReportConfession),
            Submit = (reason, done) =>
            {
                _ = Task.Run(async () =>
                {
                    var ok = false;
                    try
                    {
                        ok = await net.Safety.ReportAsync("kupo_confession", confessionId, reason, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        AepLog.Warning(ex, "[KindKupo] report confession failed");
                    }
                    done(ok);
                });
            },
        });
    }

    private void OpenReportResponse(string responseId)
    {
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.KindKupo.ReportReply),
            Submit = (reason, done) =>
            {
                _ = Task.Run(async () =>
                {
                    var ok = false;
                    try
                    {
                        ok = await net.Safety.ReportAsync("kupo_response", responseId, reason, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        AepLog.Warning(ex, "[KindKupo] report reply failed");
                    }
                    done(ok);
                });
            },
        });
    }


    private void DrawHome(Rect area)
    {
        var scale = UiScale.Current;
        var padding = 16f * scale;
        DrawHomeTopBar(area);

        var contentTop = area.Min.Y + AppHeader.Height * scale;
        var availableHeight = area.Max.Y - contentTop;

        var buttonHeight = 44f * scale;
        var gap = 12f * scale;
        var blockHeight = buttonHeight * 2f + gap;

        var startY = contentTop + MathF.Max(0f, (availableHeight - blockHeight) * 0.5f);
        var minX = area.Min.X + padding;
        var maxX = area.Max.X - padding;

        var writeRect = new Rect(new Vector2(minX, startY), new Vector2(maxX, startY + buttonHeight));
        UiAnchors.Report("kindkupo.write", writeRect);
        if (ui.PillButton(writeRect, Loc.T(L.KindKupo.Write), filled: true))
        {
            router.Push(KindKupoRoute.Write);
        }

        var respondRect = new Rect(new Vector2(minX, writeRect.Max.Y + gap), new Vector2(maxX, writeRect.Max.Y + gap + buttonHeight));
        UiAnchors.Report("kindkupo.respond", respondRect);
        if (ui.PillButton(respondRect, Loc.T(L.KindKupo.Respond), filled: false))
        {
            store.Refresh();
            router.Push(KindKupoRoute.Respond);
        }
    }
}
