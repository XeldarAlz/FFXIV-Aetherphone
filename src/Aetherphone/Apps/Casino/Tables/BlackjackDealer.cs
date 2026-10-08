using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class BlackjackDealer
{
    public const float HoldSeconds = 2.6f;
    public const float FadeSeconds = 0.4f;

    private const float EntranceSmoothing = 0.09f;
    private const float RestSmoothing = 0.14f;
    private const float BubblePadX = 10f;
    private const float BubblePadY = 6f;
    private const float TailSize = 6f;
    private const float HandRadius = 7f;
    private const float FingerLength = 6f;
    private const float FingerWidth = 2.6f;
    private const float CuffWidth = 9f;
    private const float RestOffsetX = -16f;
    private const float RestOffsetY = 12f;
    private const float LeadHold = 0.85f;
    private const float RimPulseRate = 2.4f;

    private static readonly Vector4 PuckFill = new(0.06f, 0.10f, 0.09f, 1f);
    private static readonly Vector4 Glove = new(0.97f, 0.96f, 0.93f, 1f);
    private static readonly Vector4 GloveShade = new(0f, 0f, 0f, 0.25f);
    private static readonly Vector4 Cuff = new(0.12f, 0.12f, 0.16f, 1f);

    private string message = string.Empty;
    private float remaining;
    private Spring entrance = new(0f);
    private Spring handX = new(0f);
    private Spring handY = new(0f);
    private bool handPlaced;
    private Vector2 lead;
    private float leadTravel = -1f;

    public bool Speaking => message.Length > 0;

    public void Say(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (string.Equals(message, text, StringComparison.Ordinal) && remaining > 0f)
        {
            return;
        }

        message = text;
        remaining = HoldSeconds + FadeSeconds;
        entrance.SnapTo(0f);
    }

    public void Clear()
    {
        message = string.Empty;
        remaining = 0f;
        entrance.SnapTo(0f);
        handPlaced = false;
        leadTravel = -1f;
    }

    public void Update(float deltaSeconds)
    {
        leadTravel = -1f;
        if (message.Length == 0)
        {
            return;
        }

        remaining -= deltaSeconds;
        if (remaining <= 0f)
        {
            message = string.Empty;
            remaining = 0f;
            return;
        }

        entrance.Step(1f, EntranceSmoothing, deltaSeconds);
    }

    public void TrackCard(Vector2 center, float travel)
    {
        if (travel <= 0f || travel >= LeadHold || travel < leadTravel)
        {
            return;
        }

        lead = center;
        leadTravel = travel;
    }

    public static void DrawPuck(ImDrawListPtr drawList, Vector2 center, float radius, float phase, float scale)
    {
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.12f), radius * 1.04f,
            ImGui.GetColorU32(GloveShade), 32);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(PuckFill), 32);
        var rim = 0.6f + 0.2f * (1f + MathF.Sin(phase * RimPulseRate));
        drawList.AddCircle(center, radius, ImGui.GetColorU32(CasinoColors.Money with { W = rim }), 32,
            MathF.Max(1.2f, 1.6f * scale));
        PlayingCards.DrawSuit(drawList, center, radius * 0.48f, PlayingCards.Spades, CasinoColors.InkTitle);
        var bow = new Vector2(center.X, center.Y + radius * 0.72f);
        var wing = radius * 0.22f;
        drawList.AddTriangleFilled(bow, bow + new Vector2(-wing, -wing * 0.6f), bow + new Vector2(-wing, wing * 0.6f),
            ImGui.GetColorU32(CasinoColors.LightA));
        drawList.AddTriangleFilled(bow, bow + new Vector2(wing, -wing * 0.6f), bow + new Vector2(wing, wing * 0.6f),
            ImGui.GetColorU32(CasinoColors.LightA));
    }

    public void DrawHand(ImDrawListPtr drawList, Vector2 shoe, float deltaSeconds, float scale)
    {
        var rest = shoe + new Vector2(RestOffsetX * scale, RestOffsetY * scale);
        var target = leadTravel >= 0f ? lead : rest;
        if (!handPlaced)
        {
            handX.SnapTo(rest.X);
            handY.SnapTo(rest.Y);
            handPlaced = true;
        }

        var x = handX.Step(target.X, RestSmoothing, deltaSeconds);
        var y = handY.Step(target.Y, RestSmoothing, deltaSeconds);
        DrawGlove(drawList, new Vector2(x, y), shoe, scale);
    }

    public static void DrawGlove(ImDrawListPtr drawList, Vector2 center, Vector2 shoe, float scale)
    {
        var radius = HandRadius * scale;
        var toward = center - shoe;
        var length = toward.Length();
        var direction = length > 0.01f ? toward / length : new Vector2(-1f, 0f);
        var side = new Vector2(-direction.Y, direction.X);
        var cuffCenter = center - direction * radius * 1.3f;
        var cuffHalf = side * (CuffWidth * 0.5f * scale);
        var cuffBack = direction * (radius * 0.6f);
        drawList.AddQuadFilled(cuffCenter - cuffHalf - cuffBack, cuffCenter + cuffHalf - cuffBack,
            cuffCenter + cuffHalf + cuffBack, cuffCenter - cuffHalf + cuffBack, ImGui.GetColorU32(Cuff));
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.15f), radius, ImGui.GetColorU32(GloveShade), 20);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Glove), 20);
        var finger = FingerLength * scale;
        var width = FingerWidth * scale;
        var glove = ImGui.GetColorU32(Glove);
        for (var fingerIndex = 0; fingerIndex < 4; fingerIndex++)
        {
            var spread = (fingerIndex - 1.5f) * width * 0.95f;
            var start = center + direction * (radius * 0.55f) + side * spread;
            drawList.AddLine(start, start + direction * finger, glove, width);
            drawList.AddCircleFilled(start + direction * finger, width * 0.5f, glove, 8);
        }
    }

    public void DrawSpeech(ImDrawListPtr drawList, in Rect area, Vector2 puck, float puckRadius, float scale)
    {
        if (message.Length == 0 || area.Width <= 0f)
        {
            return;
        }

        var fade = remaining < FadeSeconds ? remaining / FadeSeconds : 1f;
        var alpha = fade * Math.Clamp(entrance.Value, 0f, 1f);
        if (alpha <= 0.01f)
        {
            return;
        }

        var padX = BubblePadX * scale;
        var padY = BubblePadY * scale;
        var text = Typography.FitText(message, MathF.Max(1f, area.Width - padX * 2f), TextStyles.Subheadline);
        var size = Typography.Measure(text, TextStyles.Subheadline);
        var right = puck.X - puckRadius - TailSize * scale;
        var max = new Vector2(right, puck.Y + size.Y * 0.5f + padY);
        var min = new Vector2(right - size.X - padX * 2f, puck.Y - size.Y * 0.5f - padY);
        var rounding = (max.Y - min.Y) * 0.5f;
        Material.Frosted(drawList, min, max, rounding, scale, alpha * 0.9f);
        var tail = TailSize * scale;
        drawList.AddTriangleFilled(new Vector2(max.X - 1f, puck.Y - tail * 0.7f),
            new Vector2(max.X - 1f, puck.Y + tail * 0.7f), new Vector2(max.X + tail, puck.Y),
            ImGui.GetColorU32(CasinoColors.InkTitle with { W = 0.22f * alpha }));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(CasinoColors.Money with { W = 0.35f * alpha }), Metrics.Stroke.Hairline * scale);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, text, CasinoColors.InkTitle with { W = alpha },
            TextStyles.Subheadline);
    }
}
