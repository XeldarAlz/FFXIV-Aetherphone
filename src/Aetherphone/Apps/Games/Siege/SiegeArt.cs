using Aetherphone.Apps.Games.Framework;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Siege;

internal static class SiegeArt
{
    public static readonly Vector4 Leaf = new(0.36f, 0.72f, 0.30f, 1f);
    public static readonly Vector4 LeafDark = new(0.22f, 0.52f, 0.22f, 1f);
    public static readonly Vector4 Sun = new(1f, 0.84f, 0.30f, 1f);
    public static readonly Vector4 SunCore = new(1f, 0.97f, 0.78f, 1f);
    public static readonly Vector4 Frost = new(0.58f, 0.84f, 1f, 1f);
    public static readonly Vector4 FrostCore = new(0.92f, 0.98f, 1f, 1f);
    public static readonly Vector4 Bramble = new(0.40f, 0.34f, 0.20f, 1f);
    public static readonly Vector4 Cap = new(0.90f, 0.26f, 0.24f, 1f);
    public static readonly Vector4 Dirt = new(0.52f, 0.38f, 0.24f, 1f);
    public static readonly Vector4 Pot = new(0.78f, 0.44f, 0.26f, 1f);
    public static readonly Vector4 SeedGreen = new(0.62f, 0.92f, 0.36f, 1f);
    private const int RoundSegments = 18;
    private const float Stride = 9f;
    private static readonly Vector2[] Round = BuildCircle(RoundSegments);
    private static readonly Vector4 Stem = new(0.28f, 0.58f, 0.24f, 1f);
    private static readonly Vector4 SproutHead = new(0.46f, 0.82f, 0.34f, 1f);
    private static readonly Vector4 SproutDark = new(0.30f, 0.62f, 0.24f, 1f);
    private static readonly Vector4 FrostHead = new(0.50f, 0.78f, 0.98f, 1f);
    private static readonly Vector4 FrostDark = new(0.32f, 0.56f, 0.86f, 1f);
    private static readonly Vector4 Petal = new(1f, 0.80f, 0.26f, 1f);
    private static readonly Vector4 PetalTip = new(1f, 0.90f, 0.50f, 1f);
    private static readonly Vector4 Disc = new(0.78f, 0.46f, 0.18f, 1f);
    private static readonly Vector4 DiscLight = new(0.92f, 0.60f, 0.26f, 1f);
    private static readonly Vector4 BrambleTop = new(0.50f, 0.58f, 0.26f, 1f);
    private static readonly Vector4 BrambleVine = new(0.30f, 0.46f, 0.20f, 1f);
    private static readonly Vector4 Thorn = new(0.94f, 0.88f, 0.70f, 1f);
    private static readonly Vector4 Crack = new(0.16f, 0.12f, 0.08f, 0.9f);
    private static readonly Vector4 CapSpot = new(1f, 0.96f, 0.90f, 1f);
    private static readonly Vector4 Stalk = new(0.96f, 0.90f, 0.78f, 1f);
    private static readonly Vector4 Fuse = new(1f, 0.64f, 0.22f, 1f);
    private static readonly Vector4 Ink = new(0.12f, 0.10f, 0.10f, 1f);
    private static readonly Vector4 Glint = new(1f, 1f, 1f, 0.9f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.26f);
    private static readonly Vector4 Highlight = new(1f, 1f, 1f, 0.32f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Mouth = new(0.36f, 0.14f, 0.14f, 1f);
    private static readonly Vector4 PotRim = new(0.62f, 0.32f, 0.18f, 1f);
    private static readonly Vector4 Crown = new(1f, 0.80f, 0.24f, 1f);
    private static readonly Vector4 HealthTrack = new(0f, 0f, 0f, 0.45f);
    private static readonly Vector4 HealthHigh = new(0.48f, 0.90f, 0.40f, 1f);
    private static readonly Vector4 HealthLow = new(0.96f, 0.34f, 0.28f, 1f);
    private static readonly Vector4 MoundTop = new(0.62f, 0.48f, 0.30f, 1f);
    private static readonly Vector4 Steel = new(0.78f, 0.82f, 0.88f, 1f);
    private static readonly Vector4 SteelDark = new(0.52f, 0.56f, 0.64f, 1f);
    private static readonly Vector4 Handle = new(0.66f, 0.46f, 0.28f, 1f);
    private static readonly Vector4[] BodyColors =
    {
        new(0.94f, 0.76f, 0.64f, 1f), new(0.98f, 0.62f, 0.38f, 1f), new(0.88f, 0.70f, 0.60f, 1f),
        new(0.80f, 0.68f, 0.96f, 1f), new(0.70f, 0.58f, 0.42f, 1f), new(0.64f, 0.40f, 0.66f, 1f),
    };

    private static readonly float[] BodySizes = { 1f, 0.88f, 1.04f, 0.84f, 1f, 1.9f };

    public static float Size(EnemyKind kind) => BodySizes[(int)kind];

    public static Vector4 BodyColor(EnemyKind kind) => BodyColors[(int)kind];

    public static Vector4 Tint(DefenderKind kind) => kind switch
    {
        DefenderKind.Sunbloom => Sun,
        DefenderKind.Thornwall => BrambleTop,
        DefenderKind.Frostbud => Frost,
        DefenderKind.Bombcap => Cap,
        _ => SproutHead,
    };

    public static void Ellipse(ImDrawListPtr drawList, Vector2 center, float radiusX, float radiusY, float angle,
        uint color)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        for (var index = 0; index < Round.Length; index++)
        {
            var local = new Vector2(Round[index].X * radiusX, Round[index].Y * radiusY);
            drawList.PathLineTo(center + new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos));
        }

        drawList.PathFillConvex(color);
    }

    public static void Oval(ImDrawListPtr drawList, Vector2 center, float radiusX, float radiusY, uint color)
    {
        for (var index = 0; index < Round.Length; index++)
        {
            drawList.PathLineTo(center + new Vector2(Round[index].X * radiusX, Round[index].Y * radiusY));
        }

        drawList.PathFillConvex(color);
    }

    public static void Dome(ImDrawListPtr drawList, Vector2 baseCenter, float radiusX, float radiusY, uint color)
    {
        var half = RoundSegments / 2;
        for (var index = half; index <= RoundSegments; index++)
        {
            var point = Round[index % RoundSegments];
            drawList.PathLineTo(baseCenter + new Vector2(point.X * radiusX, point.Y * radiusY));
        }

        drawList.PathFillConvex(color);
    }

    public static void GroundShadow(ImDrawListPtr drawList, Vector2 feet, float unit, float width, float alpha)
    {
        Oval(drawList, feet, unit * 0.3f * width, unit * 0.085f * width, Color(Shadow, alpha));
    }

    public static void Defender(ImDrawListPtr drawList, in SiegeDefender defender, Vector2 center, float unit,
        float time, float alpha)
    {
        var grow = defender.Age < 0.3f ? MathF.Max(0.05f, GameJuice.PopIn(defender.Age / 0.3f)) : 1f;
        var size = unit * grow;
        var sway = MathF.Sin(time * 2.1f + center.X * 0.05f) * 0.03f;
        GroundShadow(drawList, center + new Vector2(0f, unit * 0.34f), unit, defender.Kind == DefenderKind.Thornwall ? 1.5f : 1f,
            alpha);
        switch (defender.Kind)
        {
            case DefenderKind.Sunbloom:
                Sunbloom(drawList, center, size, sway, defender.Recoil, time, defender.Flash, alpha);
                return;
            case DefenderKind.Thornwall:
                Thornwall(drawList, center, size, defender.HealthFraction, defender.Flash, alpha);
                return;
            case DefenderKind.Bombcap:
                Bombcap(drawList, center, size, defender.Timer / SiegeRules.BombFuseSeconds, time, alpha);
                return;
            default:
                Shooter(drawList, center, size, sway, defender.Recoil, defender.Flash,
                    defender.Kind == DefenderKind.Frostbud, time, alpha);
                return;
        }
    }

    public static void Card(ImDrawListPtr drawList, DefenderKind kind, Vector2 center, float unit, float time,
        float alpha)
    {
        switch (kind)
        {
            case DefenderKind.Sunbloom:
                Sunbloom(drawList, center, unit, 0f, 0f, time, 0f, alpha);
                return;
            case DefenderKind.Thornwall:
                Thornwall(drawList, center, unit, 1f, 0f, alpha);
                return;
            case DefenderKind.Bombcap:
                Bombcap(drawList, center, unit, 1f, time, alpha);
                return;
            default:
                Shooter(drawList, center, unit, 0f, 0f, 0f, kind == DefenderKind.Frostbud, time, alpha);
                return;
        }
    }

    public static void Shooter(ImDrawListPtr drawList, Vector2 center, float unit, float sway, float recoil,
        float flash, bool frost, float time, float alpha)
    {
        var headColor = frost ? FrostHead : SproutHead;
        var darkColor = frost ? FrostDark : SproutDark;
        var stemBase = center + new Vector2(0f, unit * 0.32f);
        var head = center + new Vector2(sway * unit, -unit * 0.04f + recoil * unit * 0.05f);
        var leaf = Color(frost ? FrostDark : LeafDark, alpha);
        Ellipse(drawList, stemBase + new Vector2(-unit * 0.15f, -unit * 0.04f), unit * 0.17f, unit * 0.065f, -0.5f, leaf);
        Ellipse(drawList, stemBase + new Vector2(unit * 0.15f, -unit * 0.04f), unit * 0.17f, unit * 0.065f, 0.5f, leaf);
        drawList.AddLine(stemBase, head, Color(Stem, alpha), unit * 0.075f);
        if (frost)
        {
            ProgressRing.Glow(head, unit * 0.32f, Frost, 0.35f * alpha);
            var crystal = Color(FrostCore, alpha);
            for (var spike = 0; spike < 5; spike++)
            {
                var angle = spike * MathF.Tau / 5f + time * 0.6f;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var side = new Vector2(-direction.Y, direction.X) * unit * 0.045f;
                var root = head + direction * unit * 0.18f;
                drawList.AddTriangleFilled(root + side, root - side, root + direction * unit * 0.12f, crystal);
            }
        }

        var muzzleTop = head.Y - unit * 0.34f + recoil * unit * 0.07f;
        drawList.AddRectFilled(new Vector2(head.X - unit * 0.075f, muzzleTop), new Vector2(head.X + unit * 0.075f, head.Y - unit * 0.08f),
            Color(darkColor, alpha), unit * 0.04f);
        Oval(drawList, new Vector2(head.X, muzzleTop), unit * 0.09f, unit * 0.045f, Color(darkColor, alpha));
        Oval(drawList, new Vector2(head.X, muzzleTop), unit * 0.055f, unit * 0.026f, Color(Ink, alpha));
        Oval(drawList, head, unit * 0.22f * (1f + 0.12f * recoil), unit * 0.2f * (1f - 0.1f * recoil),
            Color(Mix(headColor, White, flash * 0.6f), alpha));
        Ellipse(drawList, head + new Vector2(-unit * 0.08f, -unit * 0.07f), unit * 0.07f, unit * 0.045f, -0.6f,
            Color(Highlight, alpha));
        Eyes(drawList, head + new Vector2(0f, unit * 0.02f), unit * 0.08f, unit * 0.034f, alpha);
    }

    public static void Sunbloom(ImDrawListPtr drawList, Vector2 center, float unit, float sway, float glow, float time,
        float flash, float alpha)
    {
        var stemBase = center + new Vector2(0f, unit * 0.32f);
        var flower = center + new Vector2(sway * unit, -unit * 0.08f);
        var leaf = Color(LeafDark, alpha);
        Ellipse(drawList, stemBase + new Vector2(-unit * 0.14f, -unit * 0.05f), unit * 0.15f, unit * 0.06f, -0.55f, leaf);
        Ellipse(drawList, stemBase + new Vector2(unit * 0.14f, -unit * 0.05f), unit * 0.15f, unit * 0.06f, 0.55f, leaf);
        drawList.AddLine(stemBase, flower, Color(Stem, alpha), unit * 0.07f);
        ProgressRing.Glow(flower, unit * (0.34f + 0.12f * glow), Sun, (0.3f + 0.6f * glow) * alpha);
        var spin = MathF.Sin(time * 0.8f) * 0.15f;
        var petal = Color(Mix(Petal, White, flash * 0.5f), alpha);
        var tip = Color(PetalTip, alpha);
        for (var index = 0; index < 10; index++)
        {
            var angle = index * MathF.Tau / 10f + spin;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            Ellipse(drawList, flower + direction * unit * 0.18f, unit * 0.11f, unit * 0.055f, angle, petal);
            drawList.AddCircleFilled(flower + direction * unit * 0.25f, unit * 0.025f, tip, 6);
        }

        drawList.AddCircleFilled(flower, unit * 0.135f, Color(Disc, alpha), 20);
        drawList.AddCircleFilled(flower - new Vector2(unit * 0.03f, unit * 0.03f), unit * 0.08f, Color(DiscLight, alpha), 16);
        Eyes(drawList, flower - new Vector2(0f, unit * 0.015f), unit * 0.05f, unit * 0.022f, alpha);
        drawList.PathArcTo(flower + new Vector2(0f, unit * 0.025f), unit * 0.045f, 0.3f, MathF.PI - 0.3f, 8);
        drawList.PathStroke(Color(Ink, alpha), ImDrawFlags.None, MathF.Max(1f, unit * 0.018f));
    }

    public static void Thornwall(ImDrawListPtr drawList, Vector2 center, float unit, float health, float flash,
        float alpha)
    {
        var min = center + new Vector2(-unit * 0.4f, -unit * 0.3f);
        var max = center + new Vector2(unit * 0.4f, unit * 0.32f);
        var thorn = Color(Thorn, alpha);
        for (var index = 0; index < 4; index++)
        {
            var x = min.X + unit * (0.14f + index * 0.17f);
            drawList.AddTriangleFilled(new Vector2(x - unit * 0.05f, min.Y + unit * 0.06f), new Vector2(x + unit * 0.05f, min.Y + unit * 0.06f),
                new Vector2(x, min.Y - unit * 0.1f), thorn);
        }

        for (var side = -1; side <= 1; side += 2)
        {
            var edge = side < 0 ? min.X : max.X;
            for (var index = 0; index < 2; index++)
            {
                var y = center.Y + unit * (-0.08f + index * 0.22f);
                drawList.AddTriangleFilled(new Vector2(edge - side * unit * 0.04f, y - unit * 0.045f),
                    new Vector2(edge - side * unit * 0.04f, y + unit * 0.045f), new Vector2(edge + side * unit * 0.1f, y), thorn);
            }
        }

        var body = Mix(Bramble, White, flash * 0.5f);
        drawList.AddRectFilled(min, max, Color(body, alpha), unit * 0.16f);
        drawList.AddRectFilled(min, new Vector2(max.X, center.Y), Color(Mix(BrambleTop, White, flash * 0.5f), alpha), unit * 0.16f);
        var vine = Color(BrambleVine, alpha);
        var vineWidth = MathF.Max(1f, unit * 0.045f);
        drawList.AddBezierCubic(new Vector2(min.X + unit * 0.04f, center.Y + unit * 0.1f), new Vector2(center.X - unit * 0.2f, min.Y),
            new Vector2(center.X + unit * 0.1f, max.Y), new Vector2(max.X - unit * 0.04f, center.Y - unit * 0.12f), vine, vineWidth, 12);
        drawList.AddBezierCubic(new Vector2(min.X + unit * 0.06f, max.Y - unit * 0.08f), new Vector2(center.X, center.Y),
            new Vector2(center.X + unit * 0.2f, max.Y), new Vector2(max.X - unit * 0.06f, max.Y - unit * 0.1f), vine, vineWidth, 12);
        var face = center + new Vector2(0f, -unit * 0.06f);
        Eyes(drawList, face, unit * 0.11f, unit * 0.035f, alpha);
        var brow = Color(Ink, alpha);
        drawList.AddLine(face + new Vector2(-unit * 0.17f, -unit * 0.09f), face + new Vector2(-unit * 0.05f, -unit * 0.05f), brow,
            MathF.Max(1f, unit * 0.03f));
        drawList.AddLine(face + new Vector2(unit * 0.17f, -unit * 0.09f), face + new Vector2(unit * 0.05f, -unit * 0.05f), brow,
            MathF.Max(1f, unit * 0.03f));
        var crack = Color(Crack, alpha);
        var crackWidth = MathF.Max(1f, unit * 0.035f);
        if (health < 0.66f)
        {
            drawList.AddLine(new Vector2(min.X + unit * 0.12f, min.Y + unit * 0.02f), new Vector2(min.X + unit * 0.24f, center.Y), crack, crackWidth);
            drawList.AddLine(new Vector2(min.X + unit * 0.24f, center.Y), new Vector2(min.X + unit * 0.16f, max.Y - unit * 0.12f), crack, crackWidth);
            drawList.AddLine(new Vector2(min.X + unit * 0.24f, center.Y), new Vector2(min.X + unit * 0.34f, center.Y + unit * 0.06f), crack, crackWidth);
        }

        if (health >= 0.33f)
        {
            return;
        }

        drawList.AddLine(new Vector2(max.X - unit * 0.1f, min.Y + unit * 0.04f), new Vector2(max.X - unit * 0.26f, center.Y + unit * 0.04f), crack, crackWidth);
        drawList.AddLine(new Vector2(max.X - unit * 0.26f, center.Y + unit * 0.04f), new Vector2(max.X - unit * 0.18f, max.Y - unit * 0.04f), crack, crackWidth);
        drawList.AddTriangleFilled(new Vector2(max.X - unit * 0.02f, min.Y + unit * 0.1f), new Vector2(max.X - unit * 0.02f, min.Y + unit * 0.28f),
            new Vector2(max.X - unit * 0.14f, min.Y + unit * 0.18f), crack);
    }

    public static void Bombcap(ImDrawListPtr drawList, Vector2 center, float unit, float fuse, float time, float alpha)
    {
        var urgency = 1f - Math.Clamp(fuse, 0f, 1f);
        var throb = 1f + urgency * 0.16f * MathF.Abs(MathF.Sin(time * (8f + 34f * urgency)));
        var blink = urgency * (0.5f + 0.5f * MathF.Sin(time * (10f + 40f * urgency)));
        drawList.AddRectFilled(center + new Vector2(-unit * 0.11f, -unit * 0.02f), center + new Vector2(unit * 0.11f, unit * 0.3f),
            Color(Stalk, alpha), unit * 0.07f);
        Eyes(drawList, center + new Vector2(0f, unit * 0.12f), unit * 0.05f, unit * 0.026f, alpha);
        var capBase = center + new Vector2(0f, unit * 0.03f);
        if (urgency > 0f)
        {
            ProgressRing.Glow(capBase - new Vector2(0f, unit * 0.12f), unit * 0.4f * throb, Fuse, blink * 0.8f * alpha);
        }

        Dome(drawList, capBase, unit * 0.34f * throb, unit * 0.32f * throb, Color(Mix(Cap, White, blink * 0.55f), alpha));
        var spot = Color(CapSpot, alpha);
        drawList.AddCircleFilled(capBase + new Vector2(-unit * 0.15f, -unit * 0.1f) * throb, unit * 0.055f, spot, 10);
        drawList.AddCircleFilled(capBase + new Vector2(unit * 0.05f, -unit * 0.2f) * throb, unit * 0.06f, spot, 10);
        drawList.AddCircleFilled(capBase + new Vector2(unit * 0.18f, -unit * 0.07f) * throb, unit * 0.045f, spot, 10);
        var sparkAt = capBase - new Vector2(0f, unit * 0.36f * throb);
        drawList.AddLine(capBase - new Vector2(0f, unit * 0.3f * throb), sparkAt, Color(Bramble, alpha), MathF.Max(1f, unit * 0.03f));
        drawList.AddCircleFilled(sparkAt, unit * (0.035f + 0.025f * blink), Color(Fuse, alpha), 8);
    }

    public static void Mandragora(ImDrawListPtr drawList, in SiegeEnemy enemy, Vector2 feet, float unit, float time,
        float alpha)
    {
        var kind = enemy.Kind;
        var size = unit * Size(kind);
        var gait = enemy.Stride * Stride;
        var walking = enemy.State == EnemyState.Walking;
        var bob = walking ? MathF.Abs(MathF.Sin(gait)) * size * 0.05f : 0f;
        var altitude = kind == EnemyKind.Flyer ? unit * (0.26f + 0.04f * MathF.Sin(time * 5f + enemy.Id)) : 0f;
        GroundShadow(drawList, feet, unit, Size(kind) * (kind == EnemyKind.Flyer ? 0.7f : 1f), alpha);
        var rise = enemy.State == EnemyState.Surfacing
            ? 1f - Math.Clamp(enemy.StateTimer / SiegeRules.DiggerSurfaceSeconds, 0f, 1f)
            : 1f;
        var squash = enemy.Squash;
        var radiusX = size * 0.27f * (1f + 0.25f * squash);
        var radiusY = size * 0.3f * (1f - 0.2f * squash) * (0.4f + 0.6f * rise);
        var body = feet - new Vector2(0f, radiusY + size * 0.06f + bob + altitude);
        var tint = BodyColor(kind);
        if (enemy.Slowed)
        {
            tint = Mix(tint, Frost, 0.5f);
        }

        tint = Mix(tint, White, squash * 0.55f);
        if (kind != EnemyKind.Flyer && rise >= 1f)
        {
            Legs(drawList, feet - new Vector2(0f, altitude), size, gait, walking, Color(Mix(tint, Ink, 0.25f), alpha));
        }

        if (kind == EnemyKind.Flyer)
        {
            Wings(drawList, body, size, time + enemy.Id * 0.37f, alpha);
        }

        var swing = walking ? MathF.Sin(gait) * 0.5f : MathF.Sin(time * 14f) * 0.4f;
        var arm = Color(Mix(tint, Ink, 0.15f), alpha);
        Ellipse(drawList, body + new Vector2(-radiusX * 0.98f, size * 0.04f), size * 0.08f, size * 0.045f, 1.2f + swing, arm);
        Ellipse(drawList, body + new Vector2(radiusX * 0.98f, size * 0.04f), size * 0.08f, size * 0.045f, -1.2f - swing, arm);
        Oval(drawList, body, radiusX, radiusY, Color(tint, alpha));
        Oval(drawList, body + new Vector2(0f, radiusY * 0.35f), radiusX * 0.62f, radiusY * 0.5f,
            Color(Mix(tint, White, 0.25f), alpha * 0.8f));
        Ellipse(drawList, body + new Vector2(-radiusX * 0.4f, -radiusY * 0.45f), radiusX * 0.28f, radiusY * 0.16f, -0.5f,
            Color(Highlight, alpha));
        var top = body - new Vector2(0f, radiusY * 0.92f);
        if (enemy.HasPot)
        {
            PotHelmet(drawList, top, size, alpha);
        }
        else
        {
            LeafCrown(drawList, top, size, MathF.Sin(time * 3f + enemy.Id) * 0.15f, kind == EnemyKind.Runner, alpha);
        }

        if (kind == EnemyKind.Boss)
        {
            BossCrown(drawList, top + new Vector2(0f, size * 0.03f), size, alpha);
        }

        var eyeLift = enemy.HasPot ? size * 0.04f : 0f;
        Eyes(drawList, body + new Vector2(0f, -radiusY * 0.12f + eyeLift), size * 0.09f, size * 0.036f, alpha);
        if (kind == EnemyKind.Boss)
        {
            var brow = Color(Ink, alpha);
            var browCenter = body + new Vector2(0f, -radiusY * 0.32f);
            drawList.AddLine(browCenter + new Vector2(-size * 0.16f, -size * 0.04f), browCenter + new Vector2(-size * 0.04f, size * 0.01f), brow,
                MathF.Max(1f, size * 0.03f));
            drawList.AddLine(browCenter + new Vector2(size * 0.16f, -size * 0.04f), browCenter + new Vector2(size * 0.04f, size * 0.01f), brow,
                MathF.Max(1f, size * 0.03f));
        }

        var chomp = enemy.State == EnemyState.Eating ? 0.5f + 0.5f * MathF.Sin(time * 16f + enemy.Id) : 0f;
        Oval(drawList, body + new Vector2(0f, radiusY * 0.3f), size * 0.06f, size * (0.022f + 0.04f * chomp),
            Color(Mouth, alpha));
        if (kind == EnemyKind.Runner && walking)
        {
            var streak = Color(White with { W = 0.55f }, alpha);
            for (var line = -1; line <= 1; line += 2)
            {
                var x = body.X + line * radiusX * 0.5f;
                drawList.AddLine(new Vector2(x, top.Y - size * 0.32f), new Vector2(x, top.Y - size * 0.14f), streak,
                    MathF.Max(1f, size * 0.025f));
            }
        }

        if (enemy.Slowed)
        {
            FrostSparkles(drawList, body, radiusX, time + enemy.Id, alpha);
        }
    }

    public static void Mound(ImDrawListPtr drawList, Vector2 feet, float unit, float stride, float time, float alpha)
    {
        var wobble = MathF.Sin(stride * Stride) * unit * 0.02f;
        var leaf = Color(Leaf, alpha);
        for (var index = -1; index <= 1; index++)
        {
            var angle = index * 0.45f + MathF.Sin(time * 6f + index) * 0.12f;
            var direction = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
            Ellipse(drawList, feet + new Vector2(index * unit * 0.05f, -unit * 0.14f) + direction * unit * 0.1f,
                unit * 0.1f, unit * 0.04f, angle - MathF.PI * 0.5f, leaf);
        }

        Oval(drawList, feet + new Vector2(wobble, -unit * 0.04f), unit * 0.3f, unit * 0.13f, Color(Dirt, alpha));
        Oval(drawList, feet + new Vector2(wobble, -unit * 0.08f), unit * 0.22f, unit * 0.07f, Color(MoundTop, alpha));
        var pebble = Color(Mix(Dirt, Ink, 0.3f), alpha);
        drawList.AddCircleFilled(feet + new Vector2(-unit * 0.2f, -unit * 0.02f), unit * 0.03f, pebble, 6);
        drawList.AddCircleFilled(feet + new Vector2(unit * 0.17f, 0f), unit * 0.025f, pebble, 6);
    }

    public static void HealthBar(ImDrawListPtr drawList, Vector2 center, float width, float height, float fraction,
        float alpha)
    {
        var min = center - new Vector2(width * 0.5f, height * 0.5f);
        var max = center + new Vector2(width * 0.5f, height * 0.5f);
        drawList.AddRectFilled(min, max, Color(HealthTrack, alpha), height * 0.5f);
        var fill = Vector4.Lerp(HealthLow, HealthHigh, fraction);
        drawList.AddRectFilled(min, new Vector2(min.X + width * Math.Clamp(fraction, 0f, 1f), max.Y), Color(fill, alpha),
            height * 0.5f);
    }

    public static void Seed(ImDrawListPtr drawList, Vector2 center, float unit, bool frost, float alpha)
    {
        if (frost)
        {
            drawList.AddCircleFilled(center, unit * 0.15f, Color(Frost with { W = 0.3f }, alpha), 12);
            drawList.AddCircleFilled(center, unit * 0.09f, Color(Frost, alpha), 12);
            drawList.AddCircleFilled(center, unit * 0.045f, Color(FrostCore, alpha), 8);
            return;
        }

        drawList.AddCircleFilled(center, unit * 0.095f, Color(SproutDark, alpha), 12);
        drawList.AddCircleFilled(center, unit * 0.075f, Color(SeedGreen, alpha), 12);
        drawList.AddCircleFilled(center - new Vector2(unit * 0.025f, unit * 0.025f), unit * 0.025f, Color(Glint, alpha), 6);
    }

    public static void Mote(ImDrawListPtr drawList, Vector2 center, float radius, float time, float alpha)
    {
        var pulse = 0.85f + 0.15f * MathF.Sin(time * 5f);
        ProgressRing.Glow(center, radius * 2.1f * pulse, Sun, 0.9f * alpha);
        var ray = Color(Sun with { W = 0.85f }, alpha);
        for (var index = 0; index < 8; index++)
        {
            var angle = index * MathF.Tau / 8f + time * 0.9f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddLine(center + direction * radius * 1.15f, center + direction * radius * (1.55f + 0.15f * pulse), ray,
                MathF.Max(1f, radius * 0.18f));
        }

        drawList.AddCircleFilled(center, radius, Color(Sun, alpha), 18);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.12f, radius * 0.12f), radius * 0.6f, Color(SunCore, alpha), 14);
    }

    public static void Shovel(ImDrawListPtr drawList, Vector2 center, float unit, float alpha)
    {
        var up = new Vector2(0.55f, -0.83f);
        var side = new Vector2(-up.Y, up.X);
        var grip = center + up * unit * 0.36f;
        var neck = center - up * unit * 0.02f;
        drawList.AddLine(neck, grip, Color(Handle, alpha), MathF.Max(1.5f, unit * 0.075f));
        drawList.AddLine(grip - side * unit * 0.1f, grip + side * unit * 0.1f, Color(Handle, alpha), MathF.Max(1.5f, unit * 0.08f));
        var bladeTop = neck;
        var bladeTip = center - up * unit * 0.38f;
        var shoulderLeft = bladeTop - side * unit * 0.13f - up * unit * 0.04f;
        var shoulderRight = bladeTop + side * unit * 0.13f - up * unit * 0.04f;
        var bellyLeft = bladeTip + up * unit * 0.12f - side * unit * 0.12f;
        var bellyRight = bladeTip + up * unit * 0.12f + side * unit * 0.12f;
        var steel = Color(Steel, alpha);
        drawList.AddQuadFilled(shoulderLeft, shoulderRight, bellyRight, bellyLeft, steel);
        drawList.AddTriangleFilled(bellyLeft, bellyRight, bladeTip, steel);
        drawList.AddLine(bladeTop - up * unit * 0.04f, bladeTip + up * unit * 0.06f, Color(SteelDark, alpha),
            MathF.Max(1f, unit * 0.03f));
    }

    public static void Sparkle(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, float alpha)
    {
        var ink = Color(color, alpha);
        drawList.AddTriangleFilled(center + new Vector2(0f, -size), center + new Vector2(size * 0.3f, 0f),
            center + new Vector2(-size * 0.3f, 0f), ink);
        drawList.AddTriangleFilled(center + new Vector2(0f, size), center + new Vector2(size * 0.3f, 0f),
            center + new Vector2(-size * 0.3f, 0f), ink);
        drawList.AddTriangleFilled(center + new Vector2(-size, 0f), center + new Vector2(0f, size * 0.3f),
            center + new Vector2(0f, -size * 0.3f), ink);
        drawList.AddTriangleFilled(center + new Vector2(size, 0f), center + new Vector2(0f, size * 0.3f),
            center + new Vector2(0f, -size * 0.3f), ink);
    }

    public static uint Color(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = color.W * alpha });

    public static Vector4 Mix(Vector4 from, Vector4 to, float amount) =>
        amount <= 0f ? from : Vector4.Lerp(from, to with { W = from.W }, Math.Clamp(amount, 0f, 1f));

    private static void Eyes(ImDrawListPtr drawList, Vector2 center, float spread, float radius, float alpha)
    {
        var ink = Color(Ink, alpha);
        var glint = Color(Glint, alpha);
        for (var side = -1; side <= 1; side += 2)
        {
            var eye = center + new Vector2(side * spread, 0f);
            Oval(drawList, eye, radius, radius * 1.25f, ink);
            drawList.AddCircleFilled(eye - new Vector2(radius * 0.3f, radius * 0.45f), radius * 0.38f, glint, 6);
        }
    }

    private static void Legs(ImDrawListPtr drawList, Vector2 feet, float size, float gait, bool walking, uint color)
    {
        var step = walking ? MathF.Sin(gait) : 0f;
        Oval(drawList, feet + new Vector2(-size * 0.1f, -size * (0.04f + 0.04f * MathF.Max(0f, step))), size * 0.065f,
            size * 0.05f, color);
        Oval(drawList, feet + new Vector2(size * 0.1f, -size * (0.04f + 0.04f * MathF.Max(0f, -step))), size * 0.065f,
            size * 0.05f, color);
    }

    private static void LeafCrown(ImDrawListPtr drawList, Vector2 top, float size, float sway, bool swept, float alpha)
    {
        var leaf = Color(Leaf, alpha);
        var dark = Color(LeafDark, alpha);
        for (var index = -1; index <= 1; index++)
        {
            var angle = index * (swept ? 0.32f : 0.55f) + sway;
            var direction = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
            var length = size * (index == 0 ? 0.17f : 0.14f);
            Ellipse(drawList, top + direction * length, length, size * 0.06f, angle - MathF.PI * 0.5f, index == 0 ? leaf : dark);
        }
    }

    private static void PotHelmet(ImDrawListPtr drawList, Vector2 top, float size, float alpha)
    {
        var rimY = top.Y + size * 0.12f;
        var crownY = top.Y - size * 0.2f;
        var leaf = Color(Leaf, alpha);
        Ellipse(drawList, new Vector2(top.X - size * 0.05f, crownY - size * 0.06f), size * 0.08f, size * 0.035f, -1f, leaf);
        Ellipse(drawList, new Vector2(top.X + size * 0.06f, crownY - size * 0.07f), size * 0.08f, size * 0.035f, -2.2f, leaf);
        drawList.AddQuadFilled(new Vector2(top.X - size * 0.2f, crownY), new Vector2(top.X + size * 0.2f, crownY),
            new Vector2(top.X + size * 0.27f, rimY), new Vector2(top.X - size * 0.27f, rimY), Color(Pot, alpha));
        drawList.AddRectFilled(new Vector2(top.X - size * 0.3f, rimY - size * 0.02f), new Vector2(top.X + size * 0.3f, rimY + size * 0.07f),
            Color(PotRim, alpha), size * 0.03f);
        drawList.AddLine(new Vector2(top.X - size * 0.12f, crownY + size * 0.05f), new Vector2(top.X - size * 0.15f, rimY - size * 0.04f),
            Color(Highlight, alpha), MathF.Max(1f, size * 0.03f));
    }

    private static void BossCrown(ImDrawListPtr drawList, Vector2 top, float size, float alpha)
    {
        var crown = Color(Crown, alpha);
        var baseY = top.Y + size * 0.02f;
        var width = size * 0.22f;
        drawList.AddRectFilled(new Vector2(top.X - width, baseY - size * 0.05f), new Vector2(top.X + width, baseY + size * 0.02f), crown,
            size * 0.01f);
        for (var point = 0; point < 3; point++)
        {
            var x = top.X - width + point * width;
            drawList.AddTriangleFilled(new Vector2(x - size * 0.07f + (point == 0 ? size * 0.07f : 0f), baseY - size * 0.04f),
                new Vector2(x + size * 0.07f - (point == 2 ? size * 0.07f : 0f), baseY - size * 0.04f),
                new Vector2(x + (point == 0 ? size * 0.03f : point == 2 ? -size * 0.03f : 0f), baseY - size * 0.16f), crown);
        }

        drawList.AddCircleFilled(new Vector2(top.X, baseY - size * 0.015f), size * 0.025f, Color(Cap, alpha), 8);
    }

    private static void Wings(ImDrawListPtr drawList, Vector2 body, float size, float time, float alpha)
    {
        var flap = MathF.Sin(time * 16f);
        var wing = Color(Leaf with { W = 0.9f }, alpha);
        var lift = 0.6f + 0.45f * flap;
        for (var side = -1; side <= 1; side += 2)
        {
            var angle = side > 0 ? -lift : MathF.PI + lift;
            var root = body + new Vector2(side * size * 0.18f, -size * 0.1f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            Ellipse(drawList, root + direction * size * 0.16f, size * 0.19f, size * 0.07f, angle, wing);
        }
    }

    private static void FrostSparkles(ImDrawListPtr drawList, Vector2 body, float radius, float time, float alpha)
    {
        for (var index = 0; index < 3; index++)
        {
            var angle = index * MathF.Tau / 3f + time * 1.5f;
            var position = body + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 1.15f;
            Sparkle(drawList, position, radius * 0.16f, FrostCore, alpha * (0.6f + 0.4f * MathF.Sin(time * 6f + index)));
        }
    }

    private static Vector2[] BuildCircle(int segments)
    {
        var points = new Vector2[segments];
        for (var index = 0; index < segments; index++)
        {
            var angle = index * MathF.Tau / segments;
            points[index] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        return points;
    }
}
