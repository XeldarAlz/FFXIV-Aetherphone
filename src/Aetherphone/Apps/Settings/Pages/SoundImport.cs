using Aetherphone.Core.Platform;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class SoundImport
{
    private string? pendingPath;

    public void Launch(string title)
    {
        _ = NativeFileDialog.OpenAudioAsync(title).ContinueWith(task =>
        {
            if (task.Status == TaskStatus.RanToCompletion && !string.IsNullOrEmpty(task.Result))
            {
                Interlocked.Exchange(ref pendingPath, task.Result);
            }
        });
    }

    public bool TryTake(out string path)
    {
        var taken = Interlocked.Exchange(ref pendingPath, null);
        path = taken ?? string.Empty;
        return taken is not null;
    }
}
