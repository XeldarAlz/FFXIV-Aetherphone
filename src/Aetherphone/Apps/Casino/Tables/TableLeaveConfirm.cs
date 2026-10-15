using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Tables;

internal static class TableLeaveConfirm
{
    public static void Ask(ConfirmService confirm, Action leave)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Tables.LeaveTitle),
            Message = Loc.T(L.Tables.LeaveBody),
            ConfirmLabel = Loc.T(L.Tables.LeaveConfirm),
            CancelLabel = Loc.T(L.Strip.KeepPlaying),
            Sheet = true,
            Danger = true,
            Confirm = leave,
        });
    }
}
