using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Campaigns;

internal enum CampaignLaunchKind : byte
{
    None,
    Post,
    Experience,
    Perks,
    Syncshells,
}

internal readonly record struct CampaignLaunch(CampaignLaunchKind Kind, string Id)
{
    public static readonly CampaignLaunch None = new(CampaignLaunchKind.None, string.Empty);
}

internal sealed class CampaignLauncher
{
    public const string PostPrefix = "post:";
    public const string ExperiencePrefix = "experience:";
    public const string PerksKey = "perks";
    public const string SyncshellsKey = "syncshells";

    private readonly LaunchIntent intent = new();

    public void Request(string groupKey) => intent.Request(groupKey);

    public bool TryConsume(out CampaignLaunch launch)
    {
        if (!intent.TryConsume(out var groupKey))
        {
            launch = CampaignLaunch.None;
            return false;
        }

        launch = Parse(groupKey);
        return launch.Kind != CampaignLaunchKind.None;
    }

    public static CampaignLaunch Parse(string groupKey)
    {
        if (groupKey.StartsWith(PostPrefix, StringComparison.Ordinal))
        {
            return new CampaignLaunch(CampaignLaunchKind.Post, groupKey.Substring(PostPrefix.Length));
        }

        if (groupKey.StartsWith(ExperiencePrefix, StringComparison.Ordinal))
        {
            return new CampaignLaunch(CampaignLaunchKind.Experience, groupKey.Substring(ExperiencePrefix.Length));
        }

        if (string.Equals(groupKey, PerksKey, StringComparison.Ordinal))
        {
            return new CampaignLaunch(CampaignLaunchKind.Perks, string.Empty);
        }

        if (string.Equals(groupKey, SyncshellsKey, StringComparison.Ordinal))
        {
            return new CampaignLaunch(CampaignLaunchKind.Syncshells, string.Empty);
        }

        return CampaignLaunch.None;
    }
}
