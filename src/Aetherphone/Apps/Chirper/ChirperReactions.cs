using System.Numerics;
using Aetherphone.Core.Localization;
using Dalamud.Interface;

namespace Aetherphone.Apps.Chirper;

internal readonly struct ChirperReaction
{
    public readonly FontAwesomeIcon Icon;
    public readonly Vector4 Color;
    public readonly LocString Label;

    public ChirperReaction(FontAwesomeIcon icon, Vector4 color, LocString label)
    {
        Icon = icon;
        Color = color;
        Label = label;
    }
}

internal static class ChirperReactions
{
    public const int DefaultKind = 1;

    private static readonly ChirperReaction[] Kinds =
    {
        new(FontAwesomeIcon.ThumbsUp, new Vector4(0.23f, 0.51f, 0.96f, 1f), L.Chirper.ReactLike),
        new(FontAwesomeIcon.Heart, new Vector4(0.96f, 0.27f, 0.42f, 1f), L.Chirper.ReactLove),
        new(FontAwesomeIcon.GrinTears, new Vector4(0.98f, 0.74f, 0.18f, 1f), L.Chirper.ReactLaugh),
        new(FontAwesomeIcon.Surprise, new Vector4(0.98f, 0.74f, 0.18f, 1f), L.Chirper.ReactWow),
        new(FontAwesomeIcon.SadTear, new Vector4(0.40f, 0.68f, 0.92f, 1f), L.Chirper.ReactSad),
        new(FontAwesomeIcon.Angry, new Vector4(0.95f, 0.45f, 0.18f, 1f), L.Chirper.ReactAngry),
    };

    public static int Count => Kinds.Length;

    public static ChirperReaction Get(int kind) => Kinds[Math.Clamp(kind, 0, Kinds.Length - 1)];

    public static string Glyph(int kind) => Get(kind).Icon.ToIconString();

    public static Vector4 Color(int kind) => Get(kind).Color;

    public static string Label(int kind) => Loc.T(Get(kind).Label);
}
