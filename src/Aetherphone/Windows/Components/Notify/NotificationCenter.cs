using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class NotificationCenter
{
    private const float SummaryHeight = 36f;
    private const float SummaryGap = 10f;
    private const float SummaryPadX = 16f;
    private const float EmptyHeight = 72f;
    private const float WheelStep = 48f;
    private const float PillPadX = 12f;
    private const float PillGap = 8f;
    private const float HoverLift = 0.35f;
    private const string SummarySeparator = "  ·  ";
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private readonly NotificationService notifications;
    private readonly NotificationDeck deck;
    private readonly NotificationGroups groups = new();
    private readonly KineticScroller scroller = new();
    private int builtVersion = -1;
    private LanguageInfo? builtLanguage;
    private string summaryLabel = string.Empty;
    private int summaryCount = -1;
    private long summaryAgeMinutes = -1;
    private LanguageInfo? summaryLanguage;
    private float scrollY;

    public NotificationCenter(NotificationService notifications, NotificationRouter router, Action? navigated = null)
    {
        this.notifications = notifications;
        deck = new NotificationDeck(notifications, router, navigated);
    }

    public void Reset()
    {
        deck.Reset();
        scrollY = 0f;
        scroller.Reset();
        builtVersion = -1;
    }

    public void DrawOverlay(ImDrawListPtr drawList, Rect area, PhoneTheme theme, float opacity, bool interactive)
    {
        DrawCore(drawList, area, theme, UiScale.Current, opacity, interactive);
    }

    public float MeasureHeight(float scale)
    {
        EnsureBuilt();
        if (groups.Count == 0)
        {
            return EmptyHeight * scale;
        }

        return (SummaryHeight + SummaryGap) * scale + ContentHeight(scale);
    }

    private void DrawCore(ImDrawListPtr drawList, Rect body, PhoneTheme theme, float scale, float opacity,
        bool interactive)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        EnsureBuilt();
        if (groups.Count == 0)
        {
            Typography.DrawCentered(drawList, body.Center, Loc.T(L.Notifications.Empty),
                Palette.WithAlpha(theme.TextMuted, opacity));
        }
        else
        {
            var summary = new Rect(body.Min, new Vector2(body.Max.X, body.Min.Y + SummaryHeight * scale));
            DrawSummary(drawList, summary, theme, scale, opacity, interactive);
            var listArea = new Rect(new Vector2(body.Min.X, summary.Max.Y + SummaryGap * scale), body.Max);
            if (listArea.Height > 1f && listArea.Width > 1f)
            {
                DrawList(drawList, theme, listArea, scale, opacity, interactive);
            }
        }

        deck.Advance(delta);
    }

    private void EnsureBuilt()
    {
        var language = Loc.Current;
        if (builtVersion == notifications.Version && ReferenceEquals(builtLanguage, language))
        {
            return;
        }

        builtVersion = notifications.Version;
        builtLanguage = language;
        groups.Rebuild(notifications.Recent);
        deck.Sync(groups);
        summaryCount = -1;
    }

    private void DrawSummary(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, float scale, float opacity,
        bool interactive)
    {
        Material.LiquidGlass(drawList, rect.Min, rect.Max, rect.Height * 0.5f, scale, GlassTone.Dark, 0f, opacity);
        var padX = SummaryPadX * scale;
        var clearLabel = Loc.T(L.Notifications.ClearAll);
        var clearSize = Typography.Measure(clearLabel, TextStyles.FootnoteEmphasized);
        var clearMin = new Vector2(rect.Max.X - padX - clearSize.X - PillPadX * scale, rect.Min.Y);
        var clearMax = rect.Max;
        var clearHovered = interactive && UiInteract.Hover(clearMin, clearMax);
        var accent = clearHovered ? Palette.Mix(theme.Accent, White, HoverLift) : theme.Accent;
        Typography.Draw(drawList, new Vector2(rect.Max.X - padX - clearSize.X, rect.Center.Y - clearSize.Y * 0.5f),
            clearLabel, Palette.WithAlpha(accent, opacity), TextStyles.FootnoteEmphasized);
        var textLeft = rect.Min.X + padX;
        var textMaxWidth = MathF.Max(1f, clearMin.X - PillGap * scale - textLeft);
        var summary = Typography.FitText(SummaryLabel(), textMaxWidth, TextStyles.Footnote);
        var summarySize = Typography.Measure(summary, TextStyles.Footnote);
        var muted = NotificationCard.MutedInk(GlassTone.Dark, theme);
        Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - summarySize.Y * 0.5f), summary,
            Palette.WithAlpha(muted, muted.W * opacity), TextStyles.Footnote);
        if (!interactive)
        {
            return;
        }

        if (clearHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(clearMin, clearMax, clearHovered))
        {
            ClearAll();
        }
    }

    private string SummaryLabel()
    {
        var count = groups.TotalCount;
        var oldest = groups.OldestReceivedAt;
        var ageMinutes = oldest == default ? -1L : (long)Math.Max(0d, (DateTime.Now - oldest).TotalMinutes);
        var language = Loc.Current;
        if (count == summaryCount && ageMinutes == summaryAgeMinutes && ReferenceEquals(language, summaryLanguage))
        {
            return summaryLabel;
        }

        summaryCount = count;
        summaryAgeMinutes = ageMinutes;
        summaryLanguage = language;
        var waiting = Loc.Plural(L.Notifications.Waiting, count);
        summaryLabel = oldest == default
            ? waiting
            : string.Concat(waiting, SummarySeparator,
                Loc.T(L.Notifications.Oldest, TimeText.Ago(oldest.ToUniversalTime())));
        return summaryLabel;
    }

    private void ClearAll()
    {
        UiFeedback.Play(UiSound.Tap);
        deck.Router.AcknowledgeAll();
        notifications.Clear();
        Reset();
    }

    private float ContentHeight(float scale)
    {
        var total = 0f;
        var list = groups.Groups;
        for (var index = 0; index < list.Count; index++)
        {
            total += deck.Height(list[index], scale) + NotificationDeck.GroupGap * scale;
        }

        return total;
    }

    private void DrawList(ImDrawListPtr drawList, PhoneTheme theme, Rect listArea, float scale, float opacity,
        bool interactive)
    {
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var maxScroll = MathF.Max(0f, ContentHeight(scale) - listArea.Height);
        scroller.Scale = scale;
        scroller.SetBounds(maxScroll);

        if (!deck.UpdateDrag(scale, deltaSeconds, scroller))
        {
            scroller.Tick(deltaSeconds);
            if (interactive && UiInteract.HoverWindowOnly(listArea.Min, listArea.Max, false))
            {
                var wheel = ImGui.GetIO().MouseWheel;
                if (wheel != 0f)
                {
                    var basis = scroller.IsControlling ? scroller.Offset : scrollY;
                    scrollY = Math.Clamp(basis - wheel * WheelStep * scale, 0f, maxScroll);
                    scroller.Reset();
                    scroller.SetBounds(maxScroll);
                    scroller.SyncOffset(scrollY);
                }
            }
        }

        if (scroller.IsControlling)
        {
            scrollY = scroller.Offset;
        }
        else
        {
            scroller.SyncOffset(Math.Clamp(scrollY, 0f, maxScroll));
        }

        scrollY = Math.Clamp(scrollY, 0f, maxScroll);
        if (interactive && (deck.DragActive || UiInteract.HoverWindowOnly(listArea.Min, listArea.Max, false)))
        {
            UiInteract.ReportGestureSurface();
        }

        deck.BeginFrame(listArea);
        var style = NotificationDeckStyle.ForGlass(theme, GlassTone.Dark);
        drawList.PushClipRect(listArea.Min, listArea.Max, true);
        var y = listArea.Min.Y - scrollY;
        var list = groups.Groups;
        for (var index = 0; index < list.Count; index++)
        {
            var group = list[index];
            var blockHeight = deck.Height(group, scale);
            if (y + blockHeight >= listArea.Min.Y && y <= listArea.Max.Y)
            {
                deck.DrawGroup(drawList, group, new Vector2(listArea.Min.X, y), listArea.Width, style, scale,
                    opacity, interactive);
            }

            y += blockHeight + NotificationDeck.GroupGap * scale;
        }

        drawList.PopClipRect();
        deck.EndFrame(scale, interactive, scroller);
    }
}
