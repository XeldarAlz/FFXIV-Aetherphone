using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Widgets;

internal sealed class DailySpinWidget : IHomeWidget
{
    private const string AppKey = "casino";
    private const int FreshMilliseconds = 5000;
    private const float ButtonUnits = 30f;
    private const float BarUnits = 5f;
    private const long DaySeconds = 86_400;
    private const long SampleRemaining = 5 * 3600 + 42 * 60;
    private const long SampleAward = 250;

    private readonly CasinoSpinStore spin;
    private readonly AethernetSession session;
    private WidgetRefresh fresh;
    private CachedText countdownText;
    private CachedText sampleCountdownText;
    private CachedText awardText;
    private long expiredRefreshFor;

    public DailySpinWidget(CasinoSpinStore spin, AethernetSession session)
    {
        this.spin = spin;
        this.session = session;
    }

    public string Id => "casino.spin";
    public string DisplayName => Loc.T(L.WidgetsUtility.DailySpinName);
    public string Description => Loc.T(L.WidgetsUtility.DailySpinDescription);
    public string AppId => AppKey;
    public WidgetSizeSet Sizes => WidgetSizeSet.Small;

    public float Relevance(string config) =>
        session.IsSignedIn && DailySpinStatus.Of(spin.Answer) == DailySpinClaim.Available ? 0.8f : 0f;

    public void Draw(in WidgetContext context)
    {
        var signedIn = session.IsSignedIn;
        if (signedIn && !context.Preview)
        {
            Freshen();
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var accent = AppAccents.For(AppKey);
        var content = WidgetMetrics.Content(context);
        var top = WidgetChrome.Header(context, ink, AppKey, L.WidgetsUtility.DailySpinName, accent);
        var body = new Rect(new Vector2(content.Min.X, top + WidgetMetrics.Gutter * context.Scale), content.Max);
        var answer = spin.Answer;
        var claim = DailySpinStatus.Of(answer);
        if (context.Preview && (!signedIn || claim == DailySpinClaim.Unknown))
        {
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            DrawClaimed(context, ink, body, accent, nowUnix + SampleRemaining, nowUnix, SampleAward,
                ref sampleCountdownText);
            return;
        }

        if (!signedIn)
        {
            WidgetChrome.Message(context, ink, body, FontAwesomeIcon.Gift, accent,
                Loc.T(L.WidgetsUtility.CasinoSignIn), string.Empty);
            return;
        }

        switch (claim)
        {
            case DailySpinClaim.Available:
                DrawAvailable(context, ink, body, accent);
                return;
            case DailySpinClaim.Claimed:
                var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                RefreshWhenExpired(answer!.NextSpinAtUnix, nowUnix);
                DrawClaimed(context, ink, body, accent, answer.NextSpinAtUnix, nowUnix,
                    DailySpinStatus.AwardOf(answer), ref countdownText);
                return;
            case DailySpinClaim.Denied:
                WidgetChrome.Message(context, ink, body, FontAwesomeIcon.Gift, accent,
                    Loc.T(L.WidgetsUtility.SpinUnavailable), string.Empty);
                return;
            default:
                var bar = WidgetType.Headline.Scale * 12f * context.Scale;
                WidgetChrome.Redacted(context.DrawList,
                    new Rect(body.Min, new Vector2(body.Min.X + body.Width * 0.7f, body.Min.Y + bar)), ink);
                WidgetChrome.Redacted(context.DrawList,
                    new Rect(new Vector2(body.Min.X, body.Max.Y - bar), new Vector2(body.Max.X, body.Max.Y)), ink);
                return;
        }
    }

    private void DrawAvailable(in WidgetContext context, in WidgetInk ink, Rect body, Vector4 accent)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var titleHeight = WidgetText.SpacedLineHeight(WidgetType.Title);
        var lines = WidgetText.Clamp(Loc.T(L.WidgetsUtility.SpinReady), WidgetType.Title, body.Width, 2);
        WidgetText.Lines(drawList, lines, body.Min, ink.Primary, WidgetType.Title, titleHeight);
        var buttonHeight = ButtonUnits * scale;
        var button = new Rect(new Vector2(body.Min.X, body.Max.Y - buttonHeight), body.Max);
        var claiming = spin.Claiming;
        var label = Loc.T(claiming ? L.WidgetsUtility.Spinning : L.WidgetsUtility.Spin);
        var fired = WidgetControls.Button(context, ink, 0, button, claiming ? FontAwesomeIcon.Sync : FontAwesomeIcon.Gift,
            label, claiming ? default : accent);
        if (fired && !claiming)
        {
            spin.Claim();
        }
    }

    private void DrawClaimed(in WidgetContext context, in WidgetInk ink, Rect body, Vector4 accent, long nextAtUnix,
        long nowUnix, long award, ref CachedText countdown)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var remaining = Math.Max(0, nextAtUnix - nowUnix);
        var caption = WidgetText.Upper(L.WidgetsUtility.NextSpin);
        WidgetText.Tracked(drawList, body.Min, caption, ink.Secondary, WidgetType.Eyebrow,
            WidgetType.EyebrowTracking * scale);
        var heroTop = body.Min.Y + WidgetText.EyebrowHeight() + WidgetMetrics.RowGap * scale;
        var hero = WidgetText.Countdown(ref countdown, TimeSpan.FromSeconds(remaining));
        WidgetText.Tabular(drawList, new Vector2(body.Min.X, heroTop), hero, ink.Primary, WidgetType.DisplayCompact);

        var bar = BarUnits * scale;
        var barRect = new Rect(new Vector2(body.Min.X, body.Max.Y - bar), body.Max);
        WidgetChrome.Bar(drawList, barRect, 1f - remaining / (float)DaySeconds, ink.Fill, ink.Accent(accent));

        if (award <= 0)
        {
            return;
        }

        var awardLine = awardText.IsCurrent(award)
            ? awardText.Value
            : awardText.Store(award, Loc.T(L.WidgetsUtility.CoinsWon, NumberText.Group(award)));
        var lineHeight = WidgetText.SpacedLineHeight(WidgetType.Headline);
        WidgetText.Draw(drawList,
            new Vector2(body.Min.X, barRect.Min.Y - WidgetMetrics.Gutter * scale - lineHeight), awardLine,
            ink.Accent(accent), WidgetType.Headline, body.Width);
    }

    private void RefreshWhenExpired(long nextAtUnix, long nowUnix)
    {
        if (nowUnix < nextAtUnix || expiredRefreshFor == nextAtUnix)
        {
            return;
        }

        expiredRefreshFor = nextAtUnix;
        spin.RefreshNow();
    }

    private void Freshen()
    {
        if (!fresh.Due(FreshMilliseconds))
        {
            return;
        }

        spin.EnsureFresh();
    }

    public void Dispose()
    {
    }
}
