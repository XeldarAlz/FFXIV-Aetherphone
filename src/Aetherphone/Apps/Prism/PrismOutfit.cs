using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Game;

namespace Aetherphone.Apps.Prism;

internal sealed class PrismGearRow
{
    public readonly uint ItemId;
    public readonly byte Stain0;
    public readonly byte Stain1;
    public readonly string Name;
    public readonly uint IconId;
    public readonly string Dyes;

    public PrismGearRow(uint itemId, byte stain0, byte stain1, string name, uint iconId, string dyes)
    {
        ItemId = itemId;
        Stain0 = stain0;
        Stain1 = stain1;
        Name = name;
        IconId = iconId;
        Dyes = dyes;
    }
}

internal static class PrismOutfit
{
    public static bool Has(PrismGearDto[]? outfit) => outfit is { Length: > 0 };

    public static void Fill(List<PrismGearRow> into, PrismGearDto[]? outfit, GameData gameData)
    {
        into.Clear();
        if (outfit is null)
        {
            return;
        }

        for (var index = 0; index < outfit.Length; index++)
        {
            var piece = outfit[index];
            if (!gameData.TryGetItem(piece.ItemId, out var name, out var iconId, out _) || name.Length == 0)
            {
                continue;
            }

            var first = gameData.StainName(piece.Stain0);
            var second = gameData.StainName(piece.Stain1);
            var dyes = first.Length == 0 ? second : second.Length == 0 ? first : string.Concat(first, ", ", second);
            into.Add(new PrismGearRow(piece.ItemId, piece.Stain0, piece.Stain1, name, iconId, dyes));
        }
    }
}
