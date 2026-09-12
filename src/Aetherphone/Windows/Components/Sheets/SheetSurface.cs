using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class SheetSurface
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                 ImGuiWindowFlags.NoBackground;

    private const float RevealSmoothTime = 0.16f;
    private const float MaxDim = 0.5f;
    private const float PanelRounding = 28f;
    private const float GrabberWidth = 36f;
    private const float GrabberHeight = 5f;

    private readonly string id;
    private Spring reveal;
    private bool open;
    private int openedFrame;

    internal SheetSurface(string id)
    {
        this.id = id;
    }

    internal bool IsOpen => open;

    internal static float ChromeHeight()
    {
        var scale = UiScale.Current;
        return (Metrics.Space.Md + GrabberHeight + Metrics.Space.Md + Metrics.Space.Md + Metrics.Space.Lg) * scale +
               Typography.LineHeight(TextStyles.Headline);
    }

    internal bool CapturesPointer => open || !reveal.IsResting(0f, 0.001f, 0.005f);

    internal void Open()
    {
        if (open)
        {
            return;
        }

        open = true;
        openedFrame = ImGui.GetFrameCount();
    }

    internal void Close() => open = false;

    internal void Toggle()
    {
        if (open)
        {
            Close();
            return;
        }

        Open();
    }

    internal void Draw(Rect screen, PhoneTheme theme, string title, float heightFraction, Action<Rect> drawContent) =>
        Draw(screen, SheetSkin.From(theme), title, heightFraction, drawContent);

    internal void Draw(Rect screen, in SheetSkin skin, string title, float heightFraction, Action<Rect> drawContent)
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

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##sheet" + id, screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));

            var panelHeight = screen.Height * Math.Clamp(heightFraction, 0.2f, 0.95f);
            var panelBottom = screen.Max.Y + panelHeight * (1f - slide);
            var panelTop = panelBottom - panelHeight;
            var panelMin = new Vector2(screen.Min.X, panelTop);
            var panelMax = new Vector2(screen.Max.X, panelBottom);
            var rounding = PanelRounding * scale;

            Squircle.Fill(drawList, panelMin, panelMax, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(skin.Panel, opacity)));
            Squircle.Stroke(drawList, panelMin, panelMax, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(skin.Stroke, skin.Stroke.W * opacity)), Metrics.Stroke.Hairline);

            var grabberWidth = GrabberWidth * scale;
            var grabberHeight = GrabberHeight * scale;
            var grabberMin = new Vector2(screen.Center.X - grabberWidth * 0.5f, panelTop + Metrics.Space.Md * scale);
            drawList.AddRectFilled(grabberMin, grabberMin + new Vector2(grabberWidth, grabberHeight),
                ImGui.GetColorU32(Palette.WithAlpha(skin.Grabber, skin.Grabber.W * opacity)), grabberHeight * 0.5f);

            var titleHeight = Typography.LineHeight(TextStyles.Headline);
            var titleY = grabberMin.Y + grabberHeight + Metrics.Space.Md * scale;
            Typography.DrawCentered(drawList, new Vector2(screen.Center.X, titleY + titleHeight * 0.5f), title,
                Palette.WithAlpha(skin.Title, skin.Title.W * opacity), TextStyles.Headline);

            var contentTop = titleY + titleHeight + Metrics.Space.Md * scale;
            var margin = Metrics.Space.Lg * scale;
            var content = new Rect(new Vector2(panelMin.X + margin, contentTop),
                new Vector2(panelMax.X - margin, panelMax.Y - Metrics.Space.Lg * scale));

            if (opacity > 0.5f)
            {
                drawContent(content);
            }

            if (!open || opacity <= 0.5f)
            {
                return;
            }

            if (ImGui.GetFrameCount() != openedFrame && UiInteract.ClickedOutside(panelMin, panelMax))
            {
                Close();
            }
        }
    }
}
