using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.News;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.News.Widgets;

internal sealed class LodestoneWidget : IHomeWidget
{
    private const string AppKey = "news";
    private const string CategoryKey = "category";
    private const int RequestMilliseconds = 60000;
    private const int MaxRows = 6;
    private const float RowUnits = 36f;
    private const float HeroShare = 0.46f;
    private const float ScrimAlpha = 0.62f;
    private static readonly long[] SampleAgesMinutes = { 50, 5 * 60, 26 * 60, 50 * 60 };
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    private static readonly WidgetChoice[] CategoryChoices =
    {
        new("topics", L.News.Topics), new("notices", L.News.Notices), new("maintenance", L.News.Maintenance),
        new("updates", L.News.Updates),
    };

    private static readonly LocString[] CategoryLabels =
        { L.News.Topics, L.News.Notices, L.News.Maintenance, L.News.Updates };

    private readonly NewsService news;
    private readonly GameData gameData;
    private readonly RemoteImageCache images;
    private readonly WidgetOption[] options;
    private readonly NewsEntry?[] entries = new NewsEntry?[NewsCategories.All.Length];
    private readonly WidgetRefresh[] requests = new WidgetRefresh[NewsCategories.All.Length];
    private readonly CachedText[] captions = new CachedText[NewsCategories.All.Length * (MaxRows + 1)];
    private readonly CachedText[] sampleCaptions = new CachedText[MaxRows];

    public LodestoneWidget(NewsService news, GameData gameData, RemoteImageCache images)
    {
        this.news = news;
        this.gameData = gameData;
        this.images = images;
        options = new[]
        {
            new WidgetOption(CategoryKey, L.WidgetsUtility.NewsCategoryOption, CategoryChoices,
                CategoryChoices[0].Value),
        };
    }

    public string Id => "news.headlines";
    public string DisplayName => Loc.T(L.WidgetsUtility.LodestoneName);
    public string Description => Loc.T(L.WidgetsUtility.NewsDescription);
    public string AppId => AppKey;
    public WidgetSizeSet Sizes => WidgetSizeSet.Medium | WidgetSizeSet.Large;
    public IReadOnlyList<WidgetOption> Options => options;

    public float Relevance(string config)
    {
        var entry = entries[(int)CategoryOf(config)];
        if (entry is null || entry.Items.Length == 0)
        {
            return 0f;
        }

        return DateTimeOffset.UtcNow - entry.Items[0].Time < TimeSpan.FromHours(12) ? 0.3f : 0f;
    }

    public void Draw(in WidgetContext context)
    {
        var category = CategoryOf(context.Config);
        var slot = (int)category;
        if (!context.Preview && context.Opacity > 0f)
        {
            Request(slot, category);
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var drawList = context.DrawList;
        var scale = context.Scale;
        var content = WidgetMetrics.Content(context);
        var accent = AppAccents.For(AppKey);
        var top = WidgetChrome.Header(context, ink, AppKey, Loc.T(L.WidgetsUtility.LodestoneName), accent,
            Loc.T(CategoryLabels[slot]), ink.Secondary);
        var body = new Rect(new Vector2(content.Min.X, top + WidgetMetrics.RowGap * scale), content.Max);
        var entry = entries[slot];
        var items = entry?.Items ?? Array.Empty<LodestoneNewsItem>();
        var sample = context.Preview && items.Length == 0;
        if (!sample && items.Length == 0)
        {
            DrawEmptyState(context, ink, body, entry, accent);
            return;
        }

        var total = sample ? WidgetSamples.Headlines.Length : items.Length;
        var rowArea = body;
        var first = 0;
        if (context.Size == WidgetSize.Large && !sample && items[0].Image is { Length: > 0 } image &&
            images.Sized(image, body.Width) is { } texture)
        {
            var heroHeight = body.Height * HeroShare;
            var hero = new Rect(body.Min, new Vector2(body.Max.X, body.Min.Y + heroHeight));
            DrawHero(context, ink, hero, items[0], texture);
            rowArea = new Rect(new Vector2(body.Min.X, hero.Max.Y + WidgetMetrics.Gutter * scale), body.Max);
            first = 1;
        }

        var capacity = Math.Clamp((int)(rowArea.Height / (RowUnits * scale)), 1, MaxRows);
        var rowHeight = rowArea.Height / capacity;
        var rows = Math.Min(capacity, total - first);
        var route = WidgetRoute.App(AppKey);
        for (var index = 0; index < rows; index++)
        {
            var rowRect = new Rect(new Vector2(rowArea.Min.X, rowArea.Min.Y + index * rowHeight),
                new Vector2(rowArea.Max.X, rowArea.Min.Y + (index + 1) * rowHeight));
            WidgetControls.Link(context, ink, index + 1, rowRect, route);
            string title;
            string caption;
            if (sample)
            {
                title = Loc.T(WidgetSamples.Headlines[index]);
                caption = SampleCaption(index);
            }
            else
            {
                var item = items[first + index];
                title = item.Title;
                caption = Caption(ref captions[slot * (MaxRows + 1) + first + index], item, category);
            }

            var titleHeight = WidgetText.SpacedLineHeight(WidgetType.Headline);
            var captionHeight = WidgetText.SpacedLineHeight(WidgetType.Caption);
            var textTop = rowRect.Center.Y - (titleHeight + captionHeight) * 0.5f;
            WidgetText.Draw(drawList, new Vector2(rowRect.Min.X, textTop), title, ink.Primary,
                WidgetType.Headline, rowRect.Width);
            WidgetText.Draw(drawList, new Vector2(rowRect.Min.X, textTop + titleHeight), caption,
                ink.Secondary, WidgetType.Caption, rowRect.Width);
            if (index < rows - 1)
            {
                WidgetChrome.Separator(context, ink, rowRect.Min.X, rowRect.Max.X, rowRect.Max.Y);
            }
        }
    }

    private void DrawHero(in WidgetContext context, in WidgetInk ink, Rect hero, LodestoneNewsItem item,
        IDalamudTextureWrap texture)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var radius = WidgetMetrics.InnerRadius(context);
        WidgetControls.Link(context, ink, 0, hero, WidgetRoute.App(AppKey));
        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, hero.Width, hero.Height);
        Squircle.FillImage(drawList, hero.Min, hero.Max, radius, texture.Handle, ImGui.GetColorU32(ink.ImageTint),
            uv0, uv1);
        var titleHeight = WidgetText.SpacedLineHeight(WidgetType.Headline);
        var inset = WidgetMetrics.Gutter * scale;
        var lines = WidgetText.Clamp(item.Title, WidgetType.Headline, hero.Width - inset * 2f, 2);
        var scrimTop = hero.Max.Y - lines.Length * titleHeight - inset * 2.5f;
        Squircle.FillVerticalGradient(drawList, new Vector2(hero.Min.X, scrimTop), hero.Max, radius,
            ImGui.GetColorU32(Black with { W = 0f }), ImGui.GetColorU32(ink.Fade(Black, ScrimAlpha)));
        WidgetText.Lines(drawList, lines,
            new Vector2(hero.Min.X + inset, hero.Max.Y - inset - lines.Length * titleHeight), ink.Fade(White),
            WidgetType.Headline, titleHeight);
    }

    private static void DrawEmptyState(in WidgetContext context, in WidgetInk ink, Rect body, NewsEntry? entry,
        Vector4 accent)
    {
        var state = entry?.State ?? NewsState.Idle;
        if (state is NewsState.Idle or NewsState.Loading)
        {
            var rows = Math.Clamp((int)(body.Height / (RowUnits * context.Scale)), 1, MaxRows);
            WidgetChrome.RedactedRows(context, ink, body, rows, WidgetRowLead.None);
            return;
        }

        var line = state == NewsState.Failed ? L.WidgetsUtility.NewsFailed : L.WidgetsUtility.NewsEmpty;
        WidgetChrome.Message(context, ink, body, FontAwesomeIcon.Newspaper, accent, Loc.T(line), string.Empty);
    }

    private void Request(int slot, NewsCategory category)
    {
        var entry = entries[slot];
        var waiting = entry is null || entry.State == NewsState.Idle;
        if (!requests[slot].Due(RequestMilliseconds) && !waiting)
        {
            return;
        }

        entries[slot] = news.Request(category, gameData.LodestoneLocale(), false);
    }

    private static NewsCategory CategoryOf(string config)
    {
        var value = WidgetConfig.Get(config, CategoryKey);
        for (var index = 0; index < CategoryChoices.Length; index++)
        {
            if (string.Equals(CategoryChoices[index].Value, value, StringComparison.Ordinal))
            {
                return NewsCategories.All[index];
            }
        }

        return NewsCategory.Topics;
    }

    private static string Caption(ref CachedText cache, LodestoneNewsItem item, NewsCategory category)
    {
        if (category == NewsCategory.Maintenance && item.Start is { } start && item.End is { } end)
        {
            var windowKey = start.UtcTicks ^ (end.UtcTicks << 1);
            return cache.IsCurrent(windowKey) ? cache.Value : cache.Store(windowKey, NewsFormat.Window(start, end));
        }

        var minutes = (long)(DateTimeOffset.UtcNow - item.Time).TotalMinutes;
        var key = item.Time.UtcTicks / TimeSpan.TicksPerMinute * 1_000_003L + minutes;
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, TimeText.Ago(item.Time));
    }

    private string SampleCaption(int index)
    {
        var minutes = SampleAgesMinutes[index % SampleAgesMinutes.Length];
        ref var cache = ref sampleCaptions[index];
        return cache.IsCurrent(minutes)
            ? cache.Value
            : cache.Store(minutes, TimeText.Ago(DateTime.UtcNow.AddMinutes(-minutes)));
    }

    public void Dispose()
    {
    }
}
