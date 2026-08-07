using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal sealed class PhotoViewerOverlay
{
    private const float RevealSmoothTime = 0.15f;
    private const float BottomInset = 16f;
    private readonly PhotoZoomView zoomView = new();
    private Aetherphone.Core.Animation.Spring reveal;
    private Func<IDalamudTextureWrap?>? source;
    private IPhoneApp? owner;
    private bool open;

    public bool Active => open || reveal.Value > 0.01f;

    public void Open(IPhoneApp app, Func<IDalamudTextureWrap?> textureSource)
    {
        source = textureSource;
        owner = app;
        zoomView.Reset();
        open = true;
    }

    public void Close() => open = false;

    public void Draw(Rect area, PhoneTheme theme)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, Aetherphone.Core.Animation.TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, RevealSmoothTime, delta);
        var eased = Math.Clamp(reveal.Value, 0f, 1f);
        if (eased <= 0.01f)
        {
            if (!open)
            {
                source = null;
                owner = null;
            }

            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.96f * eased)));
        var grow = 0.94f + 0.06f * Aetherphone.Core.Animation.Easing.EaseOutCubic(eased);
        var contentTop = area.Min.Y + theme.TopZoneHeight * scale;
        var contentLeft = area.Min.X + theme.SidePadding * scale;
        var headerBottom = contentTop + AppHeader.Height * scale;
        var controlsBottom = area.Max.Y - BottomInset * scale;
        var stageTarget = new Rect(new Vector2(area.Min.X, headerBottom),
            new Vector2(area.Max.X, controlsBottom - PhotoZoomView.ControlBandUnits * scale));
        var stageHalf = stageTarget.Size * 0.5f * grow;
        var stage = new Rect(stageTarget.Center - stageHalf, stageTarget.Center + stageHalf);
        var controls = new Rect(new Vector2(stageTarget.Min.X, stageTarget.Max.Y),
            new Vector2(stageTarget.Max.X, controlsBottom));
        var texture = source?.Invoke();
        if (texture is not null)
        {
            if (zoomView.Draw(stage, texture, theme, Metrics.Radius.Sm * scale, open && eased > 0.9f, controls) &&
                source is not null && owner is not null)
            {
                Plugin.PhotoWindow.Open(source, owner);
            }
        }
        else
        {
            LoadingPulse.Draw(new Vector2(stage.Center.X, stage.Center.Y - 14f * scale), 13f * scale, theme.Accent,
                theme.TextMuted, Loc.T(L.Common.Loading));
        }

        var rowCenterY = contentTop + AppHeader.Height * scale * 0.5f;
        var hitMin = new Vector2(contentLeft, contentTop);
        var hitMax = new Vector2(contentLeft + 44f * scale, headerBottom);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var backCenter = new Vector2(contentLeft + 13f * scale, rowCenterY);
        if (BackButton.Draw("photoviewer.back", backCenter, 15f * scale, new Vector4(1f, 1f, 1f, 0.95f), hovered, scale,
                shadow: true))
        {
            Close();
        }
    }
}
