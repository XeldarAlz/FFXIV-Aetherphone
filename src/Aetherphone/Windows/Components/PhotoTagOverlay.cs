using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal readonly record struct PhotoTagOverlayResult(bool InputConsumed, string? OpenUserId);

internal sealed class PhotoTagOverlay
{
    private const float RevealSeconds = 0.16f;
    private const float ChipRadius = 12f;
    private const float PillHeightFactor = 0.6f;

    private static readonly Vector4 BadgeSurface = new(0f, 0f, 0f, 0.55f);

    private readonly HashSet<string> revealed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> reveals = new(StringComparer.Ordinal);

    public void Clear()
    {
        revealed.Clear();
        reveals.Clear();
    }

    public PhotoTagOverlayResult Draw(ImDrawListPtr drawList, Rect frame, string postId, int photoIndex,
        PhotoTagDto[]? tags, PhoneTheme theme, float deltaSeconds)
    {
        if (PhotoTagGeometry.CountFor(tags, photoIndex) == 0)
        {
            return new PhotoTagOverlayResult(false, null);
        }

        var scale = ImGuiHelpers.GlobalScale;
        var isRevealed = revealed.Contains(postId);
        reveals.TryGetValue(postId, out var reveal);
        var target = isRevealed ? 1f : 0f;
        var step = deltaSeconds / RevealSeconds;
        reveal = target > reveal ? MathF.Min(target, reveal + step) : MathF.Max(target, reveal - step);
        reveals[postId] = reveal;

        var consumed = false;
        string? openUserId = null;

        var chipCenter = new Vector2(frame.Min.X + 14f * scale + ChipRadius * scale,
            frame.Max.Y - 14f * scale - ChipRadius * scale);
        drawList.AddCircleFilled(chipCenter, ChipRadius * scale, ImGui.GetColorU32(BadgeSurface), 24);
        AppSkin.Icon(chipCenter, FontAwesomeIcon.User.ToIconString(), new Vector4(1f, 1f, 1f, 0.92f), 0.62f);
        var chipMin = chipCenter - new Vector2(ChipRadius * scale, ChipRadius * scale);
        var chipMax = chipCenter + new Vector2(ChipRadius * scale, ChipRadius * scale);
        if (UiInteract.HoverClick(chipMin, chipMax))
        {
            if (!revealed.Add(postId))
            {
                revealed.Remove(postId);
            }

            consumed = true;
        }

        if (reveal <= 0.001f)
        {
            return new PhotoTagOverlayResult(consumed, null);
        }

        var eased = Easing.EaseOutQuint(reveal);
        var alpha = Easing.SmoothStep(Math.Clamp(reveal / 0.7f, 0f, 1f));
        for (var index = 0; index < tags!.Length; index++)
        {
            var tag = tags[index];
            if (tag.PhotoIndex != photoIndex)
            {
                continue;
            }

            var anchor = PhotoTagGeometry.ToScreen(frame, tag.X, tag.Y);
            if (DrawPill(drawList, frame, anchor, tag, theme, eased, alpha, scale))
            {
                openUserId = tag.UserId;
                consumed = true;
            }
        }

        return new PhotoTagOverlayResult(consumed, openUserId);
    }

    private static bool DrawPill(ImDrawListPtr drawList, Rect frame, Vector2 anchor, PhotoTagDto tag,
        PhoneTheme theme, float eased, float alpha, float scale)
    {
        var label = SocialIdentity.Name(tag.DisplayName, tag.Handle);
        var maxPillWidth = frame.Width * PillHeightFactor;
        var textStyle = TextStyles.FootnoteEmphasized;
        var text = Typography.FitText(label, maxPillWidth - 16f * scale, textStyle);
        var textSize = Typography.Measure(text, textStyle);
        var pillWidth = textSize.X + 16f * scale;
        var pillHeight = textSize.Y + 8f * scale;
        var nub = 4f * scale;

        var below = anchor.Y - pillHeight - nub < frame.Min.Y;
        var pillTop = below ? anchor.Y + nub : anchor.Y - nub - pillHeight;
        var pillLeft = Math.Clamp(anchor.X - pillWidth * 0.5f, frame.Min.X + 6f * scale,
            MathF.Max(frame.Min.X + 6f * scale, frame.Max.X - 6f * scale - pillWidth));

        var pivot = new Vector2(Math.Clamp(anchor.X, pillLeft, pillLeft + pillWidth), below ? pillTop : pillTop + pillHeight);
        var revealScale = 0.94f + 0.06f * eased;
        var min = pivot + (new Vector2(pillLeft, pillTop) - pivot) * revealScale;
        var max = pivot + (new Vector2(pillLeft + pillWidth, pillTop + pillHeight) - pivot) * revealScale;

        var fill = ImGui.GetColorU32(Palette.WithAlpha(BadgeSurface, BadgeSurface.W * alpha));
        var nubCenterX = Math.Clamp(anchor.X, min.X + 6f * scale, max.X - 6f * scale);
        if (below)
        {
            drawList.AddTriangleFilled(new Vector2(nubCenterX - nub, min.Y), new Vector2(nubCenterX + nub, min.Y),
                new Vector2(nubCenterX, min.Y - nub), fill);
        }
        else
        {
            drawList.AddTriangleFilled(new Vector2(nubCenterX - nub, max.Y), new Vector2(nubCenterX + nub, max.Y),
                new Vector2(nubCenterX, max.Y + nub), fill);
        }

        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f, fill);
        Typography.Draw(drawList, new Vector2(min.X + 8f * scale, min.Y + 4f * scale), text,
            Palette.WithAlpha(new Vector4(1f, 1f, 1f, 1f), alpha), textStyle);

        return alpha > 0.6f && UiInteract.HoverClick(min, max);
    }
}
