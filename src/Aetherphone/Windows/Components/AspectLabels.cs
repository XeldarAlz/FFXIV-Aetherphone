using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;

namespace Aetherphone.Windows.Components;

internal static class AspectLabels
{
    public static LocString For(PostAspect aspect) => aspect switch
    {
        PostAspect.Portrait => L.Social.AspectPortrait,
        PostAspect.Landscape => L.Social.AspectLandscape,
        _ => L.Social.AspectSquare,
    };
}
