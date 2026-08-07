namespace Aetherphone.Core.YellowPages;

internal sealed class YellowPagesLauncher
{
    private string? pendingAdId;
    private string? pendingInquiryId;

    public void RequestDetail(string adId)
    {
        pendingAdId = adId;
        pendingInquiryId = null;
    }

    public void RequestInquiry(string inquiryId)
    {
        pendingInquiryId = inquiryId;
        pendingAdId = null;
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

    public bool TryConsumeInquiry(out string inquiryId)
    {
        if (pendingInquiryId is null)
        {
            inquiryId = string.Empty;
            return false;
        }

        inquiryId = pendingInquiryId;
        pendingInquiryId = null;
        return true;
    }
}
