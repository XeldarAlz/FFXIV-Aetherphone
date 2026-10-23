using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal enum HoverLabelSide : byte
{
    Below,
    Above,
}

internal static class HoverButton
{
    private const float GlyphFraction = 0.9f;
    private const float RimLightAlpha = 0.5f;
    private const float AccentSaturation = 0.12f;
    private const float SolidAccentAlpha = 0.5f;
    private const float TintedAlpha = 0.20f;
    private const float TintedHoverAlpha = 0.28f;
    private const float ScrimLuminance = 0.25f;
    private const float ScrimAlpha = 0.3f;
    private const float ScrimHoverLift = 0.08f;
    private const float ProminentHoverLift = 0.10f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static bool Circle(ImDrawListPtr drawList, string id, Vector2 center, float radius, FontAwesomeIcon icon,
        Vector4 tint, Vector4 ink, float delta, float alpha, bool interactive, string? label = null,
        HoverLabelSide side = HoverLabelSide.Below)
    {
        var extent = new Vector2(radius, radius);
        var rect = new Rect(center - extent, center + extent);
        var hovered = interactive && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var pose = MotionButton.Animate(rect, (int)ImGui.GetID(id), hovered, pressed);
        var face = pose.Face;
        var faceRadius = face.Height * 0.5f;
        var ghost = tint.W <= 0f;
        var fill = FillFor(tint, ink, ghost, pose.Hover);
        if (fill.W > 0.001f)
        {
            Squircle.Fill(drawList, face.Min, face.Max, faceRadius, ImGui.GetColorU32(fill with { W = fill.W * alpha }));
        }

        if (!ghost)
        {
            MotionButton.RimLight(drawList, face, faceRadius, pose.Hover * RimLightAlpha * alpha);
        }

        ProgressRing.CenterIcon(drawList, center, icon, Palette.WithAlpha(ink, alpha),
            faceRadius * GlyphFraction);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (label is not null && pose.Hover > 0.02f)
        {
            HoverTooltip.Enqueue(face, label, pose.Hover * alpha, side);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static Vector4 FillFor(Vector4 tint, Vector4 ink, bool ghost, float hover)
    {
        if (ghost)
        {
            var wash = Surfaces.Fill(ink with { W = 1f }, FillLevel.Tertiary);
            return wash with { W = wash.W * hover };
        }

        var saturation = MathF.Max(tint.X, MathF.Max(tint.Y, tint.Z)) - MathF.Min(tint.X, MathF.Min(tint.Y, tint.Z));
        if (saturation > AccentSaturation)
        {
            if (tint.W >= SolidAccentAlpha)
            {
                return Palette.Mix(tint, White, ProminentHoverLift * hover) with { W = tint.W };
            }

            return tint with { W = TintedAlpha + (TintedHoverAlpha - TintedAlpha) * hover };
        }

        if (Palette.Luminance(tint) < ScrimLuminance && tint.W >= ScrimAlpha)
        {
            return Palette.Mix(tint, White, ScrimHoverLift * hover) with { W = tint.W };
        }

        var ladderInk = ink with { W = 1f };
        return Vector4.Lerp(Surfaces.Fill(ladderInk, FillLevel.Secondary), Surfaces.Fill(ladderInk, FillLevel.Primary),
            hover);
    }
}
