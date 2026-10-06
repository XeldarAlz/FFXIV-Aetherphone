using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.GemSwap;

internal sealed class GemSwapPowerDock
{
    public const float BandHeight = 64f;
    private const float ButtonRadius = 20f;
    private const float ButtonGap = 14f;
    private const float ChargeInset = 6f;
    private const float ChargeAlpha = 0.14f;
    private const float NearlyFull = 0.8f;
    private const float ChargeFollow = 9f;
    private const float FlashDecay = 2.2f;
    private const float ReadyFlashDecay = 1.8f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private readonly float[] readyFlash = new float[GemSwapBlitz.PowerCount];
    private float chargeShown;
    private float chargeFlash;

    public void Reset()
    {
        Array.Clear(readyFlash, 0, readyFlash.Length);
        chargeShown = 0f;
        chargeFlash = 0f;
    }

    public void Update(GemSwapBlitz blitz, float deltaSeconds)
    {
        chargeShown += (blitz.BarFraction - chargeShown) * MathF.Min(1f, deltaSeconds * ChargeFollow);
        chargeFlash = MathF.Max(0f, chargeFlash - deltaSeconds * FlashDecay);
        for (var power = 0; power < readyFlash.Length; power++)
        {
            readyFlash[power] = MathF.Max(0f, readyFlash[power] - deltaSeconds * ReadyFlashDecay);
        }
    }

    public void OnBonus()
    {
        chargeShown = 1f;
        chargeFlash = 1f;
    }

    public void OnReady(int power)
    {
        readyFlash[power] = 1f;
    }

    public static Rect Band(in GameGrid grid, float scale)
    {
        var top = grid.Bounds.Max.Y + grid.Gap;
        return new Rect(new Vector2(grid.Bounds.Min.X, top),
            new Vector2(grid.Bounds.Max.X, top + BandHeight * scale));
    }

    public static Vector2 ButtonCenter(in GameGrid grid, int power, float scale)
    {
        var band = Band(grid, scale);
        var pitch = (ButtonRadius * 2f + ButtonGap) * scale;
        var totalWidth = pitch * GemSwapBlitz.PowerCount - ButtonGap * scale;
        var left = band.Center.X - totalWidth * 0.5f + ButtonRadius * scale;
        return new Vector2(left + power * pitch, band.Center.Y);
    }

    public static float ButtonSize(float scale) => ButtonRadius * scale;

    public void DrawCharge(ImDrawListPtr drawList, Rect band, Vector4 accent, float scale)
    {
        var inset = ChargeInset * scale;
        var min = band.Min + new Vector2(inset, inset);
        var max = band.Max - new Vector2(inset, inset);
        var radius = (max.Y - min.Y) * 0.5f;
        var near = chargeShown >= NearlyFull;
        var pulse = near ? Pulse.Wave(Pulse.Fast) : 0f;
        var fillWidth = (max.X - min.X) * Math.Clamp(chargeShown, 0f, 1f);
        if (fillWidth > radius)
        {
            var fillMax = new Vector2(min.X + fillWidth, max.Y);
            Squircle.FillHorizontalGradient(drawList, min, fillMax, radius,
                ImGui.GetColorU32(accent with { W = ChargeAlpha * 0.5f }),
                ImGui.GetColorU32(GamePalette.Lighten(accent, 0.3f) with { W = ChargeAlpha + 0.12f * pulse }));
            ProgressRing.Glow(new Vector2(fillMax.X - radius, band.Center.Y), radius * (1.2f + 0.5f * pulse),
                GamePalette.Lighten(accent, 0.35f), 0.35f + 0.5f * pulse);
        }

        if (chargeFlash > 0f)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(White with { W = 0.45f * chargeFlash }));
        }
    }

    public int DrawPowers(ImDrawListPtr drawList, in GameGrid grid, GemSwapBlitz blitz, float scale, bool interactive)
    {
        var clicked = -1;
        var radius = ButtonRadius * scale;
        var corner = new Vector2(radius, radius);
        var time = (float)ImGui.GetTime();
        for (var power = 0; power < GemSwapBlitz.PowerCount; power++)
        {
            var center = ButtonCenter(grid, power, scale);
            var color = GemSwapRenderer.ColorOf(GemSwapBlitz.PowerColor(power));
            var ready = blitz.IsReady(power);
            var hovered = interactive && ready && UiInteract.Hover(center - corner, center + corner);
            var press = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) ? 0.94f : 1f;
            var drawRadius = radius * press * (1f + 0.12f * Easing.EaseOutCubic(readyFlash[power]));
            if (ready)
            {
                ProgressRing.Glow(center, drawRadius * (1.35f + 0.12f * Pulse.Wave(Pulse.Fast)), color,
                    hovered ? 1.1f : 0.8f);
            }

            var drawCorner = new Vector2(drawRadius, drawRadius);
            Material.Frosted(drawList, center - drawCorner, center + drawCorner, drawRadius, scale);
            drawList.AddCircleFilled(center, drawRadius - 4f * scale,
                ImGui.GetColorU32(GamePalette.Darken(color, ready ? 0.05f : 0.35f) with { W = ready ? 0.95f : 0.45f }));
            var ringRadius = drawRadius - 2f * scale;
            ProgressRing.Track(drawList, center, ringRadius, 3f * scale, White with { W = 0.14f });
            ProgressRing.Fill(drawList, center, ringRadius, 3f * scale, blitz.Charge(power),
                ready ? GamePalette.Lighten(color, 0.35f) : color);
            var ink = White with { W = ready ? 1f : 0.5f };
            DrawPowerGlyph(drawList, (GemPower)power, center, drawRadius * 0.5f, ink, color, time);
            if (readyFlash[power] > 0f)
            {
                var flash = readyFlash[power];
                drawList.AddCircle(center, drawRadius * (1f + (1f - flash) * 1.1f),
                    ImGui.GetColorU32(GamePalette.Lighten(color, 0.4f) with { W = flash }), 0, 2.5f * scale);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (interactive && ready && UiInteract.HoverClickCircle(center, radius))
            {
                clicked = power;
            }
        }

        return clicked;
    }

    private static void DrawPowerGlyph(ImDrawListPtr drawList, GemPower power, Vector2 center, float extent,
        Vector4 ink, Vector4 color, float time)
    {
        var inkColor = ImGui.GetColorU32(ink);
        var thickness = MathF.Max(1.5f, extent * 0.2f);
        switch (power)
        {
            case GemPower.Fire:
            {
                var flicker = 1f + 0.08f * MathF.Sin(time * 14f);
                var baseCenter = new Vector2(center.X, center.Y + extent * 0.28f);
                drawList.AddCircleFilled(baseCenter, extent * 0.52f, inkColor, 16);
                drawList.AddTriangleFilled(new Vector2(center.X + extent * 0.05f, center.Y - extent * 0.95f * flicker),
                    new Vector2(center.X - extent * 0.5f, baseCenter.Y - extent * 0.1f),
                    new Vector2(center.X + extent * 0.5f, baseCenter.Y - extent * 0.1f), inkColor);
                var core = ImGui.GetColorU32(GamePalette.Lighten(color, 0.2f) with { W = ink.W });
                drawList.AddCircleFilled(new Vector2(baseCenter.X, baseCenter.Y + extent * 0.06f), extent * 0.24f, core,
                    12);
                break;
            }
            case GemPower.Frost:
                for (var arm = 0; arm < 3; arm++)
                {
                    var angle = arm * MathF.PI / 3f + MathF.PI * 0.5f;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    var normal = new Vector2(-direction.Y, direction.X);
                    drawList.AddLine(center - direction * extent, center + direction * extent, inkColor, thickness);
                    for (var end = -1; end <= 1; end += 2)
                    {
                        var tip = center + direction * extent * 0.62f * end;
                        var back = direction * extent * 0.3f * end;
                        drawList.AddLine(tip, tip + back + normal * extent * 0.28f, inkColor, thickness * 0.7f);
                        drawList.AddLine(tip, tip + back - normal * extent * 0.28f, inkColor, thickness * 0.7f);
                    }
                }

                break;
            case GemPower.Gale:
                for (var gust = 0; gust < 3; gust++)
                {
                    var y = center.Y + (gust - 1) * extent * 0.6f;
                    var length = extent * (gust == 1 ? 1.5f : 1.1f);
                    var shift = MathF.Sin(time * 4f + gust) * extent * 0.08f;
                    var startX = center.X - extent + shift;
                    var endX = startX + length;
                    var curl = extent * 0.24f;
                    drawList.PathLineTo(new Vector2(startX, y));
                    drawList.PathLineTo(new Vector2(endX, y));
                    drawList.PathArcTo(new Vector2(endX, y - curl), curl, MathF.PI * 0.5f, -MathF.PI, 10);
                    drawList.PathStroke(inkColor, ImDrawFlags.None, thickness * 0.8f);
                }

                break;
            case GemPower.Storm:
                drawList.AddTriangleFilled(new Vector2(center.X + extent * 0.35f, center.Y - extent),
                    new Vector2(center.X - extent * 0.45f, center.Y + extent * 0.12f),
                    new Vector2(center.X + extent * 0.08f, center.Y + extent * 0.12f), inkColor);
                drawList.AddTriangleFilled(new Vector2(center.X - extent * 0.08f, center.Y - extent * 0.12f),
                    new Vector2(center.X + extent * 0.45f, center.Y - extent * 0.12f),
                    new Vector2(center.X - extent * 0.35f, center.Y + extent), inkColor);
                break;
        }
    }
}
