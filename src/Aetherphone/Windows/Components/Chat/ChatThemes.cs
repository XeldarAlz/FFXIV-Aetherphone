using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal readonly struct ChatTheme
{
    public readonly string Id;
    public readonly LocString Name;
    public readonly Vector4 Accent;

    public ChatTheme(string id, LocString name, Vector4 accent)
    {
        Id = id;
        Name = name;
        Accent = accent;
    }

    public Vector4 OutgoingBubble => Palette.Mix(Accent, ChatThemes.Body, ChatThemes.OutgoingMix);

    public Vector4 Badge => Palette.Lighten(Accent, ChatThemes.BadgeLift);
}

internal static class ChatThemes
{
    public const float OutgoingMix = 0.55f;
    public const float BadgeLift = 0.12f;
    public const float BubbleRounding = 9f;

    public static readonly Vector4 Body = new(0.043f, 0.078f, 0.102f, 1f);
    public static readonly Vector4 IncomingBubble = new(0.125f, 0.173f, 0.200f, 1f);
    public static readonly Vector4 IncomingInk = new(0.914f, 0.929f, 0.937f, 1f);
    public static readonly Vector4 OutgoingInk = new(0.914f, 0.929f, 0.937f, 1f);

    public static readonly ChatTheme[] All =
    {
        new("chocobo", L.Chat.ThemeChocobo, AccentRing.Orange),
        new("leaf", L.Chat.ThemeLeaf, AccentRing.Green),
        new("emerald", L.Chat.ThemeEmerald, new Vector4(0.000f, 0.659f, 0.518f, 1f)),
        new("ocean", L.Chat.ThemeOcean, AccentRing.Azure),
        new("lavender", L.Chat.ThemeLavender, AccentRing.Violet),
        new("rose", L.Chat.ThemeRose, AccentRing.Rose),
        new("sunset", L.Chat.ThemeSunset, AccentRing.Red),
        new("sky", L.Chat.ThemeSky, AccentRing.Cyan),
        new("slate", L.Chat.ThemeSlate, AccentRing.Slate),
    };

    private static readonly SocialInk?[] InkCache = new SocialInk?[All.Length];
    private static readonly AppPalette?[] PaletteCache = new AppPalette?[All.Length];

    public static int IndexOf(string id)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    public static ChatTheme Resolve(string id, string fallbackId)
    {
        var index = id.Length > 0 ? IndexOf(id) : IndexOf(fallbackId);
        return All[index];
    }

    public static ChatTheme Resolve(string id) => All[IndexOf(id)];

    public static ChatBubbleStyle BubbleStyleFor(in ChatTheme theme) =>
        new(theme.OutgoingBubble, OutgoingInk, IncomingBubble, IncomingInk, BubbleRounding, tails: true);

    public static AppPalette PaletteFor(string id)
    {
        var index = IndexOf(id);
        var cached = PaletteCache[index];
        if (cached is { } palette)
        {
            return palette;
        }

        var accent = All[index].Accent;
        var built = AppPalettes.Message with
        {
            Accent = accent,
            HeaderInk = Palette.WithAlpha(Palette.Lighten(accent, 0.55f), 0.95f),
            BloomTop = Palette.WithAlpha(accent, 0.06f),
        };
        PaletteCache[index] = built;
        return built;
    }

    public static SocialInk InkFor(string id)
    {
        var index = IndexOf(id);
        var cached = InkCache[index];
        if (cached is not null)
        {
            return cached;
        }

        var built = new SocialInk(PaletteFor(id));
        InkCache[index] = built;
        return built;
    }
}
