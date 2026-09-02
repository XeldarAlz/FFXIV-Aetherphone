using System.Diagnostics;
using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Onboarding;
using Aetherphone.Harness.Fakes;
using Aetherphone.Harness.Fonts;
using Aetherphone.Harness.Native;
using Aetherphone.Harness.Rendering;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Harness.Host;

internal sealed unsafe class PhoneHost : IDisposable
{
    private const float FrameSeconds = 1f / 60f;
    private const float DalamudDefaultFontPx = 16.5f;
    private const int HomeReturnFrames = 20;
    private const float PhoneMargin = 40f;
    private readonly TextureStore textures = new();
    private FrameRenderer renderer;
    private int rasterizedFrame = -1;
    private readonly FakeFontAtlas fontAtlas;
    private readonly FakeUiBuilder uiBuilder;
    private readonly FakeFramework framework;
    private readonly FakeClientState clientState;
    private readonly FakeCommandManager commands;
    private readonly FakeDataManager data;
    private readonly Plugin plugin;
    private readonly Stopwatch stopwatch = new();
    private readonly List<KeyValuePair<string, Rect>> anchorScratch = new();

    public PhoneHost(HarnessOptions options)
    {
        NativeImGuiLoader.Configure();
        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(options.Width, options.Height);
        io.DeltaTime = FrameSeconds;
        io.IniFilename = null;
        io.FontGlobalScale = 1f;
        renderer = new FrameRenderer(options.Width, options.Height, textures);
        var assets = new DalamudAssetFiles(options.AssetDirectory);
        fontAtlas = new FakeFontAtlas(textures, assets, "Aetherphone.Harness");
        var defaultHandle = (FakeFontHandle)fontAtlas.NewDelegateFontHandle(toolkit =>
            toolkit.OnPreBuild(preBuild => preBuild.AddDalamudDefaultFont(DalamudDefaultFontPx)));
        var iconHandle = (FakeFontHandle)fontAtlas.NewDelegateFontHandle(toolkit =>
            toolkit.OnPreBuild(preBuild => preBuild.AddFontAwesomeIconFont(new SafeFontConfig { SizePx = DalamudDefaultFontPx })));
        uiBuilder = new FakeUiBuilder(fontAtlas, defaultHandle, iconHandle);
        framework = new FakeFramework();
        clientState = new FakeClientState();
        commands = new FakeCommandManager();
        data = new FakeDataManager(options.SqpackDirectory);
        var pluginInterface = new FakePluginInterface(options.ConfigDirectory, options.AssetDirectory, uiBuilder,
            typeof(Plugin).Assembly);
        HarnessLog.Note(data.HasGameData ? "game data: " + options.SqpackDirectory : "game data: none, sheet-bound apps disabled");
        GameMemory.Detach();
        if (!data.HasGameData)
        {
            GameSheets.MarkUnavailable();
        }

        UiAnchors.ForceRecording = true;
        Plugin.PluginInterface = pluginInterface;
        Plugin.CommandManager = commands;
        Plugin.DtrBar = new FakeDtrBar();
        Plugin.ChatGui = new FakeChatGui();
        Plugin.DataManager = data;
        Plugin.ObjectTable = new FakeObjectTable();
        Plugin.ClientState = clientState;
        Plugin.Framework = framework;
        Plugin.Condition = new FakeCondition();
        Plugin.DutyState = NullProxy.Create<IDutyState>();
        Plugin.TextureProvider = new FakeTextureProvider(textures, data);
        Plugin.TextureSubstitution = NullProxy.Create<ITextureSubstitutionProvider>();
        Plugin.GameGui = NullProxy.Create<IGameGui>();
        Plugin.NamePlateGui = NullProxy.Create<INamePlateGui>();
        Plugin.ContextMenu = NullProxy.Create<IContextMenu>();
        Plugin.Log = new FakePluginLog();
        Plugin.GameConfig = NullProxy.Create<IGameConfig>();
        Plugin.UnlockState = NullProxy.Create<IUnlockState>();
        Plugin.InteropProvider = NullProxy.Create<IGameInteropProvider>();
        Plugin.KeyState = NullProxy.Create<IKeyState>();
        Plugin.GamepadState = NullProxy.Create<IGamepadState>();
        Plugin.AetheryteList = NullProxy.Create<IAetheryteList>();
        fontAtlas.RebuildIfDirty();
        plugin = new Plugin();
        uiBuilder.PinWindow(plugin.MainWindow, new Vector2(PhoneMargin, PhoneMargin));
        HarnessLog.Note("plugin constructed");
    }

    public int FrameIndex { get; private set; }

    public int Width => renderer.Width;

    public int Height => renderer.Height;

    public TextureStore Textures => textures;

    public ImDrawDataPtr DrawData => ImGui.GetDrawData();

    public Vector2 PhoneSize => plugin.MainWindow.Size ?? new Vector2(Width, Height);

    public float PhoneMarginPixels => PhoneMargin;

    public void SetDisplaySize(int width, int height)
    {
        ImGui.GetIO().DisplaySize = new Vector2(width, height);
        if (width == renderer.Width && height == renderer.Height)
        {
            return;
        }

        renderer = new FrameRenderer(width, height, textures);
        rasterizedFrame = -1;
    }

    public bool HasGameData => data.HasGameData;

    public bool IsLoggedIn => clientState.IsLoggedIn;

    public bool PhoneOpen => plugin.MainWindow.IsOpen;

    public string? CurrentAppId => plugin.Shell.CurrentAppId;

    public string MinimizePhase => plugin.Shell.MinimizePhase.ToString();

    public bool HomeEditing => plugin.Shell.HomeEditing;

    public Rect PhoneRect
    {
        get
        {
            var window = plugin.MainWindow;
            if (window.LastSize.X <= 0f || window.LastSize.Y <= 0f)
            {
                return new Rect(Vector2.Zero, new Vector2(Width, Height));
            }

            return new Rect(window.LastPosition, window.LastPosition + window.LastSize);
        }
    }

    public void Step(int frames)
    {
        for (var index = 0; index < frames; index++)
        {
            Step();
        }
    }

    public void Step() => Step(FrameSeconds);

    public void Step(float deltaSeconds)
    {
        fontAtlas.RebuildIfDirty();
        var io = ImGui.GetIO();
        io.DeltaTime = deltaSeconds;
        framework.Tick(TimeSpan.FromSeconds(deltaSeconds));
        stopwatch.Restart();
        ImGui.NewFrame();
        uiBuilder.InvokeDraw();
        ImGui.Render();
        stopwatch.Stop();
        uiBuilder.FrameCount += 1;
        FrameIndex += 1;
        if (FrameIndex % 300 == 0)
        {
            HarnessLog.Note($"frame {FrameIndex}: {ImGui.GetDrawData().TotalIdxCount / 3} triangles, simulate {stopwatch.Elapsed.TotalMilliseconds:F1} ms");
        }
    }

    public void Rasterize()
    {
        if (rasterizedFrame == FrameIndex)
        {
            return;
        }

        renderer.Render(ImGui.GetDrawData(), 24, 26, 32);
        rasterizedFrame = FrameIndex;
    }

    public void OpenPhone()
    {
        if (!plugin.MainWindow.IsOpen)
        {
            uiBuilder.InvokeOpenMainUi();
        }
    }

    public void OpenApp(string appId)
    {
        var window = plugin.MainWindow;
        window.Maximize();
        window.IsOpen = true;
        var current = plugin.Shell.CurrentAppId;
        if (current is not null && current != appId)
        {
            plugin.Shell.GoHome();
            Step(HomeReturnFrames);
        }

        plugin.Shell.OpenApp(appId);
    }

    public void GoHome() => plugin.Shell.GoHome();

    public void OpenSettings() => uiBuilder.InvokeOpenConfigUi();

    public void Login() => clientState.SimulateLogin();

    public void Logout() => clientState.SimulateLogout();

    public bool RunCommand(string text) => commands.ProcessCommand(text);

    public void MouseMove(Vector2 screen) => ImGui.GetIO().AddMousePosEvent(screen.X, screen.Y);

    public void MouseButton(int button, bool down) => ImGui.GetIO().AddMouseButtonEvent(button, down);

    public void MouseWheel(float deltaX, float deltaY) => ImGui.GetIO().AddMouseWheelEvent(deltaX, deltaY);

    public void Tap(Vector2 screen, int button, int settleFrames)
    {
        MouseMove(screen);
        Step();
        MouseButton(button, true);
        Step();
        MouseButton(button, false);
        Step();
        Step(settleFrames);
    }

    public void Drag(Vector2 from, Vector2 to, int frames, int settleFrames)
    {
        MouseMove(from);
        Step();
        MouseButton(0, true);
        Step();
        var steps = Math.Max(frames, 1);
        for (var index = 1; index <= steps; index++)
        {
            MouseMove(Vector2.Lerp(from, to, index / (float)steps));
            Step();
        }

        MouseButton(0, false);
        Step();
        Step(settleFrames);
    }

    public void Scroll(Vector2 screen, float deltaY, int settleFrames)
    {
        MouseMove(screen);
        Step();
        MouseWheel(0f, deltaY);
        Step();
        Step(settleFrames);
    }

    public void TypeText(string text, int settleFrames)
    {
        TextInput(text);
        Step();
        Step(settleFrames);
    }

    public bool PressKey(string name, int settleFrames)
    {
        if (!KeyEvent(name, true))
        {
            return false;
        }

        Step();
        KeyEvent(name, false);
        Step();
        Step(settleFrames);
        return true;
    }

    public bool KeyEvent(string name, bool down)
    {
        if (!BrowserKeys.TryMap(name, out var key, out var modifier))
        {
            return false;
        }

        KeyEvent(key, modifier, down);
        return true;
    }

    public void KeyEvent(ImGuiKey key, ImGuiKey modifier, bool down)
    {
        var io = ImGui.GetIO();
        if (modifier != ImGuiKey.None)
        {
            io.AddKeyEvent(modifier, down);
        }

        io.AddKeyEvent(key, down);
    }

    public void TextInput(string text) => ImGui.GetIO().AddInputCharacters(text);

    public void MouseLeave() => ImGui.GetIO().AddMousePosEvent(-float.MaxValue, -float.MaxValue);

    public List<KeyValuePair<string, Rect>> Anchors()
    {
        UiAnchors.CopyTo(anchorScratch);
        return anchorScratch;
    }

    public bool TryFindAnchor(string key, out Rect rect) => UiAnchors.TryGet(key, out rect);

    public byte[] ScreenshotPng(bool cropToPhone) => ScreenshotPng(cropToPhone, false, out _, out _);

    public byte[] ScreenshotPng(bool cropToPhone, bool fast, out int originX, out int originY)
    {
        var pixels = ScreenshotRaw(cropToPhone, out var width, out var height, out originX, out originY);
        return PngWriter.Encode(pixels, width, height, fast);
    }

    public byte[] ScreenshotRaw(bool cropToPhone, out int width, out int height, out int originX, out int originY)
    {
        Rasterize();
        originX = 0;
        originY = 0;
        width = Width;
        height = Height;
        if (cropToPhone)
        {
            var rect = PhoneRect;
            originX = Math.Clamp((int)MathF.Floor(rect.Min.X), 0, Width - 1);
            originY = Math.Clamp((int)MathF.Floor(rect.Min.Y), 0, Height - 1);
            width = Math.Clamp((int)MathF.Ceiling(rect.Max.X), originX + 1, Width) - originX;
            height = Math.Clamp((int)MathF.Ceiling(rect.Max.Y), originY + 1, Height) - originY;
        }

        return renderer.Resolve(originX, originY, width, height);
    }

    public void Screenshot(string path, bool cropToPhone)
    {
        File.WriteAllBytes(path, ScreenshotPng(cropToPhone));
        HarnessLog.Note($"screenshot written to {path}");
    }

    public void Dispose()
    {
        plugin.Dispose();
        fontAtlas.Dispose();
        ImGui.DestroyContext();
    }
}
