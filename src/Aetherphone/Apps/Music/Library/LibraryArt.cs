using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Media;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music.Library;

internal readonly struct CoverArt
{
    public static readonly CoverArt None = new(0, string.Empty, string.Empty, string.Empty, string.Empty,
        PlaylistCoverSet.None);

    public readonly int Count;
    public readonly string First;
    public readonly string Second;
    public readonly string Third;
    public readonly string Fourth;
    public readonly PlaylistCoverSet Custom;

    public CoverArt(int count, string first, string second, string third, string fourth, PlaylistCoverSet custom)
    {
        Count = count;
        First = first;
        Second = second;
        Third = third;
        Fourth = fourth;
        Custom = custom;
    }

    public string At(int index) => index switch
    {
        0 => First,
        1 => Second,
        2 => Third,
        _ => Fourth,
    };

    public static CoverArt Of(PlaylistRecord playlist)
    {
        Span<string> picked = [string.Empty, string.Empty, string.Empty, string.Empty];
        var count = LibraryArt.SelectMosaic(playlist.Songs, picked);
        return new CoverArt(count, picked[0], picked[1], picked[2], picked[3],
            PlaylistCoverSet.From(playlist.CoverPath));
    }

    public static CoverArt Of(ReadOnlySpan<Song> songs)
    {
        Span<string> picked = [string.Empty, string.Empty, string.Empty, string.Empty];
        var count = LibraryArt.SelectMosaic(songs, picked);
        return new CoverArt(count, picked[0], picked[1], picked[2], picked[3], PlaylistCoverSet.None);
    }
}

internal static class LibraryArt
{
    public const int MosaicTiles = 4;
    private const float GlyphFraction = 0.022f;
    private const float RimAlpha = 0.06f;
    private const float PlaceholderGlyphAlpha = 0.85f;
    private static readonly Vector4 Rim = new(1f, 1f, 1f, RimAlpha);
    private static readonly Vector4 GlyphInk = new(1f, 1f, 1f, PlaceholderGlyphAlpha);

    public static int SelectMosaic(IReadOnlyList<SongRecord> songs, Span<string> picked)
    {
        var found = 0;
        for (var index = 0; index < songs.Count && found < Math.Min(MosaicTiles, picked.Length); index++)
        {
            Consider(songs[index].ThumbnailUrl, picked, ref found);
        }

        return MosaicCount(found);
    }

    public static int SelectMosaic(ReadOnlySpan<Song> songs, Span<string> picked)
    {
        var found = 0;
        for (var index = 0; index < songs.Length && found < Math.Min(MosaicTiles, picked.Length); index++)
        {
            Consider(songs[index].ThumbnailUrl, picked, ref found);
        }

        return MosaicCount(found);
    }

    public static void DrawCover(ImDrawListPtr drawList, RemoteImageCache images, WallpaperImageCache covers,
        Vector2 min, float side, in CoverArt art, string seed, float radiusFraction = ArtworkTile.TileRadiusFraction)
    {
        var max = min + new Vector2(side, side);
        var radius = side * radiusFraction;
        if (art.Custom.HasCover && covers.Get(art.Custom.For(side)) is { } custom)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(custom.Size);
            Squircle.FillImage(drawList, min, max, radius, custom.Handle, 0xFFFFFFFFu, uv0, uv1);
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Rim), 1f);
            return;
        }

        if (art.Count == 1)
        {
            ArtworkTile.Draw(drawList, images, min, side, art.First, seed, radiusFraction);
            return;
        }

        var swatch = ArtGradient.FromName(seed);
        Squircle.FillVerticalGradient(drawList, min, max, radius, ImGui.GetColorU32(swatch.Top),
            ImGui.GetColorU32(swatch.Bottom));
        if (art.Count < MosaicTiles)
        {
            AppSkin.Icon(drawList, (min + max) * 0.5f, IconGlyph.Of(FontAwesomeIcon.Music), GlyphInk,
                GlyphScale(side));
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Rim), 1f);
            return;
        }

        var half = side * 0.5f;
        for (var tile = 0; tile < MosaicTiles; tile++)
        {
            var texture = images.Sized(art.At(tile), half);
            if (texture is null)
            {
                continue;
            }

            var column = tile % 2;
            var row = tile / 2;
            var quadrantMin = new Vector2(min.X + column * half, min.Y + row * half);
            var (cover0, cover1) = ImageFit.CoverSquare(texture.Size);
            var span = cover1 - cover0;
            var uv0 = new Vector2(cover0.X - column * span.X, cover0.Y - row * span.Y);
            var uv1 = new Vector2(cover0.X + (2 - column) * span.X, cover0.Y + (2 - row) * span.Y);
            drawList.PushClipRect(quadrantMin, quadrantMin + new Vector2(half, half), true);
            Squircle.FillImage(drawList, min, max, radius, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
            drawList.PopClipRect();
        }

        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Rim), 1f);
    }

    public static void DrawCircle(ImDrawListPtr drawList, RemoteImageCache images, Vector2 center, float radius,
        string url, string seed)
    {
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var texture = string.IsNullOrEmpty(url) ? null : images.Sized(url, radius * 2f);
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, radius);
        }
        else
        {
            ArtGradient.DrawDisc(drawList, center, radius, ArtGradient.FromName(seed), 1f);
            AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.Microphone), GlyphInk,
                GlyphScale(radius * 2f));
        }

        drawList.AddCircle(center, radius, ImGui.GetColorU32(Rim), 0, 1f);
    }

    private static float GlyphScale(float side) => side / MathF.Max(0.01f, UiScale.Current) * GlyphFraction;

    private static void Consider(string url, Span<string> picked, ref int found)
    {
        if (string.IsNullOrEmpty(url) || Contains(picked, found, url))
        {
            return;
        }

        picked[found] = url;
        found++;
    }

    private static int MosaicCount(int found)
    {
        if (found >= MosaicTiles)
        {
            return MosaicTiles;
        }

        return found > 0 ? 1 : 0;
    }

    private static bool Contains(Span<string> picked, int count, string url)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(picked[index], url, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
