namespace Aetherphone.Core.Message;

internal static class ChatPresence
{
    public const int Hidden = 0;
    public const int Online = 1;
    public const int Offline = 2;

    public static bool IsOnline(int presence) => presence == Online;
}
