using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Venues;

internal enum VenueCardAction : byte
{
    None,
    Open,
    ToggleFavorite,
}

internal static class VenueCard
{
    public const float Height = 100f;
    public const float Gap = 10f;

    public static readonly Vector4 LiveGreen = new(0.24f, 0.82f, 0.44f, 1f);

    public static VenueCardAction Draw(Rect card, VenueEvent venue, bool favorite, MediaCache media, HttpService http,
        ArtworkCache art, AppSkin ui, DateTime nowUtc)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Lg * scale;
        var palette = ui.Palette;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var pad = 12f * scale;
        var thumbSide = card.Height - pad * 2f;
        var thumb = new Rect(new Vector2(card.Min.X + pad, card.Min.Y + pad),
            new Vector2(card.Min.X + pad + thumbSide, card.Min.Y + pad + thumbSide));
        VenueImage.Draw(drawList, thumb, venue, media, http, art, 14f * scale);
        var live = venue.IsLive(nowUtc);
        if (live)
        {
            DrawLiveDot(drawList, new Vector2(thumb.Min.X + 9f * scale, thumb.Min.Y + 9f * scale), scale);
        }

        var starCenter = new Vector2(card.Max.X - 19f * scale, card.Min.Y + 19f * scale);
        var starHit = new Vector2(14f * scale, 14f * scale);
        var starMin = starCenter - starHit;
        var starMax = starCenter + starHit;
        var starHovered = UiInteract.Hover(starMin, starMax);
        AppSkin.Icon(starCenter, FontAwesomeIcon.Star.ToIconString(),
            favorite ? palette.Accent : starHovered ? palette.TitleInk : Palette.WithAlpha(palette.MutedInk, 0.75f),
            0.85f);
        var textLeft = thumb.Max.X + 12f * scale;
        var textRight = card.Max.X - 36f * scale;
        var textWidth = textRight - textLeft;
        var title = VenueText.Fit(venue.Title, textWidth, TextStyles.Headline.Scale, TextStyles.Headline.Weight);
        Typography.Draw(new Vector2(textLeft, card.Min.Y + 13f * scale), title, palette.TitleInk, TextStyles.Headline);
        var subtitle = VenueText.Fit(BuildSubtitle(venue), textWidth, TextStyles.Footnote.Scale,
            TextStyles.Footnote.Weight);
        Typography.Draw(new Vector2(textLeft, card.Min.Y + 34f * scale), subtitle, palette.MutedInk,
            TextStyles.Footnote);
        DrawTimeRow(drawList, venue, live, textLeft, card.Min.Y + 53f * scale, card.Max.X - pad, palette, scale);
        DrawTags(drawList, venue, textLeft, card.Max.Y - pad - VenueChips.Height(scale), textRight);
        var hovered = UiInteract.Hover(card.Min, card.Max);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, card.Min, card.Max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(starMin, starMax, starHovered))
        {
            return VenueCardAction.ToggleFavorite;
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            return VenueCardAction.Open;
        }

        return VenueCardAction.None;
    }

    private static void DrawLiveDot(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var pulse = 0.55f + 0.45f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircleFilled(center, 6.5f * scale, ImGui.GetColorU32(new Vector4(0.03f, 0.05f, 0.04f, 0.85f)), 20);
        drawList.AddCircle(center, 5.6f * scale, ImGui.GetColorU32(Palette.WithAlpha(LiveGreen, 0.45f * pulse)), 20,
            1.4f * scale);
        drawList.AddCircleFilled(center, 3.4f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(LiveGreen, 0.65f + 0.35f * pulse)), 20);
    }

    private static void DrawTimeRow(ImDrawListPtr drawList, VenueEvent venue, bool live, float left, float top,
        float right, in AppPalette palette, float scale)
    {
        if (live)
        {
            var liveLabel = Loc.T(L.Common.Live);
            Typography.Draw(new Vector2(left, top), liveLabel, LiveGreen, TextStyles.FootnoteEmphasized);
            var ends = VenueFormat.EndsAt(venue);
            if (ends.Length > 0)
            {
                var offset = Typography.Measure(liveLabel, TextStyles.FootnoteEmphasized).X + 7f * scale;
                Typography.Draw(new Vector2(left + offset, top), Loc.T(L.Venues.UntilTime, ends), palette.MutedInk,
                    TextStyles.Footnote);
            }
        }
        else
        {
            Typography.Draw(new Vector2(left, top), VenueFormat.Range(venue), palette.Accent,
                new TextStyle(TextStyles.Footnote.Scale, FontWeight.Medium));
        }

        if (venue.AttendeeCount <= 0)
        {
            return;
        }

        var count = venue.AttendeeCount.ToString(Loc.Culture);
        var countSize = Typography.Measure(count, TextStyles.Footnote);
        var countLeft = right - countSize.X;
        Typography.Draw(new Vector2(countLeft, top), count, palette.MutedInk, TextStyles.Footnote);
        AppSkin.Icon(drawList, new Vector2(countLeft - 10f * scale, top + countSize.Y * 0.5f),
            FontAwesomeIcon.Users.ToIconString(), Palette.WithAlpha(palette.MutedInk, 0.8f), 0.58f);
    }

    private static string BuildSubtitle(VenueEvent venue)
    {
        var place = venue.World;
        if (venue.DataCenter.Length > 0)
        {
            place = place.Length > 0 ? $"{place} · {venue.DataCenter}" : venue.DataCenter;
        }

        if (venue.LocationLine.Length > 0 && !string.Equals(venue.LocationLine, venue.World, StringComparison.Ordinal))
        {
            return place.Length > 0 ? $"{place} · {venue.LocationLine}" : venue.LocationLine;
        }

        return place;
    }

    private static void DrawTags(ImDrawListPtr drawList, VenueEvent venue, float left, float top, float right)
    {
        if (venue.Tags.Count == 0)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var gap = 5f * scale;
        var cursor = left;
        for (var index = 0; index < venue.Tags.Count; index++)
        {
            var tag = venue.Tags[index];
            var width = VenueChips.Measure(tag, scale);
            if (cursor + width > right)
            {
                DrawPlusChip(drawList, new Vector2(cursor, top), venue.Tags.Count - index, scale);
                return;
            }

            VenueChips.Draw(drawList, new Vector2(cursor, top), tag, scale);
            cursor += width + gap;
        }
    }

    private static void DrawPlusChip(ImDrawListPtr drawList, Vector2 position, int remaining, float scale)
    {
        var label = $"+{remaining}";
        var textSize = Typography.Measure(label, TextStyles.Caption1);
        var width = textSize.X + 12f * scale;
        var height = VenueChips.Height(scale);
        var min = position;
        var max = new Vector2(position.X + width, position.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        Typography.Draw(new Vector2(min.X + (width - textSize.X) * 0.5f, min.Y + (height - textSize.Y) * 0.5f), label,
            new Vector4(1f, 1f, 1f, 0.72f), TextStyles.Caption1);
    }
}
