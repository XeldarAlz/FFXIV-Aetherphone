using Aetherphone.Core.Maps;
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

            var body = messages[index].Body ?? string.Empty;
            ImGui.SetClipboardText(LocationShare.TryParse(body, out var location)
                ? LocationShare.Summary(location)
                : body);
            return;
        }
    }
}
