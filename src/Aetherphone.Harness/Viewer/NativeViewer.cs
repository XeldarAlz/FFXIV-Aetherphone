using Aetherphone.Harness.Driver;
using Aetherphone.Harness.Fakes;
using Aetherphone.Harness.Host;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Aetherphone.Harness.Viewer;

internal sealed class NativeViewer : IDisposable
{
    private const double MinimumDelta = 1.0 / 240.0;
    private const double MaximumDelta = 1.0 / 10.0;
    private readonly PhoneHost host;
    private readonly DriverServer driver;
    private readonly IWindow window;
    private GL? gl;
    private OpenGlRenderer? renderer;
    private IInputContext? input;

    public NativeViewer(PhoneHost host, DriverServer driver, int width, int height)
    {
        this.host = host;
        this.driver = driver;
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(width, height),
            Title = "Aetherphone",
            VSync = true,
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
        };
        window = Window.Create(options);
        window.Load += OnLoad;
        window.Render += OnRender;
        window.Resize += OnResize;
        window.Closing += OnClosing;
    }

    public void Run() => window.Run();

    public void Dispose() => window.Dispose();

    private void OnClosing()
    {
        input?.Dispose();
        input = null;
        renderer?.Dispose();
        renderer = null;
        HarnessLog.Note("native window closed");
    }

    private void OnLoad()
    {
        gl = window.CreateOpenGL();
        renderer = new OpenGlRenderer(gl, host.Textures);
        host.SetDisplaySize(window.Size.X, window.Size.Y);
        input = window.CreateInput();
        for (var index = 0; index < input.Mice.Count; index++)
        {
            var mouse = input.Mice[index];
            mouse.MouseMove += OnMouseMove;
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.Scroll += OnScroll;
        }

        for (var index = 0; index < input.Keyboards.Count; index++)
        {
            var keyboard = input.Keyboards[index];
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
            keyboard.KeyChar += OnKeyChar;
        }

        HarnessLog.Note($"native window open: {window.Size.X}x{window.Size.Y} points, framebuffer {window.FramebufferSize.X}x{window.FramebufferSize.Y}");
    }

    private void OnResize(Vector2D<int> size) => host.SetDisplaySize(size.X, size.Y);

    private void OnRender(double delta)
    {
        if (renderer is null)
        {
            return;
        }

        if (driver.Pump())
        {
            window.Close();
            return;
        }

        host.Step((float)Math.Clamp(delta, MinimumDelta, MaximumDelta));
        var framebuffer = window.FramebufferSize;
        gl!.ClearColor(24f / 255f, 26f / 255f, 32f / 255f, 1f);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        renderer.Render(host.DrawData, framebuffer.X, framebuffer.Y);
    }

    private void OnMouseMove(IMouse mouse, Vector2 position) => host.MouseMove(position);

    private void OnMouseDown(IMouse mouse, MouseButton button) => host.MouseButton(MapButton(button), true);

    private void OnMouseUp(IMouse mouse, MouseButton button) => host.MouseButton(MapButton(button), false);

    private void OnScroll(IMouse mouse, ScrollWheel wheel) => host.MouseWheel(wheel.X, wheel.Y);

    private void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        if (SilkKeys.TryMap(key, out var imGuiKey, out var modifier))
        {
            host.KeyEvent(imGuiKey, modifier, true);
        }
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int scancode)
    {
        if (SilkKeys.TryMap(key, out var imGuiKey, out var modifier))
        {
            host.KeyEvent(imGuiKey, modifier, false);
        }
    }

    private void OnKeyChar(IKeyboard keyboard, char character) => host.TextInput(character.ToString());

    private static int MapButton(MouseButton button) => button switch
    {
        MouseButton.Right => 1,
        MouseButton.Middle => 2,
        _ => 0,
    };
}
