namespace Aetherphone.Core.Venues;

internal readonly record struct VenueAddress(string World, string District, int Ward, int Plot)
{
    public bool IsKnown => Ward > 0 && Plot > 0 && !string.IsNullOrEmpty(World) && !string.IsNullOrEmpty(District);

    public static VenueAddress Of(string? world, string? district, int? ward, int? plot)
    {
        if (string.IsNullOrWhiteSpace(world) || string.IsNullOrWhiteSpace(district) || ward is not > 0 ||
            plot is not > 0)
        {
            return default;
        }

        var normalizedDistrict = district.Trim();
        if (normalizedDistrict.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            normalizedDistrict = normalizedDistrict[4..];
        }

        return new VenueAddress(world.Trim().ToLowerInvariant(), normalizedDistrict.ToLowerInvariant(), ward.Value,
            plot.Value);
    }
}
