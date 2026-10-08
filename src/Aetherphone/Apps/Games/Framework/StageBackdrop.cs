using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal enum StageInk : byte
{
    Light,
    Dark,
}

internal sealed class StageBackdrop
{
    private const int LayerCount = 3;
    private const float PointerSmoothSeconds = 0.35f;
    private const float SweepSeconds = 0.6f;
    private const float SweepWidth = 120f;
    private const float SweepAlpha = 0.08f;
    private const float VignetteFraction = 0.34f;
    private const float VignetteAlpha = 0.38f;
    private const float SlateGridPitch = 26f;
    private const float SlateGridAlpha = 0.035f;
    private const float SlateGlowAlpha = 0.07f;
    private const float CornerAlpha = 0.18f;
    private const float CornerFraction = 0.28f;
    private const int StarCount = 36;
    private const int DustCount = 10;
    private const int SparkCount = 14;
    private const int FacetCount = 24;
    private const int BladeCount = 12;
    private const int BankPuffs = 7;
    private const int HillColumns = 28;
    private const int GridLines = 9;
    private const float GridScrollSpeed = 18f;
    private const int SkyBands = 5;
    private const int BuildingCount = 15;
    private const int WindowCount = 40;
    private const int BokehCount = 24;
    private const int CrowdHeads = 30;
    private const int PhoneLightCount = 60;
    private const int ConeLayers = 5;
    private const float SkylineBase = 0.80f;
    private const float CrowdTop = 0.66f;
    private const float CrowdBottom = 0.82f;
    private const float TowerTop = 0.16f;
    private const float TowerBase = 0.70f;
    private const float HazeDrift = 7f;
    private const float BokehRise = 9f;
    private const ulong TableSeed = 0x5747A6E1C0FFEEUL;

    private static readonly float[] LayerParallax = { 0.05f, 0.15f, 0.35f };
    private static readonly float[] LayerPointerShift = { 2f, 5f, 9f };
    private static readonly float[] StarX = new float[StarCount];
    private static readonly float[] StarY = new float[StarCount];
    private static readonly float[] StarSize = new float[StarCount];
    private static readonly float[] StarPhase = new float[StarCount];
    private static readonly float[] DustX = new float[DustCount];
    private static readonly float[] DustY = new float[DustCount];
    private static readonly float[] DustSize = new float[DustCount];
    private static readonly float[] DustSpeed = new float[DustCount];
    private static readonly float[] SparkX = new float[SparkCount];
    private static readonly float[] SparkY = new float[SparkCount];
    private static readonly float[] SparkPhase = new float[SparkCount];
    private static readonly float[] FacetX = new float[FacetCount];
    private static readonly float[] FacetY = new float[FacetCount];
    private static readonly float[] FacetSize = new float[FacetCount];
    private static readonly float[] FacetAngle = new float[FacetCount];
    private static readonly float[] BladeX = new float[BladeCount];
    private static readonly float[] BladeHeight = new float[BladeCount];
    private static readonly float[] BladePhase = new float[BladeCount];
    private static readonly float[] StalactiteX = { 0.18f, 0.52f, 0.81f };
    private static readonly float[] StalactiteWidth = { 0.12f, 0.08f, 0.15f };
    private static readonly float[] StalactiteDepth = { 0.22f, 0.15f, 0.27f };
    private static readonly float[] BuildingX = new float[BuildingCount];
    private static readonly float[] BuildingWidth = new float[BuildingCount];
    private static readonly float[] BuildingHeight = new float[BuildingCount];
    private static readonly byte[] WindowBuilding = new byte[WindowCount];
    private static readonly float[] WindowX = new float[WindowCount];
    private static readonly float[] WindowY = new float[WindowCount];
    private static readonly float[] WindowPhase = new float[WindowCount];
    private static readonly float[] BokehX = new float[BokehCount];
    private static readonly float[] BokehY = new float[BokehCount];
    private static readonly float[] BokehSize = new float[BokehCount];
    private static readonly float[] BokehSpeed = new float[BokehCount];
    private static readonly byte[] BokehTint = new byte[BokehCount];
    private static readonly float[] HeadX = new float[CrowdHeads];
    private static readonly float[] HeadSize = new float[CrowdHeads];
    private static readonly float[] PhoneX = new float[PhoneLightCount];
    private static readonly float[] PhoneY = new float[PhoneLightCount];
    private static readonly float[] PhonePhase = new float[PhoneLightCount];
    private static readonly float[] TowerX = { 0.10f, 0.90f };

    private static readonly Vector4 StripTop = new(0.039f, 0.027f, 0.086f, 1f);
    private static readonly Vector4 StripBottom = new(0.016f, 0.012f, 0.035f, 1f);
    private static readonly Vector4 StripWarmTop = new(0.085f, 0.040f, 0.035f, 1f);
    private static readonly Vector4 StripWarmBottom = new(0.030f, 0.014f, 0.012f, 1f);
    private static readonly Vector4 StripSkyline = new(0.075f, 0.055f, 0.13f, 1f);
    private static readonly Vector4 WindowLit = new(1f, 0.80f, 0.48f, 1f);
    private static readonly Vector4 NeonRose = new(1f, 0.239f, 0.604f, 1f);
    private static readonly Vector4 NeonCyan = new(0.180f, 0.902f, 1f, 1f);
    private static readonly Vector4 BokehGold = new(1f, 0.788f, 0.290f, 1f);
    private static readonly Vector4 ArenaTop = new(0.024f, 0.063f, 0.110f, 1f);
    private static readonly Vector4 ArenaBottom = new(0.008f, 0.024f, 0.047f, 1f);
    private static readonly Vector4 ArenaSteel = new(0.10f, 0.14f, 0.20f, 1f);
    private static readonly Vector4 Floodlight = new(0.93f, 0.96f, 1f, 1f);
    private static readonly Vector4 CrowdShade = new(0.020f, 0.035f, 0.060f, 1f);
    private static readonly Vector4 PhoneGlow = new(0.82f, 0.90f, 1f, 1f);
    private static readonly Vector4 TrackDust = new(0.86f, 0.72f, 0.52f, 1f);
    private static readonly Vector4 LampWarm = new(1f, 0.78f, 0.45f, 1f);
    private static readonly Vector4[] BokehTints = { BokehGold, NeonRose, NeonCyan };

    private static readonly Vector4[] SkyTops =
    {
        new(0.33f, 0.42f, 0.74f, 1f), new(0.24f, 0.53f, 0.92f, 1f), new(0.30f, 0.22f, 0.52f, 1f),
        new(0.03f, 0.04f, 0.13f, 1f), new(0.02f, 0.04f, 0.11f, 1f),
    };

    private static readonly Vector4[] SkyMiddles =
    {
        new(0.78f, 0.60f, 0.74f, 1f), new(0.47f, 0.74f, 0.97f, 1f), new(0.86f, 0.42f, 0.42f, 1f),
        new(0.08f, 0.10f, 0.25f, 1f), new(0.04f, 0.13f, 0.21f, 1f),
    };

    private static readonly Vector4[] SkyBottoms =
    {
        new(1.00f, 0.78f, 0.60f, 1f), new(0.78f, 0.92f, 1.00f, 1f), new(1.00f, 0.66f, 0.38f, 1f),
        new(0.16f, 0.18f, 0.36f, 1f), new(0.07f, 0.22f, 0.29f, 1f),
    };

    private static readonly Vector4[] SkySuns =
    {
        new(1f, 0.86f, 0.62f, 1f), new(1f, 0.97f, 0.84f, 1f), new(1f, 0.56f, 0.30f, 1f), new(0.92f, 0.94f, 1f, 1f),
        new(0.86f, 0.96f, 1f, 1f),
    };

    private static readonly float[] SkySunHeights = { 0.74f, 0.16f, 0.66f, 0.18f, 0.14f };
    private static readonly float[] SkyMoons = { 0f, 0f, 0f, 1f, 1f };
    private static readonly float[] SkyStars = { 0f, 0f, 0.12f, 0.85f, 1f };
    private static readonly Vector4 NearBlack = new(0.03f, 0.03f, 0.05f, 1f);
    private static readonly Vector4 NeonTop = new(0.024f, 0.027f, 0.059f, 1f);
    private static readonly Vector4 NeonBottom = new(0.063f, 0.078f, 0.165f, 1f);
    private static readonly Vector4 CavernTop = new(0.13f, 0.10f, 0.08f, 1f);
    private static readonly Vector4 CavernBottom = new(0.02f, 0.015f, 0.015f, 1f);
    private static readonly Vector4 SlateTop = new(0.125f, 0.13f, 0.165f, 1f);
    private static readonly Vector4 SlateBottom = new(0.055f, 0.058f, 0.08f, 1f);
    private static readonly Vector4 MeadowSky = new(0.50f, 0.74f, 0.96f, 1f);
    private static readonly Vector4 MeadowHorizon = new(0.78f, 0.90f, 0.98f, 1f);
    private static readonly Vector4 MeadowGrassFar = new(0.38f, 0.66f, 0.36f, 1f);
    private static readonly Vector4 MeadowGrassNear = new(0.26f, 0.52f, 0.26f, 1f);
    private static readonly Vector4 MeadowSun = new(1f, 0.93f, 0.66f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    private Spring pointerX;
    private Spring pointerY;
    private Vector2 cameraShift;
    private bool cameraSeen;
    private float time;
    private float sweepProgress = 1f;
    private float sweepStrength;
    private float skyProgress;
    private float lampPool;
    private float warmth;
    private Vector4 ground = NearBlack;

    static StageBackdrop()
    {
        var random = GameRandom.FromSeed(TableSeed);
        for (var index = 0; index < StarCount; index++)
        {
            StarX[index] = random.NextFloat();
            StarY[index] = random.NextFloat();
            StarSize[index] = 1f + random.NextFloat();
            StarPhase[index] = random.NextFloat() * MathF.Tau;
        }

        for (var index = 0; index < DustCount; index++)
        {
            DustX[index] = random.NextFloat();
            DustY[index] = random.NextFloat();
            DustSize[index] = 3f + random.NextFloat() * 3f;
            DustSpeed[index] = 6f + random.NextFloat() * 10f;
        }

        for (var index = 0; index < SparkCount; index++)
        {
            SparkX[index] = random.NextFloat();
            SparkY[index] = random.NextFloat();
            SparkPhase[index] = random.NextFloat() * MathF.Tau;
        }

        for (var index = 0; index < FacetCount; index++)
        {
            FacetX[index] = random.NextFloat();
            FacetY[index] = random.NextFloat();
            FacetSize[index] = 14f + random.NextFloat() * 30f;
            FacetAngle[index] = random.NextFloat() * MathF.Tau;
        }

        for (var index = 0; index < BladeCount; index++)
        {
            BladeX[index] = (index + random.NextFloat() * 0.8f) / BladeCount;
            BladeHeight[index] = 14f + random.NextFloat() * 16f;
            BladePhase[index] = random.NextFloat() * MathF.Tau;
        }

        for (var index = 0; index < BuildingCount; index++)
        {
            BuildingWidth[index] = 0.055f + random.NextFloat() * 0.045f;
            BuildingX[index] = (index + 0.5f) / BuildingCount - BuildingWidth[index] * 0.5f
                + (random.NextFloat() - 0.5f) * 0.02f;
            BuildingHeight[index] = 0.06f + random.NextFloat() * 0.18f;
        }

        for (var index = 0; index < WindowCount; index++)
        {
            WindowBuilding[index] = (byte)random.Next(BuildingCount);
            WindowX[index] = 0.18f + random.NextFloat() * 0.64f;
            WindowY[index] = 0.12f + random.NextFloat() * 0.76f;
            WindowPhase[index] = random.NextFloat() * MathF.Tau;
        }

        for (var index = 0; index < BokehCount; index++)
        {
            BokehX[index] = random.NextFloat();
            BokehY[index] = random.NextFloat();
            BokehSize[index] = 5f + random.NextFloat() * 9f;
            BokehSpeed[index] = 0.5f + random.NextFloat();
            BokehTint[index] = (byte)random.Next(BokehTints.Length);
        }

        for (var index = 0; index < CrowdHeads; index++)
        {
            HeadX[index] = (index + random.NextFloat() * 0.6f) / CrowdHeads;
            HeadSize[index] = 0.6f + random.NextFloat() * 0.5f;
        }

        for (var index = 0; index < PhoneLightCount; index++)
        {
            PhoneX[index] = random.NextFloat();
            PhoneY[index] = 0.08f + random.NextFloat() * 0.8f;
            PhonePhase[index] = random.NextFloat() * MathF.Tau;
        }
    }

    public Backdrop Preset { get; private set; }

    public StageInk Ink => StageInk.Light;

    public Vector4 Ground => ground;

    public float Time => time;

    public static Vector4 LastGround { get; private set; } = NearBlack;

    public void Set(Backdrop preset)
    {
        Preset = preset;
        sweepProgress = 1f;
        sweepStrength = 0f;
        cameraSeen = false;
        lampPool = 0f;
        warmth = 0f;
    }

    public void SetSky(float progress)
    {
        skyProgress = Math.Clamp(progress, 0f, 1f);
    }

    public void SetLampPool(float strength)
    {
        lampPool = Math.Clamp(strength, 0f, 1f);
    }

    public void SetWarmth(float amount)
    {
        warmth = Math.Clamp(amount, 0f, 1f);
    }

    public void Sweep(float strength)
    {
        sweepStrength = Math.Clamp(strength, 0f, 1f);
        sweepProgress = 0f;
    }

    public void SetCamera(in Camera2D camera)
    {
        cameraShift = -camera.Origin * camera.EffectiveZoom;
        cameraSeen = true;
    }

    public void Update(float deltaSeconds, Rect full, Vector2 pointer, bool pointerInside)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        time += deltaSeconds;
        var targetX = 0f;
        var targetY = 0f;
        if (pointerInside && full.Width > 0f && full.Height > 0f)
        {
            targetX = Math.Clamp((pointer.X - full.Center.X) / full.Width, -0.5f, 0.5f);
            targetY = Math.Clamp((pointer.Y - full.Center.Y) / full.Height, -0.5f, 0.5f);
        }

        pointerX.Step(targetX, PointerSmoothSeconds, deltaSeconds);
        pointerY.Step(targetY, PointerSmoothSeconds, deltaSeconds);
        if (sweepProgress < 1f)
        {
            sweepProgress = MathF.Min(1f, sweepProgress + deltaSeconds / SweepSeconds);
        }
    }

    public void Draw(ImDrawListPtr drawList, Rect full, Vector4 accent, float scale)
    {
        drawList.PushClipRect(full.Min, full.Max, true);
        switch (Preset)
        {
            case Backdrop.Sky:
                DrawSky(drawList, full, scale);
                break;
            case Backdrop.Felt:
                DrawFelt(drawList, full, accent, scale);
                break;
            case Backdrop.Meadow:
                DrawMeadow(drawList, full, scale);
                break;
            case Backdrop.Neon:
                DrawNeon(drawList, full, accent, scale);
                break;
            case Backdrop.Cavern:
                DrawCavern(drawList, full, accent, scale);
                break;
            case Backdrop.Slate:
                DrawSlate(drawList, full, accent, scale);
                break;
            case Backdrop.Strip:
                DrawStrip(drawList, full, scale);
                break;
            case Backdrop.Arena:
                DrawArena(drawList, full, scale);
                break;
            default:
                DrawNebula(drawList, full, accent, scale);
                break;
        }

        DrawVignette(drawList, full);
        DrawSweep(drawList, full, scale);
        drawList.PopClipRect();
        LastGround = ground;
        cameraSeen = false;
    }

    private Vector2 LayerOffset(int layer, float scale)
    {
        var pointer = new Vector2(pointerX.Value, pointerY.Value) * LayerPointerShift[layer] * scale;
        return cameraSeen ? pointer + cameraShift * LayerParallax[layer] : pointer;
    }

    private static float Wrap(float value, float size)
    {
        if (size <= 0f)
        {
            return 0f;
        }

        return value - MathF.Floor(value / size) * size;
    }

    private void Gradient(ImDrawListPtr drawList, Rect full, Vector4 top, Vector4 bottom)
    {
        var topColor = ImGui.GetColorU32(top);
        var bottomColor = ImGui.GetColorU32(bottom);
        drawList.AddRectFilledMultiColor(full.Min, full.Max, topColor, topColor, bottomColor, bottomColor);
        ground = Vector4.Lerp(top, bottom, 0.5f);
    }

    private void DrawNebula(ImDrawListPtr drawList, Rect full, Vector4 accent, float scale)
    {
        Gradient(drawList, full, Palette.Darken(accent, 0.82f), NearBlack);
        DrawStars(drawList, full, LayerOffset(0, scale), scale, 1f, 0.9f);
        DrawGlowBlobs(drawList, full, accent, LayerOffset(1, scale));
        DrawDust(drawList, full, LayerOffset(2, scale), scale, Palette.Lighten(accent, 0.5f), 0.18f);
    }

    private void DrawStars(ImDrawListPtr drawList, Rect full, Vector2 offset, float scale, float amount,
        float sizeFactor)
    {
        if (amount <= 0.01f)
        {
            return;
        }

        for (var index = 0; index < StarCount; index++)
        {
            var x = full.Min.X + Wrap(StarX[index] * full.Width + offset.X, full.Width);
            var y = full.Min.Y + Wrap(StarY[index] * full.Height + offset.Y, full.Height);
            var twinkle = 0.45f + 0.55f * MathF.Sin(time * (1.1f + StarSize[index]) + StarPhase[index]);
            var alpha = amount * twinkle * 0.9f;
            var radius = StarSize[index] * sizeFactor * scale;
            drawList.AddCircleFilled(new Vector2(x, y), radius,
                ImGui.GetColorU32(new Vector4(1f, 1f, 0.96f, alpha)), 8);
        }
    }

    private void DrawGlowBlobs(ImDrawListPtr drawList, Rect full, Vector4 accent, Vector2 offset)
    {
        DrawGlowBlob(drawList, full, accent, offset, 0.52f, 0.31f, 0.23f, 0f, 1.7f, 0.22f, 0.20f, 0.075f);
        DrawGlowBlob(drawList, full, accent, offset, 0.44f, 0.22f, 0.29f, 2.4f, 4.1f, 0.80f, 0.42f, 0.060f);
        DrawGlowBlob(drawList, full, accent, offset, 0.60f, 0.17f, 0.21f, 4.9f, 0.8f, 0.46f, 0.86f, 0.055f);
    }

    private void DrawGlowBlob(ImDrawListPtr drawList, Rect full, Vector4 accent, Vector2 offset, float radiusFactor,
        float speedX, float speedY, float phaseX, float phaseY, float anchorX, float anchorY, float alpha)
    {
        var anchor = new Vector2(full.Min.X + full.Width * anchorX, full.Min.Y + full.Height * anchorY) + offset;
        var drift = new Vector2(MathF.Sin(time * speedX + phaseX) * full.Width * 0.14f,
            MathF.Cos(time * speedY + phaseY) * full.Height * 0.10f);
        var center = anchor + drift;
        var radius = full.Width * radiusFactor;
        var lit = Palette.Lighten(accent, 0.25f);
        for (var layer = 3; layer >= 1; layer--)
        {
            var layerRadius = radius * (0.45f + layer * 0.19f);
            var layerAlpha = alpha * (4 - layer) * 0.34f;
            drawList.AddCircleFilled(center, layerRadius, ImGui.GetColorU32(lit with { W = layerAlpha }));
        }
    }

    private void DrawDust(ImDrawListPtr drawList, Rect full, Vector2 offset, float scale, Vector4 tint, float alpha)
    {
        for (var index = 0; index < DustCount; index++)
        {
            var rise = time * DustSpeed[index] * scale;
            var x = full.Min.X + Wrap(DustX[index] * full.Width + offset.X + MathF.Sin(time * 0.4f + index) * 6f * scale,
                full.Width);
            var y = full.Min.Y + Wrap(DustY[index] * full.Height - rise + offset.Y, full.Height);
            var radius = DustSize[index] * scale * 0.5f;
            drawList.AddCircleFilled(new Vector2(x, y), radius * 2.2f, ImGui.GetColorU32(tint with { W = alpha * 0.25f }));
            drawList.AddCircleFilled(new Vector2(x, y), radius, ImGui.GetColorU32(tint with { W = alpha }));
        }
    }

    private void DrawSky(ImDrawListPtr drawList, Rect full, float scale)
    {
        var position = skyProgress * (SkyBands - 1);
        var band = Math.Min(SkyBands - 2, (int)position);
        var blend = Easing.SmoothStep(position - band);
        var top = Vector4.Lerp(SkyTops[band], SkyTops[band + 1], blend);
        var middle = Vector4.Lerp(SkyMiddles[band], SkyMiddles[band + 1], blend);
        var bottom = Vector4.Lerp(SkyBottoms[band], SkyBottoms[band + 1], blend);
        var sun = Vector4.Lerp(SkySuns[band], SkySuns[band + 1], blend);
        var sunHeight = Easing.Lerp(SkySunHeights[band], SkySunHeights[band + 1], blend);
        var moon = Easing.Lerp(SkyMoons[band], SkyMoons[band + 1], blend);
        var stars = Easing.Lerp(SkyStars[band], SkyStars[band + 1], blend);
        var split = full.Min.Y + full.Height * 0.55f;
        var topColor = ImGui.GetColorU32(top);
        var middleColor = ImGui.GetColorU32(middle);
        var bottomColor = ImGui.GetColorU32(bottom);
        drawList.AddRectFilledMultiColor(full.Min, new Vector2(full.Max.X, split), topColor, topColor, middleColor,
            middleColor);
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, split), full.Max, middleColor, middleColor,
            bottomColor, bottomColor);
        ground = middle;
        var far = LayerOffset(0, scale);
        DrawStars(drawList, full, far, scale, stars, 0.8f);
        var sunCenter = new Vector2(full.Min.X + full.Width * 0.76f, full.Min.Y + full.Height * sunHeight) + far * 0.5f;
        var sunRadius = full.Width * 0.075f;
        for (var layer = 3; layer >= 1; layer--)
        {
            drawList.AddCircleFilled(sunCenter, sunRadius * (1f + layer * 0.6f),
                ImGui.GetColorU32(sun with { W = 0.08f * (4 - layer) }), 36);
        }

        drawList.AddCircleFilled(sunCenter, sunRadius, ImGui.GetColorU32(sun), 36);
        if (moon > 0.01f)
        {
            drawList.AddCircleFilled(sunCenter + new Vector2(sunRadius * 0.42f, -sunRadius * 0.22f), sunRadius * 0.9f,
                ImGui.GetColorU32(top with { W = moon }), 36);
        }

        var cloud = Vector4.Lerp(White, bottom, 0.25f);
        var cloudAlpha = 0.26f * (1f - 0.7f * stars);
        DrawCloudBanks(drawList, full, LayerOffset(1, scale), scale, cloud with { W = cloudAlpha }, 0.38f, 1f, 4f);
        DrawCloudBanks(drawList, full, LayerOffset(2, scale), scale, cloud with { W = cloudAlpha * 0.8f }, 0.72f, 1.6f,
            9f);
    }

    private void DrawCloudBanks(ImDrawListPtr drawList, Rect full, Vector2 offset, float scale, Vector4 color,
        float rowFraction, float sizeFactor, float drift)
    {
        var fill = ImGui.GetColorU32(color);
        var soft = ImGui.GetColorU32(color with { W = color.W * 0.6f });
        var span = full.Width * 1.4f;
        for (var puff = 0; puff < BankPuffs; puff++)
        {
            var seed = puff * 3.7f + rowFraction * 11f;
            var travel = (puff + Hash(seed) * 0.6f) / BankPuffs * span + offset.X - time * drift * scale;
            var x = full.Min.X - full.Width * 0.2f + Wrap(travel, span);
            var y = full.Min.Y + full.Height * rowFraction + offset.Y + Hash(seed + 2f) * 16f * scale;
            var radius = (14f + Hash(seed + 1f) * 12f) * sizeFactor * scale;
            drawList.AddCircleFilled(new Vector2(x, y), radius, fill, 24);
            drawList.AddCircleFilled(new Vector2(x + radius * 0.8f, y + radius * 0.15f), radius * 0.72f, soft, 24);
            drawList.AddCircleFilled(new Vector2(x - radius * 0.7f, y + radius * 0.2f), radius * 0.6f, soft, 24);
        }
    }

    private void DrawFelt(ImDrawListPtr drawList, Rect full, Vector4 accent, float scale)
    {
        Gradient(drawList, full, Palette.ShadeToLuminance(accent, 0.35f), Palette.ShadeToLuminance(accent, 0.22f));
        var far = LayerOffset(0, scale);
        var spot = new Vector2(full.Center.X, full.Min.Y - full.Width * 0.25f) + far;
        for (var layer = 3; layer >= 1; layer--)
        {
            drawList.AddCircleFilled(spot, full.Width * (0.35f + layer * 0.22f),
                ImGui.GetColorU32(White with { W = 0.10f / layer }), 48);
        }

        var mid = LayerOffset(1, scale);
        var weave = ImGui.GetColorU32(White with { W = 0.035f });
        var bandWidth = full.Width * 0.22f;
        for (var band = 0; band < 2; band++)
        {
            var travel = Wrap(full.Width * (0.15f + band * 0.5f) + mid.X + time * 4f * scale, full.Width * 1.6f);
            var left = full.Min.X - full.Width * 0.3f + travel;
            drawList.AddQuadFilled(new Vector2(left, full.Min.Y), new Vector2(left + bandWidth, full.Min.Y),
                new Vector2(left + bandWidth + full.Height * 0.35f, full.Max.Y),
                new Vector2(left + full.Height * 0.35f, full.Max.Y), weave);
        }

        if (lampPool <= 0f)
        {
            return;
        }

        var pool = new Vector2(full.Center.X, full.Min.Y + full.Height * 0.52f) + mid * 0.5f;
        for (var layer = 4; layer >= 1; layer--)
        {
            drawList.AddCircleFilled(pool, full.Width * (0.18f + layer * 0.12f),
                ImGui.GetColorU32(LampWarm with { W = 0.055f * lampPool / layer }), 48);
        }
    }

    private void DrawStrip(ImDrawListPtr drawList, Rect full, float scale)
    {
        Gradient(drawList, full, Vector4.Lerp(StripTop, StripWarmTop, warmth),
            Vector4.Lerp(StripBottom, StripWarmBottom, warmth));
        var far = LayerOffset(0, scale);
        var horizon = full.Min.Y + full.Height * SkylineBase + far.Y;
        var glowColor = Vector4.Lerp(NeonRose, WindowLit, warmth);
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, horizon - full.Height * 0.22f),
            new Vector2(full.Max.X, horizon), ImGui.GetColorU32(glowColor with { W = 0f }),
            ImGui.GetColorU32(glowColor with { W = 0f }), ImGui.GetColorU32(glowColor with { W = 0.10f }),
            ImGui.GetColorU32(glowColor with { W = 0.10f }));
        var skyline = ImGui.GetColorU32(StripSkyline);
        for (var index = 0; index < BuildingCount; index++)
        {
            var left = full.Min.X + BuildingX[index] * full.Width + far.X;
            var top = horizon - BuildingHeight[index] * full.Height;
            drawList.AddRectFilled(new Vector2(left, top),
                new Vector2(left + BuildingWidth[index] * full.Width, full.Max.Y), skyline);
        }

        var pane = new Vector2(2f, 3f) * scale;
        for (var index = 0; index < WindowCount; index++)
        {
            var building = WindowBuilding[index];
            var left = full.Min.X + BuildingX[building] * full.Width + far.X;
            var height = BuildingHeight[building] * full.Height;
            var center = new Vector2(left + WindowX[index] * BuildingWidth[building] * full.Width,
                horizon - height + WindowY[index] * height);
            var twinkle = 0.45f + 0.4f * MathF.Sin(time * 0.7f + WindowPhase[index])
                + 0.15f * MathF.Sin(time * 2.3f + WindowPhase[index] * 1.7f);
            drawList.AddRectFilled(center - pane, center + pane,
                ImGui.GetColorU32(WindowLit with { W = Math.Clamp(twinkle, 0.12f, 0.95f) }));
        }

        var mid = LayerOffset(1, scale);
        DrawHaze(drawList, full, mid, NeonRose, 0.30f, HazeDrift * scale, 0.07f);
        DrawHaze(drawList, full, mid, NeonCyan, 0.50f, -HazeDrift * scale, 0.055f);
        Bokeh(drawList, full, time, 1f, LayerOffset(2, scale), scale);
    }

    private void DrawHaze(ImDrawListPtr drawList, Rect full, Vector2 offset, Vector4 tint, float rowFraction,
        float speed, float alpha)
    {
        var y = full.Min.Y + full.Height * rowFraction + offset.Y
            + MathF.Sin(time * 0.21f + rowFraction * 9f) * full.Height * 0.01f;
        var height = full.Height * 0.08f;
        var lit = ImGui.GetColorU32(tint with { W = alpha });
        var clear = ImGui.GetColorU32(tint with { W = 0f });
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, y - height), new Vector2(full.Max.X, y), clear, clear,
            lit, lit);
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, y), new Vector2(full.Max.X, y + height), lit, lit,
            clear, clear);
        var span = full.Width * 1.5f;
        for (var blob = 0; blob < 3; blob++)
        {
            var travel = Wrap(blob * span / 3f + time * speed + offset.X, span);
            var center = new Vector2(full.Min.X - full.Width * 0.25f + travel, y);
            for (var layer = 3; layer >= 1; layer--)
            {
                drawList.AddCircleFilled(center, full.Width * (0.10f + layer * 0.07f),
                    ImGui.GetColorU32(tint with { W = alpha * 0.45f / layer }), 32);
            }
        }
    }

    public static void Bokeh(ImDrawListPtr drawList, Rect rect, float time, float density, Vector2 offset,
        float scale)
    {
        if (density <= 0f || rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var shown = Math.Min(BokehCount, (int)MathF.Ceiling(BokehCount * Math.Clamp(density, 0f, 1f)));
        for (var index = 0; index < shown; index++)
        {
            var rise = time * BokehRise * BokehSpeed[index] * scale;
            var x = rect.Min.X + Wrap(BokehX[index] * rect.Width + offset.X + MathF.Sin(time * 0.3f + index) * 5f * scale,
                rect.Width);
            var y = rect.Min.Y + Wrap(BokehY[index] * rect.Height - rise + offset.Y, rect.Height);
            var radius = BokehSize[index] * scale;
            var breath = 0.6f + 0.4f * MathF.Sin(time * 0.8f + index * 1.3f);
            var tint = BokehTints[BokehTint[index]];
            drawList.AddCircleFilled(new Vector2(x, y), radius * 1.8f, ImGui.GetColorU32(tint with { W = 0.025f * breath }),
                20);
            drawList.AddCircleFilled(new Vector2(x, y), radius, ImGui.GetColorU32(tint with { W = 0.07f * breath }), 20);
        }
    }

    private void DrawArena(ImDrawListPtr drawList, Rect full, float scale)
    {
        Gradient(drawList, full, ArenaTop, ArenaBottom);
        var far = LayerOffset(0, scale);
        var steel = ImGui.GetColorU32(ArenaSteel);
        for (var tower = 0; tower < TowerX.Length; tower++)
        {
            var x = full.Min.X + TowerX[tower] * full.Width + far.X;
            var top = new Vector2(x, full.Min.Y + full.Height * TowerTop + far.Y);
            var bottom = new Vector2(x, full.Min.Y + full.Height * TowerBase + far.Y);
            drawList.AddLine(top, bottom, steel, MathF.Max(1f, 3f * scale));
            var bank = new Vector2(14f, 6f) * scale;
            drawList.AddRectFilled(top - bank, top + new Vector2(bank.X, bank.Y * 0.4f), steel, 2f * scale);
            for (var lamp = 0; lamp < 4; lamp++)
            {
                var lampCenter = new Vector2(top.X - bank.X * 0.75f + lamp * bank.X * 0.5f, top.Y - bank.Y * 0.3f);
                drawList.AddCircleFilled(lampCenter, 2.4f * scale, ImGui.GetColorU32(Floodlight with { W = 0.9f }), 10);
                drawList.AddCircleFilled(lampCenter, 6f * scale, ImGui.GetColorU32(Floodlight with { W = 0.08f }), 12);
            }

            var inward = tower == 0 ? 1f : -1f;
            for (var cone = 0; cone < 2; cone++)
            {
                var sweep = MathF.Sin(time * (0.18f + cone * 0.07f) + tower * 1.9f + cone) * 0.12f;
                var angle = MathF.PI * 0.5f - inward * (0.42f + cone * 0.30f) + sweep;
                Cone(drawList, top, angle, full.Height * 0.85f, 0.10f + cone * 0.02f, Floodlight, 0.05f);
            }
        }

        var mid = LayerOffset(1, scale);
        var crowdTop = full.Min.Y + full.Height * CrowdTop + mid.Y;
        var crowdBottom = full.Min.Y + full.Height * CrowdBottom + mid.Y;
        var shade = ImGui.GetColorU32(CrowdShade);
        drawList.AddRectFilled(new Vector2(full.Min.X, crowdTop), new Vector2(full.Max.X, full.Max.Y), shade);
        var headRadius = full.Width / CrowdHeads * 0.62f;
        for (var index = 0; index < CrowdHeads; index++)
        {
            var x = full.Min.X + Wrap(HeadX[index] * full.Width + mid.X, full.Width);
            drawList.AddCircleFilled(new Vector2(x, crowdTop), headRadius * HeadSize[index], shade, 14);
        }

        var crowdHeight = crowdBottom - crowdTop;
        for (var index = 0; index < PhoneLightCount; index++)
        {
            var flicker = MathF.Sin(time * (1.4f + (index % 5) * 0.37f) + PhonePhase[index]);
            if (flicker < 0.35f)
            {
                continue;
            }

            var x = full.Min.X + Wrap(PhoneX[index] * full.Width + mid.X, full.Width);
            var center = new Vector2(x, crowdTop + PhoneY[index] * crowdHeight);
            var alpha = (flicker - 0.35f) / 0.65f;
            drawList.AddCircleFilled(center, 1.3f * scale, ImGui.GetColorU32(PhoneGlow with { W = 0.85f * alpha }), 6);
            drawList.AddCircleFilled(center, 3.4f * scale, ImGui.GetColorU32(PhoneGlow with { W = 0.10f * alpha }), 8);
        }

        DrawDust(drawList, full, LayerOffset(2, scale), scale, TrackDust, 0.14f);
    }

    public static void Cone(ImDrawListPtr drawList, Vector2 origin, float angle, float length, float spread,
        Vector4 color, float alpha)
    {
        for (var layer = 0; layer < ConeLayers; layer++)
        {
            var share = 1f - layer / (float)ConeLayers;
            var half = spread * share;
            var reach = length * (0.7f + 0.3f * share);
            var left = origin + new Vector2(MathF.Cos(angle - half), MathF.Sin(angle - half)) * reach;
            var right = origin + new Vector2(MathF.Cos(angle + half), MathF.Sin(angle + half)) * reach;
            drawList.AddTriangleFilled(origin, left, right, ImGui.GetColorU32(color with { W = alpha / ConeLayers * 1.6f }));
        }
    }

    private void DrawMeadow(ImDrawListPtr drawList, Rect full, float scale)
    {
        var horizon = full.Min.Y + full.Height * 0.58f;
        var sky = ImGui.GetColorU32(MeadowSky);
        var horizonColor = ImGui.GetColorU32(MeadowHorizon);
        drawList.AddRectFilledMultiColor(full.Min, new Vector2(full.Max.X, horizon), sky, sky, horizonColor,
            horizonColor);
        var grassFar = ImGui.GetColorU32(MeadowGrassFar);
        var grassNear = ImGui.GetColorU32(MeadowGrassNear);
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, horizon), full.Max, grassFar, grassFar, grassNear,
            grassNear);
        ground = Vector4.Lerp(MeadowGrassFar, MeadowGrassNear, 0.5f);
        var far = LayerOffset(0, scale);
        var sun = new Vector2(full.Min.X + full.Width * 0.22f, full.Min.Y + full.Height * 0.16f) + far;
        var sunRadius = full.Width * 0.07f;
        for (var layer = 3; layer >= 1; layer--)
        {
            drawList.AddCircleFilled(sun, sunRadius * (1f + layer * 0.5f),
                ImGui.GetColorU32(MeadowSun with { W = 0.07f * (4 - layer) }), 36);
        }

        drawList.AddCircleFilled(sun, sunRadius, ImGui.GetColorU32(MeadowSun), 36);
        DrawCloudBanks(drawList, full, far, scale, White with { W = 0.85f }, 0.14f, 1f, 3f);
        DrawCloudBanks(drawList, full, far, scale, White with { W = 0.7f }, 0.30f, 0.8f, 5f);
        var mid = LayerOffset(1, scale);
        DrawHills(drawList, full, mid, horizon - full.Height * 0.04f, full.Height * 0.05f, 0.9f,
            Palette.Lighten(MeadowGrassFar, 0.12f));
        DrawHills(drawList, full, mid * 1.4f, horizon + full.Height * 0.03f, full.Height * 0.035f, 1.6f,
            MeadowGrassFar);
        var near = LayerOffset(2, scale);
        var blade = ImGui.GetColorU32(Palette.Darken(MeadowGrassNear, 0.25f));
        for (var index = 0; index < BladeCount; index++)
        {
            var x = full.Min.X + Wrap(BladeX[index] * full.Width + near.X, full.Width);
            var height = BladeHeight[index] * scale;
            var sway = MathF.Sin(time * 1.3f + BladePhase[index]) * 4f * scale;
            var root = new Vector2(x, full.Max.Y + 2f * scale);
            drawList.AddBezierQuadratic(root, new Vector2(x + sway * 0.4f, root.Y - height * 0.6f),
                new Vector2(x + sway, root.Y - height), blade, MathF.Max(1f, 2f * scale), 8);
        }
    }

    private void DrawHills(ImDrawListPtr drawList, Rect full, Vector2 offset, float baseY, float amplitude,
        float frequency, Vector4 color)
    {
        var fill = ImGui.GetColorU32(color);
        var step = full.Width / HillColumns;
        var previous = new Vector2(full.Min.X, HillHeight(baseY, amplitude, frequency, 0f, offset, full.Width));
        for (var column = 1; column <= HillColumns; column++)
        {
            var x = full.Min.X + column * step;
            var current = new Vector2(x, HillHeight(baseY, amplitude, frequency, column * step, offset, full.Width));
            drawList.AddQuadFilled(previous, current, new Vector2(current.X, full.Max.Y),
                new Vector2(previous.X, full.Max.Y), fill);
            previous = current;
        }
    }

    private static float HillHeight(float baseY, float amplitude, float frequency, float x, Vector2 offset,
        float width)
    {
        var phase = (x + offset.X) / width * MathF.Tau * frequency;
        return baseY + offset.Y * 0.2f - amplitude * (0.6f * MathF.Sin(phase) + 0.4f * MathF.Sin(phase * 2.3f + 1.1f));
    }

    private void DrawNeon(ImDrawListPtr drawList, Rect full, Vector4 accent, float scale)
    {
        Gradient(drawList, full, NeonTop, NeonBottom);
        var far = LayerOffset(0, scale);
        var horizon = full.Min.Y + full.Height * 0.56f + far.Y * 0.3f;
        var line = ImGui.GetColorU32(accent with { W = 0.35f });
        var faint = ImGui.GetColorU32(accent with { W = 0.12f });
        drawList.AddLine(new Vector2(full.Min.X, horizon), new Vector2(full.Max.X, horizon), line, MathF.Max(1f, 1.5f * scale));
        var depth = full.Max.Y - horizon;
        var scroll = Wrap(time * GridScrollSpeed * scale, depth / GridLines);
        for (var index = 0; index < GridLines; index++)
        {
            var linear = (index * (depth / GridLines) + scroll) / depth;
            var y = horizon + linear * linear * depth;
            drawList.AddLine(new Vector2(full.Min.X, y), new Vector2(full.Max.X, y), faint, 1f);
        }

        var vanish = new Vector2(full.Center.X + far.X, horizon);
        for (var index = 0; index <= GridLines; index++)
        {
            var bottomX = full.Min.X + (index / (float)GridLines - 0.5f) * full.Width * 2.4f + full.Width * 0.5f;
            drawList.AddLine(vanish, new Vector2(bottomX, full.Max.Y), faint, 1f);
        }

        var mid = LayerOffset(1, scale);
        for (var band = 0; band < 2; band++)
        {
            var y = full.Min.Y + full.Height * (0.22f + band * 0.26f) + mid.Y + MathF.Sin(time * 0.3f + band * 2f) * 8f * scale;
            var height = full.Height * 0.09f;
            var lit = ImGui.GetColorU32(accent with { W = 0.08f });
            var clear = ImGui.GetColorU32(accent with { W = 0f });
            drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, y - height), new Vector2(full.Max.X, y), clear,
                clear, lit, lit);
            drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, y), new Vector2(full.Max.X, y + height), lit, lit,
                clear, clear);
        }

        var near = LayerOffset(2, scale);
        var spark = Palette.Lighten(accent, 0.45f);
        for (var index = 0; index < SparkCount; index++)
        {
            var x = full.Min.X + Wrap(SparkX[index] * full.Width + near.X, full.Width);
            var y = full.Min.Y + Wrap(SparkY[index] * full.Height - time * 12f * scale + near.Y, full.Height);
            var pulse = 0.4f + 0.6f * MathF.Sin(time * 3f + SparkPhase[index]);
            drawList.AddCircleFilled(new Vector2(x, y), 1f * scale, ImGui.GetColorU32(spark with { W = pulse }), 6);
        }
    }

    private void DrawCavern(ImDrawListPtr drawList, Rect full, Vector4 accent, float scale)
    {
        Gradient(drawList, full, CavernTop, CavernBottom);
        var far = LayerOffset(0, scale);
        var facet = ImGui.GetColorU32(accent with { W = 0.06f });
        for (var index = 0; index < FacetCount; index++)
        {
            var center = new Vector2(full.Min.X + Wrap(FacetX[index] * full.Width + far.X, full.Width),
                full.Min.Y + Wrap(FacetY[index] * full.Height + far.Y, full.Height));
            var size = FacetSize[index] * scale;
            var angle = FacetAngle[index];
            var first = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size;
            var second = center + new Vector2(MathF.Cos(angle + 2.2f), MathF.Sin(angle + 2.2f)) * size * 0.7f;
            var third = center + new Vector2(MathF.Cos(angle + 4.1f), MathF.Sin(angle + 4.1f)) * size * 0.85f;
            drawList.AddTriangleFilled(first, second, third, facet);
        }

        var mid = LayerOffset(1, scale);
        var rock = ImGui.GetColorU32(Palette.Darken(CavernTop, 0.45f));
        for (var index = 0; index < StalactiteX.Length; index++)
        {
            var x = full.Min.X + StalactiteX[index] * full.Width + mid.X;
            var halfWidth = StalactiteWidth[index] * full.Width * 0.5f;
            var tip = new Vector2(x, full.Min.Y + StalactiteDepth[index] * full.Height + mid.Y * 0.5f);
            drawList.AddTriangleFilled(new Vector2(x - halfWidth, full.Min.Y - 2f), new Vector2(x + halfWidth, full.Min.Y - 2f),
                tip, rock);
        }

        DrawDust(drawList, full, LayerOffset(2, scale), scale, Palette.Lighten(accent, 0.3f), 0.12f);
    }

    private void DrawSlate(ImDrawListPtr drawList, Rect full, Vector4 accent, float scale)
    {
        Gradient(drawList, full, SlateTop, SlateBottom);
        ground = Vector4.Lerp(SlateTop, SlateBottom, 0.5f);
        var back = LayerOffset(0, scale);
        var pitch = SlateGridPitch * scale;
        var line = ImGui.GetColorU32(White with { W = SlateGridAlpha });
        var thickness = MathF.Max(1f, scale);
        for (var x = full.Min.X + Wrap(back.X, pitch); x < full.Max.X; x += pitch)
        {
            drawList.AddLine(new Vector2(x, full.Min.Y), new Vector2(x, full.Max.Y), line, thickness);
        }

        for (var y = full.Min.Y + Wrap(back.Y, pitch); y < full.Max.Y; y += pitch)
        {
            drawList.AddLine(new Vector2(full.Min.X, y), new Vector2(full.Max.X, y), line, thickness);
        }

        var mid = LayerOffset(1, scale);
        var soft = ImGui.GetColorU32(accent with { W = SlateGlowAlpha });
        var first = new Vector2(full.Min.X + full.Width * 0.22f, full.Min.Y + full.Height * 0.26f) + mid +
                    new Vector2(MathF.Sin(time * 0.25f) * 10f * scale, MathF.Cos(time * 0.2f) * 8f * scale);
        var second = new Vector2(full.Min.X + full.Width * 0.78f, full.Min.Y + full.Height * 0.72f) + mid +
                     new Vector2(MathF.Cos(time * 0.22f) * 9f * scale, MathF.Sin(time * 0.18f) * 7f * scale);
        drawList.AddCircleFilled(first, full.Width * 0.34f, soft, 48);
        drawList.AddCircleFilled(second, full.Width * 0.40f, soft, 48);
    }

    private void DrawVignette(ImDrawListPtr drawList, Rect full)
    {
        var strength = VignetteAlpha;
        var corner = CornerAlpha;
        var clear = ImGui.GetColorU32(Black with { W = 0f });
        var bottom = ImGui.GetColorU32(Black with { W = strength });
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, full.Max.Y - full.Height * VignetteFraction), full.Max,
            clear, clear, bottom, bottom);
        var lit = ImGui.GetColorU32(Black with { W = corner });
        var cornerWidth = full.Width * CornerFraction;
        var cornerHeight = full.Height * CornerFraction * 0.7f;
        drawList.AddRectFilledMultiColor(full.Min, new Vector2(full.Min.X + cornerWidth, full.Min.Y + cornerHeight), lit,
            clear, clear, clear);
        drawList.AddRectFilledMultiColor(new Vector2(full.Max.X - cornerWidth, full.Min.Y),
            new Vector2(full.Max.X, full.Min.Y + cornerHeight), clear, lit, clear, clear);
    }

    private void DrawSweep(ImDrawListPtr drawList, Rect full, float scale)
    {
        if (sweepProgress >= 1f || sweepStrength <= 0f)
        {
            return;
        }

        var width = SweepWidth * scale;
        var travel = full.Width + width * 2f + full.Height * 0.5f;
        var x = full.Min.X - width - full.Height * 0.5f + travel * Easing.SmoothStep(sweepProgress);
        var fade = MathF.Sin(sweepProgress * MathF.PI);
        var lit = ImGui.GetColorU32(White with { W = SweepAlpha * sweepStrength * fade });
        var skew = full.Height * 0.35f;
        drawList.AddQuadFilled(new Vector2(x + skew, full.Min.Y), new Vector2(x + skew + width, full.Min.Y),
            new Vector2(x + width, full.Max.Y), new Vector2(x, full.Max.Y), lit);
    }

    private static float Hash(float value)
    {
        var scaled = MathF.Sin(value * 12.9898f + 78.233f) * 43758.5453f;
        return scaled - MathF.Floor(scaled);
    }
}
