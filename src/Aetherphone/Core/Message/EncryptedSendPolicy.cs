using Aetherphone.Core.Crypto;

namespace Aetherphone.Core.Message;

internal static class EncryptedSendPolicy
{
    public static bool IsEncryptedThread(ChatKeyStatus status)
    {
        return status.CurrentGeneration > 0 && status.MembersWithoutKeys.Length == 0;
    }

    public static bool MustHoldPlaintext(bool encrypted, ChatKeyStatus status)
    {
        if (encrypted)
        {
            return false;
        }

        return !status.Known || IsEncryptedThread(status);
    }
}
