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
    Action,
}

internal enum GuideCondition
{
    None,
    AppOpened,
    AtHome,
    MinimizeRoundTrip,
    TapAnchor,
    AnchorVisible,
}

internal enum HeroMotif
{
    Constellation,
    Care,
    Finale,
    AppIcon,
}

internal enum GuideGesture
{
    None,
    Tap,
    Hold,
    SwipeDown,
    SwipeUp,
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
    public readonly HeroMotif Hero;
    public readonly bool OverControlCenter;
    public readonly GuideGesture Gesture;
    public readonly string? SecondaryAnchorKey;
    public readonly GuideCondition Condition;
    public readonly string? WaitAnchorKey;

    public GuideStep(LocString title, LocString body, LocString buttonLabel, string? anchorKey, GuideSurface surface,
        GuideAdvance advance, Action<INavigator>? onAdvance, HeroMotif hero = HeroMotif.Constellation,
        bool overControlCenter = false, GuideGesture gesture = GuideGesture.None, string? secondaryAnchorKey = null,
        GuideCondition condition = GuideCondition.None, string? waitAnchorKey = null)
    {
        Title = title;
        Body = body;
        ButtonLabel = buttonLabel;
        AnchorKey = anchorKey;
        Surface = surface;
        Advance = advance;
        OnAdvance = onAdvance;
        Hero = hero;
        OverControlCenter = overControlCenter;
        Gesture = gesture;
        SecondaryAnchorKey = secondaryAnchorKey;
        Condition = condition;
        WaitAnchorKey = waitAnchorKey;
    }

    public bool IsAction => Advance == GuideAdvance.Action;

    public static GuideStep Page(LocString title, LocString body, LocString buttonLabel,
        HeroMotif hero = HeroMotif.Constellation) =>
        new(title, body, buttonLabel, null, GuideSurface.FullCard, GuideAdvance.Button, null, hero);

    public static GuideStep Note(LocString title, LocString body) =>
        new(title, body, L.Onboarding.GotIt, null, GuideSurface.Coachmark, GuideAdvance.Button, null);

    public static GuideStep Note(LocString title, LocString body, string intent) =>
        new(title, body, L.Onboarding.GotIt, null, GuideSurface.Coachmark, GuideAdvance.Button,
            _ => GuideIntents.Post(intent));

    public static GuideStep Point(LocString title, LocString body, string anchorKey) =>
        new(title, body, L.Onboarding.GotIt, anchorKey, GuideSurface.Coachmark, GuideAdvance.Button, null);

    public static GuideStep Point(LocString title, LocString body, string anchorKey, GuideGesture gesture) =>
        new(title, body, L.Onboarding.Continue, anchorKey, GuideSurface.Coachmark, GuideAdvance.Button, null,
            gesture: gesture);

    public static GuideStep Try(LocString title, LocString body, string anchorKey, GuideGesture gesture,
        GuideCondition condition) =>
        new(title, body, L.Onboarding.Continue, anchorKey, GuideSurface.Coachmark, GuideAdvance.Action, null,
            gesture: gesture, condition: condition);

    public static GuideStep Intro(LocString title, LocString body) =>
        new(title, body, L.Onboarding.TakeTour, null, GuideSurface.FullCard, GuideAdvance.Button, null,
            HeroMotif.AppIcon);

    public static GuideStep TryTap(LocString title, LocString body, string anchorKey) =>
        new(title, body, L.Onboarding.Continue, anchorKey, GuideSurface.Coachmark, GuideAdvance.Action, null,
            gesture: GuideGesture.Tap, condition: GuideCondition.TapAnchor);

    public static GuideStep TryUntil(LocString title, LocString body, string anchorKey, GuideGesture gesture,
        string waitAnchorKey) =>
        new(title, body, L.Onboarding.Continue, anchorKey, GuideSurface.Coachmark, GuideAdvance.Action, null,
            gesture: gesture, condition: GuideCondition.AnchorVisible, waitAnchorKey: waitAnchorKey);

    public static GuideStep Span(LocString title, LocString body, string anchorKey, string secondaryAnchorKey) =>
        new(title, body, L.Onboarding.Continue, anchorKey, GuideSurface.Coachmark, GuideAdvance.Button, null,
            secondaryAnchorKey: secondaryAnchorKey);

    public static GuideStep Tap(LocString title, LocString body, string anchorKey, Action<INavigator> onAdvance) =>
        new(title, body, L.Onboarding.Continue, anchorKey, GuideSurface.Coachmark, GuideAdvance.TapTarget, onAdvance,
            gesture: GuideGesture.Tap);

    public static GuideStep Tap(LocString title, LocString body, string anchorKey, string intent) =>
        new(title, body, L.Onboarding.Continue, anchorKey, GuideSurface.Coachmark, GuideAdvance.TapTarget,
            _ => GuideIntents.Post(intent), gesture: GuideGesture.Tap);

    public static GuideStep ControlCenterNote(LocString title, LocString body, string closeIntent) =>
        new(title, body, L.Onboarding.Continue, null, GuideSurface.Coachmark, GuideAdvance.Button,
            _ => GuideIntents.Post(closeIntent), HeroMotif.Constellation, true);
}
