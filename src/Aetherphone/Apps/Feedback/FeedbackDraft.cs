namespace Aetherphone.Apps.Feedback;

internal sealed class FeedbackDraft
{
    public const int MaxLength = 1000;
    public const int MaxAttachments = 5;

    private readonly List<string> attachments = new(MaxAttachments);

    public string Text = string.Empty;
    public FeedbackCategory Category = FeedbackCategory.Bug;
    public bool IncludeDeviceInfo = true;

    public IReadOnlyList<string> Attachments => attachments;

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public bool IsEmpty => Text.Length == 0 && attachments.Count == 0;

    public bool IsFull => attachments.Count >= MaxAttachments;

    public int IndexOf(string path)
    {
        for (var index = 0; index < attachments.Count; index++)
        {
            if (string.Equals(attachments[index], path, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    public bool Add(string path)
    {
        if (string.IsNullOrEmpty(path) || IsFull || IndexOf(path) >= 0)
        {
            return false;
        }

        attachments.Add(path);
        return true;
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= attachments.Count)
        {
            return;
        }

        attachments.RemoveAt(index);
    }

    public string[] SnapshotAttachments() => attachments.ToArray();

    public void Clear()
    {
        Text = string.Empty;
        attachments.Clear();
    }
}
