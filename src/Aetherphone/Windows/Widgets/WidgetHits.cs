using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Widgets;

internal enum WidgetHitKind : byte
{
    Control,
    Link,
}

internal readonly struct WidgetHit
{
    public readonly int Id;
    public readonly Rect Rect;
    public readonly WidgetHitKind Kind;
    public readonly WidgetRoute Route;

    public WidgetHit(int id, Rect rect, WidgetHitKind kind, in WidgetRoute route)
    {
        Id = id;
        Rect = rect;
        Kind = kind;
        Route = route;
    }
}

internal static class WidgetHits
{
    private const int Capacity = 256;
    private const float PressDepth = 1f - Motion.PressScaleControl;

    private static readonly WidgetHit[] Hits = new WidgetHit[Capacity];
    private static int count;
    private static int recordedFrame = -1;
    private static int pressedId;
    private static bool pressing;
    private static int animatedId;
    private static Spring pressSpring;
    private static int firedId;
    private static int firedFrame = -1;

    public static int Key(in WidgetContext context, int controlId) =>
        HashCode.Combine(context.InstanceKey, controlId);

    public static void BeginFrame(float delta)
    {
        count = 0;
        recordedFrame = ImGui.GetFrameCount();
        pressSpring.Step(pressing ? 1f : 0f, pressing ? Motion.PressIn : Motion.Release, delta);
        if (!pressing && pressSpring.Value < 0.005f)
        {
            animatedId = 0;
            pressSpring.SnapTo(0f);
        }
    }

    public static void Register(int id, Rect rect) =>
        Add(new WidgetHit(id, rect, WidgetHitKind.Control, default));

    public static void RegisterLink(int id, Rect rect, in WidgetRoute route) =>
        Add(new WidgetHit(id, rect, WidgetHitKind.Link, route));

    public static bool TryFind(Vector2 point, out WidgetHit hit)
    {
        hit = default;
        var frame = ImGui.GetFrameCount();
        if (recordedFrame != frame && recordedFrame != frame - 1)
        {
            return false;
        }

        for (var index = count - 1; index >= 0; index--)
        {
            if (Hits[index].Rect.Contains(point))
            {
                hit = Hits[index];
                return true;
            }
        }

        return false;
    }

    public static void Press(int id)
    {
        pressedId = id;
        animatedId = id;
        pressing = true;
        pressSpring.SnapTo(0f);
    }

    public static void Release() => pressing = false;

    public static void Cancel()
    {
        pressing = false;
        pressedId = 0;
    }

    public static void Fire(int id)
    {
        firedId = id;
        firedFrame = ImGui.GetFrameCount();
        pressing = false;
        pressedId = 0;
    }

    public static bool ConsumeFired(int id)
    {
        if (firedId != id || firedFrame < ImGui.GetFrameCount() - 1)
        {
            return false;
        }

        firedId = 0;
        firedFrame = -1;
        return true;
    }

    public static bool IsPressed(int id) => pressing && pressedId == id;

    public static float PressScale(int id) => animatedId == id ? 1f - PressDepth * pressSpring.Value : 1f;

    private static void Add(in WidgetHit hit)
    {
        if (count >= Capacity)
        {
            return;
        }

        Hits[count++] = hit;
    }
}
