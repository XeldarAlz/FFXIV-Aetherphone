using System.Collections.Frozen;
using Aetherphone.Core.Theme;

namespace Aetherphone.Core.Apps;

internal static class AppAccents
{
    private static readonly FrozenDictionary<string, Vector4> Accents = new Dictionary<string, Vector4>
    {
        ["messages"] = AccentRing.Green,
        ["message"] = AccentRing.Orange,
        ["chirper"] = BrandAccents.Chirper,
        ["aethergram"] = BrandAccents.Aethergram,
        ["velvet"] = BrandAccents.Velvet,
        ["character"] = AccentRing.Azure,
        ["health"] = AccentRing.Lime,
        ["camera"] = AccentRing.Slate,
        ["photos"] = AccentRing.Gold,
        ["collections"] = AccentRing.Indigo,
        ["skywatcher"] = AccentRing.Cyan,
        ["venues"] = AccentRing.Orchid,
        ["venue-sync"] = AccentRing.Azure,
        ["maps"] = AccentRing.Teal,
        ["news"] = AccentRing.Slate,
        ["market"] = AccentRing.Gold,
        ["music"] = AccentRing.Green,
        ["aetherstream"] = AccentRing.Violet,
        ["wallet"] = AccentRing.Green,
        ["coin"] = AccentRing.Gold,
        ["casino"] = AccentRing.Emerald,
        ["inventory"] = AccentRing.Orange,
        ["jobs"] = AccentRing.Indigo,
        ["clock"] = AccentRing.Red,
        ["timers"] = AccentRing.Lime,
        ["calendar"] = AccentRing.Red,
        ["notes"] = AccentRing.Gold,
        ["calculator"] = AccentRing.Slate,
        ["shortcuts"] = AccentRing.Indigo,
        ["dailies"] = AccentRing.Teal,
        ["fishing"] = AccentRing.Teal,
        ["notifications"] = AccentRing.Red,
        ["settings"] = AccentRing.Slate,
        ["appstore"] = AccentRing.Azure,
        ["feedback"] = AccentRing.Teal,
        ["polls"] = AccentRing.Indigo,
        ["announcements"] = AccentRing.Orange,
        ["muster"] = AccentRing.Cyan,
        ["yellowpages"] = AccentRing.Gold,
        ["games"] = AccentRing.Red,
        ["housing"] = AccentRing.Emerald,
        ["memory"] = AccentRing.Gold,
        ["bubbles"] = AccentRing.Teal,
        ["whack"] = AccentRing.Lime,
        ["breakout"] = AccentRing.Orchid,
        ["nonogram"] = AccentRing.Slate,
        ["watersort"] = AccentRing.Azure,
        ["simon"] = AccentRing.Green,
        ["flap"] = AccentRing.Cyan,
        ["snake"] = AccentRing.Lime,
        ["flow"] = AccentRing.Violet,
        ["match3"] = AccentRing.Orchid,
        ["2048"] = AccentRing.Orange,
        ["solitaire"] = AccentRing.Emerald,
        ["tetris"] = AccentRing.Cyan,
        ["reversi"] = AccentRing.Teal,
        ["minesweeper"] = AccentRing.Red,
        ["sudoku"] = AccentRing.Azure,
        ["chess"] = AccentRing.Gold,
        ["stack"] = AccentRing.Indigo,
        ["crystaldrop"] = AccentRing.Violet,
        ["beat"] = AccentRing.Rose,
        ["blade"] = AccentRing.Red,
        ["trivia"] = AccentRing.Indigo,
        ["rolladeck"] = AccentRing.Violet,
        ["hunts"] = AccentRing.Indigo
    }.ToFrozenDictionary();

    private static readonly FrozenSet<string> BrandLocked =
        new[] { "chirper", "aethergram", "velvet" }.ToFrozenSet();

    public static IReadOnlyList<string> Ids { get; } = Accents.Keys;

    public static bool IsBrandLocked(string id) => BrandLocked.Contains(id);

    public static Vector4 For(string id) =>
        Accents.TryGetValue(id, out var accent) ? accent : AccentRing.Fallback;

    public static Vector4 InkFor(string id) => AccentRing.Ink;
}
