using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class VertexWarp
{
    public static void Tilt(ImDrawListPtr drawList, int firstVertex, Vector2 center, float half, Vector2 direction,
        float amount)
    {
        if (amount <= 0f || half <= 0f || direction.LengthSquared() < 0.0001f)
        {
            return;
        }

        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = Math.Max(0, firstVertex); index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            var offset = vertex.Pos - center;
            var depth = (offset.X * direction.X + offset.Y * direction.Y) / half;
            vertex.Pos = center + offset * (1f + amount * depth);
        }
    }
}
