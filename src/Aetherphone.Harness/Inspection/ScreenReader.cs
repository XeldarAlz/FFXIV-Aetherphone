using System.Text;
using Aetherphone.Core;
using Aetherphone.Harness.Rendering;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Harness.Inspection;

internal sealed unsafe class ScreenReader
{
    private const float LineTolerance = 0.75f;
    private const float KerningSpaces = 0.5f;
    private const float WordGapSpaces = 0.4f;
    private const float RunBreakSpaces = 2.5f;
    private const float UvTolerance = 0.000001f;
    private const float ShadowDistance = 3f;
    private const float MinimumVisibleShare = 0.3f;
    private const uint VisibleAlpha = 8;
    private const uint CoverAlpha = 128;
    private readonly List<LineBuilder> builders = new();
    private readonly List<SolidTriangle> solids = new();

    public List<ScreenLine> Read(ImDrawDataPtr drawData, GlyphCatalog catalog)
    {
        builders.Clear();
        solids.Clear();
        var order = 0;
        var data = drawData.Handle;
        var lists = data->CmdLists;
        for (var listIndex = 0; listIndex < data->CmdListsCount; listIndex++)
        {
            var list = lists[listIndex];
            var vertices = (DrawVertex*)list->VtxBuffer.Data;
            var indices = list->IdxBuffer.Data;
            var commands = list->CmdBuffer.Data;
            LineBuilder? current = null;
            for (var commandIndex = 0; commandIndex < list->CmdBuffer.Size; commandIndex++)
            {
                var command = commands[commandIndex];
                if (command.UserCallback != null)
                {
                    continue;
                }

                var clip = new Rect(new Vector2(command.ClipRect.X, command.ClipRect.Y),
                    new Vector2(command.ClipRect.Z, command.ClipRect.W));
                var texture = (nint)command.TextureId.Handle;
                var vertexBase = vertices + command.VtxOffset;
                var indexBase = indices + command.IdxOffset;
                for (var element = 0; element + 2 < command.ElemCount; element += 3)
                {
                    order += 1;
                    var first = indexBase[element];
                    var second = indexBase[element + 1];
                    var third = indexBase[element + 2];
                    var a = vertexBase[first];
                    var b = vertexBase[second];
                    var c = vertexBase[third];
                    if (a.Uv == b.Uv && b.Uv == c.Uv)
                    {
                        if (Alpha(a.Color) >= CoverAlpha && Alpha(b.Color) >= CoverAlpha && Alpha(c.Color) >= CoverAlpha)
                        {
                            solids.Add(new SolidTriangle(order, clip, a.Position, b.Position, c.Position));
                        }

                        continue;
                    }

                    if (second != first + 1 || third != first + 2 || Alpha(a.Color) < VisibleAlpha ||
                        !catalog.TryGet(texture, a.Uv, c.Uv, out var glyph))
                    {
                        continue;
                    }

                    current = Append(current, listIndex, clip, order, glyph, a, c);
                }
            }
        }

        return Build();
    }

    private LineBuilder? Append(LineBuilder? current, int listIndex, Rect clip, int order, in GlyphEntry glyph,
        in DrawVertex topLeft, in DrawVertex bottomRight)
    {
        var quad = new Rect(topLeft.Position, bottomRight.Position);
        var startExact = Same(topLeft.Uv.X, glyph.U0) && Same(topLeft.Uv.Y, glyph.V0);
        var fullWidth = Same(topLeft.Uv.X, glyph.U0) && Same(bottomRight.Uv.X, glyph.U1);
        var fullHeight = Same(topLeft.Uv.Y, glyph.V0) && Same(bottomRight.Uv.Y, glyph.V1);
        var scale = fullHeight ? quad.Height / (glyph.Y1 - glyph.Y0) :
            fullWidth ? quad.Width / (glyph.X1 - glyph.X0) : 0f;
        var visible = quad.Intersect(clip);
        if (!(scale > 0f) || visible.Width <= 0f || visible.Height < quad.Height * MinimumVisibleShare)
        {
            return current;
        }

        var pen = startExact ? quad.Min.X - glyph.X0 * scale : quad.Max.X - glyph.X1 * scale;
        var origin = startExact ? quad.Min.Y - glyph.Y0 * scale : quad.Max.Y - glyph.Y1 * scale;
        var size = glyph.FontSize * scale;
        var space = glyph.SpaceAdvance * scale;
        var clipped = !fullWidth || visible.Width < quad.Width - LineTolerance;
        if (current is null || !current.Continues(listIndex, origin, size, topLeft.Color, pen, space))
        {
            current = new LineBuilder(listIndex, origin, size, topLeft.Color);
            builders.Add(current);
        }
        else if (pen - current.Pen > space * WordGapSpaces)
        {
            current.AddSpace(pen);
        }

        current.Add(glyph.Text, visible, clipped, order);
        current.Pen = pen + glyph.Advance * scale;
        return current;
    }

    private List<ScreenLine> Build()
    {
        var lines = new List<ScreenLine>(builders.Count);
        for (var index = 0; index < builders.Count; index++)
        {
            var builder = builders[index];
            lines.Add(builder.Build(IsCovered(builder.Bounds.Center, builder.LastOrder)));
        }

        RemoveShadows(lines);
        return lines;
    }

    private bool IsCovered(Vector2 point, int after)
    {
        for (var index = 0; index < solids.Count; index++)
        {
            var solid = solids[index];
            if (solid.Order > after && solid.Clip.Contains(point) && solid.Contains(point))
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveShadows(List<ScreenLine> lines)
    {
        for (var index = lines.Count - 1; index >= 0; index--)
        {
            var line = lines[index];
            for (var later = index + 1; later < lines.Count; later++)
            {
                var other = lines[later];
                if (other.Text == line.Text &&
                    MathF.Abs(other.Bounds.Min.X - line.Bounds.Min.X) <= ShadowDistance &&
                    MathF.Abs(other.Bounds.Min.Y - line.Bounds.Min.Y) <= ShadowDistance)
                {
                    lines.RemoveAt(index);
                    break;
                }
            }
        }
    }

    private static uint Alpha(uint color) => color >> 24;

    private static bool Same(float left, float right) => MathF.Abs(left - right) <= UvTolerance;

    private readonly record struct SolidTriangle(int Order, Rect Clip, Vector2 A, Vector2 B, Vector2 C)
    {
        public bool Contains(Vector2 point)
        {
            var first = Cross(A, B, point);
            var second = Cross(B, C, point);
            var third = Cross(C, A, point);
            var hasNegative = first < 0f || second < 0f || third < 0f;
            var hasPositive = first > 0f || second > 0f || third > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Cross(Vector2 from, Vector2 to, Vector2 point) =>
            (to.X - from.X) * (point.Y - from.Y) - (to.Y - from.Y) * (point.X - from.X);
    }

    private sealed class LineBuilder
    {
        private readonly StringBuilder text = new();
        private readonly List<float> lefts = new();
        private readonly List<float> rights = new();
        private readonly int listIndex;
        private readonly float origin;
        private readonly float size;
        private readonly uint color;
        private bool clipped;

        public LineBuilder(int listIndex, float origin, float size, uint color)
        {
            this.listIndex = listIndex;
            this.origin = origin;
            this.size = size;
            this.color = color;
        }

        public float Pen { get; set; }

        public Rect Bounds { get; private set; }

        public int LastOrder { get; private set; }

        public bool Continues(int otherList, float otherOrigin, float otherSize, uint otherColor, float pen, float space) =>
            otherList == listIndex && otherColor == color &&
            MathF.Abs(otherOrigin - origin) <= LineTolerance && MathF.Abs(otherSize - size) <= LineTolerance &&
            pen >= Pen - space * KerningSpaces && pen - Pen <= space * RunBreakSpaces;

        public void AddSpace(float nextPen)
        {
            text.Append(' ');
            lefts.Add(Pen);
            rights.Add(nextPen);
        }

        public void Add(string glyph, Rect visible, bool glyphClipped, int order)
        {
            Bounds = text.Length == 0 ? visible : new Rect(Vector2.Min(Bounds.Min, visible.Min), Vector2.Max(Bounds.Max, visible.Max));
            text.Append(glyph);
            for (var index = 0; index < glyph.Length; index++)
            {
                lefts.Add(visible.Min.X);
                rights.Add(visible.Max.X);
            }

            clipped |= glyphClipped;
            LastOrder = order;
        }

        public ScreenLine Build(bool covered) =>
            new(text.ToString(), Bounds, size, color, clipped, covered, lefts.ToArray(), rights.ToArray());
    }
}
