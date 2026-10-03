using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed class StackPresenter
{
    private const float RotateCooldownSeconds = 180f;
    private const float RotateCheckSeconds = 10f;
    private const float RelevanceMargin = 0.2f;
    private const float FlipEpsilon = 0.001f;
    private const float MaximumLead = 1.5f;
    private const int StaleFrames = 120;
    private const float DotUnits = 5f;
    private const float DotGapUnits = 4f;
    private const float DotRailInsetUnits = 9f;
    private const float DotRailPadUnits = 4f;
    private const float DotInactiveAlpha = 0.4f;
    private const float DotRailAlpha = 0.26f;
    private const float PeekInsetUnits = 7f;
    private const float PeekDropUnits = 6f;
    private const float PeekAlpha = 0.34f;

    private sealed class StackState
    {
        public HomeTile Tile = null!;
        public int Position;
        public Spring Scroll;
        public Spring Dots;
        public float Wheel;
        public float SinceInteraction;
        public float SinceRotateCheck;
        public int SeenFrame;
        public bool PendingSave;
    }

    private readonly HomeLayoutService layout;
    private readonly WidgetHost widgetHost;
    private readonly List<StackState> states = new();
    private readonly Stack<StackState> pool = new();
    private int prunedFrame;

    public StackPresenter(HomeLayoutService layout, WidgetHost widgetHost)
    {
        this.layout = layout;
        this.widgetHost = widgetHost;
    }

    public void Draw(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, HomeTile tile, float scale, float delta,
        float opacity, bool interactive, bool browsable)
    {
        var state = Acquire(tile);
        var count = tile.Stack.Count;
        Sync(state, count);
        var hovered = browsable && UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            Browse(state, count);
        }

        Rotate(state, count, delta, hovered, !browsable);
        state.Scroll.Step(state.Position, Motion.PageSettle, delta);
        var flipping = MathF.Abs(state.Scroll.Value - state.Position) > FlipEpsilon;
        if (!flipping)
        {
            state.Scroll.SnapTo(state.Position);
            if (state.PendingSave)
            {
                state.PendingSave = false;
                layout.Persist();
            }
        }

        state.Dots.Step(hovered || flipping ? 1f : 0f, Motion.Appear, delta);
        DrawMembers(drawList, rect, theme, tile, state, count, scale, delta, opacity, interactive && !flipping,
            flipping);
        DrawDots(drawList, rect, state, count, scale, opacity);
        Prune();
    }

    public void DrawGhost(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, HomeTile tile, float scale,
        float delta)
    {
        var inset = PeekInsetUnits * scale;
        var drop = PeekDropUnits * scale;
        var radius = WidgetChrome.Radius(scale);
        var fill = ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, PeekAlpha));
        Squircle.Fill(drawList, new Vector2(rect.Min.X + inset, rect.Min.Y + drop),
            new Vector2(rect.Max.X - inset, rect.Max.Y + drop), radius, fill);
        tile.Widget!.Draw(widgetHost.Tile(drawList, rect, theme, tile.Visible, scale, delta, 1f, false));
    }

    private StackState Acquire(HomeTile tile)
    {
        var state = Find(tile);
        if (state is null)
        {
            state = pool.Count > 0 ? pool.Pop() : new StackState();
            state.Tile = tile;
            state.Position = tile.StackIndex;
            state.Scroll.SnapTo(tile.StackIndex);
            state.Dots.SnapTo(0f);
            state.Wheel = 0f;
            state.SinceInteraction = 0f;
            state.SinceRotateCheck = 0f;
            state.PendingSave = false;
            states.Add(state);
        }

        state.SeenFrame = ImGui.GetFrameCount();
        return state;
    }

    private StackState? Find(HomeTile tile)
    {
        for (var index = 0; index < states.Count; index++)
        {
            if (ReferenceEquals(states[index].Tile, tile))
            {
                return states[index];
            }
        }

        return null;
    }

    private static void Sync(StackState state, int count)
    {
        var wanted = state.Tile.StackIndex;
        if (Wrap(state.Position, count) == wanted)
        {
            return;
        }

        state.Position = wanted;
        state.Scroll.SnapTo(wanted);
    }

    private void Browse(StackState state, int count)
    {
        var wheel = ImGui.GetIO().MouseWheel;
        if (wheel == 0f)
        {
            return;
        }

        state.Wheel += wheel;
        if (MathF.Abs(state.Wheel) < 1f)
        {
            return;
        }

        var step = state.Wheel < 0f ? 1 : -1;
        state.Wheel = 0f;
        if (MathF.Abs(state.Position + step - state.Scroll.Value) > MaximumLead)
        {
            return;
        }

        MoveTo(state, state.Position + step, count);
    }

    private void Rotate(StackState state, int count, float delta, bool hovered, bool paused)
    {
        if (hovered)
        {
            state.SinceInteraction = 0f;
        }

        if (hovered || paused || !state.Tile.SmartRotate)
        {
            state.SinceRotateCheck = 0f;
            return;
        }

        state.SinceInteraction += delta;
        state.SinceRotateCheck += delta;
        if (state.SinceRotateCheck < RotateCheckSeconds || state.SinceInteraction < RotateCooldownSeconds)
        {
            return;
        }

        state.SinceRotateCheck = 0f;
        var stack = state.Tile.Stack;
        var current = state.Tile.StackIndex;
        var currentRelevance = Relevance(stack[current]);
        var best = current;
        var bestRelevance = currentRelevance;
        for (var index = 0; index < count; index++)
        {
            var relevance = Relevance(stack[index]);
            if (relevance > bestRelevance)
            {
                best = index;
                bestRelevance = relevance;
            }
        }

        if (best == current || bestRelevance < currentRelevance + RelevanceMargin)
        {
            return;
        }

        MoveTo(state, state.Position + ShortestStep(current, best, count), count);
    }

    private static float Relevance(HomeTile member) => Math.Clamp(member.Widget!.Relevance(member.Config), 0f, 1f);

    private static void MoveTo(StackState state, int position, int count)
    {
        state.Position = position;
        state.Tile.StackIndex = Wrap(position, count);
        state.PendingSave = true;
        state.SinceInteraction = 0f;
    }

    private void DrawMembers(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, HomeTile tile, StackState state,
        int count, float scale, float delta, float opacity, bool interactive, bool flipping)
    {
        if (!flipping)
        {
            DrawMember(drawList, rect, theme, tile.Stack[Wrap(state.Position, count)], scale, delta, opacity,
                interactive);
            return;
        }

        var scroll = state.Scroll.Value;
        var baseIndex = (int)MathF.Floor(scroll);
        var fraction = scroll - baseIndex;
        var height = rect.Height;
        var firstVertex = drawList.VtxBuffer.Size;
        drawList.PushClipRect(rect.Min, rect.Max, true);
        DrawMember(drawList, rect.Translate(new Vector2(0f, -fraction * height)), theme,
            tile.Stack[Wrap(baseIndex, count)], scale, delta, opacity, false);
        DrawMember(drawList, rect.Translate(new Vector2(0f, (1f - fraction) * height)), theme,
            tile.Stack[Wrap(baseIndex + 1, count)], scale, delta, opacity, false);
        drawList.PopClipRect();
        RoundCorners(drawList, firstVertex, rect, WidgetChrome.Radius(scale));
    }

    private void DrawMember(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, HomeTile member, float scale,
        float delta, float opacity, bool interactive) =>
        member.Widget!.Draw(widgetHost.Tile(drawList, rect, theme, member, scale, delta, opacity, interactive));

    private static void RoundCorners(ImDrawListPtr drawList, int firstVertex, Rect rect, float radius)
    {
        var vertices = drawList.VtxBuffer.AsSpan();
        var innerMin = rect.Min + new Vector2(radius, radius);
        var innerMax = rect.Max - new Vector2(radius, radius);
        for (var index = Math.Max(0, firstVertex); index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            var position = vertex.Pos;
            var cornerX = position.X < innerMin.X ? innerMin.X : position.X > innerMax.X ? innerMax.X : position.X;
            var cornerY = position.Y < innerMin.Y ? innerMin.Y : position.Y > innerMax.Y ? innerMax.Y : position.Y;
            if (cornerX == position.X || cornerY == position.Y)
            {
                continue;
            }

            var corner = new Vector2(cornerX, cornerY);
            var offset = position - corner;
            var reach = MathF.Pow(MathF.Abs(offset.X) / radius, Squircle.Exponent) +
                        MathF.Pow(MathF.Abs(offset.Y) / radius, Squircle.Exponent);
            if (reach > 1f)
            {
                vertex.Pos = corner + offset * MathF.Pow(reach, -1f / Squircle.Exponent);
            }
        }
    }

    private static void DrawDots(ImDrawListPtr drawList, Rect rect, StackState state, int count, float scale,
        float opacity)
    {
        var alpha = state.Dots.Value * opacity;
        if (alpha <= 0.01f || count < 2)
        {
            return;
        }

        var dot = DotUnits * scale;
        var gap = DotGapUnits * scale;
        var pad = DotRailPadUnits * scale;
        var columnHeight = count * dot + (count - 1) * gap;
        var centerX = rect.Max.X - DotRailInsetUnits * scale;
        var top = rect.Center.Y - columnHeight * 0.5f;
        var railMin = new Vector2(centerX - dot * 0.5f - pad, top - pad);
        var railMax = new Vector2(centerX + dot * 0.5f + pad, top + columnHeight + pad);
        drawList.AddRectFilled(railMin, railMax,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, DotRailAlpha * alpha)), (railMax.X - railMin.X) * 0.5f);
        var scroll = state.Scroll.Value;
        for (var index = 0; index < count; index++)
        {
            var distance = MathF.Abs(WrappedDistance(scroll, index, count));
            var strength = DotInactiveAlpha + (1f - DotInactiveAlpha) * Math.Clamp(1f - distance, 0f, 1f);
            var center = new Vector2(centerX, top + dot * 0.5f + index * (dot + gap));
            drawList.AddCircleFilled(center, dot * 0.5f,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, strength * alpha)), 12);
        }
    }

    private void Prune()
    {
        var frame = ImGui.GetFrameCount();
        if (frame - prunedFrame < StaleFrames)
        {
            return;
        }

        prunedFrame = frame;
        for (var index = states.Count - 1; index >= 0; index--)
        {
            var state = states[index];
            if (frame - state.SeenFrame < StaleFrames)
            {
                continue;
            }

            if (state.PendingSave)
            {
                layout.Persist();
            }

            states.RemoveAt(index);
            state.Tile = null!;
            pool.Push(state);
        }
    }

    private static int Wrap(int position, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        var wrapped = position % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }

    private static int ShortestStep(int from, int to, int count)
    {
        var forward = Wrap(to - from, count);
        return forward <= count / 2 ? forward : forward - count;
    }

    private static float WrappedDistance(float scroll, int index, int count)
    {
        var difference = (scroll - index) % count;
        if (difference > count * 0.5f)
        {
            difference -= count;
        }
        else if (difference < -count * 0.5f)
        {
            difference += count;
        }

        return difference;
    }
}
