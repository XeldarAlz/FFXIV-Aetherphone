namespace Aetherphone.Core.Game;

internal static class FieldOperations
{
    public const uint Eureka = 41;
    public const uint FieldOperation = 48;
    public const uint OccultCrescent = 61;

    public static bool IsFieldOperationZone(uint territoryIntendedUseRowId) =>
        territoryIntendedUseRowId is Eureka or FieldOperation or OccultCrescent;
}
