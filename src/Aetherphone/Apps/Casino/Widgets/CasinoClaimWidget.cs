using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Widgets;

internal sealed class CasinoClaimWidget : IHomeWidget
{
    private const string AppKey = "casino";
    private const int FreshMilliseconds = 30000;
    private const float ButtonUnits = 30f;
    private const float BarUnits = 5f;
    private const float RingUnits = 50f;
    private const float RingThicknessUnits = 4f;
    private const float ClaimShare = 0.52f;
    private const long PeriodSeconds = Strip.StripHero.TimedPeriodSeconds;
    private const long SampleRemaining = 1 * 3600 + 12 * 60;
    private const long SampleAward = 1_500;

    private static readonly CasinoBonusDto SampleTimed = new(CasinoBonusKinds.Timed, false, SampleAward,
        0, 0, true);

    private readonly CasinoStore casino;
    private readonly AethernetSession session;
    private WidgetRefresh fresh;
    private CachedText countdownText;
    private CachedText sampleCountdownText;
    private CachedText awardText;
    private CachedText levelText;
    private CachedText capText;
    private bool claimPending;
    private long expiredRefreshFor;

    public CasinoClaimWidget(CasinoStore casino, AethernetSession session)
    {
        this.casino = casino;
        this.session = session;
    }

    public string Id => "casino.claim";
    public string DisplayName => Loc.T(L.WidgetsUtility.CasinoClaimName);
    public string Description => Loc.T(L.WidgetsUtility.CasinoClaimDescription);
    public string AppId => AppKey;
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public WidgetRoute Target(in WidgetContext context) => WidgetRoute.App(AppId);

    public float Relevance(string config) => session.IsSignedIn && Ready(casino.BonusFor(CasinoBonusKinds.Timed))
        ? 0.85f
        : 0f;

    public static bool Ready(CasinoBonusDto? timed) => timed is { Ready: true, Eligible: true };

    public static float LevelFraction(CasinoProgressDto? progress)
    {
        if (progress is null)
        {
            return 0f;
        }

        var span = progress.NextLevelXp - progress.LevelStartXp;
        return span <= 0 ? 1f : Math.Clamp((float)(progress.Xp - progress.LevelStartXp) / span, 0f, 1f);
    }

    public void Draw(in WidgetContext context)
    {
        var signedIn = session.IsSignedIn;
        if (signedIn && !context.Preview)
        {
            Maintain();
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var accent = AppAccents.For(AppKey);
        var top = WidgetChrome.Header(context, ink, AppKey, L.WidgetsUtility.CasinoClaimName, accent);
        var body = WidgetMetrics.Below(context, top + WidgetMetrics.Gutter * context.Scale);
        var timed = casino.HasFeature(CasinoFeatures.Bonus) ? casino.BonusFor(CasinoBonusKinds.Timed) : null;
        var sample = context.Preview && (!signedIn || timed is null);
        if (!signedIn && !sample)
        {
            WidgetChrome.Message(context, ink, body, FontAwesomeIcon.Coins, accent,
                Loc.T(L.WidgetsUtility.CasinoSignInClaim), string.Empty);
            return;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var shown = sample ? SampleTimed with { NextAtUnix = nowUnix + SampleRemaining } : timed;
        if (shown is null)
        {
            var bar = WidgetType.Headline.Scale * 12f * context.Scale;
            WidgetChrome.Redacted(context.DrawList,
                new Rect(body.Min, new Vector2(body.Min.X + body.Width * 0.7f, body.Min.Y + bar)), ink);
            WidgetChrome.Redacted(context.DrawList,
                new Rect(new Vector2(body.Min.X, body.Max.Y - bar), new Vector2(body.Max.X, body.Max.Y)), ink);
            return;
        }

        if (!sample)
        {
            RefreshWhenExpired(shown, nowUnix);
        }

        if (context.Size == WidgetSize.Small)
        {
            DrawClaim(context, ink, body, accent, shown, nowUnix, sample);
            return;
        }

        var gutter = WidgetMetrics.Gutter * context.Scale;
        var split = body.Min.X + body.Width * ClaimShare;
        DrawClaim(context, ink, new Rect(body.Min, new Vector2(split - gutter * 0.5f, body.Max.Y)), accent, shown,
            nowUnix, sample);
        DrawLevel(context, ink, new Rect(new Vector2(split + gutter * 0.5f, body.Min.Y), body.Max), accent, sample);
    }

    private void DrawClaim(in WidgetContext context, in WidgetInk ink, Rect area, Vector4 accent,
        CasinoBonusDto timed, long nowUnix, bool sample)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        if (Ready(timed) && !sample)
        {
            var buttonHeight = ButtonUnits * scale;
            var button = new Rect(new Vector2(area.Min.X, area.Max.Y - buttonHeight), area.Max);
            var lines = WidgetText.Clamp(Loc.T(L.WidgetsUtility.ClaimReady), WidgetType.Title, area.Width, 2);
            WidgetText.Lines(drawList, lines, area.Min, ink.Primary, WidgetType.Title,
                WidgetText.SpacedLineHeight(WidgetType.Title));
            var claiming = casino.ClaimingBonus.Length > 0;
            var label = claiming
                ? Loc.T(L.WidgetsUtility.Claiming)
                : Loc.T(L.WidgetsUtility.ClaimAmount, NumberText.Compact(timed.Amount));
            var fired = WidgetControls.Button(context, ink, 0, button, claiming ? FontAwesomeIcon.Sync : FontAwesomeIcon.Coins,
                label, claiming ? default : accent);
            if (fired && !claiming)
            {
                claimPending = true;
                casino.ClaimBonus(CasinoBonusKinds.Timed);
            }

            return;
        }

        var remaining = CashierBonusShelf.SecondsUntil(timed, nowUnix);
        WidgetText.Tracked(drawList, area.Min, WidgetText.Upper(L.WidgetsUtility.NextClaim), ink.Secondary,
            WidgetType.Eyebrow, WidgetType.EyebrowTracking * scale);
        var heroTop = area.Min.Y + WidgetText.EyebrowHeight() + WidgetMetrics.RowGap * scale;
        var hero = WidgetText.Countdown(ref sample ? ref sampleCountdownText : ref countdownText,
            TimeSpan.FromSeconds(remaining));
        WidgetText.Tabular(drawList, new Vector2(area.Min.X, heroTop), hero, ink.Primary,
            WidgetText.FitStyle(hero, WidgetType.DisplayCompact, area.Width, true));
        var bar = BarUnits * scale;
        var barRect = new Rect(new Vector2(area.Min.X, area.Max.Y - bar), area.Max);
        WidgetChrome.Bar(drawList, barRect, 1f - Math.Clamp(remaining / (float)PeriodSeconds, 0f, 1f), ink.Fill,
            ink.Accent(accent));
        if (timed.Amount <= 0)
        {
            return;
        }

        var awardLine = awardText.IsCurrent(timed.Amount)
            ? awardText.Value
            : awardText.Store(timed.Amount, Loc.T(L.WidgetsUtility.CoinsWon, NumberText.Group(timed.Amount)));
        var lineHeight = WidgetText.SpacedLineHeight(WidgetType.Headline);
        WidgetText.Draw(drawList, new Vector2(area.Min.X, barRect.Min.Y - WidgetMetrics.Gutter * scale - lineHeight),
            awardLine, ink.Accent(accent), WidgetType.Headline, area.Width);
    }

    private void DrawLevel(in WidgetContext context, in WidgetInk ink, Rect area, Vector4 accent, bool sample)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var progress = sample ? null : casino.Progress;
        var level = sample ? WidgetSamples.CasinoLevel : progress?.Level ?? 1;
        var fraction = sample ? WidgetSamples.CasinoLevelFraction : LevelFraction(progress);
        var cap = sample ? WidgetSamples.CasinoLevelCap : casino.Ceiling.LevelCap;
        var title = StatusTitle.ForLevel(level);
        var ring = MathF.Min(RingUnits * scale, area.Height);
        var radius = ring * 0.5f;
        var ringCenter = new Vector2(area.Min.X + radius, area.Min.Y + radius);
        var thickness = RingThicknessUnits * scale;
        WidgetChrome.Ring(drawList, ink, ringCenter, radius - thickness * 0.5f, thickness, fraction, accent);
        WidgetText.TabularCentered(drawList, ringCenter, GameNumber.Label(level), ink.Primary, WidgetType.Title,
            ring - thickness * 4f);

        var textLeft = ringCenter.X + radius + WidgetMetrics.Gutter * scale;
        var textWidth = MathF.Max(1f, area.Max.X - textLeft);
        var levelLine = levelText.IsCurrent(level)
            ? levelText.Value
            : levelText.Store(level, Loc.T(L.Strip.Level, GameNumber.Label(level)));
        var lineTop = area.Min.Y;
        WidgetText.Draw(drawList, new Vector2(textLeft, lineTop), levelLine, ink.Primary, WidgetType.Headline, textWidth);
        lineTop += WidgetText.SpacedLineHeight(WidgetType.Headline);
        if (title != BalanceTitle.None)
        {
            var pillHeight = StatusTitle.PillHeight(title, scale);
            var pillWidth = StatusTitle.Width(title, textWidth, scale);
            StatusTitle.Draw(drawList, new Vector2(textLeft + pillWidth * 0.5f, lineTop + pillHeight * 0.5f), title,
                textWidth, scale);
        }

        var bar = BarUnits * scale;
        var barRect = new Rect(new Vector2(area.Min.X, area.Max.Y - bar), area.Max);
        WidgetChrome.Bar(drawList, barRect, fraction, ink.Fill, ink.Accent(accent));
        if (cap <= 0)
        {
            return;
        }

        var capLine = capText.IsCurrent(cap)
            ? capText.Value
            : capText.Store(cap, Loc.T(L.Strip.Cap, NumberText.Compact(cap)));
        WidgetText.DrawRight(drawList, area.Max.X,
            barRect.Min.Y - WidgetMetrics.Gutter * scale - WidgetText.SpacedLineHeight(WidgetType.Caption),
            WidgetText.Fit(capLine, textWidth, WidgetType.Caption, out _), ink.Secondary, WidgetType.Caption);
    }

    private void RefreshWhenExpired(CasinoBonusDto timed, long nowUnix)
    {
        if (timed.Ready || timed.NextAtUnix <= 0 || nowUnix < timed.NextAtUnix || expiredRefreshFor == timed.NextAtUnix)
        {
            return;
        }

        expiredRefreshFor = timed.NextAtUnix;
        casino.RefreshNow();
    }

    private void Maintain()
    {
        if (claimPending && casino.ClaimingBonus.Length == 0)
        {
            claimPending = false;
            casino.TakeBonusResult();
            casino.TakeBonusFailure();
        }

        if (fresh.Due(FreshMilliseconds))
        {
            casino.EnsureFresh();
        }
    }

    public void Dispose()
    {
    }
}
