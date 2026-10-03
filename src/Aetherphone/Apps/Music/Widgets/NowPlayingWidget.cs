using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music.Widgets;

internal sealed class NowPlayingWidget : IHomeWidget
{
    private const string MusicAppId = "music";
    private const float RecentsRefreshSeconds = 5f;
    private const int RecentCapacity = 4;
    private const float SmallArtworkFraction = 0.52f;
    private const float ColumnGap = 12f;
    private const float SmallControl = 32f;
    private const float SkipControl = 32f;
    private const float PlayControl = 40f;
    private const float ProgressHeight = 4f;
    private const float RecentArtwork = 34f;
    private const float LargeHeroFraction = 0.42f;
    private const float PlaceholderGlyphFraction = 0.4f;
    private const float SampleProgress = 0.38f;
    private const int PlayControlId = 1;
    private const int PreviousControlId = 2;
    private const int NextControlId = 3;
    private const int RecentLinkBase = 10;

    private static readonly Vector4 SampleWash = new(0.55f, 0.24f, 0.62f, 1f);
    private static readonly Vector4 SampleArtTop = new(0.93f, 0.42f, 0.55f, 1f);
    private static readonly Vector4 SampleArtBottom = new(0.42f, 0.2f, 0.66f, 1f);
    private static readonly string[] SampleTitles = { "Answers", "Dragonsong", "Flow" };
    private static readonly string[] SampleArtists = { "Susan Calloway", "Susan Calloway", "Amanda Achen" };

    private readonly struct Track
    {
        public readonly string Title;
        public readonly string Subtitle;
        public readonly string ArtworkUrl;
        public readonly Func<CancellationToken, Task<byte[]?>>? Source;

        public Track(string title, string subtitle, string artworkUrl, Func<CancellationToken, Task<byte[]?>>? source)
        {
            Title = title;
            Subtitle = subtitle;
            ArtworkUrl = artworkUrl;
            Source = source;
        }

        public bool IsEmpty => Title.Length == 0 && ArtworkUrl.Length == 0;
    }

    private readonly PlaybackHub playback;
    private readonly LibraryStore library;
    private readonly MediaCache media;
    private readonly HttpService http;
    private readonly ArtworkWash wash;
    private readonly Vector4 defaultWash;
    private Track current = new(string.Empty, string.Empty, string.Empty, null);
    private Track previous = new(string.Empty, string.Empty, string.Empty, null);
    private Track[] recents = Array.Empty<Track>();
    private Song[] recentSongs = Array.Empty<Song>();
    private float swap = 1f;
    private bool washResolved;
    private Vector4 washFrom;
    private Vector4 washTo;
    private float washBlend = 1f;
    private float sinceRecents = RecentsRefreshSeconds;
    private WidgetFrame frame;

    public NowPlayingWidget(PlaybackHub playback, LibraryStore library, MediaCache media, HttpService http)
    {
        this.playback = playback;
        this.library = library;
        this.media = media;
        this.http = http;
        wash = new ArtworkWash(media);
        defaultWash = AppAccents.For(MusicAppId);
        washFrom = defaultWash;
        washTo = defaultWash;
    }

    public string Id => "music.nowplaying";
    public string DisplayName => Loc.T(L.WidgetsPeople.NowPlaying);
    public string Description => Loc.T(L.WidgetsPeople.NowPlayingDescription);
    public string AppId => MusicAppId;
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config) => playback.IsPlaying ? 0.9f : playback.IsActive ? 0.4f : 0f;

    public void Draw(in WidgetContext context)
    {
        if (frame.First())
        {
            Sync(context.Delta);
        }

        var active = playback.IsActive;
        var sample = context.Preview && !active;
        var idle = !active && !sample && recents.Length > 0;
        var washColor = sample ? SampleWash : Vector4.Lerp(washFrom, washTo, washBlend);
        WidgetChrome.Container(context, ArtworkWash.Top(washColor), ArtworkWash.Bottom(washColor));
        var ink = context.Mode is WidgetMode.FullColor or WidgetMode.Dark
            ? WidgetInk.OnImage(context)
            : WidgetInk.From(context);
        if (!active && !sample && !idle)
        {
            WidgetChrome.Message(context, ink, WidgetMetrics.Content(context), MusicAppId,
                Loc.T(L.WidgetsPeople.NotPlaying),
                context.Size == WidgetSize.Small ? string.Empty : Loc.T(L.WidgetsPeople.MusicEmptyHint));
            return;
        }

        var content = WidgetMetrics.Content(context);
        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, content, sample, idle);
            return;
        }

        if (context.Size == WidgetSize.Medium)
        {
            DrawHero(context, ink, content, sample, idle);
            return;
        }

        var heroBottom = content.Min.Y + content.Height * LargeHeroFraction;
        DrawHero(context, ink, new Rect(content.Min, new Vector2(content.Max.X, heroBottom)), sample, idle);
        DrawRecents(context, ink, new Rect(new Vector2(content.Min.X, heroBottom), content.Max), sample);
    }

    private void Sync(float delta)
    {
        sinceRecents += delta;
        if (sinceRecents >= RecentsRefreshSeconds)
        {
            sinceRecents = 0f;
            RefreshRecents();
        }

        var active = playback.IsActive;
        var title = (active ? playback.Title : recents.Length > 0 ? recents[0].Title : null) ?? string.Empty;
        var artwork = (active ? ArtworkUrl() : recents.Length > 0 ? recents[0].ArtworkUrl : null) ?? string.Empty;
        var subtitle = (active ? playback.Subtitle : recents.Length > 0 ? recents[0].Subtitle : null) ?? string.Empty;
        if (!string.Equals(title, current.Title, StringComparison.Ordinal)
            || !string.Equals(artwork, current.ArtworkUrl, StringComparison.Ordinal))
        {
            previous = current;
            current = new Track(title, subtitle, artwork, SourceFor(artwork));
            swap = previous.IsEmpty ? 1f : 0f;
            washResolved = false;
        }
        else if (!ReferenceEquals(subtitle, current.Subtitle))
        {
            current = new Track(current.Title, subtitle, current.ArtworkUrl, current.Source);
        }

        swap = PeopleWidgetChrome.Step(swap, delta);
        ResolveWash();
        washBlend = PeopleWidgetChrome.Step(washBlend, delta);
    }

    private void ResolveWash()
    {
        if (washResolved)
        {
            return;
        }

        var url = current.ArtworkUrl;
        if (url.Length == 0 || current.Source is null || wash.Failed(url))
        {
            Retarget(defaultWash);
            return;
        }

        if (wash.TryGet(url, current.Source, out var color))
        {
            Retarget(color);
        }
    }

    private void Retarget(Vector4 color)
    {
        washResolved = true;
        washFrom = Vector4.Lerp(washFrom, washTo, washBlend);
        washTo = color;
        washBlend = 0f;
    }

    private string ArtworkUrl() =>
        playback.SongActive ? playback.Songs.CurrentThumbnail : playback.Radio.CurrentStationInfo.ArtworkUrl ?? string.Empty;

    private Func<CancellationToken, Task<byte[]?>>? SourceFor(string url)
    {
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return token => http.GetBytesAsync(uri, token);
    }

    private void RefreshRecents()
    {
        var songs = library.RecentlyPlayed(RecentCapacity + 1);
        if (SameSongs(songs))
        {
            return;
        }

        recentSongs = songs;
        var tracks = new Track[songs.Length];
        for (var index = 0; index < songs.Length; index++)
        {
            var song = songs[index];
            tracks[index] = new Track(song.Title ?? string.Empty, song.Author ?? string.Empty,
                song.ThumbnailUrl ?? string.Empty, SourceFor(song.ThumbnailUrl ?? string.Empty));
        }

        recents = tracks;
    }

    private bool SameSongs(Song[] songs)
    {
        if (songs.Length != recentSongs.Length)
        {
            return false;
        }

        for (var index = 0; index < songs.Length; index++)
        {
            if (!string.Equals(songs[index].VideoId, recentSongs[index].VideoId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, Rect content, bool sample, bool idle)
    {
        var scale = context.Scale;
        var side = MathF.Min(content.Height, content.Width) * SmallArtworkFraction;
        var artwork = new Rect(content.Min, content.Min + new Vector2(side, side));
        var radius = WidgetMetrics.InnerRadius(context);
        DrawLayers(context, ink, artwork, radius, sample, content, string.Empty);
        var buttonRadius = SmallControl * 0.5f * scale;
        var center = new Vector2(content.Max.X - buttonRadius, content.Min.Y + buttonRadius);
        DrawPlayButton(context, ink, center, SmallControl, idle);
    }

    private static void DrawSmallText(in WidgetContext context, in WidgetInk ink, Rect content, in Track track,
        float alpha)
    {
        var drawList = context.DrawList;
        var width = content.Width;
        var subtitleHeight = Typography.Measure("A", WidgetType.Body).Y;
        var titleHeight = Typography.Measure("A", WidgetType.Headline).Y;
        var subtitleTop = content.Max.Y - subtitleHeight;
        var titleTop = subtitleTop - WidgetMetrics.RowGap * context.Scale - titleHeight;
        Typography.Draw(drawList, new Vector2(content.Min.X, titleTop),
            Typography.FitText(track.Title, width, WidgetType.Headline), Faded(ink.Primary, alpha),
            WidgetType.Headline);
        Typography.Draw(drawList, new Vector2(content.Min.X, subtitleTop),
            Typography.FitText(track.Subtitle, width, WidgetType.Body), Faded(ink.Secondary, alpha), WidgetType.Body);
    }

    private void DrawHero(in WidgetContext context, in WidgetInk ink, Rect area, bool sample, bool idle)
    {
        var scale = context.Scale;
        var side = MathF.Min(area.Height, area.Width * 0.5f);
        var artwork = new Rect(area.Min, area.Min + new Vector2(side, side));
        var columnLeft = artwork.Max.X + ColumnGap * scale;
        var column = new Rect(new Vector2(columnLeft, area.Min.Y), area.Max);
        var radius = WidgetMetrics.InnerRadius(context);
        DrawLayers(context, ink, artwork, radius, sample, column, Eyebrow(sample, idle));

        var controlCenterY = area.Min.Y + side - PlayControl * 0.5f * scale;
        var controlsTop = controlCenterY - PlayControl * 0.5f * scale;
        var live = !sample && playback.RadioActive && !playback.SongActive;
        if (!idle && !live)
        {
            var barBottom = controlsTop - WidgetMetrics.Gutter * scale;
            var bar = new Rect(new Vector2(column.Min.X, barBottom - ProgressHeight * scale),
                new Vector2(column.Max.X, barBottom));
            WidgetChrome.Bar(context.DrawList, bar, sample ? SampleProgress : Progress(), ink.Fill, ink.Primary);
        }

        var pitch = column.Width / 3f;
        DrawPlayButton(context, ink, new Vector2(column.Min.X + pitch * 1.5f, controlCenterY), PlayControl, idle);
        if (idle)
        {
            return;
        }

        var canSkip = sample || playback.HasQueue;
        if (WidgetControls.Button(context, ink, PreviousControlId,
                new Vector2(column.Min.X + pitch * 0.5f, controlCenterY), SkipControl, FontAwesomeIcon.Backward,
                default, canSkip))
        {
            playback.Previous();
        }

        if (WidgetControls.Button(context, ink, NextControlId,
                new Vector2(column.Min.X + pitch * 2.5f, controlCenterY), SkipControl, FontAwesomeIcon.Forward,
                default, canSkip))
        {
            playback.Next();
        }
    }

    private static void DrawHeroText(in WidgetContext context, in WidgetInk ink, Rect column, string eyebrow,
        in Track track, float alpha)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var width = MathF.Max(1f, column.Width);
        var top = column.Min.Y;
        WidgetText.EyebrowFit(drawList, new Vector2(column.Min.X, top), eyebrow, width,
            Faded(ink.Secondary, alpha), scale);
        top += WidgetText.EyebrowHeight() + WidgetMetrics.RowGap * 2f * scale;
        top += WidgetText.Draw(drawList, new Vector2(column.Min.X, top), track.Title, Faded(ink.Primary, alpha),
            WidgetType.Title, width);
        top += WidgetMetrics.RowGap * scale;
        WidgetText.Draw(drawList, new Vector2(column.Min.X, top), track.Subtitle, Faded(ink.Secondary, alpha),
            WidgetType.Body, width);
    }

    private void DrawRecents(in WidgetContext context, in WidgetInk ink, Rect area, bool sample)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var top = area.Min.Y + WidgetMetrics.Gutter * scale;
        WidgetChrome.Separator(context, ink, area.Min.X, area.Max.X, top);
        top += WidgetMetrics.Gutter * 1.5f * scale;
        WidgetText.EyebrowFit(drawList, new Vector2(area.Min.X, top), Loc.T(L.WidgetsPeople.RecentlyPlayed),
            area.Width, ink.Secondary, scale);
        top += WidgetText.EyebrowHeight() + WidgetMetrics.RowGap * scale;
        var available = area.Max.Y - top;
        var count = sample ? SampleTitles.Length : CountRecents();
        if (count == 0)
        {
            return;
        }

        var rows = Math.Min(count, PeopleWidgetChrome.RowCount(available, scale, RecentCapacity));
        var rowHeight = MathF.Min(available / rows, PeopleWidgetChrome.RowHeight * 1.2f * scale);
        var shown = 0;
        if (sample)
        {
            for (var index = 0; index < SampleTitles.Length && shown < rows; index++)
            {
                var track = new Track(SampleTitles[index], SampleArtists[index], string.Empty, null);
                DrawRecentRow(context, ink, area, top + shown * rowHeight, rowHeight, shown, track, true);
                shown++;
            }

            return;
        }

        for (var index = 0; index < recents.Length && shown < rows; index++)
        {
            if (IsCurrent(recents[index]))
            {
                continue;
            }

            DrawRecentRow(context, ink, area, top + shown * rowHeight, rowHeight, shown, recents[index], false);
            shown++;
        }
    }

    private void DrawRecentRow(in WidgetContext context, in WidgetInk ink, Rect area, float rowTop, float rowHeight,
        int slot, in Track track, bool sample)
    {
        var scale = context.Scale;
        var row = new Rect(new Vector2(area.Min.X, rowTop), new Vector2(area.Max.X, rowTop + rowHeight));
        WidgetControls.Link(context, ink, RecentLinkBase + slot, row, WidgetRoute.App(MusicAppId));
        var artworkSide = RecentArtwork * scale;
        var radius = WidgetMetrics.InnerRadius(WidgetMetrics.MarginWide + 4f) * scale;
        var artworkMin = new Vector2(area.Min.X, row.Center.Y - artworkSide * 0.5f);
        var artwork = new Rect(artworkMin, artworkMin + new Vector2(artworkSide, artworkSide));
        if (sample)
        {
            DrawSampleArtwork(context, ink, artwork, radius, 1f);
        }
        else
        {
            DrawArtwork(context, ink, artwork, radius, track, 1f);
        }

        PeopleWidgetChrome.Row(context, ink, row, artwork.Max.X + PeopleWidgetChrome.RowTextGap * scale, track.Title,
            track.Subtitle, string.Empty, string.Empty, false, false);
    }

    private bool IsCurrent(in Track track) => string.Equals(track.Title, current.Title, StringComparison.Ordinal);

    private int CountRecents()
    {
        var count = 0;
        for (var index = 0; index < recents.Length; index++)
        {
            if (IsCurrent(recents[index]))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private void DrawLayers(in WidgetContext context, in WidgetInk ink, Rect artwork, float radius, bool sample,
        Rect textArea, string eyebrow)
    {
        if (sample)
        {
            var sampleTrack = new Track(SampleTitles[0], SampleArtists[0], string.Empty, null);
            DrawSampleArtwork(context, ink, artwork, radius, 1f);
            DrawText(context, ink, textArea, eyebrow, sampleTrack, 1f);
            return;
        }

        if (swap < 1f && !previous.IsEmpty)
        {
            DrawArtwork(context, ink, artwork, radius, previous, 1f - swap);
            DrawText(context, ink, textArea, eyebrow, previous, 1f - swap);
        }

        DrawArtwork(context, ink, artwork, radius, current, swap);
        DrawText(context, ink, textArea, eyebrow, current, swap);
    }

    private static void DrawText(in WidgetContext context, in WidgetInk ink, Rect area, string eyebrow,
        in Track track, float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        if (context.Size == WidgetSize.Small)
        {
            DrawSmallText(context, ink, area, track, alpha);
            return;
        }

        DrawHeroText(context, ink, area, eyebrow, track, alpha);
    }

    private void DrawArtwork(in WidgetContext context, in WidgetInk ink, Rect rect, float radius, in Track track,
        float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var drawList = context.DrawList;
        var texture = track.ArtworkUrl.Length > 0 && track.Source is not null
            ? media.GetOrRequest(track.ArtworkUrl, track.Source, rect.Width).Texture
            : null;
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            Squircle.FillImage(drawList, rect.Min, rect.Max, radius, texture.Handle,
                ImGui.GetColorU32(Faded(ink.ImageTint, alpha)), uv0, uv1);
            return;
        }

        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Faded(ink.Fill, alpha)));
        PhoneIcon.Draw(drawList, rect.Center, PhoneIcons.Music, Faded(ink.Secondary, alpha),
            rect.Height * PlaceholderGlyphFraction);
    }

    private static void DrawSampleArtwork(in WidgetContext context, in WidgetInk ink, Rect rect, float radius,
        float alpha)
    {
        var top = ink.Accent(SampleArtTop);
        var bottom = ink.Accent(SampleArtBottom);
        Squircle.FillVerticalGradient(context.DrawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Faded(top, alpha)), ImGui.GetColorU32(Faded(bottom, alpha)));
        PhoneIcon.Draw(context.DrawList, rect.Center, PhoneIcons.Music, Faded(ink.OnAccent, alpha),
            rect.Height * PlaceholderGlyphFraction);
    }

    private void DrawPlayButton(in WidgetContext context, in WidgetInk ink, Vector2 center, float diameter,
        bool idle)
    {
        var playing = playback.IsPlaying || context.Preview && !playback.IsActive;
        var icon = playing && !idle ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play;
        if (!WidgetControls.Button(context, ink, PlayControlId, center, diameter, icon))
        {
            return;
        }

        if (idle)
        {
            if (recentSongs.Length > 0)
            {
                playback.PlaySongs(recentSongs, 0);
            }

            return;
        }

        playback.TogglePlayPause();
    }

    private float Progress()
    {
        var songs = playback.Songs;
        if (!playback.SongActive || songs.Duration <= 0f)
        {
            return 0f;
        }

        return songs.Position / songs.Duration;
    }

    private string Eyebrow(bool sample, bool idle)
    {
        if (sample)
        {
            return Loc.T(L.WidgetsPeople.NowPlaying);
        }

        if (idle)
        {
            return Loc.T(L.WidgetsPeople.NotPlaying);
        }

        return playback.RadioActive && !playback.SongActive
            ? Loc.T(L.WidgetsPeople.Live)
            : Loc.T(L.WidgetsPeople.NowPlaying);
    }

    private static Vector4 Faded(Vector4 color, float alpha) => color with { W = color.W * alpha };

    public void Dispose()
    {
        wash.Dispose();
    }
}
