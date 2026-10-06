using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Framework;

internal enum Backdrop : byte
{
    Nebula,
    Sky,
    Felt,
    Meadow,
    Neon,
    Cavern,
    Paper,
}

internal enum HudStyle : byte
{
    Standard,
    Compact,
}

internal readonly struct GameSpec
{
    public readonly string Id;
    public readonly LocString Title;
    public readonly LocString? Hook;
    public readonly GameGenre Genre;
    public readonly Backdrop Backdrop;
    public readonly HudStyle Hud;
    public readonly ScoreKind Kind;
    public readonly LocString[] Modes;
    public readonly string[] ModeStatIds;
    public readonly bool Clocked;
    public readonly bool Countdown;
    public readonly bool Landscape;
    public readonly bool Keyboard;
    public readonly bool Legacy;

    public GameSpec(string id, LocString title, GameGenre genre, LocString? hook = null,
        Backdrop backdrop = Backdrop.Nebula, HudStyle hud = HudStyle.Standard, ScoreKind kind = ScoreKind.Score,
        LocString[]? modes = null, string[]? modeStatIds = null, bool clocked = false, bool countdown = false,
        bool landscape = false, bool keyboard = false, bool legacy = false)
    {
        Id = id;
        Title = title;
        Hook = hook;
        Genre = genre;
        Backdrop = backdrop;
        Hud = hud;
        Kind = kind;
        Modes = modes ?? Array.Empty<LocString>();
        ModeStatIds = modeStatIds ?? Array.Empty<string>();
        Clocked = clocked;
        Countdown = countdown;
        Landscape = landscape;
        Keyboard = keyboard;
        Legacy = legacy;
    }

    public bool HasModes => Modes.Length > 1;

    public string StatIdFor(int mode) =>
        mode >= 0 && mode < ModeStatIds.Length && ModeStatIds[mode].Length > 0 ? ModeStatIds[mode] : Id;

    public int ClampMode(int mode)
    {
        if (Modes.Length == 0)
        {
            return 0;
        }

        return Math.Clamp(mode, 0, Modes.Length - 1);
    }
}
