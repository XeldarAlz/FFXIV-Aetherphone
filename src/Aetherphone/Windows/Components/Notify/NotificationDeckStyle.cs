using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal readonly struct NotificationDeckStyle
{
    public readonly bool Glass;
    public readonly GlassTone Tone;
    public readonly Vector4 CardFill;
    public readonly Vector4 LayerFill;
    public readonly Vector4 Ink;
    public readonly Vector4 MutedInk;
    public readonly Vector4 Accent;
    public readonly Vector4 Danger;

    private NotificationDeckStyle(bool glass, GlassTone tone, Vector4 cardFill, Vector4 layerFill, Vector4 ink,
        Vector4 mutedInk, Vector4 accent, Vector4 danger)
    {
        Glass = glass;
        Tone = tone;
        CardFill = cardFill;
        LayerFill = layerFill;
        Ink = ink;
        MutedInk = mutedInk;
        Accent = accent;
        Danger = danger;
    }

    public static NotificationDeckStyle ForGlass(PhoneTheme theme, GlassTone tone) =>
        new(true, tone, default, default, NotificationCard.Ink(tone, theme), NotificationCard.MutedInk(tone, theme),
            theme.Accent, theme.Danger);

    public static NotificationDeckStyle ForCards(Vector4 cardFill, Vector4 layerFill, Vector4 ink, Vector4 mutedInk,
        Vector4 accent, Vector4 danger) =>
        new(false, GlassTone.Dark, cardFill, layerFill, ink, mutedInk, accent, danger);
}
