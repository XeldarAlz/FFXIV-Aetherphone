using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Shortcuts;

internal static class ShortcutRunText
{
    public static string Name(string name) => name.Length > 0 ? name : Loc.T(L.Shortcuts.Untitled);

    public static string Status(in ShortcutRunView view)
    {
        if (view.Holding)
        {
            return Loc.T(L.Shortcuts.RunHolding);
        }

        if (view.StepCount > 1)
        {
            return Loc.T(L.Shortcuts.RunStep, view.Step, view.StepCount);
        }

        return Loc.T(L.Shortcuts.Running);
    }

    public static string Outcome(ShortcutRunOutcome outcome, string name) => outcome switch
    {
        ShortcutRunOutcome.Completed => Loc.T(L.Shortcuts.RunDone, Name(name)),
        ShortcutRunOutcome.PluginUnavailable => Loc.T(L.Shortcuts.RunPluginMissing),
        ShortcutRunOutcome.LinkRejected => Loc.T(L.Shortcuts.RunLinkRejected),
        ShortcutRunOutcome.NotLoggedIn => Loc.T(L.Shortcuts.RunNotLoggedIn),
        ShortcutRunOutcome.GameBusy => Loc.T(L.Shortcuts.RunGameBusy),
        _ => Loc.T(L.Shortcuts.RunRejected),
    };
}
