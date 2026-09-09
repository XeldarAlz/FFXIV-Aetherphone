using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class InfoSheet
{
    public const int MaxParagraphs = 6;

    private const float RevealSmoothTime = 0.11f;
    private const float MaxDim = 0.45f;
    private const float Rounding = 24f;
    private const float PadX = 22f;
    private const float GrabberWidth = 38f;
    private const float GrabberHeight = 4.5f;
    private const float TopInset = 0.14f;
    private const float ParagraphGap = 14f;
    private const float DoneHeight = 44f;
    private const float BottomPad = 30f;
    private const float PanelLift = 0.08f;
    private const float PanelAlpha = 0.96f;
    private const float LineSpacing = 1.3f;

    private static readonly TextStyle TitleStyle = new(1.13f, FontWeight.Bold);
    private static readonly TextStyle BodyStyle = new(0.95f, FontWeight.Regular);
    private static readonly TextStyle DoneStyle = new(1f, FontWeight.Bold);
    private static readonly Vector4 GrabberFill = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 PanelStroke = new(1f, 1f, 1f, 0.10f);

    private Spring reveal;
    private bool open;
    private int openedFrame;

    public bool IsOpen => open;

    public bool CapturesPointer => open || !reveal.IsResting(0f, 0.001f, 0.005f);

    public void Open()
    {
        if (!open)
        {
            openedFrame = ImGui.GetFrameCount();
        }

        open = true;
    }

    public void Close() => open = false;

    public void Gate()
    {
        if (open)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, SocialInk ink, string title, ReadOnlySpan<string> paragraphs, string doneLabel)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, RevealSmoothTime, delta);
        if (!open && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            return;
        }

        var scale = UiScale.Current;
        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        var slide = Easing.EaseOutQuint(opacity);
        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, false);
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));

        var panelHeight = screen.Height * (1f - TopInset);
        var panelTop = screen.Max.Y - panelHeight + panelHeight * (1f - slide);
        var panelMin = new Vector2(screen.Min.X, panelTop);
        var panelMax = new Vector2(screen.Max.X, screen.Max.Y + Rounding * scale);
        var rounding = Rounding * scale;
        var panelFill = Palette.WithAlpha(Palette.Lighten(ink.BackdropTop, PanelLift), PanelAlpha * opacity);
        Squircle.Fill(drawList, panelMin, panelMax, rounding, ImGui.GetColorU32(panelFill));
        Squircle.Stroke(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(PanelStroke, PanelStroke.W * opacity)), 1f);
        var interactive = open && opacity > 0.5f;

        var grabberMin = new Vector2(screen.Center.X - GrabberWidth * scale * 0.5f, panelTop + 8f * scale);
        drawList.AddRectFilled(grabberMin, grabberMin + new Vector2(GrabberWidth, GrabberHeight) * scale,
            ImGui.GetColorU32(Palette.WithAlpha(GrabberFill, GrabberFill.W * opacity)), GrabberHeight * scale * 0.5f);

        var padX = PadX * scale;
        var left = panelMin.X + padX;
        var right = panelMax.X - padX;
        var maxWidth = right - left;
        var cursorY = grabberMin.Y + (GrabberHeight + 16f) * scale;
        var titleHeight = Typography.LineHeight(TitleStyle);
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, cursorY + titleHeight * 0.5f),
            Typography.FitText(title, maxWidth, TitleStyle), Palette.WithAlpha(ink.TitleInk, opacity), TitleStyle);
        cursorY += titleHeight + 18f * scale;

        var doneTop = screen.Max.Y - BottomPad * scale - DoneHeight * scale;
        var bodyInk = Palette.WithAlpha(ink.BodyInk, opacity);
        var count = Math.Min(paragraphs.Length, MaxParagraphs);
        drawList.PushClipRect(new Vector2(left, panelTop), new Vector2(right, doneTop - 12f * scale), true);
        for (var index = 0; index < count && cursorY < doneTop; index++)
        {
            var height = Typography.DrawWrappedCentered(drawList, paragraphs[index], BodyStyle, bodyInk,
                new Vector2(screen.Center.X, cursorY), maxWidth, LineSpacing);
            cursorY += height + ParagraphGap * scale;
        }

        drawList.PopClipRect();

        var doneMin = new Vector2(left, doneTop);
        var doneMax = new Vector2(right, doneTop + DoneHeight * scale);
        var doneHovered = interactive && UiInteract.HoverWindowOnly(doneMin, doneMax, false);
        AccentPill.Paint(drawList, doneMin, doneMax, DoneHeight * scale * 0.5f, doneHovered, ink.Accent,
            ink.AccentDeep, ink.AccentShadow, opacity);
        Typography.DrawCentered(drawList, (doneMin + doneMax) * 0.5f, doneLabel, Palette.WithAlpha(ink.White, opacity),
            DoneStyle);
        if (doneHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                UiFeedback.Play(UiSound.Tap);
                Close();
            }
        }

        drawList.PopClipRect();
        if (interactive && ImGui.GetFrameCount() != openedFrame && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && !UiInteract.HoverWindowOnly(panelMin, panelMax, false))
        {
            Close();
        }
    }
}
