using System.Reflection;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Emulation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.Syrcus;

internal enum EmulatorShortcutAction : byte
{
    None,
    FastForward,
    SaveState,
    LoadState,
}

internal enum EmulatorBrowserPurpose : byte
{
    None,
    ImportRom,
    ScanFolder,
}

internal sealed partial class SyrcusApp : IMiniGame
{
    private const string GameId = "syrcus";
    private const float SettingsRowHeight = 74f;
    private const float ScreenPadding = 4f;
    private const float PortraitGameplayScreenTopInset = 72f;
    private static readonly Vector4 N64CButtonColor = new(0.88f, 0.68f, 0.16f, 1f);
    private static readonly EmulatorButtons[] BindingOrder =
    {
        EmulatorButtons.Up, EmulatorButtons.Down, EmulatorButtons.Left, EmulatorButtons.Right,
        EmulatorButtons.A, EmulatorButtons.B, EmulatorButtons.X, EmulatorButtons.Y,
        EmulatorButtons.L, EmulatorButtons.R, EmulatorButtons.L2, EmulatorButtons.R2,
        EmulatorButtons.L3, EmulatorButtons.R3,
        EmulatorButtons.Start, EmulatorButtons.Select,
    };
    private static readonly EmulatorLayoutElement[] EditorHitOrder =
    {
        EmulatorLayoutElement.FastForward,
        EmulatorLayoutElement.CUp, EmulatorLayoutElement.CDown,
        EmulatorLayoutElement.CLeft, EmulatorLayoutElement.CRight,
        EmulatorLayoutElement.Start, EmulatorLayoutElement.Select, EmulatorLayoutElement.R,
        EmulatorLayoutElement.L, EmulatorLayoutElement.R2, EmulatorLayoutElement.L2,
        EmulatorLayoutElement.R3, EmulatorLayoutElement.L3,
        EmulatorLayoutElement.X, EmulatorLayoutElement.Y, EmulatorLayoutElement.Dpad2,
        EmulatorLayoutElement.A, EmulatorLayoutElement.B,
        EmulatorLayoutElement.LeftAnalog, EmulatorLayoutElement.RightAnalog,
        EmulatorLayoutElement.Dpad, EmulatorLayoutElement.DsTopScreen,
        EmulatorLayoutElement.DsBottomScreen, EmulatorLayoutElement.Screen,
    };
    private static readonly GamepadButtons[] ShortcutGamepadButtons =
    {
        GamepadButtons.DpadUp, GamepadButtons.DpadDown, GamepadButtons.DpadLeft, GamepadButtons.DpadRight,
        GamepadButtons.North, GamepadButtons.South, GamepadButtons.West, GamepadButtons.East,
        GamepadButtons.L1, GamepadButtons.L2, GamepadButtons.L3,
        GamepadButtons.R1, GamepadButtons.R2, GamepadButtons.R3,
        GamepadButtons.Start, GamepadButtons.Select,
    };

    private const string HomebrewUrl = "https://pdroms.de/";

    private readonly string emulatorRoot;
    private readonly EmulatorCoreProvisioner cores;
    private readonly ConfirmService confirm;
    private readonly RomLibrary library;
    private readonly EmulatorVideoTexture video;
    private readonly KeyboardInputCapture keyboardCapture;
    private readonly IKeyState keyState;
    private readonly IGamepadState gamepadState;
    private readonly Action<object, bool>? gamepadNavigationSetter;
    private readonly Func<object, bool>? gamepadNavigationGetter;
    private readonly IDisposable inputCaptureRegistration;
    private readonly Configuration configuration;
    private readonly DirectoryBrowser directoryBrowser = new();
    private readonly bool[] bindingKeyStates = new bool[256];
    private readonly EmulatorButtons[] visibleBindings = new EmulatorButtons[BindingOrder.Length];
    private readonly EmulatorRecentGame?[] recentGames = new EmulatorRecentGame?[6];
    private readonly string[] systemFilterLabels = new string[EmulatorSystemCatalog.All.Count + 1];
    private readonly HashSet<int> shortcutCaptureKeys = new();
    private readonly Dictionary<string, int> knownGameCounts = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<RomEntry> roms = Array.Empty<RomEntry>();
    private EmulatorSession? session;
    private string? pendingImport;
    private string error = string.Empty;
    private bool inputCaptured;
    private bool gamepadCaptureActive;
    private bool gamepadNavigationWasEnabled;
    private bool gamepadBlockWasEnabled;
    private bool gamepadUsesImGuiFallback;
    private bool gamepadReflectionWarningLogged;
    private volatile string installingSystemId = string.Empty;
    private Action? pendingInstalled;
    private bool phoneInteractive = true;
    private bool gameVisible;
    private bool editingLayout;
    private EmulatorBrowserPurpose browserPurpose;
    private bool layoutDirty;
    private bool fastForwardLatched;
    private bool shortcutWaitingForRelease;
    private bool shortcutHasInput;
    private bool saveStateShortcutWasDown;
    private bool loadStateShortcutWasDown;
    private ushort shortcutCaptureButtons;
    private int hubTab;
    private string selectedSystemId = string.Empty;
    private int stateSlot = 1;
    private string stateMessage = string.Empty;
    private EmulatorButtons bindingTarget;
    private EmulatorLayoutElement? auxiliaryBindingTarget;
    private EmulatorShortcutAction shortcutTarget;
    private EmulatorLayoutElement selectedLayoutElement = EmulatorLayoutElement.Screen;
    private EmulatorLayoutElement? draggedLayoutElement;
    private EmulatorLayoutElement? activeTouchAnalog;
    private Vector2 layoutDragOffset;

    public SyrcusApp(DirectoryInfo configDirectory, ITextureProvider textures, IKeyState keyState,
        IGamepadState gamepadState, Configuration configuration, ConfirmService confirm)
    {
        emulatorRoot = Path.Combine(configDirectory.FullName, "Emulator");
        cores = new EmulatorCoreProvisioner(Path.Combine(emulatorRoot, "cores"),
            Path.Combine(emulatorRoot, "system"));
        this.confirm = confirm;
        library = new RomLibrary(emulatorRoot);
        video = new EmulatorVideoTexture(textures);
        keyboardCapture = new KeyboardInputCapture();
        this.keyState = keyState;
        this.gamepadState = gamepadState;
        var navigationProperty = gamepadState.GetType().GetProperty("NavEnableGamepad",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        gamepadNavigationSetter = CreateSetter(navigationProperty);
        gamepadNavigationGetter = CreateGetter(navigationProperty);
        inputCaptureRegistration = EmulatorInputCaptureBridge.Register(SetPhoneInteractive);
        this.configuration = configuration;
        configuration.Emulator ??= new EmulatorSettings();
        configuration.Emulator.MigrateToPerCoreSettings(EmulatorSystemCatalog.All);
        foreach (var system in EmulatorSystemCatalog.All)
        {
            _ = configuration.Emulator.ForCore(system);
        }

    }

    private EmulatorSystemDefinition CurrentSystem =>
        EmulatorSystemCatalog.ById(selectedSystemId) ?? session?.System ?? EmulatorSystemCatalog.GameBoy;
    private EmulatorSettings Settings => configuration.Emulator.ForCore(CurrentSystem);
    private bool LandscapeMode => Settings.GameplayOrientation == EmulatorGameplayOrientation.Landscape;
    private EmulatorLayoutSettings CurrentLayout => Settings.LayoutFor(Settings.GameplayOrientation);
    private bool HasLeftAnalog => CurrentSystem.InputProfile is EmulatorInputProfile.Nintendo64 or
        EmulatorInputProfile.PlayStation;
    private bool HasRightAnalog => CurrentSystem.InputProfile == EmulatorInputProfile.PlayStation;
    private bool IsNintendoDs => CurrentSystem.InputProfile == EmulatorInputProfile.NintendoDs;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Syrcus);
    public string Genre => Loc.T(L.Games.GenreArcade);
    public bool WantsLandscape => session is not null && gameVisible && LandscapeMode;
    public bool UsesCompactHeader => true;
    public bool WantsImmersiveContent => session is not null && gameVisible;
    public bool WantsStatusBarInImmersiveContent => session is not null && gameVisible && !LandscapeMode;

    public void Open()
    {
        error = string.Empty;
        gameVisible = false;
        editingLayout = false;
        browserPurpose = EmulatorBrowserPurpose.None;
        hubTab = 0;
        selectedSystemId = string.Empty;
        roms = Array.Empty<RomEntry>();
        knownGameCounts.Clear();
    }

    public void Close() => StopGame();

    public bool HandleBack()
    {
        if (bindingTarget != EmulatorButtons.None || auxiliaryBindingTarget is not null ||
            shortcutTarget != EmulatorShortcutAction.None)
        {
            CancelAllBindings();
            return true;
        }

        if (session is not null && gameVisible)
        {
            PauseGame();
            return true;
        }

        if (browserPurpose != EmulatorBrowserPurpose.None)
        {
            browserPurpose = EmulatorBrowserPurpose.None;
            return true;
        }

        if (editingLayout)
        {
            FinishLayoutEditing();
            return true;
        }

        if (!string.IsNullOrEmpty(selectedSystemId))
        {
            selectedSystemId = string.Empty;
            hubTab = 0;
            roms = Array.Empty<RomEntry>();
            return true;
        }

        return false;
    }

    public void Draw(in GameContext context)
    {
        ProcessPendingInstall();
        ProcessPendingImport();
        ProcessKeyBinding();
        ProcessShortcutBinding();
        if (editingLayout)
        {
            DrawLayoutEditor(context);
            return;
        }

        if (browserPurpose != EmulatorBrowserPurpose.None)
        {
            DrawFolderBrowser(context);
            return;
        }

        if (session is not null && gameVisible)
        {
            DrawGame(context);
            return;
        }

        DrawHub(context);
    }

    private void DrawHub(in GameContext context)
    {
        if (string.IsNullOrEmpty(selectedSystemId))
        {
            DrawSystemHub(context);
            return;
        }

        var body = context.Body;
        var theme = context.Theme;
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 25f * scale),
            CurrentSystem.Name, theme.TextStrong, TextStyles.Title2);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 49f * scale),
            Typography.FitText(Loc.T(CurrentSystem.LocalizedDescription), body.Width - 36f * scale,
                TextStyles.Footnote),
            theme.TextMuted, TextStyles.Footnote);

        var tabRow = new Rect(new Vector2(body.Min.X + 16f * scale, body.Min.Y + 64f * scale),
            new Vector2(body.Max.X - 16f * scale, body.Min.Y + 104f * scale));
        var selectedTab = SegmentStrip.Draw($"emulator.{CurrentSystem.Id}.hubTab", tabRow,
            new[] { Loc.T(L.Games.EmulatorGames), Loc.T(L.Games.EmulatorSettings) }, hubTab, theme);
        if (selectedTab != hubTab)
        {
            CancelAllBindings();
            hubTab = selectedTab;
        }

        var contentTop = body.Min.Y + 108f * scale;
        ImGui.SetCursorScreenPos(new Vector2(body.Min.X, contentTop));
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f * scale, 4f * scale)))
        using (var child = ImRaii.Child($"##emulatorCoreHub.{CurrentSystem.Id}",
                   new Vector2(body.Width, body.Max.Y - contentTop), false,
                   ImGuiWindowFlags.NoBackground))
        {
            if (!child)
            {
                return;
            }

            if (hubTab == 0)
            {
                DrawRomLibrary(theme, scale);
            }
            else
            {
                DrawEmulatorSettings(theme, scale);
            }
        }
    }

    private void DrawSystemHub(in GameContext context)
    {
        var body = context.Body;
        var theme = context.Theme;
        var scale = UiScale.Current;
        GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 25f * scale),
            Loc.T(L.Games.SyrcusLibrary), theme.TextStrong, TextStyles.Title2);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 49f * scale),
            Loc.T(L.Games.EmulatorHubHint, EmulatorSystemCatalog.All.Count),
            theme.TextMuted, TextStyles.Footnote);

        var contentTop = body.Min.Y + 64f * scale;
        ImGui.SetCursorScreenPos(new Vector2(body.Min.X, contentTop));
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f * scale, 4f * scale)))
        using (var child = ImRaii.Child("##emulatorRootHub", new Vector2(body.Width, body.Max.Y - contentTop), false,
                   ImGuiWindowFlags.NoBackground))
        {
            if (!child)
            {
                return;
            }

            DrawRecentGames(theme, scale);
            DrawSystemTiles(theme, scale);
        }
    }

    private void DrawRecentGames(PhoneTheme theme, float scale)
    {
        var recentCount = 0;
        for (var index = 0; index < configuration.Emulator.RecentGames.Count &&
                            recentCount < recentGames.Length; index++)
        {
            var candidate = configuration.Emulator.RecentGames[index];
            if (File.Exists(candidate.Path) && EmulatorSystemCatalog.ById(candidate.SystemId) is not null)
            {
                recentGames[recentCount++] = candidate;
            }
        }

        if (recentCount == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Games.RecentlyPlayed), theme);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = 58f * scale;
        for (var index = 0; index < recentCount; index++)
        {
            var item = recentGames[index]!;
            var system = EmulatorSystemCatalog.ById(item.SystemId)!;
            var row = new Rect(new Vector2(origin.X, origin.Y + index * rowHeight),
                new Vector2(origin.X + width, origin.Y + (index + 1) * rowHeight - 6f * scale));
            var entry = new RomEntry(item.Path, system);
            if (!DrawRomRow(row, entry, theme, scale))
            {
                continue;
            }

            SelectSystem(system);
            StartGame(entry);
            return;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, recentCount * rowHeight));
    }

    private void DrawSystemTiles(PhoneTheme theme, float scale)
    {
        SettingsSection.Header(Loc.T(L.Games.EmulatorSystems), theme);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        const int columns = 2;
        var gap = 8f * scale;
        var tileWidth = (width - gap) / columns;
        var tileHeight = 76f * scale;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < EmulatorSystemCatalog.All.Count; index++)
        {
            var system = EmulatorSystemCatalog.All[index];
            var column = index % columns;
            var rowIndex = index / columns;
            var min = origin + new Vector2(column * (tileWidth + gap), rowIndex * (tileHeight + gap));
            var tile = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            var hovered = ImGui.IsMouseHoveringRect(tile.Min, tile.Max);
            var installed = cores.IsInstalled(system);
            Squircle.Fill(drawList, tile.Min, tile.Max, 15f * scale,
                ImGui.GetColorU32((hovered ? theme.GroupedCard : theme.Surface) with { W = installed ? 1f : 0.55f }));
            Squircle.Stroke(drawList, tile.Min, tile.Max, 15f * scale,
                ImGui.GetColorU32(installed ? Accent with { W = hovered ? 0.58f : 0.24f } : theme.Separator), scale);
            Typography.Draw(new Vector2(tile.Min.X + 12f * scale, tile.Min.Y + 15f * scale), system.ShortName,
                installed ? GamePalette.Lighten(Accent, 0.24f) : theme.TextMuted, TextStyles.Headline);
            var count = CountKnownGames(system);
            Typography.Draw(new Vector2(tile.Min.X + 12f * scale, tile.Min.Y + 43f * scale),
                installed ? Loc.T(L.Games.EmulatorGameCount, count) : SystemStatus(system),
                theme.TextMuted, TextStyles.Caption1);
            if (!hovered || installingSystemId.Length > 0)
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                continue;
            }

            if (installed)
            {
                SelectSystem(system);
                return;
            }

            AskToInstall(system, () => SelectSystem(system));
            return;
        }

        var rows = (int)Math.Ceiling(EmulatorSystemCatalog.All.Count / (float)columns);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * tileHeight + Math.Max(0, rows - 1) * gap + 12f * scale));
    }

    private int CountKnownGames(EmulatorSystemDefinition system)
    {
        if (knownGameCounts.TryGetValue(system.Id, out var cached))
        {
            return cached;
        }

        var settings = configuration.Emulator.ForCore(system);
        var count = library.Scan(system, settings.RomFolders, settings.ImportedFiles).Count;
        knownGameCounts[system.Id] = count;
        return count;
    }

    private string SystemStatus(EmulatorSystemDefinition system)
    {
        if (string.Equals(installingSystemId, system.Id, StringComparison.Ordinal))
        {
            return Loc.T(L.Games.CoreDownloading);
        }

        return Loc.T(L.Games.CoreDownloadSize, SizeText(cores.PendingBytes(system)));
    }

    private static string SizeText(long bytes)
    {
        var megabytes = bytes / (1024f * 1024f);
        return Loc.T(L.Games.CoreMegabytes, megabytes.ToString(megabytes >= 10f ? "0" : "0.0", Loc.Culture));
    }

    private void AskToInstallStarterGame(EmulatorStarterGame game)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Games.GetStarterGame, game.Title),
            Message = Loc.T(L.Games.StarterGameBody, game.Title, game.Author, game.License, SizeText(game.Bytes)),
            ConfirmLabel = Loc.T(L.Games.CoreDownloadAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.Games.CoreDownloading),
            FailedMessage = Loc.T(L.Games.StarterGameFailed),
            Danger = false,
            ConfirmAsync = done => BeginStarterGameInstall(game, done),
        });
    }

    private void BeginStarterGameInstall(EmulatorStarterGame game, Action<bool> done)
    {
        var romDirectory = Path.Combine(emulatorRoot, "roms");
        _ = Task.Run(async () =>
        {
            var installed = false;
            try
            {
                await cores.InstallStarterGameAsync(game, romDirectory, OnInstallProgress, CancellationToken.None)
                    .ConfigureAwait(false);
                installed = true;
            }
            catch (Exception exception)
            {
                AepLog.Error($"[Emulator] could not download {game.Title}: {exception}");
            }

            if (installed)
            {
                pendingInstalled = RefreshLibrary;
                knownGameCounts.Clear();
            }

            done(installed);
        });
    }

    private void AskToInstall(EmulatorSystemDefinition system, Action onInstalled)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Games.CoreDownloadTitle, system.Name),
            Message = Loc.T(L.Games.CoreDownloadBody, SizeText(cores.PendingBytes(system))),
            ConfirmLabel = Loc.T(L.Games.CoreDownloadAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.Games.CoreDownloading),
            FailedMessage = Loc.T(L.Games.CoreDownloadFailed),
            Danger = false,
            ConfirmAsync = done => BeginInstall(system, onInstalled, done),
        });
    }

    private void BeginInstall(EmulatorSystemDefinition system, Action onInstalled, Action<bool> done)
    {
        installingSystemId = system.Id;
        _ = Task.Run(async () =>
        {
            var installed = false;
            try
            {
                await cores.InstallAsync(system, OnInstallProgress, CancellationToken.None).ConfigureAwait(false);
                installed = true;
            }
            catch (Exception exception)
            {
                AepLog.Error($"[Emulator] could not install the {system.Id} core: {exception}");
            }

            installingSystemId = string.Empty;
            if (installed)
            {
                pendingInstalled = onInstalled;
                knownGameCounts.Clear();
            }

            done(installed);
        });
    }

    private void OnInstallProgress(float fraction) =>
        confirm.Report(Loc.T(L.Games.CoreDownloadProgress, (int)(fraction * 100f)));

    private void ProcessPendingInstall()
    {
        if (Interlocked.Exchange(ref pendingInstalled, null) is not { } completed)
        {
            return;
        }

        completed();
    }

    private void SelectSystem(EmulatorSystemDefinition system)
    {
        selectedSystemId = system.Id;
        hubTab = 0;
        error = string.Empty;
        stateMessage = string.Empty;
        RefreshLibrary();
    }

    private void DrawRomLibrary(PhoneTheme theme, float scale)
    {
        if (session is not null && session.System.Id == CurrentSystem.Id)
        {
            SettingsSection.Header(Loc.T(L.Games.GamePaused), theme);
            var pausedCard = GroupCard.Begin(theme, 2);
            var resume = SettingsRow.Disclosure(pausedCard.NextRow(), Loc.T(L.Games.ContinueGame),
                $"{Path.GetFileNameWithoutExtension(session.RomPath)} · {session.System.ShortName}", theme);
            var stop = SettingsRow.Action(pausedCard.NextRow(), Loc.T(L.Games.StopGame), theme.Danger, theme);
            pausedCard.End();
            if (resume)
            {
                ResumeGame();
                return;
            }

            if (stop)
            {
                StopGame();
            }
        }

        if (session is not null && session.System.Id == CurrentSystem.Id)
        {
            DrawStateControls(theme, scale);
        }

        SettingsSection.Header(Loc.T(L.Games.Roms), theme);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var importCenter = new Vector2(origin.X + width * 0.5f - 78f * scale, origin.Y + 20f * scale);
        if (GameHud.Button(importCenter, new Vector2(140f * scale, 34f * scale), Loc.T(L.Games.ImportRom), Accent,
                theme))
        {
            OpenRomBrowser();
        }

        var folderCenter = new Vector2(origin.X + width * 0.5f + 78f * scale, origin.Y + 20f * scale);
        if (GameHud.Button(folderCenter, new Vector2(140f * scale, 34f * scale), Loc.T(L.Games.ScanFolder),
                theme.TextMuted, theme))
        {
            OpenFolderBrowser();
        }

        ImGui.Dummy(new Vector2(width, 44f * scale));
        var visibleRoms = roms;
        if (!string.IsNullOrEmpty(error))
        {
            var message = Typography.FitText(error, width - 20f * scale, TextStyles.Footnote);
            var messageSize = Typography.Measure(message, TextStyles.Footnote);
            var messageOrigin = ImGui.GetCursorScreenPos();
            Typography.DrawCentered(new Vector2(messageOrigin.X + width * 0.5f,
                messageOrigin.Y + messageSize.Y * 0.5f), message, theme.Danger, TextStyles.Footnote);
            ImGui.Dummy(new Vector2(width, messageSize.Y + 12f * scale));
        }

        if (visibleRoms.Count == 0)
        {
            var emptyOrigin = ImGui.GetCursorScreenPos();
            var availableHeight = MathF.Max(100f * scale, ImGui.GetContentRegionAvail().Y);
            var centerX = emptyOrigin.X + width * 0.5f;
            var headlineY = emptyOrigin.Y + availableHeight * 0.35f;
            Typography.DrawCentered(new Vector2(centerX, headlineY),
                Loc.T(L.Games.NoRoms), theme.TextStrong, TextStyles.Headline);
            var hint = Typography.FitText(Loc.T(L.Games.RomHint), width - 40f * scale, TextStyles.Footnote);
            Typography.DrawCentered(new Vector2(centerX, headlineY + 34f * scale),
                hint, theme.TextMuted, TextStyles.Footnote);
            var cursorY = headlineY + 78f * scale;
            if (EmulatorStarterGames.For(CurrentSystem) is { } starter)
            {
                if (GameHud.Button(new Vector2(centerX, cursorY), new Vector2(206f * scale, 34f * scale),
                        Loc.T(L.Games.GetStarterGame, starter.Title), Accent, theme))
                {
                    AskToInstallStarterGame(starter);
                }

                var credit = Typography.FitText(
                    $"{starter.Author} · {starter.License} · {SizeText(starter.Bytes)}", width - 40f * scale,
                    TextStyles.Caption1);
                Typography.DrawCentered(new Vector2(centerX, cursorY + 28f * scale), credit, theme.TextMuted,
                    TextStyles.Caption1);
                cursorY += 62f * scale;
            }

            if (GameHud.Button(new Vector2(centerX, cursorY), new Vector2(196f * scale, 34f * scale),
                    Loc.T(L.Games.FindHomebrew), theme.TextMuted, theme))
            {
                UrlActions.OpenInBrowser(HomebrewUrl);
            }

            var homebrewHint = Typography.FitText(Loc.T(L.Games.FindHomebrewHint), width - 40f * scale,
                TextStyles.Caption1);
            Typography.DrawCentered(new Vector2(centerX, cursorY + 32f * scale), homebrewHint,
                theme.TextMuted, TextStyles.Caption1);
            ImGui.Dummy(new Vector2(width,
                MathF.Max(availableHeight, cursorY - emptyOrigin.Y + 52f * scale)));
            return;
        }

        var rowOrigin = ImGui.GetCursorScreenPos();
        var rowHeight = 58f * scale;
        for (var index = 0; index < visibleRoms.Count; index++)
        {
            var row = new Rect(new Vector2(rowOrigin.X, rowOrigin.Y + index * rowHeight),
                new Vector2(rowOrigin.X + width, rowOrigin.Y + (index + 1) * rowHeight - 6f * scale));
            if (DrawRomRow(row, visibleRoms[index], theme, scale))
            {
                StartGame(visibleRoms[index]);
                return;
            }
        }

        ImGui.SetCursorScreenPos(rowOrigin);
        ImGui.Dummy(new Vector2(width, visibleRoms.Count * rowHeight));
    }

    private void DrawStateControls(PhoneTheme theme, float scale)
    {
        SettingsSection.Header(Loc.T(L.Games.SaveStates), theme);
        var card = GroupCard.Begin(theme, 3, SettingsRowHeight);
        stateSlot = DrawLabeledSegments("syrcus.stateSlot", card.NextRow(), Loc.T(L.Games.StateSlot),
            new[] { "1", "2", "3", "4", "5" }, stateSlot - 1, theme) + 1;
        if (SettingsRow.Action(card.NextRow(), Loc.T(L.Games.SaveState), Accent, theme))
        {
            SaveManualState();
        }

        if (SettingsRow.Action(card.NextRow(), Loc.T(L.Games.LoadState), theme.TextStrong, theme))
        {
            LoadManualState();
        }

        card.End();
        if (!string.IsNullOrEmpty(stateMessage))
        {
            SettingsSection.Hint(stateMessage, theme);
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));

        DrawDiscControls(theme, scale);
    }

    private void DrawDiscControls(PhoneTheme theme, float scale)
    {
        var active = session;
        if (active is null || active.DiskCount <= 1)
        {
            return;
        }

        SettingsSection.Header("Disc", theme);
        var card = GroupCard.Begin(theme, 2, SettingsRowHeight);
        SettingsRow.Info(card.NextRow(), "Current disc", $"{active.DiskIndex + 1} / {active.DiskCount}", theme);
        var next = SettingsRow.Disclosure(card.NextRow(), "Change disc",
            $"Disc {(active.DiskIndex + 1) % active.DiskCount + 1}", theme);
        card.End();
        if (next)
        {
            try
            {
                active.SetDiskIndex((active.DiskIndex + 1) % active.DiskCount);
                stateMessage = $"Disc {active.DiskIndex + 1} inserted.";
            }
            catch (Exception exception)
            {
                stateMessage = exception.Message;
            }
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
    }

    private bool DrawRomRow(Rect row, RomEntry entry, PhoneTheme theme, float scale)
    {
        var path = entry.Path;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        Squircle.Fill(drawList, row.Min, row.Max, 14f * scale,
            ImGui.GetColorU32(hovered ? theme.GroupedCard : theme.Surface));
        Squircle.Stroke(drawList, row.Min, row.Max, 14f * scale,
            ImGui.GetColorU32(Accent with { W = hovered ? 0.55f : 0.22f }), 1f * scale);
        var extension = entry.System.ShortName;
        var chip = new Rect(new Vector2(row.Min.X + 9f * scale, row.Center.Y - 15f * scale),
            new Vector2(row.Min.X + 70f * scale, row.Center.Y + 15f * scale));
        Squircle.Fill(drawList, chip.Min, chip.Max, 9f * scale, ImGui.GetColorU32(Accent with { W = 0.24f }));
        Typography.DrawCentered(chip.Center, extension, GamePalette.Lighten(Accent, 0.25f), TextStyles.Caption1);
        Typography.Draw(new Vector2(chip.Max.X + 10f * scale, row.Center.Y - 9f * scale),
            Path.GetFileNameWithoutExtension(path), theme.TextStrong, TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawSystemFilters(PhoneTheme theme, float scale)
    {
        SettingsSection.Header(Loc.T(L.Games.EmulatorSystems), theme);
        var systems = EmulatorSystemCatalog.All;
        systemFilterLabels[0] = Loc.T(L.Games.AllSystems);
        for (var index = 0; index < systems.Count; index++)
        {
            systemFilterLabels[index + 1] = systems[index].ShortName;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        const int columns = 4;
        var gap = 6f * scale;
        var cellWidth = (width - gap * (columns - 1)) / columns;
        var cellHeight = 30f * scale;
        var rows = (int)Math.Ceiling(systemFilterLabels.Length / (float)columns);
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < systemFilterLabels.Length; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var min = origin + new Vector2(column * (cellWidth + gap), row * (cellHeight + gap));
            var rect = new Rect(min, min + new Vector2(cellWidth, cellHeight));
            var id = index == 0 ? string.Empty : systems[index - 1].Id;
            var selected = selectedSystemId == id;
            var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
            Squircle.Fill(drawList, rect.Min, rect.Max, 10f * scale,
                ImGui.GetColorU32(selected ? Accent with { W = 0.76f } :
                    theme.GroupedCard with { W = hovered ? 0.82f : 0.58f }));
            Squircle.Stroke(drawList, rect.Min, rect.Max, 10f * scale,
                ImGui.GetColorU32(selected ? GamePalette.Lighten(Accent, 0.25f) : theme.Separator), scale);
            var label = Typography.FitText(systemFilterLabels[index], cellWidth - 8f * scale,
                TextStyles.Caption1);
            Typography.DrawCentered(rect.Center, label, theme.TextStrong, TextStyles.Caption1);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    selectedSystemId = id;
                }
            }
        }

        ImGui.Dummy(new Vector2(width, rows * cellHeight + Math.Max(0, rows - 1) * gap + 6f * scale));
    }

    private void DrawGame(in GameContext context)
    {
        var active = session!;
        var body = context.Body;
        var theme = context.Theme;
        var scale = UiScale.Current;
        var controlsVisible = !Settings.HideOnScreenControls;
        var fastForwardRect = LayoutElementRect(EmulatorLayoutElement.FastForward, body,
            active.VideoWidth, active.VideoHeight, scale);
        if (controlsVisible && ImGui.IsMouseHoveringRect(fastForwardRect.Min, fastForwardRect.Max) &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            fastForwardLatched = !fastForwardLatched;
        }

        if (!controlsVisible)
        {
            activeTouchAnalog = null;
        }

        var saveStateShortcut = ShortcutIsDown(Settings.SaveStateShortcut);
        var loadStateShortcut = ShortcutIsDown(Settings.LoadStateShortcut);
        if (saveStateShortcut && !saveStateShortcutWasDown)
        {
            SaveManualState();
        }

        if (loadStateShortcut && !loadStateShortcutWasDown)
        {
            LoadManualState();
        }

        saveStateShortcutWasDown = saveStateShortcut;
        loadStateShortcutWasDown = loadStateShortcut;
        var fastForward = fastForwardLatched || ShortcutIsDown(Settings.FastForwardShortcut);
        active.Advance(context.DeltaSeconds,
            fastForward ? Math.Clamp(Settings.FastForwardSpeed, 2, 4) : 1f,
            configuration.DoNotDisturb ? 0f : configuration.MusicVolume);

        var screenArea = GameplayScreenArea(body, scale);
        Rect pointerRect;
        if (IsNintendoDs)
        {
            var topOuter = CalculateDsScreenOuter(EmulatorLayoutElement.DsTopScreen, screenArea, scale);
            var bottomOuter = CalculateDsScreenOuter(EmulatorLayoutElement.DsBottomScreen, screenArea, scale);
            var topImage = CalculateDsImageRect(topOuter, scale, Settings.VideoFilter);
            var bottomImage = CalculateDsImageRect(bottomOuter, scale, Settings.VideoFilter);
            var screenWidth = Math.Max(topImage.Width, bottomImage.Width);
            var screenHeight = Math.Max(topImage.Height, bottomImage.Height);
            active.UploadVideoFrame(video, Settings.VideoFilter,
                Math.Max(1, (int)MathF.Round(screenWidth)),
                Math.Max(2, (int)MathF.Round(screenHeight * 2f)));

            DrawDsScreen(topOuter, topImage, active, theme, scale, Vector2.Zero, new Vector2(1f, 0.5f),
                Loc.T(L.Games.DsTopScreen));
            DrawDsScreen(bottomOuter, bottomImage, active, theme, scale, new Vector2(0f, 0.5f), Vector2.One,
                Loc.T(L.Games.DsTouchScreen));
            pointerRect = bottomImage;
        }
        else
        {
            var displayAspect = active.VideoAspectRatio;
            var screen = CalculateScreenOuter(screenArea, active.VideoWidth, active.VideoHeight, displayAspect, scale);
            var imageRect = CalculateImageRect(screen, active.VideoWidth, active.VideoHeight, displayAspect, scale,
                Settings.VideoFilter);
            active.UploadVideoFrame(video, Settings.VideoFilter,
                Math.Max(1, (int)MathF.Round(imageRect.Width)),
                Math.Max(1, (int)MathF.Round(imageRect.Height)));

            DrawScreen(screen, imageRect, active, theme, scale);
            pointerRect = imageRect;
        }

        var buttons = KeyboardInput() | GamepadInput();
        var touchCButtons = Vector2.Zero;
        var touchLeftAnalog = Vector2.Zero;
        var touchRightAnalog = Vector2.Zero;
        if (controlsVisible)
        {
            buttons |= DrawControls(body, theme, scale, active.System.Controls);
            (touchLeftAnalog, touchRightAnalog) = DrawAnalogControls(body, theme, scale, true);
            if (active.System.InputProfile == EmulatorInputProfile.Nintendo64)
            {
                touchCButtons = DrawCButtons(body, theme, scale);
            }

            DrawFastForwardControl(body, theme, scale, fastForward);
        }

        active.Input = phoneInteractive
            ? BuildInputState(buttons, touchCButtons, touchLeftAnalog, touchRightAnalog, pointerRect)
            : default;
        SuppressGameInput();
    }

    private Rect CalculateScreenOuter(Rect body, int videoWidth, int videoHeight, float displayAspect,
        float scale, bool pixelPerfect = true)
    {
        var layout = CurrentLayout.Screen;
        var aspect = ResolveDisplayAspect(videoWidth, videoHeight, displayAspect);
        var maximum = LandscapeMode
            ? new Vector2(MathF.Max(1f, body.Width * 0.66f),
                MathF.Max(1f, MathF.Min(body.Height, 420f * scale)))
            : new Vector2(MathF.Max(1f, body.Width - 16f * scale),
                MathF.Max(1f, MathF.Min(body.Height * 0.55f, 320f * scale)));
        var imageWidth = maximum.X;
        var imageHeight = imageWidth / aspect;
        if (imageHeight > maximum.Y)
        {
            imageHeight = maximum.Y;
            imageWidth = imageHeight * aspect;
        }

        var padding = ScreenPadding * scale;
        var desired = (new Vector2(imageWidth, imageHeight) + new Vector2(padding)) * layout.SafeScale;
        desired = FitSizeWithin(desired, body.Size);
        if (pixelPerfect && Settings.VideoFilter == EmulatorVideoFilter.Pixel &&
            UsesSquarePixels(videoWidth, videoHeight, aspect))
        {
            var integerScale = NearestNeighborScaler.IntegerScale(videoWidth, videoHeight,
                MathF.Max(1f, desired.X - padding), MathF.Max(1f, desired.Y - padding));
            var maximumScale = NearestNeighborScaler.IntegerScale(videoWidth, videoHeight,
                MathF.Max(1f, body.Width - padding), MathF.Max(1f, body.Height - padding));
            integerScale = Math.Min(integerScale, maximumScale);
            desired = new Vector2(videoWidth * integerScale + padding, videoHeight * integerScale + padding);
        }

        var center = LayoutCenter(body, layout);
        center = ClampCenter(center, desired * 0.5f, body);
        return new Rect(center - desired * 0.5f, center + desired * 0.5f);
    }

    private Rect CalculateDsScreenOuter(EmulatorLayoutElement element, Rect body, float scale,
        bool pixelPerfect = true)
    {
        var layout = CurrentLayout.For(element);
        const float aspect = 4f / 3f;
        var maximum = LandscapeMode
            ? new Vector2(MathF.Max(1f, body.Width * 0.28f), MathF.Max(1f, body.Height * 0.72f))
            : new Vector2(MathF.Max(1f, body.Width * 0.72f), MathF.Max(1f, body.Height * 0.27f));

        var imageWidth = maximum.X;
        var imageHeight = imageWidth / aspect;
        if (imageHeight > maximum.Y)
        {
            imageHeight = maximum.Y;
            imageWidth = imageHeight * aspect;
        }

        var padding = ScreenPadding * scale;
        var desired = (new Vector2(imageWidth, imageHeight) + new Vector2(padding)) * layout.SafeScale;
        desired = FitSizeWithin(desired, body.Size);
        if (pixelPerfect && Settings.VideoFilter == EmulatorVideoFilter.Pixel)
        {
            var integerScale = NearestNeighborScaler.IntegerScale(256, 192,
                MathF.Max(1f, desired.X - padding), MathF.Max(1f, desired.Y - padding));
            var maximumScale = NearestNeighborScaler.IntegerScale(256, 192,
                MathF.Max(1f, body.Width - padding), MathF.Max(1f, body.Height - padding));
            integerScale = Math.Min(integerScale, maximumScale);
            desired = new Vector2(256 * integerScale + padding, 192 * integerScale + padding);
        }

        var center = LayoutCenter(body, layout);
        center = ClampCenter(center, desired * 0.5f, body);
        return new Rect(center - desired * 0.5f, center + desired * 0.5f);
    }

    private static Rect CalculateDsImageRect(Rect outer, float scale, EmulatorVideoFilter filter) =>
        CalculateImageRect(outer, 256, 192, 4f / 3f, scale, filter);

    private static float ResolveDisplayAspect(int videoWidth, int videoHeight, float displayAspect)
    {
        if (displayAspect > 0.1f && !float.IsNaN(displayAspect) && !float.IsInfinity(displayAspect))
        {
            return displayAspect;
        }

        return videoWidth > 0 && videoHeight > 0 ? videoWidth / (float)videoHeight : 1.5f;
    }

    private static bool UsesSquarePixels(int videoWidth, int videoHeight, float displayAspect)
    {
        if (videoWidth <= 0 || videoHeight <= 0)
        {
            return false;
        }

        var pixelAspect = videoWidth / (float)videoHeight;
        return MathF.Abs(pixelAspect - displayAspect) <= MathF.Max(0.01f, displayAspect * 0.01f);
    }

    internal static Vector2 FitSizeWithin(Vector2 desired, Vector2 bounds)
    {
        var fit = MathF.Min(1f, MathF.Min(bounds.X / MathF.Max(1f, desired.X),
            bounds.Y / MathF.Max(1f, desired.Y)));
        return desired * fit;
    }

    private static Rect CalculateImageRect(Rect outer, int videoWidth, int videoHeight, float displayAspect,
        float scale, EmulatorVideoFilter filter)
    {
        if (videoWidth <= 0 || videoHeight <= 0)
        {
            return new Rect(outer.Center, outer.Center);
        }

        var available = outer.Size - new Vector2(ScreenPadding * scale);
        var aspect = ResolveDisplayAspect(videoWidth, videoHeight, displayAspect);
        if (filter == EmulatorVideoFilter.Pixel && UsesSquarePixels(videoWidth, videoHeight, aspect))
        {
            var integerScale = NearestNeighborScaler.IntegerScale(videoWidth, videoHeight,
                available.X, available.Y);
            var size = new Vector2(videoWidth * integerScale, videoHeight * integerScale);
            var min = new Vector2(MathF.Round(outer.Center.X - size.X * 0.5f),
                MathF.Round(outer.Center.Y - size.Y * 0.5f));
            return new Rect(min, min + size);
        }

        var width = available.X;
        var height = width / aspect;
        if (height > available.Y)
        {
            height = available.Y;
            width = height * aspect;
        }

        var snappedSize = new Vector2(MathF.Max(1f, MathF.Floor(width)), MathF.Max(1f, MathF.Floor(height)));
        var snappedMin = new Vector2(MathF.Round(outer.Center.X - snappedSize.X * 0.5f),
            MathF.Round(outer.Center.Y - snappedSize.Y * 0.5f));
        return new Rect(snappedMin, snappedMin + snappedSize);
    }

    private void DrawScreen(Rect outer, Rect imageRect, EmulatorSession active, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, outer.Min, outer.Max, 6f * scale,
            ImGui.GetColorU32(new Vector4(0.025f, 0.03f, 0.04f, 1f)));
        Squircle.Stroke(drawList, outer.Min, outer.Max, 6f * scale,
            ImGui.GetColorU32(Accent with { W = 0.16f }), 1f * scale);
        var wrap = video.Wrap;
        if (wrap is not null && active.VideoWidth > 0 && active.VideoHeight > 0)
        {
            drawList.AddImage(wrap.Handle, imageRect.Min, imageRect.Max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu);
        }
        else
        {
            Typography.DrawCentered(outer.Center, Loc.T(L.Games.StartingCore), theme.TextMuted,
                TextStyles.Footnote);
        }
    }

    private void DrawDsScreen(Rect outer, Rect imageRect, EmulatorSession active, PhoneTheme theme,
        float scale, Vector2 uvMin, Vector2 uvMax, string placeholder)
    {
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, outer.Min, outer.Max, 6f * scale,
            ImGui.GetColorU32(new Vector4(0.025f, 0.03f, 0.04f, 1f)));
        Squircle.Stroke(drawList, outer.Min, outer.Max, 6f * scale,
            ImGui.GetColorU32(Accent with { W = 0.16f }), 1f * scale);

        var wrap = video.Wrap;
        if (wrap is not null && active.VideoWidth > 0 && active.VideoHeight > 0)
        {
            drawList.AddImage(wrap.Handle, imageRect.Min, imageRect.Max, uvMin, uvMax, 0xFFFFFFFFu);
        }
        else
        {
            Typography.DrawCentered(outer.Center, placeholder, theme.TextMuted, TextStyles.Footnote);
        }
    }

    private EmulatorButtons DrawControls(Rect body, PhoneTheme theme, float scale,
        EmulatorButtons visible = EmulatorButtons.Up | EmulatorButtons.Down | EmulatorButtons.Left |
                                  EmulatorButtons.Right | EmulatorButtons.A | EmulatorButtons.B |
                                  EmulatorButtons.X | EmulatorButtons.Y | EmulatorButtons.L | EmulatorButtons.R |
                                  EmulatorButtons.Start | EmulatorButtons.Select)
    {
        var result = EmulatorButtons.None;
        var displayed = visible;
        if (CurrentSystem.InputProfile == EmulatorInputProfile.WonderSwan)
        {
            displayed &= ~(EmulatorButtons.X | EmulatorButtons.L | EmulatorButtons.R | EmulatorButtons.L2);
        }

        var layout = CurrentLayout;
        var dpadLayout = layout.Dpad;
        var hud = scale * dpadLayout.SafeScale;
        var dpad = LayoutCenter(body, dpadLayout);
        var step = 31f * hud;
        if (DirectionButton(new Vector2(dpad.X, dpad.Y - step), 25f * hud, new Vector2(0f, -1f), theme, hud))
            result |= EmulatorButtons.Up;
        if (DirectionButton(new Vector2(dpad.X, dpad.Y + step), 25f * hud, new Vector2(0f, 1f), theme, hud))
            result |= EmulatorButtons.Down;
        if (DirectionButton(new Vector2(dpad.X - step, dpad.Y), 25f * hud, new Vector2(-1f, 0f), theme, hud))
            result |= EmulatorButtons.Left;
        if (DirectionButton(new Vector2(dpad.X + step, dpad.Y), 25f * hud, new Vector2(1f, 0f), theme, hud))
            result |= EmulatorButtons.Right;

        if (CurrentSystem.InputProfile == EmulatorInputProfile.WonderSwan)
        {
            var secondLayout = layout.Dpad2;
            var secondScale = scale * secondLayout.SafeScale;
            var second = LayoutCenter(body, secondLayout);
            var secondStep = 31f * secondScale;
            if (DirectionButton(new Vector2(second.X, second.Y - secondStep), 25f * secondScale,
                    new Vector2(0f, -1f), theme, secondScale)) result |= EmulatorButtons.L2;
            if (DirectionButton(new Vector2(second.X, second.Y + secondStep), 25f * secondScale,
                    new Vector2(0f, 1f), theme, secondScale)) result |= EmulatorButtons.R;
            if (DirectionButton(new Vector2(second.X - secondStep, second.Y), 25f * secondScale,
                    new Vector2(-1f, 0f), theme, secondScale)) result |= EmulatorButtons.X;
            if (DirectionButton(new Vector2(second.X + secondStep, second.Y), 25f * secondScale,
                    new Vector2(1f, 0f), theme, secondScale)) result |= EmulatorButtons.L;
        }

        var aLayout = layout.A;
        var aScale = scale * aLayout.SafeScale;
        if ((displayed & EmulatorButtons.A) != 0 &&
            ControlButton(LayoutCenter(body, aLayout), 29f * aScale, CurrentSystem.ButtonLabel(EmulatorButtons.A),
                theme, aScale, Accent))
            result |= EmulatorButtons.A;
        var bLayout = layout.B;
        var bScale = scale * bLayout.SafeScale;
        if ((displayed & EmulatorButtons.B) != 0 &&
            ControlButton(LayoutCenter(body, bLayout), 29f * bScale, CurrentSystem.ButtonLabel(EmulatorButtons.B),
                theme, bScale, Accent))
            result |= EmulatorButtons.B;

        var xLayout = layout.X;
        var xScale = scale * xLayout.SafeScale;
        if ((displayed & EmulatorButtons.X) != 0 &&
            ControlButton(LayoutCenter(body, xLayout), 29f * xScale, CurrentSystem.ButtonLabel(EmulatorButtons.X),
                theme, xScale, Accent))
            result |= EmulatorButtons.X;
        var yLayout = layout.Y;
        var yScale = scale * yLayout.SafeScale;
        if ((displayed & EmulatorButtons.Y) != 0 &&
            ControlButton(LayoutCenter(body, yLayout), 29f * yScale, CurrentSystem.ButtonLabel(EmulatorButtons.Y),
                theme, yScale, Accent))
            result |= EmulatorButtons.Y;

        var lLayout = layout.L;
        var lScale = scale * lLayout.SafeScale;
        if ((displayed & EmulatorButtons.L) != 0 &&
            ShoulderButton(CenteredRect(LayoutCenter(body, lLayout), new Vector2(67f, 25f) * lScale),
                CurrentSystem.ButtonLabel(EmulatorButtons.L), theme, lScale))
            result |= EmulatorButtons.L;
        var rLayout = layout.R;
        var rScale = scale * rLayout.SafeScale;
        if ((displayed & EmulatorButtons.R) != 0 &&
            ShoulderButton(CenteredRect(LayoutCenter(body, rLayout), new Vector2(67f, 25f) * rScale),
                CurrentSystem.ButtonLabel(EmulatorButtons.R), theme, rScale))
            result |= EmulatorButtons.R;

        result |= DrawRearShoulder(body, theme, scale, displayed, EmulatorButtons.L2, EmulatorLayoutElement.L2);
        result |= DrawRearShoulder(body, theme, scale, displayed, EmulatorButtons.R2, EmulatorLayoutElement.R2);
        result |= DrawRearShoulder(body, theme, scale, displayed, EmulatorButtons.L3, EmulatorLayoutElement.L3);
        result |= DrawRearShoulder(body, theme, scale, displayed, EmulatorButtons.R3, EmulatorLayoutElement.R3);

        var selectLayout = layout.Select;
        var selectScale = scale * selectLayout.SafeScale;
        if ((displayed & EmulatorButtons.Select) != 0 &&
            ShoulderButton(CenteredRect(LayoutCenter(body, selectLayout), new Vector2(70f, 25f) * selectScale),
                CurrentSystem.ButtonLabel(EmulatorButtons.Select).ToUpperInvariant(), theme, selectScale))
            result |= EmulatorButtons.Select;
        var startLayout = layout.Start;
        var startScale = scale * startLayout.SafeScale;
        if ((displayed & EmulatorButtons.Start) != 0 &&
            ShoulderButton(CenteredRect(LayoutCenter(body, startLayout), new Vector2(70f, 25f) * startScale),
                CurrentSystem.ButtonLabel(EmulatorButtons.Start).ToUpperInvariant(), theme, startScale))
            result |= EmulatorButtons.Start;
        return result;
    }

    private EmulatorButtons DrawRearShoulder(Rect body, PhoneTheme theme, float scale, EmulatorButtons visible,
        EmulatorButtons button, EmulatorLayoutElement element)
    {
        if ((visible & button) == 0)
        {
            return EmulatorButtons.None;
        }

        var buttonLayout = CurrentLayout.For(element);
        var buttonScale = scale * buttonLayout.SafeScale;
        var rect = CenteredRect(LayoutCenter(body, buttonLayout), new Vector2(58f, 23f) * buttonScale);
        return ShoulderButton(rect, CurrentSystem.ButtonLabel(button), theme, buttonScale)
            ? button
            : EmulatorButtons.None;
    }

    private (Vector2 Left, Vector2 Right) DrawAnalogControls(Rect body, PhoneTheme theme, float scale,
        bool interactive)
    {
        var left = HasLeftAnalog
            ? DrawAnalogStick(body, CurrentLayout.LeftAnalog, EmulatorLayoutElement.LeftAnalog, theme, scale,
                interactive)
            : Vector2.Zero;
        var right = HasRightAnalog
            ? DrawAnalogStick(body, CurrentLayout.RightAnalog, EmulatorLayoutElement.RightAnalog, theme, scale,
                interactive)
            : Vector2.Zero;
        return (left, right);
    }

    private Vector2 DrawAnalogStick(Rect body, EmulatorElementLayout layout, EmulatorLayoutElement element,
        PhoneTheme theme, float scale, bool interactive)
    {
        var elementScale = scale * layout.SafeScale;
        var center = LayoutCenter(body, layout);
        var outerRadius = 38f * elementScale;
        var knobRadius = 15f * elementScale;
        var mouse = ImGui.GetMousePos();
        var hovered = Vector2.DistanceSquared(mouse, center) <= outerRadius * outerRadius;

        if (interactive && hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            activeTouchAnalog = element;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && activeTouchAnalog == element)
        {
            activeTouchAnalog = null;
        }

        var screenValue = Vector2.Zero;
        var held = interactive && activeTouchAnalog == element && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (held)
        {
            var travel = MathF.Max(1f, outerRadius - knobRadius * 0.45f);
            screenValue = (mouse - center) / travel;
            var lengthSquared = screenValue.LengthSquared();
            if (lengthSquared > 1f)
            {
                screenValue /= MathF.Sqrt(lengthSquared);
            }
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddCircleFilled(center, outerRadius,
            ImGui.GetColorU32(theme.GroupedCard with { W = held ? 0.76f : hovered ? 0.62f : 0.48f }), 48);
        drawList.AddCircle(center, outerRadius, ImGui.GetColorU32(theme.Separator), 48, 1f * elementScale);
        drawList.AddCircle(center, outerRadius * 0.56f,
            ImGui.GetColorU32(theme.Separator with { W = 0.52f }), 40, 1f * elementScale);
        var knobCenter = center + screenValue * MathF.Max(1f, outerRadius - knobRadius * 0.45f);
        drawList.AddCircleFilled(knobCenter, knobRadius,
            ImGui.GetColorU32(Accent with { W = held ? 0.94f : 0.72f }), 40);
        drawList.AddCircle(knobCenter, knobRadius,
            ImGui.GetColorU32(GamePalette.Lighten(Accent, 0.22f)), 40, 1f * elementScale);

        if (hovered && interactive)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return new Vector2(screenValue.X, -screenValue.Y);
    }

    private Vector2 DrawCButtons(Rect body, PhoneTheme theme, float scale)
    {
        var result = Vector2.Zero;
        var layout = CurrentLayout;
        if (DirectionButton(LayoutCenter(body, layout.CUp), 25f * scale * layout.CUp.SafeScale,
                new Vector2(0f, -1f), theme, scale * layout.CUp.SafeScale, N64CButtonColor)) result.Y = -1f;
        if (DirectionButton(LayoutCenter(body, layout.CDown), 25f * scale * layout.CDown.SafeScale,
                new Vector2(0f, 1f), theme, scale * layout.CDown.SafeScale, N64CButtonColor)) result.Y = 1f;
        if (DirectionButton(LayoutCenter(body, layout.CLeft), 25f * scale * layout.CLeft.SafeScale,
                new Vector2(-1f, 0f), theme, scale * layout.CLeft.SafeScale, N64CButtonColor)) result.X = -1f;
        if (DirectionButton(LayoutCenter(body, layout.CRight), 25f * scale * layout.CRight.SafeScale,
                new Vector2(1f, 0f), theme, scale * layout.CRight.SafeScale, N64CButtonColor)) result.X = 1f;
        return result;
    }

    private void DrawFastForwardControl(Rect body, PhoneTheme theme, float scale, bool active)
    {
        var layout = CurrentLayout.FastForward;
        var elementScale = scale * layout.SafeScale;
        var rect = CenteredRect(LayoutCenter(body, layout), new Vector2(55f, 25f) * elementScale);
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var color = active ? Accent : theme.GroupedCard;
        Squircle.Fill(ImGui.GetWindowDrawList(), rect.Min, rect.Max, 10f * elementScale,
            ImGui.GetColorU32(color with { W = active ? 0.90f : hovered ? 0.76f : 0.62f }));
        Squircle.Stroke(ImGui.GetWindowDrawList(), rect.Min, rect.Max, 10f * elementScale,
            ImGui.GetColorU32(active ? GamePalette.Lighten(Accent, 0.22f) : theme.Separator), 1f * elementScale);
        MediaGlyph.FastForward(ImGui.GetWindowDrawList(), rect.Center, 6f * elementScale,
            ImGui.GetColorU32(theme.TextStrong));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private Rect GameplayScreenArea(Rect area, float scale)
    {
        if (LandscapeMode)
        {
            return area;
        }

        var minimumY = MathF.Min(area.Max.Y - 1f, area.Min.Y + PortraitGameplayScreenTopInset * scale);
        return new Rect(new Vector2(area.Min.X, minimumY), area.Max);
    }

    private static bool IsScreenLayoutElement(EmulatorLayoutElement element) =>
        element is EmulatorLayoutElement.Screen or EmulatorLayoutElement.DsTopScreen or
            EmulatorLayoutElement.DsBottomScreen;

    private Rect LayoutElementRect(EmulatorLayoutElement element, Rect area, int videoWidth, int videoHeight,
        float scale, float displayAspect = 0f)
    {
        var layout = CurrentLayout.For(element);
        var elementScale = scale * layout.SafeScale;
        var center = LayoutCenter(area, layout);
        var screenArea = GameplayScreenArea(area, scale);
        return element switch
        {
            EmulatorLayoutElement.DsTopScreen or EmulatorLayoutElement.DsBottomScreen =>
                CalculateDsScreenOuter(element, screenArea, scale, false),
            EmulatorLayoutElement.Screen => CalculateScreenOuter(screenArea, videoWidth, videoHeight, displayAspect,
                scale, false),
            EmulatorLayoutElement.Dpad or EmulatorLayoutElement.Dpad2 =>
                CenteredRect(center, new Vector2(87f * elementScale)),
            EmulatorLayoutElement.LeftAnalog or EmulatorLayoutElement.RightAnalog =>
                CenteredRect(center, new Vector2(76f * elementScale)),
            EmulatorLayoutElement.A or EmulatorLayoutElement.B or EmulatorLayoutElement.X or EmulatorLayoutElement.Y or
                EmulatorLayoutElement.CUp or EmulatorLayoutElement.CDown or EmulatorLayoutElement.CLeft or
                EmulatorLayoutElement.CRight =>
                CenteredRect(center, new Vector2(29f * elementScale)),
            EmulatorLayoutElement.L or EmulatorLayoutElement.R =>
                CenteredRect(center, new Vector2(67f, 25f) * elementScale),
            EmulatorLayoutElement.L2 or EmulatorLayoutElement.R2 or EmulatorLayoutElement.L3 or
                EmulatorLayoutElement.R3 => CenteredRect(center, new Vector2(58f, 23f) * elementScale),
            EmulatorLayoutElement.Select or EmulatorLayoutElement.Start =>
                CenteredRect(center, new Vector2(70f, 25f) * elementScale),
            EmulatorLayoutElement.FastForward =>
                CenteredRect(center, new Vector2(55f, 25f) * elementScale),
            _ => CenteredRect(center, new Vector2(30f * elementScale)),
        };
    }

    private static Vector2 LayoutCenter(Rect area, EmulatorElementLayout layout) =>
        new(area.Min.X + area.Width * layout.SafeX, area.Min.Y + area.Height * layout.SafeY);

    private static Rect CenteredRect(Vector2 center, Vector2 size) =>
        new(center - size * 0.5f, center + size * 0.5f);

    private static Vector2 ClampCenter(Vector2 center, Vector2 halfSize, Rect bounds)
    {
        var min = bounds.Min + halfSize;
        var max = bounds.Max - halfSize;
        if (min.X > max.X)
        {
            min.X = max.X = bounds.Center.X;
        }

        if (min.Y > max.Y)
        {
            min.Y = max.Y = bounds.Center.Y;
        }

        return Vector2.Clamp(center, min, max);
    }

    private static bool ControlButton(Vector2 center, float diameter, string label, PhoneTheme theme, float scale,
        Vector4? accent = null)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = diameter * 0.5f;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(radius), center + new Vector2(radius));
        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var color = accent ?? theme.GroupedCard;
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(color with { W = held ? 0.92f : hovered ? 0.72f : 0.52f }), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(theme.Separator), 32, 1f * scale);
        Typography.DrawCentered(center, label, theme.TextStrong, TextStyles.Caption1);
        return held;
    }

    private static bool DirectionButton(Vector2 center, float diameter, Vector2 direction, PhoneTheme theme,
        float scale, Vector4? accent = null)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = diameter * 0.5f;
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(radius), center + new Vector2(radius));
        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var color = accent ?? theme.GroupedCard;
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(color with { W = held ? 0.92f : hovered ? 0.72f : 0.52f }), 40);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(theme.Separator), 40, 1f * scale);

        var glyphSize = radius * 0.48f;
        var tip = center + direction * glyphSize;
        var baseCenter = center - direction * glyphSize * 0.52f;
        var perpendicular = new Vector2(-direction.Y, direction.X) * glyphSize * 0.66f;
        drawList.AddTriangleFilled(tip, baseCenter + perpendicular, baseCenter - perpendicular,
            ImGui.GetColorU32(theme.TextStrong));
        return held;
    }

    private static bool ShoulderButton(Rect rect, string label, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        Squircle.Fill(drawList, rect.Min, rect.Max, 10f * scale,
            ImGui.GetColorU32((held ? theme.TextMuted : theme.GroupedCard) with { W = held ? 0.38f : 0.62f }));
        Squircle.Stroke(drawList, rect.Min, rect.Max, 10f * scale, ImGui.GetColorU32(theme.Separator), 1f * scale);
        Typography.DrawCentered(rect.Center, label, theme.TextStrong, TextStyles.Caption1);
        return held;
    }

    private void DrawFolderBrowser(in GameContext context)
    {
        var body = context.Body;
        var theme = context.Theme;
        var scale = UiScale.Current;
        var importingRom = browserPurpose == EmulatorBrowserPurpose.ImportRom;
        GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 25f * scale),
            Loc.T(importingRom ? L.Games.RomBrowser : L.Games.FolderBrowser), theme.TextStrong, TextStyles.Title2);
        var path = directoryBrowser.IsDriveList ? Loc.T(L.Games.Drives) : directoryBrowser.CurrentPath;
        path = Typography.FitText(path, body.Width - 32f * scale, TextStyles.Footnote);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 51f * scale), path,
            theme.TextMuted, TextStyles.Footnote);

        if (!importingRom && !directoryBrowser.IsDriveList &&
            GameHud.Button(new Vector2(body.Center.X, body.Min.Y + 82f * scale),
                new Vector2(176f * scale, 34f * scale), Loc.T(L.Games.SelectFolder), Accent, theme))
        {
            SelectCurrentFolder();
            return;
        }

        var contentTop = body.Min.Y + (importingRom ? 73f : 107f) * scale;
        ImGui.SetCursorScreenPos(new Vector2(body.Min.X, contentTop));
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f * scale, 4f * scale)))
        using (var child = ImRaii.Child("##gameBoyFolderBrowser",
                   new Vector2(body.Width, body.Max.Y - contentTop), false, ImGuiWindowFlags.NoBackground))
        {
            if (!child)
            {
                return;
            }

            var rowCount = directoryBrowser.Directories.Count + directoryBrowser.Files.Count +
                           (directoryBrowser.IsDriveList ? 0 : 1);
            if (rowCount > 0)
            {
                var card = GroupCard.Begin(theme, rowCount);
                if (!directoryBrowser.IsDriveList &&
                    SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Games.ParentFolder), string.Empty, theme))
                {
                    directoryBrowser.Up();
                    card.End();
                    return;
                }

                for (var index = 0; index < directoryBrowser.Directories.Count; index++)
                {
                    var folder = directoryBrowser.Directories[index];
                    var label = directoryBrowser.IsDriveList
                        ? folder
                        : Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
                    if (SettingsRow.Disclosure(card.NextRow(), label, string.Empty, theme))
                    {
                        directoryBrowser.Navigate(folder);
                        card.End();
                        return;
                    }
                }

                for (var index = 0; index < directoryBrowser.Files.Count; index++)
                {
                    var file = directoryBrowser.Files[index];
                    if (SettingsRow.Disclosure(card.NextRow(), Path.GetFileName(file),
                            Path.GetExtension(file).TrimStart('.').ToUpperInvariant(), theme))
                    {
                        browserPurpose = EmulatorBrowserPurpose.None;
                        Interlocked.Exchange(ref pendingImport, file);
                        card.End();
                        return;
                    }
                }

                card.End();
            }
            else
            {
                var available = ImGui.GetContentRegionAvail();
                Typography.DrawCentered(ImGui.GetCursorScreenPos() + available * 0.5f,
                    Loc.T(importingRom ? L.Games.NoCompatibleRoms : L.Games.NoSubfolders), theme.TextMuted,
                    TextStyles.Footnote);
                ImGui.Dummy(available);
            }

            if (!string.IsNullOrEmpty(directoryBrowser.Error))
            {
                SettingsSection.Hint(directoryBrowser.Error, theme);
            }
        }
    }

    private void OpenFolderBrowser()
    {
        CancelAllBindings();
        directoryBrowser.Open();
        browserPurpose = EmulatorBrowserPurpose.ScanFolder;
    }

    private void OpenRomBrowser()
    {
        CancelAllBindings();
        directoryBrowser.OpenFiles(null, CurrentSystem.Extensions);
        browserPurpose = EmulatorBrowserPurpose.ImportRom;
    }

    private void SelectCurrentFolder()
    {
        if (directoryBrowser.IsDriveList || !Directory.Exists(directoryBrowser.CurrentPath))
        {
            return;
        }

        var path = Path.GetFullPath(directoryBrowser.CurrentPath);
        var alreadyConfigured = false;
        for (var index = 0; index < Settings.RomFolders.Count; index++)
        {
            if (string.Equals(Path.GetFullPath(Settings.RomFolders[index]), path,
                    StringComparison.OrdinalIgnoreCase))
            {
                alreadyConfigured = true;
                break;
            }
        }

        if (!alreadyConfigured)
        {
            Settings.RomFolders.Add(path);
            configuration.Save();
        }

        browserPurpose = EmulatorBrowserPurpose.None;
        hubTab = 0;
        error = string.Empty;
        RefreshLibrary();
    }

    private void SaveManualState()
    {
        if (session is null)
        {
            return;
        }

        try
        {
            session.SaveState(stateSlot);
            stateMessage = Loc.T(L.Games.StateSaved, stateSlot);
        }
        catch (Exception exception)
        {
            stateMessage = exception.Message;
            AepLog.Warning($"[Emulator] save state failed: {exception.Message}");
        }
    }

    private void LoadManualState()
    {
        if (session is null)
        {
            return;
        }

        if (!session.HasState(stateSlot))
        {
            stateMessage = Loc.T(L.Games.StateMissing, stateSlot);
            return;
        }

        try
        {
            session.LoadState(stateSlot);
            stateMessage = Loc.T(L.Games.StateLoaded, stateSlot);
            ResumeGame();
        }
        catch (Exception exception)
        {
            stateMessage = exception.Message;
            AepLog.Warning($"[Emulator] load state failed: {exception.Message}");
        }
    }

    private void SaveAutoState()
    {
        if (session is null || !configuration.Emulator.ForCore(session.System).AutoSaveState)
        {
            return;
        }

        try
        {
            session.SaveAutoState();
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Emulator] automatic save state failed: {exception.Message}");
        }
    }

    private void ProcessPendingImport()
    {
        var source = Interlocked.Exchange(ref pendingImport, null);
        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        try
        {
            var system = EmulatorSystemCatalog.ById(selectedSystemId) ??
                         throw new InvalidOperationException("Select an emulator core before importing a game.");
            var imported = library.Import(source, system);
            var settings = configuration.Emulator.ForCore(system);
            if (!imported.Path.StartsWith(Path.Combine(emulatorRoot, "roms"), StringComparison.OrdinalIgnoreCase) &&
                !settings.ImportedFiles.Contains(imported.Path, StringComparer.OrdinalIgnoreCase))
            {
                settings.ImportedFiles.Add(imported.Path);
                configuration.Save();
            }

            RefreshLibrary();
            StartGame(imported);
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }
    }

    private void StartGame(RomEntry entry)
    {
        if (!cores.IsInstalled(entry.System))
        {
            AskToInstall(entry.System, () => StartGame(entry));
            return;
        }

        StopGame();
        selectedSystemId = entry.System.Id;
        error = string.Empty;
        stateMessage = string.Empty;
        try
        {
            var path = entry.Path;
            var corePath = cores.CorePath(entry.System);
            if (!File.Exists(corePath))
            {
                throw new FileNotFoundException($"{Loc.T(L.Games.CoreMissing)} ({entry.System.Name})", corePath);
            }

            var settings = configuration.Emulator.ForCore(entry.System);
            if (entry.System.InputProfile == EmulatorInputProfile.NintendoDs)
            {
                settings.CoreOptions["melonds_number_of_screen_layouts"] = "1";
                settings.CoreOptions["melonds_screen_layout1"] = "top-bottom";
                settings.CoreOptions["melonds_screen_layout2"] = "top-bottom";
            }

            session = new EmulatorSession(corePath, entry.System, path, emulatorRoot, settings.CoreOptions,
                preserveSaveMemoryOnStateLoad: settings.ProtectSaveMemoryOnStateLoad);
            if (Settings.AutoLoadState)
            {
                try
                {
                    session.LoadAutoState();
                }
                catch (Exception exception)
                {
                    AepLog.Warning($"[Emulator] automatic load state failed: {exception.Message}");
                }
            }

            fastForwardLatched = false;
            gameVisible = true;
            SetInputCaptured(true);
            configuration.Emulator.AddRecent(entry.System, path);
            configuration.Save();
        }
        catch (Exception exception)
        {
            error = $"{Loc.T(L.Games.LoadFailed)}: {exception.Message}";
            AepLog.Error($"[Emulator] {exception}");
            session?.Dispose();
            session = null;
            gameVisible = false;
        }
    }

    private void ResumeGame()
    {
        if (session is null)
        {
            return;
        }

        CancelAllBindings();
        ResetShortcutEdges();
        gameVisible = true;
        SetInputCaptured(true);
    }

    private void PauseGame()
    {
        SaveAutoState();
        fastForwardLatched = false;
        ResetShortcutEdges();
        gameVisible = false;
        SetInputCaptured(false);
        hubTab = 0;
    }

    private void StopGame()
    {
        CancelAllBindings();
        SaveAutoState();
        fastForwardLatched = false;
        ResetShortcutEdges();
        gameVisible = false;
        SetInputCaptured(false);
        session?.Dispose();
        session = null;
    }

    private void ResetShortcutEdges()
    {
        saveStateShortcutWasDown = false;
        loadStateShortcutWasDown = false;
    }

    private void RefreshLibrary()
    {
        var system = EmulatorSystemCatalog.ById(selectedSystemId);
        if (system is null)
        {
            roms = Array.Empty<RomEntry>();
            return;
        }

        var settings = configuration.Emulator.ForCore(system);
        roms = library.Scan(system, settings.RomFolders, settings.ImportedFiles);
        knownGameCounts[system.Id] = roms.Count;
    }

    public void Dispose()
    {
        StopGame();
        cores.Dispose();
        inputCaptureRegistration.Dispose();
        keyboardCapture.Dispose();
        video.Dispose();
    }
}
