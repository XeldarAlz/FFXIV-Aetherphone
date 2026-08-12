namespace Aetherphone.Core.Emulation;

internal sealed record EmulatorStarterGame(
    string SystemId,
    string Title,
    string Author,
    string License,
    string FileName,
    string Url,
    long Bytes);

internal static class EmulatorStarterGames
{
    private static readonly EmulatorStarterGame[] All =
    {
        new("gb", "µCity", "Antonio Niño Díaz", "GPL-3.0-or-later", "ucity.gbc",
            "https://github.com/AntonioND/ucity/releases/download/v1.3/ucity.gbc", 131072L),
        new("nes", "Nova the Squirrel", "NovaSquirrel", "GPL-3.0", "nova.nes",
            "https://github.com/NovaSquirrel/NovaTheSquirrel/releases/download/v1.0.6a/nova.nes", 262160L),
        new("gba", "Skyland", "Evan Bowman", "MPL-2.0", "Skyland.gba",
            "https://github.com/evanbowman/skyland-gba/releases/download/2022.1.7.0/Skyland.gba", 3041425L),
        new("n64", "N64brew Game Jam 2024", "N64brew", "MIT", "gamejam2024.z64",
            "https://github.com/n64brew/N64brew-GameJam2024/releases/download/1.2.1/gamejam2024.z64", 11943936L),
        new("megadrive", "KleleAtoms", "Nightwolf-47", "MIT", "kleleatoms.md",
            "https://github.com/Nightwolf-47/KleleAtoms-MD/releases/download/v1.2.1/kleleatoms-md-121.bin", 262144L),
        new("atari2600", "Berta and Butterflies", "vandalton", "MIT", "berta-and-butterflies.a26",
            "https://github.com/vandalton/BertaAndButterflies/releases/download/v1.00/berta-and-butterflies.v1.00.ntsc.en.bin",
            4096L),
    };

    public static EmulatorStarterGame? For(EmulatorSystemDefinition system)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index].SystemId, system.Id, StringComparison.Ordinal))
            {
                return All[index];
            }
        }

        return null;
    }
}
