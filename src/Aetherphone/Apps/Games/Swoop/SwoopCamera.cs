using Aetherphone.Core;
using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Games.Swoop;

internal struct SwoopCamera
{
    public const float WorldPixelsPerMeter = 10f;
    public const float ViewWidthMeters = 34f;
    public const float MaxZoom = 2.8f;
    private const float AnchorXFraction = 0.4f;
    private const float AnchorYFraction = 0.72f;
    private const float LeadPerSpeed = 0.3f;
    private const float MinLead = 1f;
    private const float MaxLead = 8f;
    private const float LookBehind = 8f;
    private const float LookAhead = 40f;
    private const float SkyMargin = 7f;
    private const float GroundMargin = 3f;
    private const float FrameFill = 0.9f;
    private const float LeadSmoothSeconds = 0.6f;
    private const float FloorSmoothSeconds = 0.45f;
    private const float ZoomSmoothSeconds = 0.55f;

    private Spring lead;
    private Spring floor;
    private Spring zoom;

    public double FocusX { get; private set; }
    public float FocusY { get; private set; }
    public float Zoom { get; private set; }
    public float PixelsPerMeter { get; private set; }
    public float BasePixelsPerMeter { get; private set; }
    public Vector2 Anchor { get; private set; }

    public void Reset(SwoopBoard board, Rect area)
    {
        Targets(board, area, out var leadTarget, out var floorTarget, out var zoomTarget);
        lead.SnapTo(leadTarget);
        floor.SnapTo(floorTarget);
        zoom.SnapTo(zoomTarget);
        Apply(board, area);
    }

    public void Update(SwoopBoard board, Rect area, float deltaSeconds)
    {
        Targets(board, area, out var leadTarget, out var floorTarget, out var zoomTarget);
        if (deltaSeconds > 0f)
        {
            lead.Step(leadTarget, LeadSmoothSeconds, deltaSeconds);
            floor.Step(floorTarget, FloorSmoothSeconds, deltaSeconds);
            zoom.Step(zoomTarget, ZoomSmoothSeconds, deltaSeconds);
        }

        Apply(board, area);
    }

    public void Shift(Vector2 offset)
    {
        Anchor += offset;
    }

    public readonly Vector2 ToScreen(double x, float y) =>
        new(Anchor.X + (float)(x - FocusX) * PixelsPerMeter, Anchor.Y - (y - FocusY) * PixelsPerMeter);

    public readonly double WorldX(float screenX) => FocusX + (screenX - Anchor.X) / PixelsPerMeter;

    public static Vector2 WorldPixels(double x, float y) =>
        new((float)(x * WorldPixelsPerMeter), -y * WorldPixelsPerMeter);

    public readonly Vector2 WorldPixelsToScreen(Vector2 worldPixels)
    {
        var factor = PixelsPerMeter / WorldPixelsPerMeter;
        var originX = (float)(FocusX * WorldPixelsPerMeter);
        var originY = -FocusY * WorldPixelsPerMeter;
        return new Vector2(Anchor.X + (worldPixels.X - originX) * factor, Anchor.Y + (worldPixels.Y - originY) * factor);
    }

    private void Apply(SwoopBoard board, Rect area)
    {
        Zoom = zoom.Value;
        BasePixelsPerMeter = area.Width / ViewWidthMeters;
        PixelsPerMeter = BasePixelsPerMeter / MathF.Max(1f, Zoom);
        Anchor = new Vector2(area.Min.X + area.Width * AnchorXFraction, area.Min.Y + area.Height * AnchorYFraction);
        FocusX = board.X + lead.Value;
        FocusY = floor.Value;
    }

    private static void Targets(SwoopBoard board, Rect area, out float leadTarget, out float floorTarget, out float zoomTarget)
    {
        leadTarget = Math.Clamp(board.Speed * LeadPerSpeed, MinLead, MaxLead);
        var firstSample = Math.Max(board.FirstSample, (int)Math.Floor((board.X - LookBehind) / SwoopBoard.SampleSpacing));
        var endSample = Math.Min(board.EndSample, (int)Math.Ceiling((board.X + LookAhead) / SwoopBoard.SampleSpacing));
        var lowest = board.Y;
        var highest = board.Y;
        for (var sample = firstSample; sample < endSample; sample++)
        {
            var height = board.SampleHeight(sample);
            lowest = MathF.Min(lowest, height);
            highest = MathF.Max(highest, height);
        }

        var top = MathF.Max(highest + SkyMargin, board.Y + SwoopBoard.BirdRadius * 2f + SkyMargin * 0.6f);
        var bottom = lowest - GroundMargin;
        var baseHeight = ViewWidthMeters * area.Height / MathF.Max(1f, area.Width);
        var needed = (top - bottom) / (AnchorYFraction * FrameFill);
        zoomTarget = Math.Clamp(needed / MathF.Max(1f, baseHeight), 1f, MaxZoom);
        floorTarget = bottom;
    }
}
