using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Mahjong;

internal readonly struct MahjongGeometry
{
    public const float Aspect = 1.3f;
    public const float LayerShiftX = 0.10f;
    public const float LayerShiftY = 0.12f;
    public const float ThicknessX = 0.08f;
    public const float ThicknessY = 0.10f;
    public const float GapFraction = 0.035f;
    public const float MaxTileWidth = 62f;

    public readonly Vector2 Origin;
    public readonly float TileWidth;
    public readonly float TileHeight;
    public readonly Rect Content;

    private MahjongGeometry(Vector2 origin, float tileWidth, Rect content)
    {
        Origin = origin;
        TileWidth = tileWidth;
        TileHeight = tileWidth * Aspect;
        Content = content;
    }

    public Vector2 LayerShift => new(-TileWidth * LayerShiftX, -TileWidth * LayerShiftY);

    public Vector2 Thickness => new(TileWidth * ThicknessX, TileWidth * ThicknessY);

    public float Radius => TileWidth * 0.14f;

    public static MahjongGeometry Fit(Rect area, MahjongLayout layout, float scale)
    {
        var extraLayers = layout.Layers - 1;
        var unitsWide = layout.Width * 0.5f + extraLayers * LayerShiftX + ThicknessX;
        var unitsTall = layout.Height * 0.5f * Aspect + extraLayers * LayerShiftY + ThicknessY;
        var tileWidth = MathF.Min(MathF.Min(area.Width / unitsWide, area.Height / unitsTall), MaxTileWidth * scale);
        tileWidth = MathF.Max(1f, tileWidth);
        var size = new Vector2(tileWidth * unitsWide, tileWidth * unitsTall);
        var topLeft = area.Center - size * 0.5f;
        var origin = topLeft + new Vector2(LayerShiftX, LayerShiftY) * (extraLayers * tileWidth);
        return new MahjongGeometry(origin, tileWidth, new Rect(topLeft, topLeft + size));
    }

    public Rect Face(MahjongLayout layout, int tile)
    {
        var gap = TileWidth * GapFraction;
        var min = Origin + new Vector2(layout.X(tile) * TileWidth * 0.5f, layout.Y(tile) * TileHeight * 0.5f) +
                  LayerShift * layout.Layer(tile);
        return new Rect(min, min + new Vector2(TileWidth - gap, TileHeight - gap));
    }
}

internal sealed class MahjongRenderer
{
    public const float AppearSeconds = 0.28f;
    private const float WiggleSeconds = 0.32f;
    private const float WiggleFrequency = 42f;
    private const float WiggleAmplitude = 3.5f;
    private const float SelectLift = 4f;
    private const float FlipSeconds = 0.55f;
    private const float DealDrop = 0.9f;
    private const int MaxTiles = MahjongLayout.MaxTiles;
    public static readonly Vector4 Paper = new(0.97f, 0.94f, 0.86f, 1f);
    private static readonly Vector4 PaperShade = new(0.80f, 0.76f, 0.68f, 1f);
    private static readonly Vector4 Edge = new(0.84f, 0.79f, 0.68f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.30f);
    private static readonly Vector4 HintGlow = new(1f, 0.84f, 0.36f, 1f);
    private static readonly Vector4 BlockedTint = new(0.95f, 0.36f, 0.32f, 1f);

    private readonly float[] wiggle = new float[MaxTiles];
    private readonly float[] appear = new float[MaxTiles];
    private float flip = 1f;

    public void Clear()
    {
        Array.Clear(wiggle);
        Array.Fill(appear, 1f);
        flip = 1f;
    }

    public void Wiggle(int tile)
    {
        wiggle[tile] = WiggleSeconds;
    }

    public void Appear(int tile)
    {
        appear[tile] = 0f;
    }

    public void Flip()
    {
        flip = 0f;
    }

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        for (var tile = 0; tile < MaxTiles; tile++)
        {
            wiggle[tile] = MathF.Max(0f, wiggle[tile] - deltaSeconds);
            appear[tile] = MathF.Min(1f, appear[tile] + deltaSeconds / AppearSeconds);
        }

        flip = MathF.Min(1f, flip + deltaSeconds / FlipSeconds);
    }

    public static int HitTest(MahjongBoard board, in MahjongGeometry geometry)
    {
        var layout = board.Layout;
        for (var index = layout.Count - 1; index >= 0; index--)
        {
            var tile = layout.DrawOrder(index);
            if (!board.IsPresent(tile))
            {
                continue;
            }

            var face = geometry.Face(layout, tile);
            if (UiInteract.Hover(face.Min, face.Max))
            {
                return tile;
            }
        }

        return -1;
    }

    public void DrawBoard(ImDrawListPtr drawList, MahjongBoard board, in MahjongGeometry geometry, float entrance,
        int selected, int hintFirst, int hintSecond, float hintStrength, Vector4 accent, float scale)
    {
        var layout = board.Layout;
        var count = layout.Count;
        var bodyColor = GamePalette.Darken(accent, 0.42f);
        var body = ImGui.GetColorU32(bodyColor);
        var bodyRim = ImGui.GetColorU32(GamePalette.Darken(accent, 0.62f));
        var edge = ImGui.GetColorU32(Edge);
        var shadow = ImGui.GetColorU32(Shadow);
        var thickness = geometry.Thickness;
        var radius = geometry.Radius;
        var hintPulse = 0.55f + 0.45f * Pulse.Wave(Pulse.Fast);
        for (var index = 0; index < count; index++)
        {
            var tile = layout.DrawOrder(index);
            if (!board.IsPresent(tile))
            {
                continue;
            }

            var deal = Easing.EaseOutCubic(GameJuice.Stagger(entrance, index, count, 0.7f));
            if (deal <= 0f)
            {
                continue;
            }

            var face = geometry.Face(layout, tile);
            var offset = new Vector2(Wobble(tile, scale), -(1f - deal) * geometry.TileHeight * DealDrop);
            if (tile == selected)
            {
                offset.Y -= SelectLift * scale;
            }

            face = face.Translate(offset);
            var pop = appear[tile] < 1f ? GameJuice.PopIn(appear[tile]) : 1f;
            if (pop != 1f)
            {
                face = face.Scaled(MathF.Max(0.05f, pop));
            }

            var alpha = deal;
            var shadowOffset = thickness + new Vector2(1.5f * scale, 2.5f * scale);
            drawList.AddRectFilled(face.Min + shadowOffset, face.Max + shadowOffset,
                alpha < 1f ? ImGui.GetColorU32(Shadow with { W = Shadow.W * alpha }) : shadow, radius);
            drawList.AddRectFilled(face.Min + thickness, face.Max + thickness,
                alpha < 1f ? ImGui.GetColorU32(bodyColor with { W = alpha }) : body, radius);
            drawList.AddRect(face.Min + thickness, face.Max + thickness, bodyRim, radius, ImDrawFlags.None,
                MathF.Max(1f, 0.8f * scale));
            drawList.AddRectFilled(face.Min + thickness * 0.45f, face.Max + thickness * 0.45f,
                alpha < 1f ? ImGui.GetColorU32(Edge with { W = alpha }) : edge, radius);
            var flipPhase = FlipPhase(index, count);
            var squeeze = MathF.Abs(MathF.Cos(flipPhase * MathF.PI));
            var drawn = squeeze < 1f ? Squeeze(face, squeeze) : face;
            if (flipPhase > 0.25f && flipPhase < 0.75f)
            {
                StageCell.Draw(drawList, drawn, GamePalette.Darken(accent, 0.35f), CellDepth.Raised, radius, scale);
                continue;
            }

            var tint = board.IsFree(tile) ? Paper : Vector4.Lerp(Paper, PaperShade, 0.55f);
            if (tile == selected)
            {
                tint = Vector4.Lerp(Paper, GamePalette.Lighten(accent, 0.55f), 0.45f);
            }

            StageCell.Draw(drawList, drawn, tint with { W = alpha }, CellDepth.Raised, radius, scale);
            MahjongArt.Draw(drawList, drawn, board.Face(tile), tint, alpha, scale);
            if (wiggle[tile] > 0f)
            {
                Squircle.Fill(drawList, drawn.Min, drawn.Max, radius,
                    ImGui.GetColorU32(BlockedTint with { W = 0.22f * wiggle[tile] / WiggleSeconds }));
            }

            if (tile == selected)
            {
                Squircle.Stroke(drawList, drawn.Min - new Vector2(1.5f * scale, 1.5f * scale),
                    drawn.Max + new Vector2(1.5f * scale, 1.5f * scale), radius + 1.5f * scale,
                    ImGui.GetColorU32(accent), 2.4f * scale);
            }

            if (hintStrength > 0f && (tile == hintFirst || tile == hintSecond))
            {
                var spread = (2f + 2f * hintPulse) * scale;
                var outset = new Vector2(spread, spread);
                Squircle.Stroke(drawList, drawn.Min - outset, drawn.Max + outset, radius + spread,
                    ImGui.GetColorU32(HintGlow with { W = hintStrength * hintPulse }), 2.6f * scale);
            }
        }
    }

    public static void DrawFace(ImDrawListPtr drawList, Rect face, int faceId, Vector4 accent, float radius,
        float alpha, float scale, in MahjongGeometry geometry)
    {
        var thickness = geometry.Thickness;
        drawList.AddRectFilled(face.Min + thickness, face.Max + thickness,
            ImGui.GetColorU32(GamePalette.Darken(accent, 0.42f) with { W = alpha }), radius);
        drawList.AddRectFilled(face.Min + thickness * 0.45f, face.Max + thickness * 0.45f,
            ImGui.GetColorU32(Edge with { W = alpha }), radius);
        StageCell.Draw(drawList, face, Paper with { W = alpha }, CellDepth.Raised, radius, scale);
        MahjongArt.Draw(drawList, face, faceId, Paper, alpha, scale);
    }

    public static bool ToolButton(ImDrawListPtr drawList, Vector2 center, float radius, FontAwesomeIcon icon,
        string caption, string badge, Vector4 accent, PhoneTheme theme, bool enabled, bool highlight, float scale)
    {
        var corner = new Vector2(radius, radius);
        var hovered = enabled && UiInteract.Hover(center - corner, center + corner);
        if (highlight)
        {
            ProgressRing.Glow(center, radius * 1.5f, accent, 0.5f + 0.4f * Pulse.Wave(Pulse.Fast));
        }

        var opacity = enabled ? (hovered ? 1f : 0.88f) : 0.5f;
        Material.Frosted(drawList, center - corner, center + corner, radius, scale, opacity);
        if (highlight)
        {
            Squircle.Stroke(drawList, center - corner, center + corner, radius, ImGui.GetColorU32(accent), 2f * scale);
        }

        var ink = StageInks.Muted with { W = 0.45f };
        if (enabled)
        {
            ink = hovered || highlight ? StageInks.Strong : GamePalette.Lighten(accent, 0.35f);
        }

        ProgressRing.CenterIcon(drawList, center, icon, ink, radius * 0.82f);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + radius + 9f * scale), caption,
            enabled ? StageInks.Strong : StageInks.Muted with { W = 0.55f }, TextStyles.Caption2);
        if (badge.Length > 0)
        {
            var badgeSize = Typography.Measure(badge, TextStyles.Caption2);
            var badgeCenter = center + new Vector2(radius * 0.82f, -radius * 0.82f);
            var half = new Vector2(badgeSize.X * 0.5f + 4f * scale, badgeSize.Y * 0.5f + 1f * scale);
            Squircle.Fill(drawList, badgeCenter - half, badgeCenter + half, half.Y,
                ImGui.GetColorU32(enabled ? accent : accent with { W = 0.4f }));
            Typography.DrawCentered(drawList, badgeCenter, badge, GamePalette.InkOn(accent), TextStyles.Caption2);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.HoverClickCircle(center, radius);
    }

    private float Wobble(int tile, float scale)
    {
        var left = wiggle[tile];
        if (left <= 0f)
        {
            return 0f;
        }

        var fade = left / WiggleSeconds;
        return MathF.Sin(left * WiggleFrequency) * WiggleAmplitude * scale * fade;
    }

    private float FlipPhase(int index, int count)
    {
        if (flip >= 1f)
        {
            return 0f;
        }

        return GameJuice.Stagger(flip, index, count, 0.6f);
    }

    private static Rect Squeeze(Rect face, float factor)
    {
        var half = face.Width * 0.5f * MathF.Max(0.04f, factor);
        return new Rect(new Vector2(face.Center.X - half, face.Min.Y), new Vector2(face.Center.X + half, face.Max.Y));
    }
}
