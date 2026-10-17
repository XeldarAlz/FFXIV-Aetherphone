using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Muster;

internal struct MusterPoster
{
    public int Category;
    public string Eyebrow;
    public string Title;
    public bool TitleMuted;
    public string Status;
    public bool Live;
    public bool Going;
    public string HostName;
    public string HostWorld;
    public string HostFrameId;
    public string Identity;
    public string Place;
    public string Count;
    public bool Full;
    public bool OtherDataCenter;
    public bool ShowProgress;
    public float Progress;
}

internal static class MusterArt
{
    public const float CardGap = 12f;
    public const float SectionGap = 22f;
    public const float BottomPad = 28f;
    public const float PillHeight = Button.LargeHeight;
    public const float RowHeight = 64f;
    public const float FieldRowHeight = 50f;
    public const float LineGap = 2f;
    public const float TextGap = 12f;

    private const float PosterPadX = 16f;
    private const float PosterPadTop = 14f;
    private const float PosterPadBottom = 14f;
    private const float EyebrowHeight = 24f;
    private const float EyebrowGlyph = 13f;
    private const float EyebrowGlyphGap = 7f;
    private const float TitleGap = 8f;
    private const float FooterGap = 12f;
    private const float HostRowHeight = 26f;
    private const float HostAvatarRadius = 12f;
    private const float PlaceGap = 4f;
    private const float FooterGlyph = 11f;
    private const float FooterGlyphGap = 6f;
    private const float WatermarkSize = 92f;
    private const float WatermarkInsetX = 44f;
    private const float WatermarkTop = 58f;
    private const float WatermarkAlpha = 0.12f;
    private const float SurfaceLighten = 0.14f;
    private const float SurfaceDarken = 0.24f;
    private const float RimAlpha = 0.10f;
    private const float CapsuleHeight = 24f;
    private const float CapsulePadX = 10f;
    private const float CapsuleFillAlpha = 0.26f;
    private const float CapsuleDotSpace = 14f;
    private const float CheckBadgeRadius = 12f;
    private const float CheckBadgeGap = 6f;
    private const float StateTileSize = 68f;
    private const float StateGlyphSize = 30f;
    private const float StateTitleGap = 18f;
    private const float StateHintGap = 6f;
    private const float StateMaxText = 280f;
    private const float StateTextInset = 48f;
    private const float StateActionGap = 22f;
    private const float StateActionWidth = 220f;
    private const float WashInset = 4f;
    private const float WashRadius = 16f;
    private const float PressedWash = 1.6f;
    private const float TileActiveAlpha = 0.16f;
    private const float TileGlyph = 17f;
    private const float TilePad = 12f;
    private const float CheckRadius = 9f;
    private const float LiveRingBase = 3.4f;
    private const float LiveRingGrowth = 6f;
    private const float LiveCoreRadius = 3f;
    private const float SkeletonAlpha = 0.06f;
    private const float ProgressGap = 12f;
    private const float ProgressHeight = 5f;
    private const float ProgressTrackAlpha = 0.22f;
    private const string Ellipsis = "…";

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Shade = new(0f, 0f, 0f, 1f);
    private static readonly TextStyle TitleStyle = TextStyles.Title3;

    public static readonly Vector4 LiveColor = AccentRing.Green;
    public static readonly Vector4 OnTheWayColor = AccentRing.Azure;
    public static readonly Vector4 LateColor = AccentRing.Orange;
    public static readonly Vector4 HereColor = AccentRing.Green;
    public static readonly Vector4 AskingColor = AccentRing.Violet;
    public static readonly Vector4 FullColor = AccentRing.Gold;

    public static Vector4 Tint(int category) =>
        category switch
        {
            MusterCategories.Roleplay => AccentRing.Violet,
            MusterCategories.Pve => AccentRing.Red,
            MusterCategories.Pvp => AccentRing.Orange,
            MusterCategories.HuntTrain => AccentRing.Gold,
            MusterCategories.TreasureHunt => AccentRing.Emerald,
            MusterCategories.DeepDungeon => AccentRing.Indigo,
            MusterCategories.Fishing => AccentRing.Teal,
            MusterCategories.GoldSaucer => AccentRing.Rose,
            MusterCategories.Gpose => AccentRing.Orchid,
            MusterCategories.Fates => AccentRing.Lime,
            MusterCategories.Other => AccentRing.Slate,
            _ => AccentRing.Cyan,
        };

    public static Vector4 Surface(int category) => IconTile.Surface(Tint(category));

    public static Vector4 StatusColor(int status) =>
        status switch
        {
            MusterStatuses.RunningLate => LateColor,
            MusterStatuses.Here => HereColor,
            MusterStatuses.WhereExactly => AskingColor,
            _ => OnTheWayColor,
        };

    public static FontAwesomeIcon StatusIcon(int status) =>
        status switch
        {
            MusterStatuses.RunningLate => FontAwesomeIcon.HourglassHalf,
            MusterStatuses.Here => FontAwesomeIcon.MapMarkerAlt,
            MusterStatuses.WhereExactly => FontAwesomeIcon.QuestionCircle,
            _ => FontAwesomeIcon.Walking,
        };

    public static void Reserve(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y - ImGui.GetStyle().ItemSpacing.Y)));
    }

    public static bool RowWash(ImDrawListPtr drawList, AppSkin ui, Rect row, float scale, bool enabled = true)
    {
        var hovered = enabled && UiInteract.Hover(row.Min, row.Max);
        if (!hovered)
        {
            return false;
        }

        var inset = WashInset * scale;
        var wash = ImGui.IsMouseDown(ImGuiMouseButton.Left)
            ? Palette.WithAlpha(ui.HoverTint, ui.HoverTint.W * PressedWash)
            : ui.HoverTint;
        Squircle.Fill(drawList, row.Min + new Vector2(inset, inset), row.Max - new Vector2(inset, inset),
            WashRadius * scale, ImGui.GetColorU32(wash));
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return true;
    }

    public static float PosterHeight(in MusterPoster poster, float width, int maxTitleLines, float scale)
    {
        var lines = TitleLines(poster.Title, width, scale, maxTitleLines);
        var height = (PosterPadTop + EyebrowHeight + TitleGap) * scale + lines * Typography.LineHeight(TitleStyle);
        if (poster.HostName.Length > 0 || poster.Count.Length > 0)
        {
            height += (FooterGap + HostRowHeight) * scale;
        }

        if (poster.Place.Length > 0)
        {
            height += PlaceGap * scale + Typography.LineHeight(TextStyles.Footnote);
        }

        if (poster.ShowProgress)
        {
            height += (ProgressGap + ProgressHeight) * scale;
        }

        return height + PosterPadBottom * scale;
    }

    public static bool Poster(ImDrawListPtr drawList, uint key, in MusterPoster poster, Vector2 min, float width,
        int maxTitleLines, PhoneTheme theme, RemoteImageCache images, LodestoneService lodestone, bool interactive,
        float scale)
    {
        var height = PosterHeight(in poster, width, maxTitleLines, scale);
        var rect = new Rect(min, new Vector2(min.X + width, min.Y + height));
        if (!ImGui.IsRectVisible(rect.Min, rect.Max))
        {
            return false;
        }

        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = interactive ? PressFx.Scale(key, pressed, Motion.PressScaleCard) : 1f;
        var lift = interactive ? PressFx.Toward(key + 1u, hovered ? 1f : 0f) : 0f;
        var half = rect.Size * 0.5f * press * (1f + Motion.HoverLiftCard * lift);
        var cardMin = rect.Center - half;
        var cardMax = rect.Center + half;
        var radius = Metrics.Radius.Grouped * scale;
        var surface = Surface(poster.Category);
        Squircle.FillVerticalGradient(drawList, cardMin, cardMax, radius,
            ImGui.GetColorU32(Palette.Lighten(surface, SurfaceLighten) with { W = 1f }),
            ImGui.GetColorU32(Palette.Darken(surface, SurfaceDarken) with { W = 1f }));
        Squircle.Stroke(drawList, cardMin, cardMax, radius, ImGui.GetColorU32(White with { W = RimAlpha }),
            Metrics.Stroke.Hairline);
        drawList.PushClipRect(cardMin, cardMax, true);
        ProgressRing.CenterIcon(drawList,
            new Vector2(cardMax.X - WatermarkInsetX * scale, cardMin.Y + WatermarkTop * scale),
            MusterCategories.Icon(poster.Category), White with { W = WatermarkAlpha }, WatermarkSize * scale);
        drawList.PopClipRect();

        var left = cardMin.X + PosterPadX * scale;
        var right = cardMax.X - PosterPadX * scale;
        var eyebrowCenterY = cardMin.Y + (PosterPadTop + EyebrowHeight * 0.5f) * scale;
        var capsuleLeft = StatusCapsule(drawList, right, eyebrowCenterY, poster.Status, poster.Live, scale);
        if (poster.Going)
        {
            capsuleLeft = CheckBadge(drawList, new Vector2(capsuleLeft - (CheckBadgeGap + CheckBadgeRadius) * scale,
                eyebrowCenterY), scale) - CheckBadgeGap * scale;
        }

        Eyebrow(drawList, left, eyebrowCenterY, capsuleLeft - Metrics.Space.Sm * scale, poster.Category,
            poster.Eyebrow, scale);

        var titleTop = cardMin.Y + (PosterPadTop + EyebrowHeight + TitleGap) * scale;
        var titleBottom = DrawTitle(drawList, poster.Title, left, titleTop, width - PosterPadX * 2f * scale,
            maxTitleLines, poster.TitleMuted ? White with { W = 0.55f } : White, scale);
        var cursorY = titleBottom;
        if (poster.HostName.Length > 0 || poster.Count.Length > 0)
        {
            cursorY += FooterGap * scale;
            DrawFooter(drawList, in poster, left, right, cursorY + HostRowHeight * 0.5f * scale, theme, images,
                lodestone, scale);
            cursorY += HostRowHeight * scale;
        }

        if (poster.Place.Length > 0)
        {
            DrawPlace(drawList, in poster, left, right, cursorY + PlaceGap * scale, scale);
            cursorY += PlaceGap * scale + Typography.LineHeight(TextStyles.Footnote);
        }

        if (poster.ShowProgress)
        {
            DrawProgress(drawList, left, right, cursorY + ProgressGap * scale, poster.Progress, scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!interactive || !UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            return false;
        }

        UiFeedback.Play(UiSound.Tap);
        return true;
    }

    private static int TitleLines(string title, float width, float scale, int maxLines)
    {
        var lines = Typography.WrapText(title, TitleStyle, MathF.Max(1f, width - PosterPadX * 2f * scale));
        return maxLines > 0 ? Math.Min(lines.Length, maxLines) : lines.Length;
    }

    private static float DrawTitle(ImDrawListPtr drawList, string title, float left, float top, float maxWidth,
        int maxLines, Vector4 ink, float scale)
    {
        var lines = Typography.WrapText(title, TitleStyle, MathF.Max(1f, maxWidth));
        var count = maxLines > 0 ? Math.Min(lines.Length, maxLines) : lines.Length;
        var lineHeight = Typography.LineHeight(TitleStyle);
        for (var index = 0; index < count; index++)
        {
            var lineTop = top + index * lineHeight;
            var clipped = index == count - 1 && count < lines.Length;
            if (!clipped)
            {
                Typography.Draw(drawList, new Vector2(left, lineTop), lines[index], ink, TitleStyle);
                continue;
            }

            var ellipsisWidth = Typography.Measure(Ellipsis, TitleStyle).X;
            var fitted = Typography.FitText(lines[index], MathF.Max(1f, maxWidth - ellipsisWidth), TitleStyle);
            Typography.Draw(drawList, new Vector2(left, lineTop), fitted, ink, TitleStyle);
            if (!string.Equals(fitted, lines[index], StringComparison.Ordinal))
            {
                continue;
            }

            var fittedWidth = Typography.Measure(fitted, TitleStyle).X;
            Typography.Draw(drawList, new Vector2(left + fittedWidth, lineTop), Ellipsis, ink, TitleStyle);
        }

        return top + count * lineHeight;
    }

    private static void Eyebrow(ImDrawListPtr drawList, float left, float centerY, float right, int category,
        string label, float scale)
    {
        var glyph = EyebrowGlyph * scale;
        var ink = White with { W = 0.86f };
        ProgressRing.CenterIcon(drawList, new Vector2(left + glyph * 0.5f, centerY), MusterCategories.Icon(category),
            ink, glyph);
        var textLeft = left + glyph + EyebrowGlyphGap * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, right - textLeft), TextStyles.FootnoteEmphasized);
        var height = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - height * 0.5f), fitted, ink,
            TextStyles.FootnoteEmphasized);
    }

    public static float StatusCapsule(ImDrawListPtr drawList, float right, float centerY, string label, bool live,
        float scale)
    {
        if (label.Length == 0)
        {
            return right;
        }

        var textSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var height = CapsuleHeight * scale;
        var dotSpace = live ? CapsuleDotSpace * scale : 0f;
        var width = textSize.X + dotSpace + CapsulePadX * 2f * scale;
        var min = new Vector2(right - width, centerY - height * 0.5f);
        var max = new Vector2(right, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Shade with { W = CapsuleFillAlpha }));
        if (live)
        {
            LiveDot(drawList, new Vector2(min.X + CapsulePadX * scale + 3f * scale, centerY), scale);
        }

        Typography.Draw(drawList, new Vector2(min.X + CapsulePadX * scale + dotSpace, centerY - textSize.Y * 0.5f),
            label, White, TextStyles.FootnoteEmphasized);
        return min.X;
    }

    private static float CheckBadge(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var radius = CheckBadgeRadius * scale;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(White), 24);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Check, Shade with { W = 0.78f }, radius);
        return center.X - radius;
    }

    private static void DrawFooter(ImDrawListPtr drawList, in MusterPoster poster, float left, float right,
        float centerY, PhoneTheme theme, RemoteImageCache images, LodestoneService lodestone, float scale)
    {
        var countRight = right;
        if (poster.Count.Length > 0)
        {
            var countStyle = TextStyles.FootnoteEmphasized;
            var countSize = Typography.Measure(poster.Count, countStyle);
            var countInk = poster.Full ? FullColor : White with { W = 0.92f };
            Typography.Draw(drawList, new Vector2(right - countSize.X, centerY - countSize.Y * 0.5f), poster.Count,
                countInk, countStyle);
            var glyph = FooterGlyph * scale;
            ProgressRing.CenterIcon(drawList,
                new Vector2(right - countSize.X - FooterGlyphGap * scale - glyph * 0.5f, centerY),
                FontAwesomeIcon.Users, countInk, glyph);
            countRight = right - countSize.X - FooterGlyphGap * scale - glyph - Metrics.Space.Md * scale;
        }

        if (poster.HostName.Length == 0)
        {
            return;
        }

        var radius = HostAvatarRadius * scale;
        var avatarCenter = new Vector2(left + radius, centerY);
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, poster.HostName, poster.HostWorld, null, images,
            lodestone, 0.8f, 24, 1f, Frames.Of(poster.HostFrameId));
        var textLeft = avatarCenter.X + radius + Metrics.Space.Sm * scale;
        var fitted = Typography.FitText(poster.Identity, MathF.Max(1f, countRight - textLeft), TextStyles.Subheadline);
        var height = Typography.LineHeight(TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - height * 0.5f), fitted, White with { W = 0.92f },
            TextStyles.Subheadline);
    }

    private static void DrawPlace(ImDrawListPtr drawList, in MusterPoster poster, float left, float right, float top,
        float scale)
    {
        var height = Typography.LineHeight(TextStyles.Footnote);
        var centerY = top + height * 0.5f;
        var ink = White with { W = 0.72f };
        var placeRight = right;
        if (poster.OtherDataCenter)
        {
            var glyph = FooterGlyph * scale;
            var label = Loc.T(L.Muster.DcTravel);
            var labelWidth = Typography.Measure(label, TextStyles.Footnote).X;
            Typography.Draw(drawList, new Vector2(right - labelWidth, top), label, ink, TextStyles.Footnote);
            var glyphCenterX = right - labelWidth - FooterGlyphGap * scale - glyph * 0.5f;
            ProgressRing.CenterIcon(drawList, new Vector2(glyphCenterX, centerY), FontAwesomeIcon.Plane, ink, glyph);
            placeRight = glyphCenterX - glyph * 0.5f - Metrics.Space.Md * scale;
        }

        var pin = FooterGlyph * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(left + pin * 0.5f, centerY), FontAwesomeIcon.MapMarkerAlt, ink,
            pin);
        var textLeft = left + pin + FooterGlyphGap * scale;
        var fitted = Typography.FitText(poster.Place, MathF.Max(1f, placeRight - textLeft), TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, top), fitted, ink, TextStyles.Footnote);
    }

    private static void DrawProgress(ImDrawListPtr drawList, float left, float right, float top, float progress,
        float scale)
    {
        var height = ProgressHeight * scale;
        var radius = height * 0.5f;
        var min = new Vector2(left, top);
        var max = new Vector2(right, top + height);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(White with { W = ProgressTrackAlpha }), radius);
        var fill = MathF.Max(height, (max.X - min.X) * Math.Clamp(progress, 0f, 1f));
        drawList.AddRectFilled(min, new Vector2(min.X + fill, max.Y), ImGui.GetColorU32(White), radius);
    }

    public static void LiveDot(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var phase = Pulse.Phase(Pulse.Calm);
        LiveRing(drawList, center, phase, scale);
        LiveRing(drawList, center, phase < 0.5f ? phase + 0.5f : phase - 0.5f, scale);
        var core = LiveCoreRadius + 0.5f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircleFilled(center, core * scale, ImGui.GetColorU32(LiveColor), 20);
    }

    private static void LiveRing(ImDrawListPtr drawList, Vector2 center, float phase, float scale)
    {
        var radius = (LiveRingBase + LiveRingGrowth * phase) * scale;
        drawList.AddCircle(center, radius, ImGui.GetColorU32(LiveColor with { W = 0.55f * (1f - phase) }), 20,
            Metrics.Stroke.Thin * scale);
    }

    public static float StateScreen(ImDrawListPtr drawList, AppSkin ui, float centerX, float top, float width,
        FontAwesomeIcon icon, string title, string hint, float scale)
    {
        var tileSize = StateTileSize * scale;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, top);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, top + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, StateGlyphSize * scale);
        var maxWidth = MathF.Min(width - StateTextInset * scale, StateMaxText * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + StateTitleGap * scale), maxWidth);
        if (hint.Length == 0)
        {
            return titleBottom;
        }

        return Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
            new Vector2(centerX, titleBottom + StateHintGap * scale), maxWidth);
    }

    public static bool StateAction(AppSkin ui, float centerX, float top, float width, string label, float scale,
        out float bottom)
    {
        var actionWidth = MathF.Min(StateActionWidth * scale, width);
        var actionTop = top + StateActionGap * scale;
        var rect = new Rect(new Vector2(centerX - actionWidth * 0.5f, actionTop),
            new Vector2(centerX + actionWidth * 0.5f, actionTop + Button.LargeHeight * scale));
        bottom = rect.Max.Y;
        return Action(ui, rect, label, true);
    }

    public static bool Action(AppSkin ui, Rect rect, string label, bool enabled)
    {
        if (!Button.Draw(rect, label, ui.Ink, enabled: enabled))
        {
            return false;
        }

        UiFeedback.Play(UiSound.Tap);
        return true;
    }

    public static bool ActionTile(ImDrawListPtr drawList, AppSkin ui, uint key, Rect rect, FontAwesomeIcon icon,
        string label, Vector4 tint, bool active, bool enabled, float scale)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(key, pressed, Motion.PressScaleCard);
        var amount = PressFx.Toward(key + 1u, active ? 1f : 0f);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var rest = Surfaces.Fill(ui.TitleInk, hovered ? FillLevel.Secondary : FillLevel.Tertiary);
        var lit = Palette.WithAlpha(tint, TileActiveAlpha * (hovered ? 1.3f : 1f));
        Squircle.Fill(drawList, min, max, Metrics.Radius.Card * scale,
            ImGui.GetColorU32(Vector4.Lerp(rest, lit, amount)));
        if (amount > 0.01f)
        {
            Squircle.Stroke(drawList, min, max, Metrics.Radius.Card * scale,
                ImGui.GetColorU32(tint with { W = 0.55f * amount }), Metrics.Stroke.Thin * scale);
        }

        var alpha = enabled ? 1f : 0.45f;
        var glyph = TileGlyph * scale;
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var blockHeight = glyph + Metrics.Space.Sm * scale + labelHeight;
        var top = (min.Y + max.Y) * 0.5f - blockHeight * 0.5f;
        var glyphInk = Vector4.Lerp(ui.TitleInk, tint, MathF.Max(amount, 0.65f));
        ProgressRing.CenterIcon(drawList, new Vector2((min.X + max.X) * 0.5f, top + glyph * 0.5f), icon,
            glyphInk with { W = glyphInk.W * alpha }, glyph);
        var fitted = Typography.FitText(label, MathF.Max(1f, max.X - min.X - TilePad * scale),
            TextStyles.FootnoteEmphasized);
        Typography.DrawCentered(drawList,
            new Vector2((min.X + max.X) * 0.5f, top + glyph + Metrics.Space.Sm * scale + labelHeight * 0.5f), fitted,
            ui.TitleInk with { W = ui.TitleInk.W * alpha }, TextStyles.FootnoteEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            return false;
        }

        UiFeedback.Play(UiSound.Tap);
        return true;
    }

    public static bool ToggleTile(ImDrawListPtr drawList, AppSkin ui, uint key, Rect rect, FontAwesomeIcon icon,
        string label, bool on, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(key, pressed, Motion.PressScaleCard);
        var fill = PressFx.Toward(key + 1u, on ? 1f : 0f);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = Metrics.Radius.Card * scale;
        var rest = Surfaces.Fill(ui.TitleInk, hovered ? FillLevel.Secondary : FillLevel.Tertiary);
        var active = Palette.WithAlpha(ui.Accent, TileActiveAlpha * (hovered ? 1.3f : 1f));
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Vector4.Lerp(rest, active, fill)));
        var pad = TilePad * scale;
        var glyph = TileGlyph * scale;
        var ink = Vector4.Lerp(ui.MutedInk, ui.Accent, fill);
        ProgressRing.CenterIcon(drawList, new Vector2(min.X + pad + glyph * 0.5f, min.Y + pad + glyph * 0.5f), icon,
            ink, glyph);
        var checkRadius = CheckRadius * scale;
        var checkCenter = new Vector2(max.X - pad - checkRadius, min.Y + pad + glyph * 0.5f);
        if (fill > 0.01f)
        {
            drawList.AddCircleFilled(checkCenter, checkRadius, ImGui.GetColorU32(ui.Accent with { W = fill }), 24);
            ProgressRing.CenterIcon(drawList, checkCenter, FontAwesomeIcon.Check, AccentRing.Ink with { W = fill },
                checkRadius);
        }

        drawList.AddCircle(checkCenter, checkRadius, ImGui.GetColorU32(ui.MutedInk with { W = 1f - fill }), 24,
            Metrics.Stroke.Thin * scale);
        var labelWidth = max.X - min.X - pad * 2f;
        var labelHeight = Typography.MeasureWrappedBlock(label, TextStyles.FootnoteEmphasized, labelWidth).Y;
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, max.Y - pad * 0.75f - labelHeight), label, ui.TitleInk,
            TextStyles.FootnoteEmphasized, labelWidth);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            return false;
        }

        UiFeedback.Play(on ? UiSound.ToggleOff : UiSound.ToggleOn);
        return true;
    }

    public static void Skeleton(ImDrawListPtr drawList, AppSkin ui, Vector2 min, Vector2 max, float scale)
    {
        var alpha = SkeletonAlpha * (0.6f + 0.4f * Pulse.Wave(Pulse.Calm));
        Squircle.Fill(drawList, min, max, Metrics.Radius.Widget * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, alpha)));
    }

    public static void Labels(ImDrawListPtr drawList, float left, float right, float centerY, string title,
        string subtitle, Vector4 titleInk, Vector4 subtitleInk, float scale)
    {
        var width = MathF.Max(1f, right - left);
        var fittedTitle = Typography.FitText(title, width, TextStyles.Headline);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        if (subtitle.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(left, centerY - titleHeight * 0.5f), fittedTitle, titleInk,
                TextStyles.Headline);
            return;
        }

        var fittedSubtitle = Typography.FitText(subtitle, width, TextStyles.Footnote);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = centerY - (titleHeight + LineGap * scale + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), fittedTitle, titleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, top + titleHeight + LineGap * scale), fittedSubtitle,
            subtitleInk, TextStyles.Footnote);
    }

    public static void Chevron(ImDrawListPtr drawList, AppSkin ui, float right, float centerY, float scale)
    {
        var size = FooterGlyph * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(right - size * 0.5f, centerY), FontAwesomeIcon.ChevronRight,
            Palette.WithAlpha(ui.MutedInk, ui.MutedInk.W * 0.7f), size);
    }
}
