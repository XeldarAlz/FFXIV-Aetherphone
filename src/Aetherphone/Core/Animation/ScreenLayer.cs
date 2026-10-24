using Aetherphone.Core.Shell;
using Dalamud.Bindings.ImGui;
using Glasswork.Backend;

namespace Aetherphone.Core.Animation;

internal ref struct ScreenLayer
{
    private const ImGuiWindowFlags StageFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                ImGuiWindowFlags.NoBackground;

    private const ImGuiWindowFlags PassiveFlags = StageFlags | ImGuiWindowFlags.NoInputs;
    private const int AlphaShift = 24;

    private readonly Rect rect;
    private ImGuiWindowPtr window;
    private readonly InputShield shield;
    private readonly GlassSuspendScope glassSuspension;
    private bool open;

    private ScreenLayer(Rect rect, ImGuiWindowPtr window, InputShield shield, GlassSuspendScope glassSuspension)
    {
        this.rect = rect;
        this.window = window;
        this.shield = shield;
        this.glassSuspension = glassSuspension;
        open = true;
    }

    public static ScreenLayer Begin(string id, Rect rect, bool shield) =>
        Open(id, rect, StageFlags, InputShield.Engage(shield), false);

    public static ScreenLayer BeginWarped(string id, Rect rect, in LayerTransform warp) =>
        Open(id, rect, StageFlags, InputShield.Warp(in warp), !warp.IsIdentity);

    public static ScreenLayer BeginPassive(string id, Rect rect) =>
        Open(id, rect, PassiveFlags, InputShield.Engage(false), false);

    private static ScreenLayer Open(string id, Rect rect, ImGuiWindowFlags flags, InputShield shield,
        bool suspendGlass)
    {
        var glassSuspension = RealGlass.Suspend(suspendGlass);
        ImGui.SetCursorScreenPos(rect.Min);
        ImGui.PushID(id);
        ImGui.BeginChild("stage", rect.Size, false, flags);
        return new ScreenLayer(rect, ImGuiP.GetCurrentWindowRead(), shield, glassSuspension);
    }

    public readonly void Veil(uint color)
    {
        if (color >> AlphaShift == 0)
        {
            return;
        }

        using var veil = BeginPassive("veil", rect);
        ImGui.GetWindowDrawList().AddRectFilled(rect.Min, rect.Max, color);
    }

    public void End()
    {
        if (!open)
        {
            return;
        }

        open = false;
        shield.Dispose();
        ImGui.EndChild();
        ImGui.PopID();
        glassSuspension.Dispose();
    }

    public void Dispose() => End();

    public void Transform(in LayerTransform transform)
    {
        End();
        if (transform.IsIdentity && transform.Clip == rect)
        {
            return;
        }

        LayerCompositor.Transform(window, in transform);
    }
}

internal static class LayerCompositor
{
    public static void Transform(ImGuiWindowPtr window, in LayerTransform transform)
    {
        if (window.IsNull)
        {
            return;
        }

        TransformDrawList(window.DrawList, in transform);
        TransformChildren(window, in transform);
    }

    public static void TransformChildren(ImGuiWindowPtr window, in LayerTransform transform)
    {
        if (window.IsNull)
        {
            return;
        }

        var children = window.DC.ChildWindows.AsSpan();
        for (var index = 0; index < children.Length; index++)
        {
            Transform(children[index], in transform);
        }
    }

    public static void Fade(ImDrawListPtr drawList, int fromVertex, float alpha)
    {
        if (drawList.IsNull || alpha >= 1f)
        {
            return;
        }

        var fade = LayerTransform.Fade(alpha);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = Math.Max(0, fromVertex); index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            vertex.Col = fade.MapColor(vertex.Col);
        }
    }

    private static void TransformDrawList(ImDrawListPtr drawList, in LayerTransform transform)
    {
        if (drawList.IsNull)
        {
            return;
        }

        var vertices = drawList.VtxBuffer.AsSpan();
        var fade = transform.Alpha < 1f;
        for (var index = 0; index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            vertex.Pos = transform.Map(vertex.Pos);
            if (fade)
            {
                vertex.Col = transform.MapColor(vertex.Col);
            }
        }

        var commands = drawList.CmdBuffer.AsSpan();
        for (var index = 0; index < commands.Length; index++)
        {
            ref var command = ref commands[index];
            command.ClipRect = transform.MapClip(command.ClipRect);
        }
    }
}
