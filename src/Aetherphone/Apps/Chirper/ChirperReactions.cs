using Aetherphone.Core.Emoji;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Chirper;

internal readonly struct ChirperReaction
{
    public readonly string Shortcode;
    public readonly Vector4 Color;
    public readonly LocString Label;

    public ChirperReaction(string shortcode, Vector4 color, LocString label)
    {
        Shortcode = shortcode;
        Color = color;
        Label = label;
    }
}

internal static class ChirperReactions
{
    public const int DefaultKind = 1;

    private static readonly ChirperReaction[] Kinds =
    {
        new("+1", new Vector4(0.23f, 0.51f, 0.96f, 1f), L.Chirper.ReactLike),
        new("heart", new Vector4(0.96f, 0.27f, 0.42f, 1f), L.Chirper.ReactLove),
        new("joy", new Vector4(0.98f, 0.74f, 0.18f, 1f), L.Chirper.ReactLaugh),
        new("open_mouth", new Vector4(0.98f, 0.74f, 0.18f, 1f), L.Chirper.ReactWow),
        new("cry", new Vector4(0.40f, 0.68f, 0.92f, 1f), L.Chirper.ReactSad),
        new("angry", new Vector4(0.95f, 0.45f, 0.18f, 1f), L.Chirper.ReactAngry),
        new("fire", new Vector4(0.99f, 0.42f, 0.12f, 1f), L.Chirper.ReactFire),
        new("skull", new Vector4(0.80f, 0.83f, 0.89f, 1f), L.Chirper.ReactSkull),
        new("sob", new Vector4(0.30f, 0.52f, 0.98f, 1f), L.Chirper.ReactSob),
        new("bomb", new Vector4(0.63f, 0.61f, 0.80f, 1f), L.Chirper.ReactBomb),
        new("eyes", new Vector4(0.72f, 0.66f, 0.58f, 1f), L.Chirper.ReactEyes),
        new("100", new Vector4(0.88f, 0.16f, 0.20f, 1f), L.Chirper.ReactHundred),
        new("question", new Vector4(0.94f, 0.36f, 0.38f, 1f), L.Chirper.ReactQuestion),
    };

    private static readonly string?[] EmojiFiles = new string?[Kinds.Length];

    public static int Count => Kinds.Length;
    public static ChirperReaction Get(int kind) => Kinds[Math.Clamp(kind, 0, Kinds.Length - 1)];
    public static Vector4 Color(int kind) => Get(kind).Color;
    public static string Label(int kind) => Loc.T(Get(kind).Label);

    public static string EmojiFile(int kind)
    {
        var index = Math.Clamp(kind, 0, Kinds.Length - 1);
        var cached = EmojiFiles[index];
        if (cached is not null)
        {
            return cached;
        }

        var resolved = EmojiCatalog.TryResolve(Kinds[index].Shortcode, out var file) ? file : string.Empty;
        EmojiFiles[index] = resolved;
        return resolved;
    }
}
