using Aetherphone.Core.Housing;

namespace Aetherphone.Apps.Housing;

internal readonly struct HousingPlan
{
    private readonly HousingGameDistrictMap? district;
    private readonly HousingGameMap? active;

    public HousingPlan(HousingGameDistrictMap? district, bool subdivision)
    {
        this.district = district;
        active = district?.For(subdivision);
    }

    public bool HasDivisions => district is { HasSubdivision: true };

    public HousingGameMap? Map => active;

    public bool TryGetPoint(int plotNumber, out Vector2 normalized)
    {
        if (active is not null && active.TryGetPoint(plotNumber, out normalized))
        {
            return true;
        }

        normalized = new Vector2(0.5f, 0.5f);
        return false;
    }

    public Vector2 PositionOf(int plotNumber) =>
        TryGetPoint(plotNumber, out var normalized) ? normalized : new Vector2(0.5f, 0.5f);
    public int PlotCount => active?.Plots.Count ?? 0;
    public Vector2 PlotAt(int index) => active!.Plots[index].NormalizedPosition;
}
