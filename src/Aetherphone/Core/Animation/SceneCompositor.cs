using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Core.Animation;

internal delegate void LayerPainter(Rect target);

internal static class SceneCompositor
{
    private const ImGuiWindowFlags LayerFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                ImGuiWindowFlags.NoBackground;

    internal readonly struct Layer
    {
        public readonly string Id;
        public readonly Vector2 Offset;
        public readonly float Dim;
        public readonly Vector4 Background;
        public readonly bool Shield;
        public readonly LayerPainter Paint;

        public Layer(string id, Vector2 offset, float dim, LayerPainter paint, Vector4 background = default,
            bool shield = false)
        {
            Id = id;
            Offset = offset;
            Dim = dim;
            Paint = paint;
            Background = background;
            Shield = shield;
        }
    }

    public static void Composite(Rect clip, in Layer under, in Layer over)
    {
        DrawLayer(clip, under);
        DrawLayer(clip, over);
    }

    public static void DrawClipped(Rect clip, Rect paintTarget, float dim, LayerPainter paint)
    {
        if (clip.Height <= 0.5f)
        {
            return;
        }

        ImGui.SetCursorScreenPos(clip.Min);

        using (ImRaii.Child("clip", clip.Size, false, LayerFlags | ImGuiWindowFlags.NoInputs))
        using (InputShield.Engage(true))
        {
            paint(paintTarget);

            if (dim > 0f)
            {
                ImGui.GetWindowDrawList().AddRectFilled(paintTarget.Min, paintTarget.Max,
                    ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)));
            }
        }
    }

    public static void DrawLayer(Rect clip, in Layer layer)
    {
        var offset = new Vector2(MathF.Round(layer.Offset.X), MathF.Round(layer.Offset.Y));
        var shifted = new Rect(clip.Min + offset, clip.Max + offset);
        ImGui.SetCursorScreenPos(clip.Min);

        using (ImRaii.PushId(layer.Id))
        using (ImRaii.Child("layer", clip.Size, false, LayerFlags))
        {
            using (InputShield.Engage(layer.Shield))
            {
                if (layer.Background.W > 0f)
                {
                    ImGui.GetWindowDrawList()
                        .AddRectFilled(shifted.Min, shifted.Max, ImGui.GetColorU32(layer.Background));
                }

                layer.Paint(shifted);
            }

            if (!(layer.Dim > 0f))
            {
                return;
            }
            
            ImGui.SetCursorScreenPos(clip.Min);
            using (ImRaii.Child("dim", clip.Size, false, LayerFlags | ImGuiWindowFlags.NoInputs))
            {
                ImGui.GetWindowDrawList().AddRectFilled(shifted.Min, shifted.Max,
                    ImGui.GetColorU32(new Vector4(0f, 0f, 0f, layer.Dim)));
            }
        }
    }
}
