using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal static class PrismMotion
{
    public const float StageShiftUnits = 28f;
    public const float StageZoom = 0.03f;
    public const float RiseUnits = 18f;
    public const float Stagger = 0.06f;
    public const float Settled = 0.995f;
    public const float GrowEpsilon = 0.001f;

    public static float At(float age, float delay, float smoothTime) => Spring.Settle(age - delay, smoothTime);

    public static int Mark(ImDrawListPtr drawList) => drawList.VtxBuffer.Size;

    public static void Rise(ImDrawListPtr drawList, int from, float progress, float distance)
    {
        if (progress >= Settled)
        {
            return;
        }

        Shift(drawList, from, new Vector2(0f, distance * (1f - progress)), progress);
    }

    public static void Shift(ImDrawListPtr drawList, int from, Vector2 offset, float alpha)
    {
        var transform = new LayerTransform(1f, Vector2.Zero, offset, default, alpha);
        var fade = alpha < 1f;
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = Math.Max(0, from); index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            vertex.Pos = transform.Map(vertex.Pos);
            if (fade)
            {
                vertex.Col = transform.MapColor(vertex.Col);
            }
        }
    }

    public static void Scale(ImDrawListPtr drawList, int from, Vector2 pivot, float scale, float alpha)
    {
        if (alpha >= Settled)
        {
            return;
        }

        var transform = LayerTransform.ScaleAbout(pivot, scale, default, alpha);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = Math.Max(0, from); index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            vertex.Pos = transform.Map(vertex.Pos);
            vertex.Col = transform.MapColor(vertex.Col);
        }
    }

    public static void Grow(ImDrawListPtr drawList, int from, Vector2 pivot, float scale)
    {
        if (MathF.Abs(scale - 1f) < GrowEpsilon)
        {
            return;
        }

        var transform = LayerTransform.ScaleAbout(pivot, scale, default, 1f);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = Math.Max(0, from); index < vertices.Length; index++)
        {
            vertices[index].Pos = transform.Map(vertices[index].Pos);
        }
    }

    public static LayerTransform Stage(Rect full, float progress, int direction, float scale)
    {
        var alpha = MathF.Min(1f, progress);
        if (direction == 0)
        {
            return LayerTransform.ScaleAbout(full.Center, 1f + StageZoom * (1f - progress), full, alpha);
        }

        var offset = new Vector2(direction * StageShiftUnits * scale * (1f - progress), 0f);
        return new LayerTransform(1f, Vector2.Zero, offset, full, alpha);
    }
}
