using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Social;

internal static class FeedNotes
{
    public const string Follow = "follow";

    public const string Neighbour = "neighbour";

    public const string Fresh = "fresh";

    public const string Quality = "quality";

    public static string? SuggestionLabel(in FeedItemNote note)
    {
        return note.Source switch
        {
            Neighbour => Loc.T(L.Social.FeedSuggestedTaste),
            Fresh => Loc.T(L.Social.FeedSuggestedFresh),
            Quality => Loc.T(L.Social.FeedSuggested),
            _ => null,
        };
    }
}
