namespace Aetherphone.Core.Lodestone;

internal readonly record struct LodestoneCandidate(string Name, string Id);

internal static class LodestoneMatch
{
    public static string? ExactId(IReadOnlyList<LodestoneCandidate> candidates, string name)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (candidate.Id.Length > 0 && string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return candidate.Id;
            }
        }

        return null;
    }
}
