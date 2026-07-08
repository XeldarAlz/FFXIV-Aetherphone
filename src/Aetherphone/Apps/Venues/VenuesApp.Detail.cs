using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private const float HeroAspect = 0.72f;
    private const float HeroMaxHeight = 280f;
    private const float ActionHeight = 50f;
    private const float InfoRowHeight = 50f;
    private static readonly Vector4 HeroInk = new(1f, 1f, 1f, 0.99f);
    private static readonly Vector4 HeroMutedInk = new(1f, 1f, 1f, 0.82f);
    private static readonly Vector4 HeroPillFill = new(0.04f, 0.04f, 0.06f, 0.62f);
    private static readonly Vector4 WhenTint = new(0.95f, 0.58f, 0.20f, 1f);
    private static readonly Vector4 WorldTint = new(0.35f, 0.55f, 0.95f, 1f);
    private static readonly Vector4 LocationTint = new(0.30f, 0.75f, 0.45f, 1f);
    private static readonly Vector4 HostTint = new(0.62f, 0.45f, 0.92f, 1f);

    private void DrawDetail(Rect area, VenueEvent venue)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            detailScrollY = ImGui.GetScrollY();
            DrawHero(body, venue);
            DrawActions(venue);
            DrawInfo(venue);
            DrawTagsSection(venue);
            DrawAbout(venue);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }

        DrawDetailHeader(area, venue, scale);
    }

    private void DrawDetailHeader(Rect area, VenueEvent venue, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var fadeStart = HeroHeight(area, scale) - 64f * scale;
        var titleAlpha = Math.Clamp((detailScrollY - fadeStart) / (44f * scale), 0f, 1f);
        if (titleAlpha > 0f)
        {
            var title = VenueText.Fit(venue.Title, area.Width * 0.6f, 1.15f, FontWeight.SemiBold);
            Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), title,
                Palette.WithAlpha(AppPalettes.Venues.TitleInk, titleAlpha), 1.15f, FontWeight.SemiBold);
        }

        var hitMin = area.Min;
        var hitMax = new Vector2(area.Min.X + 44f * scale, area.Min.Y + AppHeader.Height * scale);
        var backHovered = UiInteract.Hover(hitMin, hitMax);
        var backCenter = new Vector2(area.Min.X + 13f * scale, rowCenterY);
        if (BackButton.Draw("venues.back", backCenter, 15f * scale, ui.Accent, backHovered, scale))
        {
            router.Pop();
        }

        var favorite = IsFavorite(venue.Id);
        var starCenter = new Vector2(area.Max.X - 22f * scale, rowCenterY);
        if (ui.IconButton(starCenter, 14f * scale, FontAwesomeIcon.Star.ToIconString(),
                favorite ? ui.Accent : AppPalettes.Venues.BodyInk, AppSkin.Transparent, 0.9f))
        {
            ToggleFavorite(venue.Id);
        }
    }

    private static float HeroHeight(Rect area, float scale) =>
        MathF.Min(area.Width * HeroAspect, HeroMaxHeight * scale);

    private void DrawHero(Rect body, VenueEvent venue)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        var topPad = Metrics.Space.Sm * scale;
        var height = HeroHeight(body, scale);
        var rect = new Rect(new Vector2(body.Min.X, cursor.Y - topPad),
            new Vector2(body.Max.X, cursor.Y - topPad + height));
        VenueImage.Draw(drawList, rect, venue, media, http, artwork, 0f);
        var scrimTop = new Vector2(rect.Min.X, rect.Max.Y - height * 0.72f);
        drawList.AddRectFilledMultiColor(scrimTop, rect.Max, 0u, 0u,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.88f)), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.88f)));
        var pad = Metrics.Space.Lg * scale;
        if (venue.IsLive(DateTime.UtcNow))
        {
            DrawLivePill(drawList, new Vector2(rect.Min.X + pad, rect.Min.Y + 12f * scale), venue, scale);
        }

        DrawSourcePill(drawList, rect, SourceLabel(venue.Source), pad, scale);
        var textLeft = rect.Min.X + pad;
        var textWidth = rect.Width - pad * 2f;
        var baseY = rect.Max.Y - 16f * scale;
        var meta = BuildHeroMeta(venue);
        if (meta.Length > 0)
        {
            meta = VenueText.Fit(meta, textWidth, TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight);
            var metaSize = Typography.Measure(meta, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(textLeft, baseY - metaSize.Y), meta, HeroMutedInk,
                TextStyles.Subheadline);
            baseY -= metaSize.Y + 6f * scale;
        }

        var title = VenueText.Fit(venue.Title, textWidth, TextStyles.Title1.Scale, TextStyles.Title1.Weight);
        var titleSize = Typography.Measure(title, TextStyles.Title1);
        Typography.Draw(drawList, new Vector2(textLeft, baseY - titleSize.Y), title, HeroInk, TextStyles.Title1);
        ImGui.SetCursorScreenPos(cursor);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, height - topPad + Metrics.Space.Lg * scale));
    }

    private static string BuildHeroMeta(VenueEvent venue)
    {
        var place = venue.World;
        if (venue.DataCenter.Length > 0)
        {
            place = place.Length > 0 ? $"{place} · {venue.DataCenter}" : venue.DataCenter;
        }

        if (venue.LocationLine.Length > 0 && !string.Equals(venue.LocationLine, venue.World, StringComparison.Ordinal))
        {
            place = place.Length > 0 ? $"{place} · {venue.LocationLine}" : venue.LocationLine;
        }

        if (place.Length > 0)
        {
            return place;
        }

        return venue.Host.Length > 0 ? Loc.T(L.Venues.HostedBy, venue.Host) : string.Empty;
    }

    private static void DrawLivePill(ImDrawListPtr drawList, Vector2 min, VenueEvent venue, float scale)
    {
        var label = Loc.T(L.Common.Live);
        var ends = VenueFormat.EndsAt(venue);
        var until = ends.Length > 0 ? Loc.T(L.Venues.UntilTime, ends) : string.Empty;
        var labelSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var untilSize = until.Length > 0 ? Typography.Measure(until, TextStyles.Footnote) : Vector2.Zero;
        var untilAdvance = until.Length > 0 ? untilSize.X + 6f * scale : 0f;
        var height = 24f * scale;
        var dotAdvance = 14f * scale;
        var width = dotAdvance + labelSize.X + untilAdvance + 20f * scale;
        var max = new Vector2(min.X + width, min.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(HeroPillFill));
        var pulse = 0.55f + 0.45f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircleFilled(new Vector2(min.X + 12f * scale, min.Y + height * 0.5f), 3.4f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(VenueCard.LiveGreen, 0.6f + 0.4f * pulse)), 16);
        var textLeft = min.X + dotAdvance + 6f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + (height - labelSize.Y) * 0.5f), label, HeroInk,
            TextStyles.FootnoteEmphasized);
        if (until.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft + labelSize.X + 6f * scale,
                min.Y + (height - untilSize.Y) * 0.5f), until, HeroMutedInk, TextStyles.Footnote);
        }
    }

    private static void DrawSourcePill(ImDrawListPtr drawList, Rect hero, string label, float pad, float scale)
    {
        var textSize = Typography.Measure(label, TextStyles.Caption1);
        var height = 24f * scale;
        var width = textSize.X + 18f * scale;
        var min = new Vector2(hero.Max.X - pad - width, hero.Min.Y + 12f * scale);
        var max = new Vector2(min.X + width, min.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(HeroPillFill));
        Typography.Draw(drawList, new Vector2(min.X + (width - textSize.X) * 0.5f, min.Y + (height - textSize.Y) * 0.5f),
            label, HeroMutedInk, TextStyles.Caption1);
    }

    private void DrawActions(VenueEvent venue)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = ActionHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var hasTeleport = venue.CanTeleport;
        var hasDiscord = !string.IsNullOrEmpty(venue.DiscordUrl);
        var slots = 1 + (hasTeleport ? 1 : 0) + (hasDiscord ? 1 : 0);
        var slotWidth = (width - gap * (slots - 1)) / slots;
        var cursor = origin.X;
        if (hasTeleport)
        {
            var rect = new Rect(new Vector2(cursor, origin.Y), new Vector2(cursor + slotWidth, origin.Y + height));
            if (ActionPill(rect, FontAwesomeIcon.LocationArrow, Loc.T(L.Venues.Teleport), true))
            {
                if (lifestreamAvailable)
                {
                    LifestreamBridge.Travel(venue.TeleportCode!);
                }
                else
                {
                    ImGui.SetClipboardText($"/li {venue.TeleportCode}");
                }
            }

            if (!lifestreamAvailable)
            {
                HoverTooltip.Show(rect, Loc.T(L.Venues.NeedsLifestream), HoverLabelSide.Above);
            }

            cursor += slotWidth + gap;
        }

        var openRect = new Rect(new Vector2(cursor, origin.Y), new Vector2(cursor + slotWidth, origin.Y + height));
        if (ActionPill(openRect, FontAwesomeIcon.Globe, Loc.T(L.Venues.Open), !hasTeleport))
        {
            UrlActions.OpenInBrowser(venue.Url);
        }

        cursor += slotWidth + gap;
        if (hasDiscord)
        {
            var discordRect = new Rect(new Vector2(cursor, origin.Y),
                new Vector2(cursor + slotWidth, origin.Y + height));
            if (ActionPill(discordRect, FontAwesomeIcon.Headset, Loc.T(L.Venues.Discord), false))
            {
                UrlActions.OpenInBrowser(venue.DiscordUrl!);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Lg * scale));
    }

    private bool ActionPill(Rect rect, FontAwesomeIcon icon, string label, bool filled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = filled
            ? (hovered ? Palette.Mix(ui.Accent, new Vector4(1f, 1f, 1f, 1f), 0.12f) : ui.Accent)
            : (hovered ? new Vector4(1f, 1f, 1f, 0.16f) : ui.FieldSurface);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        var ink = filled ? new Vector4(1f, 1f, 1f, 1f) : ui.TitleInk;
        var textSize = Typography.Measure(label, 0.95f, FontWeight.SemiBold);
        var iconAdvance = 22f * scale;
        var left = rect.Center.X - (iconAdvance + textSize.X) * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left + 8f * scale, rect.Center.Y), icon.ToIconString(), ink, 0.85f);
        Typography.Draw(drawList, new Vector2(left + iconAdvance, rect.Center.Y - textSize.Y * 0.5f), label, ink,
            0.95f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawInfo(VenueEvent venue)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ui.SectionHeading(Loc.T(L.Venues.Details));
        var rows = 1;
        if (venue.World.Length > 0 || venue.DataCenter.Length > 0)
        {
            rows++;
        }

        if (venue.LocationLine.Length > 0)
        {
            rows++;
        }

        if (venue.Host.Length > 0)
        {
            rows++;
        }

        if (venue.AttendeeCount > 0)
        {
            rows++;
        }

        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = rows * InfoRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, origin + new Vector2(width, height), Metrics.Radius.Card * scale, elevated: true);
        var rowIndex = 0;
        InfoRow(drawList, origin, width, rowIndex++, rows, FontAwesomeIcon.Clock, WhenTint, Loc.T(L.Venues.When),
            VenueFormat.Range(venue));
        if (venue.World.Length > 0 || venue.DataCenter.Length > 0)
        {
            var place = venue.World.Length > 0
                ? venue.DataCenter.Length > 0 ? $"{venue.World} · {venue.DataCenter}" : venue.World
                : venue.DataCenter;
            var label = venue.World.Length > 0 ? Loc.T(L.Venues.World) : Loc.T(L.Venues.DataCenter);
            InfoRow(drawList, origin, width, rowIndex++, rows, FontAwesomeIcon.Globe, WorldTint, label, place);
        }

        if (venue.LocationLine.Length > 0)
        {
            InfoRow(drawList, origin, width, rowIndex++, rows, FontAwesomeIcon.MapMarkerAlt, LocationTint,
                Loc.T(L.Venues.Location), venue.LocationLine);
        }

        if (venue.Host.Length > 0)
        {
            InfoRow(drawList, origin, width, rowIndex++, rows, FontAwesomeIcon.User, HostTint, Loc.T(L.Venues.Host),
                venue.Host);
        }

        if (venue.AttendeeCount > 0)
        {
            InfoRow(drawList, origin, width, rowIndex, rows, FontAwesomeIcon.Users, ui.Accent,
                Loc.T(L.Venues.Attendees), venue.AttendeeCount.ToString(Loc.Culture));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Lg * scale));
    }

    private void InfoRow(ImDrawListPtr drawList, Vector2 cardOrigin, float cardWidth, int rowIndex, int rowCount,
        FontAwesomeIcon icon, Vector4 tint, string label, string value)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = InfoRowHeight * scale;
        var rowTop = cardOrigin.Y + rowIndex * rowHeight;
        var centerY = rowTop + rowHeight * 0.5f;
        var inset = 14f * scale;
        var tileSide = 28f * scale;
        var tileCenter = new Vector2(cardOrigin.X + inset + tileSide * 0.5f, centerY);
        IconTile.Draw(tileCenter, tileSide, IconTile.Surface(tint), icon);
        var labelLeft = tileCenter.X + tileSide * 0.5f + 12f * scale;
        var labelSize = Typography.Measure(label, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelSize.Y * 0.5f), label,
            AppPalettes.Venues.MutedInk, TextStyles.Subheadline);
        var valueRight = cardOrigin.X + cardWidth - inset;
        var valueWidth = valueRight - labelLeft - labelSize.X - 12f * scale;
        var fitted = VenueText.Fit(value, valueWidth, TextStyles.BodyEmphasized.Scale, TextStyles.BodyEmphasized.Weight);
        var valueSize = Typography.Measure(fitted, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(valueRight - valueSize.X, centerY - valueSize.Y * 0.5f), fitted,
            AppPalettes.Venues.TitleInk, TextStyles.BodyEmphasized);
        if (rowIndex >= rowCount - 1)
        {
            return;
        }

        drawList.AddLine(new Vector2(labelLeft, rowTop + rowHeight), new Vector2(valueRight, rowTop + rowHeight),
            ImGui.GetColorU32(ui.Palette.CardStroke), Metrics.Stroke.Hairline * scale);
    }

    private void DrawTagsSection(VenueEvent venue)
    {
        if (venue.Tags.Count == 0)
        {
            return;
        }

        ui.SectionHeading(Loc.T(L.Venues.Tags));
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var right = origin.X + width;
        var gap = Metrics.Space.Sm * scale;
        var lineHeight = VenueChips.LargeHeight(scale) + gap;
        var cursorX = origin.X;
        var cursorY = origin.Y;
        for (var index = 0; index < venue.Tags.Count; index++)
        {
            var tag = venue.Tags[index];
            var chipWidth = VenueChips.MeasureLarge(tag, scale);
            if (cursorX + chipWidth > right && cursorX > origin.X)
            {
                cursorX = origin.X;
                cursorY += lineHeight;
            }

            VenueChips.DrawLarge(drawList, new Vector2(cursorX, cursorY), tag, false, false, scale);
            cursorX += chipWidth + gap;
        }

        var totalHeight = cursorY - origin.Y + lineHeight;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, totalHeight + Metrics.Space.Sm * scale));
    }

    private void DrawAbout(VenueEvent venue)
    {
        if (venue.Description.Length == 0)
        {
            return;
        }

        ui.SectionHeading(Loc.T(L.Venues.About));
        var scale = ImGuiHelpers.GlobalScale;
        using (Plugin.Fonts.Push(1f))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Venues.BodyInk))
        {
            ImGui.PushTextWrapPos(0f);
            ImGui.TextUnformatted(venue.Description);
            ImGui.PopTextWrapPos();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private static string SourceLabel(VenueSource source) =>
        source switch
        {
            VenueSource.Partake => Loc.T(L.Venues.SourcePartake),
            _ => Loc.T(L.Venues.SourceFfxiv),
        };
}
