using Aetherphone.Core.Animation;

namespace Aetherphone.Windows.Components;

internal sealed class ChatEntranceTracker
{
    private struct Entrance
    {
        public int Line;
        public float Elapsed;
    }

    private readonly List<Entrance> active = new();
    private object? trackedKey;
    private int settled;
    private bool primed;
    private string? lastTailId;

    public void Sync(object key, int lineCount, float deltaSeconds)
    {
        Sync(key, lineCount, null, deltaSeconds, loading: false);
    }

    public void Sync(object key, int lineCount, string? tailId, float deltaSeconds, bool loading)
    {
        if (!Equals(key, trackedKey))
        {
            trackedKey = key;
            settled = lineCount;
            primed = lineCount > 0 || !loading;
            lastTailId = tailId;
            active.Clear();
            return;
        }

        if (!primed)
        {
            settled = lineCount;
            primed = lineCount > 0 || !loading;
            lastTailId = tailId;
            return;
        }

        if (lineCount > settled && tailId is not null && tailId == lastTailId)
        {
            active.Clear();
            settled = lineCount;
            return;
        }

        lastTailId = tailId;
        if (lineCount < settled)
        {
            settled = lineCount;
        }

        while (settled < lineCount)
        {
            active.Add(new Entrance { Line = settled, Elapsed = 0f });
            settled++;
        }

        for (var index = active.Count - 1; index >= 0; index--)
        {
            var entrance = active[index];
            entrance.Elapsed += deltaSeconds;
            if (entrance.Elapsed >= TransitionTiming.BubbleSeconds || entrance.Line >= lineCount)
            {
                active.RemoveAt(index);
            }
            else
            {
                active[index] = entrance;
            }
        }
    }

    public float Progress(int line)
    {
        for (var index = 0; index < active.Count; index++)
        {
            if (active[index].Line == line)
            {
                return active[index].Elapsed / TransitionTiming.BubbleSeconds;
            }
        }

        return 1f;
    }
}
