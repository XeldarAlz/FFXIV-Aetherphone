using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Calculator;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Calculator;

internal sealed partial class CalculatorApp
{
    private const float DisplayInset = 8f;
    private const float TapeRowHeight = 30f;
    private const float TapeFadeHeight = 28f;
    private const float TapeMinimumHeight = 60f;
    private const float TapeTextGap = 10f;
    private const float TapeHoverAlpha = 0.07f;
    private const float ExpressionGap = 2f;
    private const float DisplayMaxScale = 2.85f;
    private const float DisplayMinScale = 1.20f;
    private const float AnswerAppearFrom = 0.35f;
    private const float SwipeDistance = 26f;
    private const float HoldSlop = 8f;
    private const float LongPressSeconds = 0.45f;
    private const float MenuHeight = 38f;
    private const float MenuSegmentPad = 18f;
    private const float MenuGap = 8f;
    private const float MenuShadowMargin = 14f;
    private const float MenuDividerAlpha = 0.22f;
    private const float MenuHiddenAlpha = 0.01f;
    private const float DisabledAlpha = 0.38f;
    private const string Ellipsis = "…";
    private static readonly TextStyle NumberStyle = new(1.32f, FontWeight.Regular);

    private Spring displayScale;
    private Spring answerAppear = new(1f);
    private Spring menuAppear;
    private int solvedAtOpen;
    private int solvedSeen;
    private bool scrollTapeToBottom;
    private int tapeVersion = -1;
    private DateTime tapeDay;
    private int tapeCount;
    private Rect screenRect;
    private string fittedSource = string.Empty;
    private float fittedWidth;
    private string fittedExpression = string.Empty;
    private float fittedScale;
    private bool gestureTracking;
    private bool gestureConsumed;
    private Vector2 gestureOrigin;
    private float gestureSeconds;
    private bool menuOpen;
    private bool menuCanPaste;
    private double menuPasteValue;
    private Rect menuRect;

    private void DrawDisplay(Rect display, Rect screen, float scale)
    {
        screenRect = screen;
        if (menuOpen)
        {
            UiInteract.HoverOverlay(menuRect);
        }

        RefreshTape();
        var resultHeight = Typography.Measure(DigitLabels[0], new TextStyle(DisplayMaxScale, FontWeight.Regular)).Y;
        var expressionHeight = Typography.Measure(DigitLabels[0], NumberStyle).Y;
        var liveHeight = resultHeight + expressionHeight + ExpressionGap * scale;
        var live = new Rect(new Vector2(display.Min.X, MathF.Max(display.Min.Y, display.Max.Y - liveHeight)),
            display.Max);
        if (tapeCount > 0 && live.Min.Y - display.Min.Y >= TapeMinimumHeight * scale)
        {
            DrawTape(new Rect(display.Min, new Vector2(display.Max.X, live.Min.Y)), scale);
        }

        DrawLive(live, scale);
    }

    private void DrawLive(Rect live, float scale)
    {
        UiAnchors.Report("calculator.display", live);
        if (engine.SolvedCount > solvedAtOpen)
        {
            UiAnchors.Report("calculator.answer", live);
        }

        if (engine.SolvedCount != solvedSeen)
        {
            solvedSeen = engine.SolvedCount;
            answerAppear.SnapTo(AnswerAppearFrom);
        }

        var appear = answerAppear.Step(1f, Motion.Appear, delta);
        var inset = DisplayInset * scale;
        var width = MathF.Max(1f, live.Width - inset * 2f);
        var right = live.Max.X - inset;
        var text = engine.IsError ? Loc.T(L.Calculator.Error) : Localize(engine.Display);
        var target = Typography.FitScale(text, width, DisplayMaxScale, DisplayMinScale, FontWeight.Regular);
        if (displayScale.Value <= 0f)
        {
            displayScale.SnapTo(target);
        }

        var shown = MathF.Min(displayScale.Step(target, Motion.Appear, delta), MathF.Max(target, DisplayMinScale));
        var style = new TextStyle(shown, FontWeight.Regular);
        var size = Typography.Measure(text, style);
        if (size.X > width)
        {
            style = new TextStyle(target, FontWeight.Regular);
            size = Typography.Measure(text, style);
        }

        var resultRect = new Rect(new Vector2(right - size.X, live.Max.Y - size.Y), new Vector2(right, live.Max.Y));
        var drawList = ImGui.GetWindowDrawList();
        var expression = engine.Expression;
        if (expression.Length > 0)
        {
            FitExpression(Localize(expression), width);
            var expressionSize = Typography.Measure(fittedExpression, new TextStyle(fittedScale, FontWeight.Regular));
            var expressionTop = resultRect.Min.Y - ExpressionGap * scale - expressionSize.Y;
            Typography.Draw(drawList, new Vector2(right - expressionSize.X, expressionTop), fittedExpression,
                ui.MutedInk, new TextStyle(fittedScale, FontWeight.Regular));
        }

        Typography.Draw(drawList, resultRect.Min, text, ui.TitleInk with { W = ui.TitleInk.W * appear }, style);
        HandleDisplayGesture(live, scale);
        DrawMenu(resultRect, live, scale);
    }

    private void FitExpression(string text, float width)
    {
        if (ReferenceEquals(text, fittedSource) && MathF.Abs(width - fittedWidth) < 0.5f)
        {
            return;
        }

        fittedSource = text;
        fittedWidth = width;
        fittedScale = Typography.FitScale(text, width, NumberStyle.Scale, TextStyles.Body.Scale, FontWeight.Regular);
        fittedExpression = text;
        if (Typography.Measure(text, fittedScale, FontWeight.Regular).X <= width)
        {
            return;
        }

        for (var start = 1; start < text.Length; start++)
        {
            var candidate = string.Concat(Ellipsis, text.AsSpan(start));
            if (Typography.Measure(candidate, fittedScale, FontWeight.Regular).X <= width)
            {
                fittedExpression = candidate;
                return;
            }
        }
    }

    private void RefreshTape()
    {
        var today = DateTime.Now.Date;
        if (tapeVersion == engine.HistoryVersion && tapeDay == today)
        {
            return;
        }

        tapeVersion = engine.HistoryVersion;
        tapeDay = today;
        scrollTapeToBottom = true;
        var history = engine.History;
        var count = 0;
        while (count < history.Count && LocalDay(history[count].SolvedAtUnix) == today)
        {
            count++;
        }

        tapeCount = count;
    }

    private void DrawTape(Rect rect, float scale)
    {
        UiAnchors.Report("calculator.tape", rect);
        CalculatorHistoryRecord? recalled = null;
        using (UiScope.PushId("calculator.tape"))
        using (AppSurface.BeginEdgeToEdge(rect))
        using (UiScope.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ImGui.GetContentRegionAvail().X;
            var rowHeight = TapeRowHeight * scale;
            var rowsHeight = tapeCount * rowHeight;
            if (rowsHeight < rect.Height)
            {
                ImGui.Dummy(new Vector2(width, rect.Height - rowsHeight));
            }

            var history = engine.History;
            for (var index = tapeCount - 1; index >= 0; index--)
            {
                if (DrawTapeRow(drawList, history[index], width, rowHeight, scale))
                {
                    recalled = history[index];
                }
            }

            if (scrollTapeToBottom)
            {
                ImGui.SetScrollHereY(1f);
                scrollTapeToBottom = false;
            }

            DrawTapeFade(drawList, rect, scale);
        }

        if (recalled is null)
        {
            return;
        }

        menuOpen = false;
        engine.Recall(recalled);
    }

    private bool DrawTapeRow(ImDrawListPtr drawList, CalculatorHistoryRecord record, float width, float rowHeight,
        float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (!ImGui.IsRectVisible(origin, max))
        {
            return false;
        }

        var hovered = UiInteract.Hover(origin, max);
        if (hovered)
        {
            Squircle.Fill(drawList, origin, max, Metrics.Radius.Md * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, TapeHoverAlpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var inset = DisplayInset * scale;
        var centerY = origin.Y + rowHeight * 0.5f;
        var result = Localize(record.Result);
        var resultSize = Typography.Measure(result, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(max.X - inset - resultSize.X, centerY - resultSize.Y * 0.5f), result,
            ui.BodyInk, TextStyles.Body);
        var expressionWidth = MathF.Max(1f, width - inset * 2f - resultSize.X - TapeTextGap * scale);
        var expression = Typography.FitText(Localize(record.Expression), expressionWidth, TextStyles.Footnote);
        var expressionHeight = Typography.Measure(expression, TextStyles.Footnote).Y;
        Typography.Draw(drawList, new Vector2(origin.X + inset, centerY - expressionHeight * 0.5f), expression,
            ui.MutedInk, TextStyles.Footnote);
        return UiInteract.Click(origin, max, hovered);
    }

    private void DrawTapeFade(ImDrawListPtr drawList, Rect rect, float scale)
    {
        var fadeBottom = rect.Min.Y + TapeFadeHeight * scale;
        var backdrop = BackdropAt(rect.Min.Y);
        drawList.AddRectFilledMultiColor(rect.Min, new Vector2(rect.Max.X, fadeBottom),
            ImGui.GetColorU32(backdrop), ImGui.GetColorU32(backdrop),
            ImGui.GetColorU32(backdrop with { W = 0f }), ImGui.GetColorU32(backdrop with { W = 0f }));
    }

    private Vector4 BackdropAt(float y)
    {
        var palette = ui.Palette;
        var fraction = screenRect.Height <= 0f ? 0f : Math.Clamp((y - screenRect.Min.Y) / screenRect.Height, 0f, 1f);
        var body = Vector4.Lerp(palette.BackdropTop, palette.BackdropBottom, fraction);
        var bloom = Vector4.Lerp(palette.BloomTop, palette.BloomBottom, fraction);
        return Vector4.Lerp(body, bloom with { W = 1f }, Math.Clamp(bloom.W, 0f, 1f)) with { W = 1f };
    }

    private void HandleDisplayGesture(Rect live, float scale)
    {
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(live.Min);
        ImGui.InvisibleButton("##calculator.display", Vector2.Max(live.Size, Vector2.One));
        var itemHovered = ImGui.IsItemHovered();
        var activated = ImGui.IsItemActivated();
        var active = ImGui.IsItemActive();
        ImGui.SetCursorScreenPos(cursor);
        var hovered = itemHovered && UiInteract.Hover(live.Min, live.Max);
        var mouse = ImGui.GetMousePos();
        if (hovered && activated)
        {
            gestureTracking = true;
            gestureConsumed = false;
            gestureOrigin = mouse;
            gestureSeconds = 0f;
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenMenu();
        }

        if (!gestureTracking)
        {
            return;
        }

        if (!active)
        {
            gestureTracking = false;
            return;
        }

        gestureSeconds += delta;
        if (gestureConsumed)
        {
            return;
        }

        var travel = mouse - gestureOrigin;
        if (MathF.Abs(travel.X) >= SwipeDistance * scale && MathF.Abs(travel.X) > MathF.Abs(travel.Y))
        {
            gestureConsumed = true;
            if (engine.CanBackspace)
            {
                engine.Backspace();
                Play(UiSound.KeystrokeDelete);
            }

            return;
        }

        var slop = HoldSlop * scale;
        if (gestureSeconds >= LongPressSeconds && travel.LengthSquared() <= slop * slop)
        {
            gestureConsumed = true;
            OpenMenu();
        }
    }

    private void OpenMenu()
    {
        menuCanPaste = CalculatorText.TryParsePasted(ImGui.GetClipboardText(), marks, out menuPasteValue);
        menuOpen = true;
        Play(UiSound.Tap);
    }

    private void DrawMenu(Rect resultRect, Rect live, float scale)
    {
        var alpha = menuAppear.Step(menuOpen ? 1f : 0f, Motion.Appear, delta);
        if (alpha <= MenuHiddenAlpha)
        {
            return;
        }

        var copyLabel = Loc.T(L.Calculator.Copy);
        var pasteLabel = Loc.T(L.Calculator.Paste);
        var pad = MenuSegmentPad * scale;
        var copyWidth = Typography.Measure(copyLabel, TextStyles.SubheadlineEmphasized).X + pad * 2f;
        var pasteWidth = Typography.Measure(pasteLabel, TextStyles.SubheadlineEmphasized).X + pad * 2f;
        var total = copyWidth + pasteWidth;
        var height = MenuHeight * scale;
        var centerX = Math.Clamp(resultRect.Center.X, live.Min.X + total * 0.5f,
            MathF.Max(live.Min.X + total * 0.5f, live.Max.X - total * 0.5f));
        var bottom = resultRect.Min.Y - MenuGap * scale;
        var min = new Vector2(centerX - total * 0.5f, bottom - height);
        menuRect = new Rect(min, new Vector2(min.X + total, bottom));
        if (menuOpen && UiInteract.ClickedOutside(menuRect.Min, menuRect.Max))
        {
            menuOpen = false;
        }

        var copyRect = new Rect(menuRect.Min, new Vector2(menuRect.Min.X + copyWidth, menuRect.Max.Y));
        var pasteRect = new Rect(new Vector2(copyRect.Max.X, menuRect.Min.Y), menuRect.Max);
        var copied = false;
        var pasted = false;
        var shadow = new Vector2(MenuShadowMargin * scale, MenuShadowMargin * scale);
        using (ScreenLayer.Begin("calculator.menu", new Rect(menuRect.Min - shadow, menuRect.Max + shadow), false))
        {
            var drawList = ImGui.GetWindowDrawList();
            var radius = height * 0.5f;
            Elevation.Floating(drawList, menuRect.Min, menuRect.Max, radius, scale, alpha);
            Material.LiquidGlass(drawList, menuRect.Min, menuRect.Max, radius, scale, GlassTone.Dark, 0f, alpha);
            drawList.AddLine(new Vector2(copyRect.Max.X, menuRect.Min.Y + height * 0.25f),
                new Vector2(copyRect.Max.X, menuRect.Max.Y - height * 0.25f),
                ImGui.GetColorU32(Palette.WithAlpha(White, MenuDividerAlpha * alpha)), Metrics.Stroke.Hairline);
            copied = DrawMenuSegment(drawList, copyRect, copyLabel, !engine.IsError, alpha, radius);
            pasted = DrawMenuSegment(drawList, pasteRect, pasteLabel, menuCanPaste, alpha, radius);
        }

        if (copied)
        {
            CopyResult();
        }
        else if (pasted)
        {
            PasteValue(menuPasteValue);
        }
    }

    private bool DrawMenuSegment(ImDrawListPtr drawList, Rect rect, string label, bool enabled, float alpha,
        float radius)
    {
        var interactive = menuOpen && enabled;
        var hovered = interactive && UiInteract.HoverWindowOnly(rect.Min, rect.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(White, TapeHoverAlpha * alpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var inkAlpha = (enabled ? 1f : DisabledAlpha) * alpha;
        Typography.DrawCentered(drawList, rect.Center, label, Palette.WithAlpha(White, inkAlpha),
            TextStyles.SubheadlineEmphasized);
        return interactive && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void CopyResult()
    {
        menuOpen = false;
        if (engine.IsError)
        {
            Play(UiSound.Blocked);
            return;
        }

        ImGui.SetClipboardText(CalculatorText.ForClipboard(engine.Display, marks));
        ShellToast.Show();
    }

    private void PasteFromClipboard()
    {
        if (!CalculatorText.TryParsePasted(ImGui.GetClipboardText(), marks, out var value))
        {
            Play(UiSound.Blocked);
            return;
        }

        PasteValue(value);
    }

    private void PasteValue(double value)
    {
        menuOpen = false;
        engine.Enter(value);
        Play(UiSound.Keystroke);
    }

    private static DateTime LocalDay(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().Date;
}
