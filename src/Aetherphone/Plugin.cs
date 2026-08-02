using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Device;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Emote;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Updates;
using Aetherphone.Core.Video;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.Game.Config;
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
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ITextureSubstitutionProvider TextureSubstitution { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static INamePlateGui NamePlateGui { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static IUnlockState UnlockState { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider InteropProvider { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IGamepadState GamepadState { get; private set; } = null!;
    [PluginService] internal static IAetheryteList AetheryteList { get; private set; } = null!;
    internal static Plugin Instance { get; private set; } = null!;
    internal static Configuration Cfg { get; private set; } = null!;
    internal static FontService Fonts { get; private set; } = null!;
    internal static WallpaperLibrary Wallpapers { get; private set; } = null!;
    internal static DeviceStatus Device { get; private set; } = null!;
    internal static UpdateCheckService Updates { get; private set; } = null!;
    private readonly WindowSystem windowSystem = new(AepConstants.Name);
    private readonly PhoneServices services;
    private readonly PhoneShell shell;
    private readonly PhoneWindow phoneWindow;
    private readonly VideoPlayer video;
    private readonly ScreenController screenController;
    private readonly AetherStreamQueue videoQueue;
    private readonly WatchAlongSession watchAlong;
    private readonly VideoDebugWindow videoDebugWindow;
    private readonly AetherStreamScreenWindow screenWindow;
    private readonly UpdateChipWindow updateChipWindow;
    private readonly PhoneEmoteController phoneEmote;
    private readonly TimerNotifier timerNotifier;
    private readonly CalendarReminderService calendarReminders;
    private readonly ClockAlarmService clockAlarms;
    private readonly ReminderService reminders;
    private readonly ScreenshotImportService screenshotImport;
    private readonly IDtrBarEntry dtrEntry;
    private static CommandInfo? primaryCommand;
    private static CommandInfo? aliasCommand;
    private bool autoOpenPending;
    private int sampleCounter;

    public Plugin()
    {
        try
        {
            Instance = this;
            ConfigMigrations.Run(PluginInterface.ConfigFile);
            Cfg = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Cfg.NormalizeAethernetBaseUrl();
            Cfg.MigrateSoundSettings();
            Cfg.MigrateChangelogSeen();
            Cfg.MigrateMessage();
            Cfg.MigrateMessagesMerge();
            Cfg.MigrateSetupCompleted();
            Cfg.MigrateControlPanelRepack();
            Cfg.MigrateCharacterSessions();
            InitializeLocalization();
            Device = new DeviceStatus(ClientState, ObjectTable, DataManager);
            services = PhoneServices.Build(Cfg, ChatGui, DataManager, ObjectTable, ClientState, Framework, DutyState,
                TextureProvider, PluginInterface.ConfigDirectory, UnlockState, Condition);
            Fonts = new FontService(PluginInterface, Cfg, services.Loading, Cfg.TextZoom);
            EmojiCatalog.Load();
            Wallpapers = services.Wallpapers;
            screenController = new ScreenController(() => Cfg.VideoHideNameplates);
            video = new VideoPlayer(screenController.Engine);
            videoQueue = new AetherStreamQueue(video);
            watchAlong = new WatchAlongSession(services.AethernetSession, Cfg, services.Confirm, video,
                videoQueue, services.StreamSignals, screenController);
            Framework.Update += OnVideoFrameworkUpdate;
            videoDebugWindow = new VideoDebugWindow(video, screenController);
            screenWindow = new AetherStreamScreenWindow(video);
            var bundle = AppRegistry.BuildDefault(services, video, screenController, videoQueue, watchAlong,
                screenWindow);
            shell = new PhoneShell(services, bundle);
            screenshotImport = new ScreenshotImportService(bundle.Photos, Cfg);
            phoneWindow = new PhoneWindow(shell, Cfg);
            Updates = new UpdateCheckService(services.Http, PluginInterface);
            updateChipWindow = new UpdateChipWindow(phoneWindow, Updates, services.Themes);
            windowSystem.AddWindow(phoneWindow);
            windowSystem.AddWindow(updateChipWindow);
            windowSystem.AddWindow(videoDebugWindow);
            windowSystem.AddWindow(screenWindow);
            services.Visibility.Bind(() => phoneWindow is { IsOpen: true, IsMinimized: false });
            phoneEmote = new PhoneEmoteController(Cfg, Framework, ObjectTable, Condition, DataManager,
                () => services.Visibility.IsVisible);
            timerNotifier = new TimerNotifier(Cfg, Framework, services.Notifications, services.Installer.Gate("timers"));
            calendarReminders = new CalendarReminderService(Cfg, Framework, services.Notifications,
                services.Installer.Gate("calendar"));
            clockAlarms = new ClockAlarmService(Cfg, Framework, services.Notifications,
                services.Installer.Gate("clock"));
            reminders = new ReminderService(Cfg, Framework, services.Notifications, services.Installer.Gate("notes"));
            services.CharacterSwitcher.Start();
            services.CharacterWatch.Start();
            services.Calls.IncomingCallPresented += OnIncomingCall;
            services.Calls.Start();
            dtrEntry = DtrBar.Get(AepConstants.Name);
            dtrEntry.OnClick = _ => phoneWindow.ToggleShell();
            services.Notifications.Changed += UpdateDtrBadge;
            UpdateDtrBadge();
            services.MarketIndex.EnsureBuilt();
            ContextMenu.OnMenuOpened += OnMenuOpened;
            primaryCommand = new CommandInfo(OnCommand) { HelpMessage = Loc.T(L.Plugin.CommandHelp) };
            aliasCommand = new CommandInfo(OnCommand) { HelpMessage = Loc.T(L.Plugin.CommandHelpAlias) };
            CommandManager.AddHandler(AepConstants.PrimaryCommand, primaryCommand);
            CommandManager.AddHandler(AepConstants.AliasCommand, aliasCommand);
            PluginInterface.UiBuilder.Draw += windowSystem.Draw;
            PluginInterface.UiBuilder.Draw += FilePicker.Draw;
            PluginInterface.UiBuilder.OpenMainUi += phoneWindow.ToggleShell;
            PluginInterface.UiBuilder.DisableGposeUiHide = Cfg.ShowInGpose;
            ClientState.Login += OnLogin;

            if (Cfg.OpenOnStartup && ClientState.IsLoggedIn)
            {
                QueueAutoOpen();
            }
        }
        catch
        {
            try
            {
                TearDownPartialConstruction();
            }
            catch (Exception cleanupException)
            {
                Log.Error(cleanupException, "Partial construction teardown failed.");
            }

            throw;
        }
    }

    private void TearDownPartialConstruction()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.Draw -= FilePicker.Draw;
        if (phoneWindow is not null)
        {
            PluginInterface.UiBuilder.OpenMainUi -= phoneWindow.ToggleShell;
        }

        ClientState.Login -= OnLogin;
        Framework.Update -= OnAutoOpenTick;
        Framework.Update -= OnVideoFrameworkUpdate;
        ContextMenu.OnMenuOpened -= OnMenuOpened;
        CommandManager.RemoveHandler(AepConstants.PrimaryCommand);
        CommandManager.RemoveHandler(AepConstants.AliasCommand);
        if (services is not null)
        {
            services.Notifications.Changed -= UpdateDtrBadge;
            services.Calls.IncomingCallPresented -= OnIncomingCall;
        }

        dtrEntry?.Remove();
        windowSystem.RemoveAllWindows();
        videoDebugWindow?.Dispose();
        screenWindow?.Dispose();
        screenController?.Dispose();
        watchAlong?.Dispose();
        video?.Dispose();
        // Must come after video.Dispose() - ScreenPainter (owned by VideoEngine) still unsubscribes
        // from DxHandler.OnPresent during its own teardown, so the hook needs to still be alive then.
        DxHandler.Dispose();
        phoneEmote?.Dispose();
        timerNotifier?.Dispose();
        calendarReminders?.Dispose();
        clockAlarms?.Dispose();
        reminders?.Dispose();
        Updates?.Dispose();
        shell?.Dispose();
        screenshotImport?.Dispose();
        services?.Dispose();
        Device?.Dispose();
        Fonts?.Dispose();
    }

    private void OnLogin()
    {
        if (!Cfg.OpenOnStartup)
        {
            return;
        }

        QueueAutoOpen();
    }

    private void QueueAutoOpen()
    {
        if (phoneWindow.IsOpen)
        {
            return;
        }

        autoOpenPending = true;
        Framework.Update -= OnAutoOpenTick;
        Framework.Update += OnAutoOpenTick;
    }

    private void OnVideoFrameworkUpdate(IFramework framework)
    {
        screenController.OnFrameworkUpdate();
        videoQueue.OnFrameworkUpdate();
        watchAlong.OnFrameworkUpdate((float)framework.UpdateDelta.TotalSeconds);
    }

    private void OnAutoOpenTick(IFramework framework)
    {
        if (!autoOpenPending)
        {
            Framework.Update -= OnAutoOpenTick;
            return;
        }

        if (ObjectTable.LocalPlayer is null || Condition[ConditionFlag.BetweenAreas] ||
            Condition[ConditionFlag.BetweenAreas51])
        {
            return;
        }

        autoOpenPending = false;
        Framework.Update -= OnAutoOpenTick;
        if (phoneWindow.IsOpen)
        {
            return;
        }

        if (Cfg.OpenMinimizedOnStartup)
        {
            phoneWindow.StartMinimized();
        }
        else
        {
            phoneWindow.Maximize();
        }

        phoneWindow.IsOpen = true;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.Draw -= FilePicker.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= phoneWindow.ToggleShell;
        ClientState.Login -= OnLogin;
        Framework.Update -= OnAutoOpenTick;
        Framework.Update -= OnVideoFrameworkUpdate;
        services.Notifications.Changed -= UpdateDtrBadge;
        services.Calls.IncomingCallPresented -= OnIncomingCall;
        ContextMenu.OnMenuOpened -= OnMenuOpened;
        dtrEntry.Remove();
        phoneWindow.PersistPositions();
        windowSystem.RemoveAllWindows();
        videoDebugWindow.Dispose();
        screenWindow.Dispose();
        screenController.Dispose();
        watchAlong.Dispose();
        video.Dispose();
        // Must come after video.Dispose() - ScreenPainter (owned by VideoEngine) still unsubscribes
        // from DxHandler.OnPresent during its own teardown, so the hook needs to still be alive then.
        DxHandler.Dispose();
        phoneEmote.Dispose();
        timerNotifier.Dispose();
        calendarReminders.Dispose();
        clockAlarms.Dispose();
        reminders.Dispose();
        screenshotImport.Dispose();
        Updates.Dispose();
        shell.Dispose();
        services.Dispose();
        Device.Dispose();
        Fonts.Dispose();
        CommandManager.RemoveHandler(AepConstants.PrimaryCommand);
        CommandManager.RemoveHandler(AepConstants.AliasCommand);
    }

    public static void OnLanguageChanged()
    {
        TimeText.ApplyClockPreference(Cfg.Use24HourClock);
        if (primaryCommand is not null)
        {
            primaryCommand.HelpMessage = Loc.T(L.Plugin.CommandHelp);
        }

        if (aliasCommand is not null)
        {
            aliasCommand.HelpMessage = Loc.T(L.Plugin.CommandHelpAlias);
        }
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
        TimeText.ApplyClockPreference(Cfg.Use24HourClock);
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

        if (argument.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            phoneWindow.Recenter();
            return;
        }

        if (argument.Equals("videodebug", StringComparison.OrdinalIgnoreCase))
        {
            videoDebugWindow.IsOpen = true;
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

    private void OnIncomingCall()
    {
        phoneWindow.Maximize();
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
        phoneWindow.IsOpen = true;
        shell.OpenApp("market");
    }

    private void OpenMarket(string query)
    {
        if (query.Length > 0)
        {
            services.MarketLauncher.RequestSearch(query);
        }

        phoneWindow.Maximize();
        phoneWindow.IsOpen = true;
        shell.OpenApp("market");
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
