namespace Aetherphone.Core.Muster;

internal sealed class MusterLauncher
{
    private string? pendingMusterId;

    public void RequestDetail(string musterId)
    {
        pendingMusterId = musterId;
    }

    public bool TryConsumeDetail(out string musterId)
    {
        if (pendingMusterId is null)
        {
            musterId = string.Empty;
            return false;
        }

        musterId = pendingMusterId;
        pendingMusterId = null;
        return true;
    }
}
