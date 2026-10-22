using Aetherphone.Core.Apps;

namespace Aetherphone.Apps.Prism;

internal static class PrismShowcase
{
    public const long Seconds = 3L * 24 * 60 * 60;

    public static HomeTileEffect Effect(long endsAtUnix, bool opened, long nowUnix)
    {
        if (nowUnix >= endsAtUnix)
        {
            return HomeTileEffect.None;
        }

        return opened ? HomeTileEffect.Glow : HomeTileEffect.Spectrum;
    }
}
