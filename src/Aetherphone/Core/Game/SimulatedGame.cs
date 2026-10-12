using FFXIVClientStructs.FFXIV.Client.Game;

namespace Aetherphone.Core.Game;

internal readonly record struct SimulatedStack(int Slot, uint ItemId, int Quantity, bool HighQuality);

internal sealed record SimulatedContainer(InventoryType Type, int Size, SimulatedStack[] Stacks);

internal readonly record struct SimulatedCurrency(uint ItemId, long Amount);

internal readonly record struct SimulatedGearset(int Id, uint ClassJobId, string Name, int ItemLevel);

internal sealed record SimulatedRetainer(ulong RetainerId, string Name, long Gil, int MarketItemCount,
    long VentureCompleteUnix, int VentureSeconds, SimulatedContainer[] Containers);

internal readonly record struct SimulatedVessel(string Name, bool Airship, long RegisterUnix, long ReturnUnix);

internal sealed record SimulatedFreeCompany(ulong Id, string Name, SimulatedContainer[] Containers);

internal sealed record SimulatedFriend(ulong ContentId, string Name, ushort HomeWorldId, ushort CurrentWorldId,
    string FreeCompanyTag, byte ClassJobId, ushort TerritoryId, bool Online);

internal sealed class SimulatedDailies
{
    public int BeastTribeAllowances { get; init; }
    public int CustomDeliveryAllowances { get; init; }
    public int LeveAllowances { get; init; }
    public int WondrousTailsStickers { get; init; }
    public bool WondrousTailsExpired { get; init; }
    public int DomanDonated { get; init; }
    public int DomanAllowance { get; init; }
    public bool DutyRouletteComplete { get; init; }
    public int HuntBillsUnlocked { get; init; }
    public int HuntBillsComplete { get; init; }
}

internal sealed class SimulatedGame
{
    public ulong ContentId { get; init; }
    public byte GrandCompany { get; init; }
    public long Gil { get; init; }
    public SimulatedContainer[] Containers { get; init; } = Array.Empty<SimulatedContainer>();
    public SimulatedCurrency[] Currencies { get; init; } = Array.Empty<SimulatedCurrency>();
    public int WeeklyTomestones { get; init; }
    public int WeeklyTomestoneLimit { get; init; }
    public int CompanySealCap { get; init; }
    public SimulatedRetainer[] Retainers { get; init; } = Array.Empty<SimulatedRetainer>();
    public int ActiveRetainer { get; set; } = -1;
    public SimulatedFreeCompany? FreeCompany { get; init; }
    public SimulatedVessel[] Vessels { get; init; } = Array.Empty<SimulatedVessel>();
    public long MapAllowanceUnix { get; init; }
    public short[] ClassJobLevels { get; init; } = Array.Empty<short>();
    public int[] ClassJobExperience { get; init; } = Array.Empty<int>();
    public byte MaxLevel { get; init; }
    public uint CurrentClassJobId { get; set; }
    public long RestedExperience { get; init; }
    public SimulatedGearset[] Gearsets { get; init; } = Array.Empty<SimulatedGearset>();
    public int ActiveGearsetId { get; set; } = -1;
    public SimulatedFriend[] Friends { get; init; } = Array.Empty<SimulatedFriend>();
    public string[] Linkshells { get; init; } = Array.Empty<string>();
    public string[] CrossWorldLinkshells { get; init; } = Array.Empty<string>();
    public SimulatedDailies Dailies { get; init; } = new();
    public byte WeatherId { get; set; }
    public int EorzeaMinuteOverride { get; set; } = -1;
    public bool Indoors { get; init; }
    public uint HouseDistrict { get; init; }
    public short Ward { get; init; }
    public short Plot { get; init; }
    public short Room { get; init; }

    public long CurrencyAmount(uint itemId)
    {
        for (var index = 0; index < Currencies.Length; index++)
        {
            if (Currencies[index].ItemId == itemId)
            {
                return Currencies[index].Amount;
            }
        }

        return 0;
    }

    public SimulatedRetainer? Retainer(int index) =>
        index >= 0 && index < Retainers.Length ? Retainers[index] : null;

    public bool TryGetGearset(int gearsetId, out SimulatedGearset gearset)
    {
        for (var index = 0; index < Gearsets.Length; index++)
        {
            if (Gearsets[index].Id == gearsetId)
            {
                gearset = Gearsets[index];
                return true;
            }
        }

        gearset = default;
        return false;
    }

    public static SimulatedContainer? Find(SimulatedContainer[] containers, InventoryType type)
    {
        for (var index = 0; index < containers.Length; index++)
        {
            if (containers[index].Type == type)
            {
                return containers[index];
            }
        }

        return null;
    }
}
