namespace Aetherphone.Core.Aethernet;

internal static class AccountSelection
{
    public static ulong Target(IReadOnlyDictionary<ulong, CharacterSession> sessions, bool followsCharacter,
        ulong pinned, ulong playing)
    {
        if (followsCharacter || pinned == 0)
        {
            return playing;
        }

        if (!sessions.TryGetValue(pinned, out var stored) || stored.Token.Length == 0)
        {
            return playing;
        }

        return pinned;
    }
}
