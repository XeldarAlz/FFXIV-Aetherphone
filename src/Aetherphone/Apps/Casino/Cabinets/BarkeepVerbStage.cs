using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class BarkeepVerbStage
{
    public const float PourFillPerSecond = 0.45f;
    public const float ShakeBeatSeconds = 0.8f;
    public const int ShakeBeatCount = 4;
    public const float ShakeLeadInSeconds = 0.8f;
    public const float ShakeGraceSeconds = 0.5f;
    public const int LayerCount = 3;
    public const float LayerSwingSeconds = 1.6f;
    public const float GarnishSweepSeconds = 1.3f;

    private const float JoltDecay = 5f;
    private const float BottlePourTilt = 0.55f;
    private const float BottleRestTilt = -0.35f;
    private const int LiquidSlices = 12;

    private static readonly Vector4 Gold = BarkeepArt.PerfectGold;
    private static readonly Vector4 PourAmber = new(0.93f, 0.56f, 0.18f, 1f);
    private static readonly Vector4 BottleGlass = new(0.36f, 0.62f, 0.40f, 0.85f);
    private static readonly Vector4 GlassTint = new(0.80f, 0.88f, 0.95f, 0.10f);
    private static readonly Vector4 SteelLight = new(0.86f, 0.88f, 0.92f, 1f);
    private static readonly Vector4 SteelDark = new(0.46f, 0.48f, 0.54f, 1f);
    private static readonly Vector4 CherryRed = new(0.82f, 0.10f, 0.18f, 1f);
    private static readonly Vector4 CherryStem = new(0.30f, 0.52f, 0.20f, 1f);

    private static readonly Vector4[] LayerColors =
    {
        new(0.80f, 0.12f, 0.22f, 1f),
        new(0.98f, 0.55f, 0.12f, 1f),
        new(0.98f, 0.86f, 0.45f, 1f),
    };

    private readonly int[] tapGrades = new int[ShakeBeatCount];
    private readonly Ribbon stream = new();

    private int kind = -1;
    private float seconds;
    private bool active;
    private int pendingGrade = -1;
    private int pendingTapGrade = -1;

    private float pourFill;
    private float pourTarget;
    private bool pourStarted;

    private int shakeTaps;
    private float shakeErrorSum;

    private int layerTaps;
    private float layerErrorSum;

    private float garnishTarget;

    private bool pouring;
    private float jolt;
    private int drawnShakeTaps;
    private Vector2 garnishFrom;
    private Vector2 garnishTo;

    public bool Active => active;

    public int Kind => kind;

    public void Begin(int stepKind, Random random)
    {
        kind = stepKind;
        seconds = 0f;
        active = true;
        pendingGrade = -1;
        pendingTapGrade = -1;
        for (var tapIndex = 0; tapIndex < tapGrades.Length; tapIndex++)
        {
            tapGrades[tapIndex] = -1;
        }

        pourFill = 0f;
        pourStarted = false;
        pouring = false;
        jolt = 0f;
        drawnShakeTaps = 0;
        stream.Clear();
        pourTarget = 0.55f + (float)random.NextDouble() * 0.30f;
        shakeTaps = 0;
        shakeErrorSum = 0f;
        layerTaps = 0;
        layerErrorSum = 0f;
        garnishTarget = 0.30f + (float)random.NextDouble() * 0.40f;
    }

    public void Cancel()
    {
        active = false;
        pendingGrade = -1;
        pendingTapGrade = -1;
    }

    public void Update(float deltaSeconds, bool held, bool tapped)
    {
        if (!active || pendingGrade >= 0)
        {
            return;
        }

        seconds += deltaSeconds;
        switch (kind)
        {
            case BarkeepRules.PourKind:
                UpdatePour(deltaSeconds, held);
                break;
            case BarkeepRules.ShakeKind:
                UpdateShake(tapped);
                break;
            case BarkeepRules.LayerKind:
                UpdateLayer(tapped);
                break;
            default:
                UpdateGarnish(tapped);
                break;
        }
    }

    public bool TryTakeGrade(out int grade)
    {
        if (pendingGrade < 0)
        {
            grade = -1;
            return false;
        }

        grade = pendingGrade;
        pendingGrade = -1;
        active = false;
        return true;
    }

    public bool TryTakeTapGrade(out int grade)
    {
        if (pendingTapGrade < 0)
        {
            grade = -1;
            return false;
        }

        grade = pendingTapGrade;
        pendingTapGrade = -1;
        return true;
    }

    private void UpdatePour(float deltaSeconds, bool held)
    {
        pouring = held;
        if (held)
        {
            pourStarted = true;
            pourFill += deltaSeconds * PourFillPerSecond;
            if (pourFill >= 1f)
            {
                pourFill = 1f;
                pendingGrade = BarkeepGrading.GradePour(pourFill, pourTarget);
            }

            return;
        }

        if (pourStarted)
        {
            pendingGrade = BarkeepGrading.GradePour(pourFill, pourTarget);
        }
    }

    private void UpdateShake(bool tapped)
    {
        if (tapped && shakeTaps < ShakeBeatCount)
        {
            var nearestBeat = (int)MathF.Round((seconds - ShakeLeadInSeconds) / ShakeBeatSeconds);
            nearestBeat = Math.Clamp(nearestBeat, 0, ShakeBeatCount - 1);
            var beatTime = ShakeLeadInSeconds + nearestBeat * ShakeBeatSeconds;
            var error = MathF.Min(MathF.Abs(seconds - beatTime), BarkeepGrading.ShakeFullScaleSeconds);
            shakeErrorSum += error;
            tapGrades[shakeTaps] = BarkeepGrading.GradeShake(error);
            shakeTaps++;
            if (shakeTaps == ShakeBeatCount)
            {
                pendingGrade = BarkeepGrading.GradeShake(shakeErrorSum / ShakeBeatCount);
            }
            else
            {
                pendingTapGrade = tapGrades[shakeTaps - 1];
            }

            return;
        }

        var lastBeat = ShakeLeadInSeconds + (ShakeBeatCount - 1) * ShakeBeatSeconds;
        if (seconds <= lastBeat + ShakeGraceSeconds || shakeTaps >= ShakeBeatCount)
        {
            return;
        }

        shakeErrorSum += (ShakeBeatCount - shakeTaps) * BarkeepGrading.ShakeFullScaleSeconds;
        for (var tapIndex = shakeTaps; tapIndex < ShakeBeatCount; tapIndex++)
        {
            tapGrades[tapIndex] = BarkeepGrading.MissGrade;
        }

        shakeTaps = ShakeBeatCount;
        pendingGrade = BarkeepGrading.GradeShake(shakeErrorSum / ShakeBeatCount);
    }

    private void UpdateLayer(bool tapped)
    {
        if (!tapped || layerTaps >= LayerCount)
        {
            return;
        }

        layerErrorSum += MathF.Abs(LayerLevel);
        tapGrades[layerTaps] = BarkeepGrading.GradeLayer(MathF.Abs(LayerLevel));
        layerTaps++;
        if (layerTaps == LayerCount)
        {
            pendingGrade = BarkeepGrading.GradeLayer(layerErrorSum / LayerCount);
        }
        else
        {
            pendingTapGrade = tapGrades[layerTaps - 1];
        }
    }

    private void UpdateGarnish(bool tapped)
    {
        if (!tapped)
        {
            return;
        }

        pendingGrade = BarkeepGrading.GradeGarnish(MathF.Abs(GarnishMarker - garnishTarget));
    }

    public float PourFill => pourFill;

    public float PourTarget => pourTarget;

    public int ShakeTaps => shakeTaps;

    public float ShakeBeatPulse
    {
        get
        {
            var phase = (seconds - ShakeLeadInSeconds) / ShakeBeatSeconds;
            var fraction = phase - MathF.Floor(phase);
            return 1f - fraction;
        }
    }

    public float LayerLevel => MathF.Sin(seconds / LayerSwingSeconds * MathF.PI * 2f);

    public int LayerTaps => layerTaps;

    public float GarnishMarker => 0.5f + 0.5f * MathF.Sin(seconds / GarnishSweepSeconds * MathF.PI * 2f);

    public float GarnishTarget => garnishTarget;

    public Vector2 GarnishFrom => garnishFrom;

    public Vector2 GarnishTo => garnishTo;

    public bool TakeShakeTap()
    {
        if (drawnShakeTaps == shakeTaps)
        {
            return false;
        }

        drawnShakeTaps = shakeTaps;
        return kind == BarkeepRules.ShakeKind && shakeTaps > 0;
    }

    public void Draw(ImDrawListPtr drawList, AppSkin ui, Rect canvas, float deltaSeconds, float scale)
    {
        if (!active)
        {
            return;
        }

        jolt = MathF.Max(0f, jolt - deltaSeconds * JoltDecay);
        switch (kind)
        {
            case BarkeepRules.PourKind:
                DrawPour(drawList, ui, canvas, scale);
                break;
            case BarkeepRules.ShakeKind:
                DrawShake(drawList, ui, canvas, scale);
                break;
            case BarkeepRules.LayerKind:
                DrawLayer(drawList, ui, canvas, scale);
                break;
            default:
                DrawGarnish(drawList, ui, canvas, scale);
                break;
        }
    }

    public void Jolt()
    {
        jolt = 1f;
    }

    private void DrawPour(ImDrawListPtr drawList, AppSkin ui, Rect canvas, float scale)
    {
        var glass = GlassRect(canvas, 0.3f, 84f, 0.22f, 0.06f, scale);
        var glassHeight = glass.Height;
        var goodHalf = BarkeepGrading.GoodWithin * BarkeepGrading.PourFullScale;
        var perfectHalf = BarkeepGrading.PerfectWithin * BarkeepGrading.PourFullScale;
        var inPerfectBand = MathF.Abs(pourFill - pourTarget) <= perfectHalf;
        var overGoodBand = pourFill > pourTarget + goodHalf;
        DrawGlassBack(drawList, glass, scale);
        var bandTop = glass.Max.Y - (pourTarget + goodHalf) * glassHeight;
        var bandBottom = glass.Max.Y - (pourTarget - goodHalf) * glassHeight;
        drawList.AddRectFilled(new Vector2(glass.Min.X, bandTop), new Vector2(glass.Max.X, bandBottom),
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.12f)));
        var perfectTop = glass.Max.Y - (pourTarget + perfectHalf) * glassHeight;
        var perfectBottom = glass.Max.Y - (pourTarget - perfectHalf) * glassHeight;
        drawList.AddRectFilled(new Vector2(glass.Min.X, perfectTop), new Vector2(glass.Max.X, perfectBottom),
            ImGui.GetColorU32(Gold with { W = 0.2f }));
        DrawBandTicks(drawList, glass, perfectTop, perfectBottom, scale);
        var liquid = overGoodBand ? Palette.Mix(PourAmber, BarkeepArt.MissRed, 0.45f) : PourAmber;
        var surface = glass.Max.Y - pourFill * glassHeight;
        var wave = (pouring ? 2.6f : 1.2f) * scale;
        if (pourFill > 0f)
        {
            DrawLiquid(drawList, glass, surface, glass.Max.Y, liquid, wave, scale);
        }

        DrawBottleAndStream(drawList, glass, surface, liquid, scale);
        DrawGlassFront(drawList, glass, inPerfectBand ? Gold with { W = 0.9f } : Palette.WithAlpha(ui.TitleInk, 0.4f),
            inPerfectBand ? 2f : 1.4f, scale);
    }

    private void DrawBottleAndStream(ImDrawListPtr drawList, Rect glass, float surface, Vector4 liquid, float scale)
    {
        var tilt = pouring ? BottlePourTilt : BottleRestTilt;
        var mouth = new Vector2(glass.Center.X - glass.Width * 0.1f, glass.Min.Y - 14f * scale);
        var firstVertex = drawList.VtxBuffer.Size;
        var bodyLength = 46f * scale;
        var bodyWidth = 18f * scale;
        var neckLength = 16f * scale;
        var neckWidth = 7f * scale;
        var bottleLeft = mouth.X - neckLength - bodyLength;
        drawList.AddRectFilled(new Vector2(bottleLeft, mouth.Y - bodyWidth * 0.5f),
            new Vector2(mouth.X - neckLength, mouth.Y + bodyWidth * 0.5f), ImGui.GetColorU32(BottleGlass), 5f * scale);
        drawList.AddRectFilled(new Vector2(bottleLeft + bodyLength * 0.2f, mouth.Y - bodyWidth * 0.5f + 2f * scale),
            new Vector2(mouth.X - neckLength, mouth.Y + bodyWidth * 0.5f), ImGui.GetColorU32(liquid with { W = 0.8f }),
            5f * scale);
        drawList.AddRectFilled(new Vector2(mouth.X - neckLength, mouth.Y - neckWidth * 0.5f),
            new Vector2(mouth.X, mouth.Y + neckWidth * 0.5f), ImGui.GetColorU32(BottleGlass));
        drawList.AddLine(new Vector2(bottleLeft + 6f * scale, mouth.Y - bodyWidth * 0.28f),
            new Vector2(mouth.X - neckLength - 6f * scale, mouth.Y - bodyWidth * 0.28f),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f)), MathF.Max(1f, 2f * scale));
        Rotate(drawList, firstVertex, mouth, tilt);
        if (!pouring)
        {
            stream.Clear();
            return;
        }

        stream.Clear();
        var top = mouth + new Vector2(0f, 2f * scale);
        var bottom = new Vector2(top.X, MathF.Max(top.Y + 4f * scale, surface));
        for (var point = Ribbon.Capacity - 1; point >= 0; point--)
        {
            var along = point / (float)(Ribbon.Capacity - 1);
            var wobble = MathF.Sin(seconds * 26f + along * 9f) * 1.1f * scale * along;
            stream.Push(Vector2.Lerp(top, bottom, along) + new Vector2(wobble, 0f));
        }

        stream.Draw(drawList, liquid, 5f * scale, true);
        var splash = 0.5f + 0.5f * MathF.Sin(seconds * 31f);
        drawList.AddCircleFilled(new Vector2(bottom.X - 4f * scale, bottom.Y - 2f * scale - splash * 3f * scale),
            1.6f * scale, ImGui.GetColorU32(liquid with { W = 0.8f }), 8);
        drawList.AddCircleFilled(new Vector2(bottom.X + 5f * scale, bottom.Y - 1f * scale - (1f - splash) * 3f * scale),
            1.3f * scale, ImGui.GetColorU32(liquid with { W = 0.8f }), 8);
    }

    private void DrawShake(ImDrawListPtr drawList, AppSkin ui, Rect canvas, float scale)
    {
        var center = canvas.Center;
        var targetRadius = MathF.Min(canvas.Height, canvas.Width) * 0.24f;
        var running = seconds >= ShakeLeadInSeconds - ShakeBeatSeconds && shakeTaps < ShakeBeatCount;
        var phase = (seconds - ShakeLeadInSeconds) / ShakeBeatSeconds;
        var fraction = phase - MathF.Floor(phase);
        var toBeatSeconds = MathF.Min(fraction, 1f - fraction) * ShakeBeatSeconds;
        var inPerfectWindow = running &&
            toBeatSeconds <= BarkeepGrading.PerfectWithin * BarkeepGrading.ShakeFullScaleSeconds;
        var targetTint = inPerfectWindow ? Gold with { W = 0.9f } : Palette.WithAlpha(ui.TitleInk, 0.35f);
        drawList.AddCircle(center, targetRadius, ImGui.GetColorU32(targetTint), 40,
            MathF.Max(1f, (inPerfectWindow ? 2.6f : 2f) * scale));
        if (running)
        {
            var pulseRadius = targetRadius + targetRadius * 1.2f * ShakeBeatPulse;
            var pulseTint = inPerfectWindow ? Gold with { W = 0.85f } : Palette.WithAlpha(ui.Accent, 0.55f);
            drawList.AddCircle(center, pulseRadius, ImGui.GetColorU32(pulseTint), 48, MathF.Max(1f, 2.4f * scale));
        }

        var sway = running ? MathF.Sin(phase * MathF.PI * 2f) * 0.18f : 0f;
        var lift = jolt * 10f * scale;
        var tinCenter = center + new Vector2(MathF.Sin(seconds * 41f) * jolt * 4f * scale, -lift);
        DrawShaker(drawList, tinCenter, targetRadius * 0.9f, sway + jolt * 0.22f, scale);
        var pipY = canvas.Max.Y - 10f * scale;
        for (var beatIndex = 0; beatIndex < ShakeBeatCount; beatIndex++)
        {
            var pipX = center.X + (beatIndex - (ShakeBeatCount - 1) * 0.5f) * 18f * scale;
            var done = beatIndex < shakeTaps;
            var pipTint = done
                ? BarkeepArt.GradeTint(tapGrades[beatIndex], ui.Accent)
                : Palette.WithAlpha(ui.MutedInk, 0.4f);
            drawList.AddCircleFilled(new Vector2(pipX, pipY), 4f * scale, ImGui.GetColorU32(pipTint), 12);
        }
    }

    private static void DrawShaker(ImDrawListPtr drawList, Vector2 center, float extent, float tilt, float scale)
    {
        var firstVertex = drawList.VtxBuffer.Size;
        var halfWidth = extent * 0.36f;
        var bodyTop = center.Y - extent * 0.3f;
        var bodyBottom = center.Y + extent * 0.8f;
        drawList.AddRectFilledMultiColor(new Vector2(center.X - halfWidth, bodyTop), new Vector2(center.X + halfWidth, bodyBottom),
            ImGui.GetColorU32(SteelLight), ImGui.GetColorU32(SteelDark), ImGui.GetColorU32(SteelDark),
            ImGui.GetColorU32(SteelLight));
        drawList.AddRectFilled(new Vector2(center.X - halfWidth * 0.82f, center.Y - extent * 0.62f),
            new Vector2(center.X + halfWidth * 0.82f, bodyTop + 2f * scale), ImGui.GetColorU32(SteelDark), 4f * scale);
        drawList.AddRectFilled(new Vector2(center.X - halfWidth * 0.36f, center.Y - extent * 0.86f),
            new Vector2(center.X + halfWidth * 0.36f, center.Y - extent * 0.6f), ImGui.GetColorU32(SteelLight), 3f * scale);
        drawList.AddLine(new Vector2(center.X - halfWidth * 0.5f, bodyTop + extent * 0.12f),
            new Vector2(center.X - halfWidth * 0.5f, bodyBottom - extent * 0.1f),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.55f)), MathF.Max(1f, 2.4f * scale));
        drawList.AddLine(new Vector2(center.X - halfWidth, bodyTop), new Vector2(center.X + halfWidth, bodyTop),
            ImGui.GetColorU32(SteelDark with { W = 0.9f }), MathF.Max(1f, 2f * scale));
        Rotate(drawList, firstVertex, center, tilt);
    }

    private void DrawLayer(ImDrawListPtr drawList, AppSkin ui, Rect canvas, float scale)
    {
        var glass = GlassRect(canvas, 0.42f, 120f, 0.12f, 0.08f, scale);
        DrawGlassBack(drawList, glass, scale);
        var layerHeight = glass.Height / (LayerCount + 1);
        for (var layerIndex = 0; layerIndex < layerTaps; layerIndex++)
        {
            var bottom = glass.Max.Y - layerIndex * layerHeight;
            var top = bottom - layerHeight;
            var color = LayerColors[layerIndex % LayerColors.Length];
            DrawLiquid(drawList, glass, top, bottom, color, 0.8f * scale, scale);
            var marker = new Vector2(glass.Max.X + 7f * scale, (top + bottom) * 0.5f);
            drawList.AddCircleFilled(marker, 3.2f * scale,
                ImGui.GetColorU32(BarkeepArt.GradeTint(tapGrades[layerIndex], ui.Accent)), 10);
        }

        var settleY = glass.Max.Y - (layerTaps + 1) * layerHeight;
        DrawDashed(drawList, new Vector2(glass.Min.X - 10f * scale, settleY), glass.Width + 20f * scale,
            ImGui.GetColorU32(Gold with { W = 0.55f }), scale);
        var level = MathF.Abs(LayerLevel);
        var levelPerfect = level <= BarkeepGrading.PerfectWithin * BarkeepGrading.LayerFullScale;
        var swingY = settleY - LayerLevel * layerHeight * 0.9f;
        var next = LayerColors[layerTaps % LayerColors.Length];
        var swingTint = levelPerfect ? Gold : next;
        drawList.AddLine(new Vector2(glass.Min.X + 2f * scale, swingY), new Vector2(glass.Max.X - 2f * scale, swingY),
            ImGui.GetColorU32(swingTint), MathF.Max(1f, (levelPerfect ? 3.4f : 2.6f) * scale));
        var spoon = new Vector2(glass.Max.X - 4f * scale, swingY);
        drawList.AddLine(spoon, spoon + new Vector2(18f * scale, -glass.Height * 0.55f),
            ImGui.GetColorU32(SteelLight), MathF.Max(1f, 1.6f * scale));
        Shapes.FillEllipse(drawList, spoon, 6f * scale, 2.4f * scale, ImGui.GetColorU32(SteelLight));
        DrawGlassFront(drawList, glass, Palette.WithAlpha(ui.TitleInk, 0.4f), 1.4f, scale);
    }

    private void DrawGarnish(ImDrawListPtr drawList, AppSkin ui, Rect canvas, float scale)
    {
        var barY = canvas.Min.Y + canvas.Height * 0.24f;
        var barLeft = canvas.Min.X + canvas.Width * 0.12f;
        var barRight = canvas.Max.X - canvas.Width * 0.12f;
        var barWidth = barRight - barLeft;
        drawList.AddLine(new Vector2(barLeft, barY), new Vector2(barRight, barY),
            ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.5f)), MathF.Max(1f, 3f * scale));
        var goodHalf = BarkeepGrading.GoodWithin * BarkeepGrading.GarnishFullScale;
        var perfectHalf = BarkeepGrading.PerfectWithin * BarkeepGrading.GarnishFullScale;
        var targetX = barLeft + garnishTarget * barWidth;
        drawList.AddRectFilled(new Vector2(targetX - goodHalf * barWidth, barY - 8f * scale),
            new Vector2(targetX + goodHalf * barWidth, barY + 8f * scale),
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.2f)), 4f * scale);
        drawList.AddRectFilled(new Vector2(targetX - perfectHalf * barWidth, barY - 10f * scale),
            new Vector2(targetX + perfectHalf * barWidth, barY + 10f * scale),
            ImGui.GetColorU32(Gold with { W = 0.32f }), 4f * scale);
        var bowlTop = canvas.Min.Y + canvas.Height * 0.52f;
        var bowlHalf = MathF.Min(canvas.Width * 0.18f, 54f * scale);
        var glassCenterX = targetX;
        DrawCocktailGlass(drawList, new Vector2(glassCenterX, bowlTop), bowlHalf, canvas.Max.Y - 6f * scale, scale);
        var markerX = barLeft + GarnishMarker * barWidth;
        var markerPerfect = MathF.Abs(GarnishMarker - garnishTarget) <= perfectHalf;
        var marker = new Vector2(markerX, barY);
        if (markerPerfect)
        {
            drawList.AddCircle(marker, 13f * scale, ImGui.GetColorU32(Gold with { W = 0.6f }), 24,
                MathF.Max(1f, 1.8f * scale));
        }

        DrawCherry(drawList, marker, 9f * scale, scale);
        garnishFrom = marker;
        garnishTo = new Vector2(glassCenterX + bowlHalf * 0.45f, bowlTop - 2f * scale);
    }

    public static void DrawCherry(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        drawList.AddLine(center, center + new Vector2(radius * 0.7f, -radius * 1.6f),
            ImGui.GetColorU32(CherryStem), MathF.Max(1f, 1.6f * scale));
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(CherryRed), 18);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.35f, -radius * 0.35f), radius * 0.28f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.55f)), 10);
    }

    private static void DrawCocktailGlass(ImDrawListPtr drawList, Vector2 bowlTopCenter, float half, float baseY,
        float scale)
    {
        var bowlDepth = half * 0.95f;
        var tip = new Vector2(bowlTopCenter.X, bowlTopCenter.Y + bowlDepth);
        var left = new Vector2(bowlTopCenter.X - half, bowlTopCenter.Y);
        var right = new Vector2(bowlTopCenter.X + half, bowlTopCenter.Y);
        drawList.AddTriangleFilled(left + new Vector2(half * 0.12f, half * 0.1f), right + new Vector2(-half * 0.12f, half * 0.1f),
            tip - new Vector2(0f, bowlDepth * 0.08f), ImGui.GetColorU32(LayerColors[0] with { W = 0.85f }));
        var glassInk = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.55f));
        drawList.AddTriangle(left, right, tip, glassInk, MathF.Max(1f, 1.4f * scale));
        drawList.AddLine(tip, new Vector2(tip.X, baseY), glassInk, MathF.Max(1f, 1.6f * scale));
        drawList.AddLine(new Vector2(tip.X - half * 0.5f, baseY), new Vector2(tip.X + half * 0.5f, baseY), glassInk,
            MathF.Max(1f, 2f * scale));
    }

    private static Rect GlassRect(Rect canvas, float widthShare, float maxWidth, float topShare, float bottomShare,
        float scale)
    {
        var width = MathF.Min(canvas.Width * widthShare, maxWidth * scale);
        var top = canvas.Min.Y + canvas.Height * topShare;
        var bottom = canvas.Max.Y - canvas.Height * bottomShare;
        return new Rect(new Vector2(canvas.Center.X - width * 0.5f, top), new Vector2(canvas.Center.X + width * 0.5f, bottom));
    }

    private static void DrawGlassBack(ImDrawListPtr drawList, Rect glass, float scale)
    {
        Squircle.Fill(drawList, glass.Min, glass.Max, 8f * scale, ImGui.GetColorU32(GlassTint));
    }

    private static void DrawGlassFront(ImDrawListPtr drawList, Rect glass, Vector4 edge, float thickness, float scale)
    {
        drawList.AddLine(new Vector2(glass.Min.X + glass.Width * 0.18f, glass.Min.Y + glass.Height * 0.08f),
            new Vector2(glass.Min.X + glass.Width * 0.18f, glass.Max.Y - glass.Height * 0.12f),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f)), MathF.Max(1f, 3f * scale));
        Squircle.Stroke(drawList, glass.Min, glass.Max, 8f * scale, ImGui.GetColorU32(edge), MathF.Max(1f, thickness * scale));
    }

    private static void DrawBandTicks(ImDrawListPtr drawList, Rect glass, float top, float bottom, float scale)
    {
        var tick = 6f * scale;
        var ink = ImGui.GetColorU32(Gold);
        drawList.AddLine(new Vector2(glass.Max.X, top), new Vector2(glass.Max.X + tick, top), ink, MathF.Max(1f, 2f * scale));
        drawList.AddLine(new Vector2(glass.Max.X, bottom), new Vector2(glass.Max.X + tick, bottom), ink,
            MathF.Max(1f, 2f * scale));
        drawList.AddLine(new Vector2(glass.Max.X + tick, top), new Vector2(glass.Max.X + tick, bottom), ink,
            MathF.Max(1f, 2f * scale));
    }

    private void DrawLiquid(ImDrawListPtr drawList, Rect glass, float surface, float bottom, Vector4 color, float wave,
        float scale)
    {
        var inset = 2f * scale;
        var left = glass.Min.X + inset;
        var right = glass.Max.X - inset;
        var floor = bottom - inset;
        if (surface >= floor)
        {
            return;
        }

        var fill = ImGui.GetColorU32(color with { W = 0.82f });
        var flat = surface + wave;
        if (flat < floor)
        {
            drawList.AddRectFilled(new Vector2(left, flat), new Vector2(right, floor), fill, 4f * scale,
                ImDrawFlags.RoundCornersBottom);
        }

        var slice = (right - left) / LiquidSlices;
        for (var sliceIndex = 0; sliceIndex < LiquidSlices; sliceIndex++)
        {
            var x = left + slice * sliceIndex;
            var crest = surface + wave * (1f - MathF.Sin(seconds * 6f + sliceIndex * 0.9f));
            drawList.AddRectFilled(new Vector2(x, MathF.Min(crest, flat)), new Vector2(x + slice + 0.5f, flat), fill);
        }

        drawList.AddLine(new Vector2(left, surface + wave), new Vector2(right, surface + wave),
            ImGui.GetColorU32(Vector4.Lerp(color, Vector4.One, 0.45f) with { W = 0.7f }), MathF.Max(1f, scale));
    }

    private static void DrawDashed(ImDrawListPtr drawList, Vector2 start, float length, uint ink, float scale)
    {
        var dash = 6f * scale;
        for (var x = 0f; x < length; x += dash * 2f)
        {
            drawList.AddLine(new Vector2(start.X + x, start.Y), new Vector2(start.X + MathF.Min(length, x + dash), start.Y),
                ink, MathF.Max(1f, 1.6f * scale));
        }
    }

    private static void Rotate(ImDrawListPtr drawList, int firstVertex, Vector2 pivot, float angle)
    {
        var sine = MathF.Sin(angle);
        var cosine = MathF.Cos(angle);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var vertexIndex = firstVertex; vertexIndex < vertices.Length; vertexIndex++)
        {
            ref var vertex = ref vertices[vertexIndex];
            var offset = vertex.Pos - pivot;
            vertex.Pos = new Vector2(pivot.X + offset.X * cosine - offset.Y * sine,
                pivot.Y + offset.X * sine + offset.Y * cosine);
        }
    }
}
