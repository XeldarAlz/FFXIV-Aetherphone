namespace Aetherphone.Core.Jam;

internal sealed class JamReactions
{
    public const int MaxKinds = 8;
    public const int Capacity = 16;

    private readonly JamReaction[] recent = new JamReaction[Capacity];
    private int next;
    private int count;

    public int Version { get; private set; }
    public int Count => count;

    public ref readonly JamReaction NewestAt(int index)
    {
        var slot = (next - 1 - index + Capacity * 2) % Capacity;
        return ref recent[slot];
    }

    public void Add(in JamReaction reaction)
    {
        if (reaction.Kind < 0 || reaction.Kind >= MaxKinds)
        {
            return;
        }

        recent[next] = reaction;
        next = (next + 1) % Capacity;
        count = Math.Min(count + 1, Capacity);
        Version++;
    }

    public void Clear()
    {
        Array.Clear(recent);
        next = 0;
        count = 0;
        Version++;
    }
}
