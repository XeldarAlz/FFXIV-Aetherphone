using System.Globalization;
using System.Numerics;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Core.Social;

internal sealed record BadgeStyle(
    string Id,
    string BaseName,
    BadgeTranslationDto[]? Translations,
    FontAwesomeIcon Glyph,
    string AssetUrl,
    Vector4[] Colors,
    NameEffectKind Effect,
    bool Hidden)
{
    private const uint FallbackGlyph = 0xF059;

    private static readonly Vector4[] WhiteOnly = { new(1f, 1f, 1f, 1f) };

    public string Name
    {
        get
        {
            if (Translations is null)
            {
                return BaseName;
            }

            var code = Loc.Current.Code;
            for (var index = 0; index < Translations.Length; index++)
            {
                var translation = Translations[index];
                if (translation.Lang == code && !string.IsNullOrEmpty(translation.Name))
                {
                    return translation.Name;
                }
            }

            return BaseName;
        }
    }

    public static BadgeStyle From(BadgeDescriptorDto descriptor)
    {
        return new BadgeStyle(
            descriptor.Id,
            descriptor.Name,
            descriptor.Translations,
            (FontAwesomeIcon)ParseHex(descriptor.Icon, FallbackGlyph),
            descriptor.AssetUrl,
            ParseColors(descriptor.Colors),
            ParseEffect(descriptor.Effect),
            descriptor.Hidden ?? false);
    }

    public static uint ParseHex(string? text, uint fallback)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        if (trimmed.Length == 0 || trimmed.Length > 8)
        {
            return fallback;
        }

        return uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed) && parsed != 0
            ? parsed
            : fallback;
    }

    private static Vector4[] ParseColors(string[]? colors)
    {
        if (colors is null || colors.Length == 0)
        {
            return WhiteOnly;
        }

        var parsed = new Vector4[colors.Length];
        for (var index = 0; index < colors.Length; index++)
        {
            var packed = ParseHex(colors[index], 0xFFFFFF);
            parsed[index] = new Vector4(
                ((packed >> 16) & 0xFF) / 255f,
                ((packed >> 8) & 0xFF) / 255f,
                (packed & 0xFF) / 255f,
                1f);
        }

        return parsed;
    }

    private static NameEffectKind ParseEffect(string key)
    {
        return key switch
        {
            "gradient" => NameEffectKind.Gradient,
            "breath" => NameEffectKind.Breath,
            "ripple" => NameEffectKind.Ripple,
            "flow" => NameEffectKind.Flow,
            "glint" => NameEffectKind.Glint,
            "sweep" => NameEffectKind.Sweep,
            "wave" => NameEffectKind.Wave,
            "ember" => NameEffectKind.Ember,
            "frost" => NameEffectKind.Frost,
            "aurora" => NameEffectKind.Aurora,
            "prism" => NameEffectKind.Prism,
            "glitch" => NameEffectKind.Glitch,
            "starfall" => NameEffectKind.Starfall,
            "eclipse" => NameEffectKind.Eclipse,
            "heartbeat" => NameEffectKind.Heartbeat,
            "pulse" => NameEffectKind.Pulse,
            "glow" => NameEffectKind.Glow,
            "spectrum" => NameEffectKind.Spectrum,
            "candy" => NameEffectKind.Candy,
            "stripes" => NameEffectKind.Stripes,
            "horizon" => NameEffectKind.Horizon,
            "chrome" => NameEffectKind.Chrome,
            "blaze" => NameEffectKind.Blaze,
            "bounce" => NameEffectKind.Bounce,
            "shiver" => NameEffectKind.Shiver,
            "wobble" => NameEffectKind.Wobble,
            "pop" => NameEffectKind.Pop,
            "flipboard" => NameEffectKind.Flipboard,
            "typewriter" => NameEffectKind.Typewriter,
            "outline" => NameEffectKind.Outline,
            "shadow" => NameEffectKind.Shadow,
            "longshadow" => NameEffectKind.Longshadow,
            "emboss" => NameEffectKind.Emboss,
            "chromatic" => NameEffectKind.Chromatic,
            "neon" => NameEffectKind.Neon,
            "underline" => NameEffectKind.Underline,
            "scan" => NameEffectKind.Scan,
            "comet" => NameEffectKind.Comet,
            "sakura" => NameEffectKind.Sakura,
            "snowfall" => NameEffectKind.Snowfall,
            "fireflies" => NameEffectKind.Fireflies,
            "hearts" => NameEffectKind.Hearts,
            "glitter" => NameEffectKind.Glitter,
            "bubbles" => NameEffectKind.Bubbles,
            "confetti" => NameEffectKind.Confetti,
            "storm" => NameEffectKind.Storm,
            _ => NameEffectKind.None,
        };
    }
}
