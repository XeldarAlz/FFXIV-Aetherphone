using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Aethergram;

internal static class AethergramInk
{
    private static readonly SocialInk Standard = new(AppPalettes.Aethergram);
    private static readonly SocialInk BloodMoon = new(AppPalettes.AethergramBloodMoon);

    public static SocialInk Shared => SeasonalTheme.Halloween ? BloodMoon : Standard;
    public static AppPalette CurrentPalette => SeasonalTheme.Halloween ? AppPalettes.AethergramBloodMoon : AppPalettes.Aethergram;

    public static Vector4 MutedInk => Shared.MutedInk;

    public static readonly Vector4 SeenRing = new(1f, 1f, 1f, 0.28f);

    public static readonly Vector4[] StoryRingStops =
    [
        new(1f, 0.863f, 0.502f, 1f), new(0.969f, 0.435f, 0.216f, 1f), new(0.882f, 0.188f, 0.424f, 1f),
        new(0.514f, 0.227f, 0.706f, 1f),
    ];

    public static readonly Vector4[] BloodMoonStoryRingStops =
    [
        new(1f, 0.416f, 0.333f, 1f), new(0.878f, 0.149f, 0.243f, 1f), new(0.427f, 0.027f, 0.086f, 1f),
        new(0.765f, 0.078f, 0.169f, 1f),
    ];

    public static Vector4[] StoryRings => SeasonalTheme.Halloween ? BloodMoonStoryRingStops : StoryRingStops;
}
