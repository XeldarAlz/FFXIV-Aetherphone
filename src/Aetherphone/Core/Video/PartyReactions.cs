namespace Aetherphone.Core.Video;

internal readonly record struct ActiveReaction(int Kind, long StartedAtTicks, float Lane);

internal sealed class PartyReactions
{
    internal const long LifetimeMilliseconds = 2600;

    internal static readonly string[] Tokens = ["heart", "laugh", "wow", ":fire:", ":clap:", "sad"];

    private const int Capacity = 24;

    private readonly ActiveReaction[] active = new ActiveReaction[Capacity];
    private int next;
    private uint laneSeed = 0x9E3779B9u;

    internal ReadOnlySpan<ActiveReaction> Active => active;

    internal void Add(int kind, long nowTicks)
    {
        if (kind < 0 || kind >= Tokens.Length)
        {
            return;
        }

        laneSeed = laneSeed * 1664525u + 1013904223u;
        var lane = ((laneSeed >> 8) & 0xFFFFu) / 65535f;
        active[next] = new ActiveReaction(kind, nowTicks, lane);
        next = (next + 1) % Capacity;
    }

    internal void Clear() => Array.Clear(active);

    internal static bool TryProgress(in ActiveReaction reaction, long nowTicks, out float progress)
    {
        progress = 0f;
        if (reaction.StartedAtTicks == 0)
        {
            return false;
        }

        var elapsed = nowTicks - reaction.StartedAtTicks;
        if (elapsed < 0 || elapsed >= LifetimeMilliseconds)
        {
            return false;
        }

        progress = elapsed / (float)LifetimeMilliseconds;
        return true;
    }
}
