using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Device;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Aetherphone;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal static Plugin Instance { get; private set; } = null!;
    internal static Configuration Cfg { get; private set; } = null!;
    internal static FontService Fonts { get; private set; } = null!;
    internal static WallpaperTextureCache Wallpapers { get; private set; } = null!;
    internal static DeviceStatus Device { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new(AepConstants.Name);
    private readonly PhoneServices services;
    private readonly PhoneShell shell;
    private readonly PhoneWindow phoneWindow;
    private readonly AboutWindow aboutWindow;
    private readonly IDtrBarEntry dtrEntry;

    private int sampleCounter;

    public Plugin()
    {
        Instance = this;
        Cfg = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Fonts = new FontService(PluginInterface);
        Wallpapers = new WallpaperTextureCache(TextureProvider);
        Device = new DeviceStatus(ClientState, ObjectTable, DataManager);

        services = PhoneServices.Build(Cfg, NotificationManager, ChatGui, DataManager, ObjectTable, ClientState, TextureProvider);
        aboutWindow = new AboutWindow();
        shell = new PhoneShell(services.Themes, AppRegistry.BuildDefault(services, ShowAbout));
        phoneWindow = new PhoneWindow(shell) { IsOpen = Cfg.OpenOnStartup };
        windowSystem.AddWindow(phoneWindow);
        windowSystem.AddWindow(aboutWindow);

        dtrEntry = DtrBar.Get(AepConstants.Name);
        dtrEntry.OnClick = _ => phoneWindow.Toggle();
        services.Notifications.Changed += UpdateDtrBadge;
        UpdateDtrBadge();

        CommandManager.AddHandler(AepConstants.PrimaryCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Aetherphone. /phone about opens credits & links, /phone test sends a sample notification."
        });
        CommandManager.AddHandler(AepConstants.AliasCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Alias for /phone."
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += phoneWindow.Toggle;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= phoneWindow.Toggle;

        services.Notifications.Changed -= UpdateDtrBadge;
        dtrEntry.Remove();

        windowSystem.RemoveAllWindows();
        shell.Dispose();
        services.Dispose();
        Device.Dispose();
        Fonts.Dispose();
        Wallpapers.Dispose();

        CommandManager.RemoveHandler(AepConstants.PrimaryCommand);
        CommandManager.RemoveHandler(AepConstants.AliasCommand);
    }

    private void UpdateDtrBadge()
    {
        var unread = services.Notifications.UnreadCount;
        dtrEntry.Text = unread > 0 ? $"Phone {unread}" : "Phone";
    }

    private void OnCommand(string command, string arguments)
    {
        var argument = arguments.Trim();

        if (argument.Equals("test", StringComparison.OrdinalIgnoreCase))
        {
            SendSampleNotification();
            return;
        }

        if (argument.Equals("about", StringComparison.OrdinalIgnoreCase))
        {
            ShowAbout();
            return;
        }

        phoneWindow.Toggle();
    }

    private void ShowAbout() => aboutWindow.IsOpen = true;

    private void SendSampleNotification()
    {
        sampleCounter++;
        var accent = new Vector4(0.30f, 0.78f, 0.42f, 1f);
        services.Notifications.Notify(new PhoneNotification("messages", "Alisaie", $"Sample message #{sampleCounter}", DateTime.Now, accent));
    }
}
