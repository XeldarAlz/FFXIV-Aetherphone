using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.AetherStream.Widgets;

internal sealed class NowWatchingWidget : IHomeWidget
{
    private const string StreamAppId = "aetherstream";
    private const float AspectRatio = 9f / 16f;
    private const float MediumThumbnailFraction = 0.46f;
    private const float ColumnGap = 12f;
    private const float SmallControl = 30f;
    private const float MediumControl = 36f;
    private const float OverlayInset = 6f;
    private const float ProgressHeight = 4f;
    private const float PlaceholderGlyphFraction = 0.32f;
    private const float SampleFraction = 0.42f;
    private const int SampleSeconds = 1520;
    private const int PlayControlId = 1;

    private static readonly Vector4 SampleTop = new(0.98f, 0.62f, 0.3f, 1f);
    private static readonly Vector4 SampleBottom = new(0.86f, 0.3f, 0.42f, 1f);

    private readonly struct Clip
    {
        public readonly Guid EntryId;
        public readonly string Title;
        public readonly string Subtitle;
        public readonly string ThumbnailKey;
        public readonly string? FallbackUrl;
        public readonly Func<CancellationToken, Task<byte[]?>>? Source;

        public Clip(Guid entryId, string title, string subtitle, string thumbnailKey, string? fallbackUrl,
            Func<CancellationToken, Task<byte[]?>>? source)
        {
            EntryId = entryId;
            Title = title;
            Subtitle = subtitle;
            ThumbnailKey = thumbnailKey;
            FallbackUrl = fallbackUrl;
            Source = source;
        }

        public bool IsEmpty => EntryId == Guid.Empty;
    }

    private readonly VideoSuite video;
    private readonly RemoteImageCache images;
    private readonly HttpService http;
    private Clip current;
    private Clip previous;
    private float swap = 1f;
    private bool queued;
    private WidgetFrame frame;
    private CachedText elapsedText;
    private CachedText remainingText;

    public NowWatchingWidget(VideoSuite video, RemoteImageCache images, HttpService http)
    {
        this.video = video;
        this.images = images;
        this.http = http;
    }

    public string Id => "aetherstream.watching";
    public string DisplayName => Loc.T(L.WidgetsPeople.NowWatching);
    public string Description => Loc.T(L.WidgetsPeople.NowWatchingDescription);
    public string AppId => StreamAppId;
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public float Relevance(string config)
    {
        var player = video.Player;
        if (player.State == VideoPlaybackState.Playing && !player.Progress.Paused)
        {
            return 0.85f;
        }

        return player.HasMedia ? 0.4f : 0f;
    }

    public void Draw(in WidgetContext context)
    {
        if (frame.First())
        {
            Sync(context.Delta);
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var upNext = queued && !current.IsEmpty;
        var watching = !queued && !current.IsEmpty;
        var sample = context.Preview && current.IsEmpty;
        var content = WidgetMetrics.Content(context);
        if (!watching && !upNext && !sample)
        {
            WidgetChrome.Message(context, ink, content, StreamAppId, Loc.T(L.WidgetsPeople.NothingPlaying),
                context.Size == WidgetSize.Small ? string.Empty : Loc.T(L.WidgetsPeople.WatchEmptyHint));
            return;
        }

        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, content, sample, upNext);
            return;
        }

        DrawMedium(context, ink, content, sample, upNext);
    }

    private void Sync(float delta)
    {
        var entry = video.Queue.Current;
        queued = false;
        if (entry is null && !video.Player.HasMedia)
        {
            var entries = video.Queue.Entries;
            entry = entries.Count > 0 ? entries[0] : null;
            queued = entry is not null;
        }

        var entryId = entry?.Id ?? Guid.Empty;
        if (entryId != current.EntryId)
        {
            previous = current;
            current = entry is null ? default : Build(entry);
            swap = previous.IsEmpty || current.IsEmpty ? 1f : 0f;
        }
        else if (entry is not null && !ReferenceEquals(entry.Title ?? string.Empty, current.Title))
        {
            current = Build(entry);
        }

        swap = PeopleWidgetChrome.Step(swap, delta);
    }

    private Clip Build(VideoQueueEntry entry)
    {
        var source = VideoThumbnailResolver.Source(http, entry.Url, out var key);
        return new Clip(entry.Id, entry.Title ?? string.Empty, entry.Subtitle ?? string.Empty, key, entry.ThumbnailUrl,
            source);
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, Rect content, bool sample, bool upNext)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var thumbnail = new Rect(content.Min,
            new Vector2(content.Max.X, content.Min.Y + content.Width * AspectRatio));
        var radius = WidgetMetrics.InnerRadius(context);
        DrawThumbnails(context, ink, thumbnail, radius, sample);

        var controlRadius = SmallControl * 0.5f * scale;
        var inset = OverlayInset * scale;
        DrawPlayButton(context, ink,
            new Vector2(thumbnail.Max.X - inset - controlRadius, thumbnail.Max.Y - inset - controlRadius),
            SmallControl, upNext, sample);

        var barTop = content.Max.Y - ProgressHeight * scale;
        var top = thumbnail.Max.Y + WidgetMetrics.Gutter * scale;
        var width = content.Width;
        var title = sample ? Loc.T(L.WidgetsPeople.SampleVideoTitle) : current.Title;
        var subtitle = Subtitle(sample, upNext);
        var titleHeight = Typography.Measure("A", WidgetType.Headline).Y;
        var subtitleHeight = Typography.Measure("A", WidgetType.Body).Y;
        Typography.Draw(drawList, new Vector2(content.Min.X, top),
            Typography.FitText(title, width, WidgetType.Headline), ink.Primary, WidgetType.Headline);
        var subtitleTop = top + titleHeight + WidgetMetrics.RowGap * scale;
        if (subtitleTop + subtitleHeight <= barTop - WidgetMetrics.RowGap * scale)
        {
            Typography.Draw(drawList, new Vector2(content.Min.X, subtitleTop),
                Typography.FitText(subtitle, width, WidgetType.Body), ink.Secondary, WidgetType.Body);
        }

        if (upNext)
        {
            return;
        }

        WidgetChrome.Bar(context.DrawList, new Rect(new Vector2(content.Min.X, barTop), content.Max), Fraction(sample),
            ink.Fill, ink.Primary);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, Rect content, bool sample, bool upNext)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var thumbnailWidth = content.Width * MediumThumbnailFraction;
        var controlDiameter = MediumControl * scale;
        var thumbnailHeight = MathF.Min(thumbnailWidth * AspectRatio,
            content.Height - controlDiameter - WidgetMetrics.Gutter * scale);
        var thumbnail = new Rect(content.Min, content.Min + new Vector2(thumbnailHeight / AspectRatio, thumbnailHeight));
        var radius = WidgetMetrics.InnerRadius(context);
        DrawThumbnails(context, ink, thumbnail, radius, sample);

        var columnLeft = thumbnail.Max.X + ColumnGap * scale;
        var width = MathF.Max(1f, content.Max.X - columnLeft);
        var top = content.Min.Y;
        var eyebrow = upNext ? Loc.T(L.WidgetsPeople.UpNext) : Loc.T(L.WidgetsPeople.NowWatching);
        WidgetText.EyebrowFit(drawList, new Vector2(columnLeft, top), eyebrow, width,
            ink.Accent(AppAccents.For(StreamAppId)), scale);
        top += WidgetText.EyebrowHeight() + WidgetMetrics.RowGap * 2f * scale;
        DrawTitles(context, ink, columnLeft, top, width, sample, upNext);

        var controlCenter = new Vector2(content.Max.X - controlDiameter * 0.5f, content.Max.Y - controlDiameter * 0.5f);
        DrawPlayButton(context, ink, controlCenter, MediumControl, upNext, sample);
        if (upNext)
        {
            return;
        }

        var barRight = controlCenter.X - controlDiameter * 0.5f - WidgetMetrics.Gutter * 1.5f * scale;
        var captionHeight = Typography.Measure("0", WidgetType.Caption).Y;
        var barTop = controlCenter.Y - (ProgressHeight * scale + WidgetMetrics.RowGap * scale + captionHeight) * 0.5f;
        var bar = new Rect(new Vector2(content.Min.X, barTop),
            new Vector2(barRight, barTop + ProgressHeight * scale));
        WidgetChrome.Bar(context.DrawList, bar, Fraction(sample), ink.Fill, ink.Primary);
        var captionTop = bar.Max.Y + WidgetMetrics.RowGap * scale;
        var (position, duration) = Times(sample);
        var elapsed = WidgetText.Seconds(ref elapsedText, TimeSpan.FromSeconds(position));
        WidgetText.Tabular(drawList, new Vector2(bar.Min.X, captionTop), elapsed, ink.Secondary, WidgetType.Caption);
        if (duration <= 0)
        {
            return;
        }

        var remaining = Remaining(Math.Max(0, duration - position));
        var remainingWidth = WidgetText.TabularWidth(remaining, WidgetType.Caption);
        WidgetText.Tabular(drawList, new Vector2(bar.Max.X - remainingWidth, captionTop), remaining, ink.Secondary,
            WidgetType.Caption);
    }

    private void DrawTitles(in WidgetContext context, in WidgetInk ink, float left, float top, float width,
        bool sample, bool upNext)
    {
        var drawList = context.DrawList;
        if (!sample && swap < 1f && !previous.IsEmpty)
        {
            var previousAlpha = 1f - swap;
            var previousBottom = top + WidgetText.Draw(drawList, new Vector2(left, top), previous.Title,
                Faded(ink.Primary, previousAlpha), WidgetType.Title, width);
            WidgetText.Draw(drawList, new Vector2(left, previousBottom + WidgetMetrics.RowGap * context.Scale),
                previous.Subtitle, Faded(ink.Secondary, previousAlpha), WidgetType.Body, width);
        }

        var alpha = sample ? 1f : swap;
        var title = sample ? Loc.T(L.WidgetsPeople.SampleVideoTitle) : current.Title;
        var bottom = top + WidgetText.Draw(drawList, new Vector2(left, top), title, Faded(ink.Primary, alpha),
            WidgetType.Title, width);
        WidgetText.Draw(drawList, new Vector2(left, bottom + WidgetMetrics.RowGap * context.Scale),
            Subtitle(sample, upNext), Faded(ink.Secondary, alpha), WidgetType.Body, width);
    }

    private void DrawThumbnails(in WidgetContext context, in WidgetInk ink, Rect rect, float radius, bool sample)
    {
        if (sample)
        {
            Squircle.FillVerticalGradient(context.DrawList, rect.Min, rect.Max, radius,
                ImGui.GetColorU32(ink.Accent(SampleTop)), ImGui.GetColorU32(ink.Accent(SampleBottom)));
            AppIconTile.TryDrawGlyph(context.DrawList, StreamAppId, rect.Center, rect.Height * PlaceholderGlyphFraction,
                ink.OnAccent);
            return;
        }

        if (swap < 1f && !previous.IsEmpty)
        {
            DrawThumbnail(context, ink, rect, radius, previous, 1f);
        }

        DrawThumbnail(context, ink, rect, radius, current, swap);
    }

    private void DrawThumbnail(in WidgetContext context, in WidgetInk ink, Rect rect, float radius, in Clip clip,
        float alpha)
    {
        var drawList = context.DrawList;
        var texture = Texture(clip);
        if (texture is null)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Faded(ink.Fill, alpha)));
            AppIconTile.TryDrawGlyph(drawList, StreamAppId, rect.Center, rect.Height * PlaceholderGlyphFraction,
                Faded(ink.Tertiary, alpha));
            return;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Width, texture.Height, rect.Width, rect.Height);
        Squircle.FillImage(drawList, rect.Min, rect.Max, radius, texture.Handle,
            ImGui.GetColorU32(Faded(ink.ImageTint, alpha)), uv0, uv1);
    }

    private IDalamudTextureWrap? Texture(in Clip clip)
    {
        if (clip.Source is not null)
        {
            return images.GetKeyed(clip.ThumbnailKey, clip.Source);
        }

        return string.IsNullOrEmpty(clip.FallbackUrl) ? null : images.Get(clip.FallbackUrl);
    }

    private void DrawPlayButton(in WidgetContext context, in WidgetInk ink, Vector2 center, float diameter,
        bool upNext, bool sample)
    {
        var watchAlong = video.WatchAlong;
        if (!sample && watchAlong.IsViewing && !watchAlong.CanControlPlayback)
        {
            return;
        }

        var player = video.Player;
        var paused = player.Progress.Paused || player.State != VideoPlaybackState.Playing;
        var icon = upNext || paused && !sample ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;
        if (!WidgetControls.Button(context, ink, PlayControlId, center, diameter, icon, AppAccents.For(StreamAppId)))
        {
            return;
        }

        if (upNext)
        {
            video.Queue.Advance();
            return;
        }

        if (watchAlong.IsViewing)
        {
            watchAlong.ControlPause(!player.Progress.Paused);
            return;
        }

        if (player.HasMedia)
        {
            player.Pause(!player.Progress.Paused);
        }
    }

    private string Subtitle(bool sample, bool upNext)
    {
        if (sample)
        {
            return Loc.T(L.WidgetsPeople.SampleVideoSource);
        }

        if (!upNext && video.Player.State == VideoPlaybackState.Loading)
        {
            return Loc.T(L.Common.Loading);
        }

        return current.Subtitle;
    }

    private float Fraction(bool sample) => sample ? SampleFraction : video.Player.Progress.Fraction;

    private (int Position, int Duration) Times(bool sample)
    {
        if (sample)
        {
            return ((int)(SampleSeconds * SampleFraction), SampleSeconds);
        }

        var progress = video.Player.Progress;
        return ((int)MathF.Max(0f, progress.Position), (int)MathF.Max(0f, progress.Duration));
    }

    private string Remaining(int seconds) =>
        remainingText.IsCurrent(seconds)
            ? remainingText.Value
            : remainingText.Store(seconds, string.Concat("-", TimeText.MinutesSeconds(seconds)));

    private static Vector4 Faded(Vector4 color, float alpha) => color with { W = color.W * alpha };

    public void Dispose()
    {
    }
}
