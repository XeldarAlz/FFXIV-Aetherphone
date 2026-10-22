using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Aethernet;

internal static class InstallSourceNotice
{
    public static void Poll(AethernetSession session, ConfirmService confirm)
    {
        var notice = session.ConsumeSourceNotice();
        if (notice is null)
        {
            return;
        }

        Ask(notice, confirm);
    }

    private static void Ask(string notice, ConfirmService confirm)
    {
        var blocked = string.Equals(notice, AethernetClientIdentity.StatusBlocked, StringComparison.Ordinal);
        var url = AepConstants.OfficialRepositoryUrl;
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(blocked ? L.Account.FailSourceBlockedTitle : L.Account.SourceWarnedTitle),
            Message = Loc.T(blocked ? L.Account.FailSourceBlockedBody : L.Account.SourceWarnedBody),
            Sections = new[] { ConfirmSection.Card(Loc.T(L.Account.SourceOfficialRepoLabel), url) },
            ConfirmLabel = Loc.T(L.Account.SourceCopyLink),
            CancelLabel = Loc.T(L.Account.FailDismiss),
            Danger = false,
            Confirm = () =>
            {
                ImGui.SetClipboardText(url);
                ShellToast.Show();
            },
        });
    }
}
