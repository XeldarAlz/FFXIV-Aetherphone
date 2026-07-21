namespace Aetherphone.Core.Conduct;

internal sealed class ConductGateService
{
    private readonly Configuration configuration;

    public ConductGateService(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public ConductGate? Active { get; private set; }

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
}
