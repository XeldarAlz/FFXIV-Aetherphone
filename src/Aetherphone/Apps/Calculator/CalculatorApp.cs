using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Calculator;

internal sealed class CalculatorApp : IPhoneApp
{
    private static readonly Vector4 DigitBg = new(0.22f, 0.22f, 0.24f, 1f);
    private static readonly Vector4 FunctionBg = new(0.64f, 0.64f, 0.67f, 1f);
    private static readonly Vector4 FunctionInk = new(0.06f, 0.06f, 0.08f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly float[] DisplayScales = { 3.0f, 2.6f, 2.25f, 1.95f, 1.65f, 1.4f, 1.2f };
    private static readonly float[] ExpressionScales = { 1.45f, 1.25f, 1.05f, 0.9f, 0.8f };

    public string Id => "calculator";
    public string DisplayName => Loc.T(L.Apps.Calculator);
    public string Glyph => "=";
    public Vector4 Accent => AppAccents.For("calculator");
    public int BadgeCount => 0;

    private readonly CalculatorEngine engine = new();
    private readonly AppSkin ui = new(AppPalettes.Calculator);
    private int lastHistoryCount;
    private bool scrollHistoryToBottom;

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ui.Theme = context.Theme;
        ui.Palette = AppPalettes.Calculator;
        var content = context.Content;
        var screen = SceneChrome.ScreenFrom(content, context.Theme, scale);
        ui.Backdrop(screen);
        AppHeader.Draw(context, DisplayName);

        var top = content.Min.Y + AppHeader.Height * scale;
        var margin = 14f * scale;
        var gap = 9f * scale;
        var gridLeft = content.Min.X + margin;
        var gridRight = content.Max.X - margin;
        var gridWidth = gridRight - gridLeft;
        var button = (gridWidth - 3f * gap) / 4f;
        var gridHeight = 5f * button + 4f * gap;
        var gridBottom = content.Max.Y - margin;
        var gridTop = gridBottom - gridHeight;

        var displayRect = new Rect(new Vector2(gridLeft, top + 6f * scale),
            new Vector2(gridRight, gridTop - gap));
        DrawDisplayArea(displayRect, scale);
        DrawKeypad(gridLeft, gridTop, button, gap, scale);
    }

    private void DrawDisplayArea(Rect rect, float scale)
    {
        var history = engine.History;
        if (history.Count != lastHistoryCount)
        {
            scrollHistoryToBottom = true;
            lastHistoryCount = history.Count;
        }

        var maxLive = rect.Height - 54f * scale;
        if (history.Count == 0 || maxLive <= 92f * scale)
        {
            DrawLive(rect, scale);
            return;
        }

        var liveHeight = Math.Clamp(rect.Height * 0.44f, 92f * scale, maxLive);
        var historyRect = new Rect(rect.Min, new Vector2(rect.Max.X, rect.Max.Y - liveHeight));
        var liveRect = new Rect(new Vector2(rect.Min.X, historyRect.Max.Y), rect.Max);
        DrawHistoryTape(historyRect, scale);
        ImGui.GetWindowDrawList().AddLine(new Vector2(historyRect.Min.X, historyRect.Max.Y),
            new Vector2(historyRect.Max.X, historyRect.Max.Y), ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.22f)),
            1f);
        DrawLive(liveRect, scale);
    }

    private void DrawLive(Rect rect, float scale)
    {
        var result = engine.Display;
        var resultScale = FitScale(result, rect.Width, DisplayScales);
        var resultSize = Typography.Measure(result, resultScale, FontWeight.Regular);
        var resultPosition = new Vector2(rect.Max.X - resultSize.X, rect.Max.Y - resultSize.Y);

        var expression = engine.Expression;
        if (expression.Length > 0)
        {
            var expressionScale = FitScale(expression, rect.Width, ExpressionScales);
            var expressionSize = Typography.Measure(expression, expressionScale, FontWeight.Regular);
            Typography.Draw(new Vector2(rect.Max.X - expressionSize.X, resultPosition.Y - expressionSize.Y - 2f * scale),
                expression, ui.MutedInk, expressionScale, FontWeight.Regular);
        }

        Typography.Draw(resultPosition, result, ui.TitleInk, resultScale, FontWeight.Regular);
    }

    private void DrawHistoryTape(Rect rect, float scale)
    {
        var history = engine.History;
        using (AppSurface.Begin(rect))
        {
            var width = ImGui.GetContentRegionAvail().X;
            var rowHeight = 30f * scale;
            var drawList = ImGui.GetWindowDrawList();
            for (var index = history.Count - 1; index >= 0; index--)
            {
                var entry = history[index];
                var origin = ImGui.GetCursorScreenPos();
                var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + rowHeight));
                var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
                if (hovered)
                {
                    Squircle.Fill(drawList, row.Min, row.Max, 6f * scale,
                        ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)));
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                var resultSize = Typography.Measure(entry.Result, TextStyles.SubheadlineEmphasized);
                Typography.Draw(new Vector2(row.Max.X - resultSize.X, row.Center.Y - resultSize.Y * 0.5f), entry.Result,
                    ui.BodyInk, TextStyles.SubheadlineEmphasized);

                var expressionRight = row.Max.X - resultSize.X - 10f * scale;
                ImGui.PushClipRect(new Vector2(row.Min.X, row.Min.Y), new Vector2(expressionRight, row.Max.Y), true);
                var expressionSize = Typography.Measure(entry.Expression, TextStyles.Caption1);
                Typography.Draw(new Vector2(row.Min.X, row.Center.Y - expressionSize.Y * 0.5f), entry.Expression,
                    ui.MutedInk, TextStyles.Caption1);
                ImGui.PopClipRect();

                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(width, rowHeight));
                if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    engine.Recall(entry.Result);
                }
            }

            if (scrollHistoryToBottom)
            {
                ImGui.SetScrollHereY(1f);
                scrollHistoryToBottom = false;
            }
        }
    }

    private static float FitScale(string text, float maxWidth, float[] scales)
    {
        for (var index = 0; index < scales.Length; index++)
        {
            if (Typography.Measure(text, scales[index], FontWeight.Regular).X <= maxWidth)
            {
                return scales[index];
            }
        }

        return scales[scales.Length - 1];
    }

    private void DrawKeypad(float gridLeft, float gridTop, float button, float gap, float scale)
    {
        var radius = button * 0.5f;
        var active = engine.ActiveOperator;

        var clearLabel = engine.ShowAllClear ? "AC" : "C";
        if (DrawKey(Center(gridLeft, gridTop, button, gap, 0, 0), radius, clearLabel, FunctionBg, FunctionInk, 1.15f))
        {
            engine.Clear();
        }

        if (DrawKey(Center(gridLeft, gridTop, button, gap, 0, 1), radius, "±", FunctionBg, FunctionInk, 1.3f))
        {
            engine.Negate();
        }

        if (DrawKey(Center(gridLeft, gridTop, button, gap, 0, 2), radius, "%", FunctionBg, FunctionInk, 1.25f))
        {
            engine.Percent();
        }

        DrawOperator(gridLeft, gridTop, button, gap, 0, 3, "÷", CalcOp.Divide, active, radius);

        DrawDigitRow(gridLeft, gridTop, button, gap, 1, 7, 8, 9, radius);
        DrawOperator(gridLeft, gridTop, button, gap, 1, 3, "×", CalcOp.Multiply, active, radius);

        DrawDigitRow(gridLeft, gridTop, button, gap, 2, 4, 5, 6, radius);
        DrawOperator(gridLeft, gridTop, button, gap, 2, 3, "-", CalcOp.Subtract, active, radius);

        DrawDigitRow(gridLeft, gridTop, button, gap, 3, 1, 2, 3, radius);
        DrawOperator(gridLeft, gridTop, button, gap, 3, 3, "+", CalcOp.Add, active, radius);

        DrawZeroKey(gridLeft, gridTop, button, gap, radius);
        if (DrawKey(Center(gridLeft, gridTop, button, gap, 4, 2), radius, ".", DigitBg, White, 1.6f))
        {
            engine.InputDecimal();
        }

        DrawOperator(gridLeft, gridTop, button, gap, 4, 3, "=", CalcOp.None, active, radius, isEquals: true);
    }

    private void DrawDigitRow(float gridLeft, float gridTop, float button, float gap, int row, int first, int second,
        int third, float radius)
    {
        Span<int> digits = stackalloc int[3] { first, second, third };
        for (var column = 0; column < 3; column++)
        {
            var digit = digits[column];
            if (DrawKey(Center(gridLeft, gridTop, button, gap, row, column), radius, digit.ToString(), DigitBg, White,
                    1.6f))
            {
                engine.InputDigit(digit);
            }
        }
    }

    private void DrawOperator(float gridLeft, float gridTop, float button, float gap, int row, int column, string label,
        CalcOp op, CalcOp active, float radius, bool isEquals = false)
    {
        var highlighted = !isEquals && op != CalcOp.None && active == op;
        var background = highlighted ? White : ui.Accent;
        var ink = highlighted ? ui.Accent : White;
        if (DrawKey(Center(gridLeft, gridTop, button, gap, row, column), radius, label, background, ink, 1.7f))
        {
            if (isEquals)
            {
                engine.Equals();
            }
            else
            {
                engine.SetOperator(op);
            }
        }
    }

    private void DrawZeroKey(float gridLeft, float gridTop, float button, float gap, float radius)
    {
        var left = gridLeft;
        var top = gridTop + 4 * (button + gap);
        var min = new Vector2(left, top);
        var max = new Vector2(left + button * 2f + gap, top + button);
        var center = new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var fill = hovered ? Palette.Mix(DigitBg, White, 0.14f) : DigitBg;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(fill));
        var textCenter = new Vector2(min.X + radius, center.Y);
        Typography.DrawCentered(drawList, textCenter, "0", White, 1.6f, FontWeight.Regular);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            engine.InputDigit(0);
        }
    }

    private static Vector2 Center(float gridLeft, float gridTop, float button, float gap, int row, int column)
    {
        return new Vector2(gridLeft + column * (button + gap) + button * 0.5f,
            gridTop + row * (button + gap) + button * 0.5f);
    }

    private static bool DrawKey(Vector2 center, float radius, string label, Vector4 background, Vector4 ink,
        float labelScale)
    {
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var fill = hovered ? Palette.Mix(background, White, 0.14f) : background;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 40);
        Typography.DrawCentered(drawList, center, label, ink, labelScale, FontWeight.Medium);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public void Dispose()
    {
    }
}
