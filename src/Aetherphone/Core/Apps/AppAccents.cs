using System.Collections.Frozen;
using System.Numerics;

namespace Aetherphone.Core.Apps;

internal static class AppAccents
{
    private static readonly Vector4 Fallback = new(0.40f, 0.42f, 0.48f, 1f);

    private static readonly FrozenDictionary<string, Vector4> Accents = new Dictionary<string, Vector4>
    {
        ["messages"] = new(0.30f, 0.78f, 0.42f, 1f),
        ["phone"] = new(0.20f, 0.78f, 0.35f, 1f),
        ["contacts"] = new(0.45f, 0.55f, 0.95f, 1f),
        ["chirper"] = new(0.16f, 0.52f, 0.94f, 1f),
        ["aethergram"] = new(0.92f, 0.30f, 0.38f, 1f),
        ["velvet"] = new(0.84f, 0.16f, 0.40f, 1f),
        ["findpeople"] = new(0.36f, 0.68f, 0.92f, 1f),
        ["character"] = new(0.98f, 0.22f, 0.36f, 1f),
        ["camera"] = new(0.70f, 0.72f, 0.78f, 1f),
        ["photos"] = new(0.95f, 0.62f, 0.25f, 1f),
        ["collections"] = new(0.36f, 0.62f, 0.96f, 1f),
        ["skywatcher"] = new(0.28f, 0.68f, 0.92f, 1f),
        ["venues"] = new(0.93f, 0.28f, 0.55f, 1f),
        ["maps"] = new(0.20f, 0.62f, 0.86f, 1f),
        ["news"] = new(0.96f, 0.44f, 0.27f, 1f),
        ["market"] = new(0.92f, 0.62f, 0.18f, 1f),
        ["music"] = new(0.13f, 0.75f, 0.36f, 1f),
        ["wallet"] = new(0.26f, 0.78f, 0.52f, 1f),
        ["inventory"] = new(0.42f, 0.58f, 0.86f, 1f),
        ["clock"] = new(1.00f, 0.58f, 0.00f, 1f),
        ["timers"] = new(1.00f, 0.62f, 0.04f, 1f),
        ["calendar"] = new(1.00f, 0.231f, 0.188f, 1f),
        ["notes"] = new(1.00f, 0.79f, 0.16f, 1f),
        ["calculator"] = new(1.00f, 0.62f, 0.10f, 1f),
        ["dailies"] = new(0.36f, 0.78f, 0.62f, 1f),
        ["fishing"] = new(0.24f, 0.62f, 0.86f, 1f),
        ["notifications"] = new(0.80f, 0.16f, 0.24f, 1f),
        ["settings"] = new(0.56f, 0.57f, 0.63f, 1f),
        ["feedback"] = new(0.08f, 0.66f, 0.55f, 1f),
        ["games"] = new(0.32f, 0.78f, 0.50f, 1f),
        ["memory"] = new(0.92f, 0.74f, 0.34f, 1f),
        ["bubbles"] = new(0.30f, 0.82f, 0.74f, 1f),
        ["whack"] = new(0.46f, 0.78f, 0.46f, 1f),
        ["breakout"] = new(0.95f, 0.45f, 0.50f, 1f),
        ["nonogram"] = new(0.40f, 0.78f, 0.82f, 1f),
        ["watersort"] = new(0.40f, 0.68f, 0.98f, 1f),
        ["simon"] = new(0.46f, 0.86f, 0.66f, 1f),
        ["flap"] = new(0.40f, 0.68f, 0.98f, 1f),
        ["snake"] = new(0.42f, 0.84f, 0.48f, 1f),
        ["flow"] = new(0.72f, 0.46f, 0.96f, 1f),
        ["match3"] = new(0.72f, 0.46f, 0.96f, 1f),
        ["2048"] = new(0.96f, 0.58f, 0.39f, 1f),
        ["solitaire"] = new(0.30f, 0.64f, 0.44f, 1f),
        ["tetris"] = new(0.52f, 0.78f, 0.98f, 1f),
        ["reversi"] = new(0.36f, 0.78f, 0.56f, 1f),
        ["minesweeper"] = new(0.40f, 0.68f, 0.98f, 1f),
    }.ToFrozenDictionary();

    public static Vector4 For(string id) => Accents.TryGetValue(id, out var accent) ? accent : Fallback;
}
