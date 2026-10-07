namespace Aetherphone.Core.Games;

internal enum ParticipationChange : byte
{
    None,
    Joined,
    Left,
}

internal sealed class LeaderboardParticipation
{
    private string accountId = string.Empty;
    private bool optedIn;

    public ParticipationChange Observe(string? account, bool joined)
    {
        var id = account ?? string.Empty;
        var sameAccount = id.Length > 0 && string.Equals(id, accountId, StringComparison.Ordinal);
        var wasJoined = optedIn;
        accountId = id;
        optedIn = joined;
        if (!sameAccount || wasJoined == joined)
        {
            return ParticipationChange.None;
        }

        return joined ? ParticipationChange.Joined : ParticipationChange.Left;
    }
}
