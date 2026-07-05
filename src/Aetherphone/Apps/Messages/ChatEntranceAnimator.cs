using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Messages;

internal sealed class ChatEntranceAnimator
{
    private struct Entrance
    {
        public int Line;
        public float Elapsed;
    }

    private readonly List<Entrance> active = new();
    private object? tracked;
    private int settledCount;

    public void Sync(object thread, int lineCount, float deltaSeconds)
    {
        if (!ReferenceEquals(thread, tracked))
        {
            tracked = thread;
            settledCount = lineCount;
            active.Clear();
            return;
        }

        while (settledCount < lineCount)
        {
            active.Add(new Entrance { Line = settledCount, Elapsed = 0f });
            settledCount++;
        }

        for (var index = active.Count - 1; index >= 0; index--)
        {
            var entrance = active[index];
            entrance.Elapsed += deltaSeconds;
            if (entrance.Elapsed >= TransitionTiming.BubbleSeconds)
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
