using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Crates;

internal readonly struct CratesView
{
    public readonly Vector2 Origin;
    public readonly float Pitch;
    public readonly int Columns;
    public readonly int Rows;

    public CratesView(Vector2 origin, float pitch, int columns, int rows)
    {
        Origin = origin;
        Pitch = pitch;
        Columns = columns;
        Rows = rows;
    }

    public Rect Bounds => new(Origin, Origin + new Vector2(Columns * Pitch, Rows * Pitch));

    public Vector2 Center(Vector2 cell) => Origin + (cell + new Vector2(0.5f, 0.5f)) * Pitch;

    public Rect CellRect(int column, int row)
    {
        var min = Origin + new Vector2(column * Pitch, row * Pitch);
        return new Rect(min, min + new Vector2(Pitch, Pitch));
    }

    public static CratesView Fit(Rect area, int columns, int rows, float maxPitch)
    {
        var pitch = MathF.Min(maxPitch, MathF.Min(area.Width / Math.Max(1, columns), area.Height / Math.Max(1, rows)));
        var size = new Vector2(columns * pitch, rows * pitch);
        return new CratesView(area.Center - size * 0.5f, pitch, columns, rows);
    }
}

internal static class CratesRenderer
{
    public static readonly Vector4 Wood = new(0.80f, 0.57f, 0.32f, 1f);
    public static readonly Vector4 Dust = new(0.62f, 0.54f, 0.44f, 0.55f);
    public static readonly Vector4 Lit = new(1f, 0.83f, 0.36f, 1f);
    public static readonly Vector4 Fluff = new(0.99f, 0.97f, 0.93f, 1f);
    public static readonly Vector4 Pompom = new(0.94f, 0.27f, 0.36f, 1f);
    private const float CrateSize = 0.80f;
    private const float FloorGap = 0.05f;
    private const float WallGap = 0.06f;
    private static readonly Vector4 Floor = GamePalette.Cell;
    private static readonly Vector4 FloorAlternate = GamePalette.CellSunken;
    private static readonly Vector4 Wall = new(0.52f, 0.44f, 0.40f, 1f);
    private static readonly Vector4 WallTop = new(0.62f, 0.54f, 0.49f, 1f);
    private static readonly Vector4 WoodDark = new(0.52f, 0.33f, 0.17f, 1f);
    private static readonly Vector4 WoodLight = new(0.90f, 0.70f, 0.45f, 1f);
    private static readonly Vector4 LitDark = new(0.72f, 0.50f, 0.12f, 1f);
    private static readonly Vector4 FluffShade = new(0.86f, 0.83f, 0.80f, 1f);
    private static readonly Vector4 Wing = new(0.58f, 0.44f, 0.82f, 1f);
    private static readonly Vector4 Ink = new(0.18f, 0.16f, 0.20f, 1f);
    private static readonly Vector4 Nose = new(0.96f, 0.56f, 0.62f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.18f);

    public static void DrawBoard(ImDrawListPtr drawList, CratesBoard board, in CratesView view, float wave,
        Vector4 accent, StageInk ink, float scale)
    {
        BoardPlate.Draw(drawList, BoardPlate.Around(view.Bounds, scale), BoardPlate.Radius * scale, scale, accent, ink);
        var radius = view.Pitch * 0.16f;
        var spread = board.Columns + board.Rows;
        for (var row = 0; row < board.Rows; row++)
        {
            for (var column = 0; column < board.Columns; column++)
            {
                var tile = board.TileAt(column, row);
                if (tile == CratesTile.Void)
                {
                    continue;
                }

                var distance = Math.Abs(column - board.PlayerColumn) + Math.Abs(row - board.PlayerRow);
                var lift = wave > 0f ? StageCell.Lift(1f - WavePhase(wave, distance, spread)) * scale * 2f : 0f;
                var rect = view.CellRect(column, row).Translate(new Vector2(0f, -lift));
                if (tile == CratesTile.Wall)
                {
                    DrawWall(drawList, rect, view.Pitch, radius, scale);
                    continue;
                }

                var fill = ((column + row) & 1) == 0 ? Floor : FloorAlternate;
                StageCell.Draw(drawList, rect.Inset(view.Pitch * FloorGap), fill, CellDepth.Sunken, radius, scale);
                if (board.IsTarget(column, row))
                {
                    DrawTarget(drawList, rect.Center, view.Pitch, accent, board.CrateAt(column, row) != CratesBoard.NoCrate,
                        scale);
                }
            }
        }
    }

    public static void DrawCrate(ImDrawListPtr drawList, Vector2 center, float pitch, bool onTarget, float landed,
        float scale)
    {
        var pop = 1f + 0.14f * MathF.Sin(MathF.Min(1f, landed) * MathF.PI);
        var half = pitch * CrateSize * 0.5f * pop;
        var min = center - new Vector2(half, half);
        var max = center + new Vector2(half, half);
        var radius = pitch * 0.12f;
        if (onTarget)
        {
            ProgressRing.Glow(center, half * 1.6f, Lit, 0.55f + 0.25f * Pulse.Wave(Pulse.Calm) + 0.6f * landed);
        }

        var face = onTarget ? Lit : Wood;
        var frame = onTarget ? LitDark : WoodDark;
        StageCell.Draw(drawList, new Rect(min, max), frame, CellDepth.Raised, radius, scale);
        var inset = pitch * 0.11f;
        var innerMin = min + new Vector2(inset, inset);
        var innerMax = max - new Vector2(inset, inset);
        Squircle.FillVerticalGradient(drawList, innerMin, innerMax, radius * 0.5f,
            ImGui.GetColorU32(GamePalette.Lighten(face, 0.12f)), ImGui.GetColorU32(face));
        var brace = ImGui.GetColorU32(frame with { W = 0.9f });
        var thickness = MathF.Max(1.5f * scale, pitch * 0.07f);
        drawList.AddLine(innerMin + new Vector2(thickness * 0.3f, thickness * 0.3f),
            innerMax - new Vector2(thickness * 0.3f, thickness * 0.3f), brace, thickness);
        drawList.AddLine(new Vector2(innerMin.X, innerMax.Y - pitch * 0.18f), new Vector2(innerMax.X, innerMax.Y - pitch * 0.18f),
            ImGui.GetColorU32((onTarget ? LitDark : WoodDark) with { W = 0.35f }), MathF.Max(1f, scale));
        drawList.AddLine(new Vector2(innerMin.X, innerMin.Y + pitch * 0.18f), new Vector2(innerMax.X, innerMin.Y + pitch * 0.18f),
            ImGui.GetColorU32((onTarget ? LitDark : WoodDark) with { W = 0.35f }), MathF.Max(1f, scale));
        var nail = ImGui.GetColorU32(onTarget ? White with { W = 0.85f } : WoodLight);
        var nailRadius = MathF.Max(1f, pitch * 0.035f);
        drawList.AddCircleFilled(min + new Vector2(inset * 0.5f, inset * 0.5f), nailRadius, nail, 8);
        drawList.AddCircleFilled(new Vector2(max.X - inset * 0.5f, min.Y + inset * 0.5f), nailRadius, nail, 8);
        drawList.AddCircleFilled(new Vector2(min.X + inset * 0.5f, max.Y - inset * 0.5f), nailRadius, nail, 8);
        drawList.AddCircleFilled(max - new Vector2(inset * 0.5f, inset * 0.5f), nailRadius, nail, 8);
        if (landed > 0f)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(White with { W = 0.55f * landed }));
        }
    }

    public static void DrawMoogle(ImDrawListPtr drawList, Vector2 center, float pitch, CratesDirection facing,
        float squash, bool horizontalPush, float bob)
    {
        var stretchX = horizontalPush ? 1f - 0.2f * squash : 1f + 0.14f * squash;
        var stretchY = horizontalPush ? 1f + 0.12f * squash : 1f - 0.2f * squash;
        var breathe = 1f + 0.03f * bob;
        var body = new Vector2(pitch * 0.34f * stretchX * breathe, pitch * 0.32f * stretchY / breathe);
        var feet = center + new Vector2(0f, pitch * 0.30f);
        var bodyCenter = feet - new Vector2(0f, body.Y * 0.95f) + CratesBoard.Step(facing) * pitch * 0.06f * squash;
        Shapes.FillEllipse(drawList, feet + new Vector2(0f, pitch * 0.02f), new Vector2(pitch * 0.26f, pitch * 0.07f),
            ImGui.GetColorU32(Shadow));
        var side = facing switch
        {
            CratesDirection.Left => -1f,
            CratesDirection.Right => 1f,
            _ => 0f,
        };
        var wingFlap = 0.5f + 0.5f * MathF.Sin(bob * 2.4f);
        DrawWing(drawList, bodyCenter + new Vector2(-body.X * 0.82f, -body.Y * 0.35f), -1f, pitch, wingFlap);
        DrawWing(drawList, bodyCenter + new Vector2(body.X * 0.82f, -body.Y * 0.35f), 1f, pitch, wingFlap);
        Shapes.FillEllipse(drawList, bodyCenter + new Vector2(0f, body.Y * 0.08f), body * 1.04f,
            ImGui.GetColorU32(FluffShade));
        Shapes.FillEllipse(drawList, bodyCenter, body, ImGui.GetColorU32(Fluff));
        Shapes.FillEllipse(drawList, bodyCenter - new Vector2(body.X * 0.3f, body.Y * 0.4f), body * 0.28f,
            ImGui.GetColorU32(White with { W = 0.7f }));
        var earOffset = body.X * 0.62f;
        var earTop = bodyCenter.Y - body.Y * 1.05f;
        drawList.AddTriangleFilled(new Vector2(bodyCenter.X - earOffset - pitch * 0.05f, earTop + pitch * 0.12f),
            new Vector2(bodyCenter.X - earOffset + pitch * 0.02f, earTop - pitch * 0.02f),
            new Vector2(bodyCenter.X - earOffset + pitch * 0.09f, earTop + pitch * 0.12f), ImGui.GetColorU32(Fluff));
        drawList.AddTriangleFilled(new Vector2(bodyCenter.X + earOffset - pitch * 0.09f, earTop + pitch * 0.12f),
            new Vector2(bodyCenter.X + earOffset - pitch * 0.02f, earTop - pitch * 0.02f),
            new Vector2(bodyCenter.X + earOffset + pitch * 0.05f, earTop + pitch * 0.12f), ImGui.GetColorU32(Fluff));
        var stemBase = new Vector2(bodyCenter.X + side * body.X * 0.15f, bodyCenter.Y - body.Y * 0.95f);
        var sway = MathF.Sin(bob * 1.7f) * pitch * 0.03f - side * pitch * 0.04f - CratesBoard.Step(facing).X * squash * pitch * 0.08f;
        var pompom = stemBase + new Vector2(sway, -pitch * 0.2f);
        drawList.AddLine(stemBase, pompom, ImGui.GetColorU32(Wing), MathF.Max(1f, pitch * 0.025f));
        ProgressRing.Glow(pompom, pitch * 0.12f, Pompom, 0.35f);
        drawList.AddCircleFilled(pompom, pitch * 0.075f, ImGui.GetColorU32(Pompom), 14);
        drawList.AddCircleFilled(pompom - new Vector2(pitch * 0.02f, pitch * 0.025f), pitch * 0.025f,
            ImGui.GetColorU32(White with { W = 0.6f }), 8);
        if (facing == CratesDirection.Up)
        {
            return;
        }

        var faceCenter = bodyCenter + new Vector2(side * body.X * 0.28f, -body.Y * 0.05f);
        var eyeSpread = body.X * (side == 0f ? 0.36f : 0.3f);
        var eyeWidth = pitch * 0.07f;
        var eyeInk = ImGui.GetColorU32(Ink);
        var eyeThickness = MathF.Max(1f, pitch * 0.03f);
        var squint = squash > 0.3f ? pitch * 0.015f : 0f;
        drawList.AddLine(faceCenter + new Vector2(-eyeSpread - eyeWidth * 0.5f, squint),
            faceCenter + new Vector2(-eyeSpread + eyeWidth * 0.5f, -squint), eyeInk, eyeThickness);
        drawList.AddLine(faceCenter + new Vector2(eyeSpread - eyeWidth * 0.5f, -squint),
            faceCenter + new Vector2(eyeSpread + eyeWidth * 0.5f, squint), eyeInk, eyeThickness);
        drawList.AddCircleFilled(faceCenter + new Vector2(0f, body.Y * 0.22f), pitch * 0.035f,
            ImGui.GetColorU32(Nose), 10);
        var blush = ImGui.GetColorU32(Nose with { W = 0.35f });
        drawList.AddCircleFilled(faceCenter + new Vector2(-eyeSpread - eyeWidth * 0.2f, body.Y * 0.28f), pitch * 0.04f,
            blush, 10);
        drawList.AddCircleFilled(faceCenter + new Vector2(eyeSpread + eyeWidth * 0.2f, body.Y * 0.28f), pitch * 0.04f,
            blush, 10);
    }

    public static bool ToolButton(ImDrawListPtr drawList, Vector2 center, float radius, FontAwesomeIcon icon,
        string caption, Vector4 accent, bool enabled, float scale)
    {
        var corner = new Vector2(radius, radius);
        var hovered = enabled && UiInteract.Hover(center - corner, center + corner);
        var opacity = enabled ? (hovered ? 1f : 0.9f) : 0.55f;
        if (hovered)
        {
            ProgressRing.Glow(center, radius * 1.4f, accent, 0.4f);
        }

        Material.Frosted(drawList, center - corner, center + corner, radius, scale, opacity);
        if (hovered)
        {
            Squircle.Stroke(drawList, center - corner, center + corner, radius,
                ImGui.GetColorU32(accent with { W = 0.8f }), 1f * scale);
        }

        var ink = StageInks.Muted with { W = 0.45f };
        if (enabled)
        {
            ink = hovered ? GamePalette.Lighten(accent, 0.15f) : StageInks.Strong;
        }

        ProgressRing.CenterIcon(drawList, center, icon, ink, radius * 0.8f);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + radius + 9f * scale), caption,
            enabled ? StageInks.Strong : StageInks.Muted with { W = 0.55f }, TextStyles.Caption2);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.HoverClickCircle(center, radius);
    }

    private static float WavePhase(float wave, int distance, int spread)
    {
        var start = distance / (float)Math.Max(1, spread) * 0.6f;
        var local = (wave - start) / 0.4f;
        if (local <= 0f || local >= 1f)
        {
            return 0f;
        }

        return MathF.Sin(local * MathF.PI);
    }

    private static void DrawWall(ImDrawListPtr drawList, Rect rect, float pitch, float radius, float scale)
    {
        var block = rect.Inset(pitch * WallGap * 0.5f);
        StageCell.Draw(drawList, block, Wall, CellDepth.Raised, radius, scale);
        var top = new Rect(block.Min, new Vector2(block.Max.X, block.Min.Y + block.Height * 0.42f));
        Squircle.FillVerticalGradient(drawList, top.Min, top.Max, radius,
            ImGui.GetColorU32(WallTop), ImGui.GetColorU32(WallTop with { W = 0f }));
        var seam = ImGui.GetColorU32(Ink with { W = 0.12f });
        var middle = block.Center.Y;
        drawList.AddLine(new Vector2(block.Min.X + radius * 0.5f, middle), new Vector2(block.Max.X - radius * 0.5f, middle),
            seam, MathF.Max(1f, scale));
        drawList.AddLine(new Vector2(block.Center.X, block.Min.Y + radius * 0.4f), new Vector2(block.Center.X, middle),
            seam, MathF.Max(1f, scale));
    }

    private static void DrawTarget(ImDrawListPtr drawList, Vector2 center, float pitch, Vector4 accent, bool covered,
        float scale)
    {
        if (covered)
        {
            return;
        }

        var pulse = 0.5f + 0.5f * Pulse.Wave(Pulse.Calm);
        var radius = pitch * (0.26f + 0.02f * pulse);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(accent with { W = 0.14f + 0.08f * pulse }), 24);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(accent with { W = 0.75f }), 24, MathF.Max(1.5f * scale, pitch * 0.04f));
        var gem = pitch * 0.09f;
        drawList.AddQuadFilled(center + new Vector2(0f, -gem * 1.3f), center + new Vector2(gem, 0f),
            center + new Vector2(0f, gem * 1.3f), center + new Vector2(-gem, 0f), ImGui.GetColorU32(accent with { W = 0.9f }));
    }

    private static void DrawWing(ImDrawListPtr drawList, Vector2 root, float side, float pitch, float flap)
    {
        var span = pitch * (0.15f + 0.04f * flap);
        var tip = root + new Vector2(side * span, -pitch * (0.1f + 0.05f * flap));
        var lower = root + new Vector2(side * span * 0.7f, pitch * 0.06f);
        drawList.AddTriangleFilled(root, tip, lower, ImGui.GetColorU32(Wing));
        drawList.AddTriangleFilled(root, root + new Vector2(side * span * 0.55f, -pitch * 0.02f), lower,
            ImGui.GetColorU32(GamePalette.Lighten(Wing, 0.2f)));
    }
}
