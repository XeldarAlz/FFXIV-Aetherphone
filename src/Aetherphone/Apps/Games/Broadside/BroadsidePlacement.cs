using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Broadside;

internal enum PlacementAction : byte
{
    None,
    Auto,
    Ready,
}

internal sealed class BroadsidePlacement
{
    private const string SurfaceId = "broadside.place";
    private const int ShipCount = BroadsideFleet.ShipCount;
    private const float DragThreshold = 6f;
    private const float PopSeconds = 0.32f;
    private const float WarnDecay = 2.2f;
    private const float PopStagger = 0.12f;
    private static readonly Vector4 AutoInk = new(0.36f, 0.56f, 0.86f, 1f);
    private static readonly ParticleSpec Dust = new(new Vector4(1f, 1f, 1f, 0.6f), new Vector4(1f, 1f, 1f, 0f), 3f,
        60f, 0.4f, 0f, 2.6f, shape: ParticleShape.GlowCircle, curve: SizeCurve.Grow);

    private readonly float[] pop = new float[ShipCount];
    private readonly float[] warn = new float[ShipCount];
    private int grab;
    private Vector2 start;
    private bool across;
    private bool moved;
    private bool wasPlaced;

    public BroadsidePlacement()
    {
        Reset();
    }

    public int DragShip { get; private set; } = -1;

    public bool Lifted(int ship) => ship == DragShip && moved;

    public float Pop(int ship) => pop[ship];

    public float Warn(int ship) => warn[ship];

    public void Reset()
    {
        Array.Fill(pop, 1f);
        Array.Clear(warn);
        DragShip = -1;
    }

    public void Release()
    {
        DragShip = -1;
    }

    public void Age(float raw)
    {
        for (var ship = 0; ship < ShipCount; ship++)
        {
            pop[ship] = MathF.Min(1f, pop[ship] + raw / PopSeconds);
            warn[ship] = MathF.Max(0f, warn[ship] - raw * WarnDecay);
        }
    }

    public void Handle(BroadsideFleet fleet, BroadsideLayout layout, Rect grid, bool chromeHit, float scale,
        ParticleSystem particles, FeedbackFx fx)
    {
        var mouse = ImGui.GetMousePos();
        var surface = new Rect(Vector2.Min(grid.Min, layout.Dock.Min), Vector2.Max(grid.Max, layout.Dock.Max));
        PressSurface.Claim(SurfaceId, surface, out var activated);
        if (DragShip < 0)
        {
            if (!activated || chromeHit)
            {
                return;
            }

            var picked = PickShip(fleet, layout, grid, mouse, out var pickedGrab, out var pickedAcross);
            if (picked < 0)
            {
                return;
            }

            DragShip = picked;
            grab = pickedGrab;
            across = pickedAcross;
            start = mouse;
            moved = false;
            wasPlaced = fleet.IsPlaced(picked);
            UiFeedback.Play(UiSound.GameTick);
            return;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (!moved && Vector2.Distance(mouse, start) > DragThreshold * scale)
            {
                moved = true;
            }

            return;
        }

        var ship = DragShip;
        DragShip = -1;
        if (!moved)
        {
            if (wasPlaced)
            {
                Rotate(fleet, grid, ship, particles, fx);
            }

            return;
        }

        Drop(fleet, grid, ship, mouse, particles, fx);
    }

    public void AutoPlace(BroadsideFleet fleet, ref GameRandom random)
    {
        fleet.AutoPlace(ref random);
        DragShip = -1;
        for (var ship = 0; ship < ShipCount; ship++)
        {
            pop[ship] = -ship * PopStagger;
        }

        UiFeedback.Play(UiSound.GameShuffle);
    }

    public void DrawGhost(ImDrawListPtr drawList, BroadsideFleet fleet, Rect grid, Vector4 accent, float clock,
        float scale)
    {
        if (DragShip < 0 || !moved)
        {
            return;
        }

        var mouse = ImGui.GetMousePos();
        var length = BroadsideFleet.Length(DragShip);
        var pitch = BroadsideLayout.Pitch(grid);
        if (grid.Contains(mouse) && GhostHead(grid, mouse, DragShip, out var column, out var row))
        {
            var valid = fleet.CanPlace(DragShip, column, row, across);
            var snapped = BroadsideLayout.ShipRect(grid, column, row, length, across);
            Squircle.Fill(drawList, snapped.Min, snapped.Max, pitch * 0.2f,
                ImGui.GetColorU32((valid ? accent : BroadsideArt.Danger) with { W = 0.22f }));
            BroadsideArt.DrawAirship(drawList, snapped, across, BroadsideArt.Hull, scale, clock, -1f, 0.9f,
                valid ? 0f : 0.8f, 3f * scale);
            return;
        }

        var offset = across ? new Vector2((grab + 0.5f) * pitch, pitch * 0.5f)
            : new Vector2(pitch * 0.5f, (grab + 0.5f) * pitch);
        var free = BroadsideLayout.ShipRect(new Rect(mouse - offset, mouse - offset + new Vector2(pitch * 10f, pitch * 10f)),
            0, 0, length, across);
        BroadsideArt.DrawAirship(drawList, free, across, BroadsideArt.Hull, scale, clock, -1f, 0.75f, 0f, 4f * scale);
    }

    public PlacementAction DrawControls(ImDrawListPtr drawList, BroadsideFleet fleet, BroadsideLayout layout,
        PhoneTheme theme, Vector4 accent, float clock, float scale, float fade, bool interactive, bool keyboard)
    {
        if (fade <= 0.01f)
        {
            return PlacementAction.None;
        }

        var hint = layout.Hint;
        Typography.DrawCentered(drawList, hint.Center,
            Typography.FitText(Loc.T(L.Broadside.PlaceHint), hint.Width, TextStyles.Caption1),
            BroadsideArt.Muted with { W = BroadsideArt.Muted.W * fade }, TextStyles.Caption1);
        DrawDock(drawList, fleet, layout, clock, scale);
        var auto = GameHud.Button(layout.AutoCenter, layout.ButtonSize, Loc.T(L.Broadside.Auto), AutoInk, theme);
        var ready = false;
        if (fleet.AllPlaced)
        {
            ProgressRing.Glow(layout.ReadyCenter, layout.ButtonSize.X * 0.55f, accent, 0.2f + 0.2f * Pulse.Wave(Pulse.Calm));
            ready = GameHud.Button(layout.ReadyCenter, layout.ButtonSize, Loc.T(L.Broadside.Ready), accent, theme);
        }
        else
        {
            var half = layout.ButtonSize * 0.5f;
            Material.Frosted(drawList, layout.ReadyCenter - half, layout.ReadyCenter + half, half.Y, scale, 0.6f);
            Typography.DrawCentered(drawList, layout.ReadyCenter, Loc.T(L.Broadside.Ready), BroadsideArt.Disabled,
                TextStyles.Headline);
        }

        if (!interactive || DragShip >= 0)
        {
            return PlacementAction.None;
        }

        if (auto)
        {
            return PlacementAction.Auto;
        }

        return ready || (keyboard && fleet.AllPlaced && GameInput.Pressed(ImGuiKey.Enter))
            ? PlacementAction.Ready
            : PlacementAction.None;
    }

    private void DrawDock(ImDrawListPtr drawList, BroadsideFleet fleet, BroadsideLayout layout, float clock,
        float scale)
    {
        var pitch = layout.DockPitch;
        for (var ship = 0; ship < ShipCount; ship++)
        {
            var slot = layout.DockSlot(ship);
            Squircle.Stroke(drawList, slot.Min, slot.Max, pitch * 0.3f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.14f)),
                MathF.Max(1f, 1f * scale));
            if (fleet.IsPlaced(ship) || Lifted(ship))
            {
                continue;
            }

            BroadsideArt.DrawAirship(drawList, slot, true, BroadsideArt.Hull, scale, clock, -1f, 1f, warn[ship],
                ship == DragShip ? 3f * scale : 0f);
        }
    }

    private int PickShip(BroadsideFleet fleet, BroadsideLayout layout, Rect grid, Vector2 mouse, out int pickedGrab,
        out bool pickedAcross)
    {
        pickedGrab = 0;
        pickedAcross = true;
        if (grid.Contains(mouse) && BroadsideLayout.CellAt(grid, mouse, out var column, out var row))
        {
            var occupant = fleet.OccupantAt(BroadsideFleet.CellOf(column, row));
            if (occupant >= 0)
            {
                pickedAcross = fleet.Horizontal(occupant);
                pickedGrab = pickedAcross ? column - fleet.Column(occupant) : row - fleet.Row(occupant);
                return occupant;
            }
        }

        for (var ship = 0; ship < ShipCount; ship++)
        {
            var slot = layout.DockSlot(ship);
            if (fleet.IsPlaced(ship) || !slot.Contains(mouse))
            {
                continue;
            }

            pickedGrab = Math.Clamp((int)((mouse.X - slot.Min.X) / layout.DockPitch), 0, BroadsideFleet.Length(ship) - 1);
            return ship;
        }

        return -1;
    }

    private void Rotate(BroadsideFleet fleet, Rect grid, int ship, ParticleSystem particles, FeedbackFx fx)
    {
        var column = fleet.Column(ship);
        var row = fleet.Row(ship);
        var horizontal = fleet.Horizontal(ship);
        var turned = fleet.Place(ship, column, row, !horizontal);
        if (!turned)
        {
            var pivotColumn = horizontal ? column + grab : column;
            var pivotRow = horizontal ? row : row + grab;
            turned = horizontal
                ? fleet.Place(ship, pivotColumn, pivotRow - grab, false)
                : fleet.Place(ship, pivotColumn - grab, pivotRow, true);
        }

        if (!turned)
        {
            Reject(ship, fx);
            return;
        }

        Settle(fleet, grid, ship, particles);
    }

    private void Drop(BroadsideFleet fleet, Rect grid, int ship, Vector2 mouse, ParticleSystem particles, FeedbackFx fx)
    {
        if (grid.Contains(mouse) && GhostHead(grid, mouse, ship, out var column, out var row) &&
            fleet.Place(ship, column, row, across))
        {
            Settle(fleet, grid, ship, particles);
            return;
        }

        Reject(ship, fx);
    }

    private void Settle(BroadsideFleet fleet, Rect grid, int ship, ParticleSystem particles)
    {
        pop[ship] = 0f;
        Span<int> cells = stackalloc int[BroadsideFleet.LongestShip];
        var count = fleet.ShipCellsOf(ship, cells);
        for (var index = 0; index < count; index++)
        {
            particles.Emit(Dust, BroadsideLayout.CellCenter(grid, cells[index]), 3);
        }

        UiFeedback.Play(UiSound.GamePiece);
    }

    private void Reject(int ship, FeedbackFx fx)
    {
        warn[ship] = 1f;
        fx.AddTrauma(0.08f);
        UiFeedback.Play(UiSound.GameWrong);
    }

    private bool GhostHead(Rect grid, Vector2 mouse, int ship, out int column, out int row)
    {
        BroadsideLayout.CellAt(grid, mouse, out var pointerColumn, out var pointerRow);
        column = across ? pointerColumn - grab : pointerColumn;
        row = across ? pointerRow : pointerRow - grab;
        return BroadsideFleet.Fits(ship, column, row, across);
    }
}
