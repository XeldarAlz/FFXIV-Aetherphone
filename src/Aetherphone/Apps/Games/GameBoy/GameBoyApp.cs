using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Emulation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.GameBoy;

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

internal sealed class GameBoyApp : IMiniGame
{
    private const string GameId = "gameboy";
    private const float SettingsRowHeight = 74f;
    private const float ScreenPadding = 4f;
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
        EmulatorLayoutElement.Dpad, EmulatorLayoutElement.Screen,
    };
    private static readonly GamepadButtons[] ShortcutGamepadButtons =
    {
        GamepadButtons.DpadUp, GamepadButtons.DpadDown, GamepadButtons.DpadLeft, GamepadButtons.DpadRight,
        GamepadButtons.North, GamepadButtons.South, GamepadButtons.West, GamepadButtons.East,
        GamepadButtons.L1, GamepadButtons.L2, GamepadButtons.L3,
        GamepadButtons.R1, GamepadButtons.R2, GamepadButtons.R3,
        GamepadButtons.Start, GamepadButtons.Select,
    };

    private readonly string emulatorRoot;
    private readonly string coreDirectory;
    private readonly RomLibrary library;
    private readonly EmulatorVideoTexture video;
    private readonly KeyboardInputCapture keyboardCapture;
    private readonly IKeyState keyState;
    private readonly IGamepadState gamepadState;
    private readonly Configuration configuration;
    private readonly DirectoryBrowser directoryBrowser = new();
    private readonly bool[] bindingKeyStates = new bool[256];
    private readonly HashSet<int> shortcutCaptureKeys = new();
    private readonly Dictionary<string, int> knownGameCounts = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<RomEntry> roms = Array.Empty<RomEntry>();
    private EmulatorSession? session;
    private string? pendingImport;
    private string error = string.Empty;
    private bool inputCaptured;
    private bool gamepadCaptureActive;
    private bool gamepadNavigationWasEnabled;
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
    private Vector2 layoutDragOffset;

    public GameBoyApp(DirectoryInfo configDirectory, ITextureProvider textures, IKeyState keyState,
        IGamepadState gamepadState, Configuration configuration)
    {
        emulatorRoot = Path.Combine(configDirectory.FullName, "Emulator");
        coreDirectory = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Cores");
        library = new RomLibrary(emulatorRoot);
        video = new EmulatorVideoTexture(textures);
        keyboardCapture = new KeyboardInputCapture();
        this.keyState = keyState;
        this.gamepadState = gamepadState;
        this.configuration = configuration;
        configuration.Emulator ??= new EmulatorSettings();
        configuration.Emulator.MigrateToPerCoreSettings(EmulatorSystemCatalog.All);
        foreach (var system in EmulatorSystemCatalog.All)
        {
            _ = configuration.Emulator.ForCore(system);
        }

        configuration.Save();
    }

    private EmulatorSystemDefinition CurrentSystem =>
        EmulatorSystemCatalog.ById(selectedSystemId) ?? session?.System ?? EmulatorSystemCatalog.GameBoy;
    private EmulatorSettings Settings => configuration.Emulator.ForCore(CurrentSystem);
    private bool LandscapeMode => Settings.GameplayOrientation == EmulatorGameplayOrientation.Landscape;
    private EmulatorLayoutSettings CurrentLayout => Settings.LayoutFor(Settings.GameplayOrientation);
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.GameBoy);
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
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 25f * scale),
            CurrentSystem.Name, theme.TextStrong, TextStyles.Title2);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 49f * scale),
            Typography.FitText(CurrentSystem.Description, body.Width - 36f * scale, TextStyles.Footnote),
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
        var scale = ImGuiHelpers.GlobalScale;
        GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 25f * scale),
            Loc.T(L.Games.GameBoyLibrary), theme.TextStrong, TextStyles.Title2);
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
        var recent = configuration.Emulator.RecentGames
            .Where(entry => File.Exists(entry.Path) && EmulatorSystemCatalog.ById(entry.SystemId) is not null)
            .Take(6).ToArray();
        if (recent.Length == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Games.RecentlyPlayed), theme);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = 58f * scale;
        for (var index = 0; index < recent.Length; index++)
        {
            var item = recent[index];
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
        ImGui.Dummy(new Vector2(width, recent.Length * rowHeight));
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
            var installed = File.Exists(Path.Combine(coreDirectory, system.CoreFileName));
            Squircle.Fill(drawList, tile.Min, tile.Max, 15f * scale,
                ImGui.GetColorU32((hovered ? theme.GroupedCard : theme.Surface) with { W = installed ? 1f : 0.55f }));
            Squircle.Stroke(drawList, tile.Min, tile.Max, 15f * scale,
                ImGui.GetColorU32(installed ? Accent with { W = hovered ? 0.58f : 0.24f } : theme.Separator), scale);
            Typography.Draw(new Vector2(tile.Min.X + 12f * scale, tile.Min.Y + 15f * scale), system.ShortName,
                installed ? GamePalette.Lighten(Accent, 0.24f) : theme.TextMuted, TextStyles.Headline);
            var count = CountKnownGames(system);
            Typography.Draw(new Vector2(tile.Min.X + 12f * scale, tile.Min.Y + 43f * scale),
                installed ? Loc.T(L.Games.EmulatorGameCount, count) : Loc.T(L.Games.CoreMissingShort),
                theme.TextMuted, TextStyles.Caption1);
            if (hovered && installed)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    SelectSystem(system);
                    return;
                }
            }
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
            Typography.DrawCentered(new Vector2(emptyOrigin.X + width * 0.5f,
                    emptyOrigin.Y + availableHeight * 0.35f),
                Loc.T(L.Games.NoRoms), theme.TextStrong, TextStyles.Headline);
            var hint = Typography.FitText(Loc.T(L.Games.RomHint), width - 40f * scale, TextStyles.Footnote);
            Typography.DrawCentered(new Vector2(emptyOrigin.X + width * 0.5f,
                    emptyOrigin.Y + availableHeight * 0.35f + 34f * scale),
                hint, theme.TextMuted, TextStyles.Footnote);
            ImGui.Dummy(new Vector2(width, availableHeight));
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
        stateSlot = DrawLabeledSegments("gameboy.stateSlot", card.NextRow(), Loc.T(L.Games.StateSlot),
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
        var labels = new[] { Loc.T(L.Games.AllSystems) }
            .Concat(systems.Select(static system => system.ShortName)).ToArray();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        const int columns = 4;
        var gap = 6f * scale;
        var cellWidth = (width - gap * (columns - 1)) / columns;
        var cellHeight = 30f * scale;
        var rows = (int)Math.Ceiling(labels.Length / (float)columns);
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < labels.Length; index++)
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
            var label = Typography.FitText(labels[index], cellWidth - 8f * scale, TextStyles.Caption1);
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

    private void DrawEmulatorSettings(PhoneTheme theme, float scale)
    {
        var settings = Settings;
        var changed = false;
        changed |= DrawCoreSpecificSettings(CurrentSystem, settings, theme);

        SettingsSection.Header(Loc.T(L.Games.Video), theme);
        var videoCard = GroupCard.Begin(theme, 2, SettingsRowHeight);
        var filter = DrawLabeledSegments("gameboy.filter", videoCard.NextRow(), Loc.T(L.Games.VideoFilter),
            new[]
            {
                Loc.T(L.Games.FilterPixel), Loc.T(L.Games.FilterBalanced), Loc.T(L.Games.FilterSharp),
                Loc.T(L.Games.FilterSmooth),
            },
            VideoFilterIndex(settings.VideoFilter), theme);
        var nextFilter = filter switch
        {
            0 => EmulatorVideoFilter.Pixel,
            1 => EmulatorVideoFilter.Balanced,
            2 => EmulatorVideoFilter.Sharp,
            _ => EmulatorVideoFilter.Smooth,
        };
        if (nextFilter != settings.VideoFilter)
        {
            settings.VideoFilter = nextFilter;
            changed = true;
        }

        var orientation = DrawLabeledSegments("gameboy.orientation", videoCard.NextRow(),
            Loc.T(L.Games.Orientation), new[] { Loc.T(L.Games.Portrait), Loc.T(L.Games.Landscape) },
            settings.GameplayOrientation == EmulatorGameplayOrientation.Landscape ? 1 : 0, theme);
        var nextOrientation = orientation == 1
            ? EmulatorGameplayOrientation.Landscape
            : EmulatorGameplayOrientation.Portrait;
        if (nextOrientation != settings.GameplayOrientation)
        {
            settings.GameplayOrientation = nextOrientation;
            changed = true;
        }

        videoCard.End();
        SettingsSection.Hint(Loc.T(L.Games.PixelFilterHint), theme);

        SettingsSection.Header(Loc.T(L.Games.FastForward), theme);
        var fastForwardCard = GroupCard.Begin(theme, 1, SettingsRowHeight);
        var fastForwardSpeed = DrawLabeledSegments("gameboy.fastForwardSpeed", fastForwardCard.NextRow(),
            Loc.T(L.Games.FastForwardSpeed), new[] { "2x", "3x", "4x" },
            Math.Clamp(settings.FastForwardSpeed, 2, 4) - 2, theme) + 2;
        if (fastForwardSpeed != settings.FastForwardSpeed)
        {
            settings.FastForwardSpeed = fastForwardSpeed;
            changed = true;
        }

        fastForwardCard.End();
        SettingsSection.Hint(Loc.T(L.Games.FastForwardHint), theme);

        SettingsSection.Header(Loc.T(L.Games.SaveStates), theme);
        var autoStateCard = GroupCard.Begin(theme, 2);
        var autoSave = SettingsRow.Bool(autoStateCard.NextRow(), Loc.T(L.Games.AutoSaveState),
            settings.AutoSaveState, theme);
        var autoLoad = SettingsRow.Bool(autoStateCard.NextRow(), Loc.T(L.Games.AutoLoadState),
            settings.AutoLoadState, theme);
        autoStateCard.End();
        if (autoSave != settings.AutoSaveState)
        {
            settings.AutoSaveState = autoSave;
            changed = true;
        }

        if (autoLoad != settings.AutoLoadState)
        {
            settings.AutoLoadState = autoLoad;
            changed = true;
        }

        SettingsSection.Hint(Loc.T(L.Games.AutoStateHint), theme);

        SettingsSection.Header(Loc.T(L.Games.EmulatorShortcuts), theme);
        var shortcutCard = GroupCard.Begin(theme, 4);
        if (SettingsRow.Disclosure(shortcutCard.NextRow(), Loc.T(L.Games.FastForward),
                ShortcutValue(EmulatorShortcutAction.FastForward), theme))
        {
            BeginShortcutBinding(EmulatorShortcutAction.FastForward);
        }

        if (SettingsRow.Disclosure(shortcutCard.NextRow(), Loc.T(L.Games.SaveState),
                ShortcutValue(EmulatorShortcutAction.SaveState), theme))
        {
            BeginShortcutBinding(EmulatorShortcutAction.SaveState);
        }

        if (SettingsRow.Disclosure(shortcutCard.NextRow(), Loc.T(L.Games.LoadState),
                ShortcutValue(EmulatorShortcutAction.LoadState), theme))
        {
            BeginShortcutBinding(EmulatorShortcutAction.LoadState);
        }

        if (SettingsRow.Action(shortcutCard.NextRow(), Loc.T(L.Games.ClearShortcuts), theme.TextMuted, theme))
        {
            CancelAllBindings();
            settings.FastForwardShortcut.Clear();
            settings.SaveStateShortcut.Clear();
            settings.LoadStateShortcut.Clear();
            changed = true;
        }

        shortcutCard.End();
        SettingsSection.Hint(Loc.T(L.Games.ShortcutsHint), theme);

        SettingsSection.Header(Loc.T(L.Games.InterfaceLayout), theme);
        var layoutCard = GroupCard.Begin(theme, 1);
        if (SettingsRow.Disclosure(layoutCard.NextRow(), Loc.T(L.Games.EditInterface), string.Empty, theme))
        {
            CancelAllBindings();
            editingLayout = true;
            selectedLayoutElement = EmulatorLayoutElement.Screen;
        }

        layoutCard.End();
        SettingsSection.Hint(Loc.T(L.Games.InterfaceLayoutHint), theme);

        SettingsSection.Header(Loc.T(L.Games.KeyboardControls), theme);
        var visibleBindings = BindingOrder.Where(button => (CurrentSystem.Controls & button) != 0).ToArray();
        var auxiliaryBindings = CurrentSystem.InputProfile == EmulatorInputProfile.Nintendo64 ? 4 : 0;
        var controlsCard = GroupCard.Begin(theme, visibleBindings.Length + auxiliaryBindings + 1);
        for (var index = 0; index < visibleBindings.Length; index++)
        {
            var button = visibleBindings[index];
            var value = bindingTarget == button
                ? Loc.T(L.Games.PressKey)
                : EmulatorKeyCatalog.Name(settings.KeyFor(button));
            if (SettingsRow.Disclosure(controlsCard.NextRow(), ControlLabel(button), value, theme))
            {
                BeginKeyBinding(button);
            }
        }

        if (CurrentSystem.InputProfile == EmulatorInputProfile.Nintendo64)
        {
            DrawAuxiliaryBindingRow(controlsCard.NextRow(), EmulatorLayoutElement.CUp, "C Up", settings.KeyCUp,
                theme);
            DrawAuxiliaryBindingRow(controlsCard.NextRow(), EmulatorLayoutElement.CDown, "C Down", settings.KeyCDown,
                theme);
            DrawAuxiliaryBindingRow(controlsCard.NextRow(), EmulatorLayoutElement.CLeft, "C Left", settings.KeyCLeft,
                theme);
            DrawAuxiliaryBindingRow(controlsCard.NextRow(), EmulatorLayoutElement.CRight, "C Right", settings.KeyCRight,
                theme);
        }

        if (SettingsRow.Action(controlsCard.NextRow(), Loc.T(L.Games.ResetControls), theme.Accent, theme))
        {
            CancelKeyBinding();
            settings.ResetKeys();
            changed = true;
        }

        controlsCard.End();
        SettingsSection.Hint(Loc.T(L.Games.ControlsHint), theme);

        SettingsSection.Header(Loc.T(L.Games.RomFolders), theme);
        var folderCard = GroupCard.Begin(theme, settings.RomFolders.Count + 1);
        var removeFolder = -1;
        for (var index = 0; index < settings.RomFolders.Count; index++)
        {
            var path = settings.RomFolders[index];
            var label = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            if (string.IsNullOrEmpty(label))
            {
                label = path;
            }

            if (SettingsRow.Disclosure(folderCard.NextRow(), label, Loc.T(L.Games.RemoveFolder), theme))
            {
                removeFolder = index;
            }
        }

        if (SettingsRow.Action(folderCard.NextRow(), Loc.T(L.Games.ScanFolder), Accent, theme))
        {
            OpenFolderBrowser();
        }

        folderCard.End();
        if (removeFolder >= 0)
        {
            settings.RomFolders.RemoveAt(removeFolder);
            RefreshLibrary();
            changed = true;
        }

        SettingsSection.Hint(Loc.T(L.Games.RomFolderHint), theme);
        ImGui.Dummy(new Vector2(0f, 16f * scale));
        if (changed)
        {
            configuration.Save();
        }
    }

    private bool DrawCoreSpecificSettings(EmulatorSystemDefinition system, EmulatorSettings settings,
        PhoneTheme theme)
    {
        var changed = false;
        SettingsSection.Header("Core", theme);
        var infoCard = GroupCard.Begin(theme, 2);
        SettingsRow.Info(infoCard.NextRow(), "Libretro core", system.CoreFileName, theme);
        SettingsRow.Info(infoCard.NextRow(), "Persistent storage", system.SaveDescription, theme);
        infoCard.End();

        if (system.CoreOptions.Count > 0)
        {
            SettingsSection.Header("Core options", theme);
            var optionsCard = GroupCard.Begin(theme, system.CoreOptions.Count);
            for (var index = 0; index < system.CoreOptions.Count; index++)
            {
                var option = system.CoreOptions[index];
                var current = settings.CoreOptions.TryGetValue(option.Key, out var configured) &&
                              option.Values.Contains(configured, StringComparer.OrdinalIgnoreCase)
                    ? configured
                    : system.DefaultCoreOptions.TryGetValue(option.Key, out var defaultValue) &&
                      option.Values.Contains(defaultValue, StringComparer.OrdinalIgnoreCase)
                        ? defaultValue
                        : option.Values[0];
                if (!SettingsRow.Disclosure(optionsCard.NextRow(), option.Label, option.Display(current), theme))
                {
                    continue;
                }

                var currentIndex = 0;
                for (var valueIndex = 0; valueIndex < option.Values.Count; valueIndex++)
                {
                    if (string.Equals(option.Values[valueIndex], current, StringComparison.OrdinalIgnoreCase))
                    {
                        currentIndex = valueIndex;
                        break;
                    }
                }

                settings.CoreOptions[option.Key] = option.Values[(currentIndex + 1) % option.Values.Count];
                changed = true;
            }

            optionsCard.End();
            SettingsSection.Hint("Core options are applied the next time a game starts.", theme);
        }

        if (system.Firmware.Count > 0)
        {
            SettingsSection.Header("BIOS / firmware", theme);
            var firmwareCard = GroupCard.Begin(theme, system.Firmware.Count);
            var systemDirectory = Path.Combine(emulatorRoot, "system");
            for (var index = 0; index < system.Firmware.Count; index++)
            {
                var firmware = system.Firmware[index];
                var present = File.Exists(Path.Combine(systemDirectory, firmware.FileName));
                var state = present ? "Installed" : firmware.Required ? "Required" : "Optional";
                SettingsRow.Info(firmwareCard.NextRow(), firmware.FileName, state, theme);
            }

            firmwareCard.End();
            SettingsSection.Hint($"Place legally obtained firmware files in {systemDirectory}", theme);
        }

        if (system.InputProfile == EmulatorInputProfile.PlayStation)
        {
            SettingsSection.Header("Memory cards", theme);
            var memoryCard = GroupCard.Begin(theme, 2);
            SettingsRow.Info(memoryCard.NextRow(), "Storage", @"saves\ps1", theme);
            var protect = SettingsRow.Bool(memoryCard.NextRow(), "Protect cards when loading states",
                settings.ProtectSaveMemoryOnStateLoad, theme);
            memoryCard.End();
            if (protect != settings.ProtectSaveMemoryOnStateLoad)
            {
                settings.ProtectSaveMemoryOnStateLoad = protect;
                changed = true;
            }

            SettingsSection.Hint("Protection prevents an old save state from replacing newer memory-card data.",
                theme);
        }

        if (system.InputProfile == EmulatorInputProfile.Nintendo64)
        {
            SettingsSection.Hint(
                "Controller Pak and Rumble Pak are selected above. Transfer Pak and N64DD require libretro " +
                "subsystems and are reserved for a later phase.", theme);
        }

        return changed;
    }

    private void DrawAuxiliaryBindingRow(Rect row, EmulatorLayoutElement control, string label, int key,
        PhoneTheme theme)
    {
        var value = auxiliaryBindingTarget == control
            ? Loc.T(L.Games.PressKey)
            : EmulatorKeyCatalog.Name(key);
        if (SettingsRow.Disclosure(row, label, value, theme))
        {
            BeginAuxiliaryKeyBinding(control);
        }
    }

    private static int DrawLabeledSegments(string id, Rect row, string label, IReadOnlyList<string> options,
        int selected, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 7f * scale), label, theme.TextStrong,
            TextStyles.FootnoteEmphasized);
        var segments = new Rect(new Vector2(row.Min.X, row.Min.Y + 29f * scale),
            new Vector2(row.Max.X, row.Max.Y - 7f * scale));
        return SegmentStrip.Draw(id, segments, options, selected, theme);
    }

    private static int VideoFilterIndex(EmulatorVideoFilter filter) => filter switch
    {
        EmulatorVideoFilter.Pixel => 0,
        EmulatorVideoFilter.Balanced => 1,
        EmulatorVideoFilter.Sharp => 2,
        _ => 3,
    };

    private string ControlLabel(EmulatorButtons button) => button switch
    {
        EmulatorButtons.Up => Loc.T(L.Games.ControlUp),
        EmulatorButtons.Down => Loc.T(L.Games.ControlDown),
        EmulatorButtons.Left => Loc.T(L.Games.ControlLeft),
        EmulatorButtons.Right => Loc.T(L.Games.ControlRight),
        _ => CurrentSystem.ButtonLabel(button),
    };

    private void BeginKeyBinding(EmulatorButtons button)
    {
        CancelShortcutBinding();
        auxiliaryBindingTarget = null;
        bindingTarget = button;
        keyboardCapture.SetCaptured(true);
        Array.Clear(bindingKeyStates);
        var keys = EmulatorKeyCatalog.SupportedKeys;
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            bindingKeyStates[key] = keyboardCapture.IsKeyDown(key);
        }
    }

    private void ProcessKeyBinding()
    {
        if (bindingTarget == EmulatorButtons.None && auxiliaryBindingTarget is null)
        {
            return;
        }

        var keys = EmulatorKeyCatalog.SupportedKeys;
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            var down = keyboardCapture.IsKeyDown(key);
            if (down && !bindingKeyStates[key])
            {
                if (key != 0x1B)
                {
                    if (bindingTarget != EmulatorButtons.None)
                    {
                        Settings.SetKey(bindingTarget, key);
                    }
                    else if (auxiliaryBindingTarget is { } auxiliary)
                    {
                        SetAuxiliaryKey(auxiliary, key);
                    }
                    configuration.Save();
                }

                CancelKeyBinding();
                return;
            }

            bindingKeyStates[key] = down;
        }
    }

    private void CancelKeyBinding()
    {
        if (bindingTarget == EmulatorButtons.None && auxiliaryBindingTarget is null)
        {
            return;
        }

        bindingTarget = EmulatorButtons.None;
        auxiliaryBindingTarget = null;
        Array.Clear(bindingKeyStates);
        keyboardCapture.SetCaptured(inputCaptured || shortcutTarget != EmulatorShortcutAction.None);
    }

    private void BeginAuxiliaryKeyBinding(EmulatorLayoutElement control)
    {
        CancelShortcutBinding();
        bindingTarget = EmulatorButtons.None;
        auxiliaryBindingTarget = control;
        keyboardCapture.SetCaptured(true);
        Array.Clear(bindingKeyStates);
        var keys = EmulatorKeyCatalog.SupportedKeys;
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            bindingKeyStates[key] = keyboardCapture.IsKeyDown(key);
        }
    }

    private void SetAuxiliaryKey(EmulatorLayoutElement control, int key)
    {
        switch (control)
        {
            case EmulatorLayoutElement.CUp: Settings.KeyCUp = key; break;
            case EmulatorLayoutElement.CDown: Settings.KeyCDown = key; break;
            case EmulatorLayoutElement.CLeft: Settings.KeyCLeft = key; break;
            case EmulatorLayoutElement.CRight: Settings.KeyCRight = key; break;
        }
    }

    private void BeginShortcutBinding(EmulatorShortcutAction action)
    {
        CancelKeyBinding();
        shortcutTarget = action;
        shortcutCaptureKeys.Clear();
        shortcutCaptureButtons = 0;
        shortcutWaitingForRelease = true;
        shortcutHasInput = false;
        keyboardCapture.SetCaptured(true);
    }

    private void ProcessShortcutBinding()
    {
        if (shortcutTarget == EmulatorShortcutAction.None)
        {
            return;
        }

        var keys = EmulatorKeyCatalog.SupportedKeys;
        var anyKeyDown = false;
        var escapeDown = false;
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            if (!keyboardCapture.IsKeyDown(key))
            {
                continue;
            }

            anyKeyDown = true;
            escapeDown |= key == 0x1B;
        }

        var currentButtons = CurrentShortcutGamepadButtons();
        var anyDown = anyKeyDown || currentButtons != 0;
        if (shortcutWaitingForRelease)
        {
            if (!anyDown)
            {
                shortcutWaitingForRelease = false;
            }

            return;
        }

        if (escapeDown)
        {
            CancelShortcutBinding();
            return;
        }

        if (anyDown)
        {
            for (var index = 0; index < keys.Count; index++)
            {
                var key = keys[index];
                if (key != 0x1B && keyboardCapture.IsKeyDown(key))
                {
                    shortcutCaptureKeys.Add(key);
                }
            }

            shortcutCaptureButtons |= currentButtons;
            shortcutHasInput = shortcutCaptureKeys.Count > 0 || shortcutCaptureButtons != 0;
            return;
        }

        if (!shortcutHasInput)
        {
            return;
        }

        ShortcutFor(shortcutTarget).Set(shortcutCaptureKeys, shortcutCaptureButtons);
        configuration.Save();
        CancelShortcutBinding();
    }

    private void CancelShortcutBinding()
    {
        if (shortcutTarget == EmulatorShortcutAction.None)
        {
            return;
        }

        shortcutTarget = EmulatorShortcutAction.None;
        shortcutCaptureKeys.Clear();
        shortcutCaptureButtons = 0;
        shortcutWaitingForRelease = false;
        shortcutHasInput = false;
        keyboardCapture.SetCaptured(inputCaptured || bindingTarget != EmulatorButtons.None);
    }

    private void CancelAllBindings()
    {
        CancelKeyBinding();
        CancelShortcutBinding();
    }

    private ushort CurrentShortcutGamepadButtons()
    {
        ushort result = 0;
        for (var index = 0; index < ShortcutGamepadButtons.Length; index++)
        {
            var button = ShortcutGamepadButtons[index];
            if (gamepadState.Raw(button) > 0.5f)
            {
                result |= (ushort)button;
            }
        }

        return result;
    }

    private bool ShortcutIsDown(EmulatorShortcutSettings shortcut)
    {
        if (shortcut.IsEmpty)
        {
            return false;
        }

        for (var index = 0; index < shortcut.Keys.Count; index++)
        {
            if (!keyboardCapture.IsKeyDown(shortcut.Keys[index]))
            {
                return false;
            }
        }

        for (var index = 0; index < ShortcutGamepadButtons.Length; index++)
        {
            var button = ShortcutGamepadButtons[index];
            if ((shortcut.GamepadButtons & (ushort)button) != 0 && gamepadState.Raw(button) <= 0.5f)
            {
                return false;
            }
        }

        return true;
    }

    private string ShortcutValue(EmulatorShortcutAction action)
    {
        if (shortcutTarget == action)
        {
            return Loc.T(L.Games.PressCombination);
        }

        var shortcut = ShortcutFor(action);
        if (shortcut.IsEmpty)
        {
            return Loc.T(L.Games.Unassigned);
        }

        var parts = new List<string>(shortcut.Keys.Count + ShortcutGamepadButtons.Length);
        for (var index = 0; index < shortcut.Keys.Count; index++)
        {
            parts.Add(EmulatorKeyCatalog.Name(shortcut.Keys[index]));
        }

        for (var index = 0; index < ShortcutGamepadButtons.Length; index++)
        {
            var button = ShortcutGamepadButtons[index];
            if ((shortcut.GamepadButtons & (ushort)button) != 0)
            {
                parts.Add(GamepadButtonName(button));
            }
        }

        return string.Join(" + ", parts);
    }

    private EmulatorShortcutSettings ShortcutFor(EmulatorShortcutAction action) => action switch
    {
        EmulatorShortcutAction.FastForward => Settings.FastForwardShortcut,
        EmulatorShortcutAction.SaveState => Settings.SaveStateShortcut,
        EmulatorShortcutAction.LoadState => Settings.LoadStateShortcut,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private static string GamepadButtonName(GamepadButtons button) => button switch
    {
        GamepadButtons.DpadUp => "D-pad Up",
        GamepadButtons.DpadDown => "D-pad Down",
        GamepadButtons.DpadLeft => "D-pad Left",
        GamepadButtons.DpadRight => "D-pad Right",
        GamepadButtons.North => "Pad North",
        GamepadButtons.South => "Pad South",
        GamepadButtons.West => "Pad West",
        GamepadButtons.East => "Pad East",
        _ => button.ToString(),
    };

    private void DrawLayoutEditor(in GameContext context)
    {
        var body = context.Body;
        var theme = context.Theme;
        var scale = ImGuiHelpers.GlobalScale;
        GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 25f * scale),
            Loc.T(L.Games.LayoutEditor), theme.TextStrong, TextStyles.Title2);
        var hint = Typography.FitText(Loc.T(L.Games.LayoutEditorHint), body.Width - 32f * scale,
            TextStyles.Footnote);
        Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 51f * scale), hint, theme.TextMuted,
            TextStyles.Footnote);

        var previewBounds = new Rect(new Vector2(body.Min.X + 14f * scale, body.Min.Y + 70f * scale),
            new Vector2(body.Max.X - 14f * scale, body.Max.Y - 116f * scale));
        var gameplayBodySize = TargetGameplayBodySize(theme);
        var previewAspect = gameplayBodySize.X / MathF.Max(1f, gameplayBodySize.Y);
        var preview = FitEditorPreview(previewBounds, previewAspect);
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, preview.Min, preview.Max, 18f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Surface, 0.92f)));
        Squircle.Stroke(drawList, preview.Min, preview.Max, 18f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextMuted, 0.35f)), 1f * scale);

        var previewScale = scale * preview.Width / MathF.Max(1f, gameplayBodySize.X);
        var videoWidth = session is { VideoWidth: > 0 } ? session.VideoWidth : 240;
        var videoHeight = session is { VideoHeight: > 0 } ? session.VideoHeight : 160;
        DrawLayoutPreview(preview, videoWidth, videoHeight, theme, previewScale);
        HandleLayoutDrag(preview, videoWidth, videoHeight, previewScale);

        var selectedScale = $"{MathF.Round(CurrentLayout.For(selectedLayoutElement).SafeScale * 100f):0}%";
        var selectedLabel = $"{LayoutElementLabel(selectedLayoutElement)}  ·  {selectedScale}";
        Typography.DrawCentered(new Vector2(body.Center.X, preview.Max.Y + 16f * scale), selectedLabel,
            theme.TextStrong, TextStyles.FootnoteEmphasized);
        var scaler = new Rect(new Vector2(body.Min.X + 34f * scale, preview.Max.Y + 31f * scale),
            new Vector2(body.Max.X - 34f * scale, preview.Max.Y + 61f * scale));
        DrawLayoutScaleStepper(scaler, theme, scale);

        var buttonY = body.Max.Y - 25f * scale;
        if (GameHud.Button(new Vector2(body.Center.X - 72f * scale, buttonY), new Vector2(126f * scale, 32f * scale),
                Loc.T(L.Games.ResetInterface), theme.TextMuted, theme))
        {
            if (LandscapeMode)
            {
                CurrentLayout.ResetLandscape();
            }
            else
            {
                CurrentLayout.Reset();
            }
            selectedLayoutElement = EmulatorLayoutElement.Screen;
            layoutDirty = true;
            SaveLayoutIfDirty();
        }

        if (GameHud.Button(new Vector2(body.Center.X + 72f * scale, buttonY), new Vector2(126f * scale, 32f * scale),
                Loc.T(L.Games.FinishEditing), Accent, theme))
        {
            FinishLayoutEditing();
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            draggedLayoutElement = null;
            SaveLayoutIfDirty();
        }
    }

    private static Rect FitEditorPreview(Rect bounds, float aspect)
    {
        var width = MathF.Max(1f, bounds.Width);
        var height = width / MathF.Max(0.1f, aspect);
        if (height > bounds.Height)
        {
            height = MathF.Max(1f, bounds.Height);
            width = height * aspect;
        }

        return CenteredRect(bounds.Center, new Vector2(width, height));
    }

    private Vector2 TargetGameplayBodySize(PhoneTheme theme)
    {
        var window = LandscapeMode
            ? PhoneSizeCatalog.LandscapeSizeFor(configuration.PhoneScale)
            : PhoneSizeCatalog.SizeFor(configuration.PhoneScale);
        var width = window.X;
        var height = window.Y;
        if (LandscapeMode)
        {
            height -= DeviceChrome.RailWidth * 2f;
        }
        else
        {
            width -= DeviceChrome.RailWidth * 2f;
        }

        width -= theme.BezelThickness * 2f + ShellScreenPainter.ImmersiveInset * 2f;
        height -= theme.BezelThickness * 2f + ShellScreenPainter.ImmersiveInset * 2f;
        return new Vector2(MathF.Max(1f, width), MathF.Max(1f, height));
    }

    private void DrawLayoutPreview(Rect preview, int videoWidth, int videoHeight, PhoneTheme theme, float scale)
    {
        var screen = CalculateScreenOuter(preview, videoWidth, videoHeight, scale, false);
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, screen.Min, screen.Max, 6f * scale,
            ImGui.GetColorU32(new Vector4(0.025f, 0.03f, 0.04f, 1f)));
        Squircle.Stroke(drawList, screen.Min, screen.Max, 6f * scale,
            ImGui.GetColorU32(Accent with { W = 0.22f }), 1f * scale);
        var wrap = video.Wrap;
        if (wrap is not null && session is { VideoWidth: > 0, VideoHeight: > 0 })
        {
            var image = CalculateImageRect(screen, videoWidth, videoHeight, scale, EmulatorVideoFilter.Smooth);
            drawList.AddImage(wrap.Handle, image.Min, image.Max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu);
        }
        else
        {
            Typography.DrawCentered(screen.Center, Loc.T(L.Games.LayoutScreen),
                new Vector4(1f, 1f, 1f, 0.56f), TextStyles.Caption1);
        }

        _ = DrawControls(preview, theme, scale, CurrentSystem.Controls);
        if (CurrentSystem.InputProfile == EmulatorInputProfile.Nintendo64)
        {
            _ = DrawCButtons(preview, theme, scale);
        }
        DrawFastForwardControl(preview, theme, scale, false);

        var selected = LayoutElementRect(selectedLayoutElement, preview, videoWidth, videoHeight, scale);
        var expand = new Vector2(4f * scale);
        Squircle.Stroke(drawList, selected.Min - expand, selected.Max + expand, 10f * scale,
            ImGui.GetColorU32(GamePalette.Lighten(Accent, 0.28f)), 2f * scale);
    }

    private void HandleLayoutDrag(Rect preview, int videoWidth, int videoHeight, float scale)
    {
        var mouse = ImGui.GetMousePos();
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            ImGui.IsMouseHoveringRect(preview.Min, preview.Max, false))
        {
            for (var index = 0; index < EditorHitOrder.Length; index++)
            {
                var candidate = EditorHitOrder[index];
                if (!IsLayoutElementVisible(candidate))
                {
                    continue;
                }

                var rect = LayoutElementRect(candidate, preview, videoWidth, videoHeight, scale);
                if (!ImGui.IsMouseHoveringRect(rect.Min, rect.Max, false))
                {
                    continue;
                }

                selectedLayoutElement = candidate;
                draggedLayoutElement = candidate;
                layoutDragOffset = mouse - rect.Center;
                break;
            }
        }

        if (draggedLayoutElement is not { } dragged || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            return;
        }

        var currentRect = LayoutElementRect(dragged, preview, videoWidth, videoHeight, scale);
        var center = ClampCenter(mouse - layoutDragOffset, currentRect.Size * 0.5f, preview);
        var element = CurrentLayout.For(dragged);
        element.X = Math.Clamp((center.X - preview.Min.X) / MathF.Max(1f, preview.Width), 0f, 1f);
        element.Y = Math.Clamp((center.Y - preview.Min.Y) / MathF.Max(1f, preview.Height), 0f, 1f);
        layoutDirty = true;
    }

    private void DrawLayoutScaleStepper(Rect row, PhoneTheme theme, float scale)
    {
        var element = CurrentLayout.For(selectedLayoutElement);
        var buttonSize = new Vector2(48f, 30f) * scale;
        var minus = CenteredRect(new Vector2(row.Center.X - 72f * scale, row.Center.Y), buttonSize);
        var plus = CenteredRect(new Vector2(row.Center.X + 72f * scale, row.Center.Y), buttonSize);
        if (DrawScaleStepButton("layoutScaleMinus", minus, "-", theme, scale))
        {
            ChangeLayoutScale(element, -5);
        }

        if (DrawScaleStepButton("layoutScalePlus", plus, "+", theme, scale))
        {
            ChangeLayoutScale(element, 5);
        }

        Typography.DrawCentered(row.Center, $"{MathF.Round(element.SafeScale * 100f):0}%", theme.TextStrong,
            TextStyles.Headline);
    }

    private bool DrawScaleStepButton(string id, Rect rect, string label, PhoneTheme theme, float scale)
    {
        ImGui.SetCursorScreenPos(rect.Min);
        var clicked = ImGui.InvisibleButton($"##{id}", rect.Size);
        var hovered = ImGui.IsItemHovered();
        var pressed = ImGui.IsItemActive();
        var fill = pressed ? Accent with { W = 0.82f } :
            hovered ? Accent with { W = 0.58f } : theme.GroupedCard with { W = 0.72f };
        Squircle.Fill(ImGui.GetWindowDrawList(), rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(fill));
        Squircle.Stroke(ImGui.GetWindowDrawList(), rect.Min, rect.Max, rect.Height * 0.5f,
            ImGui.GetColorU32(hovered ? Accent : theme.Separator), 1f * scale);
        Typography.DrawCentered(rect.Center, label, theme.TextStrong, TextStyles.Title3);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return clicked;
    }

    private void ChangeLayoutScale(EmulatorElementLayout element, int deltaPercent)
    {
        var current = (int)MathF.Round(element.SafeScale * 100f / 5f) * 5;
        var next = Math.Clamp(current + deltaPercent, 50, 100);
        if (next == current)
        {
            return;
        }

        element.Scale = next / 100f;
        layoutDirty = true;
        SaveLayoutIfDirty();
    }

    private static string LayoutElementLabel(EmulatorLayoutElement element) => element switch
    {
        EmulatorLayoutElement.Screen => Loc.T(L.Games.LayoutScreen),
        EmulatorLayoutElement.Dpad => "D-pad",
        EmulatorLayoutElement.A => "A",
        EmulatorLayoutElement.B => "B",
        EmulatorLayoutElement.X => "X",
        EmulatorLayoutElement.Y => "Y",
        EmulatorLayoutElement.L => "L",
        EmulatorLayoutElement.R => "R",
        EmulatorLayoutElement.L2 => "L2 / Z",
        EmulatorLayoutElement.R2 => "R2",
        EmulatorLayoutElement.L3 => "L3",
        EmulatorLayoutElement.R3 => "R3",
        EmulatorLayoutElement.Dpad2 => "Y cursor",
        EmulatorLayoutElement.CUp => "C Up",
        EmulatorLayoutElement.CDown => "C Down",
        EmulatorLayoutElement.CLeft => "C Left",
        EmulatorLayoutElement.CRight => "C Right",
        EmulatorLayoutElement.Select => "Select",
        EmulatorLayoutElement.Start => "Start",
        EmulatorLayoutElement.FastForward => "FF",
        _ => string.Empty,
    };

    private bool IsLayoutElementVisible(EmulatorLayoutElement element) => element switch
    {
        EmulatorLayoutElement.Screen or EmulatorLayoutElement.Dpad or EmulatorLayoutElement.FastForward => true,
        EmulatorLayoutElement.Dpad2 => CurrentSystem.InputProfile == EmulatorInputProfile.WonderSwan,
        EmulatorLayoutElement.CUp or EmulatorLayoutElement.CDown or EmulatorLayoutElement.CLeft or
            EmulatorLayoutElement.CRight => CurrentSystem.InputProfile == EmulatorInputProfile.Nintendo64,
        EmulatorLayoutElement.A => (CurrentSystem.Controls & EmulatorButtons.A) != 0,
        EmulatorLayoutElement.B => (CurrentSystem.Controls & EmulatorButtons.B) != 0,
        EmulatorLayoutElement.X => (CurrentSystem.Controls & EmulatorButtons.X) != 0,
        EmulatorLayoutElement.Y => (CurrentSystem.Controls & EmulatorButtons.Y) != 0,
        EmulatorLayoutElement.L => (CurrentSystem.Controls & EmulatorButtons.L) != 0,
        EmulatorLayoutElement.R => (CurrentSystem.Controls & EmulatorButtons.R) != 0,
        EmulatorLayoutElement.L2 => (CurrentSystem.Controls & EmulatorButtons.L2) != 0,
        EmulatorLayoutElement.R2 => (CurrentSystem.Controls & EmulatorButtons.R2) != 0,
        EmulatorLayoutElement.L3 => (CurrentSystem.Controls & EmulatorButtons.L3) != 0,
        EmulatorLayoutElement.R3 => (CurrentSystem.Controls & EmulatorButtons.R3) != 0,
        EmulatorLayoutElement.Select => (CurrentSystem.Controls & EmulatorButtons.Select) != 0,
        EmulatorLayoutElement.Start => (CurrentSystem.Controls & EmulatorButtons.Start) != 0,
        _ => false,
    };

    private void FinishLayoutEditing()
    {
        editingLayout = false;
        draggedLayoutElement = null;
        SaveLayoutIfDirty();
    }

    private void SaveLayoutIfDirty()
    {
        if (!layoutDirty)
        {
            return;
        }

        layoutDirty = false;
        configuration.Save();
    }

    private void DrawGame(in GameContext context)
    {
        var active = session!;
        var body = context.Body;
        var theme = context.Theme;
        var scale = ImGuiHelpers.GlobalScale;
        var fastForwardRect = LayoutElementRect(EmulatorLayoutElement.FastForward, body,
            active.VideoWidth, active.VideoHeight, scale);
        if (ImGui.IsMouseHoveringRect(fastForwardRect.Min, fastForwardRect.Max) &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            fastForwardLatched = !fastForwardLatched;
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
        active.Advance(context.DeltaSeconds, fastForward ? Math.Clamp(Settings.FastForwardSpeed, 2, 4) : 1f);
        var screen = CalculateScreenOuter(body, active.VideoWidth, active.VideoHeight, scale);
        var imageRect = CalculateImageRect(screen, active.VideoWidth, active.VideoHeight, scale,
            Settings.VideoFilter);
        if (active.HasNewFrame)
        {
            video.Upload(active.VideoFrame, active.VideoWidth, active.VideoHeight, Settings.VideoFilter,
                Math.Max(1, (int)MathF.Round(imageRect.Width)), Math.Max(1, (int)MathF.Round(imageRect.Height)));
        }

        DrawScreen(screen, imageRect, active, theme, scale);
        var buttons = KeyboardInput() | GamepadInput() | DrawControls(body, theme, scale, active.System.Controls);
        var touchCButtons = active.System.InputProfile == EmulatorInputProfile.Nintendo64
            ? DrawCButtons(body, theme, scale)
            : Vector2.Zero;
        DrawFastForwardControl(body, theme, scale, fastForward);
        SuppressGameInput();
        active.Input = BuildInputState(buttons, touchCButtons);
    }

    private Rect CalculateScreenOuter(Rect body, int videoWidth, int videoHeight, float scale,
        bool pixelPerfect = true)
    {
        var layout = CurrentLayout.Screen;
        var aspect = videoWidth > 0 && videoHeight > 0 ? videoWidth / (float)videoHeight : 1.5f;
        var maximum = LandscapeMode
            ? new Vector2(MathF.Max(1f, body.Width * 0.66f),
                MathF.Max(1f, MathF.Min(body.Height, 420f * scale)))
            : new Vector2(MathF.Max(1f, body.Width - 16f * scale),
                MathF.Max(1f, MathF.Min(body.Height * 0.46f, 274f * scale)));
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
        if (pixelPerfect && Settings.VideoFilter == EmulatorVideoFilter.Pixel && videoWidth > 0 && videoHeight > 0)
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

    internal static Vector2 FitSizeWithin(Vector2 desired, Vector2 bounds)
    {
        var fit = MathF.Min(1f, MathF.Min(bounds.X / MathF.Max(1f, desired.X),
            bounds.Y / MathF.Max(1f, desired.Y)));
        return desired * fit;
    }

    private static Rect CalculateImageRect(Rect outer, int videoWidth, int videoHeight, float scale,
        EmulatorVideoFilter filter)
    {
        if (videoWidth <= 0 || videoHeight <= 0)
        {
            return new Rect(outer.Center, outer.Center);
        }

        var available = outer.Size - new Vector2(ScreenPadding * scale);
        if (filter == EmulatorVideoFilter.Pixel)
        {
            var integerScale = NearestNeighborScaler.IntegerScale(videoWidth, videoHeight,
                available.X, available.Y);
            var size = new Vector2(videoWidth * integerScale, videoHeight * integerScale);
            var min = new Vector2(MathF.Round(outer.Center.X - size.X * 0.5f),
                MathF.Round(outer.Center.Y - size.Y * 0.5f));
            return new Rect(min, min + size);
        }

        var aspect = videoWidth / (float)videoHeight;
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

    private Rect LayoutElementRect(EmulatorLayoutElement element, Rect area, int videoWidth, int videoHeight,
        float scale)
    {
        var layout = CurrentLayout.For(element);
        var elementScale = scale * layout.SafeScale;
        var center = LayoutCenter(area, layout);
        return element switch
        {
            EmulatorLayoutElement.Screen => CalculateScreenOuter(area, videoWidth, videoHeight, scale, false),
            EmulatorLayoutElement.Dpad or EmulatorLayoutElement.Dpad2 =>
                CenteredRect(center, new Vector2(87f * elementScale)),
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

    private EmulatorButtons KeyboardInput()
    {
        var settings = Settings;
        var result = EmulatorButtons.None;
        for (var index = 0; index < BindingOrder.Length; index++)
        {
            var button = BindingOrder[index];
            if (keyboardCapture.IsKeyDown(settings.KeyFor(button)))
            {
                result |= button;
            }
        }

        return result;
    }

    private EmulatorButtons GamepadInput()
    {
        var result = EmulatorButtons.None;
        var leftStick = gamepadState.LeftStick;
        var stickAsDirections = CurrentSystem.InputProfile is not EmulatorInputProfile.Nintendo64 and
            not EmulatorInputProfile.PlayStation;
        if (gamepadState.Raw(GamepadButtons.DpadUp) > 0.5f || stickAsDirections && leftStick.Y > 0.5f)
            result |= EmulatorButtons.Up;
        if (gamepadState.Raw(GamepadButtons.DpadDown) > 0.5f || stickAsDirections && leftStick.Y < -0.5f)
            result |= EmulatorButtons.Down;
        if (gamepadState.Raw(GamepadButtons.DpadLeft) > 0.5f || stickAsDirections && leftStick.X < -0.5f)
            result |= EmulatorButtons.Left;
        if (gamepadState.Raw(GamepadButtons.DpadRight) > 0.5f || stickAsDirections && leftStick.X > 0.5f)
            result |= EmulatorButtons.Right;
        if (gamepadState.Raw(GamepadButtons.East) > 0.5f) result |= EmulatorButtons.A;
        if (gamepadState.Raw(GamepadButtons.South) > 0.5f) result |= EmulatorButtons.B;
        if (gamepadState.Raw(GamepadButtons.North) > 0.5f) result |= EmulatorButtons.X;
        if (gamepadState.Raw(GamepadButtons.West) > 0.5f) result |= EmulatorButtons.Y;
        if (gamepadState.Raw(GamepadButtons.L1) > 0.5f) result |= EmulatorButtons.L;
        if (gamepadState.Raw(GamepadButtons.R1) > 0.5f) result |= EmulatorButtons.R;
        if (gamepadState.Raw(GamepadButtons.L2) > 0.5f) result |= EmulatorButtons.L2;
        if (gamepadState.Raw(GamepadButtons.R2) > 0.5f) result |= EmulatorButtons.R2;
        if (gamepadState.Raw(GamepadButtons.L3) > 0.5f) result |= EmulatorButtons.L3;
        if (gamepadState.Raw(GamepadButtons.R3) > 0.5f) result |= EmulatorButtons.R3;
        if (gamepadState.Raw(GamepadButtons.Start) > 0.5f) result |= EmulatorButtons.Start;
        if (gamepadState.Raw(GamepadButtons.Select) > 0.5f) result |= EmulatorButtons.Select;
        return result;
    }

    private EmulatorInputState BuildInputState(EmulatorButtons buttons, Vector2 touchCButtons)
    {
        var profile = CurrentSystem.InputProfile;
        if (profile is not EmulatorInputProfile.Nintendo64 and not EmulatorInputProfile.PlayStation)
        {
            return new EmulatorInputState(buttons);
        }

        var left = gamepadState.LeftStick;
        if (profile == EmulatorInputProfile.Nintendo64)
        {
            // Mupen maps RetroPad B/Y to the physical N64 A/B buttons. Keep the
            // Aetherphone-facing controls labelled A/B and translate only at the core boundary.
            var logicalButtons = buttons;
            buttons &= ~(EmulatorButtons.A | EmulatorButtons.B);
            if ((logicalButtons & EmulatorButtons.A) != 0) buttons |= EmulatorButtons.B;
            if ((logicalButtons & EmulatorButtons.B) != 0) buttons |= EmulatorButtons.Y;

            if (MathF.Abs(left.X) < 0.2f)
            {
                left.X = (buttons & EmulatorButtons.Left) != 0 ? -1f :
                    (buttons & EmulatorButtons.Right) != 0 ? 1f : 0f;
            }

            if (MathF.Abs(left.Y) < 0.2f)
            {
                left.Y = (buttons & EmulatorButtons.Up) != 0 ? 1f :
                    (buttons & EmulatorButtons.Down) != 0 ? -1f : 0f;
            }

            buttons &= ~(EmulatorButtons.Up | EmulatorButtons.Down | EmulatorButtons.Left | EmulatorButtons.Right);
        }

        var right = gamepadState.RightStick;
        if (profile == EmulatorInputProfile.Nintendo64)
        {
            var keyboardC = KeyboardCButtons();
            if (MathF.Abs(right.X) < 0.2f)
            {
                right.X = Math.Clamp(keyboardC.X + touchCButtons.X, -1f, 1f);
            }

            if (MathF.Abs(right.Y) < 0.2f)
            {
                // Dalamud uses positive Y for up; the touch/keyboard helper uses negative Y for up.
                right.Y = -Math.Clamp(keyboardC.Y + touchCButtons.Y, -1f, 1f);
            }
        }

        return new EmulatorInputState(buttons, ToAnalog(left.X), ToAnalog(-left.Y),
            ToAnalog(right.X), ToAnalog(-right.Y));
    }

    private Vector2 KeyboardCButtons()
    {
        var result = Vector2.Zero;
        if (keyboardCapture.IsKeyDown(Settings.KeyCUp)) result.Y -= 1f;
        if (keyboardCapture.IsKeyDown(Settings.KeyCDown)) result.Y += 1f;
        if (keyboardCapture.IsKeyDown(Settings.KeyCLeft)) result.X -= 1f;
        if (keyboardCapture.IsKeyDown(Settings.KeyCRight)) result.X += 1f;
        return result;
    }

    private static short ToAnalog(float value) =>
        (short)MathF.Round(Math.Clamp(value, -1f, 1f) * short.MaxValue);

    private void SuppressGameInput()
    {
        var io = ImGui.GetIO();
        io.WantCaptureKeyboard = true;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
        ImGui.SetNextFrameWantCaptureKeyboard(true);
        keyState.ClearAll();
    }

    private void SetInputCaptured(bool captured)
    {
        if (inputCaptured == captured)
        {
            return;
        }

        inputCaptured = captured;
        keyboardCapture.SetCaptured(captured);
        if (captured)
        {
            var io = ImGui.GetIO();
            gamepadNavigationWasEnabled = (io.ConfigFlags & ImGuiConfigFlags.NavEnableGamepad) != 0;
            gamepadCaptureActive = true;
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
            keyState.ClearAll();
            return;
        }

        RestoreGamepadNavigation();
    }

    private void RestoreGamepadNavigation()
    {
        if (!gamepadCaptureActive)
        {
            return;
        }

        if (!gamepadNavigationWasEnabled)
        {
            var io = ImGui.GetIO();
            io.ConfigFlags &= ~ImGuiConfigFlags.NavEnableGamepad;
        }

        gamepadCaptureActive = false;
    }

    private void DrawFolderBrowser(in GameContext context)
    {
        var body = context.Body;
        var theme = context.Theme;
        var scale = ImGuiHelpers.GlobalScale;
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
        directoryBrowser.Open(Settings.RomFolders.LastOrDefault());
        browserPurpose = EmulatorBrowserPurpose.ScanFolder;
    }

    private void OpenRomBrowser()
    {
        CancelAllBindings();
        var initialPath = Settings.ImportedFiles.LastOrDefault(File.Exists) ??
                          Settings.RomFolders.LastOrDefault(Directory.Exists);
        directoryBrowser.OpenFiles(initialPath, CurrentSystem.Extensions);
        browserPurpose = EmulatorBrowserPurpose.ImportRom;
    }

    private void SelectCurrentFolder()
    {
        if (directoryBrowser.IsDriveList || !Directory.Exists(directoryBrowser.CurrentPath))
        {
            return;
        }

        var path = Path.GetFullPath(directoryBrowser.CurrentPath);
        if (!Settings.RomFolders.Any(configured => string.Equals(Path.GetFullPath(configured), path,
                StringComparison.OrdinalIgnoreCase)))
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
        StopGame();
        selectedSystemId = entry.System.Id;
        error = string.Empty;
        stateMessage = string.Empty;
        try
        {
            var path = entry.Path;
            var corePath = Path.Combine(coreDirectory, entry.System.CoreFileName);
            if (!File.Exists(corePath))
            {
                throw new FileNotFoundException($"{Loc.T(L.Games.CoreMissing)} ({entry.System.Name})", corePath);
            }

            var settings = configuration.Emulator.ForCore(entry.System);
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
        keyboardCapture.Dispose();
        video.Dispose();
    }
}
