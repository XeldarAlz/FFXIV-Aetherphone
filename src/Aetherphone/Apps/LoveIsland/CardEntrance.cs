using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal sealed class CardEntrance
{
    private const float StaggerSeconds = 0.055f;
    private const float SmoothTime = 0.12f;
    private const float RiseUnits = 22f;
    private const int MaxStaggerSlots = 9;
    private const float SettledShare = 0.999f;
    private const uint ColorMask = 0x00FFFFFFu;
    private const int AlphaShift = 24;

    private double startedAt = double.MinValue;
    private int slot;

    public void Restart()
    {
        startedAt = ImGui.GetTime();
    }

    public void BeginFrame()
    {
        slot = 0;
    }

    public static int Mark(ImDrawListPtr drawList) => drawList.VtxBuffer.Size;

    private float Progress(int index)
    {
        var elapsed = (float)(ImGui.GetTime() - startedAt) - Math.Min(index, MaxStaggerSlots) * StaggerSeconds;
        return Spring.Settle(elapsed, SmoothTime);
    }

    public void Settle(ImDrawListPtr drawList, int firstVertex, float scale)
    {
        var progress = Progress(slot);
        slot++;
        if (progress >= SettledShare)
        {
            return;
        }

        Shift(drawList, firstVertex, (1f - progress) * RiseUnits * scale, progress);
    }

    public static void Fade(ImDrawListPtr drawList, int firstVertex, float alpha)
    {
        if (alpha >= 0.999f)
        {
            return;
        }

        Shift(drawList, firstVertex, 0f, alpha);
    }

    private static void Shift(ImDrawListPtr drawList, int firstVertex, float rise, float alpha)
    {
        var clamped = Math.Clamp(alpha, 0f, 1f);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = Math.Max(0, firstVertex); index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            vertex.Pos = new Vector2(vertex.Pos.X, vertex.Pos.Y + rise);
            var faded = (uint)((vertex.Col >> AlphaShift) * clamped + 0.5f);
            vertex.Col = (vertex.Col & ColorMask) | (faded << AlphaShift);
        }
    }
}
