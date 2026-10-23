using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Glasswork.Backend;
using Glasswork.Glass;

namespace Aetherphone.Core.Shell;

internal static class RealGlass
{
    private const string LogPrefix = "[RealGlass] ";

    private static IDalamudPluginInterface? pluginInterface;
    private static IPluginLog? pluginLog;
    private static Configuration? configuration;
    private static GlassLayer? layer;
    private static int suspendDepth;

    public static bool Ready => layer is { Ready: true };

    public static bool Suspended => suspendDepth > 0;

    public static void Initialize(IDalamudPluginInterface host, IPluginLog log, Configuration settings)
    {
        Dispose();
        pluginInterface = host;
        pluginLog = log;
        configuration = settings;
        layer = new GlassLayer(() => host.UiBuilder.DeviceHandle, Log);
        host.UiBuilder.ResizeBuffers += OnResizeBuffers;
    }

    public static void BeginFrame()
    {
        if (layer is null || pluginInterface is null)
        {
            return;
        }

        layer.ReducedMotion = pluginInterface.UiBuilder.ShouldUseReducedMotion;
        layer.BeginFrame();
    }

    public static bool TryDraw(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, RealGlassKind kind,
        GlassTone tone, Vector4 accent, float opacity)
    {
        if (configuration is not { RealGlass: true } settings || suspendDepth > 0 || layer is not { Ready: true } glass)
        {
            return false;
        }

        if (glass.Quality == GlassQuality.Flat || !OnMainViewport())
        {
            return false;
        }

        var size = max - min;
        if (size.X <= 0f || size.Y <= 0f)
        {
            return false;
        }

        var material = RealGlassMaterials.Build(kind, tone, accent, opacity, settings.GlassAmount, radius, size)
            .Scaled(UiScale.Current);
        glass.Draw(drawList, GlassShape.FromRect(min, size, radius), in material);
        return true;
    }

    public static GlassSuspension Suspend(bool transformed = true) => new(transformed);

    public static void PushSuspension()
    {
        suspendDepth++;
        SyncSuspended();
    }

    public static void PopSuspension()
    {
        if (suspendDepth == 0)
        {
            return;
        }

        suspendDepth--;
        SyncSuspended();
    }

    public static void Dispose()
    {
        if (pluginInterface is not null)
        {
            pluginInterface.UiBuilder.ResizeBuffers -= OnResizeBuffers;
            pluginInterface = null;
        }

        layer?.Dispose();
        layer = null;
        configuration = null;
        pluginLog = null;
        suspendDepth = 0;
    }

    private static void SyncSuspended()
    {
        if (layer is not null)
        {
            layer.Suspended = suspendDepth > 0;
        }
    }

    private static bool OnMainViewport()
    {
        if (ImGuiP.GetCurrentWindowRead().IsNull)
        {
            return true;
        }

        return ImGui.GetWindowViewport().ID == ImGui.GetMainViewport().ID;
    }

    private static void OnResizeBuffers() => layer?.ReleaseResources();

    private static void Log(string message, Exception? exception)
    {
        if (exception is null)
        {
            pluginLog?.Information(string.Concat(LogPrefix, message));
            return;
        }

        pluginLog?.Warning(exception, string.Concat(LogPrefix, message));
    }
}

internal readonly ref struct GlassSuspension
{
    private readonly bool active;

    public GlassSuspension(bool active)
    {
        this.active = active;
        if (active)
        {
            RealGlass.PushSuspension();
        }
    }

    public void Dispose()
    {
        if (active)
        {
            RealGlass.PopSuspension();
        }
    }
}
