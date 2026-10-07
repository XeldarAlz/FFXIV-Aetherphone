using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Crater;

internal readonly struct CraterLayout
{
    public readonly Rect Weapons;
    public readonly Rect Pad;
    public readonly Vector2 FireCenter;
    public readonly float FireRadius;
    public readonly Rect Teams;
    public readonly Rect Wind;
    public readonly float HintY;

    public CraterLayout(Rect weapons, Rect pad, Vector2 fireCenter, float fireRadius, Rect teams, Rect wind,
        float hintY)
    {
        Weapons = weapons;
        Pad = pad;
        FireCenter = fireCenter;
        FireRadius = fireRadius;
        Teams = teams;
        Wind = wind;
        HintY = hintY;
    }

    public bool Covers(Vector2 point)
    {
        var fireReach = FireRadius * 1.15f;
        return Weapons.Contains(point) || Pad.Contains(point) || Teams.Contains(point) ||
               Vector2.DistanceSquared(point, FireCenter) <= fireReach * fireReach;
    }
}

internal static class CraterControls
{
    public const float WindWidth = 84f;
    private const float SlotWidth = 46f;
    private const float SlotHeight = 44f;
    private const float RowPadding = 5f;
    private const float FireRadius = 31f;
    private const float Margin = 14f;
    private const float PadWidth = 196f;
    private const float PadHeight = 64f;
    private const float StackGap = 8f;
    private const float TeamWidth = 156f;
    private const float TeamRowHeight = 19f;
    private const float TeamPadding = 7f;
    private const float TeamBarWidth = 52f;
    private const float IconSize = 20f;
    private const float WindDrop = 4f;
    private const float WindRowClearance = 112f;
    private const float WindInset = 10f;
    private const float WindLabelGap = 6f;
    private const float WindGlyphShare = 1.05f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Dim = new(1f, 1f, 1f, 0.32f);
    private static readonly Vector4 Calm = new(1f, 1f, 1f, 0.5f);
    private static readonly Vector4 Track = new(1f, 1f, 1f, 0.18f);
    private static readonly Vector4 Gale = new(1f, 0.62f, 0.3f, 1f);
    private static readonly Vector4 BarBack = new(0f, 0f, 0f, 0.4f);

    public static CraterLayout Layout(Rect full, Rect safe, int teams, float scale)
    {
        var margin = Margin * scale;
        var fireRadius = FireRadius * scale;
        var fireCenter = new Vector2(full.Max.X - margin - fireRadius, full.Max.Y - margin - fireRadius);
        var padWidth = PadWidth * scale;
        var padHeight = PadHeight * scale;
        var pad = new Rect(new Vector2(full.Min.X + margin * 0.5f, full.Max.Y - margin * 0.5f - padHeight),
            new Vector2(full.Min.X + margin * 0.5f + padWidth, full.Max.Y - margin * 0.5f));
        var rowWidth = (SlotWidth * CraterRules.WeaponCount + RowPadding * 2f) * scale;
        var rowHeight = SlotHeight * scale;
        var middleLeft = pad.Max.X + margin;
        var middleRight = fireCenter.X - fireRadius - margin;
        Rect weapons;
        if (middleRight - middleLeft >= rowWidth)
        {
            var centerX = MathF.Max(middleLeft + rowWidth * 0.5f, MathF.Min(full.Center.X, middleRight - rowWidth * 0.5f));
            weapons = new Rect(new Vector2(centerX - rowWidth * 0.5f, full.Max.Y - margin - rowHeight),
                new Vector2(centerX + rowWidth * 0.5f, full.Max.Y - margin));
        }
        else
        {
            var width = MathF.Min(rowWidth, full.Width - margin * 2f);
            var bottom = MathF.Min(pad.Min.Y, fireCenter.Y - fireRadius) - StackGap * scale;
            weapons = new Rect(new Vector2(full.Center.X - width * 0.5f, bottom - rowHeight),
                new Vector2(full.Center.X + width * 0.5f, bottom));
        }

        var teamHeight = (TeamRowHeight * teams + TeamPadding * 2f) * scale;
        var teamWidth = TeamWidth * scale;
        var windSize = new Vector2(WindWidth, StageLayout.SecondaryHeight) * scale;
        var landscape = full.IsLandscape();
        var teamTopLeft = landscape
            ? new Vector2(full.Max.X - margin - teamWidth, full.Min.Y + margin)
            : new Vector2(safe.Min.X, safe.Min.Y);
        var windTopLeft = landscape
            ? LandscapeWind(full, teamTopLeft.X - margin, windSize, scale)
            : new Vector2(safe.Max.X - windSize.X, safe.Min.Y);
        var teamsRect = new Rect(teamTopLeft, teamTopLeft + new Vector2(teamWidth, teamHeight));
        var windRect = new Rect(windTopLeft, windTopLeft + windSize);
        return new CraterLayout(weapons, pad, fireCenter, fireRadius, teamsRect, windRect,
            weapons.Min.Y - StackGap * scale);
    }

    private static Vector2 LandscapeWind(Rect full, float right, Vector2 size, float scale)
    {
        var left = right - size.X;
        if (left >= full.Center.X + WindRowClearance * scale)
        {
            return new Vector2(left, full.Min.Y + StageLayout.ChipCenterY * scale - size.Y * 0.5f);
        }

        return new Vector2(full.Center.X - size.X * 0.5f, full.Min.Y + (StageLayout.ChromeBand + WindDrop) * scale);
    }

    public static int Weapons(ImDrawListPtr drawList, Rect row, CraterBoard board, CraterLabels labels, int team,
        Vector4 accent, bool enabled, float scale)
    {
        Span<int> ammo = stackalloc int[CraterRules.WeaponCount];
        for (var slot = 0; slot < CraterRules.WeaponCount; slot++)
        {
            ammo[slot] = board.Ammo(team, (CraterWeapon)slot);
        }

        return Weapons(drawList, row, ammo, board.Weapon, board.Fuse, labels, accent, enabled, scale);
    }

    public static int Weapons(ImDrawListPtr drawList, Rect row, ReadOnlySpan<int> ammo, CraterWeapon selection,
        int fuse, CraterLabels labels, Vector4 accent, bool enabled, float scale)
    {
        var alpha = enabled ? 1f : 0.55f;
        Material.Frosted(drawList, row.Min, row.Max, row.Height * 0.5f, scale, 0.92f * alpha);
        var padding = RowPadding * scale;
        var slotWidth = (row.Width - padding * 2f) / CraterRules.WeaponCount;
        var clicked = -1;
        for (var slot = 0; slot < CraterRules.WeaponCount; slot++)
        {
            var weapon = (CraterWeapon)slot;
            var min = new Vector2(row.Min.X + padding + slot * slotWidth, row.Min.Y + padding * 0.6f);
            var max = new Vector2(min.X + slotWidth, row.Max.Y - padding * 0.6f);
            var left = slot < ammo.Length ? ammo[slot] : 0;
            var available = left != 0;
            var selected = selection == weapon;
            var hovered = enabled && available && UiInteract.Hover(min, max);
            var radius = (max.Y - min.Y) * 0.32f;
            if (selected)
            {
                Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.34f * alpha }));
                Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.9f * alpha }),
                    1.5f * scale);
            }
            else if (hovered)
            {
                Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.45f }), 1f * scale);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var ink = available ? White with { W = alpha } : Dim;
            var center = (min + max) * 0.5f;
            CraterArt.WeaponIcon(drawList, center - new Vector2(0f, 4f * scale), IconSize * scale, weapon, ink);
            var badge = weapon == CraterWeapon.Grenade ? labels.FuseLabel(fuse)
                : left >= 0 ? GameNumber.Label(left) : string.Empty;
            if (badge.Length > 0)
            {
                Typography.DrawCentered(drawList, new Vector2(center.X, max.Y - 7f * scale), badge,
                    available ? accent with { W = alpha } : Dim, TextStyles.Caption2);
            }

            if (hovered && UiInteract.HoverClick(min, max))
            {
                clicked = slot;
            }
        }

        return clicked;
    }

    public static void Fire(ImDrawListPtr drawList, Vector2 center, float radius, CraterWeapon weapon, float charge,
        Vector4 accent, bool enabled, bool held, float scale)
    {
        var alpha = enabled ? 1f : 0.45f;
        var size = held ? radius * 0.94f : radius;
        if (held)
        {
            ProgressRing.Glow(center, size * 1.25f, accent, 0.6f);
        }

        var corner = new Vector2(size, size);
        Material.Frosted(drawList, center - corner, center + corner, size, scale, 0.94f * alpha);
        var ring = size - 4f * scale;
        var thickness = 3.5f * scale;
        ProgressRing.Track(drawList, center, ring, thickness, Track with { W = Track.W * alpha });
        if (charge > 0f)
        {
            ProgressRing.Fill(drawList, center, ring, thickness, charge,
                Vector4.Lerp(accent, CraterArt.Fire, charge) with { W = alpha });
        }

        CraterArt.WeaponIcon(drawList, center, size * 0.85f, weapon, (held ? accent : White) with { W = alpha });
    }

    public static void Wind(ImDrawListPtr drawList, Rect rect, int level, float time, float scale)
    {
        StageHud.Capsule(drawList, rect, scale);
        var strength = Math.Abs(level);
        var gale = strength >= CraterRules.MaxWindLevel * 7 / 10;
        var label = GameNumber.Label(strength);
        var labelWidth = Typography.Measure(label, TextStyles.FootnoteEmphasized).X;
        var right = rect.Max.X - WindInset * scale;
        Typography.DrawCentered(drawList, new Vector2(right - labelWidth * 0.5f, rect.Center.Y), label,
            gale ? Gale : White, TextStyles.FootnoteEmphasized);
        var glyphCenter = new Vector2(
            (rect.Min.X + WindInset * scale + right - labelWidth - WindLabelGap * scale) * 0.5f, rect.Center.Y);
        var ink = level == 0 ? Calm : gale ? Gale : White;
        CraterArt.Wind(drawList, glyphCenter, rect.Height * WindGlyphShare, level,
            strength / (float)CraterRules.MaxWindLevel, ink, time);
    }

    public static void Teams(ImDrawListPtr drawList, Rect rect, CraterBoard board, CraterLabels labels, float scale)
    {
        Span<int> health = stackalloc int[CraterRules.MaxTeams];
        var teams = Math.Min(board.TeamCount, CraterRules.MaxTeams);
        for (var team = 0; team < teams; team++)
        {
            health[team] = board.TeamHealth(team);
        }

        Teams(drawList, rect, labels.TeamNames[..teams], health[..teams],
            CraterRules.MaxHealth * CraterRules.MooglesPerTeam, board.Over ? CraterBoard.NoTeam : board.ActiveTeam,
            scale);
    }

    public static void Teams(ImDrawListPtr drawList, Rect rect, ReadOnlySpan<string> names, ReadOnlySpan<int> health,
        float full, int activeTeam, float scale)
    {
        Material.Frosted(drawList, rect.Min, rect.Max, 12f * scale, scale, 0.88f);
        var padding = TeamPadding * scale;
        var rowHeight = TeamRowHeight * scale;
        var barWidth = TeamBarWidth * scale;
        var teams = Math.Min(names.Length, health.Length);
        for (var team = 0; team < teams; team++)
        {
            var top = rect.Min.Y + padding + team * rowHeight;
            var centerY = top + rowHeight * 0.5f;
            var color = GameSeats.Color(team);
            var alive = health[team] > 0;
            var ink = alive ? White : Dim;
            var active = alive && team == activeTeam;
            var dotCenter = new Vector2(rect.Min.X + padding + 4f * scale, centerY);
            drawList.AddCircleFilled(dotCenter, (active ? 5f : 3.5f) * scale,
                ImGui.GetColorU32(alive ? color : Dim), 12);
            var nameLeft = dotCenter.X + 9f * scale;
            var barRight = rect.Max.X - padding;
            var barLeft = barRight - barWidth;
            var name = Typography.FitText(names[team], barLeft - nameLeft - 6f * scale, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(nameLeft, centerY - Typography.LineHeight(TextStyles.Caption1) * 0.5f),
                name, active ? GamePalette.Lighten(color, 0.4f) : ink, TextStyles.Caption1);
            var barHeight = 5f * scale;
            var barMin = new Vector2(barLeft, centerY - barHeight * 0.5f);
            var barMax = new Vector2(barRight, centerY + barHeight * 0.5f);
            drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(BarBack), barHeight);
            var fraction = Math.Clamp(health[team] / full, 0f, 1f);
            if (fraction > 0f)
            {
                drawList.AddRectFilled(barMin, new Vector2(barMin.X + barWidth * fraction, barMax.Y),
                    ImGui.GetColorU32(color), barHeight);
            }
        }
    }
}
