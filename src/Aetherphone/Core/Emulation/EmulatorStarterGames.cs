namespace Aetherphone.Core.Emulation;

internal sealed record EmulatorStarterGame(
    string SystemId,
    string Title,
    string Author,
    string License,
    string FileName,
    string Url,
    long Bytes,
    string Sha256);

internal static class EmulatorStarterGames
{
    private static readonly EmulatorStarterGame[] All =
    {
        new("gb", "µCity", "Antonio Niño Díaz", "GPL-3.0-or-later", "ucity.gbc",
            "https://github.com/AntonioND/ucity/releases/download/v1.3/ucity.gbc", 131072L,
            "9422ee2ca7b7ea1d46b58b2a429fff3f354dfd3e732dee1e7ae6220f148ce6e0"),
        new("nes", "Nova the Squirrel", "NovaSquirrel", "GPL-3.0", "nova.nes",
            "https://github.com/NovaSquirrel/NovaTheSquirrel/releases/download/v1.0.6a/nova.nes", 262160L,
            "e4780e90b9d1587489bfb797d2ca395be21371ea9262fa9f87f99324ec6960ab"),
        new("gba", "Skyland", "Evan Bowman", "MPL-2.0", "Skyland.gba",
            "https://github.com/evanbowman/skyland-gba/releases/download/2022.1.7.0/Skyland.gba", 3041425L,
            "516f185f206366f9d44ed2df0d8afb2bc0e2f7d3adc2a2ed6e686c6c3eb4ac90"),
        new("n64", "N64brew Game Jam 2024", "N64brew", "MIT", "gamejam2024.z64",
            "https://github.com/n64brew/N64brew-GameJam2024/releases/download/1.2.1/gamejam2024.z64", 11943936L,
            "6856206289fbfc9a79cae73c19f51fec9e383de550e058556e26dead925cfae1"),
        new("megadrive", "KleleAtoms", "Nightwolf-47", "MIT", "kleleatoms.md",
            "https://github.com/Nightwolf-47/KleleAtoms-MD/releases/download/v1.2.1/kleleatoms-md-121.bin", 262144L,
            "d0660df5af9d39696272b0c6774f25f296ee38e86eacf8b52645eab3ea8b003a"),
        new("atari2600", "Berta and Butterflies", "vandalton", "MIT", "berta-and-butterflies.a26",
            "https://github.com/vandalton/BertaAndButterflies/releases/download/v1.00/berta-and-butterflies.v1.00.ntsc.en.bin",
            4096L, "3e0efbbf4aba069cbe06d5bf2795c2110914aa40e378743cfcc40321aae3928d"),
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
