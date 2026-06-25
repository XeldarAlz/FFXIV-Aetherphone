using System.IO;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Device;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
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
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal static Plugin Instance { get; private set; } = null!;
    internal static Configuration Cfg { get; private set; } = null!;
    internal static FontService Fonts { get; private set; } = null!;
    internal static WallpaperLibrary Wallpapers { get; private set; } = null!;
    internal static WallpaperImageCache WallpaperImages { get; private set; } = null!;
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
        InitializeLocalization();
        Fonts = new FontService(PluginInterface, Cfg.TextZoom);
        Loc.LanguageChanged += Fonts.OnLanguageChanged;
        var builtInWallpaperDirectory = new DirectoryInfo(Path.Combine(PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Wallpapers"));
        var customWallpaperDirectory = new DirectoryInfo(Path.Combine(PluginInterface.ConfigDirectory.FullName, "Wallpapers"));
        Wallpapers = new WallpaperLibrary(TextureProvider, builtInWallpaperDirectory, customWallpaperDirectory, Cfg);
        WallpaperImages = new WallpaperImageCache();
        Device = new DeviceStatus(ClientState, ObjectTable, DataManager);

        services = PhoneServices.Build(Cfg, ChatGui, DataManager, ObjectTable, ClientState, TextureProvider, PluginInterface.ConfigDirectory);
        aboutWindow = new AboutWindow();
        shell = new PhoneShell(services.Themes, AppRegistry.BuildDefault(services, ShowAbout), services.Notifications, services.Playback);
        phoneWindow = new PhoneWindow(shell) { IsOpen = Cfg.OpenOnStartup };
        windowSystem.AddWindow(phoneWindow);
        windowSystem.AddWindow(aboutWindow);

        dtrEntry = DtrBar.Get(AepConstants.Name);
        dtrEntry.OnClick = _ => phoneWindow.Toggle();
        services.Notifications.Changed += UpdateDtrBadge;
        UpdateDtrBadge();

        services.MarketIndex.EnsureBuilt();
        ContextMenu.OnMenuOpened += OnMenuOpened;

        CommandManager.AddHandler(AepConstants.PrimaryCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = Loc.T(L.Plugin.CommandHelp)
        });
        CommandManager.AddHandler(AepConstants.AliasCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = Loc.T(L.Plugin.CommandHelpAlias)
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += phoneWindow.Toggle;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= phoneWindow.Toggle;

        Loc.LanguageChanged -= Fonts.OnLanguageChanged;
        services.Notifications.Changed -= UpdateDtrBadge;
        ContextMenu.OnMenuOpened -= OnMenuOpened;
        dtrEntry.Remove();

        windowSystem.RemoveAllWindows();
        shell.Dispose();
        services.Dispose();
        Device.Dispose();
        Fonts.Dispose();
        Wallpapers.Dispose();
        WallpaperImages.Dispose();

        CommandManager.RemoveHandler(AepConstants.PrimaryCommand);
        CommandManager.RemoveHandler(AepConstants.AliasCommand);
    }

    private static void InitializeLocalization()
    {
        var directory = Path.Combine(PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Localization");
        if (string.IsNullOrEmpty(Cfg.Language))
        {
            Cfg.Language = DetectLanguage();
            Cfg.Save();
        }

        Loc.Initialize(Cfg.Language, directory);
    }

    private static string DetectLanguage()
    {
        return ClientState.ClientLanguage switch
        {
            Dalamud.Game.ClientLanguage.German => "de",
            Dalamud.Game.ClientLanguage.French => "fr",
            _ => "en",
        };
    }

    private void UpdateDtrBadge()
    {
        var unread = services.Notifications.UnreadCount;
        dtrEntry.Text = unread > 0 ? Loc.T(L.Plugin.DtrBadge, unread) : Loc.T(L.Plugin.Dtr);
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

        if (argument.StartsWith("market", StringComparison.OrdinalIgnoreCase))
        {
            var query = argument.Length > 6 ? argument.Substring(6).Trim() : string.Empty;
            OpenMarket(query);
            return;
        }

        phoneWindow.Toggle();
    }

    private void ShowAbout() => aboutWindow.IsOpen = true;

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        var itemId = ResolveContextItem(args);
        if (itemId == 0 || !services.MarketIndex.TryGet(itemId, out _))
        {
            return;
        }

        args.AddMenuItem(new MenuItem
        {
            Name = Loc.T(L.Plugin.SearchTheMarket),
            OnClicked = _ => OpenMarketAt(itemId),
        });
    }

    private static uint ResolveContextItem(IMenuOpenedArgs args)
    {
        if (args.Target is MenuTargetInventory inventory && inventory.TargetItem is { } targetItem)
        {
            return targetItem.ItemId;
        }

        var hovered = GameGui.HoveredItem;
        return hovered == 0 ? 0u : (uint)(hovered % 1_000_000);
    }

    private void OpenMarketAt(uint itemId)
    {
        services.MarketLauncher.RequestItem(itemId);
        phoneWindow.IsOpen = true;
        shell.OpenApp("market");
    }

    private void OpenMarket(string query)
    {
        if (query.Length > 0)
        {
            services.MarketLauncher.RequestSearch(query);
        }

        phoneWindow.IsOpen = true;
        shell.OpenApp("market");
    }

    private void SendSampleNotification()
    {
        sampleCounter++;
        var accent = new Vector4(0.30f, 0.78f, 0.42f, 1f);
        services.Notifications.Notify(new PhoneNotification("messages", "Alisaie", $"Sample message #{sampleCounter}", DateTime.Now, accent));
    }
}
