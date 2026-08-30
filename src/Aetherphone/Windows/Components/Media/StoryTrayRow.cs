using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal delegate void StoryRingPainter(ImDrawListPtr drawList, Vector2 center, float radius, float scale, bool unseen);

internal sealed class StoryTrayRow
{
    private const float Height = 100f;
    private const float TileWidth = 76f;
    private const float RingRadius = 30f;
    private const float AvatarInset = 4f;
    private const float AvatarLift = 9f;
    private const float LabelGap = 9f;
    private const float AddBadgeRadius = 9f;
    private const float AddBadgeRing = 2f;
    private const float AddBadgeGlyph = 12f;
    private const float DragSlop = 5f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 EmptyRing = new(1f, 1f, 1f, 0.18f);

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
        StoryRingPainter painter, Action onAddStory, Action<StoryRingDto> onOpenRing, string? ownAvatarUrl = null,
        string ownName = "", string? ownFrameId = null)
    {
        var scale = UiScale.Current;
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
            DrawAddTile(drawList, new Vector2(x + tile * 0.5f, row.Center.Y), theme, palette, scale, onAddStory,
                ownAvatarUrl, ownName, ownFrameId);
            x += tile;
        }

        for (var index = 0; index < rings.Length; index++)
        {
            DrawRingTile(drawList, new Vector2(x + tile * 0.5f, row.Center.Y), theme, palette, rings[index], scale,
                painter, onOpenRing);
            x += tile;
        }

        drawList.PopClipRect();
        ImGui.SetCursorScreenPos(new Vector2(row.Min.X, row.Max.Y));
    }

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
        var hovering = UiInteract.Hover(row.Min, row.Max);
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
        float scale, Action onAddStory, string? ownAvatarUrl, string ownName, string? ownFrameId)
    {
        var radius = RingRadius * scale;
        var center = new Vector2(slotCenter.X, slotCenter.Y - AvatarLift * scale);
        var avatarRadius = radius - AvatarInset * scale;
        if (ownAvatarUrl is null && ownName.Length == 0)
        {
            drawList.AddCircleFilled(center, avatarRadius, ImGui.GetColorU32(palette.FieldSurface), 32);
            drawList.AddCircle(center, radius, ImGui.GetColorU32(EmptyRing), 32, 1.4f * scale);
            PhoneIcon.Draw(drawList, center, PhoneIcons.Plus, palette.BodyInk, 22f * scale);
        }
        else
        {
            AvatarView.DrawRemote(drawList, center, avatarRadius, theme, ownName, string.Empty, ownAvatarUrl, images,
                lodestone, 0.8f, 32, 1f, Frames.Of(ownFrameId));
            var badgeCenter = center + new Vector2(avatarRadius, avatarRadius) * 0.72f;
            var badgeRadius = AddBadgeRadius * scale;
            drawList.AddCircleFilled(badgeCenter, badgeRadius + AddBadgeRing * scale,
                ImGui.GetColorU32(palette.BackdropTop), 24);
            drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(palette.Accent), 24);
            PhoneIcon.Draw(drawList, badgeCenter, PhoneIcons.Plus, White, AddBadgeGlyph * scale);
        }

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
        var center = new Vector2(slotCenter.X, slotCenter.Y - AvatarLift * scale);
        painter(drawList, center, radius, scale, ring.HasUnseen);
        var name = SocialIdentity.Name(ring.AuthorDisplayName, ring.AuthorHandle);
        var label = ring.IsMe ? Loc.T(L.Story.YourStory) : ring.AuthorHandle.Length > 0 ? ring.AuthorHandle : name;
        AvatarView.DrawRemote(drawList, center, radius - AvatarInset * scale, theme, name, string.Empty,
            ring.AuthorAvatarUrl, images, lodestone, 0.8f, 32, 1f, Frames.Of(ring.AuthorFrameId));
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
        var fitted = Typography.FitText(label, maxWidth, TextStyles.Footnote);
        var size = Typography.Measure(fitted, TextStyles.Footnote);
        var top = new Vector2(center.X - size.X * 0.5f, center.Y + radius + LabelGap * scale - size.Y * 0.5f);
        Typography.Draw(drawList, top, fitted, palette.MutedInk, TextStyles.Footnote);
    }
}
