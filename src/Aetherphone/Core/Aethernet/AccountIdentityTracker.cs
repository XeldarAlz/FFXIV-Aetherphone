using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet;

internal sealed class AccountIdentityTracker
{
    private string displayName = string.Empty;
    private string handle = string.Empty;

    public bool Track(UserDto? user)
    {
        var nextDisplayName = user?.DisplayName ?? string.Empty;
        var nextHandle = user?.Handle ?? string.Empty;
        if (string.Equals(nextDisplayName, displayName, StringComparison.Ordinal)
            && string.Equals(nextHandle, handle, StringComparison.Ordinal))
        {
            return false;
        }

        displayName = nextDisplayName;
        handle = nextHandle;
        return true;
    }
}
