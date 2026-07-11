using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Device;
using Aetherphone.Core.Emote;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Report;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows;
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
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ITextureSubstitutionProvider TextureSubstitution { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    internal static Plugin Instance { get; private set; } = null!;
    internal static Configuration Cfg { get; private set; } = null!;
    internal static FontService Fonts { get; private set; } = null!;
    internal static LoadingScreen Loading { get; private set; } = null!;
    internal static WallpaperLibrary Wallpapers { get; private set; } = null!;
    internal static WallpaperImageCache WallpaperImages { get; private set; } = null!;
    internal static DeviceStatus Device { get; private set; } = null!;
    internal static IAnalyticsService Analytics { get; private set; } = null!;
    internal static ConfirmService Confirm { get; private set; } = null!;
    internal static ReportService Report { get; private set; } = null!;
    private readonly WindowSystem windowSystem = new(AepConstants.Name);
    private readonly PhoneServices services;
    private readonly PhoneShell shell;
    private readonly PhoneWindow phoneWindow;
    private readonly AboutWindow aboutWindow;
    private readonly PhoneEmoteController phoneEmote;
    private readonly TimerNotifier timerNotifier;
    private readonly CalendarReminderService calendarReminders;
    private readonly ClockAlarmService clockAlarms;
    private readonly ReminderService reminders;
    private readonly DateTime sessionStartedAt;
    private readonly IDtrBarEntry dtrEntry;
    private int sampleCounter;

    public Plugin()
    {
        Instance = this;
        Cfg = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Cfg.NormalizeAethernetBaseUrl();
        Cfg.MigrateSoundSettings();
        Cfg.MigrateChangelogSeen();
        Cfg.MigrateMessage();
        Cfg.MigrateMessagesMerge();
        Cfg.MigrateSetupCompleted();
        Cfg.MigrateControlPanelRepack();
        InitializeLocalization();
        Fonts = new FontService(PluginInterface, Cfg.TextZoom);
        Loading = new LoadingScreen();
        var builtInWallpaperDirectory =
            new DirectoryInfo(
                Path.Combine(PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Wallpapers"));
        var customWallpaperDirectory =
            new DirectoryInfo(Path.Combine(PluginInterface.ConfigDirectory.FullName, "Wallpapers"));
        Wallpapers = new WallpaperLibrary(TextureProvider, builtInWallpaperDirectory, customWallpaperDirectory, Cfg);
        WallpaperImages = new WallpaperImageCache();
        Device = new DeviceStatus(ClientState, ObjectTable, DataManager);
        services = PhoneServices.Build(Cfg, ChatGui, DataManager, ObjectTable, ClientState, Framework, TextureProvider,
            PluginInterface.ConfigDirectory);
        sessionStartedAt = DateTime.UtcNow;
        Analytics = services.Analytics;
        if (Analytics.IsFirstRun)
        {
            Analytics.Track(AnalyticsEvents.FirstRun());
        }

        Analytics.Track(AnalyticsEvents.SessionStart(BuildSessionProperties()));
        aboutWindow = new AboutWindow();
        Confirm = new ConfirmService();
        Report = new ReportService();
        shell = new PhoneShell(services.Themes, AppRegistry.BuildDefault(services, ShowAbout), services.Notifications,
            services.Playback, services.Calls, services.MessageLauncher, services.VelvetLauncher,
            services.DmLauncher, services.SocialLauncher, Confirm, Report, services.AethernetSession,
            services.AethernetClient, services.GameData, services.RemoteImages, services.Lodestone);
        phoneWindow = new PhoneWindow(shell) { IsOpen = Cfg.OpenOnStartup };
        if (Cfg.OpenOnStartup)
        {
            phoneWindow.MarkOpenTrigger("startup");
            if (Cfg.OpenMinimizedOnStartup)
            {
                phoneWindow.StartMinimized();
            }
        }

        windowSystem.AddWindow(phoneWindow);
        windowSystem.AddWindow(aboutWindow);
        phoneEmote = new PhoneEmoteController(Cfg, Framework, ObjectTable, Condition, DataManager,
            () => phoneWindow.IsOpen && !phoneWindow.IsMinimized);
        timerNotifier = new TimerNotifier(Cfg, Framework, services.Notifications);
        calendarReminders = new CalendarReminderService(Cfg, Framework, services.Notifications);
        clockAlarms = new ClockAlarmService(Cfg, Framework, services.Notifications);
        reminders = new ReminderService(Cfg, Framework, services.Notifications);
        services.AethernetClient.EnsureCurrentUser();
        services.Calls.IncomingCallPresented += OnIncomingCall;
        services.Calls.Start();
        dtrEntry = DtrBar.Get(AepConstants.Name);
        dtrEntry.OnClick = _ => phoneWindow.ToggleShell();
        services.Notifications.Changed += UpdateDtrBadge;
        UpdateDtrBadge();
        services.MarketIndex.EnsureBuilt();
        ContextMenu.OnMenuOpened += OnMenuOpened;
        CommandManager.AddHandler(AepConstants.PrimaryCommand,
            new CommandInfo(OnCommand) { HelpMessage = Loc.T(L.Plugin.CommandHelp) });
        CommandManager.AddHandler(AepConstants.AliasCommand,
            new CommandInfo(OnCommand) { HelpMessage = Loc.T(L.Plugin.CommandHelpAlias) });
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += phoneWindow.ToggleShell;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= phoneWindow.ToggleShell;
        services.Notifications.Changed -= UpdateDtrBadge;
        services.Calls.IncomingCallPresented -= OnIncomingCall;
        ContextMenu.OnMenuOpened -= OnMenuOpened;
        dtrEntry.Remove();
        windowSystem.RemoveAllWindows();
        phoneEmote.Dispose();
        timerNotifier.Dispose();
        calendarReminders.Dispose();
        clockAlarms.Dispose();
        reminders.Dispose();
        shell.Dispose();
        var sessionDuration = (DateTime.UtcNow - sessionStartedAt).TotalSeconds;
        Analytics.Track(AnalyticsEvents.SessionEnd(sessionDuration));
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
        switch (ClientState.ClientLanguage)
        {
            case Dalamud.Game.ClientLanguage.German:
                return "de";
            case Dalamud.Game.ClientLanguage.French:
                return "fr";
            case Dalamud.Game.ClientLanguage.Japanese:
                return "ja";
        }

        var osLanguage = System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        for (var index = 0; index < Languages.All.Length; index++)
        {
            if (string.Equals(Languages.All[index].Code, osLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return Languages.All[index].Code;
            }
        }

        return "en";
    }

    private void UpdateDtrBadge()
    {
        var unread = services.Notifications.UnreadCount;
        dtrEntry.Text = unread > 0 ? $"{AepConstants.Name} ({unread})" : AepConstants.Name;
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

        if (argument.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            phoneWindow.Recenter();
            return;
        }

        if (argument.StartsWith("market", StringComparison.OrdinalIgnoreCase))
        {
            var query = argument.Length > 6 ? argument.Substring(6).Trim() : string.Empty;
            OpenMarket(query);
            return;
        }

        phoneWindow.ToggleShell();
    }

    private void ShowAbout() => aboutWindow.IsOpen = true;

    private Dictionary<string, string> BuildSessionProperties()
    {
        var properties = new Dictionary<string, string>(8);

        if (Cfg.Language.Length > 0)
        {
            properties["language"] = Cfg.Language;
        }

        properties["theme"] = Cfg.ThemeMode.ToString().ToLowerInvariant();
        properties["scale"] = Cfg.PhoneScale.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        properties["wallpaper"] = Cfg.DarkWallpaperId;
        properties["tutorials"] = Cfg.TutorialsEnabled ? "1" : "0";
        properties["logged_in"] = Cfg.AethernetToken.Length > 0 ? "1" : "0";
        properties["dnd"] = Cfg.DoNotDisturb ? "1" : "0";
        properties["calls"] = Cfg.CallsEnabled ? "1" : "0";

        return properties;
    }

    private void OnIncomingCall()
    {
        phoneWindow.Maximize();
        phoneWindow.MarkOpenTrigger("call");
        phoneWindow.IsOpen = true;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        var itemId = ResolveContextItem(args);
        if (itemId == 0 || !services.MarketIndex.TryGet(itemId, out _))
        {
            return;
        }

        args.AddMenuItem(
            new MenuItem { Name = Loc.T(L.Plugin.SearchTheMarket), OnClicked = _ => OpenMarketAt(itemId), });
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
        phoneWindow.Maximize();
        phoneWindow.MarkOpenTrigger("command");
        phoneWindow.IsOpen = true;
        shell.OpenApp("market", AppOpenSource.Command);
    }

    private void OpenMarket(string query)
    {
        if (query.Length > 0)
        {
            services.MarketLauncher.RequestSearch(query);
        }

        phoneWindow.Maximize();
        phoneWindow.MarkOpenTrigger("command");
        phoneWindow.IsOpen = true;
        shell.OpenApp("market", AppOpenSource.Command);
    }

    private static readonly string[] SampleSenders = { "Alisaie", "Y'shtola", "Thancred" };

    private void SendSampleNotification()
    {
        sampleCounter++;
        var accent = new Vector4(0.30f, 0.78f, 0.42f, 1f);
        var sender = SampleSenders[sampleCounter % SampleSenders.Length];
        services.Notifications.Notify(new PhoneNotification("messages", sender, $"Sample message #{sampleCounter}",
            DateTime.Now, accent, $"{sender}@Sample"));
    }
}
