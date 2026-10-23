using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Campaigns;

internal sealed record CampaignDefinition(string AppId, string Slug, LocString FallbackName, Vector4 FallbackAccent,
    string FallbackHashtag)
{
    public static readonly CampaignDefinition LoveIsland = new("loveisland", "love-island-eclipse",
        L.Apps.LoveIsland, new Vector4(0.812f, 0.145f, 0.357f, 1f), "LoveIslandEclipse");
}
