using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;
using System.Diagnostics;

namespace Aetherphone.Windows;

internal static class UrlActions
{
    public static void OpenInBrowser(string url, Action<Exception>? onError = null)
    {
        if (!IsWebUrl(url))
        {
            var rejected = new NotSupportedException("Only http and https links can be opened.");
            AepLog.Warning(rejected, $"Refused to open a non-web link: {url}");
            onError?.Invoke(rejected);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"Opening {url} in a browser failed; copied it to the clipboard instead");
            ImGui.SetClipboardText(url);
            onError?.Invoke(exception);
        }
    }

    public static void AskThenOpen(ConfirmService confirm, string url)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Common.OpenLinkTitle),
            Message = string.Empty,
            Sections =
            [
                ConfirmSection.Paragraph(Loc.T(L.Common.OpenLinkWarning)),
                ConfirmSection.Chip(Loc.T(L.Common.OpenLinkDestination), url),
            ],
            ConfirmLabel = Loc.T(L.Common.OpenLinkConfirm),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            Confirm = () => OpenInBrowser(url),
        });
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
