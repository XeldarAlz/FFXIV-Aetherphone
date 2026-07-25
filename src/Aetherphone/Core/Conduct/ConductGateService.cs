namespace Aetherphone.Core.Conduct;

internal sealed class ConductGateService
{
    private readonly Configuration configuration;

    public ConductGateService(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public ConductGate? Active { get; private set; }

    public bool ActiveIsReview { get; private set; }

    public void NotifyAppOpened(string appId)
    {
        if (ConductRules.For(appId) is not { } gate)
        {
            return;
        }

        if (configuration.HasAcknowledgedConduct(gate.AppId, gate.Version))
        {
            return;
        }

        Open(gate, false);
    }

    public void ShowRules(string appId)
    {
        if (ConductRules.For(appId) is not { } gate)
        {
            return;
        }

        Open(gate, true);
    }

    private void Open(ConductGate gate, bool review)
    {
        if (Active is not null)
        {
            return;
        }

        Active = gate;
        ActiveIsReview = review;
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

    public void Dismiss()
    {
        Active = null;
    }
}
