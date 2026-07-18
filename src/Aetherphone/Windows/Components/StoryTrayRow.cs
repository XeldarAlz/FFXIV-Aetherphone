using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal delegate void StoryRingPainter(ImDrawListPtr drawList, Vector2 center, float radius, float scale, bool unseen);

/// <summary>
/// The horizontal row of story rings that opens a feed. Like Instagram, the row is the first item
/// inside the feed's scroll container rather than a band pinned above it, so it scrolls away with the
/// posts instead of having them slide under it. ImGui has no horizontal scroll container in use
/// anywhere in this app, so the row hand rolls drag scrolling inside a clip rect: the offset is
/// carried per instance and a drag past a small threshold suppresses the click that would open a ring.
/// The wheel is deliberately left alone so it scrolls the feed underneath, which is what the same
/// gesture does on Instagram.
/// The row paints no background of its own. The app backdrop behind it is a vertical gradient plus a
/// bloom, so any edge treatment painted from a single palette colour reads as a seam.
/// </summary>
internal sealed class StoryTrayRow
{
    private const float Height = 92f;
    private const float TileWidth = 68f;
    private const float RingRadius = 27f;
    private const float DragSlop = 5f;

    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private float offset;
    private float maxOffset;
    private bool dragging;
    private float dragTravel;
    private float lastMouseX;

    public StoryTrayRow(RemoteImageCache images, LodestoneService lodestone)
    {
        this.images = images;
        this.lodestone = lodestone;
    }

    public void Draw(PhoneTheme theme, AppPalette palette, StoryRingDto[] rings, bool hasOwnStory,
        StoryRingPainter painter, Action onAddStory, Action<StoryRingDto> onOpenRing)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var tile = TileWidth * scale;
        var slots = rings.Length + (hasOwnStory ? 0 : 1);
        if (slots == 0)
        {
            return;
        }

        var row = ReserveRow(scale);
        var content = tile * slots + 12f * scale;
        maxOffset = MathF.Max(0f, content - row.Width);
        HandleDrag(row);

        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(row.Min, row.Max, true);
        var x = row.Min.X + 6f * scale - offset;
        if (!hasOwnStory)
        {
            DrawAddTile(drawList, new Vector2(x + tile * 0.5f, row.Center.Y), theme, palette, scale, onAddStory);
            x += tile;
        }

        for (var index = 0; index < rings.Length; index++)
        {
            DrawRingTile(drawList, new Vector2(x + tile * 0.5f, row.Center.Y), theme, palette, rings[index], scale,
                painter, onOpenRing);
            x += tile;
        }

        drawList.PopClipRect();
    }

    // Only the height is taken from the layout. The band itself spans the scroll container edge to edge
    // so rings pan past the window padding the posts are inset by, the way they do on Instagram.
    private static Rect ReserveRow(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = Height * scale;
        ImGui.Dummy(new Vector2(width, height));
        return new Rect(new Vector2(ImGui.GetWindowPos().X, origin.Y),
            new Vector2(origin.X + width, origin.Y + height));
    }

    private void HandleDrag(Rect row)
    {
        var hovering = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        if (hovering && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            dragTravel = 0f;
            lastMouseX = ImGui.GetIO().MousePos.X;
        }

        if (dragging && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var mouseX = ImGui.GetIO().MousePos.X;
            var travel = mouseX - lastMouseX;
            lastMouseX = mouseX;
            dragTravel += MathF.Abs(travel);
            offset = Math.Clamp(offset - travel, 0f, maxOffset);
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            dragging = false;
        }

        offset = Math.Clamp(offset, 0f, maxOffset);
    }

    private bool ClickedTile(Vector2 center, float radius)
    {
        if (dragTravel > DragSlop)
        {
            return false;
        }

        return UiInteract.HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
    }

    private void DrawAddTile(ImDrawListPtr drawList, Vector2 slotCenter, PhoneTheme theme, AppPalette palette,
        float scale, Action onAddStory)
    {
        var radius = RingRadius * scale;
        var center = new Vector2(slotCenter.X, slotCenter.Y - 8f * scale);
        drawList.AddCircleFilled(center, radius - 2f * scale, ImGui.GetColorU32(palette.FieldSurface), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.18f)), 32, 1.4f * scale);
        AppSkin.Icon(center, FontAwesomeIcon.Plus.ToIconString(), palette.BodyInk, 1.2f);
        DrawLabel(drawList, center, radius, Loc.T(L.Story.YourStory), palette, scale);
        if (ClickedTile(center, radius))
        {
            onAddStory();
        }
    }

    private void DrawRingTile(ImDrawListPtr drawList, Vector2 slotCenter, PhoneTheme theme, AppPalette palette,
        StoryRingDto ring, float scale, StoryRingPainter painter, Action<StoryRingDto> onOpenRing)
    {
        var radius = RingRadius * scale;
        var center = new Vector2(slotCenter.X, slotCenter.Y - 8f * scale);
        painter(drawList, center, radius, scale, ring.HasUnseen);
        var label = ring.IsMe ? Loc.T(L.Story.YourStory) : SocialIdentity.Name(ring.AuthorDisplayName, ring.AuthorHandle);
        AvatarView.DrawRemote(drawList, center, radius - 4f * scale, theme, label, string.Empty, ring.AuthorAvatarUrl,
            images, lodestone, 0.8f, 32);
        DrawLabel(drawList, center, radius, label, palette, scale);
        if (ClickedTile(center, radius))
        {
            onOpenRing(ring);
        }
    }

    private static void DrawLabel(ImDrawListPtr drawList, Vector2 center, float radius, string label,
        AppPalette palette, float scale)
    {
        var maxWidth = TileWidth * scale - 8f * scale;
        var fitted = Typography.FitText(label, maxWidth, TextStyles.Caption1);
        var baseline = new Vector2(center.X, center.Y + radius + 11f * scale);
        Typography.DrawCentered(drawList, baseline, fitted, palette.MutedInk, TextStyles.Caption1);
    }

}
