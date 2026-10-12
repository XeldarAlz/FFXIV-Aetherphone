using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Config;

namespace Aetherphone.Core.Games;

internal sealed class LeaderboardConsent
{
    private readonly AnsweredAccounts answered;

    public LeaderboardConsent(IScoreUploadConfiguration configuration)
    {
        answered = new AnsweredAccounts(() => configuration.LeaderboardConsentAnswered, configuration.Save);
    }

    public bool Needed(bool signedIn, UserDto? user) =>
        signedIn && user is { ShowOnLeaderboards: false } && !HasAnswered(user.Id);

    public bool HasAnswered(string accountId) => answered.Contains(accountId);

    public bool Answer(string accountId) => answered.Add(accountId);
}
