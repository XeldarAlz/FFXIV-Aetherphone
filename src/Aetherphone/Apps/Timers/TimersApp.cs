using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Timers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Timers;

internal sealed partial class TimersApp : IPhoneApp
{
    private const float BottomPad = 28f;

    public string Id => "timers";

    public string DisplayName => Loc.T(L.Apps.Timers);

    public string Glyph => "T";

    public int BadgeCount => TimerBoard.Tally(timers.Characters, timers.Workshops,
        DateTimeOffset.UtcNow.ToUnixTimeSeconds()).Ready;

    public bool HasBadge => true;

    private readonly Configuration configuration;
    private readonly GameTimers timers;
    private readonly AppSkin ui = new(AppPalettes.Timers);
    private readonly List<TimerCharacterRecord> orderedCharacters = new();
    private readonly List<TimerText> textPool = new();
    private INavigator navigation = null!;
    private int textCursor;

    public TimersApp(Configuration configuration, GameTimers timers)
    {
        this.configuration = configuration;
        this.timers = timers;
    }

    public void OnOpened()
    {
        heroPrimed = false;
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        navigation = context.Navigation;
        ui.Theme = context.Theme;
        var area = context.Content;
        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(area, context.Theme, scale));
        ui.Body(area);

        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (AppSurface.Begin(navBar.Body))
        {
            textCursor = 0;
            var utcNow = DateTime.UtcNow;
            var nowUnix = new DateTimeOffset(utcNow).ToUnixTimeSeconds();
            var tally = TimerBoard.Tally(timers.Characters, timers.Workshops, nowUnix);
            var width = ImGui.GetContentRegionAvail().X;
            DrawHero(tally, utcNow, nowUnix, width, scale);
            DrawRetainers(nowUnix, width, scale);
            DrawVoyages(nowUnix, width, scale);
            DrawResets(utcNow, nowUnix, width, scale);
            DrawActivities(utcNow, nowUnix, width, scale);
            ImGui.Dummy(new Vector2(0f, BottomPad * scale));
        }

        AppHeader.EndLargeTitle(in navBar, context, "timers.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private TimerText NextText()
    {
        if (textCursor == textPool.Count)
        {
            textPool.Add(new TimerText());
        }

        return textPool[textCursor++];
    }

    private static void Advance(Vector2 origin, float width, float height, float gap, float scale)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + gap * scale - ImGui.GetStyle().ItemSpacing.Y));
    }

    private bool SectionHeader(string title, float width, float scale, string? bellId, bool bellOn,
        out Rect headerRect, out Rect bellRect)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = TimersArt.SectionHeaderHeight * scale;
        headerRect = new Rect(origin, origin + new Vector2(width, height));
        bellRect = default;
        var drawList = ImGui.GetWindowDrawList();
        var centerY = origin.Y + height * 0.5f;
        var bellSpan = bellId is null ? 0f : TimersArt.BellHitRadius * 2f * scale;
        var fitted = Typography.FitText(title, MathF.Max(1f, width - bellSpan), TextStyles.Title3);
        var titleHeight = Typography.Measure(fitted, TextStyles.Title3).Y;
        Typography.Draw(drawList, new Vector2(origin.X, centerY - titleHeight * 0.5f), fitted, ui.TitleInk,
            TextStyles.Title3);
        var clicked = false;
        if (bellId is not null)
        {
            var center = new Vector2(origin.X + width - TimersArt.BellHitRadius * scale, centerY);
            var hit = new Vector2(TimersArt.BellHitRadius, TimersArt.BellHitRadius) * scale;
            bellRect = new Rect(center - hit, center + hit);
            clicked = TimersArt.Bell(drawList, bellId, center, bellOn, ui, BellTooltip(bellOn), scale);
        }

        Advance(origin, width, height, 0f, scale);
        return clicked;
    }

    private static string BellTooltip(bool on) => Loc.T(on ? L.Timers.NotifyOn : L.Timers.NotifyOff);

    private void SaveToggle()
    {
        configuration.Save();
    }

    private void ReportAnchor(string key, Rect rect)
    {
        if (UiAnchors.Recording)
        {
            UiAnchors.Report(key, rect);
        }
    }

    public void Dispose()
    {
    }
}
