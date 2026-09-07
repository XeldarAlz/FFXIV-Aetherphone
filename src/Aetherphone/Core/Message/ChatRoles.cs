namespace Aetherphone.Core.Message;

internal static class ChatRoles
{
    public const int Member = 0;
    public const int Owner = 1;
    public const int Admin = 2;

    public static bool CanManage(int role) => role is Owner or Admin;
}
