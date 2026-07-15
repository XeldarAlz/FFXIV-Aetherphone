using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

// The @ autocomplete list. Shaped like DropdownMenu (foreground draw list, flip and clamp against the
// phone frame, reveal easing) but with avatar rows and a keyboard selection the dropdown has no concept
// of. Anchors to the field rather than the caret, so a composer pinned to the bottom always opens upward
// and the list can never leave the screen.
internal sealed class MentionPopup
{
    private const float RowHeight = 44f;
    private const float MinWidth = 200f;
    private const float MaxRows = 5f;
    private const double RevealSeconds = 0.14;

    private double openedAt = -1d;

    public void Gate(MentionAutocomplete autocomplete)
    {
        if (autocomplete.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public int Draw(MentionAutocomplete autocomplete, Rect screen, PhoneTheme theme, RemoteImageCache images,
        LodestoneService lodestone)
    {
        if (!autocomplete.IsOpen)
        {
            openedAt = -1d;
            return -1;
        }

        if (openedAt < 0d)
        {
            openedAt = ImGui.GetTime();
        }

        var rows = autocomplete.Rows;
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetForegroundDrawList();
        var reveal = Easing.EaseOutQuint(Math.Clamp((float)((ImGui.GetTime() - openedAt) / RevealSeconds), 0f, 1f));
        var alpha = Easing.SmoothStep(Math.Clamp(reveal / 0.7f, 0f, 1f));
        var anchor = autocomplete.Anchor;
        var padY = 6f * scale;
        var rowHeight = RowHeight * scale;
        var visible = rows.Length == 0 ? 1 : (int)MathF.Min(rows.Length, MaxRows);
        var width = MathF.Max(MinWidth * scale, anchor.Width);
        var height = visible * rowHeight + padY * 2f;

        var left = Math.Clamp(anchor.Min.X, screen.Min.X + 8f * scale,
            MathF.Max(screen.Min.X + 8f * scale, screen.Max.X - 8f * scale - width));
        var top = anchor.Min.Y - 6f * scale - height;
        if (top < screen.Min.Y + 8f * scale)
        {
            top = anchor.Max.Y + 6f * scale;
        }

        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + height);
        Elevation.Floating(drawList, min, max, 14f * scale, scale);
        Squircle.Fill(drawList, min, max, 14f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, MathF.Min(0.98f, theme.GroupedCard.W + 0.4f) * alpha)));
        Material.EdgeSquircle(drawList, min, max, 14f * scale, scale);

        if (rows.Length == 0)
        {
            var message = Loc.T(L.Social.MentionSearching);
            Typography.DrawCentered(new Vector2((min.X + max.X) * 0.5f, min.Y + padY + rowHeight * 0.5f - 7f * scale),
                message, Palette.WithAlpha(theme.TextMuted, alpha), 0.9f);
            return -1;
        }

        var clicked = -1;
        for (var index = 0; index < visible; index++)
        {
            var row = rows[index];
            var rowMin = new Vector2(min.X + padY, min.Y + padY + index * rowHeight);
            var rowMax = new Vector2(max.X - padY, rowMin.Y + rowHeight);
            var hovered = ImGui.IsMouseHoveringRect(rowMin, rowMax);
            if (hovered || index == autocomplete.SelectedIndex)
            {
                Squircle.Fill(drawList, rowMin, rowMax, 9f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, (hovered ? 0.09f : 0.06f) * alpha)));
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    clicked = index;
                }
            }

            var avatarRadius = 14f * scale;
            var avatarCenter = new Vector2(rowMin.X + 6f * scale + avatarRadius, (rowMin.Y + rowMax.Y) * 0.5f);
            AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, row.DisplayName, string.Empty,
                row.AvatarUrl, images, lodestone, 0.8f, 28);

            var textLeft = avatarCenter.X + avatarRadius + 9f * scale;
            var name = SocialIdentity.Name(row.DisplayName, row.Handle);
            var nameSize = Typography.Measure(name, 0.92f, FontWeight.SemiBold);
            Typography.Draw(new Vector2(textLeft, rowMin.Y + 6f * scale), name,
                Palette.WithAlpha(theme.TextStrong, alpha), 0.92f, FontWeight.SemiBold);
            Typography.Draw(new Vector2(textLeft, rowMin.Y + 6f * scale + nameSize.Y), "@" + row.Handle,
                Palette.WithAlpha(theme.TextMuted, alpha), 0.82f);
        }

        return clicked;
    }
}
