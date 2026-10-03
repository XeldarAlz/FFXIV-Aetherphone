using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Activity;

internal sealed partial class ActivityApp : IPhoneApp
{
    private const float BottomBreathing = 28f;
    private const float WeekCardHeight = 92f;
    private const float WeekRingRadius = 15f;
    private const float WeekRingThickness = 3.6f;
    private const float WeekRingGap = 0.8f;
    private const float WeekLetterTop = 14f;
    private const float WeekRingTop = 46f;
    private const float WeekSelectRadius = 13f;
    private const float WeekHoverAlpha = 0.06f;
    private const string TimersAppId = "timers";

    public string Id => "character";
    public string DisplayName => Loc.T(L.Character.Activity);
    public string Glyph => "Ac";
    public int BadgeCount => tracker.VenturesReady;
    public bool HasBadge => true;

    private readonly ActivityTracker tracker;
    private readonly Configuration configuration;
    private readonly AppSkin ui = new(AppPalettes.Activity);
    private readonly ActivityDigest digest = new();
    private readonly ViewRouter<ActivityView> router;
    private readonly RouterDraw<ActivityView> drawView;
    private readonly Action back;
    private readonly float[] ringScratch = new float[ActivityGoals.RingCount];
    private readonly Spring[] weekFills = new Spring[ActivityDigest.WeekLength * ActivityGoals.RingCount];
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private ActivityTargets targets;
    private int selectedSlot = ActivityDigest.TodaySlot;

    public ActivityApp(ActivityTracker tracker, Configuration configuration)
    {
        this.tracker = tracker;
        this.configuration = configuration;
        router = new ViewRouter<ActivityView>(ActivityView.Summary());
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        selectedSlot = ActivityDigest.TodaySlot;
        digest.Invalidate();
        ResetFills();
    }

    public void OnClosed()
    {
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        targets = ActivityTargets.From(configuration);
        if (tracker.IsTracking)
        {
            TourHolds.Release(Id);
            digest.Refresh(tracker, targets);
        }
        else
        {
            TourHolds.Hold(Id);
        }

        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, theme, scale));
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(ActivityView view, Rect area, int depth)
    {
        ui.Body(area);
        if (!tracker.IsTracking)
        {
            DrawSignedOut(area);
            return;
        }

        switch (view.Kind)
        {
            case ActivityViewKind.Day:
                DrawDay(area);
                break;
            case ActivityViewKind.Goals:
                DrawGoals(area);
                break;
            default:
                DrawSummary(area);
                break;
        }
    }

    private void DrawSignedOut(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            ResetFills();
            var bottom = origin.Y + ActivityArt.State(drawList, ui, origin, width, FontAwesomeIcon.UserClock,
                ui.Accent, Loc.T(L.Character.SignedOutTitle), Loc.T(L.Character.SignedOutBody), scale);
            ReserveTo(origin, width, bottom + BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "character.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void ResetFills()
    {
        for (var index = 0; index < weekFills.Length; index++)
        {
            weekFills[index].SnapTo(0f);
        }

        for (var ring = 0; ring < ActivityGoals.RingCount; ring++)
        {
            heroFills[ring].SnapTo(0f);
            dayFills[ring].SnapTo(0f);
            goalFills[ring].SnapTo(0f);
        }
    }

    private void OpenDay(int slot)
    {
        UiFeedback.Play(UiSound.Tap);
        selectedSlot = slot;
        for (var ring = 0; ring < ActivityGoals.RingCount; ring++)
        {
            dayFills[ring].SnapTo(0f);
        }

        router.Push(ActivityView.Day());
    }

    private float WeekStrip(ImDrawListPtr drawList, Vector2 origin, float width, int highlight, bool pushOnTap,
        float scale)
    {
        var height = WeekCardHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var card = new Rect(origin, max);
        UiAnchors.Report("character.week", card);
        ui.Card(drawList, origin, max, Metrics.Radius.Widget * scale, true);
        var pad = ActivityArt.CardPad * scale * 0.5f;
        var columnWidth = (width - pad * 2f) / ActivityDigest.WeekLength;
        var delta = ActivityArt.FrameDelta();
        for (var slot = 0; slot < ActivityDigest.WeekLength; slot++)
        {
            var columnMin = new Vector2(origin.X + pad + columnWidth * slot, origin.Y);
            var column = new Rect(columnMin, new Vector2(columnMin.X + columnWidth, max.Y));
            var centerX = column.Center.X;
            var hovered = UiInteract.Hover(column.Min, column.Max);
            if (hovered)
            {
                Squircle.Fill(drawList, column.Min + new Vector2(0f, pad), column.Max - new Vector2(0f, pad),
                    Metrics.Radius.Md * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, WeekHoverAlpha)));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var letterCenter = new Vector2(centerX, origin.Y + WeekLetterTop * scale +
                                                    Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f);
            var isToday = slot == ActivityDigest.TodaySlot;
            var selected = slot == highlight;
            if (selected)
            {
                drawList.AddCircleFilled(letterCenter, WeekSelectRadius * scale, ImGui.GetColorU32(ui.TitleInk), 24);
            }

            var letterInk = selected ? ui.Palette.BackdropBottom : isToday ? ui.Accent : ui.MutedInk;
            Typography.DrawCentered(drawList, letterCenter,
                Typography.FitText(digest.Letters[slot], columnWidth, TextStyles.FootnoteEmphasized), letterInk,
                TextStyles.FootnoteEmphasized);
            var offset = slot * ActivityGoals.RingCount;
            for (var ring = 0; ring < ActivityGoals.RingCount; ring++)
            {
                ringScratch[ring] = weekFills[offset + ring].Step(digest.Fractions[offset + ring], Motion.Sheet, delta);
            }

            var ringRadius = MathF.Min(WeekRingRadius * scale, columnWidth * 0.42f);
            ActivityArt.Rings(drawList, new Vector2(centerX, origin.Y + WeekRingTop * scale + ringRadius), ringRadius,
                WeekRingThickness * scale * ringRadius / (WeekRingRadius * scale), WeekRingGap * scale, ringScratch,
                false);
            if (!UiInteract.Click(column.Min, column.Max, hovered))
            {
                continue;
            }

            if (pushOnTap)
            {
                OpenDay(slot);
                continue;
            }

            UiFeedback.Play(UiSound.Tap);
            selectedSlot = slot;
        }

        return max.Y;
    }

    private static void ReserveTo(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y)));
    }

    public void Dispose()
    {
    }
}
