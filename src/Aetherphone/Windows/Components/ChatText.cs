using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;

namespace Aetherphone.Windows.Components;

internal static class ChatText
{
    private const int VoiceKind = 3;
    private const int ImageKind = 1;
    private const int PostKind = 4;
    private const int StoryReplyKind = 5;
    private const int PreviewLength = 90;

    public const int LocationKind = 6;

    public static string QuotePreview(string? body, int kind)
    {
        var text = body ?? string.Empty;
        if (kind == VoiceKind)
        {
            return Loc.T(L.DirectMessages.VoicePreview);
        }

        if (kind == PostKind)
        {
            return Loc.T(L.DirectMessages.PostPreview);
        }

        if (kind == StoryReplyKind)
        {
            return Loc.T(L.DirectMessages.StoryReplyPreview);
        }

        if (kind == ImageKind && text.Length == 0)
        {
            return Loc.T(L.DirectMessages.PhotoPreview);
        }

        if (kind == LocationKind || LocationShare.IsToken(text))
        {
            return Loc.T(L.DirectMessages.LocationPreview);
        }

        return UiText.Truncate(text.Replace('\n', ' ').Replace('\r', ' '), PreviewLength);
    }

    public static int EffectiveKind(string? body, int kind)
    {
        return kind == 0 && LocationShare.IsToken(body) ? LocationKind : kind;
    }

    public static string ListPreview(string? text)
    {
        if (LocationShare.IsToken(text))
        {
            return Loc.T(L.DirectMessages.LocationPreview);
        }

        return text ?? string.Empty;
    }
}
