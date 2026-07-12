using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ChatActions
{
    public static void CopyMessageText(ReadOnlySpan<TranscriptMessage> messages, string id, Func<string, bool> canReveal)
    {
        for (var index = 0; index < messages.Length; index++)
        {
            if (messages[index].Id != id)
            {
                continue;
            }

            if (!canReveal(id))
            {
                return;
            }

            ImGui.SetClipboardText(messages[index].Body ?? string.Empty);
            return;
        }
    }
}
