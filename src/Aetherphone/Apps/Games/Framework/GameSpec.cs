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
    public readonly ScoreKind[] ModeKinds;
    public readonly bool[] CountdownModes;
    public readonly bool Clocked;
    public readonly bool Countdown;
    public readonly bool Landscape;
    public readonly bool Keyboard;

    public GameSpec(string id, LocString title, GameGenre genre, LocString? hook = null,
        Backdrop backdrop = Backdrop.Nebula, HudStyle hud = HudStyle.Standard, ScoreKind kind = ScoreKind.Score,
        LocString[]? modes = null, string[]? modeStatIds = null, bool clocked = false, bool countdown = false,
        bool landscape = false, bool keyboard = false, ScoreKind[]? modeKinds = null, bool[]? countdownModes = null)
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
        ModeKinds = modeKinds ?? Array.Empty<ScoreKind>();
        CountdownModes = countdownModes ?? Array.Empty<bool>();
        Clocked = clocked;
        Countdown = countdown;
        Landscape = landscape;
        Keyboard = keyboard;
    }

    public bool HasModes => Modes.Length > 1;

    public string StatIdFor(int mode) =>
        mode >= 0 && mode < ModeStatIds.Length && ModeStatIds[mode].Length > 0 ? ModeStatIds[mode] : Id;

    public ScoreKind KindFor(int mode) => mode >= 0 && mode < ModeKinds.Length ? ModeKinds[mode] : Kind;

    public bool CountdownFor(int mode) => mode >= 0 && mode < CountdownModes.Length ? CountdownModes[mode] : Countdown;

    public int ClampMode(int mode)
    {
        if (Modes.Length == 0)
        {
            return 0;
        }

        return Math.Clamp(mode, 0, Modes.Length - 1);
    }
}
