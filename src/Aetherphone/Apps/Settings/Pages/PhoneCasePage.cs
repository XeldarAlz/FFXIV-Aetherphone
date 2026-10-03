using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class PhoneCasePage : ISettingsPage
{
    private const float PreviewScale = 0.55f;
    private const float VisibleCards = 2.5f;
    private const float CardGap = 12f;
    private const float CardRadius = 16f;
    private const float LabelZone = 30f;
    private const float CheckRadius = 10f;
    private const float CheckInset = 15f;
    private const float DragSlop = 5f;
    private const float ArrowRadius = 14f;
    private const float ArrowInset = 4f;
    private const float EdgeEpsilon = 1f;

    private static readonly PhoneCaseCategory[] CategoryOrder =
    {
        PhoneCaseCategory.Colors, PhoneCaseCategory.Gradients, PhoneCaseCategory.ArtistSeries,
    };

    public string Title => Loc.T(L.Settings.PhoneCase);
    public string Summary => CatalogLabels.PhoneCase(configuration.PhoneCaseName);
    public FontAwesomeIcon Icon => FontAwesomeIcon.MobileAlt;
    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);
    private readonly Configuration configuration;
    private readonly ThemeProvider themes;
    private readonly ISettingsNavigator navigator;
    private readonly PhoneCase[][] sections;
    private readonly Spring[] railSprings;
    private readonly float[] railTargets;
    private readonly bool[] railCentered;
    private readonly string[] leftArrowIds;
    private readonly string[] rightArrowIds;
    private int draggingRail = -1;
    private float dragTravel;
    private float lastMouseX;

    public PhoneCasePage(Configuration configuration, ThemeProvider themes, ISettingsNavigator navigator)
    {
        this.configuration = configuration;
        this.themes = themes;
        this.navigator = navigator;
        sections = BuildSections();
        railSprings = new Spring[sections.Length];
        railTargets = new float[sections.Length];
        railCentered = new bool[sections.Length];
        leftArrowIds = new string[sections.Length];
        rightArrowIds = new string[sections.Length];
        for (var railIndex = 0; railIndex < sections.Length; railIndex++)
        {
            leftArrowIds[railIndex] = "case.rail.left." + railIndex;
            rightArrowIds[railIndex] = "case.rail.right." + railIndex;
        }
    }

    private static PhoneCase[][] BuildSections()
    {
        var cases = ThemeCatalog.Cases;
        var result = new PhoneCase[CategoryOrder.Length][];
        for (var categoryIndex = 0; categoryIndex < CategoryOrder.Length; categoryIndex++)
        {
            var category = CategoryOrder[categoryIndex];
            var count = 0;
            for (var caseIndex = 0; caseIndex < cases.Count; caseIndex++)
            {
                if (cases[caseIndex].Category == category)
                {
                    count++;
                }
            }

            var section = new PhoneCase[count];
            var writeIndex = 0;
            for (var caseIndex = 0; caseIndex < cases.Count; caseIndex++)
            {
                if (cases[caseIndex].Category == category)
                {
                    section[writeIndex++] = cases[caseIndex];
                }
            }

            result[categoryIndex] = section;
        }

        return result;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            var cardWidth = (width - CardGap * scale) / VisibleCards;
            var cardHeight = cardWidth / PhoneCasePreview.CanvasAspect + LabelZone * scale;
            var windowRight = ImGui.GetWindowPos().X + ImGui.GetWindowWidth();
            for (var railIndex = 0; railIndex < sections.Length; railIndex++)
            {
                if (sections[railIndex].Length == 0)
                {
                    continue;
                }

                SettingsSection.Header(CatalogLabels.CaseCategory(CategoryOrder[railIndex]), theme);
                var origin = ImGui.GetCursorScreenPos();
                ImGui.Dummy(new Vector2(width, cardHeight));
                DrawRail(railIndex, new Rect(origin, new Vector2(windowRight, origin.Y + cardHeight)), cardWidth,
                    theme, scale);
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            }
        }
    }

    private void DrawRail(int railIndex, Rect row, float cardWidth, PhoneTheme theme, float scale)
    {
        var cases = sections[railIndex];
        var stride = cardWidth + CardGap * scale;
        var contentWidth = cases.Length * stride - CardGap * scale;
        var maxOffset = MathF.Max(0f, contentWidth - row.Width + Metrics.Space.Lg * scale);
        if (!railCentered[railIndex])
        {
            railCentered[railIndex] = true;
            var initial = InitialOffset(cases, stride, row.Width, cardWidth, maxOffset);
            railSprings[railIndex].SnapTo(initial);
            railTargets[railIndex] = initial;
        }

        var radius = ArrowRadius * scale;
        var arrowCenterY = row.Min.Y + (row.Height - LabelZone * scale) * 0.5f;
        var leftCenter = new Vector2(row.Min.X + radius + ArrowInset * scale, arrowCenterY);
        var rightCenter = new Vector2(row.Max.X - radius - ArrowInset * scale, arrowCenterY);
        var currentOffset = Math.Clamp(railSprings[railIndex].Value, 0f, maxOffset);
        var showLeft = maxOffset > EdgeEpsilon && currentOffset > EdgeEpsilon;
        var showRight = maxOffset > EdgeEpsilon && currentOffset < maxOffset - EdgeEpsilon;
        var overArrow = (showLeft && OverCircle(leftCenter, radius)) || (showRight && OverCircle(rightCenter, radius));
        HandleDrag(railIndex, row, maxOffset, overArrow);
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        if (draggingRail != railIndex)
        {
            railSprings[railIndex].Step(railTargets[railIndex], Motion.PageSettle, deltaSeconds);
        }

        var offset = Math.Clamp(railSprings[railIndex].Value, 0f, maxOffset);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(row.Min, row.Max, true);
        var cursorX = row.Min.X - offset;
        for (var cardIndex = 0; cardIndex < cases.Length; cardIndex++)
        {
            if (cursorX + cardWidth >= row.Min.X && cursorX <= row.Max.X)
            {
                DrawCard(cases[cardIndex],
                    new Rect(new Vector2(cursorX, row.Min.Y), new Vector2(cursorX + cardWidth, row.Max.Y)), theme,
                    scale, overArrow);
            }

            cursorX += stride;
        }

        drawList.PopClipRect();
        var pageDelta = stride * MathF.Max(1f, MathF.Floor(VisibleCards));
        var interactive = draggingRail < 0;
        if (showLeft && HoverButton.Circle(drawList, leftArrowIds[railIndex], leftCenter, radius,
                FontAwesomeIcon.ChevronLeft, theme.Surface, theme.TextStrong, deltaSeconds, 1f, interactive))
        {
            Nudge(railIndex, -pageDelta, stride, maxOffset);
        }

        if (showRight && HoverButton.Circle(drawList, rightArrowIds[railIndex], rightCenter, radius,
                FontAwesomeIcon.ChevronRight, theme.Surface, theme.TextStrong, deltaSeconds, 1f, interactive))
        {
            Nudge(railIndex, pageDelta, stride, maxOffset);
        }
    }

    private static bool OverCircle(Vector2 center, float radius)
    {
        var mouse = ImGui.GetIO().MousePos;
        return mouse.X >= center.X - radius && mouse.X <= center.X + radius && mouse.Y >= center.Y - radius &&
               mouse.Y <= center.Y + radius;
    }

    private void Nudge(int railIndex, float amount, float stride, float maxOffset)
    {
        var raw = railTargets[railIndex] + amount;
        var snapped = MathF.Round(raw / stride) * stride;
        railTargets[railIndex] = Math.Clamp(snapped, 0f, maxOffset);
    }

    private void DrawCard(PhoneCase option, Rect card, PhoneTheme theme, float scale, bool overArrow)
    {
        var drawList = ImGui.GetWindowDrawList();
        var selected = option.Id == configuration.PhoneCaseName;
        var radius = CardRadius * scale;
        var hovered = !overArrow && UiInteract.Hover(card.Min, card.Max);
        if (selected)
        {
            Elevation.Floating(drawList, card.Min, card.Max, radius, scale, 0.7f);
        }

        var fill = hovered && draggingRail < 0 ? Palette.Lighten(theme.GroupedCard, 0.04f) : theme.GroupedCard;
        Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(fill));
        Material.EdgeSquircle(drawList, card.Min, card.Max, radius, scale);
        var preview = new Rect(card.Min, new Vector2(card.Max.X, card.Max.Y - LabelZone * scale));
        PhoneCasePreview.Draw(drawList, preview, option, theme, scale * PreviewScale);
        if (selected)
        {
            Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(theme.Accent), 2f * scale);
            var checkCenter = new Vector2(card.Max.X - CheckInset * scale, card.Min.Y + CheckInset * scale);
            drawList.AddCircleFilled(checkCenter, CheckRadius * scale, ImGui.GetColorU32(theme.Accent), 24);
            ProgressRing.CenterIcon(drawList, checkCenter, FontAwesomeIcon.Check, AccentRing.Ink,
                CheckRadius * 1.1f * scale);
        }

        var label = CatalogLabels.PhoneCase(option.Id);
        var labelColor = selected ? theme.Accent : theme.TextMuted;
        var labelStyle = selected ? TextStyles.SubheadlineEmphasized : TextStyles.Subheadline;
        Typography.DrawWrappedCentered(drawList, new Vector2(card.Center.X, card.Max.Y - LabelZone * scale * 0.5f),
            label, labelColor, labelStyle, card.Width - 8f * scale);
        if (hovered && draggingRail < 0)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (dragTravel <= DragSlop * scale && UiInteract.Click(card.Min, card.Max, hovered))
        {
            navigator.Open(new PhoneCaseDetailPage(option, configuration, themes));
        }
    }

    private float InitialOffset(PhoneCase[] cases, float stride, float rowWidth, float cardWidth, float maxOffset)
    {
        for (var cardIndex = 0; cardIndex < cases.Length; cardIndex++)
        {
            if (cases[cardIndex].Id == configuration.PhoneCaseName)
            {
                return Math.Clamp(cardIndex * stride - (rowWidth - cardWidth) * 0.5f, 0f, maxOffset);
            }
        }

        return 0f;
    }

    private void HandleDrag(int railIndex, Rect row, float maxOffset, bool overArrow)
    {
        if (!overArrow && UiInteract.Hover(row.Min, row.Max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            draggingRail = railIndex;
            dragTravel = 0f;
            lastMouseX = ImGui.GetIO().MousePos.X;
        }

        if (draggingRail == railIndex)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var mouseX = ImGui.GetIO().MousePos.X;
                var travel = mouseX - lastMouseX;
                lastMouseX = mouseX;
                dragTravel += MathF.Abs(travel);
                if (dragTravel > DragSlop * UiScale.Current)
                {
                    var dragged = Math.Clamp(railSprings[railIndex].Value - travel, 0f, maxOffset);
                    railSprings[railIndex].SnapTo(dragged);
                    railTargets[railIndex] = dragged;
                }
            }
            else
            {
                draggingRail = -1;
            }
        }

        railTargets[railIndex] = Math.Clamp(railTargets[railIndex], 0f, maxOffset);
    }
}
