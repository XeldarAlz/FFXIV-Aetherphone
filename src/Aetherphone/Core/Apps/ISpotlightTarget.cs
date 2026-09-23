using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Apps;

internal interface ISpotlightPages
{
    int SpotlightPageCount { get; }

    string SpotlightPageTitle(int pageIndex);

    bool IsSpotlightPageHidden(int pageIndex);

    void RequestSpotlightPage(int pageIndex);
}

internal interface ISpotlightNotes
{
    void RequestNote(Guid noteId);

    void RequestNewNote();
}

internal interface ISpotlightConversations
{
    ConversationDto[] SpotlightConversations { get; }
}

internal interface ISpotlightStoreApps
{
    void RequestStoreApp(string appId);
}

internal interface ISpotlightFights
{
    void RequestFight(string fightKey);
}

internal interface ISpotlightVenues
{
    void RequestVenue(string venueId);
}
