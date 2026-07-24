using Dalamud.Bindings.ImGui;
using System.Diagnostics;

namespace Aetherphone.Windows;

internal static class UrlActions
{
    public static void OpenInBrowser(string url, Action<Exception>? onError = null)
    {
        if (!IsWebUrl(url))
        {
            onError?.Invoke(new NotSupportedException("Only http and https links can be opened."));
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ImGui.SetClipboardText(url);
            onError?.Invoke(ex);
        }
    }

    public static void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, $"Failed to open the folder: {path}");
        }
    }

    private static bool IsWebUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return parsed.Host.Length > 0;
    }
}
