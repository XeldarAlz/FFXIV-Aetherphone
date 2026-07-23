namespace Aetherphone.Core.Conduct;

internal sealed class ConductGateService
{
    private readonly Configuration configuration;

    public ConductGateService(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public ConductGate? Active { get; private set; }

    /// <summary>True when <see cref="Active"/> was opened voluntarily (e.g. a "?" button) rather than as a
    /// first-open gate, so the overlay shows a plain close (X) instead of the read-it countdown and agree button.</summary>
    public bool ActiveIsReview { get; private set; }

    public void NotifyAppOpened(string appId)
    {
        if (Active is not null)
        {
            return;
        }

        if (ConductRules.For(appId) is not { } gate)
        {
            return;
        }

        if (configuration.HasAcknowledgedConduct(gate.AppId, gate.Version))
        {
            return;
        }

        Active = gate;
        ActiveIsReview = false;
    }

    /// <summary>Reopens the community rules for an app on demand, regardless of prior acknowledgement.</summary>
    public void ShowRules(string appId)
    {
        if (Active is not null)
        {
            return;
        }

        if (ConductRules.For(appId) is not { } gate)
        {
            return;
        }

        Active = gate;
        ActiveIsReview = true;
    }

    public void Acknowledge()
    {
        if (Active is not { } gate)
        {
            return;
        }

        configuration.AcknowledgeConduct(gate.AppId, gate.Version);
        Active = null;
    }

    /// <summary>Closes a voluntary review (see <see cref="ShowRules"/>) without touching acknowledgement state.</summary>
    public void Dismiss()
    {
        Active = null;
    }
}
