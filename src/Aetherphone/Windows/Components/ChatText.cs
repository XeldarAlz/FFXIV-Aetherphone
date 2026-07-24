using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;

namespace Aetherphone.Windows.Components;

internal static class ChatText
{
    private const int VoiceKind = 3;
    private const int ImageKind = 1;
    private const int PostKind = 4;
    private const int StoryReplyKind = 5;
    private const int PreviewLength = 90;

    public const int LocationKind = 6;

    public const int MusterKind = 7;

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

        if (kind == MusterKind || MusterShare.IsToken(text))
        {
            return Loc.T(L.Muster.InvitePreview);
        }

        return UiText.Truncate(text.Replace('\n', ' ').Replace('\r', ' '), PreviewLength);
    }

    public static int EffectiveKind(string? body, int kind)
    {
        if (kind != 0)
        {
            return kind;
        }

        if (LocationShare.IsToken(body))
        {
            return LocationKind;
        }

        return MusterShare.IsToken(body) ? MusterKind : kind;
    }

    public static string ListPreview(string? text)
    {
        if (LocationShare.IsToken(text))
        {
            return Loc.T(L.DirectMessages.LocationPreview);
        }

        if (MusterShare.IsToken(text))
        {
            return Loc.T(L.Muster.InvitePreview);
        }

        return text ?? string.Empty;
    }
}
