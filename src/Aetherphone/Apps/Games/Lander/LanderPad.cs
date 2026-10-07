using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Lander;

internal readonly struct LanderPadInput
{
    public readonly int Rotate;
    public readonly bool Thrust;

    public LanderPadInput(int rotate, bool thrust)
    {
        Rotate = rotate;
        Thrust = thrust;
    }
}

internal sealed class LanderPad
{
    private const string SurfaceId = "lander.pad";
    private const float KeyHeight = 50f;
    private const float KeyGap = 8f;
    private const float SideShare = 0.24f;
    private const float SteerDeadZone = 14f;

    private PadKey pressed;
    private float anchorX;

    private enum PadKey : byte
    {
        None,
        Left,
        Thrust,
        Right,
    }

    public LanderPadInput Draw(ImDrawListPtr drawList, Rect band, Vector4 accent, bool interactive, string thrustLabel)
    {
        var scale = UiScale.Current;
        var gap = KeyGap * scale;
        var height = MathF.Min(KeyHeight * scale, band.Height - gap * 2f);
        var top = band.Center.Y - height * 0.5f;
        var inner = new Rect(new Vector2(band.Min.X + gap * 2f, top), new Vector2(band.Max.X - gap * 2f, top + height));
        var side = inner.Width * SideShare;
        var left = new Rect(inner.Min, new Vector2(inner.Min.X + side, inner.Max.Y));
        var right = new Rect(new Vector2(inner.Max.X - side, inner.Min.Y), inner.Max);
        var thrust = new Rect(new Vector2(left.Max.X + gap, inner.Min.Y), new Vector2(right.Min.X - gap, inner.Max.Y));
        var input = interactive ? Read(inner, left, thrust, right, scale) : default;
        if (!interactive)
        {
            pressed = PadKey.None;
        }

        var steering = pressed == PadKey.Thrust ? input.Rotate : 0;
        DrawKey(drawList, left, pressed == PadKey.Left || steering < 0, FontAwesomeIcon.UndoAlt, string.Empty, accent,
            scale);
        DrawKey(drawList, right, pressed == PadKey.Right || steering > 0, FontAwesomeIcon.RedoAlt, string.Empty, accent,
            scale);
        DrawKey(drawList, thrust, input.Thrust, FontAwesomeIcon.Fire, thrustLabel, LanderRenderer.Flame, scale);
        return input;
    }

    private LanderPadInput Read(Rect inner, Rect left, Rect thrust, Rect right, float scale)
    {
        PressSurface.Claim(SurfaceId, inner, out var activated);
        var mouse = ImGui.GetMousePos();
        if (activated)
        {
            pressed = left.Contains(mouse) ? PadKey.Left
                : right.Contains(mouse) ? PadKey.Right
                : thrust.Contains(mouse) ? PadKey.Thrust
                : PadKey.None;
            anchorX = mouse.X;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pressed = PadKey.None;
        }

        switch (pressed)
        {
            case PadKey.Left:
                return new LanderPadInput(-1, false);
            case PadKey.Right:
                return new LanderPadInput(1, false);
            case PadKey.Thrust:
            {
                var offset = mouse.X - anchorX;
                var deadZone = SteerDeadZone * scale;
                var rotate = offset > deadZone ? 1 : offset < -deadZone ? -1 : 0;
                return new LanderPadInput(rotate, true);
            }
            default:
                return default;
        }
    }

    private static void DrawKey(ImDrawListPtr drawList, Rect rect, bool held, FontAwesomeIcon icon, string label,
        Vector4 accent, float scale)
    {
        var radius = rect.Height * 0.3f;
        GamePad.KeyFace(drawList, rect, radius, held, false, accent, scale);
        var ink = GamePad.GlyphInk(held, accent);
        var iconSize = rect.Height * 0.36f;
        var style = TextStyles.FootnoteEmphasized;
        var gap = 6f * scale;
        var textWidth = label.Length == 0 ? 0f : Typography.Measure(label, style).X;
        var total = iconSize + gap + textWidth;
        if (label.Length == 0 || total > rect.Width - radius * 2f)
        {
            ProgressRing.CenterIcon(drawList, rect.Center, icon, ink, iconSize);
            return;
        }

        var startX = rect.Center.X - total * 0.5f;
        ProgressRing.CenterIcon(drawList, new Vector2(startX + iconSize * 0.5f, rect.Center.Y), icon, ink, iconSize);
        Typography.Draw(drawList, new Vector2(startX + iconSize + gap, rect.Center.Y - Typography.LineHeight(style) * 0.5f),
            label, ink, style);
    }
}
