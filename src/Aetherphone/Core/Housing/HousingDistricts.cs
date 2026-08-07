using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Housing;

internal readonly record struct HousingDistrict(uint Id, string Name, string ShortName, int Wards);

internal static class HousingDistricts
{
    public const int PlotsPerDivision = 30;
    public const int PlotsPerWard = PlotsPerDivision * 2;
    public const int DefaultWards = 30;

    public const uint MistId = 339u;
    public const uint LavenderBedsId = 340u;
    public const uint GobletId = 341u;
    public const uint ShiroganeId = 641u;
    public const uint EmpyreumId = 979u;

    public static readonly HousingDistrict Mist = new(MistId, "Mist", "Mist", DefaultWards);
    public static readonly HousingDistrict LavenderBeds =
        new(LavenderBedsId, "The Lavender Beds", "Lavender", DefaultWards);
    public static readonly HousingDistrict Goblet = new(GobletId, "The Goblet", "Goblet", DefaultWards);
    public static readonly HousingDistrict Shirogane = new(ShiroganeId, "Shirogane", "Shirogane", DefaultWards);
    public static readonly HousingDistrict Empyreum = new(EmpyreumId, "Empyreum", "Empyreum", DefaultWards);

    public static readonly IReadOnlyList<HousingDistrict> All = new[]
    {
        Mist, LavenderBeds, Goblet, Shirogane, Empyreum,
    };

    public static uint DefaultId => Mist.Id;

    public static bool TryGet(uint districtId, out HousingDistrict district)
    {
        for (var index = 0; index < All.Count; index++)
        {
            if (All[index].Id == districtId)
            {
                district = All[index];
                return true;
            }
        }

        district = default;
        return false;
    }

    public static HousingDistrict Resolve(uint districtId) => TryGet(districtId, out var district) ? district : Mist;

    public static string Name(uint districtId) => TryGet(districtId, out var district) ? district.Name : string.Empty;

    public static string DisplayName(uint districtId) => districtId switch
    {
        LavenderBedsId => Loc.T(L.Housing.DistrictLavenderBeds),
        GobletId => Loc.T(L.Housing.DistrictGoblet),
        ShiroganeId => Loc.T(L.Housing.DistrictShirogane),
        EmpyreumId => Loc.T(L.Housing.DistrictEmpyreum),
        _ => Loc.T(L.Housing.DistrictMist),
    };

    public static string ShortDisplayName(uint districtId) => districtId switch
    {
        LavenderBedsId => Loc.T(L.Housing.DistrictLavenderBedsShort),
        GobletId => Loc.T(L.Housing.DistrictGobletShort),
        ShiroganeId => Loc.T(L.Housing.DistrictShiroganeShort),
        EmpyreumId => Loc.T(L.Housing.DistrictEmpyreumShort),
        _ => Loc.T(L.Housing.DistrictMistShort),
    };

    public static int ClampWard(uint districtId, int ward)
    {
        var wards = Resolve(districtId).Wards;
        return Math.Clamp(ward, 1, wards);
    }

    public static bool IsSubdivision(int plotNumber) => plotNumber > PlotsPerDivision;
}
