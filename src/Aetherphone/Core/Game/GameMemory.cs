namespace Aetherphone.Core.Game;

internal static class GameMemory
{
    public static bool Attached { get; private set; } = true;

    public static SimulatedGame? Simulated { get; private set; }

    public static event Action<string>? ActionSimulated;

    public static void Detach() => Attached = false;

    public static void Simulate(SimulatedGame? game)
    {
        Attached = false;
        Simulated = game;
    }

    public static bool SimulateAction(string description)
    {
        if (Simulated is null)
        {
            return false;
        }

        ActionSimulated?.Invoke(description);
        return true;
    }
}
