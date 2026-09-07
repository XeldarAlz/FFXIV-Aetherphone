using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Message;

internal readonly struct MessageTheme
{
    public readonly string Id;
    public readonly LocString Name;
    public readonly Vector4 Accent;

    public MessageTheme(string id, LocString name, Vector4 accent)
    {
        Id = id;
        Name = name;
        Accent = accent;
    }

    public Vector4 OutgoingBubble => Palette.Mix(Accent, MessageThemes.Body, MessageThemes.OutgoingMix);

    public Vector4 Badge => Palette.Lighten(Accent, MessageThemes.BadgeLift);
}

internal static class MessageThemes
{
    public const float OutgoingMix = 0.55f;
    public const float BadgeLift = 0.12f;

    public static readonly Vector4 Body = new(0.043f, 0.078f, 0.102f, 1f);
    public static readonly Vector4 IncomingBubble = new(0.125f, 0.173f, 0.200f, 1f);
    public static readonly Vector4 IncomingInk = new(0.914f, 0.929f, 0.937f, 1f);
    public static readonly Vector4 OutgoingInk = new(0.914f, 0.929f, 0.937f, 1f);

    public static readonly MessageTheme[] All =
    {
        new("chocobo", L.Message.ThemeChocobo, AccentRing.Orange),
        new("emerald", L.Message.ThemeEmerald, new Vector4(0.000f, 0.659f, 0.518f, 1f)),
        new("ocean", L.Message.ThemeOcean, AccentRing.Azure),
        new("lavender", L.Message.ThemeLavender, AccentRing.Violet),
        new("rose", L.Message.ThemeRose, AccentRing.Rose),
        new("sunset", L.Message.ThemeSunset, AccentRing.Red),
        new("sky", L.Message.ThemeSky, AccentRing.Cyan),
        new("slate", L.Message.ThemeSlate, AccentRing.Slate),
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

    public static MessageTheme Resolve(string id) => All[IndexOf(id)];

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
