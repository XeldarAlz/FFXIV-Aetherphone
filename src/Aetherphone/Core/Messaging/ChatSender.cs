using System.Text;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Aetherphone.Core.Messaging;

internal static unsafe class ChatSender
{
    public static bool TrySend(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        if (bytes.Length == 0 || bytes.Length > 500)
        {
            return false;
        }

        if (message.Length != Sanitise(message).Length)
        {
            return false;
        }

        var uiModule = UIModule.Instance();
        if (uiModule == null)
        {
            return false;
        }

        var entry = Utf8String.FromSequence(bytes);
        uiModule->ProcessChatBoxEntry(entry);
        entry->Dtor(true);
        return true;
    }

    private static string Sanitise(string text)
    {
        var utf8 = Utf8String.FromString(text);
        utf8->SanitizeString((AllowedEntities)0x27F);
        var sanitised = utf8->ToString();
        utf8->Dtor(true);
        return sanitised;
    }
}
