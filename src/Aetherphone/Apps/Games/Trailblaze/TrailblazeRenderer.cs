using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Trailblaze;

internal sealed class TrailblazeRenderer
{
    public const float SliceLength = 2.5f;
    private const float RoadHalf = TrailblazeBoard.LaneCount * 0.5f;
    private const float CurbWidth = 0.16f;
    private const float DividerHalf = 0.035f;
    private const float GroundReach = 40f;
    private const float FenceOffset = RoadHalf + 0.36f;
    private const float FenceHeight = 0.62f;
    private const float FencePostHalf = 0.035f;
    private const float HazardHalf = TrailblazeBoard.HazardHalfWidth;
    private const float CartBodyBottom = 0.42f;
    private const float CartBodyTop = 1.55f;
    private const float CartCanvasInset = 0.08f;
    private const float WheelRadius = 0.38f;
    private const float CoinRadius = 0.3f;
    private const float PickupRadius = 0.42f;
    private const float PropDensity = 0.6f;
    private const int MountainPeaks = 24;
    private const int SpeedLineCount = 22;
    private const int EllipseSegments = 14;
    private const int ArchSegments = 8;
    private const int Stripes = 5;
    private const float StripeSkew = 0.14f;
    private static readonly Vector4 GrassLight = new(0.44f, 0.70f, 0.31f, 1f);
    private static readonly Vector4 GrassDark = new(0.38f, 0.63f, 0.27f, 1f);
    private static readonly Vector4 DirtLight = new(0.82f, 0.68f, 0.47f, 1f);
    private static readonly Vector4 DirtDark = new(0.77f, 0.62f, 0.42f, 1f);
    private static readonly Vector4 CurbWhite = new(0.96f, 0.94f, 0.88f, 1f);
    private static readonly Vector4 CurbRed = new(0.86f, 0.30f, 0.25f, 1f);
    private static readonly Vector4 Divider = new(0.98f, 0.93f, 0.80f, 0.85f);
    private static readonly Vector4 FenceWhite = new(0.95f, 0.92f, 0.85f, 1f);
    private static readonly Vector4 PitDeep = new(0.09f, 0.06f, 0.05f, 1f);
    private static readonly Vector4 PitWall = new(0.34f, 0.23f, 0.15f, 1f);
    private static readonly Vector4 PitLip = new(0.93f, 0.80f, 0.58f, 1f);
    private static readonly Vector4 StripeYellow = new(1f, 0.80f, 0.18f, 1f);
    private static readonly Vector4 StripeDark = new(0.16f, 0.13f, 0.12f, 1f);
    private static readonly Vector4 PostWood = new(0.44f, 0.29f, 0.18f, 1f);
    private static readonly Vector4 CartWood = new(0.64f, 0.31f, 0.19f, 1f);
    private static readonly Vector4 CartWoodDark = new(0.43f, 0.20f, 0.12f, 1f);
    private static readonly Vector4 Canvas = new(0.96f, 0.90f, 0.75f, 1f);
    private static readonly Vector4 CanvasShade = new(0.80f, 0.72f, 0.58f, 1f);
    private static readonly Vector4 CanvasStripe = new(0.82f, 0.27f, 0.24f, 1f);
    private static readonly Vector4 Wheel = new(0.22f, 0.16f, 0.12f, 1f);
    private static readonly Vector4 Lantern = new(1f, 0.84f, 0.45f, 1f);
    private static readonly Vector4 CoinGold = new(1f, 0.83f, 0.27f, 1f);
    private static readonly Vector4 CoinRim = new(0.86f, 0.58f, 0.10f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 PineGreen = new(0.16f, 0.42f, 0.27f, 1f);
    private static readonly Vector4 LeafGreen = new(0.33f, 0.60f, 0.25f, 1f);
    private static readonly Vector4 Bark = new(0.40f, 0.27f, 0.17f, 1f);
    private static readonly Vector4 Rock = new(0.56f, 0.58f, 0.54f, 1f);
    private static readonly Vector4 LampPost = new(0.24f, 0.22f, 0.26f, 1f);
    private static readonly Vector4 MountainFar = new(0.42f, 0.50f, 0.68f, 1f);
    private static readonly Vector4 MountainNear = new(0.30f, 0.42f, 0.42f, 1f);
    private static readonly Vector4 NightTint = new(0.62f, 0.68f, 1f, 1f);
    private static readonly float[] FarPeaks = BuildPeaks(0x51UL);
    private static readonly float[] NearPeaks = BuildPeaks(0xA7UL);
    private static readonly float[] LineAngle = new float[SpeedLineCount];
    private static readonly float[] LinePhase = new float[SpeedLineCount];
    private static readonly float[] LineRate = new float[SpeedLineCount];

    private float light = 1f;
    private float night;
    private Vector4 fog = White;

    static TrailblazeRenderer()
    {
        var random = GameRandom.FromSeed(0x7EA1B1A2EUL);
        for (var line = 0; line < SpeedLineCount; line++)
        {
            LineAngle[line] = (line + random.NextFloat() * 0.8f) / SpeedLineCount * MathF.Tau;
            LinePhase[line] = random.NextFloat();
            LineRate[line] = 1.4f + random.NextFloat() * 1.2f;
        }
    }

    public void Prepare(Vector4 fogColor, float skyProgress)
    {
        night = SmoothStep(0.36f, 0.74f, skyProgress);
        light = 1f - 0.52f * night;
        fog = fogColor with { W = 1f };
    }

    public uint Shade(Vector4 color, float fogAmount, float alpha = 1f)
    {
        var tint = Vector4.Lerp(White, NightTint, night);
        var lit = new Vector4(color.X * light * tint.X, color.Y * light * tint.Y, color.Z * light * tint.Z, color.W);
        var mixed = Vector4.Lerp(lit, fog, Math.Clamp(fogAmount, 0f, 1f));
        return ImGui.GetColorU32(mixed with { W = color.W * alpha });
    }

    public void DrawBackdrop(ImDrawListPtr drawList, in TrailblazeView view, float scroll)
    {
        var area = view.Area;
        DrawMountains(drawList, view, FarPeaks, scroll * 0.25f, 0.12f, Vector4.Lerp(fog, MountainFar, 0.45f));
        DrawMountains(drawList, view, NearPeaks, scroll * 0.55f, 0.07f, Vector4.Lerp(fog, MountainNear, 0.62f));
        var horizon = view.Road.HorizonY;
        var left = area.Min.X - area.Width;
        var right = area.Max.X + area.Width;
        var farLine = view.Road.ToScreen(0f, 0f, TrailblazeView.FarZ).Y;
        var haze = ImGui.GetColorU32(fog);
        drawList.AddQuadFilled(view.Transform(new Vector2(left, horizon - 1f)), view.Transform(new Vector2(right, horizon - 1f)),
            view.Transform(new Vector2(right, farLine + 2f)), view.Transform(new Vector2(left, farLine + 2f)), haze);
        var bandHeight = area.Height * 0.035f;
        for (var band = 0; band < 3; band++)
        {
            var top = horizon - bandHeight * (band + 1);
            var bottom = horizon - bandHeight * band;
            var color = ImGui.GetColorU32(fog with { W = 0.42f - band * 0.13f });
            drawList.AddQuadFilled(view.Transform(new Vector2(left, top)), view.Transform(new Vector2(right, top)),
                view.Transform(new Vector2(right, bottom)), view.Transform(new Vector2(left, bottom)), color);
        }
    }

    public void DrawGround(ImDrawListPtr drawList, in TrailblazeView view, TrailblazeBoard board)
    {
        var near = view.NearWorldZ;
        var far = view.FarWorldZ;
        var first = (int)MathF.Floor(far / SliceLength);
        var last = (int)MathF.Floor(near / SliceLength);
        for (var slice = first; slice >= last; slice--)
        {
            var z0 = MathF.Max(slice * SliceLength, near);
            var z1 = MathF.Min((slice + 1) * SliceLength, far);
            if (z1 <= z0)
            {
                continue;
            }

            var fogAmount = view.Fog(view.Depth((z0 + z1) * 0.5f));
            var even = (slice & 1) == 0;
            Strip(drawList, view, -GroundReach, GroundReach, z0, z1, Shade(even ? GrassLight : GrassDark, fogAmount));
            Strip(drawList, view, -RoadHalf, RoadHalf, z0, z1, Shade(even ? DirtLight : DirtDark, fogAmount));
            var curb = Shade(even ? CurbWhite : CurbRed, fogAmount);
            Strip(drawList, view, -RoadHalf - CurbWidth, -RoadHalf, z0, z1, curb);
            Strip(drawList, view, RoadHalf, RoadHalf + CurbWidth, z0, z1, curb);
            if (!even)
            {
                continue;
            }

            var divider = Shade(Divider, fogAmount);
            for (var edge = 1; edge < TrailblazeBoard.LaneCount; edge++)
            {
                var offset = edge - RoadHalf;
                Strip(drawList, view, offset - DividerHalf, offset + DividerHalf, z0, z1, divider);
            }
        }

        for (var index = board.HazardCount - 1; index >= 0; index--)
        {
            ref readonly var hazard = ref board.HazardAt(index);
            if (hazard.Kind == TrailblazeCell.Gap)
            {
                DrawPit(drawList, view, hazard);
            }
        }
    }

    public void DrawRoadside(ImDrawListPtr drawList, in TrailblazeView view, float time)
    {
        var near = view.NearWorldZ;
        var far = view.FarWorldZ;
        var first = (int)MathF.Floor(far / SliceLength);
        var last = (int)MathF.Ceiling(near / SliceLength);
        for (var slice = first; slice >= last; slice--)
        {
            var postZ = slice * SliceLength;
            for (var side = -1; side <= 1; side += 2)
            {
                if (Hash(slice, side) < PropDensity)
                {
                    DrawProp(drawList, view, slice, side, postZ + SliceLength * 0.5f, time);
                }
            }

            DrawFence(drawList, view, postZ);
        }
    }

    public void DrawObjects(ImDrawListPtr drawList, in TrailblazeView view, TrailblazeBoard board, float nearZ,
        float farZ, float time)
    {
        var hazard = LastAtOrBelow(board, farZ, 0);
        var coin = LastAtOrBelow(board, farZ, 1);
        var pickup = LastAtOrBelow(board, farZ, 2);
        while (true)
        {
            while (hazard >= 0 && board.HazardAt(hazard).Kind == TrailblazeCell.Gap)
            {
                hazard--;
            }

            var hazardZ = hazard >= 0 ? board.HazardAt(hazard).Z : float.MinValue;
            var coinZ = coin >= 0 ? board.CoinAt(coin).Z : float.MinValue;
            var pickupZ = pickup >= 0 ? board.PickupAt(pickup).Z : float.MinValue;
            var best = MathF.Max(hazardZ, MathF.Max(coinZ, pickupZ));
            if (best <= nearZ || best == float.MinValue)
            {
                return;
            }

            if (best == hazardZ)
            {
                DrawHazard(drawList, view, board.HazardAt(hazard));
                hazard--;
            }
            else if (best == coinZ)
            {
                DrawCoin(drawList, view, board.CoinAt(coin), time);
                coin--;
            }
            else
            {
                DrawPickup(drawList, view, board.PickupAt(pickup), time);
                pickup--;
            }
        }
    }

    public void DrawSpeedLines(ImDrawListPtr drawList, in TrailblazeView view, float intensity, float time, float scale)
    {
        if (intensity <= 0.01f)
        {
            return;
        }

        var area = view.Area;
        var origin = view.VanishingPoint;
        var reach = MathF.Max(Vector2.Distance(origin, area.Min), Vector2.Distance(origin, area.Max)) * 1.1f;
        var thickness = MathF.Max(1f, 1.6f * scale);
        for (var line = 0; line < SpeedLineCount; line++)
        {
            var travel = Fraction(time * LineRate[line] * (0.6f + intensity) + LinePhase[line]);
            var start = reach * (0.32f + 0.75f * travel);
            var length = reach * (0.06f + 0.16f * intensity) * (0.4f + travel);
            var direction = new Vector2(MathF.Cos(LineAngle[line]), MathF.Sin(LineAngle[line]));
            var alpha = 0.32f * intensity * MathF.Sin(travel * MathF.PI);
            drawList.AddLine(origin + direction * start, origin + direction * (start + length),
                ImGui.GetColorU32(White with { W = alpha }), thickness);
        }
    }

    public static void FillEllipse(ImDrawListPtr drawList, Vector2 center, float radiusX, float radiusY, uint color,
        float angle = 0f, int segments = EllipseSegments)
    {
        if (radiusX <= 0.2f || radiusY <= 0.2f)
        {
            return;
        }

        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        drawList.PathClear();
        for (var segment = 0; segment < segments; segment++)
        {
            var theta = MathF.Tau * segment / segments;
            var localX = MathF.Cos(theta) * radiusX;
            var localY = MathF.Sin(theta) * radiusY;
            drawList.PathLineTo(new Vector2(center.X + localX * cosine - localY * sine,
                center.Y + localX * sine + localY * cosine));
        }

        drawList.PathFillConvex(color);
    }

    private static int LastAtOrBelow(TrailblazeBoard board, float farZ, int source)
    {
        var count = source switch
        {
            0 => board.HazardCount,
            1 => board.CoinCount,
            _ => board.PickupCount,
        };
        for (var index = count - 1; index >= 0; index--)
        {
            var z = source switch
            {
                0 => board.HazardAt(index).Z,
                1 => board.CoinAt(index).Z,
                _ => board.PickupAt(index).Z,
            };
            if (z <= farZ)
            {
                return index;
            }
        }

        return -1;
    }

    private void DrawMountains(ImDrawListPtr drawList, in TrailblazeView view, float[] peaks, float scroll,
        float heightFraction, Vector4 color)
    {
        var area = view.Area;
        var span = area.Width * 2.2f;
        var spacing = span / MountainPeaks;
        var shift = (scroll + view.CameraOffset * area.Width * 0.08f) / spacing;
        var first = (int)MathF.Floor(shift);
        var fraction = shift - first;
        var columns = (int)MathF.Ceiling(area.Width * 1.6f / spacing) + 2;
        var horizon = view.Road.HorizonY;
        var left = area.Center.X - columns * spacing * 0.5f;
        var fill = ImGui.GetColorU32(Vector4.Lerp(color * light, fog, 0.15f) with { W = 1f });
        var previousPeak = Vector2.Zero;
        var previousBase = Vector2.Zero;
        for (var column = 0; column <= columns; column++)
        {
            var peak = peaks[Modulo(first + column, MountainPeaks)];
            var x = left + (column - fraction) * spacing;
            var top = view.Transform(new Vector2(x, horizon - peak * area.Height * heightFraction));
            var bottom = view.Transform(new Vector2(x, horizon + 3f));
            if (column > 0)
            {
                drawList.AddQuadFilled(previousBase, previousPeak, top, bottom, fill);
            }

            previousPeak = top;
            previousBase = bottom;
        }
    }

    private void DrawPit(ImDrawListPtr drawList, in TrailblazeView view, in TrailblazeHazard gap)
    {
        var depth = view.Depth(gap.Z + gap.Length);
        if (!view.Visible(view.Depth(gap.Z)) && !view.Visible(depth))
        {
            return;
        }

        var fogAmount = view.Fog(view.Depth(gap.Z + gap.Length * 0.5f));
        var lane = gap.Lane - 1f;
        var left = lane - 0.5f;
        var right = lane + 0.5f;
        var far = MathF.Min(gap.Z + gap.Length, view.FarWorldZ);
        Strip(drawList, view, left, right, gap.Z, far, Shade(PitDeep, fogAmount * 0.7f));
        Strip(drawList, view, left, right, gap.Z + gap.Length * 0.6f, far, Shade(PitWall, fogAmount));
        Strip(drawList, view, left, right, gap.Z + gap.Length * 0.84f, far,
            Shade(Vector4.Lerp(PitWall, PitLip, 0.35f), fogAmount));
        Strip(drawList, view, left, right, gap.Z, gap.Z + 0.14f, Shade(PitLip, fogAmount));
        Strip(drawList, view, left - 0.02f, left + 0.05f, gap.Z, far, Shade(PitLip, fogAmount, 0.7f));
        Strip(drawList, view, right - 0.05f, right + 0.02f, gap.Z, far, Shade(PitLip, fogAmount, 0.7f));
    }

    private void DrawFence(ImDrawListPtr drawList, in TrailblazeView view, float z)
    {
        var depth = view.Depth(z);
        if (!view.Visible(depth))
        {
            return;
        }

        var fogAmount = view.Fog(depth);
        var alpha = view.Fade(depth);
        var post = Shade(FenceWhite, fogAmount, alpha);
        var rail = Shade(FenceWhite, fogAmount, alpha * 0.9f);
        var railWidth = MathF.Max(1f, 0.07f * view.Scale(depth));
        for (var side = -1; side <= 1; side += 2)
        {
            var offset = side * FenceOffset;
            drawList.AddQuadFilled(view.At(offset - FencePostHalf, 0f, z), view.At(offset + FencePostHalf, 0f, z),
                view.At(offset + FencePostHalf, FenceHeight, z), view.At(offset - FencePostHalf, FenceHeight, z), post);
            var farZ = z + SliceLength;
            if (!view.Visible(view.Depth(farZ)))
            {
                continue;
            }

            drawList.AddLine(view.At(offset, FenceHeight * 0.88f, z), view.At(offset, FenceHeight * 0.88f, farZ), rail,
                railWidth);
            drawList.AddLine(view.At(offset, FenceHeight * 0.45f, z), view.At(offset, FenceHeight * 0.45f, farZ), rail,
                railWidth * 0.8f);
        }
    }

    private void DrawProp(ImDrawListPtr drawList, in TrailblazeView view, int slice, int side, float z, float time)
    {
        var depth = view.Depth(z);
        if (!view.Visible(depth))
        {
            return;
        }

        var offset = side * (RoadHalf + 0.95f + Hash(slice, side + 13) * 2.8f);
        var size = 0.85f + Hash(slice, side + 29) * 0.5f;
        var fogAmount = view.Fog(depth);
        var alpha = view.Fade(depth);
        var scale = view.Scale(depth);
        var kind = (int)(Hash(slice, side + 7) * 4f);
        var ground = view.At(offset, 0f, z);
        FillEllipse(drawList, ground, 0.7f * size * scale, 0.16f * size * scale, Shade(Shadow with { W = 0.18f }, fogAmount, alpha));
        switch (kind)
        {
            case 0:
                DrawPine(drawList, view, offset, z, size, fogAmount, alpha);
                return;
            case 1:
                DrawRoundTree(drawList, view, offset, z, size, scale, fogAmount, alpha);
                return;
            case 2:
                DrawLamp(drawList, view, offset, z, scale, fogAmount, alpha, time + slice);
                return;
            default:
                FillEllipse(drawList, view.At(offset, 0.28f * size, z), 0.55f * size * scale, 0.36f * size * scale,
                    Shade(Rock, fogAmount, alpha));
                FillEllipse(drawList, view.At(offset - 0.1f, 0.42f * size, z), 0.28f * size * scale, 0.14f * size * scale,
                    Shade(Vector4.Lerp(Rock, White, 0.3f), fogAmount, alpha));
                return;
        }
    }

    private void DrawPine(ImDrawListPtr drawList, in TrailblazeView view, float offset, float z, float size, float fogAmount,
        float alpha)
    {
        var trunk = Shade(Bark, fogAmount, alpha);
        drawList.AddQuadFilled(view.At(offset - 0.08f, 0f, z), view.At(offset + 0.08f, 0f, z),
            view.At(offset + 0.08f, 0.7f * size, z), view.At(offset - 0.08f, 0.7f * size, z), trunk);
        for (var tier = 0; tier < 3; tier++)
        {
            var bottom = (0.5f + tier * 0.75f) * size;
            var width = (0.95f - tier * 0.22f) * size;
            var top = bottom + 1.25f * size;
            var shade = Shade(Vector4.Lerp(PineGreen, LeafGreen, tier * 0.18f), fogAmount, alpha);
            drawList.AddTriangleFilled(view.At(offset - width, bottom, z), view.At(offset + width, bottom, z),
                view.At(offset, top, z), shade);
        }
    }

    private void DrawRoundTree(ImDrawListPtr drawList, in TrailblazeView view, float offset, float z, float size,
        float scale, float fogAmount, float alpha)
    {
        drawList.AddQuadFilled(view.At(offset - 0.1f, 0f, z), view.At(offset + 0.1f, 0f, z),
            view.At(offset + 0.07f, 1.4f * size, z), view.At(offset - 0.07f, 1.4f * size, z), Shade(Bark, fogAmount, alpha));
        var leaf = Shade(LeafGreen, fogAmount, alpha);
        var light = Shade(Vector4.Lerp(LeafGreen, White, 0.22f), fogAmount, alpha);
        drawList.AddCircleFilled(view.At(offset, 1.9f * size, z), 0.8f * size * scale, leaf, 20);
        drawList.AddCircleFilled(view.At(offset - 0.45f * size, 1.55f * size, z), 0.55f * size * scale, leaf, 18);
        drawList.AddCircleFilled(view.At(offset + 0.45f * size, 1.6f * size, z), 0.55f * size * scale, leaf, 18);
        drawList.AddCircleFilled(view.At(offset - 0.2f * size, 2.15f * size, z), 0.35f * size * scale, light, 16);
    }

    private void DrawLamp(ImDrawListPtr drawList, in TrailblazeView view, float offset, float z, float scale,
        float fogAmount, float alpha, float phase)
    {
        drawList.AddQuadFilled(view.At(offset - 0.05f, 0f, z), view.At(offset + 0.05f, 0f, z),
            view.At(offset + 0.04f, 2.5f, z), view.At(offset - 0.04f, 2.5f, z), Shade(LampPost, fogAmount, alpha));
        var lamp = view.At(offset, 2.6f, z);
        var glow = (0.35f + 0.65f * night) * alpha * (0.9f + 0.1f * MathF.Sin(phase * 3f));
        drawList.AddCircleFilled(lamp, 0.9f * scale, ImGui.GetColorU32(Lantern with { W = 0.16f * glow }), 20);
        drawList.AddCircleFilled(lamp, 0.45f * scale, ImGui.GetColorU32(Lantern with { W = 0.32f * glow }), 16);
        drawList.AddCircleFilled(lamp, 0.17f * scale, ImGui.GetColorU32(Vector4.Lerp(Lantern, White, 0.4f) with { W = alpha }), 12);
    }

    private void DrawHazard(ImDrawListPtr drawList, in TrailblazeView view, in TrailblazeHazard hazard)
    {
        if (hazard.Kind == TrailblazeCell.Barrier)
        {
            DrawBarrier(drawList, view, hazard);
            return;
        }

        if (hazard.Kind == TrailblazeCell.Cart)
        {
            DrawCart(drawList, view, hazard);
        }
    }

    private void DrawBarrier(ImDrawListPtr drawList, in TrailblazeView view, in TrailblazeHazard barrier)
    {
        var z = barrier.Z;
        var depth = view.Depth(z);
        if (!view.Visible(depth))
        {
            return;
        }

        var fogAmount = view.Fog(depth);
        var alpha = view.Fade(depth);
        var lane = barrier.Lane - 1f;
        var left = lane - HazardHalf;
        var right = lane + HazardHalf;
        var back = z + barrier.Length;
        Strip(drawList, view, left - 0.04f, right + 0.04f, z, back + 0.25f, Shade(Shadow with { W = 0.2f }, fogAmount, alpha));
        var post = Shade(PostWood, fogAmount, alpha);
        Post(drawList, view, left, z, TrailblazeBoard.BarrierTop, post);
        Post(drawList, view, right, z, TrailblazeBoard.BarrierTop, post);
        var bottom = TrailblazeBoard.BarrierBottom;
        var top = TrailblazeBoard.BarrierTop;
        drawList.AddQuadFilled(view.At(left, top, z), view.At(right, top, z), view.At(right, top, back),
            view.At(left, top, back), Shade(Vector4.Lerp(StripeYellow, White, 0.25f), fogAmount, alpha));
        var lowerLeft = view.At(left, bottom, z);
        var lowerRight = view.At(right, bottom, z);
        var upperRight = view.At(right, top, z);
        var upperLeft = view.At(left, top, z);
        drawList.AddQuadFilled(lowerLeft, lowerRight, upperRight, upperLeft, Shade(StripeYellow, fogAmount, alpha));
        var dark = Shade(StripeDark, fogAmount, alpha);
        for (var stripe = 0; stripe < Stripes; stripe++)
        {
            var start = (stripe + 0.1f) / Stripes;
            var end = (stripe + 0.55f) / Stripes;
            var bottomStart = Vector2.Lerp(lowerLeft, lowerRight, start);
            var bottomEnd = Vector2.Lerp(lowerLeft, lowerRight, end);
            var topStart = Vector2.Lerp(upperLeft, upperRight, MathF.Min(1f, start + StripeSkew));
            var topEnd = Vector2.Lerp(upperLeft, upperRight, MathF.Min(1f, end + StripeSkew));
            drawList.AddQuadFilled(bottomStart, bottomEnd, topEnd, topStart, dark);
        }

        drawList.AddQuad(lowerLeft, lowerRight, upperRight, upperLeft, dark, MathF.Max(1f, 0.05f * view.Scale(depth)));
    }

    private static void Post(ImDrawListPtr drawList, in TrailblazeView view, float offset, float z, float height, uint color)
    {
        drawList.AddQuadFilled(view.At(offset - 0.05f, 0f, z), view.At(offset + 0.05f, 0f, z),
            view.At(offset + 0.05f, height, z), view.At(offset - 0.05f, height, z), color);
    }

    private void DrawCart(ImDrawListPtr drawList, in TrailblazeView view, in TrailblazeHazard cart)
    {
        var near = cart.Z;
        var nearDepth = view.Depth(near);
        if (nearDepth >= TrailblazeView.FarZ || view.Depth(near + cart.Length) <= TrailblazeView.NearZ * 0.35f)
        {
            return;
        }

        var far = MathF.Min(near + cart.Length, view.FarWorldZ);
        var fogAmount = view.Fog(MathF.Max(nearDepth, TrailblazeView.NearZ));
        var alpha = view.Fade(nearDepth);
        var lane = cart.Lane - 1f;
        var left = lane - HazardHalf;
        var right = lane + HazardHalf;
        var canvasTop = TrailblazeBoard.CartHeight;
        Strip(drawList, view, left - 0.06f, right + 0.14f, near - 0.1f, far + 0.3f, Shade(Shadow with { W = 0.26f }, fogAmount, alpha));
        var wheel = Shade(Wheel, fogAmount, alpha);
        var camera = view.CameraOffset;
        if (right < camera || left > camera)
        {
            var side = right < camera ? right : left;
            var inner = right < camera ? right - CartCanvasInset : left + CartCanvasInset;
            drawList.AddQuadFilled(view.At(side, CartBodyBottom, near), view.At(side, CartBodyBottom, far),
                view.At(side, CartBodyTop, far), view.At(side, CartBodyTop, near), Shade(CartWoodDark, fogAmount, alpha));
            drawList.AddQuadFilled(view.At(side, CartBodyTop, near), view.At(side, CartBodyTop, far),
                view.At(inner, canvasTop, far), view.At(inner, canvasTop, near), Shade(CanvasShade, fogAmount, alpha));
            drawList.AddLine(view.At(side, (CartBodyTop + canvasTop) * 0.5f, near), view.At(inner, (CartBodyTop + canvasTop) * 0.5f, far),
                Shade(CanvasStripe, fogAmount, alpha), MathF.Max(1f, 0.06f * view.Scale(nearDepth)));
            DrawWheel(drawList, view, side, near + 0.75f, wheel);
            if (cart.Length > 2.5f)
            {
                DrawWheel(drawList, view, side, far - 0.75f, wheel);
            }
        }

        drawList.AddQuadFilled(view.At(left + CartCanvasInset, canvasTop, near), view.At(right - CartCanvasInset, canvasTop, near),
            view.At(right - CartCanvasInset, canvasTop, far), view.At(left + CartCanvasInset, canvasTop, far),
            Shade(Vector4.Lerp(Canvas, CanvasShade, 0.35f), fogAmount, alpha));
        DrawWheel(drawList, view, left + 0.1f, near + 0.2f, wheel);
        DrawWheel(drawList, view, right - 0.1f, near + 0.2f, wheel);
        var bodyLowerLeft = view.At(left, CartBodyBottom, near);
        var bodyLowerRight = view.At(right, CartBodyBottom, near);
        var bodyUpperRight = view.At(right, CartBodyTop, near);
        var bodyUpperLeft = view.At(left, CartBodyTop, near);
        drawList.AddQuadFilled(bodyLowerLeft, bodyLowerRight, bodyUpperRight, bodyUpperLeft, Shade(CartWood, fogAmount, alpha));
        var plank = Shade(CartWoodDark, fogAmount, alpha);
        var plankWidth = MathF.Max(1f, 0.04f * view.Scale(nearDepth));
        for (var line = 1; line < 3; line++)
        {
            var amount = line / 3f;
            drawList.AddLine(Vector2.Lerp(bodyLowerLeft, bodyUpperLeft, amount), Vector2.Lerp(bodyLowerRight, bodyUpperRight, amount),
                plank, plankWidth);
        }

        DrawArch(drawList, view, left, right, CartBodyTop, canvasTop, near, Shade(Canvas, fogAmount, alpha));
        DrawArch(drawList, view, left + 0.16f, right - 0.16f, CartBodyTop, canvasTop - 0.2f, near,
            Shade(Vector4.Lerp(Wheel, CartWoodDark, 0.4f), fogAmount, alpha));
        var lantern = view.At(right - 0.05f, CartBodyTop + 0.1f, near - 0.02f);
        var glow = (0.4f + 0.6f * night) * alpha;
        var scale = view.Scale(nearDepth);
        drawList.AddCircleFilled(lantern, 0.5f * scale, ImGui.GetColorU32(Lantern with { W = 0.18f * glow }), 16);
        drawList.AddCircleFilled(lantern, 0.12f * scale, ImGui.GetColorU32(Lantern with { W = alpha }), 10);
    }

    private void DrawWheel(ImDrawListPtr drawList, in TrailblazeView view, float offset, float z, uint color)
    {
        var depth = view.Depth(z);
        if (!view.Visible(depth))
        {
            return;
        }

        var scale = view.Scale(depth);
        var center = view.At(offset, WheelRadius, z);
        FillEllipse(drawList, center, WheelRadius * scale * 0.42f, WheelRadius * scale, color, view.Roll);
    }

    private static void DrawArch(ImDrawListPtr drawList, in TrailblazeView view, float left, float right, float bottom,
        float top, float z, uint color)
    {
        var middle = (left + right) * 0.5f;
        var halfWidth = (right - left) * 0.5f;
        drawList.PathClear();
        for (var segment = 0; segment <= ArchSegments; segment++)
        {
            var theta = MathF.PI * segment / ArchSegments;
            drawList.PathLineTo(view.At(middle - MathF.Cos(theta) * halfWidth, bottom + MathF.Sin(theta) * (top - bottom), z));
        }

        drawList.PathFillConvex(color);
    }

    private void DrawCoin(ImDrawListPtr drawList, in TrailblazeView view, in TrailblazeCoin coin, float time)
    {
        var depth = view.Depth(coin.Z);
        if (!view.Visible(depth))
        {
            return;
        }

        var scale = view.Scale(depth);
        var radius = CoinRadius * scale;
        var alpha = view.Fade(depth);
        var fogAmount = view.Fog(depth) * 0.55f;
        var offset = coin.LaneX - 1f;
        if (coin.Height > 0.9f && !coin.Magnetized)
        {
            FillEllipse(drawList, view.At(offset, 0f, coin.Z), radius * 0.9f, radius * 0.3f,
                Shade(Shadow with { W = 0.16f }, fogAmount, alpha), view.Roll, 10);
        }

        var center = view.At(offset, coin.Height, coin.Z);
        if (radius < 1.6f)
        {
            drawList.AddCircleFilled(center, MathF.Max(0.8f, radius), Shade(CoinGold, fogAmount, alpha), 6);
            return;
        }

        var spin = MathF.Abs(MathF.Cos(time * 4.6f + coin.Z * 0.45f));
        var width = radius * (0.22f + 0.78f * spin);
        var glow = 0.18f + 0.22f * night;
        drawList.AddCircleFilled(center, radius * 1.9f, ImGui.GetColorU32(CoinGold with { W = glow * alpha }), 16);
        FillEllipse(drawList, center, width, radius, Shade(CoinRim, fogAmount, alpha), view.Roll);
        FillEllipse(drawList, center, width * 0.76f, radius * 0.76f, Shade(CoinGold, fogAmount, alpha), view.Roll);
        if (spin <= 0.45f)
        {
            return;
        }

        FillEllipse(drawList, center - new Vector2(width * 0.28f, radius * 0.3f), width * 0.22f, radius * 0.18f,
            ImGui.GetColorU32(White with { W = 0.8f * alpha }), view.Roll, 8);
    }

    private void DrawPickup(ImDrawListPtr drawList, in TrailblazeView view, in TrailblazePickup pickup, float time)
    {
        var depth = view.Depth(pickup.Z);
        if (!view.Visible(depth))
        {
            return;
        }

        var scale = view.Scale(depth);
        var alpha = view.Fade(depth);
        var bob = MathF.Sin(time * 3f + pickup.Z) * 0.15f;
        var offset = pickup.Lane - 1f;
        var center = view.At(offset, TrailblazeBoard.PickupHeight + bob, pickup.Z);
        var radius = PickupRadius * scale;
        var color = TrailblazePowerArt.Color(pickup.Kind);
        FillEllipse(drawList, view.At(offset, 0f, pickup.Z), radius * 0.9f, radius * 0.28f,
            Shade(Shadow with { W = 0.18f }, view.Fog(depth), alpha), view.Roll, 10);
        for (var layer = 3; layer >= 1; layer--)
        {
            drawList.AddCircleFilled(center, radius * (1f + layer * 0.45f), ImGui.GetColorU32(color with { W = 0.09f * (4 - layer) * alpha }), 20);
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Vector4.Lerp(White, color, 0.18f) with { W = 0.95f * alpha }), 24);
        var ring = time * 2.4f;
        drawList.PathClear();
        drawList.PathArcTo(center, radius * 1.18f, ring, ring + MathF.PI * 0.7f, 12);
        drawList.PathStroke(ImGui.GetColorU32(color with { W = alpha }), ImDrawFlags.None, MathF.Max(1f, radius * 0.12f));
        drawList.PathClear();
        drawList.PathArcTo(center, radius * 1.18f, ring + MathF.PI, ring + MathF.PI * 1.7f, 12);
        drawList.PathStroke(ImGui.GetColorU32(color with { W = alpha }), ImDrawFlags.None, MathF.Max(1f, radius * 0.12f));
        TrailblazePowerArt.Draw(drawList, pickup.Kind, center, radius * 0.62f, alpha);
    }

    private static void Strip(ImDrawListPtr drawList, in TrailblazeView view, float leftOffset, float rightOffset,
        float nearZ, float farZ, uint color)
    {
        drawList.AddQuadFilled(view.At(leftOffset, 0f, nearZ), view.At(rightOffset, 0f, nearZ),
            view.At(rightOffset, 0f, farZ), view.At(leftOffset, 0f, farZ), color);
    }

    private static float[] BuildPeaks(ulong seed)
    {
        var random = GameRandom.FromSeed(seed);
        var raw = new float[MountainPeaks];
        for (var index = 0; index < raw.Length; index++)
        {
            raw[index] = 0.25f + random.NextFloat() * 0.75f;
        }

        var peaks = new float[MountainPeaks];
        for (var index = 0; index < peaks.Length; index++)
        {
            var before = raw[Modulo(index - 1, MountainPeaks)];
            var after = raw[Modulo(index + 1, MountainPeaks)];
            peaks[index] = raw[index] * 0.6f + (before + after) * 0.2f;
        }

        return peaks;
    }

    private static float Hash(int index, int salt)
    {
        var value = (uint)index * 0x9E3779B1u ^ (uint)(salt + 101) * 0x85EBCA6Bu;
        value ^= value >> 15;
        value *= 0x2C1B3C6Du;
        value ^= value >> 12;
        value *= 0x297A2D39u;
        value ^= value >> 15;
        return (value & 0xFFFFFFu) / 16777216f;
    }

    private static int Modulo(int value, int divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static float Fraction(float value) => value - MathF.Floor(value);

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var progress = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return progress * progress * (3f - 2f * progress);
    }
}
