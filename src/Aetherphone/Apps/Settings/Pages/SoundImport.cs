using Aetherphone.Core.Platform;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class SoundImport
{
    private string? pendingPath;

    public void Launch(string title)
    {
        NativeFileDialog.PickAudio(title, path => Interlocked.Exchange(ref pendingPath, path));
    }

    public bool TryTake(out string path)
    {
        var taken = Interlocked.Exchange(ref pendingPath, null);
        path = taken ?? string.Empty;
        return taken is not null;
    }
}
