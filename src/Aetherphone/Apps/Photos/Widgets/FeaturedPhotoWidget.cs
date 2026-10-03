using System.Collections.Concurrent;
using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Photos.Widgets;

internal sealed class FeaturedPhotoWidget : IHomeWidget
{
    private const int ListRefreshMilliseconds = 20000;
    private const int EmptyRefreshMilliseconds = 5000;
    private const int EvictMilliseconds = 5000;
    private const long StaleMilliseconds = 4000;
    private const long SlideMilliseconds = 16000;
    private const long FadeMilliseconds = 1200;
    private const int ThumbnailMaxDimension = 256;
    private const float ZoomNear = 1.06f;
    private const float ZoomFar = 1.18f;
    private const float ScrimFraction = 0.46f;
    private const float ScrimAlpha = 0.55f;
    private const string NamePrefix = "AEP_";
    private const string NameStamp = "yyyyMMdd";
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 YearInk = new(1f, 1f, 1f, 0.78f);
    private static readonly Vector4 SampleSkyTop = new(0.16f, 0.22f, 0.46f, 1f);
    private static readonly Vector4 SampleSkyBottom = new(0.98f, 0.62f, 0.42f, 1f);
    private static readonly Vector4 SampleSun = new(1f, 0.86f, 0.58f, 1f);
    private static readonly Vector4 SampleSea = new(0.14f, 0.20f, 0.36f, 1f);
    private static readonly DateTime SampleDate = new(2026, 6, 21);

    private sealed class Entry
    {
        public readonly IDalamudTextureWrap Wrap;
        public long LastUsed;

        public Entry(IDalamudTextureWrap wrap, long lastUsed)
        {
            Wrap = wrap;
            LastUsed = lastUsed;
        }
    }

    private sealed class Show
    {
        public long StartedAt = -1;
        public int Seed = -1;
        public CachedText Title;
        public CachedText Year;
    }

    private readonly PhotoLibrary library;
    private readonly ConcurrentDictionary<(string Path, int Level), Entry> ready = new();
    private readonly ConcurrentDictionary<(string Path, int Level), byte> loading = new();
    private readonly ConcurrentDictionary<string, byte> failed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> dates = new(StringComparer.Ordinal);
    private readonly List<(string Path, int Level)> evictions = new();
    private readonly WidgetStates<Show> shows = new();
    private readonly CancellationTokenSource cancellation = new();
    private CachedText sampleTitle;
    private CachedText sampleYear;
    private volatile bool disposed;
    private string[] paths = Array.Empty<string>();
    private WidgetRefresh listCadence;
    private WidgetRefresh evictCadence;

    public FeaturedPhotoWidget(PhotoLibrary library)
    {
        this.library = library;
    }

    public string Id => "photos.shuffle";
    public string DisplayName => Loc.T(L.WidgetsLife.FeaturedName);
    public string Description => Loc.T(L.WidgetsLife.FeaturedDescription);
    public string AppId => "photos";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public void Draw(in WidgetContext context)
    {
        RefreshList();
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        if (context.Opacity <= 0f)
        {
            return;
        }

        if (paths.Length == 0)
        {
            if (context.Preview)
            {
                DrawSample(context, ink);
                return;
            }

            DrawEmpty(context, ink);
            return;
        }

        var show = shows.For(context.InstanceKey);
        var now = Environment.TickCount64;
        if (show.StartedAt < 0)
        {
            show.StartedAt = now;
            show.Seed = (context.InstanceKey.GetHashCode() & int.MaxValue) % paths.Length;
        }

        var elapsed = now - show.StartedAt;
        var slide = elapsed / SlideMilliseconds;
        var intoSlide = elapsed - slide * SlideMilliseconds;
        var level = TextureSizes.LevelFor(MathF.Max(context.Bounds.Width, context.Bounds.Height) * ZoomFar);
        var currentIndex = IndexFor(show, slide);
        var current = Get(paths[currentIndex], level, now);
        var radius = WidgetChrome.Radius(context.Scale);
        var fading = slide > 0 && intoSlide < FadeMilliseconds && paths.Length > 1;
        if (fading)
        {
            var previous = Get(paths[IndexFor(show, slide - 1)], level, now);
            if (previous is not null)
            {
                DrawPhoto(context, previous, radius, ink.ImageTint, slide - 1, intoSlide + SlideMilliseconds);
            }
        }

        if (intoSlide > SlideMilliseconds - FadeMilliseconds * 2 && paths.Length > 1)
        {
            Get(paths[IndexFor(show, slide + 1)], level, now);
        }

        if (current is null)
        {
            if (!fading)
            {
                WidgetChrome.Redacted(context.DrawList, context.Bounds, ink, radius);
            }
        }
        else
        {
            var alpha = fading ? Math.Clamp(intoSlide / (float)FadeMilliseconds, 0f, 1f) : 1f;
            var tint = ink.ImageTint;
            DrawPhoto(context, current, radius, tint with { W = tint.W * alpha }, slide, intoSlide);
        }

        DrawCaption(context, ink, show, paths[currentIndex]);
        WidgetChrome.Edge(context);
        Evict(now);
    }

    private void RefreshList()
    {
        if (!listCadence.Due(paths.Length == 0 ? EmptyRefreshMilliseconds : ListRefreshMilliseconds))
        {
            return;
        }

        paths = library.List();
    }

    private int IndexFor(Show show, long slide)
    {
        var count = paths.Length;
        var offset = (int)(slide % count);
        return (show.Seed % count + offset) % count;
    }

    private static void DrawPhoto(in WidgetContext context, IDalamudTextureWrap wrap, float radius, Vector4 tint,
        long slide, long intoSlide)
    {
        var bounds = context.Bounds;
        var (baseMin, baseMax) = ImageFit.Cover(wrap.Width, wrap.Height, bounds.Width, bounds.Height);
        var progress = Math.Clamp(intoSlide / (float)(SlideMilliseconds + FadeMilliseconds), 0f, 1f);
        var eased = progress * progress * (3f - 2f * progress);
        var zoomIn = (slide & 1) == 0;
        var zoom = zoomIn ? ZoomNear + (ZoomFar - ZoomNear) * eased : ZoomFar - (ZoomFar - ZoomNear) * eased;
        var angle = (slide * 2.399963f) % MathF.Tau;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var baseSize = baseMax - baseMin;
        var window = baseSize / zoom;
        var slack = (baseSize - window) * 0.5f;
        var travel = eased * 2f - 1f;
        var center = (baseMin + baseMax) * 0.5f + slack * direction * travel;
        var uv0 = center - window * 0.5f;
        var uv1 = center + window * 0.5f;
        Squircle.FillImage(context.DrawList, bounds.Min, bounds.Max, radius, wrap.Handle, ImGui.GetColorU32(tint), uv0,
            uv1);
    }

    private void DrawCaption(in WidgetContext context, in WidgetInk ink, Show show, string path)
    {
        var bounds = context.Bounds;
        var scrimTop = bounds.Max.Y - bounds.Height * ScrimFraction;
        Squircle.FillVerticalGradient(context.DrawList, new Vector2(bounds.Min.X, scrimTop), bounds.Max,
            WidgetChrome.Radius(context.Scale), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f)),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, ScrimAlpha * context.Opacity)));
        var date = DateOf(path);
        var key = date.Date.Ticks;
        var title = show.Title.IsCurrent(key)
            ? show.Title.Value
            : show.Title.Store(key, date.ToString(Loc.Culture.DateTimeFormat.MonthDayPattern, Loc.Culture));
        var year = show.Year.IsCurrent(key) ? show.Year.Value : show.Year.Store(key, date.Year.ToString(Loc.Culture));
        DrawDate(context, ink, title, year);
    }

    private static void DrawDate(in WidgetContext context, in WidgetInk ink, string title, string year)
    {
        var content = WidgetMetrics.Content(context);
        var titleStyle = context.Size == WidgetSize.Small ? WidgetType.Headline : WidgetType.Title;
        var primary = ink.KeepsOwnColors || ink.Mode == WidgetMode.Dark ? ink.Fade(White) : ink.Primary;
        var secondary = ink.KeepsOwnColors || ink.Mode == WidgetMode.Dark ? ink.Fade(YearInk) : ink.Secondary;
        var yearHeight = Typography.Measure(year, WidgetType.Caption).Y;
        var yearTop = content.Max.Y - yearHeight;
        WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, yearTop), year, secondary, WidgetType.Caption,
            content.Width);
        var titleHeight = Typography.Measure(title, titleStyle).Y;
        WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, yearTop - titleHeight), title, primary,
            titleStyle, content.Width);
    }

    private DateTime DateOf(string path)
    {
        if (dates.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var name = Path.GetFileName(path.AsSpan());
        var date = DateTime.MinValue;
        if (name.StartsWith(NamePrefix, StringComparison.Ordinal) && name.Length >= NamePrefix.Length + NameStamp.Length &&
            DateTime.TryParseExact(name.Slice(NamePrefix.Length, NameStamp.Length), NameStamp,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
        }
        else
        {
            try
            {
                date = File.GetLastWriteTime(path);
            }
            catch (IOException)
            {
                date = DateTime.Today;
            }
        }

        dates[path] = date;
        return date;
    }

    private void DrawSample(in WidgetContext context, in WidgetInk ink)
    {
        var bounds = context.Bounds;
        var drawList = context.DrawList;
        var radius = WidgetChrome.Radius(context.Scale);
        Squircle.FillVerticalGradient(drawList, bounds.Min, bounds.Max, radius,
            ImGui.GetColorU32(Mapped(ink, SampleSkyTop)), ImGui.GetColorU32(Mapped(ink, SampleSkyBottom)));
        var horizon = bounds.Min.Y + bounds.Height * 0.68f;
        var sunRadius = MathF.Min(bounds.Width, bounds.Height) * 0.16f;
        var sunCenter = new Vector2(bounds.Min.X + bounds.Width * 0.62f, horizon - sunRadius * 0.35f);
        drawList.AddCircleFilled(sunCenter, sunRadius * 1.9f, ImGui.GetColorU32(Mapped(ink, SampleSun) with
        {
            W = 0.18f * ink.Opacity,
        }), 48);
        drawList.AddCircleFilled(sunCenter, sunRadius, ImGui.GetColorU32(Mapped(ink, SampleSun)), 48);
        Squircle.FillCap(drawList, new Vector2(bounds.Min.X, horizon), bounds.Max, radius,
            ImGui.GetColorU32(Mapped(ink, SampleSea)), false);
        var title = sampleTitle.IsCurrent(SampleDate.Ticks)
            ? sampleTitle.Value
            : sampleTitle.Store(SampleDate.Ticks,
                SampleDate.ToString(Loc.Culture.DateTimeFormat.MonthDayPattern, Loc.Culture));
        var year = sampleYear.IsCurrent(SampleDate.Year)
            ? sampleYear.Value
            : sampleYear.Store(SampleDate.Year, SampleDate.Year.ToString(Loc.Culture));
        var bottom = bounds.Max.Y - bounds.Height * ScrimFraction;
        Squircle.FillVerticalGradient(drawList, new Vector2(bounds.Min.X, bottom), bounds.Max, radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f)),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, ScrimAlpha * 0.6f * ink.Opacity)));
        DrawDate(context, ink, title, year);
        WidgetChrome.Edge(context);
    }

    private static Vector4 Mapped(in WidgetInk ink, Vector4 color) =>
        ink.KeepsOwnColors ? ink.Fade(color) : ink.Accent(color);

    private static void DrawEmpty(in WidgetContext context, in WidgetInk ink)
    {
        var content = WidgetMetrics.Content(context);
        var glyph = WidgetMetrics.GlyphSmall * 1.6f * context.Scale;
        AppIconTile.TryDrawGlyph(context.DrawList, "photos", new Vector2(content.Min.X + glyph * 0.5f,
            content.Min.Y + glyph * 0.5f), glyph, ink.Accent(AppAccents.For("photos")));
        WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, content.Min.Y + glyph + WidgetMetrics.Gutter * context.Scale), Loc.T(L.Photos.NoPhotos),
            Loc.T(L.Photos.UseCameraHint));
    }

    private IDalamudTextureWrap? Get(string path, int level, long now)
    {
        if (ready.TryGetValue((path, level), out var entry))
        {
            entry.LastUsed = now;
            return entry.Wrap;
        }

        if (!failed.ContainsKey(path) && loading.TryAdd((path, level), 0))
        {
            _ = LoadAsync(path, level);
        }

        for (var fallback = TextureSizes.LevelCount; fallback >= 1; fallback--)
        {
            if (fallback != level && ready.TryGetValue((path, fallback), out var nearest))
            {
                nearest.LastUsed = now;
                return nearest.Wrap;
            }
        }

        return null;
    }

    private async Task LoadAsync(string path, int level)
    {
        try
        {
            var token = cancellation.Token;
            var size = TextureSizes.SizeOf(level);
            var thumbnail = library.ThumbnailPathFor(path);
            var useThumbnail = size <= ThumbnailMaxDimension && File.Exists(thumbnail) &&
                               File.GetLastWriteTimeUtc(thumbnail) >= File.GetLastWriteTimeUtc(path);
            var bytes = await File.ReadAllBytesAsync(useThumbnail ? thumbnail : path, token).ConfigureAwait(false);
            var wrap = await ImageProcessor.DecodeToTextureAsync(Plugin.TextureProvider, bytes, "widget:" + path,
                ImageProcessor.MaxLocalDecodePixels, size, token).ConfigureAwait(false);
            if (!ready.TryAdd((path, level), new Entry(wrap, Environment.TickCount64)))
            {
                wrap.Dispose();
                return;
            }

            if (disposed && ready.TryRemove((path, level), out var orphan))
            {
                orphan.Wrap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            failed.TryAdd(path, 0);
            AepLog.Warning(exception, $"[FeaturedPhotoWidget] failed to load {Path.GetFileName(path)}");
        }
        finally
        {
            loading.TryRemove((path, level), out _);
        }
    }

    private void Evict(long now)
    {
        if (!evictCadence.Due(EvictMilliseconds))
        {
            return;
        }

        evictions.Clear();
        foreach (var pair in ready)
        {
            if (now - pair.Value.LastUsed > StaleMilliseconds)
            {
                evictions.Add(pair.Key);
            }
        }

        for (var index = 0; index < evictions.Count; index++)
        {
            if (ready.TryRemove(evictions[index], out var entry))
            {
                entry.Wrap.Dispose();
            }
        }
    }

    public void Dispose()
    {
        disposed = true;
        cancellation.Cancel();
        foreach (var pair in ready)
        {
            if (ready.TryRemove(pair.Key, out var entry))
            {
                entry.Wrap.Dispose();
            }
        }
    }
}
