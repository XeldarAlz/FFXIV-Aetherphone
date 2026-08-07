using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Moderation;

internal sealed class ModerationNoticePresenter : IDisposable
{
    private const string SettingsAppId = "settings";

    private readonly ModerationNoticeService notices;
    private readonly ConfirmService confirm;
    private readonly NotificationService notifications;
    private readonly AccountStateService accountState;
    private readonly IFramework framework;
    private readonly HashSet<string> presented = new(StringComparer.Ordinal);

    public ModerationNoticePresenter(ModerationNoticeService notices, ConfirmService confirm,
        NotificationService notifications, AccountStateService accountState, IFramework framework)
    {
        this.notices = notices;
        this.confirm = confirm;
        this.notifications = notifications;
        this.accountState = accountState;
        this.framework = framework;
        framework.Update += OnFrameworkTick;
    }

    private void OnFrameworkTick(IFramework _)
    {
        var pending = notices.Pending;
        if (pending.Length == 0)
        {
            if (presented.Count > 0)
            {
                presented.Clear();
            }

            return;
        }

        presented.RemoveWhere(id => !Contains(pending, id));

        for (var index = 0; index < pending.Length; index++)
        {
            var notice = pending[index];
            if (!presented.Add(notice.Id))
            {
                continue;
            }

            Present(notice);
        }
    }

    private static bool Contains(ModerationNoticeDto[] pending, string noticeId)
    {
        for (var index = 0; index < pending.Length; index++)
        {
            if (string.Equals(pending[index].Id, noticeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void Present(ModerationNoticeDto notice)
    {
        if (notice.Kind == ModerationNoticeKinds.BadgeGranted || notice.Kind == ModerationNoticeKinds.BadgeRevoked)
        {
            accountState.RefreshNow();
        }

        var title = ModerationNoticeText.Title(notice);
        var body = ModerationNoticeText.Body(notice);

        notifications.Notify(new PhoneNotification(SettingsAppId, title, body, DateTime.Now,
            AppAccents.For(SettingsAppId), notice.Id));

        if (!ModerationNoticeText.IsBlocking(notice))
        {
            notices.Acknowledge(notice);
            return;
        }

        confirm.Alert(title, body, Loc.T(L.Moderation.RemovedDismiss), () => notices.Acknowledge(notice));
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkTick;
    }
}
