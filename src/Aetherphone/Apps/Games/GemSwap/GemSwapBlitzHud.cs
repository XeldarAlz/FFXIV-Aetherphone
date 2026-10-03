using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.GemSwap;

internal sealed class GemSwapBlitzHud
{
    public const float DockHeight = 84f;
    public const float ClockRadius = 23f;
    public const float LowSeconds = 10f;
    private const float BarHeight = 11f;
    private const float BarGap = 12f;
    private const float ButtonRadius = 20f;
    private const float ButtonGap = 14f;
    private const float NearlyFull = 0.8f;
    private static readonly Vector4 LowRed = new(0.98f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private readonly float[] readyFlash = new float[GemSwapBlitz.PowerCount];
    private float barShown;
    private float barFlash;
    private float clockPop;
    private float frostShown;
    private int lastSecond = -1;

    public float FrostShown => frostShown;

    public void Reset()
    {
        Array.Clear(readyFlash, 0, readyFlash.Length);
        barShown = 0f;
        barFlash = 0f;
        clockPop = 0f;
        frostShown = 0f;
        lastSecond = -1;
    }

    public bool Update(GemSwapBlitz blitz, float deltaSeconds, bool running)
    {
        var target = blitz.BarFraction;
        barShown += (target - barShown) * MathF.Min(1f, deltaSeconds * 9f);
        barFlash = MathF.Max(0f, barFlash - deltaSeconds * 2.2f);
        clockPop = MathF.Max(0f, clockPop - deltaSeconds * 3.2f);
        var frostTarget = blitz.Frozen && running ? 1f : 0f;
        frostShown += (frostTarget - frostShown) * MathF.Min(1f, deltaSeconds * 5f);
        for (var power = 0; power < readyFlash.Length; power++)
        {
            readyFlash[power] = MathF.Max(0f, readyFlash[power] - deltaSeconds * 1.8f);
        }

        var second = (int)MathF.Ceiling(blitz.TimeLeft);
        var ticked = running && second != lastSecond && lastSecond >= 0 && second < lastSecond &&
            blitz.TimeLeft < LowSeconds && second > 0;
        lastSecond = second;
        if (ticked)
        {
            clockPop = 1f;
        }

        return ticked;
    }

    public void OnBonus()
    {
        barShown = 1f;
        barFlash = 1f;
    }

    public void OnBonusArrived()
    {
        clockPop = 1f;
    }

    public void OnReady(int power)
    {
        readyFlash[power] = 1f;
    }

    public static Rect BarRect(GameGrid grid, float scale)
    {
        var top = grid.Bounds.Max.Y + grid.Gap + BarGap * scale;
        return new Rect(new Vector2(grid.Bounds.Min.X, top), new Vector2(grid.Bounds.Max.X, top + BarHeight * scale));
    }

    public static Vector2 ButtonCenter(GameGrid grid, int power, float scale)
    {
        var bar = BarRect(grid, scale);
        var pitch = (ButtonRadius * 2f + ButtonGap) * scale;
        var totalWidth = pitch * GemSwapBlitz.PowerCount - ButtonGap * scale;
        var left = grid.Center.X - totalWidth * 0.5f + ButtonRadius * scale;
        return new Vector2(left + power * pitch, bar.Max.Y + (BarGap + ButtonRadius) * scale);
    }

    public static float ButtonSize(float scale) => ButtonRadius * scale;

    public void DrawBar(ImDrawListPtr drawList, Rect bar, Vector4 accent, float scale)
    {
        var radius = bar.Height * 0.5f;
        Squircle.Fill(drawList, bar.Min, bar.Max, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.38f)));
        var near = barShown >= NearlyFull;
        var pulse = near ? Pulse.Wave(Pulse.Fast) : 0f;
        if (near)
        {
            var bloom = new Vector2(4f, 4f) * scale * (1f + pulse);
            Squircle.Fill(drawList, bar.Min - bloom, bar.Max + bloom, radius + bloom.Y,
                ImGui.GetColorU32(accent with { W = 0.14f + 0.16f * pulse }));
        }

        var fillWidth = bar.Width * Math.Clamp(barShown, 0f, 1f);
        if (fillWidth > bar.Height * 0.5f)
        {
            var fillMax = new Vector2(bar.Min.X + fillWidth, bar.Max.Y);
            var head = new Vector2(fillMax.X - radius, bar.Center.Y);
            ProgressRing.Glow(head, bar.Height * (1.4f + 0.6f * pulse), GamePalette.Lighten(accent, 0.35f),
                0.7f + 0.6f * pulse);
            Squircle.FillHorizontalGradient(drawList, bar.Min, fillMax, radius,
                ImGui.GetColorU32(GamePalette.Darken(accent, 0.05f)),
                ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f + 0.2f * pulse)));
            var sweep = Pulse.Phase(1400.0);
            var sheenX = bar.Min.X + (fillWidth + 30f * scale) * sweep - 15f * scale;
            drawList.PushClipRect(bar.Min, fillMax, true);
            drawList.AddQuadFilled(new Vector2(sheenX - 6f * scale, bar.Max.Y), new Vector2(sheenX, bar.Min.Y),
                new Vector2(sheenX + 8f * scale, bar.Min.Y), new Vector2(sheenX + 2f * scale, bar.Max.Y),
                ImGui.GetColorU32(White with { W = 0.35f }));
            drawList.PopClipRect();
        }

        if (barFlash > 0f)
        {
            Squircle.Fill(drawList, bar.Min, bar.Max, radius, ImGui.GetColorU32(White with { W = 0.7f * barFlash }));
        }

        Squircle.Stroke(drawList, bar.Min, bar.Max, radius,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.3f) with { W = 0.35f + 0.4f * pulse }), 1f * scale);
    }

    public void DrawClock(ImDrawListPtr drawList, Vector2 center, GemSwapBlitz blitz, PhoneTheme theme,
        Vector4 accent, float scale)
    {
        var low = blitz.TimeLeft < LowSeconds && !blitz.Frozen;
        var tone = low ? LowRed : accent;
        tone = Vector4.Lerp(tone, GemSwapRenderer.FrostTint, frostShown);
        var pop = Easing.EaseOutCubic(clockPop);
        var radius = ClockRadius * scale * (1f + 0.14f * pop);
        var corner = new Vector2(radius, radius);
        var glow = low ? 0.55f + 0.45f * Pulse.Wave(Pulse.Fast) : 0.35f + 0.3f * frostShown;
        ProgressRing.Glow(center, radius * 1.3f, tone, glow + pop * 0.6f);
        Material.Frosted(drawList, center - corner, center + corner, radius, scale);
        if (frostShown > 0.01f)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GemSwapRenderer.FrostTint with
            {
                W = 0.28f * frostShown,
            }));
        }

        var ringRadius = radius - 3.5f * scale;
        var ringThickness = 3.5f * scale;
        ProgressRing.Track(drawList, center, ringRadius, ringThickness, White with { W = 0.12f });
        ProgressRing.Fill(drawList, center, ringRadius, ringThickness,
            blitz.TimeLeft / GemSwapBlitz.StartSeconds, tone);
        var seconds = (int)MathF.Ceiling(blitz.TimeLeft);
        var ink = low ? LowRed : Vector4.Lerp(theme.TextStrong, GemSwapRenderer.FrostTint, frostShown);
        var textScale = TextStyles.Title3.Scale * (1f + (low ? 0.45f : 0.2f) * pop);
        Typography.DrawCentered(drawList, center, GameNumber.Label(seconds), ink, textScale, TextStyles.Title3.Weight);
        if (frostShown > 0.01f)
        {
            GemSwapRenderer.DrawCrystal(drawList, new Vector2(center.X, center.Y - radius), 5f * scale * frostShown,
                ImGui.GetColorU32(White with { W = 0.85f * frostShown }), scale);
        }
    }

    public int DrawPowers(ImDrawListPtr drawList, GameGrid grid, GemSwapBlitz blitz, float scale, bool interactive)
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
