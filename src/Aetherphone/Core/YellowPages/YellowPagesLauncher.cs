namespace Aetherphone.Core.YellowPages;

internal sealed class YellowPagesLauncher
{
    private string? pendingAdId;

    public void RequestDetail(string adId)
    {
        pendingAdId = adId;
    }

    public bool TryConsumeDetail(out string adId)
    {
        if (pendingAdId is null)
        {
            adId = string.Empty;
            return false;
        }

        adId = pendingAdId;
        pendingAdId = null;
        return true;
    }
}
