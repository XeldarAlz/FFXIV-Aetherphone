using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Venues;

internal enum VenueCardAction : byte
{
    None,
    Open,
    ToggleFavorite,
}

internal static class VenueCard
{
    public const float Height = 92f;

    public static VenueCardAction Draw(Rect card, VenueEvent venue, bool favorite, MediaCache media, HttpService http,
        ArtworkCache art, PhoneTheme theme, DateTime nowUtc)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 16f * scale;
        var hovered = ImGui.IsMouseHoveringRect(card.Min, card.Max);
        var fill = hovered ? Palette.Mix(theme.GroupedCard, theme.TextStrong, 0.05f) : theme.GroupedCard;
        Squircle.Fill(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(fill));
        Material.EdgeSquircle(drawList, card.Min, card.Max, rounding, scale);
        var pad = 10f * scale;
        var thumbSide = card.Height - pad * 2f;
        var thumb = new Rect(new Vector2(card.Min.X + pad, card.Min.Y + pad),
            new Vector2(card.Min.X + pad + thumbSide, card.Min.Y + pad + thumbSide));
        VenueImage.Draw(drawList, thumb, venue, media, http, art, 12f * scale);
        var live = venue.IsLive(nowUtc);
        if (live)
        {
            var dotCenter = new Vector2(thumb.Min.X + 8f * scale, thumb.Min.Y + 8f * scale);
            drawList.AddCircleFilled(dotCenter, 5.5f * scale,
                ImGui.GetColorU32(new Vector4(0.04f, 0.04f, 0.06f, 0.85f)), 18);
            drawList.AddCircleFilled(dotCenter, 3.4f * scale, ImGui.GetColorU32(theme.ToggleOn), 18);
        }

        var starCenter = new Vector2(card.Max.X - 17f * scale, card.Min.Y + 17f * scale);
        var starHovered = ImGui.IsMouseHoveringRect(starCenter - new Vector2(14f * scale, 14f * scale),
            starCenter + new Vector2(14f * scale, 14f * scale));
        DrawStar(starCenter, favorite ? theme.Accent :
            starHovered ? theme.TextStrong : theme.TextMuted);
        var textLeft = thumb.Max.X + 12f * scale;
        var textRight = card.Max.X - 34f * scale;
        var textWidth = textRight - textLeft;
        var title = VenueText.Fit(venue.Title, textWidth, 0.98f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, card.Min.Y + 11f * scale), title, theme.TextStrong, 0.98f,
            FontWeight.SemiBold);
        var subtitle = VenueText.Fit(BuildSubtitle(venue), textWidth, 0.78f, FontWeight.Regular);
        Typography.Draw(new Vector2(textLeft, card.Min.Y + 31f * scale), subtitle, theme.TextMuted, 0.78f);
        var timeY = card.Min.Y + 49f * scale;
        if (live)
        {
            var liveLabel = Loc.T(L.Common.Live);
            Typography.Draw(new Vector2(textLeft, timeY), liveLabel, theme.ToggleOn, 0.78f, FontWeight.SemiBold);
            var ends = VenueFormat.EndsAt(venue);
            if (ends.Length > 0)
            {
                var offset = Typography.Measure(liveLabel, 0.78f, FontWeight.SemiBold).X + 8f * scale;
                Typography.Draw(new Vector2(textLeft + offset, timeY), $"· {ends}", theme.TextMuted, 0.78f);
            }
        }
        else
        {
            Typography.Draw(new Vector2(textLeft, timeY), VenueFormat.Range(venue), theme.Accent, 0.78f,
                FontWeight.Medium);
        }

        DrawTags(drawList, venue, textLeft, card.Max.Y - pad - VenueChips.Height(scale), textRight, theme, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (starHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return VenueCardAction.ToggleFavorite;
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return VenueCardAction.Open;
        }

        return VenueCardAction.None;
    }

    private static string BuildSubtitle(VenueEvent venue)
    {
        var place = venue.World;
        if (venue.DataCenter.Length > 0)
        {
            place = place.Length > 0 ? $"{place} ({venue.DataCenter})" : venue.DataCenter;
        }

        if (venue.LocationLine.Length > 0 && !string.Equals(venue.LocationLine, venue.World, StringComparison.Ordinal))
        {
            return place.Length > 0 ? $"{place} · {venue.LocationLine}" : venue.LocationLine;
        }

        return place;
    }

    private static void DrawTags(ImDrawListPtr drawList, VenueEvent venue, float left, float top, float right,
        PhoneTheme theme, float scale)
    {
        if (venue.Tags.Count == 0)
        {
            return;
        }

        var gap = 5f * scale;
        var cursor = left;
        for (var index = 0; index < venue.Tags.Count; index++)
        {
            var tag = venue.Tags[index];
            var width = VenueChips.Measure(tag, scale);
            if (cursor + width > right)
            {
                DrawPlusChip(drawList, new Vector2(cursor, top), venue.Tags.Count - index, theme, scale);
                return;
            }

            VenueChips.Draw(drawList, new Vector2(cursor, top), tag, scale);
            cursor += width + gap;
        }
    }

    private static void DrawPlusChip(ImDrawListPtr drawList, Vector2 position, int remaining, PhoneTheme theme,
        float scale)
    {
        var label = $"+{remaining}";
        var textSize = Typography.Measure(label, 0.72f);
        var width = textSize.X + 12f * scale;
        var height = VenueChips.Height(scale);
        var min = position;
        var max = new Vector2(position.X + width, position.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(theme.SurfaceMuted));
        Typography.Draw(new Vector2(min.X + (width - textSize.X) * 0.5f, min.Y + (height - textSize.Y) * 0.5f), label,
            theme.TextMuted, 0.72f);
    }

    private static void DrawStar(Vector2 center, Vector4 color)
    {
        var glyph = FontAwesomeIcon.Star.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(glyph);
            }
        }
    }
}
