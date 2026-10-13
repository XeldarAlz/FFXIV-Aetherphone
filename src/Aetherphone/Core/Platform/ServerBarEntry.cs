using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Platform;

internal sealed class ServerBarEntry : IDisposable
{
    public const string DefaultText = "Aetherphone";
    public const int MaxTextLength = 20;
    public const BitmapFontIcon Icon = BitmapFontIcon.Aethernet;

    private readonly IDtrBarEntry entry;
    private readonly Configuration configuration;
    private readonly NotificationService notifications;
    private int unread;

    public ServerBarEntry(IDtrBar bar, Configuration configuration, NotificationService notifications, Action onClick)
    {
        this.configuration = configuration;
        this.notifications = notifications;
        entry = bar.Get(AepConstants.Name);
        entry.OnClick = _ => onClick();
        notifications.Changed += Refresh;
        configuration.BadgeSettingsChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        unread = configuration.IsAppBadgeEnabled(NotificationChannels.NotificationsAppId)
            ? notifications.UnreadCount
            : 0;
        entry.Text = BuildText();
        entry.Tooltip = BuildTooltip();
    }

    public void Dispose()
    {
        notifications.Changed -= Refresh;
        configuration.BadgeSettingsChanged -= Refresh;
        entry.Remove();
    }

    private SeString BuildText()
    {
        var style = configuration.ServerBarStyle;
        var builder = new SeStringBuilder();
        if (style != ServerBarStyle.Text)
        {
            builder.AddIcon(Icon);
        }

        builder.AddText(Label());
        if (unread > 0)
        {
            builder.AddText(string.Concat(" ", unread.ToString(Loc.Culture)));
        }

        return builder.Build();
    }

    private string Label()
    {
        if (configuration.ServerBarStyle == ServerBarStyle.Icon)
        {
            return AepConstants.ServerBarTag;
        }

        var text = string.IsNullOrWhiteSpace(configuration.ServerBarText)
            ? DefaultText
            : configuration.ServerBarText.Trim();
        return AepConstants.ServerBarTag.Length > 0 ? string.Concat(text, " ", AepConstants.ServerBarTag) : text;
    }

    private SeString BuildTooltip()
    {
        var unreadLine = unread > 0
            ? Loc.Plural(L.Plugin.ServerBarUnread, unread)
            : Loc.T(L.Plugin.ServerBarNoUnread);
        return new SeStringBuilder()
            .AddText(AepConstants.Name)
            .Add(NewLinePayload.Payload)
            .AddText(unreadLine)
            .Add(NewLinePayload.Payload)
            .AddText(Loc.T(L.Plugin.ServerBarClickHint))
            .Build();
    }
}
