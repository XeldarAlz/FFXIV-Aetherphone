using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class ReportOverlay
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const string CategoryMenuId = "reportCategory";
    private const float RevealSmoothTime = 0.16f;
    private const float MaxDim = 0.55f;
    private const float MinCardScale = 0.92f;
    private const float CardRounding = 24f;
    private const float CardPadding = 20f;
    private const float CardMaxWidth = 360f;
    private const float CardSideMargin = 24f;
    private const float FieldHeight = 42f;
    private const float FieldGap = 10f;
    private const float TitleGap = 12f;
    private const float ButtonGap = 10f;
    private const float ButtonsTopGap = 18f;
    private const float ButtonHeight = 38f;
    private const float TitleScale = 1.25f;
    private const float DisclosureScale = 0.82f;
    private const float FieldTextScale = 0.92f;
    private const int ReasonMaxLength = 170;

    private readonly ReportService service;
    private readonly DropdownMenu categoryMenu = new();
    private readonly DropdownMenu.Item[] categoryItems = new DropdownMenu.Item[ReportCategories.All.Length];
    private Spring reveal;
    private ReportPrompt? shown;

    public ReportOverlay(ReportService service)
    {
        this.service = service;
    }

    public bool CapturesPointer => service.Active is not null || !reveal.IsResting(0f, 0.001f, 0.005f);

    public void Draw(Rect screen, PhoneTheme theme)
    {
        var active = service.Active;
        if (active is not null)
        {
            shown = active;
        }
        else if (categoryMenu.Open)
        {
            categoryMenu.Close();
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(active is not null ? 1f : 0f, RevealSmoothTime, delta);
        if (shown is null)
        {
            return;
        }

        if (active is null && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            shown = null;
            return;
        }

        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        var cardScale = MinCardScale + (1f - MinCardScale) * Easing.EaseOutQuint(opacity);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##reportOverlay", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
            var menuWasOpen = categoryMenu.Open;
            var interactive = active is not null && opacity > 0.5f && !menuWasOpen;
            var cardRect = DrawCard(screen, theme, shown, opacity, cardScale, interactive);
            DrawCategoryMenu(screen, theme);
            if (active is null || opacity <= 0.5f || menuWasOpen)
            {
                return;
            }

            if (!service.Busy && ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
                !ImGui.IsMouseHoveringRect(cardRect.Min, cardRect.Max))
            {
                service.Dismiss();
            }
        }
    }

    private Rect DrawCard(Rect screen, PhoneTheme theme, ReportPrompt prompt, float opacity, float cardScale,
        bool interactive)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var s = scale * cardScale;
        var drawList = ImGui.GetWindowDrawList();
        var pad = CardPadding * s;
        var available = screen.Width - CardSideMargin * 2f * scale;
        var cardWidth = MathF.Min(CardMaxWidth * scale, available) * cardScale;
        var innerWidth = cardWidth - pad * 2f;

        var titleHeight = Typography.Measure(prompt.Title, TitleScale * cardScale, FontWeight.Bold).Y;
        float bodyHeight;
        if (service.Sent)
        {
            var sentHeight = Typography.MeasureWrapped(Loc.T(L.Report.Sent), innerWidth, FieldTextScale * cardScale,
                FontWeight.Medium);
            bodyHeight = sentHeight + ButtonsTopGap * s + ButtonHeight * s;
        }
        else
        {
            var disclosureHeight = prompt.Disclosure is { Length: > 0 } disclosure
                ? Typography.MeasureWrapped(disclosure, innerWidth, DisclosureScale * cardScale) + TitleGap * s
                : 0f;
            var failedHeight = service.Failed
                ? FieldGap * s + Typography.Measure(Loc.T(L.Report.Failed), 0.8f * cardScale).Y
                : 0f;
            bodyHeight = disclosureHeight + FieldHeight * s + FieldGap * s + FieldHeight * s + failedHeight +
                         ButtonsTopGap * s + ButtonHeight * s;
        }

        var cardHeight = pad + titleHeight + TitleGap * s + bodyHeight + pad;
        var cardMin = new Vector2(screen.Center.X - cardWidth * 0.5f, screen.Center.Y - cardHeight * 0.5f);
        var cardMax = cardMin + new Vector2(cardWidth, cardHeight);
        var cardRect = new Rect(cardMin, cardMax);
        Squircle.Fill(drawList, cardMin, cardMax, CardRounding * s,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Surface, opacity)));
        Squircle.Stroke(drawList, cardMin, cardMax, CardRounding * s,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f * opacity)), 1f);

        var titleColor = Palette.WithAlpha(theme.TextStrong, opacity);
        Typography.DrawCentered(drawList, new Vector2(cardRect.Center.X, cardMin.Y + pad + titleHeight * 0.5f),
            Typography.FitText(prompt.Title, innerWidth, TitleScale * cardScale, FontWeight.Bold), titleColor,
            TitleScale * cardScale, FontWeight.Bold);
        var y = cardMin.Y + pad + titleHeight + TitleGap * s;
        var left = cardMin.X + pad;
        if (service.Sent)
        {
            DrawSentBody(cardRect, theme, pad, left, innerWidth, y, s, opacity, cardScale, interactive);
            return cardRect;
        }

        if (prompt.Disclosure is { Length: > 0 } disclosureText)
        {
            DrawWrapped(disclosureText, new Vector2(left, y), innerWidth, DisclosureScale * cardScale,
                Palette.WithAlpha(theme.TextMuted, opacity));
            y += Typography.MeasureWrapped(disclosureText, innerWidth, DisclosureScale * cardScale) + TitleGap * s;
        }

        var categoryRect = new Rect(new Vector2(left, y), new Vector2(left + innerWidth, y + FieldHeight * s));
        DrawCategoryField(categoryRect, theme, s, opacity, cardScale, interactive);
        y += FieldHeight * s + FieldGap * s;
        var detailsRect = new Rect(new Vector2(left, y), new Vector2(left + innerWidth, y + FieldHeight * s));
        DrawDetailsField(detailsRect, theme, s, opacity, cardScale, interactive);
        y += FieldHeight * s;
        if (service.Failed)
        {
            y += FieldGap * s;
            var failedText = Loc.T(L.Report.Failed);
            var failedHeight = Typography.Measure(failedText, 0.8f * cardScale).Y;
            Typography.DrawCentered(drawList, new Vector2(cardRect.Center.X, y + failedHeight * 0.5f), failedText,
                Palette.WithAlpha(theme.Danger, opacity), 0.8f * cardScale, FontWeight.Medium);
            y += failedHeight;
        }

        y += ButtonsTopGap * s;
        var buttonWidth = (innerWidth - ButtonGap * s) * 0.5f;
        var cancelRect = new Rect(new Vector2(left, y), new Vector2(left + buttonWidth, y + ButtonHeight * s));
        var submitRect = new Rect(new Vector2(cancelRect.Max.X + ButtonGap * s, y),
            new Vector2(left + innerWidth, y + ButtonHeight * s));
        if (ConfirmDialog.DrawPillButton(cancelRect, Loc.T(L.Common.Cancel), !service.Busy, theme, cardScale,
                opacity) && interactive && !service.Busy)
        {
            service.Dismiss();
        }

        var canSubmit = !service.Busy && service.CategoryIndex >= 0;
        var submitLabel = Loc.T(service.Busy ? L.Report.Sending : L.Report.Submit);
        if (ConfirmDialog.DrawPillButton(submitRect, submitLabel, canSubmit, theme, cardScale, opacity,
                ConfirmButtonTone.Danger) && interactive && canSubmit)
        {
            service.Submit();
        }

        return cardRect;
    }

    private void DrawSentBody(Rect cardRect, PhoneTheme theme, float pad, float left, float innerWidth, float y,
        float s, float opacity, float cardScale, bool interactive)
    {
        DrawWrapped(Loc.T(L.Report.Sent), new Vector2(left, y), innerWidth, FieldTextScale * cardScale,
            Palette.WithAlpha(theme.TextStrong, 0.88f * opacity), FontWeight.Medium);
        var buttonY = cardRect.Max.Y - pad - ButtonHeight * s;
        var closeRect = new Rect(new Vector2(left, buttonY), new Vector2(left + innerWidth, buttonY + ButtonHeight * s));
        if (ConfirmDialog.DrawPillButton(closeRect, Loc.T(L.Common.Close), true, theme, cardScale, opacity,
                ConfirmButtonTone.Primary) && interactive)
        {
            service.Dismiss();
        }
    }

    private void DrawCategoryField(Rect rect, PhoneTheme theme, float s, float opacity, float cardScale,
        bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = interactive && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var fill = hovered ? Palette.Mix(theme.SurfaceMuted, theme.TextStrong, 0.06f) : theme.SurfaceMuted;
        Squircle.Fill(drawList, rect.Min, rect.Max, 12f * s, ImGui.GetColorU32(Palette.WithAlpha(fill, opacity)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, 12f * s,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.10f * opacity)), 1f);
        var hasCategory = service.CategoryIndex >= 0;
        var label = hasCategory
            ? Loc.T(ReportCategories.All[service.CategoryIndex].Label)
            : Loc.T(L.Report.CategoryHint);
        var ink = hasCategory ? theme.TextStrong : theme.TextMuted;
        var labelMaxWidth = rect.Max.X - 30f * s - (rect.Min.X + 14f * s);
        var fittedLabel = Typography.FitText(label, labelMaxWidth, FieldTextScale * cardScale, FontWeight.Medium);
        var labelSize = Typography.Measure(fittedLabel, FieldTextScale * cardScale, FontWeight.Medium);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 14f * s, rect.Center.Y - labelSize.Y * 0.5f), fittedLabel,
            Palette.WithAlpha(ink, opacity), FieldTextScale * cardScale, FontWeight.Medium);
        AppSkin.Icon(drawList, new Vector2(rect.Max.X - 18f * s, rect.Center.Y),
            FontAwesomeIcon.ChevronDown.ToIconString(), Palette.WithAlpha(theme.TextMuted, opacity), 0.72f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !service.Busy)
            {
                categoryMenu.Toggle(CategoryMenuId, rect);
            }
        }
    }

    private void DrawDetailsField(Rect rect, PhoneTheme theme, float s, float opacity, float cardScale,
        bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, 12f * s,
            ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, opacity)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, 12f * s,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.10f * opacity)), 1f);
        if (!interactive)
        {
            if (service.ReasonDraft.Length > 0)
            {
                var textSize = Typography.Measure(service.ReasonDraft, FieldTextScale * cardScale);
                var textLeft = rect.Min.X + 14f * s;
                var textMaxWidth = rect.Max.X - 14f * s - textLeft;
                var reasonHovering = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
                Marquee.DrawLeft("reportoverlay.reason", service.ReasonDraft, textLeft,
                    rect.Center.Y - textSize.Y * 0.5f, textMaxWidth, new TextStyle(FieldTextScale * cardScale,
                        FontWeight.Regular), Palette.WithAlpha(theme.TextStrong, opacity), reasonHovering);
            }

            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + 8f * s,
            rect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(rect.Width - 16f * s);
        var draft = service.ReasonDraft;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f))
                   .Push(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##reportDetails", Loc.T(L.Report.DetailsHint), ref draft, ReasonMaxLength,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                service.ReasonDraft = draft;
                service.Submit();
                return;
            }
        }

        service.ReasonDraft = draft;
    }

    private void DrawCategoryMenu(Rect screen, PhoneTheme theme)
    {
        if (!categoryMenu.IsOpenFor(CategoryMenuId))
        {
            return;
        }

        for (var index = 0; index < ReportCategories.All.Length; index++)
        {
            categoryItems[index] = new DropdownMenu.Item(Loc.T(ReportCategories.All[index].Label),
                Selected: index == service.CategoryIndex);
        }

        var picked = categoryMenu.Draw(screen, theme, categoryItems);
        if (picked >= 0)
        {
            service.CategoryIndex = picked;
        }
    }

    private static void DrawWrapped(string text, Vector2 position, float width, float fontScale, Vector4 color,
        FontWeight weight = FontWeight.Regular)
    {
        ImGui.SetCursorScreenPos(position);
        using (Plugin.Fonts.Push(fontScale, weight))
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.PushTextWrapPos(position.X + width - ImGui.GetWindowPos().X);
            Typography.Wrapped(text);
            ImGui.PopTextWrapPos();
        }
    }
}
