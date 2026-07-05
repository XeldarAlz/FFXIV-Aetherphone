using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Core.Onboarding;

internal sealed class OnboardingDirector
{
    private const float EnterSeconds = 0.34f;
    private const float AnchorMissGrace = 0.4f;
    private const float AnchorSmoothing = 16f;
    private readonly INavigator navigation;
    private GuideSequence? active;
    private int stepIndex;
    private GuideSequence? suspended;
    private int suspendedIndex;
    private bool pendingWelcome;
    private bool pendingResume;
    private string? pendingAppId;
    private bool suppressed = true;
    private float frameDelta;
    private float enterClock;
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

        suspended = active;
        suspendedIndex = stepIndex;
        active = null;
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
            return;
        }

        suppressed = busy;
        if (active.HasValue)
        {
            var current = active.Value;
            if (current.RequiredAppId is not null && currentAppId != current.RequiredAppId)
            {
                active = null;
                return;
            }

            if (!busy)
            {
                enterClock = MathF.Min(enterClock + frameDelta, EnterSeconds);
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
            ResetForStep();
            return;
        }

        if (pendingWelcome && atHome)
        {
            pendingWelcome = false;
            Start(TourRegistry.GetWelcome());
            return;
        }

        if (pendingAppId is not null && currentAppId == pendingAppId)
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
        var progress = Math.Clamp(enterClock / EnterSeconds, 0f, 1f);
        var anchor = ResolveAnchor(step);
        var result = CoachmarkOverlay.Draw(screen, theme, step, anchor, progress, stepIndex, sequence.Steps.Length);
        switch (result)
        {
            case CoachmarkAction.Advance:
                step.OnAdvance?.Invoke(navigation);
                stepIndex++;
                if (stepIndex >= sequence.Steps.Length)
                {
                    Finish(sequence);
                }
                else
                {
                    ResetForStep();
                }

                break;
            case CoachmarkAction.Skip:
                Complete(sequence);
                break;
        }
    }

    private void Start(GuideSequence sequence)
    {
        active = sequence;
        stepIndex = 0;
        ResetForStep();
    }

    private void Complete(GuideSequence sequence)
    {
        OnboardingState.MarkCompleted(sequence.Id, sequence.ContentVersion);
        active = null;
    }

    private void Finish(GuideSequence sequence)
    {
        OnboardingState.MarkCompleted(sequence.Id, sequence.ContentVersion);
        if (sequence.CompletesOnFinish is { } covered)
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

    private void ResetForStep()
    {
        enterClock = 0f;
        anchorInitialized = false;
        missTimer = 0f;
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
                var t = 1f - MathF.Exp(-AnchorSmoothing * frameDelta);
                anchorMin = Vector2.Lerp(anchorMin, rect.Min, t);
                anchorMax = Vector2.Lerp(anchorMax, rect.Max, t);
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
