using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Config;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoFameChoiceStore : IDisposable
{
    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore chips;
    private readonly CasinoFloorStore floor;
    private readonly AnsweredAccounts answered;
    private readonly StoreWork work = new("CasinoFameChoice");
    private readonly object gate = new();

    private volatile bool saving;
    private volatile bool requested;
    private AepFailure failure;
    private int inFlight;
    private string? lastAccountId;

    public CasinoFameChoiceStore(Configuration configuration, AethernetSession session, CasinoClient casino,
        CasinoStore chips, CasinoFloorStore floor)
    {
        this.session = session;
        this.casino = casino;
        this.chips = chips;
        this.floor = floor;
        answered = AnsweredFor(configuration);
        lastAccountId = session.CurrentUser?.Id;
        session.Changed += OnSessionChanged;
    }

    public UserDto? CurrentUser => session.CurrentUser;

    public bool Needed => ConsentNeeded(chips.HasFeature(CasinoFeatures.Fame), session.IsSignedIn,
        session.CurrentUser, answered);

    public bool Shown => saving ? requested : session.IsSignedIn && session.CurrentUser is { ShowInHallOfFame: true };

    public bool Saving => saving;

    public AepFailure Failure
    {
        get
        {
            lock (gate)
            {
                return failure;
            }
        }
    }

    internal static AnsweredAccounts AnsweredFor(Configuration configuration) =>
        new(() => configuration.HallOfFameConsentAnswered, configuration.Save);

    internal static bool ConsentNeeded(bool fameOpen, bool signedIn, UserDto? user, AnsweredAccounts answered) =>
        fameOpen && signedIn && user is { ShowInHallOfFame: false } && !answered.Contains(user.Id);

    public void SetChoice(bool show)
    {
        if (session.CurrentUser is null || !session.IsSignedIn)
        {
            return;
        }

        if (Interlocked.Exchange(ref inFlight, 1) != 0)
        {
            return;
        }

        requested = show;
        saving = true;
        SetFailure(AepFailure.None);
        var reported = AepFailure.None;
        work.Run("hall of fame choice", async token =>
        {
            var reply = await casino.SetShowInHallOfFameAsync(show, token, value => reported = value)
                .ConfigureAwait(false);
            if (reply is null)
            {
                return false;
            }

            answered.Add(reply.Id);
            session.SetUser(reply);
            floor.RefreshFame();
            return true;
        }, succeeded =>
        {
            Interlocked.Exchange(ref inFlight, 0);
            if (!succeeded)
            {
                SetFailure(reported.Failed ? reported : AepFailure.Transport(AepFailureKind.BadResponse));
            }

            saving = false;
        });
    }

    public void Decline()
    {
        var user = session.CurrentUser;
        if (user is null)
        {
            return;
        }

        answered.Add(user.Id);
        SetFailure(AepFailure.None);
    }

    public void ClearFailure() => SetFailure(AepFailure.None);

    private void SetFailure(in AepFailure value)
    {
        lock (gate)
        {
            failure = value;
        }
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        SetFailure(AepFailure.None);
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
