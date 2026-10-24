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

    public static bool Ready => layer is { Ready: true };

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

    public static GlassGroup Group() => new(layer);

    public static bool TryDraw(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, RealGlassKind kind,
        GlassTone tone, Vector4 accent, float opacity) =>
        TryDrawShape(drawList, RealGlassMaterials.Shape(min, max, radius), kind, tone, accent, opacity);

    public static bool TryDrawRing(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float thickness,
        RealGlassKind kind, GlassTone tone, Vector4 accent, float opacity) =>
        TryDrawShape(drawList, RealGlassMaterials.Ring(min, max, radius, thickness), kind, tone, accent, opacity);

    public static GlassSuspendScope Suspend(bool transformed = true) =>
        transformed && layer is { } glass ? glass.Suspend() : default;

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
    }

    private static bool TryDrawShape(ImDrawListPtr drawList, in GlassShape shape, RealGlassKind kind, GlassTone tone,
        Vector4 accent, float opacity)
    {
        if (configuration is not { RealGlass: true } settings || layer is not { } glass || !glass.CanRecord())
        {
            return false;
        }

        if (shape.HalfSize.X <= 0f || shape.HalfSize.Y <= 0f)
        {
            return false;
        }

        var material = RealGlassMaterials.Build(kind, tone, accent, opacity, settings.GlassAmount, shape.Radius,
            shape.HalfSize * 2f).Scaled(UiScale.Current);
        return glass.TryDraw(drawList, in shape, in material);
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

internal readonly ref struct GlassGroup
{
    private readonly GlassLayer? layer;

    public GlassGroup(GlassLayer? layer)
    {
        this.layer = layer;
        layer?.BeginGroup();
    }

    public void Dispose() => layer?.EndGroup();
}
