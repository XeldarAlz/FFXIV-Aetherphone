namespace Aetherphone.Apps.YellowPages;

internal enum YellowPagesScreen : byte
{
    Browse,
    Intent,
    Detail,
    Compose,
    Thread,
    NewInquiry,
    Encryption,
}

internal enum YellowPagesTab : byte
{
    Browse,
    Saved,
    Inquiries,
    Mine,
}

internal readonly record struct YellowPagesRoute(YellowPagesScreen Screen, string? AdId = null, int Intent = 0)
{
    public static readonly YellowPagesRoute Browse = new(YellowPagesScreen.Browse);
    public static readonly YellowPagesRoute Compose = new(YellowPagesScreen.Compose);
    public static readonly YellowPagesRoute Encryption = new(YellowPagesScreen.Encryption);

    public static YellowPagesRoute Detail(string adId) => new(YellowPagesScreen.Detail, adId);

    public static YellowPagesRoute ForIntent(int intent) => new(YellowPagesScreen.Intent, null, intent);

    public static YellowPagesRoute Thread(string inquiryId) => new(YellowPagesScreen.Thread, inquiryId);

    public static YellowPagesRoute NewInquiry(string adId) => new(YellowPagesScreen.NewInquiry, adId);
}
