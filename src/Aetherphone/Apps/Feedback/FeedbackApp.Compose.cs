using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Feedback;

internal sealed partial class FeedbackApp
{
    private const string FieldId = "##feedbackBody";
    private const float SwitcherHeight = 72f;
    private const float SwitcherPad = 6f;
    private const float SwitcherIconSize = 30f;
    private const float SwitcherLabelGap = 6f;
    private const float SwitcherInactiveAlpha = 0.45f;
    private const float SwitcherHighlightAlpha = 0.16f;
    private const float PromptGap = 8f;
    private const float FieldCardHeight = 188f;
    private const float FieldPad = 12f;
    private const float CounterRowHeight = 22f;
    private const float CounterRadius = 8f;
    private const float CounterThickness = 2.4f;
    private const int CounterQuietThreshold = 150;
    private const int CounterNumberThreshold = 50;
    private const float AttachmentGap = 8f;
    private const float AttachmentMaxTile = 64f;
    private const float AttachmentRadius = 12f;
    private const float BadgeRadius = 9f;
    private const float BadgeInset = 4f;
    private const float BadgeGlyphScale = 0.55f;
    private const float AddGlyphScale = 0.85f;
    private const float DeviceRowHeight = 64f;
    private const float DeviceIconSize = 34f;
    private const float DisclosureRowHeight = 34f;
    private const float InfoRowHeight = 24f;
    private const float InfoDimAlpha = 0.45f;
    private const float SendBandPad = 12f;
    private const float SendPillHeight = 50f;
    private const float BannerPad = 10f;
    private const float BannerIconScale = 0.7f;
    private const float BannerFillAlpha = 0.16f;

    private static readonly Vector4 BadgeFill = new(0f, 0f, 0f, 0.62f);
    private static readonly Vector4 BadgeHover = new(0f, 0f, 0f, 0.88f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private Spring switcherSpring = new(-1f);
    private Spring disclosureSpring;
    private bool disclosureOpen;
    private string sentText = string.Empty;
    private int counterCacheValue = -1;
    private string counterCacheLabel = string.Empty;
    private int cooldownCacheValue = -1;
    private string cooldownCacheLabel = string.Empty;
    private int uploadCacheKey = -1;
    private string uploadCacheLabel = string.Empty;
    private int attachmentCountCache = -1;
    private string attachmentCountLabel = string.Empty;

    private void DrawCompose(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var bandHeight = SendBandHeight(area.Width, scale);
        var body = new Rect(navBar.Body.Min, new Vector2(navBar.Body.Max.X, area.Max.Y - bandHeight));
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawKindSwitcher(drawList, origin, width, scale);
            cursorY += SectionGap * scale;
            ref readonly var kind = ref FeedbackKinds.Of(draft.Category);
            cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(kind.Prompt), ui.TitleInk,
                TextStyles.Title3, width);
            cursorY += PromptGap * scale;
            cursorY = DrawField(drawList, new Vector2(origin.X, cursorY), width, in kind, scale);
            cursorY += SectionGap * scale;
            cursorY = DrawAttachments(drawList, new Vector2(origin.X, cursorY), width, scale);
            cursorY += SectionGap * scale;
            cursorY = DrawDeviceCard(drawList, new Vector2(origin.X, cursorY), width, scale);
            ReserveTo(origin, width, cursorY + BottomBreathing * scale);
        }

        DrawSendBand(new Rect(new Vector2(area.Min.X, area.Max.Y - bandHeight), area.Max), scale);
        composeButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.TrashAlt), Loc.T(L.Feedback.DiscardDraft));
        var buttons = draft.IsEmpty ? ReadOnlySpan<NavBarButton>.Empty : composeButtons.AsSpan();
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "feedback.compose.nav", Loc.T(L.Feedback.NewSection),
            NavBarStyle.From(ui), buttons, DisplayName, back);
        if (pressed == 0)
        {
            AskDiscard();
        }
    }

    private float DrawKindSwitcher(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + SwitcherHeight * scale);
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, min, max, radius, true);
        var kinds = FeedbackKinds.All;
        var pad = SwitcherPad * scale;
        var segmentWidth = (width - pad * 2f) / kinds.Length;
        var selected = (float)draft.Category;
        if (switcherSpring.Value < 0f)
        {
            switcherSpring.SnapTo(selected);
        }

        var slide = switcherSpring.Step(selected, Motion.PageSettle,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        var highlightMin = new Vector2(min.X + pad + segmentWidth * slide, min.Y + pad);
        var highlightMax = new Vector2(highlightMin.X + segmentWidth, max.Y - pad);
        ref readonly var active = ref FeedbackKinds.Of(draft.Category);
        Squircle.Fill(drawList, highlightMin, highlightMax, radius - pad,
            ImGui.GetColorU32(Palette.WithAlpha(active.Tint, SwitcherHighlightAlpha)));

        var iconSize = SwitcherIconSize * scale;
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var blockHeight = iconSize + SwitcherLabelGap * scale + labelHeight;
        var blockTop = (min.Y + max.Y - blockHeight) * 0.5f;
        for (var index = 0; index < kinds.Length; index++)
        {
            ref readonly var kind = ref kinds[index];
            var segmentMin = new Vector2(min.X + pad + segmentWidth * index, min.Y + pad);
            var segmentMax = new Vector2(segmentMin.X + segmentWidth, max.Y - pad);
            var isActive = kind.Category == draft.Category;
            var hovered = UiInteract.Hover(segmentMin, segmentMax);
            var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var grow = PressFx.Scale(kind.Title.Key, down, PressFx.ControlPressedScale);
            var centerX = (segmentMin.X + segmentMax.X) * 0.5f;
            var alpha = isActive || hovered ? 1f : SwitcherInactiveAlpha;
            FeedbackArt.CategoryTile(drawList, new Vector2(centerX, blockTop + iconSize * 0.5f), iconSize * grow,
                in kind, alpha);
            var label = Typography.FitText(Loc.T(kind.Title), segmentWidth - pad, TextStyles.FootnoteEmphasized);
            Typography.DrawCentered(drawList,
                new Vector2(centerX, blockTop + iconSize + SwitcherLabelGap * scale + labelHeight * 0.5f), label,
                isActive ? ui.TitleInk : ui.MutedInk, TextStyles.FootnoteEmphasized);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(segmentMin, segmentMax, hovered) && !isActive)
            {
                draft.Category = kind.Category;
            }
        }

        return max.Y;
    }

    private float DrawField(ImDrawListPtr drawList, Vector2 origin, float width, in FeedbackKind kind, float scale)
    {
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + FieldCardHeight * scale);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale, true);
        var pad = FieldPad * scale;
        var fieldMin = new Vector2(min.X + pad, min.Y + pad);
        var fieldSize = new Vector2(width - pad * 2f, max.Y - pad - CounterRowHeight * scale - fieldMin.Y);
        UiAnchors.Report("feedback.input", new Rect(fieldMin, fieldMin + fieldSize));

        var framePadding = ImGui.GetStyle().FramePadding;
        var before = draft.Text;
        ImGui.SetCursorScreenPos(fieldMin);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        {
            var wrapWidth = fieldSize.X - framePadding.X * 2f - Metrics.Space.Xxs * scale;
            SoftWrapField.Multiline(FieldId, ref draft.Text, FeedbackDraft.MaxLength, fieldSize, wrapWidth);
        }

        if (!ReferenceEquals(before, draft.Text) && !string.Equals(before, draft.Text, StringComparison.Ordinal))
        {
            sendFailure.Clear();
        }

        if (draft.Text.Length == 0)
        {
            Typography.DrawWrappedLeft(fieldMin + framePadding, Loc.T(kind.Placeholder), ui.MutedInk,
                TextStyles.Body, fieldSize.X - framePadding.X * 2f);
        }

        DrawCounter(drawList, new Vector2(max.X - pad, max.Y - pad - CounterRowHeight * scale * 0.5f), scale);
        return max.Y;
    }

    private void DrawCounter(ImDrawListPtr drawList, Vector2 rightCenter, float scale)
    {
        var remaining = FeedbackDraft.MaxLength - draft.Text.Length;
        if (remaining > CounterQuietThreshold)
        {
            return;
        }

        var tint = remaining <= 0 ? theme.Danger : AccentRing.Orange;
        var radius = CounterRadius * scale;
        var center = new Vector2(rightCenter.X - radius, rightCenter.Y);
        ProgressRing.Track(drawList, center, radius, CounterThickness * scale, ui.FieldSurface);
        ProgressRing.Fill(drawList, center, radius, CounterThickness * scale,
            (float)draft.Text.Length / FeedbackDraft.MaxLength, tint);
        if (remaining > CounterNumberThreshold)
        {
            return;
        }

        if (remaining != counterCacheValue)
        {
            counterCacheValue = remaining;
            counterCacheLabel = remaining.ToString(Loc.Culture);
        }

        var size = Typography.Measure(counterCacheLabel, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList,
            new Vector2(center.X - radius - Metrics.Space.Xs * scale - size.X, center.Y - size.Y * 0.5f),
            counterCacheLabel, tint, TextStyles.FootnoteEmphasized);
    }

    private float DrawAttachments(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var count = draft.Attachments.Count;
        var title = Loc.T(L.Feedback.Screenshots);
        var headerHeight = Typography.Measure(title, TextStyles.Headline).Y;
        Typography.Draw(drawList, origin, title, ui.TitleInk, TextStyles.Headline);
        if (count != attachmentCountCache)
        {
            attachmentCountCache = count;
            attachmentCountLabel = Loc.T(L.Common.PhotoCounter, count, FeedbackDraft.MaxAttachments);
        }

        var countSize = Typography.Measure(attachmentCountLabel, TextStyles.Footnote);
        Typography.Draw(drawList,
            new Vector2(origin.X + width - countSize.X, origin.Y + (headerHeight - countSize.Y) * 0.5f),
            attachmentCountLabel, ui.MutedInk, TextStyles.Footnote);

        var gap = AttachmentGap * scale;
        var tile = MathF.Min(AttachmentMaxTile * scale,
            (width - gap * FeedbackDraft.MaxAttachments) / (FeedbackDraft.MaxAttachments + 1));
        var top = origin.Y + headerHeight + HeaderGap * scale;
        var radius = AttachmentRadius * scale;
        var removeIndex = -1;
        var previewIndex = -1;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(origin.X + (tile + gap) * index, top);
            var action = DrawAttachmentThumb(drawList, draft.Attachments[index], min, min + new Vector2(tile, tile),
                radius, scale);
            if (action == 1)
            {
                previewIndex = index;
            }
            else if (action == 2)
            {
                removeIndex = index;
            }
        }

        if (draft.IsFull)
        {
            UiAnchors.Report("feedback.attach",
                new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + tile)));
        }
        else
        {
            var min = new Vector2(origin.X + (tile + gap) * count, top);
            var max = min + new Vector2(tile, tile);
            UiAnchors.Report("feedback.attach", new Rect(min, max));
            if (DrawAddTile(drawList, min, max, radius, scale))
            {
                OpenPicker();
            }
        }

        if (removeIndex >= 0)
        {
            draft.RemoveAt(removeIndex);
            attachmentCountCache = -1;
        }
        else if (previewIndex >= 0)
        {
            var paths = draft.SnapshotAttachments();
            photoViewer.Open(this, paths.Length, previewIndex, page => wallpaperImages.Get(paths[page]));
        }

        var bottom = top + tile;
        if (count == 0 && draft.Category == FeedbackCategory.Bug)
        {
            bottom += Metrics.Space.Sm * scale;
            bottom += Typography.DrawWrappedLeft(new Vector2(origin.X, bottom), Loc.T(L.Feedback.ScreenshotHint),
                ui.MutedInk, TextStyles.Footnote, width);
        }

        return bottom;
    }

    private int DrawAttachmentThumb(ImDrawListPtr drawList, string path, Vector2 min, Vector2 max, float radius,
        float scale)
    {
        var texture = wallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.FieldSurface));
        }
        else
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            Squircle.FillImage(drawList, min, max, radius, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        }

        var badgeRadius = BadgeRadius * scale;
        var badgeCenter = new Vector2(max.X - badgeRadius - BadgeInset * scale, min.Y + badgeRadius + BadgeInset * scale);
        var badgeMin = badgeCenter - new Vector2(badgeRadius, badgeRadius);
        var badgeMax = badgeCenter + new Vector2(badgeRadius, badgeRadius);
        var badgeHovered = UiInteract.Hover(badgeMin, badgeMax);
        var tileHovered = !badgeHovered && UiInteract.Hover(min, max);
        if (tileHovered)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverTint));
        }

        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(badgeHovered ? BadgeHover : BadgeFill),
            20);
        AppSkin.Icon(drawList, badgeCenter, IconGlyph.Of(FontAwesomeIcon.Times), White, BadgeGlyphScale);
        if (badgeHovered || tileHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(badgeMin, badgeMax, badgeHovered))
        {
            return 2;
        }

        return !badgeHovered && UiInteract.Click(min, max, tileHovered) ? 1 : 0;
    }

    private bool DrawAddTile(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float scale)
    {
        var hovered = UiInteract.Hover(min, max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale("feedback.attach.add", down, PressFx.ControlPressedScale);
        var center = (min + max) * 0.5f;
        var half = (max - min) * 0.5f * grow;
        Squircle.Fill(drawList, center - half, center + half, radius,
            ImGui.GetColorU32(hovered ? Palette.Mix(ui.FieldSurface, ui.TitleInk, 0.08f) : ui.FieldSurface));
        var label = Loc.T(L.Feedback.AddShort);
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var fits = Typography.Measure(label, TextStyles.Footnote).X <= max.X - min.X - Metrics.Space.Xs * scale;
        var glyphCenter = fits ? new Vector2(center.X, center.Y - labelHeight * 0.4f) : center;
        AppSkin.Icon(drawList, glyphCenter, IconGlyph.Of(FontAwesomeIcon.Plus), ui.Accent, AddGlyphScale * grow);
        if (fits)
        {
            Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + labelHeight * 0.55f), label, ui.Accent,
                TextStyles.Footnote);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private float DrawDeviceCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var open = disclosureSpring.Step(disclosureOpen ? 1f : 0f, Motion.PageSettle,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        open = Math.Clamp(open, 0f, 1f);
        var pad = CardPad * scale;
        var infoHeight = (InfoRowHeight * FeedbackDeviceInfo.EntryCount) * scale + pad * 0.5f;
        var height = (DeviceRowHeight + DisclosureRowHeight) * scale + infoHeight * open;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale, true);

        var iconSize = DeviceIconSize * scale;
        var rowCenterY = min.Y + DeviceRowHeight * scale * 0.5f;
        FeedbackArt.GlyphTile(drawList, new Vector2(min.X + pad + iconSize * 0.5f, rowCenterY), iconSize,
            FontAwesomeIcon.MobileAlt, AccentRing.Slate);
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var toggleMin = new Vector2(max.X - pad - toggleWidth, rowCenterY - toggleHeight * 0.5f);
        var textLeft = min.X + pad + iconSize + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, toggleMin.X - Metrics.Space.Sm * scale - textLeft);
        var title = Typography.FitText(Loc.T(L.Feedback.DeviceInfoTitle), textWidth, TextStyles.Headline);
        var hint = Typography.FitText(Loc.T(L.Feedback.DeviceInfoHint), textWidth, TextStyles.Footnote);
        var titleHeight = Typography.Measure(title, TextStyles.Headline).Y;
        var hintHeight = Typography.Measure(hint, TextStyles.Footnote).Y;
        var textTop = rowCenterY - (titleHeight + hintHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop), title, ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight), hint, ui.MutedInk, TextStyles.Footnote);
        draft.IncludeDeviceInfo = Toggle.Draw("feedback.deviceInfo",
            new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, toggleHeight)), draft.IncludeDeviceInfo, theme);

        var disclosureTop = min.Y + DeviceRowHeight * scale;
        FeedCell.Hairline(drawList, textLeft, max.X, disclosureTop, ui.Hairline);
        var linkLabel = Loc.T(disclosureOpen ? L.Feedback.HideDetails : L.Feedback.ShowDetails);
        var linkSize = Typography.Measure(linkLabel, TextStyles.Subheadline);
        var linkMin = new Vector2(textLeft, disclosureTop);
        var linkMax = new Vector2(max.X - pad, disclosureTop + DisclosureRowHeight * scale);
        var hovered = UiInteract.Hover(linkMin, linkMax);
        var linkInk = hovered ? Palette.Lighten(ui.Accent, 0.2f) : ui.Accent;
        var linkCenterY = (linkMin.Y + linkMax.Y) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, linkCenterY - linkSize.Y * 0.5f), linkLabel, linkInk,
            TextStyles.Subheadline);
        DrawDisclosureChevron(drawList, new Vector2(linkMax.X - Metrics.Space.Xs * scale, linkCenterY), open,
            linkInk, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(linkMin, linkMax, hovered))
        {
            disclosureOpen = !disclosureOpen;
        }

        if (open > 0.001f)
        {
            DrawDeviceInfoRows(drawList, new Vector2(textLeft, linkMax.Y), max.X - pad, max.Y, open, scale);
        }

        return max.Y;
    }

    private void DrawDeviceInfoRows(ImDrawListPtr drawList, Vector2 origin, float right, float clipBottom,
        float open, float scale)
    {
        var alpha = open * (draft.IncludeDeviceInfo ? 1f : InfoDimAlpha);
        drawList.PushClipRect(new Vector2(origin.X, origin.Y), new Vector2(right, clipBottom), true);
        var rowHeight = InfoRowHeight * scale;
        for (var index = 0; index < FeedbackDeviceInfo.EntryCount; index++)
        {
            var rowTop = origin.Y + rowHeight * index;
            var value = deviceInfo.Value(index);
            var valueSize = Typography.Measure(value, TextStyles.Footnote);
            var labelWidth = MathF.Max(1f, right - origin.X - valueSize.X - Metrics.Space.Sm * scale);
            var label = Typography.FitText(Loc.T(FeedbackDeviceInfo.Label(index)), labelWidth, TextStyles.Footnote);
            var textY = rowTop + (rowHeight - valueSize.Y) * 0.5f;
            Typography.Draw(drawList, new Vector2(origin.X, textY), label, ui.MutedInk with { W = ui.MutedInk.W * alpha },
                TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(right - valueSize.X, textY), value,
                ui.TitleInk with { W = ui.TitleInk.W * alpha }, TextStyles.Footnote);
        }

        drawList.PopClipRect();
    }

    private static void DrawDisclosureChevron(ImDrawListPtr drawList, Vector2 center, float open, Vector4 color,
        float scale)
    {
        var size = ChevronSize * scale;
        var angle = open * MathF.PI * 0.5f;
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        var upper = Rotate(new Vector2(-size * 0.5f, -size), cos, sin);
        var tip = Rotate(new Vector2(size * 0.5f, 0f), cos, sin);
        var lower = Rotate(new Vector2(-size * 0.5f, size), cos, sin);
        var packed = ImGui.GetColorU32(color);
        var thickness = Metrics.Stroke.Ring * scale;
        drawList.AddLine(center + upper, center + tip, packed, thickness);
        drawList.AddLine(center + tip, center + lower, packed, thickness);
    }

    private static Vector2 Rotate(Vector2 point, float cos, float sin) =>
        new(point.X * cos - point.Y * sin, point.X * sin + point.Y * cos);

    private float SendBandHeight(float width, float scale)
    {
        var height = (SendBandPad * 2f + SendPillHeight) * scale;
        if (sendFailure.Failed)
        {
            height += BannerHeight(width - AppSurface.SidePadding * 2f * scale, scale) + SendBandPad * scale;
        }

        return height;
    }

    private float BannerHeight(float width, float scale)
    {
        var textWidth = BannerTextWidth(width, scale);
        return BannerPad * 2f * scale
               + Typography.MeasureWrappedBlock(Loc.T(L.Feedback.SendFailed), TextStyles.FootnoteEmphasized, textWidth).Y
               + Typography.MeasureWrappedBlock(sendFailure.Text(), TextStyles.Footnote, textWidth).Y;
    }

    private static float BannerTextWidth(float width, float scale) =>
        MathF.Max(1f, width - BannerPad * 2f * scale - Metrics.Size.IconTile * scale);

    private void DrawSendBand(Rect band, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var side = AppSurface.SidePadding * scale;
        var left = band.Min.X + side;
        var right = band.Max.X - side;
        var top = band.Min.Y + SendBandPad * scale;
        if (sendFailure.Failed)
        {
            top = DrawFailureBanner(drawList, new Vector2(left, top), right - left, scale) + SendBandPad * scale;
        }

        var pill = new Rect(new Vector2(left, top), new Vector2(right, top + SendPillHeight * scale));
        UiAnchors.Report("feedback.send", pill);
        var cooldown = CooldownRemaining();
        var posting = store.Posting;
        var enabled = draft.HasText && !posting && cooldown == 0;
        var label = SendLabel(posting, cooldown);
        if (FeedbackArt.SendPill(ui, pill, label, enabled, posting, "feedback.send.pill"))
        {
            BeginSend();
        }
    }

    private float DrawFailureBanner(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var height = BannerHeight(width, scale);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var radius = Metrics.Radius.Card * scale;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, BannerFillAlpha)));
        var pad = BannerPad * scale;
        var iconSlot = Metrics.Size.IconTile * scale;
        AppSkin.Icon(drawList, new Vector2(min.X + pad + iconSlot * 0.4f, min.Y + pad + iconSlot * 0.35f),
            IconGlyph.Of(FontAwesomeIcon.ExclamationCircle), theme.Danger, BannerIconScale);
        var textLeft = min.X + pad + iconSlot;
        var textWidth = BannerTextWidth(width, scale);
        var cursorY = min.Y + pad;
        cursorY += Typography.DrawWrappedLeft(new Vector2(textLeft, cursorY), Loc.T(L.Feedback.SendFailed),
            ui.TitleInk, TextStyles.FootnoteEmphasized, textWidth);
        Typography.DrawWrappedLeft(new Vector2(textLeft, cursorY), sendFailure.Text(), ui.MutedInk,
            TextStyles.Footnote, textWidth);
        return max.Y;
    }

    private string SendLabel(bool posting, int cooldown)
    {
        if (posting)
        {
            var total = store.TotalImages;
            var uploaded = store.UploadedImages;
            if (total == 0 || uploaded >= total)
            {
                return Loc.T(L.Feedback.Sending);
            }

            var key = (uploaded + 1) * 16 + total;
            if (key != uploadCacheKey)
            {
                uploadCacheKey = key;
                uploadCacheLabel = Loc.T(L.Feedback.Uploading, uploaded + 1, total);
            }

            return uploadCacheLabel;
        }

        if (cooldown > 0)
        {
            if (cooldown != cooldownCacheValue)
            {
                cooldownCacheValue = cooldown;
                cooldownCacheLabel = Loc.T(L.Feedback.Cooldown, TimeText.MinutesSeconds(cooldown));
            }

            return cooldownCacheLabel;
        }

        return sendFailure.Failed ? Loc.T(L.Feedback.TryAgain) : Loc.T(L.Feedback.Send);
    }

    private void BeginSend()
    {
        if (!draft.HasText || store.Posting || CooldownRemaining() > 0)
        {
            return;
        }

        sendFailure.Clear();
        sentText = draft.Text;
        sentCategory = draft.Category;
        var context = draft.IncludeDeviceInfo ? deviceInfo.Context : string.Empty;
        store.Compose(draft.Text, draft.Category, context, draft.SnapshotAttachments(), result =>
        {
            sendOutcomeFailure = result.Succeeded ? null : new AepFailureBox(result.Failure);
            Interlocked.Exchange(ref sendOutcome, result.Succeeded ? SendSucceeded : SendFailed);
        });
    }

    private void AskDiscard()
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Feedback.DiscardConfirm),
            ConfirmLabel = Loc.T(L.Feedback.Discard),
            CancelLabel = Loc.T(L.Feedback.KeepEditing),
            Danger = true,
            Sheet = true,
            Confirm = DiscardDraft,
        });
    }

    private void DiscardDraft()
    {
        draft.Clear();
        sendFailure.Clear();
        attachmentCountCache = -1;
        if (router.Current.Screen == FeedbackScreen.Compose)
        {
            router.Pop();
        }
    }

    private void OpenPicker()
    {
        pickerPaths = library.List();
        router.Push(FeedbackRoute.Photos);
    }
}
