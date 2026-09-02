using System.Diagnostics;
using Aetherphone.Core.Game;
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
    private readonly TextureStore textures = new();
    private readonly FrameRenderer renderer;
    private readonly FakeFontAtlas fontAtlas;
    private readonly FakeUiBuilder uiBuilder;
    private readonly FakeFramework framework;
    private readonly FakeClientState clientState;
    private readonly Plugin plugin;
    private readonly Stopwatch stopwatch = new();
    private int frameIndex;

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
        var data = new FakeDataManager(options.SqpackDirectory);
        var pluginInterface = new FakePluginInterface(options.ConfigDirectory, options.AssetDirectory, uiBuilder,
            typeof(Plugin).Assembly);
        HarnessLog.Note(data.HasGameData ? "game data: " + options.SqpackDirectory : "game data: none (sheet reads will fail)");
        GameMemory.Detach();
        if (!data.HasGameData)
        {
            GameSheets.MarkUnavailable();
        }

        Plugin.PluginInterface = pluginInterface;
        Plugin.CommandManager = new FakeCommandManager();
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
        HarnessLog.Note($"global scale: {ImGuiHelpers.GlobalScale}");
        plugin = new Plugin();
        HarnessLog.Note("plugin constructed");
    }

    public void Step(int frames)
    {
        for (var index = 0; index < frames; index++)
        {
            Step();
        }
    }

    public void Step()
    {
        fontAtlas.RebuildIfDirty();
        var io = ImGui.GetIO();
        io.DeltaTime = FrameSeconds;
        framework.Tick(TimeSpan.FromSeconds(FrameSeconds));
        ImGui.NewFrame();
        uiBuilder.InvokeDraw();
        ImGui.Render();
        stopwatch.Restart();
        renderer.Render(ImGui.GetDrawData(), 24, 26, 32);
        stopwatch.Stop();
        uiBuilder.FrameCount += 1;
        frameIndex += 1;
        if (frameIndex % 30 == 0)
        {
            HarnessLog.Note($"frame {frameIndex}: {renderer.TrianglesDrawn} triangles, raster {stopwatch.Elapsed.TotalMilliseconds:F1} ms");
        }
    }

    public void OpenPhone() => uiBuilder.InvokeOpenMainUi();

    public void Login() => clientState.SimulateLogin();

    public void MouseMove(float x, float y) => ImGui.GetIO().AddMousePosEvent(x, y);

    public void MouseButton(int button, bool down) => ImGui.GetIO().AddMouseButtonEvent(button, down);

    public void Screenshot(string path)
    {
        PngWriter.Write(path, renderer.Resolve(), renderer.Width, renderer.Height);
        HarnessLog.Note($"screenshot written to {path}");
    }

    public void Dispose()
    {
        plugin.Dispose();
        fontAtlas.Dispose();
        ImGui.DestroyContext();
    }
}
