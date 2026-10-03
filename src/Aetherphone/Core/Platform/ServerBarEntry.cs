using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherphone.Core.Platform;

internal sealed unsafe class ServerBarEntry : IDisposable
{
    private const string AddonName = "_DTR";
    private const FontAwesomeIcon IdleIcon = FontAwesomeIcon.Mobile;
    private const FontAwesomeIcon UnreadIcon = FontAwesomeIcon.MobileVibrate;
    private const float IconPixels = 15f;
    private const string IconSlot = "     ";
    private const ushort IconSlotWidth = 17;
    private const uint IconInk = 0xFFFFFFFF;
    private const uint IconShadow = 0xA0000000;

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
        entry.MinimumWidth = IconSlotWidth;
        notifications.Changed += Refresh;
        configuration.BadgeSettingsChanged += Refresh;
        Plugin.PluginInterface.UiBuilder.Draw += Draw;
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
        Plugin.PluginInterface.UiBuilder.Draw -= Draw;
        notifications.Changed -= Refresh;
        configuration.BadgeSettingsChanged -= Refresh;
        entry.Remove();
    }

    private void Draw()
    {
        if (Plugin.GameGui.GameUiHidden)
        {
            return;
        }

        var bounds = entry.ScreenBounds;
        if (bounds.Max == Vector2.Zero)
        {
            return;
        }

        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AddonName).Address;
        if (addon == null || !addon->IsVisible || addon->RootNode == null)
        {
            return;
        }

        var scale = addon->RootNode->ScaleX;
        var slotHeight = (bounds.Max.Y - bounds.Min.Y) * scale;
        var drawList = ImGui.GetBackgroundDrawList();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = IconGlyph.Of(unread > 0 ? UnreadIcon : IdleIcon);
            var fontSize = IconPixels * scale;
            var size = ImGui.CalcTextSize(glyph) * (fontSize / ImGui.GetFontSize());
            var position = new Vector2(
                MathF.Round(bounds.Min.X + MathF.Max(0f, (IconSlotWidth * scale - size.X) * 0.5f)),
                MathF.Round(bounds.Min.Y + (slotHeight - size.Y) * 0.5f));
            var shadowOffset = MathF.Max(1f, MathF.Round(scale));
            drawList.AddText(UiBuilder.IconFont, fontSize,
                new Vector2(position.X + shadowOffset, position.Y + shadowOffset), IconShadow, glyph);
            drawList.AddText(UiBuilder.IconFont, fontSize, position, IconInk, glyph);
        }
    }

    private SeString BuildText()
    {
        var builder = new SeStringBuilder().AddText(string.Concat(IconSlot, AepConstants.ServerBarTag));
        if (unread > 0)
        {
            builder.AddText(string.Concat(" ", unread.ToString(Loc.Culture)));
        }

        return builder.Build();
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
