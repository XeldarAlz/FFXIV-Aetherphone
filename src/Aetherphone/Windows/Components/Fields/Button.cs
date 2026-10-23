using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum ButtonStyle : byte
{
    Prominent,
    Tinted,
    Gray,
    Plain,
    Bare,
    Solid,
}

internal enum ButtonSize : byte
{
    Small,
    Regular,
    Large,
}

internal enum ButtonRole : byte
{
    Normal,
    Destructive,
}

internal readonly record struct ButtonFace(Rect Face, Vector4 LabelInk, bool Hovered);

internal static class Button
{
    public const float SmallHeight = 28f;
    public const float RegularHeight = 34f;
    public const float LargeHeight = Metrics.Size.Pill;

    private const float SmallLabelCeiling = 31f;
    private const float RegularLabelCeiling = 39f;
    private const float DisabledAlpha = 0.4f;
    private const float TintedAlpha = 0.20f;
    private const float TintedHoverAlpha = 0.28f;
    private const float ProminentHoverLift = 0.10f;
    private const float ProminentTopLift = 0.12f;
    private const float SolidHoverMix = 0.08f;
    private const float SolidPressMix = 0.15f;
    private const float HaloRest = 0.08f;
    private const float HaloHover = 0.26f;
    private const float SheenAlpha = 0.55f;
    private const float TopEdgeAlpha = 0.28f;
    private const float RimLightAlpha = 0.6f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static Vector4 solidTone = new(-1f);
    private static Vector4 solidInk;
    private static Vector4 solidLabel;
    private static bool solidLight;

    public static float Height(ButtonSize size) => size switch
    {
        ButtonSize.Small => SmallHeight,
        ButtonSize.Regular => RegularHeight,
        _ => LargeHeight,
    };

    public static TextStyle LabelStyle(float heightPixels)
    {
        var units = heightPixels / MathF.Max(UiScale.Current, 0.0001f);
        if (units <= SmallLabelCeiling)
        {
            return TextStyles.FootnoteEmphasized;
        }

        return units <= RegularLabelCeiling ? TextStyles.SubheadlineEmphasized : TextStyles.Headline;
    }

    public static float WidthFor(string label, ButtonSize size)
    {
        var height = Height(size) * UiScale.Current;
        return Typography.Measure(label, LabelStyle(height)).X + height;
    }

    public static bool Draw(Rect rect, string label, in ControlInk ink, ButtonStyle style = ButtonStyle.Prominent,
        ButtonRole role = ButtonRole.Normal, bool enabled = true, bool overlay = false, string? id = null,
        float opacity = 1f) =>
        Draw(ImGui.GetWindowDrawList(), rect, label, ink, style, role, enabled, overlay, id, opacity);

    public static bool Draw(ImDrawListPtr drawList, Rect rect, string label, in ControlInk ink,
        ButtonStyle style = ButtonStyle.Prominent, ButtonRole role = ButtonRole.Normal, bool enabled = true,
        bool overlay = false, string? id = null, float opacity = 1f, UiSound tapSound = UiSound.Tap,
        float labelRoom = 0f)
    {
        var hovered = enabled && (overlay
            ? UiInteract.HoverWindowOnly(rect.Min, rect.Max)
            : UiInteract.Hover(rect.Min, rect.Max));
        var face = Surface(drawList, rect, ink, style, role, enabled, hovered, ImGui.GetID(id ?? label), opacity);
        if (labelRoom > 0f)
        {
            DrawFittedLabel(drawList, face, label, LabelStyle(rect.Height), labelRoom);
        }
        else
        {
            DrawLabel(drawList, face, label, id);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered, tapSound);
    }

    public static ButtonFace Surface(ImDrawListPtr drawList, Rect rect, in ControlInk ink, ButtonStyle style,
        ButtonRole role, bool enabled, bool hovered, uint key, float opacity = 1f) =>
        Surface(drawList, rect, ink, style, role, enabled, hovered, key, opacity, rect.Height * 0.5f);

    public static ButtonFace Surface(ImDrawListPtr drawList, Rect rect, in ControlInk ink, ButtonStyle style,
        ButtonRole role, bool enabled, bool hovered, uint key, float opacity, float cornerRadius)
    {
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var pose = enabled ? MotionButton.Animate(rect, (int)key, hovered, pressed) : new ButtonPose(rect, 0f, 0f, -1f);
        var face = pose.Face;
        var radius = MathF.Min(cornerRadius, rect.Height * 0.5f) * (face.Height / MathF.Max(rect.Height, 0.0001f));
        var alpha = (enabled ? 1f : DisabledAlpha) * opacity;
        var tone = role == ButtonRole.Destructive ? ink.Danger : ink.Accent;
        var toneInk = role == ButtonRole.Destructive ? ink.DangerInk : ink.AccentInk;
        Vector4 labelInk;
        switch (style)
        {
            case ButtonStyle.Prominent:
                PaintProminent(drawList, face, radius, tone, pose, alpha);
                labelInk = White;
                break;
            case ButtonStyle.Tinted:
                Squircle.Fill(drawList, face.Min, face.Max, radius,
                    ImGui.GetColorU32(tone with { W = Lerp(TintedAlpha, TintedHoverAlpha, pose.Hover) * alpha }));
                labelInk = toneInk;
                break;
            case ButtonStyle.Gray:
                var gray = Vector4.Lerp(Surfaces.Fill(ink, FillLevel.Secondary), Surfaces.Fill(ink, FillLevel.Primary),
                    pose.Hover);
                Squircle.Fill(drawList, face.Min, face.Max, radius, ImGui.GetColorU32(gray with { W = gray.W * alpha }));
                labelInk = role == ButtonRole.Destructive ? toneInk : ink.Ink;
                break;
            case ButtonStyle.Solid:
                labelInk = SolidLabel(tone, ink);
                Squircle.Fill(drawList, face.Min, face.Max, radius, ImGui.GetColorU32(Palette.Mix(tone, labelInk,
                    SolidHoverMix * pose.Hover + SolidPressMix * pose.Press) with { W = alpha }));
                break;
            case ButtonStyle.Bare:
                labelInk = toneInk;
                break;
            default:
                if (pose.Hover > 0.001f)
                {
                    var wash = Surfaces.Fill(ink, FillLevel.Quaternary);
                    Squircle.Fill(drawList, face.Min, face.Max, radius,
                        ImGui.GetColorU32(wash with { W = wash.W * pose.Hover * alpha }));
                }

                labelInk = toneInk;
                break;
        }

        if (style is not ButtonStyle.Plain and not ButtonStyle.Bare and not ButtonStyle.Solid)
        {
            MotionButton.RimLight(drawList, face, radius, pose.Hover * RimLightAlpha * alpha);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return new ButtonFace(face, labelInk with { W = labelInk.W * alpha }, hovered);
    }

    public static void DrawLabel(ImDrawListPtr drawList, in ButtonFace face, string label, string? id = null)
    {
        var rect = face.Face;
        var style = LabelStyle(rect.Height);
        var maxLabelWidth = MathF.Max(1f, rect.Width - rect.Height);
        if (id is not null)
        {
            var labelHeight = Typography.Measure(label, style).Y;
            Marquee.DrawCenteredAuto(id, label, rect.Center.X, rect.Center.Y - labelHeight * 0.5f, maxLabelWidth,
                style, face.LabelInk);
            return;
        }

        Typography.DrawCentered(drawList, rect.Center, Typography.FitText(label, maxLabelWidth, style),
            face.LabelInk, style);
    }

    private static void DrawFittedLabel(ImDrawListPtr drawList, in ButtonFace face, string label, TextStyle largest,
        float room)
    {
        var style = new TextStyle(
            Typography.FitScale(label, room, largest.Scale, TextStyles.Footnote.Scale, largest.Weight), largest.Weight);
        Typography.DrawCentered(drawList, face.Face.Center, Typography.FitText(label, room, style), face.LabelInk,
            style);
    }

    private static void PaintProminent(ImDrawListPtr drawList, Rect face, float radius, Vector4 tone, in ButtonPose pose,
        float alpha)
    {
        var scale = UiScale.Current;
        if (alpha >= 1f)
        {
            MotionButton.Halo(drawList, face, radius, tone, Lerp(HaloRest, HaloHover, pose.Hover));
        }

        var body = Palette.Mix(tone, White, ProminentHoverLift * pose.Hover);
        var top = Palette.Mix(body, White, ProminentTopLift);
        Squircle.FillVerticalGradient(drawList, face.Min, face.Max, radius,
            ImGui.GetColorU32(top with { W = alpha }), ImGui.GetColorU32(body with { W = alpha }));
        BrandMark.Sheen(drawList, face.Min, face.Max, radius, pose.Sheen, SheenAlpha * alpha);
        Squircle.StrokeDirectional(drawList, face.Min, face.Max, radius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, TopEdgeAlpha * alpha)), 1f * scale, new Vector2(0f, -1f), 2f);
    }

    private static Vector4 SolidLabel(Vector4 tone, in ControlInk ink)
    {
        if (tone == solidTone && ink.Ink == solidInk && ink.Light == solidLight)
        {
            return solidLabel;
        }

        var opposite = ink.Light ? White : Black;
        solidTone = tone;
        solidInk = ink.Ink;
        solidLight = ink.Light;
        solidLabel = Palette.ContrastRatio(tone, ink.Ink) >= Palette.ContrastRatio(tone, opposite) ? ink.Ink : opposite;
        return solidLabel;
    }

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;
}
