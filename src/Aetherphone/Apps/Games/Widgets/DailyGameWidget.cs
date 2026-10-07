using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Widgets;

internal sealed class DailyGameWidget : IHomeWidget
{
    private const string AppKey = "games";
    private const int RefreshMilliseconds = 20000;
    private const int SlotCount = 3;
    private const int SlotControlBase = 10;
    private const float IconUnits = 42f;
    private const float SlotIconUnits = 40f;
    private const float BadgeUnits = 16f;
    private const float GradientLift = 0.08f;
    private const float GradientDrop = 0.28f;
    private const float SecondaryAlpha = 0.78f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Ember = AccentRing.Orange;

    private readonly GameStatsStore stats;
    private readonly string[] slotIds = new string[SlotCount];
    private readonly string[] slotTitles = new string[SlotCount];
    private readonly Vector4[] slotAccents = new Vector4[SlotCount];
    private readonly WidgetRoute[] slotRoutes = new WidgetRoute[SlotCount];
    private IMiniGame? game;
    private WidgetRefresh refresh;
    private CachedText streakText;
    private DailyCountdown countdown;
    private int slotFilled;
    private bool slotsRecent;

    public DailyGameWidget(GameStatsStore stats)
    {
        this.stats = stats;
    }

    public string Id => "games.daily";
    public string DisplayName => Loc.T(L.WidgetsUtility.DailyGameName);
    public string Description => Loc.T(L.WidgetsUtility.DailyGameDescription);
    public string AppId => AppKey;
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public float Relevance(string config)
    {
        if (stats.DailyDone)
        {
            return 0f;
        }

        return stats.DailyStreak > 0 ? 0.7f : 0.35f;
    }

    public void Draw(in WidgetContext context)
    {
        Refresh(context);
        var ink = WidgetInk.From(context);
        if (game is null)
        {
            WidgetChrome.Container(context);
            WidgetChrome.Message(context, ink, WidgetMetrics.Content(context), FontAwesomeIcon.Gamepad,
                AppAccents.For(AppKey), Loc.T(L.Apps.Games), string.Empty);
            return;
        }

        var accent = game.Accent;
        var colored = ink.Mode is WidgetMode.FullColor or WidgetMode.Dark;
        if (colored)
        {
            WidgetChrome.Container(context, Palette.Lighten(accent, GradientLift), Palette.Darken(accent, GradientDrop));
        }
        else
        {
            WidgetChrome.Container(context);
        }

        var primary = colored ? ink.Fade(White) : ink.Primary;
        var secondary = colored ? ink.Fade(White, SecondaryAlpha) : ink.Secondary;
        var content = WidgetMetrics.Content(context);
        if (context.Size == WidgetSize.Small || slotFilled == 0)
        {
            DrawDaily(context, ink, game, content, primary, secondary, colored);
            return;
        }

        var gutter = WidgetMetrics.Gutter * context.Scale;
        var half = (content.Width - gutter) * 0.5f;
        DrawDaily(context, ink, game, new Rect(content.Min, new Vector2(content.Min.X + half, content.Max.Y)),
            primary, secondary, colored);
        DrawSlots(context, ink, new Rect(new Vector2(content.Max.X - half, content.Min.Y), content.Max), primary,
            secondary);
    }

    private void DrawDaily(in WidgetContext context, in WidgetInk ink, IMiniGame daily, Rect content,
        Vector4 primary, Vector4 secondary, bool colored)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var accent = daily.Accent;
        var icon = IconUnits * scale;
        var iconMin = content.Min;
        var iconMax = iconMin + new Vector2(icon, icon);
        if (!AppIconTile.TryDraw(drawList, daily.Id, accent, iconMin, iconMax, icon * Metrics.Radius.TileFactor,
                ink.Opacity, false, scale))
        {
            AppIconArt.TryDraw(drawList, daily.Id, (iconMin + iconMax) * 0.5f, icon, primary,
                Palette.Darken(accent, 0.16f));
        }

        var done = stats.DailyDone;
        var streak = stats.DailyStreak;
        if (done || streak > 0)
        {
            var badge = BadgeUnits * scale;
            var badgeCenter = new Vector2(content.Max.X - badge * 0.5f, content.Min.Y + badge * 0.5f);
            ProgressRing.CenterIcon(drawList, badgeCenter, done ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.Fire,
                done ? primary : colored ? ink.Fade(White) : ink.Accent(Ember), badge);
        }

        var captionHeight = WidgetText.SpacedLineHeight(WidgetType.Caption);
        var titleHeight = WidgetText.SpacedLineHeight(WidgetType.Title);
        var eyebrowHeight = WidgetText.EyebrowHeight();
        var captionTop = content.Max.Y - captionHeight;
        var titleSpace = captionTop - (iconMax.Y + WidgetMetrics.Gutter * scale + eyebrowHeight);
        var titleLines = WidgetText.Clamp(daily.Title, WidgetType.Title, content.Width,
            Math.Clamp((int)(titleSpace / titleHeight), 1, 2));
        var titleTop = captionTop - titleLines.Length * titleHeight;
        WidgetText.EyebrowFit(drawList,
            new Vector2(content.Min.X, titleTop - eyebrowHeight - WidgetMetrics.RowGap * scale),
            Loc.T(L.WidgetsUtility.TodaysGame), content.Width, secondary, scale);
        WidgetText.Lines(drawList, titleLines, new Vector2(content.Min.X, titleTop), primary,
            WidgetType.Title, titleHeight);
        WidgetText.Draw(drawList, new Vector2(content.Min.X, captionTop), Caption(done, streak), secondary,
            WidgetType.Caption, content.Width);
    }

    private void DrawSlots(in WidgetContext context, in WidgetInk ink, Rect area, Vector4 primary, Vector4 secondary)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        WidgetText.EyebrowFit(drawList, area.Min,
            slotsRecent ? Loc.T(L.GamesHub.ContinuePlaying) : Loc.T(L.Games.ShelfLatest), area.Width, secondary,
            scale);
        var top = area.Min.Y + WidgetText.EyebrowHeight() + WidgetMetrics.RowGap * scale * 2f;
        var rowHeight = (area.Max.Y - top) / SlotCount;
        var icon = MathF.Min(SlotIconUnits * scale, rowHeight - WidgetMetrics.RowGap * scale);
        for (var index = 0; index < slotFilled; index++)
        {
            var rowTop = top + index * rowHeight;
            var row = new Rect(new Vector2(area.Min.X, rowTop), new Vector2(area.Max.X, rowTop + rowHeight));
            WidgetControls.Link(context, ink, SlotControlBase + index, row, slotRoutes[index]);
            var iconMin = new Vector2(row.Min.X, row.Center.Y - icon * 0.5f);
            var iconMax = iconMin + new Vector2(icon, icon);
            if (!AppIconTile.TryDraw(drawList, slotIds[index], slotAccents[index], iconMin, iconMax,
                    icon * Metrics.Radius.TileFactor, ink.Opacity, false, scale))
            {
                AppIconArt.TryDraw(drawList, slotIds[index], (iconMin + iconMax) * 0.5f, icon, primary,
                    Palette.Darken(slotAccents[index], 0.16f));
            }

            var textLeft = iconMax.X + WidgetMetrics.RowGap * scale * 2f;
            var lineHeight = WidgetText.LineHeight(WidgetType.Headline);
            WidgetText.Draw(drawList, new Vector2(textLeft, row.Center.Y - lineHeight * 0.5f), slotTitles[index],
                primary, WidgetType.Headline, area.Max.X - textLeft);
        }
    }

    private void Refresh(in WidgetContext context)
    {
        if (!refresh.Due(RefreshMilliseconds) && game is not null)
        {
            return;
        }

        if (context.Actions.App(AppKey) is not GamesApp games)
        {
            game = null;
            slotFilled = 0;
            return;
        }

        game = games.DailyGame;
        FillSlots(games.Library, game.Id);
    }

    private void FillSlots(GamesLibrary library, string dailyId)
    {
        slotFilled = 0;
        slotsRecent = library.Recent.Length > 0;
        var source = slotsRecent ? library.Recent : library.Latest;
        for (var position = 0; position < source.Length && slotFilled < SlotCount; position++)
        {
            ref readonly var entry = ref library.Entries[source[position]];
            if (entry.Online || string.Equals(entry.Id, dailyId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(slotIds[slotFilled], entry.Id, StringComparison.Ordinal))
            {
                slotIds[slotFilled] = entry.Id;
                slotRoutes[slotFilled] = WidgetRoute.Tab(AppKey, GamesApp.PlayRoute(entry.Id));
            }

            slotTitles[slotFilled] = library.Title(source[position]);
            slotAccents[slotFilled] = library.Accent(source[position]);
            slotFilled++;
        }
    }

    private string Caption(bool done, int streak)
    {
        if (done)
        {
            return countdown.Label();
        }

        if (streak <= 0)
        {
            return Loc.T(L.WidgetsUtility.StartStreak);
        }

        return streakText.IsCurrent(streak)
            ? streakText.Value
            : streakText.Store(streak, Loc.T(L.WidgetsUtility.Streak, streak));
    }

    public void Dispose()
    {
    }
}
