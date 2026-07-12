using Aetherphone.Core.Localization;

namespace Aetherphone.Windows.Components;

internal static class ChatText
{
    private const int VoiceKind = 3;
    private const int ImageKind = 1;
    private const int PreviewLength = 90;

    public static string QuotePreview(string? body, int kind)
    {
        var text = body ?? string.Empty;
        if (kind == VoiceKind)
        {
            return Loc.T(L.DirectMessages.VoicePreview);
        }

        if (kind == ImageKind && text.Length == 0)
        {
            return Loc.T(L.DirectMessages.PhotoPreview);
        }

        return UiText.Truncate(text.Replace('\n', ' ').Replace('\r', ' '), PreviewLength);
    }
}
