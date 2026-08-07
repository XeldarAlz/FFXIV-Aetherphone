using Aetherphone.Core.Aethernet;

namespace Aetherphone.Core.Moderation;

internal sealed class SuspensionGate
{
    private static readonly string[] GatedAppIds =
    {
        "message", "chirper", "aethergram", "velvet", "muster", "yellowpages",
        "announcements", "polls",
    };

    private readonly AethernetSession session;

    public SuspensionGate(AethernetSession session)
    {
        this.session = session;
    }

    public event Action? Blocked;

    public bool Blocks(string appId)
    {
        if (!session.IsBanned)
        {
            return false;
        }

        for (var index = 0; index < GatedAppIds.Length; index++)
        {
            if (string.Equals(appId, GatedAppIds[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public void ReportBlocked()
    {
        Blocked?.Invoke();
    }
}
