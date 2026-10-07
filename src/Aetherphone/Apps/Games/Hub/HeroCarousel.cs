using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Hub;

internal enum HeroPage : byte
{
    Daily,
    Spotlight,
    Together,
}

internal static class PageSnap
{
    public const float FlingFraction = 0.18f;

    public static int Target(float pressPosition, float position, int count)
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
}

internal sealed class HeroCarousel
{
    public const int MaxPages = 3;
    public const float AdvanceSeconds = 7f;
    public const float CardGap = 12f;
    public const float DotsGap = 10f;
    public const float DotsRow = 14f;
    private const float CardAspect = 0.78f;
    private const float MinCardHeight = 236f;
    private const float MaxCardHeight = 300f;
    private const float DragSlop = 6f;
    private const string ClaimId = "##games.hero.claim";

    private readonly HeroPage[] pages = new HeroPage[MaxPages];
    private int count = 1;
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

    public HeroPage PageAt(int index) => pages[index];

    public static float CardHeight(float width, float scale) =>
        Math.Clamp(width * CardAspect, MinCardHeight * scale, MaxCardHeight * scale);

    public static float BlockHeight(float width, float scale) =>
        CardHeight(width, scale) + (DotsGap + DotsRow) * scale;

    public static float Stride(float width, float scale) => width + CardGap * scale;

    public static int Next(int page, int count) => count <= 1 ? 0 : (page + 1) % count;

    public static int SpotlightSlot(int today, int candidates) =>
        candidates <= 0 ? -1 : (today % candidates + candidates) % candidates;

    public void SetPages(bool spotlight, bool together)
    {
        count = 0;
        pages[count++] = HeroPage.Daily;
        if (spotlight)
        {
            pages[count++] = HeroPage.Spotlight;
        }

        if (together)
        {
            pages[count++] = HeroPage.Together;
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

    public void Update(Rect rail, float stride, bool paused, bool pinned, float deltaSeconds)
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

        if (pinned)
        {
            page = 0;
        }

        if (!pressed)
        {
            Settle(paused || pinned, deltaSeconds);
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

        position.SnapTo(Math.Clamp(pressPosition - travel.X / MathF.Max(stride, 1f), 0f, count - 1));
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
        page = PageSnap.Target(pressPosition, position.Value, count);
        position.Launch(position.Value, TransitionTiming.LaunchVelocity(Motion.Sheet) * (page - position.Value));
        UiInteract.BlockThisFrame();
    }

    private void Settle(bool paused, float deltaSeconds)
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
