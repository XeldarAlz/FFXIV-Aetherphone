using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Muster;

internal static class MusterCard
{
    public const float Gap = 12f;
    private const float Pad = 14f;
    private const float AvatarRadius = 19f;
    private const float IdentityRowHeight = 50f;
    private const float MetaRowHeight = 28f;
    private const float DescriptionGap = 9f;
    private const int MaxDescriptionLines = 2;

    public static readonly Vector4 LiveGreen = new(0.24f, 0.82f, 0.44f, 1f);
    private static readonly Vector4 CapacityAmber = new(0.98f, 0.72f, 0.30f, 1f);
    private static readonly Vector4 CardWhite = new(1f, 1f, 1f, 1f);

    public static float Height(MusterDto muster, float width, float scale)
    {
        var lines = DescriptionLines(muster, width, scale, out var lineHeight);
        var descriptionHeight = lines > 0 ? lines * lineHeight + DescriptionGap * scale : 0f;
        return (Pad * 2f + IdentityRowHeight + MetaRowHeight) * scale + descriptionHeight;
    }

    public static bool Draw(Rect card, MusterDto muster, RemoteImageCache images, LodestoneService lodestone,
        PhoneTheme theme, AppSkin ui, long nowUnix, int currentDataCenterId)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Card * scale * 1.15f;
        var palette = ui.Palette;
        var hovered = UiInteract.Hover(card.Min, card.Max);
        var live = muster.StartsAtUnix <= nowUnix;
        DrawSurface(drawList, card, rounding, palette, hovered, live, scale);

        var pad = Pad * scale;
        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var rowTop = card.Min.Y + pad;
        var avatarRadius = AvatarRadius * scale;
        var avatarCenter = new Vector2(left + avatarRadius, rowTop + IdentityRowHeight * scale * 0.5f);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, MusterText.HostLabel(muster), muster.HostWorld,
            null, images, lodestone, 1.05f, 32);
        drawList.AddCircle(avatarCenter, avatarRadius + 1.5f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(palette.Accent, live ? 0.45f : 0.22f)), 32, 1.4f * scale);

        var status = live
            ? Loc.T(L.Common.Live)
            : Loc.T(L.Muster.StartsIn, MusterText.Span(muster.StartsAtUnix - nowUnix));
        var pillLeft = DrawStatusPill(drawList, right, rowTop + 13f * scale, live, status,
            live ? LiveGreen : palette.Accent, scale);

        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var name = Typography.FitText(MusterText.Identity(muster), pillLeft - 10f * scale - textLeft,
            TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(textLeft, rowTop + 2f * scale), name, palette.TitleInk,
            TextStyles.Title3);

        var chipCenterY = rowTop + 36f * scale;
        var chipRight = right;
        if (currentDataCenterId != 0 && muster.DataCenterId != 0 && muster.DataCenterId != currentDataCenterId)
        {
            chipRight = DrawDataCenterChip(drawList, right, chipCenterY, palette.Accent, scale) - 8f * scale;
        }

        DrawCategoryChip(drawList, textLeft, chipCenterY, muster, chipRight - textLeft, palette.Accent, scale);

        var descriptionTop = rowTop + IdentityRowHeight * scale;
        DrawDescription(drawList, muster, left, descriptionTop, card.Width, scale, palette.BodyInk);

        var metaTop = card.Max.Y - pad - MetaRowHeight * scale;
        DrawMetaRow(drawList, muster, left, right, metaTop + MetaRowHeight * scale * 0.5f, palette, scale);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private static void DrawSurface(ImDrawListPtr drawList, Rect card, float rounding, in AppPalette palette,
        bool hovered, bool live, float scale)
    {
        Elevation.Card(drawList, card.Min, card.Max, rounding, scale, hovered ? 1f : 0.65f);
        var breath = live ? 0.012f * Pulse.Wave(Pulse.Breath) : 0f;
        var top = Palette.WithAlpha(Palette.Mix(CardWhite, palette.Accent, 0.5f),
            (hovered ? 0.17f : 0.12f) + breath);
        var bottom = Palette.WithAlpha(CardWhite, hovered ? 0.05f : 0.03f);
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(top),
            ImGui.GetColorU32(bottom));
        var strokeAlpha = hovered ? 0.34f : live ? 0.18f + 0.14f * Pulse.Wave(Pulse.Calm) : 0.16f;
        Squircle.Stroke(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(live ? LiveGreen : palette.Accent, strokeAlpha)), 1f * scale);
    }

    private static float DrawStatusPill(ImDrawListPtr drawList, float right, float centerY, bool live, string label,
        Vector4 tint, float scale)
    {
        var textSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var height = 22f * scale;
        var dotSpace = live ? 15f * scale : 0f;
        var width = textSize.X + dotSpace + 20f * scale;
        var min = new Vector2(right - width, centerY - height * 0.5f);
        var max = new Vector2(right, centerY + height * 0.5f);
        var breath = live ? 0.07f * Pulse.Wave(Pulse.Calm) : 0f;
        Squircle.Fill(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.15f + breath)));
        Squircle.Stroke(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.34f + breath)), 1f * scale);
        if (live)
        {
            DrawLiveDot(drawList, new Vector2(min.X + 13f * scale, centerY), scale);
        }

        Typography.Draw(drawList, new Vector2(min.X + 10f * scale + dotSpace, centerY - textSize.Y * 0.5f), label,
            tint, TextStyles.FootnoteEmphasized);
        return min.X;
    }

    public static void DrawLiveDot(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var phase = Pulse.Phase(Pulse.Calm);
        DrawLiveRing(drawList, center, phase, scale);
        DrawLiveRing(drawList, center, phase < 0.5f ? phase + 0.5f : phase - 0.5f, scale);
        var core = 3f + 0.5f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircleFilled(center, core * scale, ImGui.GetColorU32(LiveGreen), 20);
    }

    private static void DrawLiveRing(ImDrawListPtr drawList, Vector2 center, float phase, float scale)
    {
        var radius = (3.4f + 6f * phase) * scale;
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(LiveGreen, 0.55f * (1f - phase))),
            20, 1.4f * scale);
    }

    private static void DrawCategoryChip(ImDrawListPtr drawList, float left, float centerY, MusterDto muster,
        float maxWidth, Vector4 accent, float scale)
    {
        var label = Loc.T(MusterCategories.Label(muster.Category));
        var height = 22f * scale;
        var iconSpace = 16f * scale;
        var fitted = Typography.FitText(label, maxWidth - iconSpace - 22f * scale,
            TextStyles.SubheadlineEmphasized);
        var textSize = Typography.Measure(fitted, TextStyles.SubheadlineEmphasized);
        var min = new Vector2(left, centerY - height * 0.5f);
        var max = new Vector2(left + textSize.X + iconSpace + 22f * scale, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.16f)));
        AppSkin.Icon(drawList, new Vector2(min.X + 13f * scale, centerY),
            MusterCategories.Icon(muster.Category).ToIconString(), accent, 0.6f);
        Typography.Draw(drawList, new Vector2(min.X + 11f * scale + iconSpace, centerY - textSize.Y * 0.5f), fitted,
            accent, TextStyles.SubheadlineEmphasized);
    }

    private static float DrawDataCenterChip(ImDrawListPtr drawList, float right, float centerY, Vector4 accent,
        float scale)
    {
        var label = Loc.T(L.Muster.DcTravel);
        var textSize = Typography.Measure(label, TextStyles.Caption1);
        var chipHeight = 20f * scale;
        var iconSpace = 14f * scale;
        var min = new Vector2(right - textSize.X - iconSpace - 18f * scale, centerY - chipHeight * 0.5f);
        var max = new Vector2(right, centerY + chipHeight * 0.5f);
        Squircle.Fill(drawList, min, max, chipHeight * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(accent, 0.14f)));
        AppSkin.Icon(drawList, new Vector2(min.X + 11f * scale, centerY), FontAwesomeIcon.Plane.ToIconString(),
            Palette.WithAlpha(accent, 0.9f), 0.52f);
        Typography.Draw(drawList, new Vector2(min.X + 9f * scale + iconSpace, centerY - textSize.Y * 0.5f), label,
            Palette.WithAlpha(accent, 0.9f), TextStyles.Caption1);
        return min.X;
    }

    private static void DrawDescription(ImDrawListPtr drawList, MusterDto muster, float left, float top, float width,
        float scale, Vector4 ink)
    {
        if (muster.Description.Length == 0)
        {
            return;
        }

        var textWidth = width - Pad * 2f * scale;
        using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
        {
            Plugin.Fonts.NoticeText(muster.Description);
            var lines = Typography.WrapCurrent(muster.Description, textWidth);
            var count = Math.Min(lines.Length, MaxDescriptionLines);
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var packed = ImGui.GetColorU32(ink);
            for (var index = 0; index < count; index++)
            {
                drawList.AddText(font, fontSize, new Vector2(left, top + index * lineHeight), packed, lines[index]);
            }
        }
    }

    private static void DrawMetaRow(ImDrawListPtr drawList, MusterDto muster, float left, float right, float centerY,
        in AppPalette palette, float scale)
    {
        var going = Loc.T(L.Muster.GoingCount, muster.RsvpCount);
        var goingSize = Typography.Measure(going, TextStyles.SubheadlineEmphasized);
        var goingLeft = right - goingSize.X;
        Typography.Draw(drawList, new Vector2(goingLeft, centerY - goingSize.Y * 0.5f), going,
            muster.RsvpCount > 0 ? palette.TitleInk : palette.MutedInk, TextStyles.SubheadlineEmphasized);
        AppSkin.Icon(drawList, new Vector2(goingLeft - 11f * scale, centerY), FontAwesomeIcon.Users.ToIconString(),
            Palette.WithAlpha(palette.MutedInk, 0.85f), 0.58f);
        var metaRight = goingLeft - 21f * scale;
        if (muster.MaxAttendees > 0 && muster.RsvpCount >= muster.MaxAttendees)
        {
            var capacity = Loc.T(L.Muster.AtCapacity);
            var capacitySize = Typography.Measure(capacity, TextStyles.FootnoteEmphasized);
            metaRight -= capacitySize.X + 10f * scale;
            Typography.Draw(drawList, new Vector2(metaRight + 10f * scale, centerY - capacitySize.Y * 0.5f),
                capacity, CapacityAmber, TextStyles.FootnoteEmphasized);
        }

        var place = MusterText.Place(muster);
        if (place.Length == 0)
        {
            return;
        }

        AppSkin.Icon(drawList, new Vector2(left + 5f * scale, centerY), FontAwesomeIcon.MapMarkerAlt.ToIconString(),
            Palette.WithAlpha(palette.MutedInk, 0.85f), 0.58f);
        var placeLeft = left + 15f * scale;
        var placeSize = Typography.Measure(place, TextStyles.Subheadline);
        Marquee.DrawLeftAuto(drawList, "muster.card.place." + muster.Id, place, placeLeft,
            centerY - placeSize.Y * 0.5f, metaRight - placeLeft, TextStyles.Subheadline, palette.MutedInk);
    }

    private static int DescriptionLines(MusterDto muster, float width, float scale, out float lineHeight)
    {
        var textWidth = width - Pad * 2f * scale;
        using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
        {
            lineHeight = ImGui.GetTextLineHeightWithSpacing();
            if (muster.Description.Length == 0)
            {
                return 0;
            }

            Plugin.Fonts.NoticeText(muster.Description);
            var lines = Typography.WrapCurrent(muster.Description, textWidth);
            return Math.Min(lines.Length, MaxDescriptionLines);
        }
    }
}
