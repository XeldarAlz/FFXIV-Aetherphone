using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Updraft;

internal readonly struct UpdraftTone
{
    public readonly Vector4 Light;
    public readonly float Aurora;
    public readonly float Night;

    public UpdraftTone(Vector4 light, float aurora, float night)
    {
        Light = light;
        Aurora = aurora;
        Night = night;
    }
}

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
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4[] AuroraColors =
    {
        new(0.36f, 1f, 0.72f, 1f), new(0.32f, 0.84f, 1f, 1f), new(0.76f, 0.48f, 1f, 1f),
    };

    private static readonly Vector3[] Mountains =
    {
        new(0.02f, 4.2f, 2.6f), new(0.28f, 5.8f, 3.0f), new(0.52f, 3.8f, 2.4f), new(0.76f, 6.4f, 3.2f),
        new(1.02f, 4.6f, 2.8f),
    };

    private static readonly Vector4[] Lights =
    {
        new(1f, 0.93f, 0.90f, 1f), new(1f, 1f, 1f, 1f), new(1f, 0.84f, 0.74f, 1f), new(0.60f, 0.66f, 0.88f, 1f),
        new(0.60f, 0.78f, 0.86f, 1f),
    };

    private static readonly float[] AuroraAmounts = { 0f, 0f, 0f, 0.12f, 1f };
    private static readonly float[] NightAmounts = { 0f, 0f, 0.12f, 0.85f, 1f };
    private const int SkyBands = 5;
    private const int AuroraColumns = 32;
    private const int WindLines = 9;
    private const int HillBumps = 9;
    private const int BankPuffs = 7;
    private const float BankPeriod = 20f;
    private const float MountainParallax = 0.16f;
    private const float HillParallax = 0.38f;
    private const float BankParallax = 0.55f;
    private const float FlapSeconds = 0.42f;
    private const float ChargeDistance = 4f;
    private const float PickupSize = 0.24f;
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

    public static UpdraftTone ToneAt(float progress)
    {
        var position = Math.Clamp(progress, 0f, 1f) * (SkyBands - 1);
        var band = Math.Min(SkyBands - 2, (int)position);
        var blend = Easing.SmoothStep(position - band);
        return new UpdraftTone(Vector4.Lerp(Lights[band], Lights[band + 1], blend),
            Easing.Lerp(AuroraAmounts[band], AuroraAmounts[band + 1], blend),
            Easing.Lerp(NightAmounts[band], NightAmounts[band + 1], blend));
    }

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

    public void Animate(UpdraftBoard board, float deltaSeconds, float time)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        landSquash = MathF.Max(0f, landSquash - deltaSeconds * 5f);
        flapTime = MathF.Min(FlapSeconds, flapTime + deltaSeconds);
        zapTime = MathF.Max(0f, zapTime - deltaSeconds * 1.6f);
        tilt.Step(board.VelocityX / UpdraftBoard.MaxSpeed * 0.34f, 0.08f, deltaSeconds);
        if (MathF.Abs(board.VelocityX) > 0.6f)
        {
            facing.Step(MathF.Sign(board.VelocityX), 0.07f, deltaSeconds);
        }

        wing.Step(WingTarget(board, time), 0.025f, deltaSeconds);
    }

    private float WingTarget(UpdraftBoard board, float time)
    {
        if (board.Gliding)
        {
            return -0.95f + MathF.Sin(time * 18f) * 0.28f;
        }

        if (flapTime < FlapSeconds)
        {
            var progress = flapTime / FlapSeconds;
            return -1.25f * MathF.Sin(progress * MathF.PI * 3f) * (1f - progress);
        }

        return board.VelocityY < 0f ? -0.55f : 0.28f;
    }

    public void Draw(ImDrawListPtr drawList, UpdraftBoard board, in Camera2D camera, float cameraBottom,
        in UpdraftTone tone, Vector4 haze, Vector4 accent, float time, float scale)
    {
        DrawAurora(drawList, camera.View, tone, time);
        DrawBanks(drawList, in camera, cameraBottom, tone);
        DrawMountains(drawList, in camera, cameraBottom, tone, haze);
        DrawHills(drawList, in camera, cameraBottom, tone, haze);
        DrawWinds(drawList, board, in camera, time, scale);
        DrawBestLine(drawList, board, in camera, accent, time, scale);
        var bird = InterpolatedBird(board);
        DrawClouds(drawList, board, in camera, tone, bird, time);
        DrawPickups(drawList, board, in camera, time);
        DrawBirds(drawList, board, in camera, bird, time);
    }

    private static float ScreenY(in Camera2D camera, float worldY) => camera.ToScreen(new Vector2(0f, -worldY)).Y;

    private static float ScreenX(in Camera2D camera, float worldX) => camera.ToScreen(new Vector2(worldX, 0f)).X;

    private static float LayerScreenY(in Camera2D camera, float cameraBottom, float layerY, float parallax) =>
        ScreenY(in camera, layerY + (cameraBottom - UpdraftBoard.StartCamera) * (1f - parallax));

    private static int FirstCopy(in Camera2D camera, float x, float marginUnits) =>
        (int)MathF.Ceiling((camera.VisibleWorld.Min.X - marginUnits - x) / UpdraftBoard.FieldWidth);

    private static int LastCopy(in Camera2D camera, float x, float marginUnits) =>
        (int)MathF.Floor((camera.VisibleWorld.Max.X + marginUnits - x) / UpdraftBoard.FieldWidth);

    private static void DrawAurora(ImDrawListPtr drawList, Rect view, in UpdraftTone tone, float time)
    {
        if (tone.Aurora < 0.01f)
        {
            return;
        }

        var columnWidth = view.Width / AuroraColumns;
        for (var ribbon = 0; ribbon < AuroraColors.Length; ribbon++)
        {
            var color = AuroraColors[ribbon];
            var baseY = view.Min.Y + view.Height * (0.2f + ribbon * 0.1f);
            for (var column = 0; column < AuroraColumns; column++)
            {
                var x = view.Min.X + column * columnWidth;
                var wave = MathF.Sin(column * 0.35f + time * (0.6f + ribbon * 0.2f) + ribbon * 1.7f) * view.Height * 0.05f +
                    MathF.Sin(column * 0.13f - time * 0.3f) * view.Height * 0.03f;
                var y = baseY + wave;
                var height = view.Height * (0.12f + 0.05f * MathF.Sin(column * 0.5f + time + ribbon));
                var alpha = tone.Aurora * 0.26f * (0.7f + 0.3f * MathF.Sin(time * 0.8f + column * 0.2f + ribbon));
                var clear = UpdraftArt.Color(color with { W = 0f });
                var lit = UpdraftArt.Color(color with { W = alpha });
                drawList.AddRectFilledMultiColor(new Vector2(x, y - height), new Vector2(x + columnWidth + 1f, y), clear,
                    clear, lit, lit);
                drawList.AddRectFilled(new Vector2(x, y - 1.5f), new Vector2(x + columnWidth + 1f, y + 1f),
                    UpdraftArt.Color(GamePalette.Lighten(color, 0.4f) with { W = alpha * 1.4f }));
            }
        }
    }

    private static void DrawBanks(ImDrawListPtr drawList, in Camera2D camera, float cameraBottom, in UpdraftTone tone)
    {
        var alpha = 0.24f * (1f - 0.75f * tone.Night);
        if (alpha < 0.01f)
        {
            return;
        }

        var view = camera.View;
        var unit = camera.Px(1f);
        var layerCamera = UpdraftBoard.StartCamera + (cameraBottom - UpdraftBoard.StartCamera) * BankParallax;
        var span = view.Height / unit;
        var first = (int)MathF.Floor((layerCamera - 4f) / BankPeriod);
        var last = (int)MathF.Ceiling((layerCamera + span + 4f) / BankPeriod);
        var color = UpdraftArt.Color(UpdraftArt.Lit(White with { W = alpha }, tone.Light));
        var soft = UpdraftArt.Color(UpdraftArt.Lit(White with { W = alpha * 0.6f }, tone.Light));
        for (var bank = Math.Max(0, first); bank <= last; bank++)
        {
            var y = LayerScreenY(in camera, cameraBottom, bank * BankPeriod + 9f, BankParallax);
            for (var puff = 0; puff < BankPuffs; puff++)
            {
                var seed = bank * 13.1f + puff * 3.7f;
                var x = view.Min.X + (puff + UpdraftArt.Hash(seed) * 0.6f - 0.3f) / (BankPuffs - 1) * view.Width;
                var radius = (1.3f + UpdraftArt.Hash(seed + 1f) * 1.1f) * unit;
                var lift = UpdraftArt.Hash(seed + 2f) * 0.7f * unit;
                drawList.AddCircleFilled(new Vector2(x, y - lift), radius, color);
                drawList.AddCircleFilled(new Vector2(x + radius * 0.5f, y - lift - radius * 0.5f), radius * 0.6f, soft);
            }
        }
    }

    private static void DrawMountains(ImDrawListPtr drawList, in Camera2D camera, float cameraBottom,
        in UpdraftTone tone, Vector4 haze)
    {
        var view = camera.View;
        var unit = camera.Px(1f);
        var baseY = LayerScreenY(in camera, cameraBottom, UpdraftBoard.StartCamera, MountainParallax);
        var tallest = 6.4f * unit;
        if (baseY - tallest > view.Max.Y)
        {
            return;
        }

        var fill = UpdraftArt.Color(UpdraftArt.Lit(Vector4.Lerp(haze, FarMountain, 0.55f), tone.Light));
        var snow = UpdraftArt.Color(UpdraftArt.Lit(White with { W = 0.85f }, tone.Light));
        for (var index = 0; index < Mountains.Length; index++)
        {
            var mountain = Mountains[index];
            var peak = new Vector2(view.Min.X + mountain.X * view.Width, baseY - mountain.Y * unit);
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

        if (baseY < view.Max.Y)
        {
            drawList.AddRectFilled(new Vector2(view.Min.X, baseY), view.Max, fill);
        }
    }

    private static void DrawHills(ImDrawListPtr drawList, in Camera2D camera, float cameraBottom, in UpdraftTone tone,
        Vector4 haze)
    {
        var view = camera.View;
        var unit = camera.Px(1f);
        var baseY = LayerScreenY(in camera, cameraBottom, UpdraftBoard.StartCamera + 0.4f, HillParallax);
        if (baseY - 2.5f * unit > view.Max.Y)
        {
            return;
        }

        var color = UpdraftArt.Lit(Vector4.Lerp(haze, NearHill, 0.7f), tone.Light);
        var fill = UpdraftArt.Color(color);
        var crest = UpdraftArt.Color(GamePalette.Lighten(color, 0.12f));
        var spacing = view.Width / (HillBumps - 1);
        for (var bump = 0; bump < HillBumps; bump++)
        {
            var radius = (1.4f + UpdraftArt.Hash(bump * 2.3f) * 1.1f) * unit;
            var center = new Vector2(view.Min.X + bump * spacing, baseY + radius * 0.45f);
            drawList.AddCircleFilled(center, radius, fill, 32);
            drawList.AddCircleFilled(center + new Vector2(-radius * 0.2f, -radius * 0.08f), radius * 0.82f, crest, 28);
            drawList.AddCircleFilled(center + new Vector2(radius * 0.05f, radius * 0.1f), radius * 0.86f, fill, 28);
        }

        drawList.AddRectFilled(new Vector2(view.Min.X, baseY + 0.4f * unit), view.Max, fill);
    }

    private static void DrawWinds(ImDrawListPtr drawList, UpdraftBoard board, in Camera2D camera, float time,
        float scale)
    {
        var view = camera.View;
        var unit = camera.Px(1f);
        var winds = board.Winds;
        for (var index = 0; index < winds.Length; index++)
        {
            ref readonly var wind = ref winds[index];
            if (!wind.Active)
            {
                continue;
            }

            var topY = ScreenY(in camera, wind.Top);
            var bottomY = ScreenY(in camera, wind.Bottom);
            if (bottomY < view.Min.Y || topY > view.Max.Y)
            {
                continue;
            }

            var band = UpdraftArt.Color(White with { W = 0.05f });
            var clear = UpdraftArt.Color(White with { W = 0f });
            var middle = (topY + bottomY) * 0.5f;
            drawList.AddRectFilledMultiColor(new Vector2(view.Min.X, topY), new Vector2(view.Max.X, middle), clear, clear,
                band, band);
            drawList.AddRectFilledMultiColor(new Vector2(view.Min.X, middle), new Vector2(view.Max.X, bottomY), band, band,
                clear, clear);
            var direction = MathF.Sign(wind.Speed);
            var travel = view.Width + 3f * unit;
            for (var line = 0; line < WindLines; line++)
            {
                var fraction = (line + 0.5f) / WindLines;
                var seed = UpdraftArt.Hash(index * 7.3f + line * 1.9f);
                var length = (0.8f + seed) * unit;
                var distance = (time * MathF.Abs(wind.Speed) * 1.6f * unit + seed * travel * 3f) % travel;
                var head = direction > 0f ? view.Min.X - 1.5f * unit + distance : view.Max.X + 1.5f * unit - distance;
                var y = bottomY + (topY - bottomY) * fraction + MathF.Sin(time * 2f + line) * 2f * scale;
                var alpha = 0.42f * MathF.Sin(fraction * MathF.PI);
                var color = UpdraftArt.Color(White with { W = alpha });
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

    private static void DrawBestLine(ImDrawListPtr drawList, UpdraftBoard board, in Camera2D camera, Vector4 accent,
        float time, float scale)
    {
        if (board.BestHeight <= 0f)
        {
            return;
        }

        var view = camera.View;
        var y = ScreenY(in camera, board.BestHeight);
        if (y < view.Min.Y - 30f * scale || y > view.Max.Y + 4f * scale)
        {
            return;
        }

        var tone = board.PassedBest ? UpdraftArt.GoldColor : White;
        var line = UpdraftArt.Color(tone with { W = 0.62f });
        var dash = 9f * scale;
        var gap = 6f * scale;
        var thickness = MathF.Max(1f, 1.6f * scale);
        for (var x = view.Min.X; x < view.Max.X; x += dash + gap)
        {
            drawList.AddLine(new Vector2(x, y), new Vector2(MathF.Min(view.Max.X, x + dash), y), line, thickness);
        }

        var poleX = view.Max.X - 24f * scale;
        var poleTop = y - 24f * scale;
        drawList.AddLine(new Vector2(poleX, y), new Vector2(poleX, poleTop), UpdraftArt.Color(White with { W = 0.9f }),
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

    private static void DrawClouds(ImDrawListPtr drawList, UpdraftBoard board, in Camera2D camera, in UpdraftTone tone,
        Vector2 bird, float time)
    {
        var view = camera.View;
        var clouds = board.Clouds;
        for (var index = 0; index < clouds.Length; index++)
        {
            ref readonly var cloud = ref clouds[index];
            if (!cloud.Active)
            {
                continue;
            }

            var halfWidth = camera.Px(cloud.HalfWidth);
            var y = ScreenY(in camera, cloud.Y);
            if (y < view.Min.Y - halfWidth * 2f || y > view.Max.Y + halfWidth)
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

            var first = FirstCopy(in camera, cloud.X, cloud.HalfWidth * 1.6f);
            var last = LastCopy(in camera, cloud.X, cloud.HalfWidth * 1.6f);
            for (var copy = first; copy <= last; copy++)
            {
                var x = ScreenX(in camera, cloud.X + copy * UpdraftBoard.FieldWidth);
                UpdraftArt.DrawCloud(drawList, new Vector2(x, y), halfWidth, cloud, tone.Light, time, charge);
            }
        }
    }

    private static void DrawPickups(ImDrawListPtr drawList, UpdraftBoard board, in Camera2D camera, float time)
    {
        var view = camera.View;
        var pickups = board.Pickups;
        var size = camera.Px(PickupSize);
        for (var index = 0; index < pickups.Length; index++)
        {
            ref readonly var pickup = ref pickups[index];
            if (!pickup.Active)
            {
                continue;
            }

            var bob = MathF.Sin(time * 2.4f + pickup.Phase) * 0.08f;
            var y = ScreenY(in camera, pickup.Y - bob);
            if (y < view.Min.Y - size * 3f || y > view.Max.Y + size * 3f)
            {
                continue;
            }

            var first = FirstCopy(in camera, pickup.X, PickupSize * 3f);
            var last = LastCopy(in camera, pickup.X, PickupSize * 3f);
            for (var copy = first; copy <= last; copy++)
            {
                var center = new Vector2(ScreenX(in camera, pickup.X + copy * UpdraftBoard.FieldWidth), y);
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

    private void DrawBirds(ImDrawListPtr drawList, UpdraftBoard board, in Camera2D camera, Vector2 bird, float time)
    {
        var y = bird.Y;
        if (!board.Launched)
        {
            y += 0.18f + MathF.Sin(time * 3f) * 0.1f;
        }

        var radius = camera.Px(UpdraftBoard.BirdRadius) * 1.15f;
        var screenY = ScreenY(in camera, y);
        var first = FirstCopy(in camera, bird.X, UpdraftBoard.BirdRadius * 2.9f);
        var last = LastCopy(in camera, bird.X, UpdraftBoard.BirdRadius * 2.9f);
        for (var copy = first; copy <= last; copy++)
        {
            var center = new Vector2(ScreenX(in camera, bird.X + copy * UpdraftBoard.FieldWidth), screenY);
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
            UpdraftArt.Color(White with { W = 0.35f }));
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
            UpdraftArt.Color(White));
        var pupil = new Vector2(0.42f + gaze.X * 0.1f, -0.22f + gaze.Y * 0.12f);
        UpdraftArt.FillEllipse(drawList, frame, pupil, new Vector2(0.14f, 0.16f), 0f, UpdraftArt.Color(BirdEye));
        UpdraftArt.FillEllipse(drawList, frame, pupil + new Vector2(-0.05f, -0.07f), new Vector2(0.05f, 0.05f), 0f,
            UpdraftArt.Color(White));
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
}
