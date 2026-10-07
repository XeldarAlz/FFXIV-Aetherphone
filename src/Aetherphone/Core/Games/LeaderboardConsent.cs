using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Games;

internal sealed class LeaderboardConsent
{
    private readonly IScoreUploadConfiguration configuration;
    private readonly object gate = new();

    public LeaderboardConsent(IScoreUploadConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public bool Needed(bool signedIn, UserDto? user) =>
        signedIn && user is { ShowOnLeaderboards: false } && !HasAnswered(user.Id);

    public bool HasAnswered(string accountId)
    {
        if (accountId.Length == 0)
        {
            return false;
        }

        lock (gate)
        {
            return IndexOf(accountId) >= 0;
        }
    }

    public bool Answer(string accountId)
    {
        if (accountId.Length == 0)
        {
            return false;
        }

        lock (gate)
        {
            if (IndexOf(accountId) >= 0)
            {
                return false;
            }

            configuration.LeaderboardConsentAnswered.Add(accountId);
        }

        configuration.Save();
        return true;
    }

    private int IndexOf(string accountId)
    {
        var answered = configuration.LeaderboardConsentAnswered;
        for (var index = 0; index < answered.Count; index++)
        {
            if (string.Equals(answered[index], accountId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
