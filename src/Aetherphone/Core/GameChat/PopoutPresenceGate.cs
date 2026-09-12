namespace Aetherphone.Core.GameChat;

internal readonly record struct PresenceSettings(bool HideInCombat, bool HideInDuty, bool FieldOperationsExempt,
    bool HideInCutscene = false, bool HideWhenUiHidden = false);

internal readonly record struct PresenceState(bool InCombat, bool BoundByDuty, bool InFieldOperation,
    bool InCutscene = false, bool UiHidden = false);

internal static class PopoutPresenceGate
{
    public static bool ShouldSuppress(in PresenceState state, in PresenceSettings settings)
    {
        if (settings.HideInCutscene && state.InCutscene)
        {
            return true;
        }

        if (settings.HideWhenUiHidden && state.UiHidden)
        {
            return true;
        }

        if (settings.HideInCombat && state.InCombat)
        {
            return true;
        }

        if (!settings.HideInDuty || !state.BoundByDuty)
        {
            return false;
        }

        return !settings.FieldOperationsExempt || !state.InFieldOperation;
    }
}

internal struct PresenceDebounce
{
    private float held;

    public bool Value { get; private set; }

    public bool Step(bool target, float deltaSeconds, float delaySeconds)
    {
        if (target == Value)
        {
            held = 0f;
            return false;
        }

        held += deltaSeconds;
        if (held < delaySeconds)
        {
            return false;
        }

        held = 0f;
        Value = target;
        return true;
    }
}
