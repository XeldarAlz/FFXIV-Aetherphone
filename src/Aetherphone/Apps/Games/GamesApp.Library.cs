using Aetherphone.Core.Apps;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private void DrawLibrary(in PhoneContext context) => DrawSearch(context);

    private void FocusLibrarySearch() => focusSearch = true;

    private void ResetLibrary()
    {
        focusSearch = false;
        searchText = string.Empty;
        lastSearchText = string.Empty;
    }
}
