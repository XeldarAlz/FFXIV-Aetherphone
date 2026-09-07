using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal readonly struct MessageWallpaperColor
{
    public readonly string Id;
    public readonly Vector4 Color;

    public MessageWallpaperColor(string id, Vector4 color)
    {
        Id = id;
        Color = color;
    }
}

internal static class MessageWallpapers
{
    public const string PhotoPrefix = "photo:";
    private const float PatternCell = 56f;
    private const float PatternGlyph = 17f;
    private const float PatternAlpha = 0.07f;
    private const float PhotoVeil = 0.42f;

    private static readonly Vector4 PatternInk = new(1f, 1f, 1f, 1f);

    private static readonly string[] PatternGlyphs =
    {
        PhoneIcons.MessageCircle, PhoneIcons.Heart, PhoneIcons.Star, PhoneIcons.Camera, PhoneIcons.MoodSmile,
        PhoneIcons.Feather, PhoneIcons.Sparkles, PhoneIcons.Compass, PhoneIcons.Gamepad, PhoneIcons.Flame,
        PhoneIcons.MapPin, PhoneIcons.Microphone, PhoneIcons.Bell, PhoneIcons.Photo, PhoneIcons.Phone,
        PhoneIcons.Bookmark,
    };

    public static readonly MessageWallpaperColor[] Colors =
    {
        new("default", MessageThemes.Body),
        new("sage", new Vector4(0.070f, 0.130f, 0.110f, 1f)),
        new("teal", new Vector4(0.050f, 0.140f, 0.160f, 1f)),
        new("ocean", new Vector4(0.030f, 0.120f, 0.190f, 1f)),
        new("navy", new Vector4(0.060f, 0.090f, 0.170f, 1f)),
        new("indigo", new Vector4(0.100f, 0.080f, 0.180f, 1f)),
        new("lilac", new Vector4(0.130f, 0.110f, 0.190f, 1f)),
        new("plum", new Vector4(0.150f, 0.070f, 0.160f, 1f)),
        new("wine", new Vector4(0.170f, 0.060f, 0.090f, 1f)),
        new("rust", new Vector4(0.170f, 0.090f, 0.050f, 1f)),
        new("mocha", new Vector4(0.130f, 0.100f, 0.080f, 1f)),
        new("olive", new Vector4(0.120f, 0.130f, 0.050f, 1f)),
        new("moss", new Vector4(0.080f, 0.120f, 0.090f, 1f)),
        new("forest", new Vector4(0.050f, 0.130f, 0.070f, 1f)),
        new("slate", new Vector4(0.100f, 0.110f, 0.130f, 1f)),
        new("charcoal", new Vector4(0.070f, 0.070f, 0.080f, 1f)),
    };

    public static bool IsPhoto(string id) => id.StartsWith(PhotoPrefix, StringComparison.Ordinal);

    public static string PhotoPath(string id) => IsPhoto(id) ? id.Substring(PhotoPrefix.Length) : string.Empty;

    public static string PhotoId(string path) => string.Concat(PhotoPrefix, path);

    public static Vector4 ColorOf(string id)
    {
        for (var index = 0; index < Colors.Length; index++)
        {
            if (string.Equals(Colors[index].Id, id, StringComparison.Ordinal))
            {
                return Colors[index].Color;
            }
        }

        return Colors[0].Color;
    }

    public static string Effective(Configuration configuration, string conversationId)
    {
        if (conversationId.Length > 0
            && configuration.MessageChatWallpapers.TryGetValue(conversationId, out var chatChoice)
            && chatChoice.Length > 0)
        {
            return chatChoice;
        }

        return configuration.MessageWallpaper;
    }

    public static void Paint(ImDrawListPtr drawList, Rect area, string id, bool pattern,
        WallpaperImageCache images)
    {
        drawList.PushClipRect(area.Min, area.Max, true);
        var painted = false;
        if (IsPhoto(id))
        {
            var texture = images.Get(PhotoPath(id));
            if (texture is not null)
            {
                var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, area.Width, area.Height);
                drawList.AddImage(texture.Handle, area.Min, area.Max, uv0, uv1);
                drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, PhotoVeil)));
                painted = true;
            }
        }

        if (!painted)
        {
            drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(ColorOf(id)));
            if (pattern)
            {
                PaintPattern(drawList, area);
            }
        }

        drawList.PopClipRect();
    }

    private static void PaintPattern(ImDrawListPtr drawList, Rect area)
    {
        var scale = UiScale.Current;
        var cell = PatternCell * scale;
        var glyphSize = PatternGlyph * scale;
        var ink = ImGui.GetColorU32(Palette.WithAlpha(PatternInk, PatternAlpha));
        var columns = (int)MathF.Ceiling(area.Width / cell) + 2;
        var rows = (int)MathF.Ceiling(area.Height / cell) + 2;
        for (var row = 0; row < rows; row++)
        {
            var offset = row % 2 == 0 ? 0f : cell * 0.5f;
            for (var column = 0; column < columns; column++)
            {
                var center = new Vector2(area.Min.X + column * cell + offset - cell * 0.5f,
                    area.Min.Y + row * cell + cell * 0.5f);
                if (center.X < area.Min.X - glyphSize || center.X > area.Max.X + glyphSize
                    || center.Y > area.Max.Y + glyphSize)
                {
                    continue;
                }

                var glyph = PatternGlyphs[(row * 7 + column * 3) % PatternGlyphs.Length];
                PhoneIcon.Draw(drawList, center, glyph, ink, glyphSize);
            }
        }
    }
}
