namespace Aetherphone.Core.Onboarding;

internal static class UiAnchors
{
    private static readonly Dictionary<string, Rect> Anchors = new();
    private static bool recording;
    public static bool Recording => recording;

    internal static bool ForceRecording { get; set; }

    public static void BeginFrame(bool enabled)
    {
        recording = enabled || ForceRecording;
        Anchors.Clear();
    }

    public static void Report(string key, Rect rect)
    {
        if (!recording)
        {
            return;
        }

        Anchors[key] = rect;
    }

    public static bool TryGet(string key, out Rect rect) => Anchors.TryGetValue(key, out rect);

    internal static void CopyTo(List<KeyValuePair<string, Rect>> into)
    {
        into.Clear();
        foreach (var entry in Anchors)
        {
            into.Add(entry);
        }
    }
}
