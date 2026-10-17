using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;

namespace Aetherphone.Core.Apps;

internal interface ISocialFeed
{
    SocialFeedScope LatestScope { get; }

    PostDto[] Feed(SocialFeedScope scope);

    bool IsLoading(SocialFeedScope scope);

    void Load(SocialFeedScope scope);
}