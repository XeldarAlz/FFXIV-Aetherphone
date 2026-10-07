using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Fuse;

internal sealed class FusePad
{
    private const string PadSurfaceId = "fuse.pad";
    private const string BombSurfaceId = "fuse.bomb";
    private const float BandInset = 6f;
    private const float PanelOpacity = 0.9f;
    private const float DeadZone = 0.16f;
    private const float KeyFraction = 0.34f;
    private const float StatIcon = 9f;
    private const float StatGap = 4f;
    private const float StatRow = 20f;
    private Rect panel;
    private Vector2 padCenter;
    private float padRadius;
    private Vector2 bombCenter;
    private float bombRadius;
    private Vector2 statsCenter;
    private float bombFlash;
    private HeldPadState padHold;

    public FuseDirection Direction { get; private set; }

    public bool BombPressed { get; private set; }

    public void Layout(Rect band, float scale)
    {
        var inset = BandInset * scale;
        panel = new Rect(new Vector2(band.Min.X + inset, band.Min.Y + inset), new Vector2(band.Max.X - inset, band.Max.Y - inset));
        var height = panel.Height;
        padRadius = MathF.Max(0f, height * 0.5f - 6f * scale);
        padCenter = new Vector2(panel.Min.X + padRadius + 12f * scale, panel.Center.Y);
        bombRadius = MathF.Max(0f, height * 0.36f);
        bombCenter = new Vector2(panel.Max.X - bombRadius - 16f * scale, panel.Center.Y);
        statsCenter = new Vector2((padCenter.X + padRadius + bombCenter.X - bombRadius) * 0.5f, panel.Center.Y);
    }

    public void Release()
    {
        padHold.Release();
        Direction = FuseDirection.None;
        BombPressed = false;
    }

    public void Update(float deltaSeconds)
    {
        bombFlash = MathF.Max(0f, bombFlash - deltaSeconds * 4f);
        var padRect = new Rect(padCenter - new Vector2(padRadius, padRadius), padCenter + new Vector2(padRadius, padRadius));
        PressSurface.Claim(PadSurfaceId, padRect, out var padActivated);
        var mouse = ImGui.GetMousePos();
        padHold.Update(mouse, ImGui.IsMouseDown(ImGuiMouseButton.Left),
            padActivated && Vector2.Distance(mouse, padCenter) <= padRadius, padCenter, padRadius * DeadZone);
        Direction = DirectionOf(padHold.Held);
        var bombRect = new Rect(bombCenter - new Vector2(bombRadius, bombRadius), bombCenter + new Vector2(bombRadius, bombRadius));
        PressSurface.Claim(BombSurfaceId, bombRect, out var bombActivated);
        BombPressed = bombActivated && Vector2.Distance(mouse, bombCenter) <= bombRadius;
        if (BombPressed)
        {
            bombFlash = 1f;
        }
    }

    public void Flash()
    {
        bombFlash = 1f;
    }

    public void Draw(ImDrawListPtr drawList, in Moogle moogle, Vector4 accent, float scale, float time)
    {
        if (panel.Width <= 0f || panel.Height <= 0f)
        {
            return;
        }

        Material.Frosted(drawList, panel.Min, panel.Max, Metrics.Radius.Lg * scale, scale, PanelOpacity);
        DrawCross(drawList, accent, scale);
        DrawStats(drawList, in moogle, scale, time);
        DrawBombButton(drawList, in moogle, accent, scale, time);
    }

    private static FuseDirection DirectionOf(PadDirection direction) => direction switch
    {
        PadDirection.Up => FuseDirection.Up,
        PadDirection.Down => FuseDirection.Down,
        PadDirection.Left => FuseDirection.Left,
        PadDirection.Right => FuseDirection.Right,
        _ => FuseDirection.None,
    };

    private void DrawCross(ImDrawListPtr drawList, Vector4 accent, float scale)
    {
        drawList.AddCircleFilled(padCenter, padRadius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.16f)), 32);
        drawList.AddCircle(padCenter, padRadius, ImGui.GetColorU32(StageInks.Strong with { W = 0.12f }), 32, 1f * scale);
        var key = padRadius * KeyFraction;
        var directions = FuseBoard.Directions;
        for (var index = 0; index < directions.Length; index++)
        {
            var direction = directions[index];
            var held = Direction == direction;
            var center = padCenter + FuseBoard.Vector(direction) * padRadius * 0.6f;
            var half = new Vector2(key, key) * (held ? 0.92f : 1f);
            var radius = key * 0.5f;
            Squircle.Fill(drawList, center - half, center + half, radius,
                ImGui.GetColorU32(held ? accent with { W = 0.55f } : StageInks.Strong with { W = 0.1f }));
            if (held)
            {
                Squircle.Stroke(drawList, center - half, center + half, radius, ImGui.GetColorU32(accent), 1.5f * scale);
            }

            DrawArrow(drawList, center, key * 0.5f, direction, held ? FuseArt.White : StageInks.Strong with { W = 0.8f });
        }

        drawList.AddCircleFilled(padCenter, key * 0.42f, ImGui.GetColorU32(StageInks.Strong with { W = 0.12f }), 20);
    }

    private static void DrawArrow(ImDrawListPtr drawList, Vector2 center, float size, FuseDirection direction, Vector4 color)
    {
        var forward = FuseBoard.Vector(direction);
        var side = new Vector2(-forward.Y, forward.X);
        drawList.AddTriangleFilled(center + forward * size * 0.7f, center - forward * size * 0.45f + side * size * 0.6f,
            center - forward * size * 0.45f - side * size * 0.6f, ImGui.GetColorU32(color));
    }

    private void DrawStats(ImDrawListPtr drawList, in Moogle moogle, float scale, float time)
    {
        var row = StatRow * scale;
        var top = statsCenter.Y - row;
        DrawStat(drawList, new Vector2(statsCenter.X - 26f * scale, top), PowerUp.ExtraBomb, GameNumber.Label(moogle.Bombs),
            scale, time);
        DrawStat(drawList, new Vector2(statsCenter.X + 26f * scale, top), PowerUp.Range, GameNumber.Label(moogle.Range),
            scale, time);
        DrawStat(drawList, new Vector2(statsCenter.X - 26f * scale, top + row), PowerUp.Speed,
            GameNumber.Label(moogle.SpeedLevel + 1), scale, time);
        var kickAlpha = moogle.Kick ? 1f : 0.3f;
        var kickCenter = new Vector2(statsCenter.X + 26f * scale - 6f * scale, top + row);
        FuseArt.DrawGlyph(drawList, kickCenter, StatIcon * scale, PowerUp.Kick, FuseArt.KickColor, kickAlpha, time);
    }

    private static void DrawStat(ImDrawListPtr drawList, Vector2 center, PowerUp kind, string label, float scale,
        float time)
    {
        var icon = center - new Vector2(StatIcon * scale + StatGap * scale * 0.5f, 0f);
        FuseArt.DrawGlyph(drawList, icon, StatIcon * scale, kind, FuseArt.PowerUpColor(kind), 1f, time);
        var style = TextStyles.FootnoteEmphasized;
        Typography.Draw(drawList, new Vector2(center.X + StatGap * scale * 0.5f, center.Y - Typography.LineHeight(style) * 0.5f),
            label, StageInks.Strong, style);
    }

    private void DrawBombButton(ImDrawListPtr drawList, in Moogle moogle, Vector4 accent, float scale, float time)
    {
        var ready = moogle.Alive && moogle.BombsOut < moogle.Bombs;
        var hovered = UiInteract.Hover(bombCenter - new Vector2(bombRadius, bombRadius), bombCenter + new Vector2(bombRadius, bombRadius));
        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var radius = bombRadius * (held ? 0.94f : 1f);
        if (ready)
        {
            ProgressRing.Glow(bombCenter, radius * 1.15f, FuseArt.Flame, 0.35f + bombFlash * 0.5f);
        }

        var top = ImGui.GetColorU32(GamePalette.Lighten(ready ? FuseArt.Flame : accent with { W = 0.5f }, 0.12f));
        var bottom = ImGui.GetColorU32(GamePalette.Darken(ready ? FuseArt.Flame : accent with { W = 0.5f }, 0.2f));
        Squircle.FillCircleVerticalGradient(drawList, bombCenter, radius, top, bottom);
        drawList.AddCircle(bombCenter, radius, ImGui.GetColorU32(FuseArt.White with { W = 0.45f }), 32, 1.5f * scale);
        FuseArt.DrawBomb(drawList, bombCenter + new Vector2(-radius * 0.06f, radius * 0.05f), radius * 0.42f,
            bombFlash * 0.12f, 0f, ready ? 1f : 0.55f, time);
        var left = Math.Max(0, moogle.Bombs - moogle.BombsOut);
        var pips = Math.Min(moogle.Bombs, FuseBoard.MaxBombsEach);
        var pipRadius = 2.6f * scale;
        var span = (pips - 1) * pipRadius * 3f;
        for (var pip = 0; pip < pips; pip++)
        {
            var center = new Vector2(bombCenter.X - span * 0.5f + pip * pipRadius * 3f, bombCenter.Y + radius + 7f * scale);
            drawList.AddCircleFilled(center, pipRadius,
                ImGui.GetColorU32(pip < left ? FuseArt.Spark : FuseArt.White with { W = 0.25f }), 10);
        }
    }
}
