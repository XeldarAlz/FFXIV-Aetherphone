using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Core.Onboarding;

internal sealed class OnboardingDirector
{
    private const float TextSeconds = 0.38f;
    private const float PresenceSmoothTime = 0.15f;
    private const float AnchorMissGrace = 0.4f;
    private const float AnchorSmoothing = 16f;
    private readonly INavigator navigation;
    private readonly CoachmarkOverlay coachmark = new();
    private GuideSequence? active;
    private int stepIndex;
    private GuideSequence? suspended;
    private int suspendedIndex;
    private bool pendingWelcome;
    private bool pendingResume;
    private string? pendingAppId;
    private bool suppressed = true;
    private float frameDelta;
    private Spring presence;
    private bool exiting;
    private bool exitCompletes;
    private float textClock;
    private Vector2 anchorMin;
    private Vector2 anchorMax;
    private bool anchorInitialized;
    private float missTimer;

    public OnboardingDirector(INavigator navigation)
    {
        this.navigation = navigation;
    }

    public bool CapturesPointer => active.HasValue && !suppressed;
    public bool WantsAnchors => active.HasValue && !suppressed;
    public bool WantsControlCenter => active is { } sequence && sequence.Steps[stepIndex].OverControlCenter;

    public void OnPhoneOpened()
    {
        if (!OnboardingState.Enabled)
        {
            return;
        }

        if (suspended.HasValue)
        {
            pendingResume = true;
            return;
        }

        var welcome = TourRegistry.GetWelcome();
        if (!OnboardingState.HasCompleted(welcome.Id, welcome.ContentVersion))
        {
            pendingWelcome = true;
        }
    }

    public void OnAppOpened(string appId)
    {
        if (!OnboardingState.Enabled)
        {
            return;
        }

        if (!TourRegistry.TryGetAppTour(appId, out var tour))
        {
            return;
        }

        if (OnboardingState.HasCompleted(tour.Id, tour.ContentVersion))
        {
            return;
        }

        pendingAppId = appId;
    }

    public void Suspend()
    {
        if (!active.HasValue)
        {
            return;
        }

        if (exiting)
        {
            CompleteExit();
            return;
        }

        suspended = active;
        suspendedIndex = stepIndex;
        active = null;
        presence.SnapTo(0f);
    }

    public void Advance(float delta, bool busy, bool atHome, string? currentAppId)
    {
        frameDelta = MathF.Min(delta, TransitionTiming.MaxFrameSeconds);
        if (!OnboardingState.Enabled)
        {
            active = null;
            suspended = null;
            pendingWelcome = false;
            pendingResume = false;
            pendingAppId = null;
            suppressed = true;
            exiting = false;
            presence.SnapTo(0f);
            GuideIntents.Clear();
            return;
        }

        suppressed = busy;
        if (active.HasValue)
        {
            var current = active.Value;
            if (current.RequiredAppId is not null && currentAppId != current.RequiredAppId)
            {
                if (exiting)
                {
                    CompleteExit();
                }

                active = null;
                exiting = false;
                presence.SnapTo(0f);
                GuideIntents.Clear();
                return;
            }

            if (!busy)
            {
                presence.Step(exiting ? 0f : 1f, PresenceSmoothTime, frameDelta);
                textClock = MathF.Min(textClock + frameDelta, TextSeconds);
                if (exiting && presence.IsResting(0f, 0.01f, 0.05f))
                {
                    CompleteExit();
                }
            }

            return;
        }

        if (busy)
        {
            return;
        }

        if (OnboardingState.ConsumeReplayWelcome())
        {
            pendingWelcome = true;
        }

        if (pendingResume && suspended.HasValue && CanStart(suspended.Value, atHome, currentAppId))
        {
            active = suspended;
            stepIndex = suspendedIndex;
            suspended = null;
            pendingResume = false;
            ResetForTour();
            return;
        }

        if (pendingWelcome && atHome)
        {
            pendingWelcome = false;
            Start(TourRegistry.GetWelcome());
            return;
        }

        if (pendingAppId is not null && currentAppId == pendingAppId && !TourHolds.IsHeld(pendingAppId))
        {
            var appId = pendingAppId;
            pendingAppId = null;
            if (TourRegistry.TryGetAppTour(appId, out var tour) &&
                !OnboardingState.HasCompleted(tour.Id, tour.ContentVersion))
            {
                Start(tour);
            }
        }
    }

    public void Draw(Rect screen, PhoneTheme theme)
    {
        if (!active.HasValue || suppressed)
        {
            return;
        }

        var sequence = active.Value;
        var step = sequence.Steps[stepIndex];
        var presenceValue = Math.Clamp(presence.Value, 0f, 1f);
        if (presenceValue <= 0.005f && exiting)
        {
            return;
        }

        var textProgress = Math.Clamp(textClock / TextSeconds, 0f, 1f);
        var anchor = ResolveAnchor(step);
        var result = coachmark.Draw(screen, theme, step, anchor, presenceValue, textProgress, stepIndex,
            sequence.Steps.Length, !exiting);
        if (exiting)
        {
            return;
        }

        switch (result)
        {
            case CoachmarkAction.Advance:
                step.OnAdvance?.Invoke(navigation);
                stepIndex++;
                if (stepIndex >= sequence.Steps.Length)
                {
                    stepIndex = sequence.Steps.Length - 1;
                    BeginExit(true);
                }
                else
                {
                    ResetForStep();
                }

                break;
            case CoachmarkAction.Skip:
                BeginExit(false);
                break;
        }
    }

    private void Start(GuideSequence sequence)
    {
        active = sequence;
        stepIndex = 0;
        ResetForTour();
    }

    private void BeginExit(bool completesCoveredTours)
    {
        exiting = true;
        exitCompletes = completesCoveredTours;
        textClock = TextSeconds;
    }

    private void CompleteExit()
    {
        exiting = false;
        if (active is not { } sequence)
        {
            return;
        }

        OnboardingState.MarkCompleted(sequence.Id, sequence.ContentVersion);
        if (exitCompletes && sequence.CompletesOnFinish is { } covered)
        {
            for (var index = 0; index < covered.Length; index++)
            {
                if (TourRegistry.TryGetAppTour(covered[index], out var tour))
                {
                    OnboardingState.MarkCompleted(tour.Id, tour.ContentVersion);
                }
            }
        }

        active = null;
    }

    private void ResetForTour()
    {
        presence.SnapTo(0f);
        exiting = false;
        anchorInitialized = false;
        missTimer = 0f;
        textClock = 0f;
        coachmark.Reset();
    }

    private void ResetForStep()
    {
        textClock = 0f;
        missTimer = 0f;
        anchorInitialized = false;
    }

    private static bool CanStart(in GuideSequence sequence, bool atHome, string? currentAppId) =>
        sequence.RequiredAppId is null ? atHome : currentAppId == sequence.RequiredAppId;

    private Rect? ResolveAnchor(in GuideStep step)
    {
        if (step.AnchorKey is null)
        {
            anchorInitialized = false;
            return null;
        }

        if (UiAnchors.TryGet(step.AnchorKey, out var rect))
        {
            missTimer = 0f;
            if (!anchorInitialized)
            {
                anchorMin = rect.Min;
                anchorMax = rect.Max;
                anchorInitialized = true;
            }
            else
            {
                var blend = 1f - MathF.Exp(-AnchorSmoothing * frameDelta);
                anchorMin = Vector2.Lerp(anchorMin, rect.Min, blend);
                anchorMax = Vector2.Lerp(anchorMax, rect.Max, blend);
            }

            return new Rect(anchorMin, anchorMax);
        }

        if (anchorInitialized && missTimer < AnchorMissGrace)
        {
            missTimer += frameDelta;
            return new Rect(anchorMin, anchorMax);
        }

        anchorInitialized = false;
        return null;
    }
}
