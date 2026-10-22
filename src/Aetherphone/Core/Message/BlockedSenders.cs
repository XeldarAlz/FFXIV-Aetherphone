using Aetherphone.Core.Aethernet;

namespace Aetherphone.Core.Message;

internal static class BlockedSenders
{
    public static bool Hides(BlockList blocks, bool isGroup, string senderId, string myUserId) =>
        isGroup && !string.Equals(senderId, myUserId, StringComparison.Ordinal) && blocks.Contains(senderId);

    public static bool Collapses(BlockList blocks, bool isGroup, string senderId, string myUserId, bool revealed) =>
        !revealed && Hides(blocks, isGroup, senderId, myUserId);

    public static bool AnyVisible(BlockList blocks, string[] userIds)
    {
        for (var index = 0; index < userIds.Length; index++)
        {
            if (!blocks.Contains(userIds[index]))
            {
                return true;
            }
        }

        return false;
    }
}
