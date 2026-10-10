using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Strip;

internal enum StripPage : byte
{
    Jackpot,
    Machine,
    Race,
    Table,
    Dealer,
    Challenge,
}

internal sealed class StripCarousel
{
    public const int MaxPages = 6;
    public const float AdvanceSeconds = 6f;
    public const float CardGap = 12f;
    public const float DotsGap = 10f;
    public const float DotsRow = 12f;
    public const float FlingFraction = 0.18f;

    private const float CardAspect = 0.62f;
    private const float MinCardHeight = 196f;
    private const float MaxCardHeight = 260f;
    private const float DragSlop = 6f;
    private const string ClaimId = "##casino.hero.claim";

    private readonly StripPage[] pages = new StripPage[MaxPages];
    private int count;
    private int page;
    private float timer;
    private Spring position;
    private bool pressed;
    private bool dragging;
    private bool hovered;
    private Vector2 pressPoint;
    private float pressPosition;

    public int Count => count;

    public int Page => page;

    public bool Dragging => dragging;

    public float Position => position.Value;

    public StripPage PageAt(int index) => pages[index];

    public static float CardHeight(float width, float scale) =>
        Math.Clamp(width * CardAspect, MinCardHeight * scale, MaxCardHeight * scale);

    public static float BlockHeight(float width, float scale) =>
        CardHeight(width, scale) + (DotsGap + DotsRow) * scale;

    public static float Stride(float width, float scale) => width + CardGap * scale;

    public static int Next(int page, int count) => count <= 1 ? 0 : (page + 1) % count;

    public static int SnapTarget(float pressPosition, float position, int count)
    {
        if (count <= 1)
        {
            return 0;
        }

        var moved = position - pressPosition;
        var target = MathF.Round(pressPosition);
        if (moved > FlingFraction)
        {
            target = MathF.Floor(position - FlingFraction) + 1f;
        }
        else if (moved < -FlingFraction)
        {
            target = MathF.Ceiling(position + FlingFraction) - 1f;
        }

        return Math.Clamp((int)target, 0, count - 1);
    }

    public void Clear()
    {
        count = 0;
    }

    public void Add(StripPage next)
    {
        if (count < MaxPages)
        {
            pages[count++] = next;
        }
    }

    public void Settle()
    {
        if (count == 0)
        {
            page = 0;
            return;
        }

        page = Math.Min(page, count - 1);
    }

    public void Reset()
    {
        position.SnapTo(0f);
        page = 0;
        timer = 0f;
        pressed = false;
        dragging = false;
    }

    public float CardLeft(int index, float originX, float stride) => originX + (index - position.Value) * stride;

    public bool TapAllowed => !dragging;

    public void Update(Rect rail, float stride, bool paused, float deltaSeconds)
    {
        var activated = Claim(rail);
        var mouse = ImGui.GetMousePos();
        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (!pressed && activated && down && !UiInteract.InputBlocked)
        {
            pressed = true;
            dragging = false;
            pressPoint = mouse;
            pressPosition = position.Value;
        }
        else if (pressed && down)
        {
            Drag(mouse, stride);
        }
        else if (pressed)
        {
            Release();
        }

        if (!pressed)
        {
            Advance(paused, deltaSeconds);
        }
    }

    public void DrawDots(ImDrawListPtr drawList, Vector2 center, float maxWidth, Vector4 ink) =>
        PhotoCarousel.DrawDots(drawList, center, count, page, maxWidth, ink);

    private bool Claim(Rect rail)
    {
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(rail.Min);
        ImGui.InvisibleButton(ClaimId, rail.Size);
        hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
                  && UiInteract.Hover(rail.Min, rail.Max);
        var activated = hovered && ImGui.IsItemActivated();
        if (hovered)
        {
            UiInteract.ReportGestureSurface();
        }

        ImGui.SetCursorScreenPos(cursor);
        return activated;
    }

    private void Drag(Vector2 mouse, float stride)
    {
        var travel = mouse - pressPoint;
        if (!dragging)
        {
            var slop = DragSlop * UiScale.Current;
            if (MathF.Abs(travel.X) > slop && MathF.Abs(travel.X) >= MathF.Abs(travel.Y))
            {
                dragging = true;
                UiInteract.CancelPendingTap();
            }
            else if (MathF.Abs(travel.Y) > slop)
            {
                pressed = false;
                return;
            }
            else
            {
                return;
            }
        }

        position.SnapTo(Math.Clamp(pressPosition - travel.X / MathF.Max(stride, 1f), 0f, MathF.Max(0f, count - 1)));
        UiInteract.BlockThisFrame();
    }

    private void Release()
    {
        pressed = false;
        timer = 0f;
        if (!dragging)
        {
            return;
        }

        dragging = false;
        page = SnapTarget(pressPosition, position.Value, count);
        position.Launch(position.Value, TransitionTiming.LaunchVelocity(Motion.Sheet) * (page - position.Value));
        UiInteract.BlockThisFrame();
    }

    private void Advance(bool paused, float deltaSeconds)
    {
        if (paused || hovered || count <= 1)
        {
            timer = 0f;
        }
        else
        {
            timer += deltaSeconds;
            if (timer >= AdvanceSeconds)
            {
                timer = 0f;
                page = Next(page, count);
            }
        }

        position.Step(page, Motion.Sheet, deltaSeconds);
    }
}
