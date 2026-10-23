using Aetherphone.Core.Shell;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Animation;

internal delegate void LayerPainter(Rect target);

internal static class SceneCompositor
{
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

    public static void DrawLayer(Rect clip, in Layer layer)
    {
        var offset = new Vector2(MathF.Round(layer.Offset.X), MathF.Round(layer.Offset.Y));
        using var glassSuspension = RealGlass.Suspend(offset != Vector2.Zero);
        using var stage = ScreenLayer.Begin(layer.Id, clip, layer.Shield);
        if (layer.Background.W > 0f)
        {
            ImGui.GetWindowDrawList().AddRectFilled(clip.Min, clip.Max, ImGui.GetColorU32(layer.Background));
        }

        layer.Paint(clip);
        if (layer.Dim > 0f)
        {
            stage.Veil(ImGui.GetColorU32(new Vector4(0f, 0f, 0f, layer.Dim)));
        }

        stage.Transform(LayerTransform.Translate(offset, clip));
    }
}
