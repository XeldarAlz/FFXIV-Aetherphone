using System.Text;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Aetherphone.Core.Linkpearl;

internal static unsafe class ChatSender
{
    public const int MaxBytes = 500;

    public static bool TrySend(string message) => Send(message, requireClean: true);

    public static bool TrySendSanitised(string message) => Send(message, requireClean: false);

    private static bool Send(string message, bool requireClean)
    {
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        var payload = requireClean ? message : Sanitise(message).Trim();
        if (payload.Length == 0)
        {
            return false;
        }

        var bytes = Encoding.UTF8.GetBytes(payload);
        if (bytes.Length == 0 || bytes.Length > MaxBytes)
        {
            return false;
        }

        if (requireClean && payload.Length != Sanitise(payload).Length)
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
