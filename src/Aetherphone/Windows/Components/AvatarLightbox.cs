using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class AvatarLightbox
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float RevealSmoothTime = 0.16f;
    private const float BackdropAlpha = 0.92f;
    private const float EdgePadding = 26f;
    private const int CircleSegments = 96;

    private Spring reveal;
    private Func<IDalamudTextureWrap?>? source;
    private Vector2 originCenter;
    private float originRadius;
    private int openedFrame = -1;
    private bool open;

    public bool Active => open || reveal.Value > 0.01f;

    public bool Expanded => open;

    public bool TryOpen(Vector2 center, float radius, string? avatarUrl, RemoteImageCache images)
    {
        if (string.IsNullOrEmpty(avatarUrl))
        {
            return false;
        }

        if (!UiInteract.HoverClickCircle(center, radius))
        {
            return false;
        }

        Open(center, radius, () => images.Get(avatarUrl));
        return true;
    }

    public void Open(Vector2 center, float radius, Func<IDalamudTextureWrap?> textureSource)
    {
        source = textureSource;
        originCenter = center;
        originRadius = radius;
        openedFrame = ImGui.GetFrameCount();
        open = true;
    }

    public void Close() => open = false;

    public void Reset()
    {
        open = false;
        source = null;
        reveal.SnapTo(0f);
    }

    public void Draw(Rect area, PhoneTheme theme)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, RevealSmoothTime, delta);
        var progress = Easing.Clamp01(reveal.Value);
        if (progress <= 0.01f)
        {
            if (!open)
            {
                source = null;
            }

            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetCursorScreenPos(area.Min);
        using (ImRaii.Child("##avatarLightbox", area.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(area.Min, area.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, BackdropAlpha * progress)));
            var eased = Easing.EaseOutQuint(progress);
            var targetRadius = MathF.Min(area.Size.X, area.Size.Y) * 0.5f - EdgePadding * scale;
            var center = Vector2.Lerp(originCenter, area.Center, eased);
            var radius = Easing.Lerp(originRadius, targetRadius, eased);
            var texture = source?.Invoke();
            if (texture is not null)
            {
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(theme.SurfaceMuted), CircleSegments);
                var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                var corner = new Vector2(radius, radius);
                drawList.AddImageRounded(texture.Handle, center - corner, center + corner, uv0, uv1, 0xFFFFFFFFu,
                    radius, ImDrawFlags.RoundCornersAll);
            }
            else
            {
                LoadingPulse.Draw(center, 13f * scale, theme.Accent, theme.TextMuted, Loc.T(L.Common.Loading));
            }
        }

        if (!open || ImGui.GetFrameCount() == openedFrame)
        {
            return;
        }

        if (ImGui.IsMouseHoveringRect(area.Min, area.Max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            Close();
        }
    }
}
