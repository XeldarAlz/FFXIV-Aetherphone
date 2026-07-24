using Aetherphone.Core;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet.Kit;

internal enum VRowLeading
{
    Avatar,
    IconTile,
    None,
}

internal enum VRowHit
{
    None,
    Body,
    Pill,
    Overflow,
    Decline,
}

internal struct VRowModel
{
    public string Title;
    public string Subtitle;
    public float Height;
    public VRowLeading Leading;
    public float AvatarRadius;
    public string Name;
    public string World;
    public string? AvatarUrl;
    public int Presence;
    public FontAwesomeIcon TileIcon;
    public Vector4 TileTint;
    public string? Pill;
    public bool PillFilled;
    public bool PillEnabled;
    public int Badge;
    public string Time;
    public bool Chevron;
    public bool Decline;
}

internal static class VRow
{
    public static VRowHit Draw(in VRowModel model, AppSkin ui, PhoneTheme theme, RemoteImageCache images,
        LodestoneService lodestone)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = (model.Height <= 0f ? 64f : model.Height) * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var centerY = (min.Y + max.Y) * 0.5f;
        var hovered = UiInteract.Hover(min, max);
        var hit = VRowHit.None;
        var titleText = model.Title ?? string.Empty;
        var subtitleText = model.Subtitle ?? string.Empty;
        var nameText = model.Name ?? string.Empty;
        var worldText = model.World ?? string.Empty;
        var timeText = model.Time ?? string.Empty;

        if (hovered)
        {
            Squircle.Fill(drawList, min, max, Metrics.Radius.Md * scale,
                VelvetTheme.Alpha(VelvetTheme.TitleInk, 0.05f).Packed());
        }

        var leftPad = Metrics.Space.Lg * scale;
        float textLeft;
        if (model.Leading == VRowLeading.Avatar)
        {
            var radius = (model.AvatarRadius <= 0f ? 20f : model.AvatarRadius) * scale;
            var avatarCenter = new Vector2(min.X + leftPad + radius, centerY);
            VAvatar.Draw(drawList, avatarCenter, radius, theme, nameText, worldText, model.AvatarUrl, images,
                lodestone, model.Presence);
            textLeft = avatarCenter.X + radius + Metrics.Space.Md * scale;
        }
        else if (model.Leading == VRowLeading.IconTile)
        {
            var tile = Metrics.Size.IconTile * scale;
            var tileMin = new Vector2(min.X + leftPad, centerY - tile * 0.5f);
            var tileMax = new Vector2(tileMin.X + tile, centerY + tile * 0.5f);
            Squircle.Fill(drawList, tileMin, tileMax, Metrics.Radius.Sm * scale,
                VelvetTheme.Alpha(model.TileTint, 0.20f).Packed());
            AppSkin.Icon(new Vector2((tileMin.X + tileMax.X) * 0.5f, centerY), model.TileIcon.ToIconString(),
                model.TileTint, 0.8f);
            textLeft = tileMax.X + Metrics.Space.Md * scale;
        }
        else
        {
            textLeft = min.X + leftPad;
        }

        var rightEdge = max.X - leftPad;
        if (model.Chevron)
        {
            AppSkin.Icon(new Vector2(rightEdge - 8f * scale, centerY), FontAwesomeIcon.ChevronRight.ToIconString(),
                VelvetTheme.MutedInk, 0.78f);
            rightEdge -= 22f * scale;
        }

        if (model.Decline)
        {
            if (ui.IconButton(new Vector2(rightEdge - 10f * scale, centerY), 13f * scale,
                    FontAwesomeIcon.Times.ToIconString(), VelvetTheme.MutedInk, AppSkin.Transparent, 0.9f))
            {
                hit = VRowHit.Decline;
            }

            rightEdge -= 30f * scale;
        }

        if (model.Pill != null)
        {
            var pillHeight = 30f * scale;
            var pillWidth = Typography.Measure(model.Pill, 0.9f, FontWeight.SemiBold).X + 26f * scale;
            var pillRect = new Rect(new Vector2(rightEdge - pillWidth, centerY - pillHeight * 0.5f),
                new Vector2(rightEdge, centerY + pillHeight * 0.5f));
            if (model.PillEnabled)
            {
                if (ui.PillButton(pillRect, model.Pill, model.PillFilled))
                {
                    hit = VRowHit.Pill;
                }
            }
            else
            {
                Squircle.Fill(drawList, pillRect.Min, pillRect.Max, pillHeight * 0.5f, VelvetTheme.PlumWell.Packed());
                Typography.DrawCentered(pillRect.Center, model.Pill, VelvetTheme.MutedInk, 0.9f, FontWeight.SemiBold);
            }

            rightEdge -= pillWidth + Metrics.Space.Sm * scale;
        }

        if (timeText.Length > 0)
        {
            var timeSize = Typography.Measure(timeText, TextStyles.Caption1);
            Typography.Draw(new Vector2(rightEdge - timeSize.X, min.Y + 12f * scale), timeText, VelvetTheme.MutedInk,
                TextStyles.Caption1);
        }

        if (model.Badge > 0)
        {
            VBadge.Count(drawList, new Vector2(rightEdge, max.Y - 16f * scale), model.Badge);
        }

        var textRight = rightEdge - Metrics.Space.Sm * scale;
        var innerWidth = MathF.Max(10f * scale, textRight - textLeft);
        if (subtitleText.Length == 0)
        {
            var titleSize = Typography.Measure(titleText, TextStyles.Headline);
            Marquee.DrawLeft("vrow.title." + titleText, titleText, textLeft, centerY - titleSize.Y * 0.5f,
                innerWidth, TextStyles.Headline, VelvetTheme.TitleInk, hovered);
        }
        else
        {
            var titleY = centerY - 15f * scale;
            var titleSize = Typography.Measure(titleText, TextStyles.Headline);
            var titleHovering = ImGui.IsMouseHoveringRect(new Vector2(textLeft, titleY),
                new Vector2(textLeft + innerWidth, titleY + titleSize.Y));
            Marquee.DrawLeft("vrow.title." + titleText, titleText, textLeft, titleY, innerWidth,
                TextStyles.Headline, VelvetTheme.TitleInk, titleHovering);
            var subtitleY = centerY + 3f * scale;
            var subtitleSize = Typography.Measure(subtitleText, TextStyles.Subheadline);
            var subtitleHovering = ImGui.IsMouseHoveringRect(new Vector2(textLeft, subtitleY),
                new Vector2(textLeft + innerWidth, subtitleY + subtitleSize.Y));
            Marquee.DrawLeft("vrow.subtitle." + subtitleText, subtitleText, textLeft, subtitleY,
                innerWidth, TextStyles.Subheadline, VelvetTheme.MutedInk, subtitleHovering);
        }

        if (hit == VRowHit.None && UiInteract.Click(min, max, hovered))
        {
            hit = VRowHit.Body;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return hit;
    }
}
