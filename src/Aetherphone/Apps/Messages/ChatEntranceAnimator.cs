using Aetherphone.Core.Animation;
using Aetherphone.Core.Messaging;

namespace Aetherphone.Apps.Messages;

internal sealed class ChatEntranceAnimator
{
    private struct Entrance
    {
        public int Line;
        public float Elapsed;
    }

    private readonly List<Entrance> active = new();

    private Conversation? tracked;
    private int settledCount;

    public void Sync(Conversation conversation, float deltaSeconds)
    {
        if (!ReferenceEquals(conversation, tracked))
        {
            tracked = conversation;
            settledCount = conversation.Lines.Count;
            active.Clear();
            return;
        }

        var lineCount = conversation.Lines.Count;
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
