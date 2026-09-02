using Aetherphone.Harness.Fonts;
using Aetherphone.Harness.Host;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.FontIdentifier;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeUiBuilder : IUiBuilder
{
    private const float DefaultSizePt = 12f;
    private const float DefaultSizePx = DefaultSizePt * 4f / 3f;
    private readonly FakeFontHandle defaultHandle;
    private readonly FakeFontHandle iconHandle;
    private readonly WindowDrawer windowDrawer = new();

    public FakeUiBuilder(FakeFontAtlas atlas, FakeFontHandle defaultHandle, FakeFontHandle iconHandle)
    {
        FontAtlas = atlas;
        this.defaultHandle = defaultHandle;
        this.iconHandle = iconHandle;
    }

    public event Action? Draw;

    public event Action? OpenConfigUi;

    public event Action? OpenMainUi;

    public event Action? ResizeBuffers { add { } remove { } }

    public event Action? ShowUi { add { } remove { } }

    public event Action? HideUi { add { } remove { } }

    public event Action? DefaultGlobalScaleChanged { add { } remove { } }

    public event Action? DefaultFontChanged { add { } remove { } }

    public event Action? DefaultStyleChanged { add { } remove { } }

    public IFontAtlas FontAtlas { get; }

    public IFontHandle DefaultFontHandle => defaultHandle;

    public IFontHandle IconFontHandle => iconHandle;

    public IFontHandle MonoFontHandle => defaultHandle;

    public IFontHandle IconFontFixedWidthHandle => iconHandle;

    public IFontSpec DefaultFontSpec => null!;

    public float FontDefaultSizePt => DefaultSizePt;

    public float FontDefaultSizePx => DefaultSizePx;

    public ImFontPtr FontDefault => defaultHandle.Font;

    public ImFontPtr FontIcon => iconHandle.Font;

    public ImFontPtr FontMono => defaultHandle.Font;

    public ImFontPtr FontIconFixedWidth => iconHandle.Font;

    public nint DeviceHandle => 0;

    public nint WindowHandlePtr => 0;

    public bool DisableAutomaticUiHide { get; set; }

    public bool DisableUserUiHide { get; set; }

    public bool DisableCutsceneUiHide { get; set; }

    public bool DisableGposeUiHide { get; set; }

    public bool OverrideGameCursor { get; set; }

    public ulong FrameCount { get; set; }

    public bool CutsceneActive => false;

    public bool ShouldModifyUi => true;

    public bool UiPrepared => true;

    public bool ShouldUseReducedMotion => false;

    public bool PluginUISoundEffectsEnabled => false;

    public UldWrapper LoadUld(string uldPath) => throw new NotSupportedException();

    public Task WaitForUi() => Task.CompletedTask;

    public Task<T> RunWhenUiPrepared<T>(Func<T> func, bool runInFrameworkThread = false) => Task.FromResult(func());

    public Task<T> RunWhenUiPrepared<T>(Func<Task<T>> func, bool runInFrameworkThread = false) => func();

    public IFontAtlas CreateFontAtlas(FontAtlasAutoRebuildMode autoRebuildMode, bool isGlobalScaled = true, string? debugName = null) =>
        throw new NotSupportedException();

    public bool InvokeDraw()
    {
        var handlers = Draw?.GetInvocationList();
        if (handlers is null)
        {
            return true;
        }

        var healthy = true;
        for (var index = 0; index < handlers.Length; index++)
        {
            var handler = (Action)handlers[index];
            try
            {
                if (handler.Target is WindowSystem windowSystem)
                {
                    windowDrawer.Draw(windowSystem.Windows);
                }
                else
                {
                    handler();
                }
            }
            catch (Exception exception)
            {
                healthy = false;
                HarnessLog.Failure($"draw {handler.Method.DeclaringType?.Name}.{handler.Method.Name}", exception);
                ImGuiP.ErrorCheckEndFrameRecover(default);
            }
        }

        return healthy;
    }

    public void InvokeOpenMainUi() => OpenMainUi?.Invoke();

    public void PinWindow(Window window, Vector2 position) => windowDrawer.Pin(window, position);

    public void InvokeOpenConfigUi() => OpenConfigUi?.Invoke();
}
