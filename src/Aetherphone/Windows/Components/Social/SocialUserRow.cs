using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct SocialUserRowStyle(
    float Height,
    float AvatarRadius,
    float PadX,
    float AvatarGap,
    float TrailingHeight,
    TextStyle NameStyle,
    TextStyle SubStyle,
    Vector4 HoverWash);

internal readonly record struct SocialUserRowResult(
    bool Tapped,
    Rect Bounds,
    Rect Trailing,
    Vector2 AvatarCenter,
    float AvatarRadius);

internal static class SocialUserRow
{
    private const float TrailingGap = 10f;
    private const float TapGap = 6f;
    private const float LineGap = 2f;

    public static bool SkipHidden(in SocialUserRowStyle style)
    {
        var size = new Vector2(ScrollLayout.StableContentWidth(), style.Height * UiScale.Current);
        if (ImGui.IsRectVisible(size))
        {
            return false;
        }

        ImGui.Dummy(size);
        return true;
    }

    public static SocialUserRowResult Draw(string idPrefix, UserDto user, string subtitle, float trailingWidth,
        in SocialUserRowStyle style, SocialInk ink, PhoneTheme theme, RemoteImageCache images,
        LodestoneService lodestone)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = style.Height * scale;
        var padX = style.PadX * scale;
        var rowMax = new Vector2(origin.X + width, origin.Y + height);
        var trailingHeight = style.TrailingHeight * scale;
        var trailingMin = new Vector2(rowMax.X - padX - trailingWidth * scale, origin.Y + (height - trailingHeight) * 0.5f);
        var trailing = new Rect(trailingMin, new Vector2(rowMax.X - padX, trailingMin.Y + trailingHeight));
        var hasTrailing = trailingWidth > 0f;
        var tapMax = new Vector2(hasTrailing ? trailingMin.X - TapGap * scale : rowMax.X, rowMax.Y);
        var hovered = UiInteract.Hover(origin, tapMax);
        if (hovered)
        {
            drawList.AddRectFilled(origin, rowMax, ImGui.GetColorU32(style.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var radius = style.AvatarRadius * scale;
        var avatarCenter = new Vector2(origin.X + padX + radius, origin.Y + height * 0.5f);
        var displayName = SocialIdentity.Name(user.DisplayName, user.Handle);
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, user.IsMe ? user.Name : displayName,
            user.IsMe ? user.World : string.Empty, user.AvatarUrl, images, lodestone, 0.95f, 32, 1f,
            Frames.Of(user.FrameId));
        var textLeft = avatarCenter.X + radius + style.AvatarGap * scale;
        var textRight = hasTrailing ? trailingMin.X - TrailingGap * scale : rowMax.X - padX;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var nameHeight = Typography.LineHeight(style.NameStyle);
        var subHeight = Typography.LineHeight(style.SubStyle);
        var textTop = origin.Y + (height - nameHeight - subHeight - LineGap * scale) * 0.5f;
        UserName.DrawAuto(drawList, idPrefix + user.Id, displayName, user.Badges, user.ProfileBadges, textLeft,
            textTop, textWidth, style.NameStyle, ink.TitleInk, theme);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight + LineGap * scale),
            Typography.FitText(subtitle, textWidth, style.SubStyle), ink.MutedInk, style.SubStyle);
        var tapped = UiInteract.Click(origin, tapMax, hovered);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return new SocialUserRowResult(tapped, new Rect(origin, rowMax), trailing, avatarCenter, radius);
    }
}
