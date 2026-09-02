namespace Aetherphone.Core.Game;

internal static class GameMemory
{
    public static bool Attached { get; private set; } = true;

    public static void Detach() => Attached = false;
}
