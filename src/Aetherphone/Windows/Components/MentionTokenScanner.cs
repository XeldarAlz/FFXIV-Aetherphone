using Aetherphone.Core.Social;

namespace Aetherphone.Windows.Components;

// Finds the @handle token the caret currently sits inside. Runs against the logical string, never the
// wrapped display string, because soft newlines shift every index. The boundary rules mirror the server
// side regex so the client offers exactly the handles the server would resolve.
internal static class MentionTokenScanner
{
    public static bool TryFind(string text, int cursor, out int start, out int length)
    {
        start = 0;
        length = 0;
        if (cursor < 1 || cursor > text.Length)
        {
            return false;
        }

        var index = cursor;
        while (index > 0 && SocialIdentity.IsHandleChar(text[index - 1]))
        {
            index--;
        }

        if (index == 0 || text[index - 1] != '@')
        {
            return false;
        }

        var at = index - 1;
        if (at > 0 && SocialIdentity.IsHandleChar(text[at - 1]))
        {
            return false;
        }

        if (cursor - index > SocialIdentity.HandleMaxLength)
        {
            return false;
        }

        start = at;
        length = cursor - at;
        return true;
    }

    public static string QueryOf(string text, int start, int length)
    {
        return length <= 1 ? string.Empty : text.Substring(start + 1, length - 1);
    }
}
