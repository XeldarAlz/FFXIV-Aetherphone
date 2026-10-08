using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Crater;

internal sealed partial class CraterApp
{
    private const string SurfaceId = "crater.world";
    private const float AimKeySpeed = 1.3f;
    private const float FacingDeadZone = 0.05f;
    private const float MinAimDistance = 0.2f;
    private static readonly Vector4 HintInk = new(1f, 1f, 1f, 0.92f);
    private static readonly Vector4 HintShadow = new(0f, 0f, 0f, 0.6f);

    private Vector2 teleportSpot;
    private bool teleportValid;
    private bool showTeleport;
    private bool aimDragging;
    private bool teleportPressed;
    private HoldLatch fireHold;
    private CraterPan pan;
    private bool spaceHeld;
    private bool firstShotTaken;

    private void ResetInput()
    {
        ReleaseHolds();
        pan.Release();
        firstShotTaken = false;
    }

    private void ReleaseHolds()
    {
        aimDragging = false;
        teleportPressed = false;
        fireHold.Release();
        spaceHeld = false;
        showTeleport = false;
    }

    private void DrawControls(ImDrawListPtr drawList, in GameContext context, in CraterLayout layout, float scale)
    {
        CraterControls.Teams(drawList, layout.Teams, board, labels, scale);
        var playing = context.Session.State == StageFlow.Playing;
        var surfaceHovered = false;
        var surfaceActivated = false;
        if (playing)
        {
            GameInput.Claim();
            surfaceHovered = PressSurface.Claim(SurfaceId, context.Full, out surfaceActivated);
        }

        var mouse = ImGui.GetMousePos();
        var free = !layout.Covers(mouse) && !context.ChromeHit(mouse);
        var rightPressed = surfaceHovered && free && ImGui.IsMouseClicked(ImGuiMouseButton.Right);
        pan.Track(in camera, playing && !board.Over, !board.HumanTurn, surfaceActivated && free, rightPressed);

        if (board.Over || board.IsBot(board.ActiveTeam))
        {
            ReleaseHolds();
            return;
        }

        var enabled = playing && board.HumanTurn;
        DrawHintLine(drawList, layout, scale);
        var clicked = CraterControls.Weapons(drawList, layout.Weapons, board, labels, board.ActiveTeam, Accent,
            enabled, scale);
        CraterControls.Fire(drawList, layout.FireCenter, layout.FireRadius, board.Weapon, board.Charge, Accent,
            enabled && board.Weapon != CraterWeapon.Teleport, fireHold.Held, scale);
        var pad = GamePad.Walker(layout.Pad, Accent, context.Theme);
        if (!enabled)
        {
            if (!board.HumanTurn)
            {
                ReleaseHolds();
            }

            return;
        }

        if (clicked >= 0)
        {
            board.SelectWeapon((CraterWeapon)clicked);
        }

        Keyboard(context.RawDeltaSeconds, pad);
        FireButton(layout);
        Pointer(context, layout, surfaceHovered, surfaceActivated);
    }

    private void Keyboard(float rawSeconds, in ShooterPadInput pad)
    {
        var left = pad.Left || GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);
        var right = pad.Right || GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow);
        board.SetWalk(left == right ? 0 : right ? 1 : -1);

        var raise = GameInput.Held(ImGuiKey.UpArrow);
        var lower = GameInput.Held(ImGuiKey.DownArrow);
        if (raise != lower)
        {
            board.SetAim(board.ActiveAim + (raise ? AimKeySpeed : -AimKeySpeed) * rawSeconds);
        }

        if (GameInput.Pressed(ImGuiKey.Q))
        {
            Cycle(-1);
        }

        if (GameInput.Pressed(ImGuiKey.E))
        {
            Cycle(1);
        }

        for (var key = 0; key < CraterRules.MaxFuse; key++)
        {
            if (!GameInput.Pressed(ImGuiKey.Key1 + key) && !GameInput.Pressed(ImGuiKey.Keypad1 + key))
            {
                continue;
            }

            board.SelectWeapon(CraterWeapon.Grenade);
            board.SetFuse(key + 1);
        }

        var space = GameInput.Held(ImGuiKey.Space);
        if (space && !spaceHeld)
        {
            board.BeginCharge();
        }
        else if (!space && spaceHeld)
        {
            board.ReleaseCharge();
        }

        spaceHeld = space;
    }

    private void Cycle(int direction)
    {
        var count = CraterRules.WeaponCount;
        var current = (int)board.Weapon;
        for (var step = 1; step <= count; step++)
        {
            var candidate = ((current + direction * step) % count + count) % count;
            if (board.Ammo(board.ActiveTeam, (CraterWeapon)candidate) == 0)
            {
                continue;
            }

            board.SelectWeapon((CraterWeapon)candidate);
            return;
        }
    }

    private void FireButton(in CraterLayout layout)
    {
        var mouse = ImGui.GetMousePos();
        var radius = layout.FireRadius;
        var corner = new Vector2(radius, radius);
        var over = UiInteract.Hover(layout.FireCenter - corner, layout.FireCenter + corner) &&
                   Vector2.DistanceSquared(mouse, layout.FireCenter) <= radius * radius;
        if (over)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pressed = over && board.Weapon != CraterWeapon.Teleport && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        switch (fireHold.Update(pressed, ImGui.IsMouseDown(ImGuiMouseButton.Left)))
        {
            case HoldEdge.Pressed:
                board.BeginCharge();
                return;
            case HoldEdge.Released:
                board.ReleaseCharge();
                return;
            default:
                return;
        }
    }

    private void Pointer(in GameContext context, in CraterLayout layout, bool hovered, bool activated)
    {
        var mouse = ImGui.GetMousePos();
        var blocked = layout.Covers(mouse) || context.ChromeHit(mouse) || fireHold.Held;
        var world = camera.ToWorld(mouse);
        showTeleport = board.Weapon == CraterWeapon.Teleport && hovered && !blocked;
        if (showTeleport)
        {
            teleportValid = board.TryTeleportSpot(world, out var spot);
            teleportSpot = teleportValid ? spot : world;
        }

        if (activated && !blocked)
        {
            if (board.Weapon == CraterWeapon.Teleport)
            {
                teleportPressed = true;
            }
            else
            {
                aimDragging = true;
            }
        }

        if (aimDragging)
        {
            AimAt(world);
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            return;
        }

        if (teleportPressed && showTeleport)
        {
            board.Teleport(world);
        }

        aimDragging = false;
        teleportPressed = false;
    }

    private void AimAt(Vector2 world)
    {
        ref readonly var active = ref board.Moogle(board.ActiveMoogle);
        var offset = world - active.Position;
        if (offset.LengthSquared() < MinAimDistance * MinAimDistance)
        {
            return;
        }

        if (MathF.Abs(offset.X) > FacingDeadZone)
        {
            board.SetFacing(offset.X > 0f ? 1 : -1);
        }

        board.SetAim(MathF.Atan2(-offset.Y, MathF.Abs(offset.X)));
    }

    private void DrawHintLine(ImDrawListPtr drawList, in CraterLayout layout, float scale)
    {
        var text = HintText();
        if (text.Length == 0)
        {
            return;
        }

        var center = new Vector2(layout.Weapons.Center.X,
            layout.HintY - Typography.LineHeight(TextStyles.Footnote) * 0.5f);
        Typography.DrawCentered(drawList, center + new Vector2(1f, 1f) * scale, text, HintShadow, TextStyles.Footnote);
        Typography.DrawCentered(drawList, center, text, HintInk, TextStyles.Footnote);
    }

    private string HintText() => board.Weapon switch
    {
        CraterWeapon.Grenade => labels.FuseHint(board.Fuse),
        CraterWeapon.Teleport => Loc.T(L.Crater.TeleportHint),
        CraterWeapon.Shield => Loc.T(L.Crater.ShieldHint),
        _ => firstShotTaken ? labels.WeaponName(board.Weapon) : Loc.T(L.Crater.ControlsHint),
    };
}
