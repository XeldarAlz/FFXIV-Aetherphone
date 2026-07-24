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
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Muster;

internal static class MusterCard
{
    public const float Gap = 10f;
    private const float Pad = 12f;
    private const float AvatarRadius = 17f;
    private const float IdentityRowHeight = 40f;
    private const float CategoryRowHeight = 20f;
    private const float MetaRowHeight = 22f;
    private const float DescriptionGap = 6f;
    private const int MaxDescriptionLines = 3;

    public static readonly Vector4 LiveGreen = new(0.24f, 0.82f, 0.44f, 1f);
    private static readonly Vector4 CapacityAmber = new(0.98f, 0.72f, 0.30f, 1f);

    public static float Height(MusterDto muster, float width, float scale)
    {
        var lines = DescriptionLines(muster, width, scale, out var lineHeight);
        var descriptionHeight = lines > 0 ? lines * lineHeight + DescriptionGap * scale : 0f;
        return (Pad * 2f + IdentityRowHeight + CategoryRowHeight + MetaRowHeight) * scale + descriptionHeight;
    }

    public static bool Draw(Rect card, MusterDto muster, RemoteImageCache images, LodestoneService lodestone,
        PhoneTheme theme, AppSkin ui, long nowUnix, int currentDataCenterId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Lg * scale;
        var palette = ui.Palette;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var pad = Pad * scale;
        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var avatarRadius = AvatarRadius * scale;
        var avatarCenter = new Vector2(left + avatarRadius, card.Min.Y + pad + avatarRadius);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, muster.HostCharacter, muster.HostWorld,
            null, images, lodestone, 0.95f, 32);
        var textLeft = avatarCenter.X + avatarRadius + 10f * scale;
        var live = muster.StartsAtUnix <= nowUnix;
        var status = live
            ? Loc.T(L.Common.Live)
            : Loc.T(L.Muster.StartsIn, MusterText.Span(muster.StartsAtUnix - nowUnix));
        var statusSize = Typography.Measure(status, TextStyles.FootnoteEmphasized);
        var statusLeft = right - statusSize.X;
        var statusTop = card.Min.Y + pad + 2f * scale;
        Typography.Draw(drawList, new Vector2(statusLeft, statusTop), status, live ? LiveGreen : palette.Accent,
            TextStyles.FootnoteEmphasized);
        if (live)
        {
            DrawLiveDot(drawList, new Vector2(statusLeft - 10f * scale, statusTop + statusSize.Y * 0.5f), scale);
        }

        var nameWidth = statusLeft - (live ? 20f : 8f) * scale - textLeft;
        var name = Typography.FitText(MusterText.Identity(muster), nameWidth, TextStyles.Headline);
        var nameSize = Typography.Measure(name, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, avatarCenter.Y - nameSize.Y * 0.5f), name, palette.TitleInk,
            TextStyles.Headline);
        var categoryTop = card.Min.Y + (Pad + IdentityRowHeight) * scale;
        var categoryLabel = Loc.T(MusterCategories.Label(muster.Category));
        var categorySize = Typography.Measure(categoryLabel, TextStyles.FootnoteEmphasized);
        AppSkin.Icon(drawList, new Vector2(left + 7f * scale, categoryTop + categorySize.Y * 0.5f),
            MusterCategories.Icon(muster.Category).ToIconString(), palette.Accent, 0.62f);
        var categoryRight = right;
        if (currentDataCenterId != 0 && muster.DataCenterId != 0 && muster.DataCenterId != currentDataCenterId)
        {
            categoryRight = DrawDataCenterChip(drawList, right, categoryTop + categorySize.Y * 0.5f, palette.Accent,
                scale) - 8f * scale;
        }

        var fittedCategory = Typography.FitText(categoryLabel, categoryRight - (left + 19f * scale),
            TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(left + 19f * scale, categoryTop), fittedCategory, palette.Accent,
            TextStyles.FootnoteEmphasized);
        var descriptionTop = categoryTop + CategoryRowHeight * scale;
        DrawDescription(drawList, muster, left, descriptionTop, card.Width, scale, palette.BodyInk);
        DrawMetaRow(drawList, muster, left, right, card.Max.Y - pad - MetaRowHeight * scale + 4f * scale, palette,
            scale);
        var hovered = UiInteract.Hover(card.Min, card.Max);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, card.Min, card.Max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private static float DrawDataCenterChip(ImDrawListPtr drawList, float right, float centerY, Vector4 accent,
        float scale)
    {
        var label = Loc.T(L.Muster.DcTravel);
        var textSize = Typography.Measure(label, TextStyles.Caption1);
        var chipHeight = 18f * scale;
        var chipWidth = textSize.X + 14f * scale;
        var min = new Vector2(right - chipWidth, centerY - chipHeight * 0.5f);
        var max = new Vector2(right, centerY + chipHeight * 0.5f);
        Squircle.Fill(drawList, min, max, chipHeight * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.16f)));
        Typography.Draw(drawList, new Vector2(min.X + 7f * scale, centerY - textSize.Y * 0.5f), label,
            Palette.WithAlpha(accent, 0.85f), TextStyles.Caption1);
        return min.X;
    }

    private static void DrawLiveDot(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var pulse = 0.55f + 0.45f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircle(center, 5.2f * scale, ImGui.GetColorU32(Palette.WithAlpha(LiveGreen, 0.40f * pulse)), 20,
            1.3f * scale);
        drawList.AddCircleFilled(center, 3.1f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(LiveGreen, 0.65f + 0.35f * pulse)), 20);
    }

    private static void DrawDescription(ImDrawListPtr drawList, MusterDto muster, float left, float top, float width,
        float scale, Vector4 ink)
    {
        if (muster.Description.Length == 0)
        {
            return;
        }

        var textWidth = width - Pad * 2f * scale;
        using (Plugin.Fonts.Push(TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight))
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

    private static void DrawMetaRow(ImDrawListPtr drawList, MusterDto muster, float left, float right, float top,
        in AppPalette palette, float scale)
    {
        var going = Loc.T(L.Muster.GoingCount, muster.RsvpCount);
        var goingSize = Typography.Measure(going, TextStyles.Footnote);
        var goingLeft = right - goingSize.X;
        Typography.Draw(drawList, new Vector2(goingLeft, top), going, palette.MutedInk, TextStyles.Footnote);
        AppSkin.Icon(drawList, new Vector2(goingLeft - 10f * scale, top + goingSize.Y * 0.5f),
            FontAwesomeIcon.Users.ToIconString(), Palette.WithAlpha(palette.MutedInk, 0.8f), 0.58f);
        var metaRight = goingLeft - 18f * scale;
        if (muster.MaxAttendees > 0 && muster.RsvpCount >= muster.MaxAttendees)
        {
            var capacity = Loc.T(L.Muster.AtCapacity);
            var capacitySize = Typography.Measure(capacity, TextStyles.FootnoteEmphasized);
            metaRight -= capacitySize.X + 8f * scale;
            Typography.Draw(drawList, new Vector2(metaRight + 8f * scale, top), capacity, CapacityAmber,
                TextStyles.FootnoteEmphasized);
        }

        var place = MusterText.Place(muster);
        if (place.Length == 0)
        {
            return;
        }

        var fitted = Typography.FitText(place, metaRight - left, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left, top), fitted, palette.MutedInk, TextStyles.Footnote);
    }

    private static int DescriptionLines(MusterDto muster, float width, float scale, out float lineHeight)
    {
        var textWidth = width - Pad * 2f * scale;
        using (Plugin.Fonts.Push(TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight))
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
