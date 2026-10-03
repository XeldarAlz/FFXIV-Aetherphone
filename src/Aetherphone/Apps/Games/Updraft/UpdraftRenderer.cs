using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Updraft;

internal sealed class UpdraftRenderer
{
    public static readonly Vector4 BirdBody = new(1f, 0.80f, 0.34f, 1f);
    private static readonly Vector4 BirdBelly = new(1f, 0.94f, 0.72f, 1f);
    private static readonly Vector4 BirdWing = new(0.97f, 0.62f, 0.24f, 1f);
    private static readonly Vector4 BirdBeak = new(1f, 0.50f, 0.22f, 1f);
    private static readonly Vector4 BirdOutline = new(0.30f, 0.16f, 0.10f, 0.38f);
    private static readonly Vector4 BirdEye = new(0.12f, 0.10f, 0.16f, 1f);
    private static readonly Vector4 BirdCheek = new(1f, 0.52f, 0.56f, 0.6f);
    private static readonly Vector4 ZapTint = new(0.78f, 0.84f, 1f, 1f);
    private static readonly Vector4 FarMountain = new(0.36f, 0.40f, 0.62f, 1f);
    private static readonly Vector4 NearHill = new(0.36f, 0.62f, 0.50f, 1f);
    private static readonly Vector4[] AuroraColors =
    {
        new(0.36f, 1f, 0.72f, 1f), new(0.32f, 0.84f, 1f, 1f), new(0.76f, 0.48f, 1f, 1f),
    };

    private static readonly Vector3[] Mountains =
    {
        new(0.02f, 4.2f, 2.6f), new(0.28f, 5.8f, 3.0f), new(0.52f, 3.8f, 2.4f), new(0.76f, 6.4f, 3.2f),
        new(1.02f, 4.6f, 2.8f),
    };

    private static readonly Vector3[] StarField = BuildStars();
    private const int StarCount = 72;
    private const int AuroraColumns = 32;
    private const int WindLines = 9;
    private const int HillBumps = 9;
    private const int BankPuffs = 7;
    private const float BankPeriod = 20f;
    private const float MountainParallax = 0.16f;
    private const float HillParallax = 0.38f;
    private const float BankParallax = 0.55f;
    private const float StarParallax = 0.08f;
    private const float FlapSeconds = 0.42f;
    private const float ChargeDistance = 4f;
    private Spring tilt = new(0f);
    private Spring facing = new(1f);
    private Spring wing = new(0f);
    private float landSquash;
    private float flapTime = FlapSeconds;
    private float zapTime;

    public static Vector2 InterpolatedBird(UpdraftBoard board)
    {
        var alpha = board.Alpha;
        var x = UpdraftBoard.Wrap(board.PreviousBirdX + UpdraftBoard.WrapDelta(board.BirdX - board.PreviousBirdX) * alpha);
        var y = board.PreviousBirdY + (board.BirdY - board.PreviousBirdY) * alpha;
        return new Vector2(x, y);
    }

    public static float InterpolatedCamera(UpdraftBoard board) =>
        board.PreviousCamera + (board.CameraBottom - board.PreviousCamera) * board.Alpha;

    public void Reset()
    {
        tilt.SnapTo(0f);
        facing.SnapTo(1f);
        wing.SnapTo(0f);
        landSquash = 0f;
        flapTime = FlapSeconds;
        zapTime = 0f;
    }

    public void OnBounce(UpdraftCloudKind kind)
    {
        landSquash = kind == UpdraftCloudKind.Spring ? 1.5f : 1f;
        flapTime = 0f;
    }

    public void OnZap()
    {
        zapTime = 1f;
    }

    public void Animate(UpdraftBoard board, float deltaSeconds)
    {
        landSquash = MathF.Max(0f, landSquash - deltaSeconds * 5f);
        flapTime = MathF.Min(FlapSeconds, flapTime + deltaSeconds);
        zapTime = MathF.Max(0f, zapTime - deltaSeconds * 1.6f);
        tilt.Step(board.VelocityX / UpdraftBoard.MaxSpeed * 0.34f, 0.08f, deltaSeconds);
        if (MathF.Abs(board.VelocityX) > 0.6f)
        {
            facing.Step(MathF.Sign(board.VelocityX), 0.07f, deltaSeconds);
        }

        wing.Step(WingTarget(board), 0.025f, deltaSeconds);
    }

    private float WingTarget(UpdraftBoard board)
    {
        if (board.Gliding)
        {
            return -0.95f + MathF.Sin((float)ImGui.GetTime() * 18f) * 0.28f;
        }

        if (flapTime < FlapSeconds)
        {
            var progress = flapTime / FlapSeconds;
            return -1.25f * MathF.Sin(progress * MathF.PI * 3f) * (1f - progress);
        }

        return board.VelocityY < 0f ? -0.55f : 0.28f;
    }

    public void Draw(ImDrawListPtr drawList, UpdraftBoard board, in UpdraftView view, in UpdraftSkyState sky,
        Vector4 accent, float time, float scale)
    {
        var body = view.Body;
        drawList.PushClipRect(body.Min, body.Max, true);
        DrawSky(drawList, body, sky);
        DrawAurora(drawList, body, sky, time);
        DrawStars(drawList, view, sky, time, scale);
        DrawSun(drawList, body, sky);
        DrawBanks(drawList, view, sky);
        DrawMountains(drawList, view, sky);
        DrawHills(drawList, view, sky);
        DrawWinds(drawList, board, view, time, scale);
        DrawBestLine(drawList, board, view, accent, time, scale);
        var bird = InterpolatedBird(board);
        DrawClouds(drawList, board, view, sky, bird, time);
        DrawPickups(drawList, board, view, time);
        DrawBirds(drawList, board, view, bird, time);
        drawList.PopClipRect();
    }

    private static void DrawSky(ImDrawListPtr drawList, Rect body, in UpdraftSkyState sky)
    {
        var top = UpdraftArt.Color(sky.Top);
        var middle = UpdraftArt.Color(sky.Middle);
        var bottom = UpdraftArt.Color(sky.Bottom);
        var split = body.Min.Y + body.Height * 0.55f;
        drawList.AddRectFilledMultiColor(body.Min, new Vector2(body.Max.X, split), top, top, middle, middle);
        drawList.AddRectFilledMultiColor(new Vector2(body.Min.X, split), body.Max, middle, middle, bottom, bottom);
    }

    private static void DrawAurora(ImDrawListPtr drawList, Rect body, in UpdraftSkyState sky, float time)
    {
        if (sky.Aurora < 0.01f)
        {
            return;
        }

        var columnWidth = body.Width / AuroraColumns;
        for (var ribbon = 0; ribbon < AuroraColors.Length; ribbon++)
        {
            var color = AuroraColors[ribbon];
            var baseY = body.Min.Y + body.Height * (0.2f + ribbon * 0.1f);
            for (var column = 0; column < AuroraColumns; column++)
            {
                var x = body.Min.X + column * columnWidth;
                var wave = MathF.Sin(column * 0.35f + time * (0.6f + ribbon * 0.2f) + ribbon * 1.7f) * body.Height * 0.05f +
                    MathF.Sin(column * 0.13f - time * 0.3f) * body.Height * 0.03f;
                var y = baseY + wave;
                var height = body.Height * (0.12f + 0.05f * MathF.Sin(column * 0.5f + time + ribbon));
                var alpha = sky.Aurora * 0.26f * (0.7f + 0.3f * MathF.Sin(time * 0.8f + column * 0.2f + ribbon));
                var clear = UpdraftArt.Color(color with { W = 0f });
                var lit = UpdraftArt.Color(color with { W = alpha });
                drawList.AddRectFilledMultiColor(new Vector2(x, y - height), new Vector2(x + columnWidth + 1f, y), clear,
                    clear, lit, lit);
                drawList.AddRectFilled(new Vector2(x, y - 1.5f), new Vector2(x + columnWidth + 1f, y + 1f),
                    UpdraftArt.Color(GamePalette.Lighten(color, 0.4f) with { W = alpha * 1.4f }));
            }
        }
    }

    private static void DrawStars(ImDrawListPtr drawList, in UpdraftView view, in UpdraftSkyState sky, float time,
        float scale)
    {
        if (sky.Stars < 0.01f)
        {
            return;
        }

        var body = view.Body;
        var tile = body.Height;
        var offset = (view.Camera - UpdraftBoard.StartCamera) * view.Unit * StarParallax % tile;
        for (var index = 0; index < StarField.Length; index++)
        {
            var star = StarField[index];
            var y = body.Min.Y + (star.Y * tile + offset) % tile;
            var x = body.Min.X + star.X * body.Width;
            var twinkle = 0.55f + 0.45f * MathF.Sin(time * (1.5f + star.Z) + star.X * 40f);
            var alpha = sky.Stars * twinkle;
            var radius = (0.6f + star.Z * 1.2f) * scale;
            drawList.AddCircleFilled(new Vector2(x, y), radius, UpdraftArt.Color(new Vector4(1f, 1f, 0.96f, alpha)));
            if (star.Z > 0.85f)
            {
                UpdraftArt.Twinkle(drawList, new Vector2(x, y), radius * 3f, UpdraftArt.Color(new Vector4(1f, 1f, 1f, alpha * 0.5f)));
            }
        }
    }

    private static void DrawSun(ImDrawListPtr drawList, Rect body, in UpdraftSkyState sky)
    {
        var center = new Vector2(body.Min.X + body.Width * 0.76f, body.Min.Y + body.Height * sky.SunHeight);
        var radius = body.Width * 0.075f;
        ProgressRing.Glow(center, radius * 2.8f, sky.Sun, 0.75f);
        drawList.AddCircleFilled(center, radius, UpdraftArt.Color(sky.Sun), 36);
        if (sky.Moon < 0.01f)
        {
            return;
        }

        var shadowSky = Vector4.Lerp(sky.Top, sky.Middle, Math.Clamp(sky.SunHeight / 0.55f, 0f, 1f));
        drawList.AddCircleFilled(center + new Vector2(radius * 0.42f, -radius * 0.22f), radius * 0.9f,
            UpdraftArt.Color(shadowSky with { W = sky.Moon }), 36);
    }

    private static float LayerScreenY(in UpdraftView view, float layerY, float parallax)
    {
        var layerCamera = UpdraftBoard.StartCamera + (view.Camera - UpdraftBoard.StartCamera) * parallax;
        return view.Bottom - (layerY - layerCamera) * view.Unit;
    }

    private static void DrawBanks(ImDrawListPtr drawList, in UpdraftView view, in UpdraftSkyState sky)
    {
        var body = view.Body;
        var unit = view.Unit;
        var alpha = 0.24f * (1f - 0.75f * sky.Stars);
        if (alpha < 0.01f)
        {
            return;
        }

        var layerCamera = UpdraftBoard.StartCamera + (view.Camera - UpdraftBoard.StartCamera) * BankParallax;
        var span = body.Height / unit;
        var first = (int)MathF.Floor((layerCamera - 4f) / BankPeriod);
        var last = (int)MathF.Ceiling((layerCamera + span + 4f) / BankPeriod);
        var color = UpdraftArt.Color(UpdraftArt.Lit(new Vector4(1f, 1f, 1f, alpha), sky.Light));
        var soft = UpdraftArt.Color(UpdraftArt.Lit(new Vector4(1f, 1f, 1f, alpha * 0.6f), sky.Light));
        for (var bank = Math.Max(0, first); bank <= last; bank++)
        {
            var y = LayerScreenY(view, bank * BankPeriod + 9f, BankParallax);
            for (var puff = 0; puff < BankPuffs; puff++)
            {
                var seed = bank * 13.1f + puff * 3.7f;
                var x = body.Min.X + (puff + UpdraftArt.Hash(seed) * 0.6f - 0.3f) / (BankPuffs - 1) * body.Width;
                var radius = (1.3f + UpdraftArt.Hash(seed + 1f) * 1.1f) * unit;
                var lift = UpdraftArt.Hash(seed + 2f) * 0.7f * unit;
                drawList.AddCircleFilled(new Vector2(x, y - lift), radius, color);
                drawList.AddCircleFilled(new Vector2(x + radius * 0.5f, y - lift - radius * 0.5f), radius * 0.6f, soft);
            }
        }
    }

    private static void DrawMountains(ImDrawListPtr drawList, in UpdraftView view, in UpdraftSkyState sky)
    {
        var body = view.Body;
        var unit = view.Unit;
        var baseY = LayerScreenY(view, UpdraftBoard.StartCamera, MountainParallax);
        var tallest = 6.4f * unit;
        if (baseY - tallest > body.Max.Y)
        {
            return;
        }

        var fill = UpdraftArt.Color(UpdraftArt.Lit(Vector4.Lerp(sky.Bottom, FarMountain, 0.55f), sky.Light));
        var snow = UpdraftArt.Color(UpdraftArt.Lit(new Vector4(1f, 1f, 1f, 0.85f), sky.Light));
        for (var index = 0; index < Mountains.Length; index++)
        {
            var mountain = Mountains[index];
            var peak = new Vector2(body.Min.X + mountain.X * body.Width, baseY - mountain.Y * unit);
            var left = new Vector2(peak.X - mountain.Z * unit, baseY);
            var right = new Vector2(peak.X + mountain.Z * unit, baseY);
            drawList.AddTriangleFilled(peak, right, left, fill);
            var capDepth = 0.22f;
            var capLeft = Vector2.Lerp(peak, left, capDepth);
            var capRight = Vector2.Lerp(peak, right, capDepth);
            var notch = Vector2.Lerp(peak, (left + right) * 0.5f, capDepth * 1.35f);
            drawList.AddTriangleFilled(peak, capRight, notch, snow);
            drawList.AddTriangleFilled(peak, notch, capLeft, snow);
        }

        if (baseY < body.Max.Y)
        {
            drawList.AddRectFilled(new Vector2(body.Min.X, baseY), body.Max, fill);
        }
    }

    private static void DrawHills(ImDrawListPtr drawList, in UpdraftView view, in UpdraftSkyState sky)
    {
        var body = view.Body;
        var unit = view.Unit;
        var baseY = LayerScreenY(view, UpdraftBoard.StartCamera + 0.4f, HillParallax);
        if (baseY - 2.5f * unit > body.Max.Y)
        {
            return;
        }

        var color = UpdraftArt.Lit(Vector4.Lerp(sky.Bottom, NearHill, 0.7f), sky.Light);
        var fill = UpdraftArt.Color(color);
        var crest = UpdraftArt.Color(GamePalette.Lighten(color, 0.12f));
        var spacing = body.Width / (HillBumps - 1);
        for (var bump = 0; bump < HillBumps; bump++)
        {
            var radius = (1.4f + UpdraftArt.Hash(bump * 2.3f) * 1.1f) * unit;
            var center = new Vector2(body.Min.X + bump * spacing, baseY + radius * 0.45f);
            drawList.AddCircleFilled(center, radius, fill, 32);
            drawList.AddCircleFilled(center + new Vector2(-radius * 0.2f, -radius * 0.08f), radius * 0.82f, crest, 28);
            drawList.AddCircleFilled(center + new Vector2(radius * 0.05f, radius * 0.1f), radius * 0.86f, fill, 28);
        }

        drawList.AddRectFilled(new Vector2(body.Min.X, baseY + 0.4f * unit), body.Max, fill);
    }

    private static void DrawWinds(ImDrawListPtr drawList, UpdraftBoard board, in UpdraftView view, float time,
        float scale)
    {
        var body = view.Body;
        var winds = board.Winds;
        for (var index = 0; index < winds.Length; index++)
        {
            ref readonly var wind = ref winds[index];
            if (!wind.Active)
            {
                continue;
            }

            var topY = view.ScreenY(wind.Top);
            var bottomY = view.ScreenY(wind.Bottom);
            if (bottomY < body.Min.Y || topY > body.Max.Y)
            {
                continue;
            }

            var band = UpdraftArt.Color(new Vector4(1f, 1f, 1f, 0.05f));
            var clear = UpdraftArt.Color(new Vector4(1f, 1f, 1f, 0f));
            var middle = (topY + bottomY) * 0.5f;
            drawList.AddRectFilledMultiColor(new Vector2(body.Min.X, topY), new Vector2(body.Max.X, middle), clear, clear,
                band, band);
            drawList.AddRectFilledMultiColor(new Vector2(body.Min.X, middle), new Vector2(body.Max.X, bottomY), band, band,
                clear, clear);
            var direction = MathF.Sign(wind.Speed);
            var travel = body.Width + 3f * view.Unit;
            for (var line = 0; line < WindLines; line++)
            {
                var fraction = (line + 0.5f) / WindLines;
                var seed = UpdraftArt.Hash(index * 7.3f + line * 1.9f);
                var length = (0.8f + seed) * view.Unit;
                var distance = (time * MathF.Abs(wind.Speed) * 1.6f * view.Unit + seed * travel * 3f) % travel;
                var head = direction > 0f ? body.Min.X - 1.5f * view.Unit + distance : body.Max.X + 1.5f * view.Unit - distance;
                var y = bottomY + (topY - bottomY) * fraction + MathF.Sin(time * 2f + line) * 2f * scale;
                var alpha = 0.42f * MathF.Sin(fraction * MathF.PI);
                var color = UpdraftArt.Color(new Vector4(1f, 1f, 1f, alpha));
                var tail = new Vector2(head - direction * length, y);
                drawList.AddLine(tail, new Vector2(head, y), color, MathF.Max(1f, 1.6f * scale));
                var chevron = 3.5f * scale;
                drawList.AddLine(new Vector2(head, y), new Vector2(head - direction * chevron, y - chevron), color,
                    MathF.Max(1f, 1.4f * scale));
                drawList.AddLine(new Vector2(head, y), new Vector2(head - direction * chevron, y + chevron), color,
                    MathF.Max(1f, 1.4f * scale));
            }
        }
    }

    private static void DrawBestLine(ImDrawListPtr drawList, UpdraftBoard board, in UpdraftView view, Vector4 accent,
        float time, float scale)
    {
        if (board.BestHeight <= 0f)
        {
            return;
        }

        var body = view.Body;
        var y = view.ScreenY(board.BestHeight);
        if (y < body.Min.Y - 30f * scale || y > body.Max.Y + 4f * scale)
        {
            return;
        }

        var tone = board.PassedBest ? UpdraftArt.GoldColor : new Vector4(1f, 1f, 1f, 1f);
        var line = UpdraftArt.Color(tone with { W = 0.62f });
        var dash = 9f * scale;
        var gap = 6f * scale;
        var thickness = MathF.Max(1f, 1.6f * scale);
        for (var x = body.Min.X; x < body.Max.X; x += dash + gap)
        {
            drawList.AddLine(new Vector2(x, y), new Vector2(MathF.Min(body.Max.X, x + dash), y), line, thickness);
        }

        var poleX = body.Max.X - 24f * scale;
        var poleTop = y - 24f * scale;
        drawList.AddLine(new Vector2(poleX, y), new Vector2(poleX, poleTop), UpdraftArt.Color(new Vector4(1f, 1f, 1f, 0.9f)),
            MathF.Max(1f, 2f * scale));
        var wave = MathF.Sin(time * 6f) * 2.5f * scale;
        var flag = board.PassedBest ? UpdraftArt.GoldColor : accent;
        drawList.AddTriangleFilled(new Vector2(poleX, poleTop), new Vector2(poleX + 15f * scale, poleTop + 5f * scale + wave),
            new Vector2(poleX, poleTop + 11f * scale), UpdraftArt.Color(flag));
        var label = Loc.T(L.Games.Best);
        var labelSize = Typography.Measure(label, TextStyles.Caption2);
        Typography.DrawCentered(drawList, new Vector2(poleX - labelSize.X * 0.5f - 6f * scale, y - labelSize.Y * 0.6f - 2f * scale),
            label, tone with { W = 0.9f }, TextStyles.Caption2.Scale, TextStyles.Caption2.Weight);
    }

    private static void DrawClouds(ImDrawListPtr drawList, UpdraftBoard board, in UpdraftView view,
        in UpdraftSkyState sky, Vector2 bird, float time)
    {
        var body = view.Body;
        var clouds = board.Clouds;
        for (var index = 0; index < clouds.Length; index++)
        {
            ref readonly var cloud = ref clouds[index];
            if (!cloud.Active)
            {
                continue;
            }

            var halfWidth = cloud.HalfWidth * view.Unit;
            var y = view.ScreenY(cloud.Y);
            if (y < body.Min.Y - halfWidth * 2f || y > body.Max.Y + halfWidth)
            {
                continue;
            }

            var charge = 0f;
            if (cloud.Kind == UpdraftCloudKind.Storm)
            {
                var horizontal = MathF.Max(0f, MathF.Abs(UpdraftBoard.WrapDelta(bird.X - cloud.X)) - cloud.HalfWidth);
                var vertical = bird.Y - cloud.Y;
                var distance = MathF.Sqrt(horizontal * horizontal + vertical * vertical);
                charge = Math.Clamp(1f - distance / ChargeDistance, 0f, 1f);
            }

            var first = view.FirstCopy(cloud.X, halfWidth * 1.6f);
            var last = view.LastCopy(cloud.X, halfWidth * 1.6f);
            for (var copy = first; copy <= last; copy++)
            {
                var x = view.Left + cloud.X * view.Unit + copy * view.FieldPixels;
                UpdraftArt.DrawCloud(drawList, new Vector2(x, y), halfWidth, cloud, sky.Light, time, charge);
            }
        }
    }

    private static void DrawPickups(ImDrawListPtr drawList, UpdraftBoard board, in UpdraftView view, float time)
    {
        var body = view.Body;
        var pickups = board.Pickups;
        var size = 0.24f * view.Unit;
        for (var index = 0; index < pickups.Length; index++)
        {
            ref readonly var pickup = ref pickups[index];
            if (!pickup.Active)
            {
                continue;
            }

            var bob = MathF.Sin(time * 2.4f + pickup.Phase) * 0.08f * view.Unit;
            var y = view.ScreenY(pickup.Y) + bob;
            if (y < body.Min.Y - size * 3f || y > body.Max.Y + size * 3f)
            {
                continue;
            }

            var first = view.FirstCopy(pickup.X, size * 3f);
            var last = view.LastCopy(pickup.X, size * 3f);
            for (var copy = first; copy <= last; copy++)
            {
                var center = new Vector2(view.Left + pickup.X * view.Unit + copy * view.FieldPixels, y);
                switch (pickup.Kind)
                {
                    case UpdraftPickupKind.Crystal:
                        UpdraftArt.DrawCrystal(drawList, center, size, time * 2.6f + pickup.Phase);
                        break;
                    case UpdraftPickupKind.Feather:
                        UpdraftArt.DrawFeather(drawList, center, size * 1.25f, MathF.Sin(time * 2f + pickup.Phase) * 0.3f);
                        break;
                    default:
                        UpdraftArt.DrawBubble(drawList, center, size * 1.6f, time + pickup.Phase, 1f);
                        break;
                }
            }
        }
    }

    private void DrawBirds(ImDrawListPtr drawList, UpdraftBoard board, in UpdraftView view, Vector2 bird, float time)
    {
        var y = bird.Y;
        if (!board.Launched)
        {
            y += 0.18f + MathF.Sin(time * 3f) * 0.1f;
        }

        var radius = UpdraftBoard.BirdRadius * view.Unit * 1.15f;
        var screenY = view.ScreenY(y);
        var first = view.FirstCopy(bird.X, radius * 2.5f);
        var last = view.LastCopy(bird.X, radius * 2.5f);
        for (var copy = first; copy <= last; copy++)
        {
            var center = new Vector2(view.Left + bird.X * view.Unit + copy * view.FieldPixels, screenY);
            DrawBird(drawList, board, center, radius, time);
        }
    }

    private void DrawBird(ImDrawListPtr drawList, UpdraftBoard board, Vector2 center, float radius, float time)
    {
        if (board.Gliding)
        {
            ProgressRing.Glow(center, radius * 2.4f, UpdraftArt.GoldColor, 0.4f + 0.2f * MathF.Sin(time * 8f));
        }

        var rising = Math.Clamp(board.VelocityY / UpdraftBoard.PlainBounce, 0f, 2f);
        var squash = MathF.Min(0.45f, landSquash * landSquash * 0.28f);
        var stretch = rising * 0.12f;
        var scaleX = 1f + squash - stretch * 0.6f;
        var scaleY = 1f - squash + stretch;
        var anchored = new Vector2(center.X, center.Y + radius * (1f - scaleY));
        var side = facing.Value >= 0f ? 1f : -1f;
        var turn = MathF.Max(0.3f, MathF.Abs(facing.Value));
        var frame = ShapeFrame.Rotated(anchored, radius * scaleX * side * turn, radius * scaleY, tilt.Value);
        var zap = zapTime > 0f && MathF.Sin(time * 40f) > 0f ? zapTime : 0f;
        var bodyColor = UpdraftArt.Color(Vector4.Lerp(BirdBody, ZapTint, zap));
        var wingColor = UpdraftArt.Color(Vector4.Lerp(BirdWing, ZapTint, zap));
        var darkWing = UpdraftArt.Color(GamePalette.Darken(Vector4.Lerp(BirdWing, ZapTint, zap), 0.12f));
        var wingAngle = wing.Value;
        UpdraftArt.FillEllipse(drawList, frame, new Vector2(-1.02f, -0.12f), new Vector2(0.34f, 0.16f), -0.5f, darkWing);
        if (board.Gliding)
        {
            var farPivot = new Vector2(0.05f, -0.05f);
            UpdraftArt.FillEllipse(drawList, frame, farPivot + Rotate(new Vector2(0.3f, 0f), -wingAngle - MathF.PI * 0.55f),
                new Vector2(0.55f, 0.24f), -wingAngle - MathF.PI * 0.55f, darkWing);
        }

        UpdraftArt.FillEllipse(drawList, frame, Vector2.Zero, new Vector2(1.07f, 1.07f), 0f, UpdraftArt.Color(BirdOutline));
        UpdraftArt.FillEllipse(drawList, frame, Vector2.Zero, Vector2.One, 0f, bodyColor);
        UpdraftArt.FillEllipse(drawList, frame, new Vector2(0.2f, 0.34f), new Vector2(0.62f, 0.5f), 0f, UpdraftArt.Color(BirdBelly));
        UpdraftArt.FillEllipse(drawList, frame, new Vector2(-0.22f, -0.55f), new Vector2(0.42f, 0.2f), -0.3f,
            UpdraftArt.Color(new Vector4(1f, 1f, 1f, 0.35f)));
        UpdraftArt.FillEllipse(drawList, frame, new Vector2(-0.02f, -1.0f), new Vector2(0.12f, 0.22f), -0.4f, wingColor);
        UpdraftArt.FillEllipse(drawList, frame, new Vector2(0.16f, -0.98f), new Vector2(0.09f, 0.18f), 0.3f, wingColor);
        DrawFace(drawList, board, frame, side);
        var pivot = new Vector2(-0.18f, 0.12f);
        UpdraftArt.FillEllipse(drawList, frame, pivot + Rotate(new Vector2(-0.34f, 0f), wingAngle),
            new Vector2(0.54f, 0.28f), wingAngle, wingColor);
        if (board.HasShield)
        {
            UpdraftArt.DrawBubble(drawList, center, radius * 1.75f, time, 1f);
        }

        if (zapTime <= 0f)
        {
            return;
        }

        for (var spark = 0; spark < 3; spark++)
        {
            var angle = time * 7f + spark * MathF.Tau / 3f;
            var sparkCenter = center + new Vector2(MathF.Cos(angle) * radius * 1.4f, -radius * 1.3f + MathF.Sin(angle) * radius * 0.35f);
            UpdraftArt.Twinkle(drawList, sparkCenter, radius * 0.22f, UpdraftArt.Color(UpdraftArt.BoltColor with { W = zapTime }));
        }
    }

    private static void DrawFace(ImDrawListPtr drawList, UpdraftBoard board, in ShapeFrame frame, float side)
    {
        var gaze = new Vector2(board.VelocityX * side, -board.VelocityY);
        var length = MathF.Max(6f, gaze.Length());
        gaze /= length;
        UpdraftArt.FillEllipse(drawList, frame, new Vector2(0.55f, 0.16f), new Vector2(0.13f, 0.1f), 0f,
            UpdraftArt.Color(BirdCheek));
        UpdraftArt.FillEllipse(drawList, frame, new Vector2(0.38f, -0.24f), new Vector2(0.27f, 0.3f), 0f,
            UpdraftArt.Color(new Vector4(1f, 1f, 1f, 1f)));
        var pupil = new Vector2(0.42f + gaze.X * 0.1f, -0.22f + gaze.Y * 0.12f);
        UpdraftArt.FillEllipse(drawList, frame, pupil, new Vector2(0.14f, 0.16f), 0f, UpdraftArt.Color(BirdEye));
        UpdraftArt.FillEllipse(drawList, frame, pupil + new Vector2(-0.05f, -0.07f), new Vector2(0.05f, 0.05f), 0f,
            UpdraftArt.Color(new Vector4(1f, 1f, 1f, 1f)));
        var beak = UpdraftArt.Color(BirdBeak);
        UpdraftArt.FillTriangle(drawList, frame, new Vector2(0.86f, -0.08f), new Vector2(1.3f, 0.06f), new Vector2(0.86f, 0.2f),
            beak);
    }

    private static Vector2 Rotate(Vector2 value, float angle)
    {
        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        return new Vector2(value.X * cosine - value.Y * sine, value.X * sine + value.Y * cosine);
    }

    private static Vector3[] BuildStars()
    {
        var random = new UpdraftRandom(7331);
        var stars = new Vector3[StarCount];
        for (var index = 0; index < StarCount; index++)
        {
            stars[index] = new Vector3(random.NextFloat(), random.NextFloat(), random.NextFloat());
        }

        return stars;
    }
}
