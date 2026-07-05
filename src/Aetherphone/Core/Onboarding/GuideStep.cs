using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal enum GuideSurface
{
    FullCard,
    Coachmark,
}

internal enum GuideAdvance
{
    Button,
    TapTarget,
}

internal readonly struct GuideStep
{
    public readonly LocString Title;
    public readonly LocString Body;
    public readonly LocString ButtonLabel;
    public readonly string? AnchorKey;
    public readonly GuideSurface Surface;
    public readonly GuideAdvance Advance;
    public readonly Action<INavigator>? OnAdvance;

    public GuideStep(LocString title, LocString body, LocString buttonLabel, string? anchorKey, GuideSurface surface,
        GuideAdvance advance, Action<INavigator>? onAdvance)
    {
        Title = title;
        Body = body;
        ButtonLabel = buttonLabel;
        AnchorKey = anchorKey;
        Surface = surface;
        Advance = advance;
        OnAdvance = onAdvance;
    }

    public static GuideStep Page(LocString title, LocString body, LocString buttonLabel) =>
        new(title, body, buttonLabel, null, GuideSurface.FullCard, GuideAdvance.Button, null);

    public static GuideStep Note(LocString title, LocString body) =>
        new(title, body, L.Onboarding.GotIt, null, GuideSurface.Coachmark, GuideAdvance.Button, null);

    public static GuideStep Point(LocString title, LocString body, string anchorKey) =>
        new(title, body, L.Onboarding.GotIt, anchorKey, GuideSurface.Coachmark, GuideAdvance.Button, null);

    public static GuideStep Tap(LocString title, LocString body, string anchorKey, Action<INavigator> onAdvance) =>
        new(title, body, L.Onboarding.Continue, anchorKey, GuideSurface.Coachmark, GuideAdvance.TapTarget, onAdvance);
}
