namespace Aetherphone.Core.Feedback;

internal sealed class FeedbackUpdateMarks
{
    public bool Initialized { get; set; }
    public long NotifiedUnix { get; set; }
    public bool HasHistory { get; set; }
    public Dictionary<string, long> Unseen { get; set; } = new(StringComparer.Ordinal);
}
