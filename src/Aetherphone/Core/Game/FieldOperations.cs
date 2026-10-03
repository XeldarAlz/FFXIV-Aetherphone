namespace Aetherphone.Core.Game;

internal static class FieldOperations
{
    private const uint EurekaIntendedUse = 41;
    private const uint BozjaIntendedUse = 48;
    private const uint OccultCrescentIntendedUse = 61;

    public static bool IsFieldOperation(uint territoryIntendedUse) =>
        territoryIntendedUse is EurekaIntendedUse or BozjaIntendedUse or OccultCrescentIntendedUse;
}
