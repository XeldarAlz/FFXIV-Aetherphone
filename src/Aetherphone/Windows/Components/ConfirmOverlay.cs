using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class ConfirmOverlay
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

    private const float RevealSmoothTime = 0.16f;
    private const float MaxDim = 0.55f;
    private const float MinCardScale = 0.92f;
    private readonly ConfirmService service;
    private Spring reveal;
    private ConfirmRequest? shown;

    public ConfirmOverlay(ConfirmService service)
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

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var target = active is not null ? 1f : 0f;
        reveal.Step(target, RevealSmoothTime, delta);
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
        using (ImRaii.Child("##confirmOverlay", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
            ConfirmDialog.Draw(screen, theme, shown.Title, shown.Message, shown.ConfirmLabel, shown.CancelLabel,
                shown.BusyLabel ?? shown.ConfirmLabel, service.Busy, service.Status, shown.Danger, shown.Acknowledge,
                opacity, cardScale, out var cardRect, out var canceled, out var confirmed);
            if (active is null || opacity <= 0.5f)
            {
                return;
            }

            if (confirmed)
            {
                service.Proceed();
            }
            else if (canceled)
            {
                service.CancelActive();
            }
            else if (!service.Busy && ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
                     !ImGui.IsMouseHoveringRect(cardRect.Min, cardRect.Max))
            {
                service.CancelActive();
            }
        }
    }
}
