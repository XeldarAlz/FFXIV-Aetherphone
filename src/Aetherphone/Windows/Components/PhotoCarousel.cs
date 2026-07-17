using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal readonly record struct CarouselResult(int Index, bool InputConsumed, bool Tapped);

internal sealed class PhotoCarousel
{
    private const float SwipeThreshold = 12f;
    private const float TapSlop = 6f;
    private const float ChevronRadius = 14f;
    private const float ChevronInset = 12f;
    private const float DotSpacing = 12f;
    private const float DotRadius = 3f;

    private static readonly Vector4 BadgeSurface = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 BadgeInk = new(1f, 1f, 1f, 0.95f);
    private static readonly Vector4 ChevronSurface = new(1f, 1f, 1f, 0.88f);
    private static readonly Vector4 ChevronInk = new(0.15f, 0.15f, 0.17f, 0.95f);

    private readonly Dictionary<string, Pager> pagers = new(StringComparer.Ordinal);

    private string pressPostId = string.Empty;
    private string dragPostId = string.Empty;
    private bool pressActive;
    private Vector2 pressPos;

    public void Clear()
    {
        pagers.Clear();
        pressActive = false;
        pressPostId = string.Empty;
        dragPostId = string.Empty;
    }

    public int IndexOf(string postId, int count)
    {
        if (count <= 1 || !pagers.TryGetValue(postId, out var pager))
        {
            return 0;
        }

        return Math.Clamp(pager.Page, 0, count - 1);
    }

    public CarouselResult Draw(
        ImDrawListPtr drawList,
        Rect rect,
        string postId,
        string[] photos,
        float rounding,
        Action<ImDrawListPtr, Vector2, Vector2, float, string?> drawPage)
    {
        var count = photos.Length;
        if (count <= 1)
        {
            drawPage(drawList, rect.Min, rect.Max, rounding, count == 1 ? photos[0] : null);
            return new CarouselResult(0, false, ResolveTap(rect, postId, false, false));
        }

        var pager = PagerFor(postId);
        var scale = ImGuiHelpers.GlobalScale;
        var delta = ImGui.GetIO().DeltaTime;
        pager.Step(delta, count);

        var consumed = DriveGesture(pager, rect, postId, count, delta, scale);

        var scroll = pager.Value;
        var first = Math.Max(0, (int)MathF.Floor(scroll));
        var last = Math.Min(count - 1, (int)MathF.Ceiling(scroll));
        drawList.PushClipRect(rect.Min, rect.Max, true);
        for (var index = first; index <= last; index++)
        {
            var offset = new Vector2((index - scroll) * rect.Width, 0f);
            drawPage(drawList, rect.Min + offset, rect.Max + offset, rounding, photos[index]);
        }

        drawList.PopClipRect();

        var active = Math.Clamp((int)MathF.Round(scroll), 0, count - 1);
        DrawCounter(drawList, rect, active, count, scale);
        var chevronHot = DrawChevrons(drawList, rect, pager, active, count, scale, out var chevronClicked);
        consumed |= chevronClicked;
        var tapped = ResolveTap(rect, postId, consumed, chevronHot);
        return new CarouselResult(active, consumed, tapped);
    }

    public static void DrawDots(ImDrawListPtr drawList, Vector2 center, int count, int active, float maxWidth,
        Vector4 ink)
    {
        if (count <= 1)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var spacing = DotSpacing * scale;
        var span = spacing * (count - 1);
        if (maxWidth > 0f && span > maxWidth)
        {
            spacing = maxWidth / (count - 1);
            span = maxWidth;
        }

        var radius = MathF.Min(DotRadius * scale, spacing * 0.32f);
        var startX = center.X - span * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var dot = new Vector2(startX + index * spacing, center.Y);
            var alpha = index == active ? 0.95f : 0.32f;
            drawList.AddCircleFilled(dot, radius, ImGui.GetColorU32(Palette.WithAlpha(ink, alpha)), 12);
        }
    }

    private Pager PagerFor(string postId)
    {
        if (pagers.TryGetValue(postId, out var existing))
        {
            return existing;
        }

        var created = new Pager();
        pagers[postId] = created;
        return created;
    }

    private bool DriveGesture(Pager pager, Rect rect, string postId, int count, float delta, float scale)
    {
        if (dragPostId == postId && pager.Dragging)
        {
            pager.Drag(ImGui.GetMousePos().X, rect.Width, count, delta);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                pager.Release(rect.Width, count);
                dragPostId = string.Empty;
            }

            return true;
        }

        if (!pressActive || pressPostId != postId || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            return false;
        }

        var move = ImGui.GetMousePos() - pressPos;
        if (MathF.Abs(move.X) <= SwipeThreshold * scale || MathF.Abs(move.X) <= MathF.Abs(move.Y) * 1.2f)
        {
            return false;
        }

        pager.Begin(pressPos.X);
        dragPostId = postId;
        pressActive = false;
        return true;
    }

    private bool ResolveTap(Rect rect, string postId, bool consumed, bool blocked)
    {
        if (!blocked && UiInteract.Hover(rect.Min, rect.Max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pressActive = true;
            pressPostId = postId;
            pressPos = ImGui.GetMousePos();
        }

        if (consumed || !pressActive || pressPostId != postId || !ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            return false;
        }

        pressActive = false;
        var move = ImGui.GetMousePos() - pressPos;
        return move.Length() < TapSlop * ImGuiHelpers.GlobalScale;
    }

    private static void DrawCounter(ImDrawListPtr drawList, Rect rect, int active, int count, float scale)
    {
        var label = Loc.T(L.Common.PhotoCounter, active + 1, count);
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var padding = new Vector2(8f * scale, 4f * scale);
        var max = new Vector2(rect.Max.X - 10f * scale, rect.Min.Y + 10f * scale + size.Y + padding.Y * 2f);
        var min = new Vector2(max.X - size.X - padding.X * 2f, rect.Min.Y + 10f * scale);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(BadgeSurface), (max.Y - min.Y) * 0.5f);
        Typography.Draw(drawList, min + padding, label, BadgeInk, TextStyles.FootnoteEmphasized);
    }

    private static bool DrawChevrons(ImDrawListPtr drawList, Rect rect, Pager pager, int active, int count, float scale,
        out bool clicked)
    {
        clicked = false;
        if (!UiInteract.Hover(rect.Min, rect.Max) || pager.Dragging)
        {
            return false;
        }

        var hot = false;
        var radius = ChevronRadius * scale;
        var inset = ChevronInset * scale;
        if (active > 0)
        {
            var center = new Vector2(rect.Min.X + inset + radius, rect.Center.Y);
            if (DrawChevron(drawList, center, radius, FontAwesomeIcon.ChevronLeft.ToIconString(), out var pressed))
            {
                hot = true;
                if (pressed)
                {
                    pager.AnimateTo(active - 1, count);
                    clicked = true;
                }
            }
        }

        if (active < count - 1)
        {
            var center = new Vector2(rect.Max.X - inset - radius, rect.Center.Y);
            if (DrawChevron(drawList, center, radius, FontAwesomeIcon.ChevronRight.ToIconString(), out var pressed))
            {
                hot = true;
                if (pressed)
                {
                    pager.AnimateTo(active + 1, count);
                    clicked = true;
                }
            }
        }

        return hot;
    }

    private static bool DrawChevron(ImDrawListPtr drawList, Vector2 center, float radius, string glyph,
        out bool clicked)
    {
        clicked = false;
        var min = new Vector2(center.X - radius, center.Y - radius);
        var max = new Vector2(center.X + radius, center.Y + radius);
        var hovered = UiInteract.Hover(min, max);
        var surface = Palette.WithAlpha(ChevronSurface, hovered ? 1f : 0.88f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(surface), 24);
        AppSkin.Icon(drawList, center, glyph, ChevronInk, 0.75f);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        return true;
    }
}
