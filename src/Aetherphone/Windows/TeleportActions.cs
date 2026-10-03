using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Venues;

namespace Aetherphone.Windows;

internal static class TeleportActions
{
    public static void AskThenTravel(ConfirmService confirm, string placeName, string address, string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        var where = ConfirmSection.Chip(Loc.T(L.Common.OpenLinkDestination),
            address.Length > 0 ? address : destination);
        if (!LifestreamBridge.IsAvailable())
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = Loc.T(L.Common.TravelNeedsLifestreamTitle),
                Message = string.Empty,
                Sections = [ConfirmSection.Paragraph(Loc.T(L.Common.TravelNeedsLifestream)), where],
                ConfirmLabel = Loc.T(L.Common.TravelLifestreamPage),
                CancelLabel = Loc.T(L.Common.Close),
                Danger = false,
                Confirm = () => UrlActions.OpenInBrowser(LifestreamBridge.ProjectUrl),
            });
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Common.TravelTitle, placeName),
            Message = string.Empty,
            Sections = [ConfirmSection.Paragraph(Loc.T(L.Common.TravelWarning)), where],
            ConfirmLabel = Loc.T(L.Common.TravelConfirm),
            CancelLabel = Loc.T(L.Common.Cancel),
            Confirm = () => LifestreamBridge.Travel(destination),
        });
    }
}
