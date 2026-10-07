using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal sealed class Ribbon
{
    public const int Capacity = 24;
    public const float TunedFrameSeconds = 1f / 60f;
    private const float HaloWidthScale = 2.4f;
    private const float HaloAlpha = 0.22f;

    private readonly Vector2[] points = new Vector2[Capacity];
    private int head;
    private int count;

    public int Count => count;

    public int Owner { get; private set; } = -1;

    public void Claim(int owner)
    {
        if (owner == Owner)
        {
            return;
        }

        Owner = owner;
        Clear();
    }

    public static float Spacing(float speed) => MathF.Abs(speed) * TunedFrameSeconds;

    public void Push(Vector2 point)
    {
        points[head] = point;
        head = (head + 1) % Capacity;
        count = Math.Min(Capacity, count + 1);
    }

    public void PushSpaced(Vector2 point, float minimumDistance)
    {
        if (count < 2 || minimumDistance <= 0f)
        {
            Push(point);
            return;
        }

        var newest = (head - 1 + Capacity) % Capacity;
        var anchor = Point(1);
        var reach = point - anchor;
        var distanceSquared = reach.LengthSquared();
        if (distanceSquared < minimumDistance * minimumDistance)
        {
            points[newest] = point;
            return;
        }

        points[newest] = anchor + reach * (minimumDistance / MathF.Sqrt(distanceSquared));
        Push(point);
    }

    public void Clear()
    {
        head = 0;
        count = 0;
    }

    public void Release()
    {
        Owner = -1;
        Clear();
    }

    public Vector2 Point(int age) => points[(head - 1 - age + Capacity * 2) % Capacity];

    public void Draw(ImDrawListPtr drawList, Vector4 color, float width, bool additive = false)
    {
        if (count < 2)
        {
            return;
        }

        if (additive)
        {
            DrawPass(drawList, color with { W = color.W * HaloAlpha }, width * HaloWidthScale, false);
        }

        DrawPass(drawList, color, width, false);
    }

    public void Draw(ImDrawListPtr drawList, in Camera2D camera, Vector4 color, float width, bool additive = false)
    {
        if (count < 2)
        {
            return;
        }

        if (additive)
        {
            DrawPass(drawList, color with { W = color.W * HaloAlpha }, width * HaloWidthScale, true, camera);
        }

        DrawPass(drawList, color, width, true, camera);
    }

    private void DrawPass(ImDrawListPtr drawList, Vector4 color, float width, bool world, in Camera2D camera = default)
    {
        var previous = world ? camera.ToScreen(Point(0)) : Point(0);
        var lastAge = count - 1;
        for (var age = 1; age <= lastAge; age++)
        {
            var current = world ? camera.ToScreen(Point(age)) : Point(age);
            var taper = 1f - age / (float)count;
            var segmentWidth = MathF.Max(1f, width * taper);
            var segmentColor = ImGui.GetColorU32(color with { W = color.W * taper });
            drawList.AddLine(previous, current, segmentColor, segmentWidth);
            previous = current;
        }
    }
}
