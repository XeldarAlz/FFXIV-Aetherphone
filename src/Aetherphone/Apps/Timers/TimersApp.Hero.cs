using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Timers;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Timers;

internal sealed partial class TimersApp
{
    private const float HeroGlassOpacity = 0.92f;
    private const float HeroTitleGap = 2f;
    private const float HeroBarGap = 14f;
    private const float HeroCheckSize = 44f;
    private const float HeroCheckGlyph = 20f;
    private const float HeroValueGap = 12f;
    private const float HeroBarTrackAlpha = 0.28f;
    private const float HeroSubAlpha = 0.82f;
    private const long DaySeconds = 86400;
    private const long WeekSeconds = 604800;

    private static readonly Vector4 HeroInk = new(1f, 1f, 1f, 1f);

    private Spring heroAppear;
    private bool heroPrimed;
    private long heroKey;
    private CachedText heroCountdown;
    private CachedText heroSub;
    private CachedText heroReady;
    private CachedText heroEnd;

    private readonly record struct HeroPick(string Title, LocString? Kind, long StartUnix, long EndUnix);

    private void DrawHero(in TimerTally tally, DateTime utcNow, long nowUnix, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var pad = Metrics.Space.Lg * scale;
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var subHeight = Typography.LineHeight(TextStyles.Subheadline);
        var height = pad * 2f + eyebrowHeight + titleHeight + HeroTitleGap * scale + subHeight +
                     (HeroBarGap + TimersArt.BarHeight) * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ReportAnchor("timers.hero", new Rect(origin, max));
        if (!ImGui.IsRectVisible(origin, max))
        {
            Advance(origin, width, height, TimersArt.SectionGap, scale);
            return;
        }

        var ready = tally.Ready > 0;
        var pick = ready ? default : PickNext(tally, utcNow);
        var key = ready ? -1L - tally.Ready : pick.EndUnix ^ ((long)pick.Title.GetHashCode() << 20);
        var alpha = StepHeroAppear(key);

        var drawList = ImGui.GetWindowDrawList();
        var accent = ready ? TimersArt.ReadyInk : ui.Accent;
        Material.AccentGlass(drawList, origin, max, Metrics.Radius.Widget * scale, scale, accent, HeroGlassOpacity);

        var ink = Palette.WithAlpha(HeroInk, alpha);
        var subInk = Palette.WithAlpha(HeroInk, HeroSubAlpha * alpha);
        var left = origin.X + pad;
        var right = max.X - pad;
        var top = origin.Y + pad;
        var eyebrow = Loc.T(ready ? L.Timers.ReadyToCollect : L.Timers.NextUp);
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(eyebrow, right - left,
            TextStyles.FootnoteEmphasized), subInk, TextStyles.FootnoteEmphasized);
        var titleTop = top + eyebrowHeight;
        var barTop = max.Y - pad - TimersArt.BarHeight * scale;

        if (ready)
        {
            var checkSize = HeroCheckSize * scale;
            var checkCenter = new Vector2(right - checkSize * 0.5f, titleTop + (barTop - titleTop) * 0.5f);
            drawList.AddCircleFilled(checkCenter, checkSize * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(HeroInk,
                0.22f * alpha)), 32);
            ProgressRing.CenterIcon(drawList, checkCenter, FontAwesomeIcon.Check, ink, HeroCheckGlyph * scale);
            var textRight = checkCenter.X - checkSize * 0.5f - HeroValueGap * scale;
            Typography.Draw(drawList, new Vector2(left, titleTop),
                Typography.FitText(ReadySummary(tally), textRight - left, TextStyles.Title3), ink, TextStyles.Title3);
            Advance(origin, width, height, TimersArt.SectionGap, scale);
            return;
        }

        var countdown = WidgetText.Countdown(ref heroCountdown, TimeSpan.FromSeconds(pick.EndUnix - nowUnix));
        var countdownSize = Typography.Measure(countdown, TextStyles.WidgetDisplayCompact);
        var countdownLeft = right - countdownSize.X;
        Typography.Draw(drawList, new Vector2(countdownLeft, titleTop + (barTop - titleTop - countdownSize.Y) * 0.5f),
            countdown, ink, TextStyles.WidgetDisplayCompact);

        var textRightEdge = countdownLeft - HeroValueGap * scale;
        Typography.Draw(drawList, new Vector2(left, titleTop),
            Typography.FitText(pick.Title, MathF.Max(1f, textRightEdge - left), TextStyles.Title3), ink,
            TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(left, titleTop + titleHeight + HeroTitleGap * scale),
            Typography.FitText(HeroSubtitle(pick), MathF.Max(1f, textRightEdge - left), TextStyles.Subheadline),
            subInk, TextStyles.Subheadline);

        var fraction = TimerLedger.Progress(pick.StartUnix, pick.EndUnix, nowUnix);
        TimersArt.Bar(drawList, new Vector2(left, barTop), new Vector2(right, barTop + TimersArt.BarHeight * scale),
            fraction, Palette.WithAlpha(HeroInk, HeroBarTrackAlpha * alpha), ink);
        Advance(origin, width, height, TimersArt.SectionGap, scale);
    }

    private HeroPick PickNext(in TimerTally tally, DateTime utcNow)
    {
        var daily = Unix(GameSchedule.NextDailyReset(utcNow));
        var best = new HeroPick(Loc.T(L.Timers.DailyReset), null, daily - DaySeconds, daily);
        var grandCompany = Unix(GameSchedule.NextGrandCompanyReset(utcNow));
        if (grandCompany < best.EndUnix)
        {
            best = new HeroPick(Loc.T(L.Timers.GrandCompanyReset), null, grandCompany - DaySeconds, grandCompany);
        }

        var weekly = Unix(GameSchedule.NextWeeklyReset(utcNow));
        if (weekly < best.EndUnix)
        {
            best = new HeroPick(Loc.T(L.Timers.WeeklyReset), null, weekly - WeekSeconds, weekly);
        }

        var running = tally.Soonest;
        if (running.Exists && running.EndUnix < best.EndUnix)
        {
            best = new HeroPick(running.Name, running.Voyage ? L.Timers.Voyage : L.Timers.Venture, running.StartUnix,
                running.EndUnix);
        }

        return best;
    }

    private string HeroSubtitle(in HeroPick pick)
    {
        var kindHash = pick.Kind is { } kind ? kind.Key.GetHashCode() : 0;
        var today = DateTime.Now.DayOfYear;
        var key = (pick.EndUnix / 60) ^ ((long)kindHash << 24) ^ ((long)today << 52);
        if (heroSub.IsCurrent(key))
        {
            return heroSub.Value;
        }

        var end = TimerLabels.End(ref heroEnd, pick.EndUnix);
        return heroSub.Store(key, pick.Kind is { } label ? string.Concat(Loc.T(label), " · ", end) : end);
    }

    private string ReadySummary(in TimerTally tally)
    {
        var key = ((long)tally.ReadyVentures << 16) | (uint)tally.ReadyVoyages;
        if (heroReady.IsCurrent(key))
        {
            return heroReady.Value;
        }

        string text;
        if (tally.ReadyVentures > 0 && tally.ReadyVoyages > 0)
        {
            text = string.Concat(Loc.Plural(L.Timers.ReadyVentures, tally.ReadyVentures), ", ",
                Loc.Plural(L.Timers.ReadyVoyages, tally.ReadyVoyages));
        }
        else if (tally.ReadyVentures > 0)
        {
            text = Loc.Plural(L.Timers.ReadyVentures, tally.ReadyVentures);
        }
        else
        {
            text = Loc.Plural(L.Timers.ReadyVoyages, tally.ReadyVoyages);
        }

        return heroReady.Store(key, text);
    }

    private float StepHeroAppear(long key)
    {
        if (!heroPrimed)
        {
            heroPrimed = true;
            heroKey = key;
            heroAppear.SnapTo(1f);
            return 1f;
        }

        if (key != heroKey)
        {
            heroKey = key;
            heroAppear.SnapTo(0f);
        }

        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        return Math.Clamp(heroAppear.Step(1f, Motion.Appear, deltaSeconds), 0f, 1f);
    }

    private static long Unix(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();
}
