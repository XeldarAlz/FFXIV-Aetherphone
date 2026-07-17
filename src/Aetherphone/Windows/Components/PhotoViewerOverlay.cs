using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal sealed class PhotoViewerOverlay
{
    private const float RevealSmoothTime = 0.15f;
    private readonly PhotoZoomView zoomView = new();
    private Aetherphone.Core.Animation.Spring reveal;
    private Func<IDalamudTextureWrap?>? source;
    private bool open;

    public bool Active => open || reveal.Value > 0.01f;

    public void Open(Func<IDalamudTextureWrap?> textureSource)
    {
        source = textureSource;
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
            }

            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.96f * eased)));
        var grow = 0.94f + 0.06f * Aetherphone.Core.Animation.Easing.EaseOutCubic(eased);
        var contentTop = area.Min.Y + theme.TopZoneHeight * scale;
        var contentLeft = area.Min.X + theme.SidePadding * scale;
        var headerBottom = contentTop + AppHeader.Height * scale;
        var stageTarget = new Rect(new Vector2(area.Min.X, headerBottom),
            new Vector2(area.Max.X, area.Max.Y - 16f * scale));
        var stageHalf = stageTarget.Size * 0.5f * grow;
        var stage = new Rect(stageTarget.Center - stageHalf, stageTarget.Center + stageHalf);
        var texture = source?.Invoke();
        if (texture is not null)
        {
            zoomView.Draw(stage, texture, theme, Metrics.Radius.Sm * scale, open && eased > 0.9f);
        }
        else
        {
            LoadingPulse.Draw(new Vector2(stage.Center.X, stage.Center.Y - 14f * scale), 13f * scale, theme.Accent,
                theme.TextMuted, Loc.T(L.Common.Loading));
        }

        var rowCenterY = contentTop + AppHeader.Height * scale * 0.5f;
        var hitMin = new Vector2(contentLeft, contentTop);
        var hitMax = new Vector2(contentLeft + 44f * scale, headerBottom);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        var backCenter = new Vector2(contentLeft + 13f * scale, rowCenterY);
        if (BackButton.Draw("photoviewer.back", backCenter, 15f * scale, new Vector4(1f, 1f, 1f, 0.95f), hovered, scale,
                shadow: true))
        {
            Close();
        }
    }
}
