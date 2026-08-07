using Dalamud.Interface;

namespace Aetherphone.Apps.Shortcuts;

internal static class ShortcutPalette
{
    public static readonly Vector4[] Wheel =
    {
        new(0.36f, 0.55f, 0.98f, 1f), new(0.55f, 0.45f, 0.95f, 1f), new(0.82f, 0.42f, 0.92f, 1f),
        new(0.95f, 0.38f, 0.62f, 1f), new(0.94f, 0.36f, 0.36f, 1f), new(0.96f, 0.55f, 0.22f, 1f),
        new(0.94f, 0.74f, 0.20f, 1f), new(0.62f, 0.78f, 0.24f, 1f), new(0.24f, 0.76f, 0.46f, 1f),
        new(0.18f, 0.76f, 0.72f, 1f), new(0.26f, 0.66f, 0.90f, 1f), new(0.52f, 0.56f, 0.64f, 1f),
    };

    public static readonly FontAwesomeIcon[] Glyphs =
    {
        FontAwesomeIcon.Bolt, FontAwesomeIcon.Star, FontAwesomeIcon.Heart, FontAwesomeIcon.Flag,
        FontAwesomeIcon.Crown, FontAwesomeIcon.Gem, FontAwesomeIcon.Fire, FontAwesomeIcon.Magic,
        FontAwesomeIcon.ShieldAlt, FontAwesomeIcon.FistRaised, FontAwesomeIcon.Skull, FontAwesomeIcon.Chess,
        FontAwesomeIcon.Bullseye, FontAwesomeIcon.Crosshairs, FontAwesomeIcon.Dice, FontAwesomeIcon.Trophy,
        FontAwesomeIcon.Horse, FontAwesomeIcon.Dragon, FontAwesomeIcon.Cat, FontAwesomeIcon.Feather,
        FontAwesomeIcon.Fish, FontAwesomeIcon.Leaf, FontAwesomeIcon.Tree, FontAwesomeIcon.Mountain,
        FontAwesomeIcon.Anchor, FontAwesomeIcon.Compass, FontAwesomeIcon.Map, FontAwesomeIcon.MapMarkedAlt,
        FontAwesomeIcon.Home, FontAwesomeIcon.DoorOpen, FontAwesomeIcon.Bed, FontAwesomeIcon.Utensils,
        FontAwesomeIcon.Hammer, FontAwesomeIcon.Wrench, FontAwesomeIcon.Cogs, FontAwesomeIcon.Flask,
        FontAwesomeIcon.Book, FontAwesomeIcon.Scroll, FontAwesomeIcon.Camera, FontAwesomeIcon.Music,
        FontAwesomeIcon.Comment, FontAwesomeIcon.Users, FontAwesomeIcon.UserFriends, FontAwesomeIcon.Handshake,
        FontAwesomeIcon.Tshirt, FontAwesomeIcon.ShoppingBag, FontAwesomeIcon.Coins, FontAwesomeIcon.Clock,
    };
}
