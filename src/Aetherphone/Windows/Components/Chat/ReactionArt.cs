using Aetherphone.Core.Emoji;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class ReactionArt
{
    private const string LegacyThumbsUp = "+1";
    private const string LegacyHeart = "heart";
    private const string LegacyLaugh = "laugh";
    private const string LegacyWow = "wow";
    private const string LegacySad = "sad";
    private const string LegacyPray = "pray";
    private const string Angry = ":rage:";

    public static readonly string[] Tokens =
        { LegacyHeart, LegacyLaugh, LegacyWow, LegacySad, Angry, LegacyThumbsUp };

    public static bool TryFile(string token, out string file)
    {
        var legacyShortcode = LegacyShortcode(token);
        if (legacyShortcode.Length > 0)
        {
            return EmojiCatalog.TryResolve(legacyShortcode, out file);
        }

        if (IsShortcodeToken(token))
        {
            return EmojiCatalog.TryResolve(token.AsSpan(1, token.Length - 2), out file);
        }

        file = string.Empty;
        return false;
    }

    public static string Normalize(string token)
    {
        if (token.Length == 0 || !TryFile(token, out var file))
        {
            return token;
        }

        var legacy = LegacyToken(file);
        if (legacy.Length > 0)
        {
            return legacy;
        }

        var glyphs = EmojiCatalog.Glyphs;
        for (var glyphIndex = 0; glyphIndex < glyphs.Length; glyphIndex++)
        {
            var glyph = glyphs[glyphIndex];
            if (glyph.File == file)
            {
                return Wrap(glyph.Shortcode);
            }

            var tones = glyph.Tones;
            for (var toneIndex = 0; toneIndex < tones.Length; toneIndex++)
            {
                if (tones[toneIndex].File == file)
                {
                    return Wrap(tones[toneIndex].Shortcode);
                }
            }
        }

        return token;
    }

    public static bool Same(string left, string right)
    {
        if (left == right)
        {
            return true;
        }

        return left.Length > 0 && right.Length > 0 && TryFile(left, out var leftFile)
            && TryFile(right, out var rightFile) && leftFile == rightFile;
    }

    public static void Draw(ImDrawListPtr drawList, string token, Vector2 center, float size, float alpha,
        float fallbackScale)
    {
        if (TryFile(token, out var file))
        {
            var half = new Vector2(size * 0.5f, size * 0.5f);
            EmojiImages.TryDraw(drawList, file, center - half, center + half,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
            return;
        }

        var color = Color(token);
        AppSkin.Icon(drawList, center, Glyph(token), Palette.WithAlpha(color, color.W * alpha), fallbackScale);
    }

    private static bool IsShortcodeToken(string token) =>
        token.Length > 2 && token[0] == ':' && token[token.Length - 1] == ':';

    private static string Wrap(string shortcode) => string.Concat(":", shortcode, ":");

    private static string LegacyShortcode(string token)
    {
        return token switch
        {
            LegacyThumbsUp => "+1",
            LegacyHeart => "heart",
            LegacyLaugh => "joy",
            LegacyWow => "open_mouth",
            LegacySad => "cry",
            LegacyPray => "pray",
            _ => string.Empty,
        };
    }

    private static string LegacyToken(string file)
    {
        return file switch
        {
            "1f44d" => LegacyThumbsUp,
            "2764" => LegacyHeart,
            "1f602" => LegacyLaugh,
            "1f62e" => LegacyWow,
            "1f622" => LegacySad,
            "1f64f" => LegacyPray,
            _ => string.Empty,
        };
    }

    private static string Glyph(string token)
    {
        return token switch
        {
            LegacyHeart => IconGlyph.Of(FontAwesomeIcon.Heart),
            LegacyLaugh => IconGlyph.Of(FontAwesomeIcon.Laugh),
            LegacyWow => IconGlyph.Of(FontAwesomeIcon.Surprise),
            LegacySad => IconGlyph.Of(FontAwesomeIcon.SadTear),
            LegacyPray => IconGlyph.Of(FontAwesomeIcon.PrayingHands),
            Angry => IconGlyph.Of(FontAwesomeIcon.Angry),
            _ => IconGlyph.Of(FontAwesomeIcon.ThumbsUp),
        };
    }

    private static Vector4 Color(string token)
    {
        return token switch
        {
            LegacyHeart => new Vector4(0.94f, 0.35f, 0.44f, 1f),
            LegacyLaugh => new Vector4(0.97f, 0.79f, 0.26f, 1f),
            LegacyWow => new Vector4(0.97f, 0.72f, 0.32f, 1f),
            LegacySad => new Vector4(0.48f, 0.71f, 0.98f, 1f),
            LegacyPray => new Vector4(0.88f, 0.76f, 0.48f, 1f),
            Angry => new Vector4(0.95f, 0.40f, 0.36f, 1f),
            _ => new Vector4(0.42f, 0.66f, 0.98f, 1f),
        };
    }
}
