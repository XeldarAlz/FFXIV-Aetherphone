using Aetherphone.Apps.Games.Crater;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Online;

internal sealed partial class OnlineCraterTable
{
    private const string SurfaceId = "games.crater.online.world";
    private const float AimKeySpeed = 1.3f;
    private const float FacingDeadZone = 0.05f;
    private const float MinAimDistance = 0.2f;
    private const float ChargeSeconds = 1.4f;
    private const float DefaultPower = 0.6f;
    private const long SentLockMilliseconds = 1_500;
    private const float ButtonReach = 1.2f;
    private const long MotionIntervalMilliseconds = 66;
    private const float MilliradiansPerRadian = 1000f;

    private CraterWeapon weapon = CraterWeapon.Shell;
    private int fuse = CraterRules.DefaultFuse;
    private float charge;
    private float lastPower = DefaultPower;
    private Vector2 teleportSpot;
    private int awaitingActionCount = -1;
    private long sentAtTick;
    private bool charging;
    private HoldLatch fireHold;
    private CraterPan pan;
    private long motionSentAt;
    private long motionSeen;
    private int sentTurn = -1;
    private int sentTicks;
    private int sentFacing;
    private int sentAim;
    private bool spaceHeld;
    private bool aimDragging;
    private bool teleportPressed;
    private bool showTeleport;
    private bool teleportValid;
    private bool firstShotTaken;

    private void ResetControls()
    {
        ReleaseHolds();
        pan.Release();
        if (Ammo(weapon) == 0)
        {
            weapon = CraterWeapon.Shell;
        }
    }

    private void ReleaseHolds()
    {
        charging = false;
        charge = 0f;
        fireHold.Release();
        spaceHeld = false;
        aimDragging = false;
        teleportPressed = false;
        showTeleport = false;
    }

    private bool Locked(CraterRoomStateDto shown) =>
        store.ActInFlight || (shown.ActionCount == awaitingActionCount &&
                              Environment.TickCount64 - sentAtTick < SentLockMilliseconds);

    private int Ammo(CraterWeapon slot) => ammo[(int)slot];

    private void FillAmmo(CraterRoomStateDto shown)
    {
        var listed = shown.Ammo ?? Array.Empty<int>();
        var offset = myTeam * CraterRules.WeaponCount;
        for (var slot = 0; slot < CraterRules.WeaponCount; slot++)
        {
            var index = offset + slot;
            ammo[slot] = myTeam >= 0 && index < listed.Length ? listed[index] : 0;
        }
    }

    private void DrawControls(ImDrawListPtr drawList, Rect body, in CraterLayout layout, CraterRoomStateDto shown,
        Vector4 accent, PhoneTheme theme, float raw, float scale)
    {
        GameInput.Claim();
        var surfaceHovered = PressSurface.Claim(SurfaceId, body, out var surfaceActivated);
        FillAmmo(shown);
        if (Ammo(weapon) == 0)
        {
            weapon = CraterWeapon.Shell;
        }

        DrawHint(drawList, layout.Weapons.Center.X, layout.HintY, HintText(), scale);
        var clicked = CraterControls.Weapons(drawList, layout.Weapons, ammo, weapon, fuse, labels, accent, true,
            scale);
        CraterControls.Fire(drawList, layout.FireCenter, layout.FireRadius, weapon, charge, accent,
            weapon != CraterWeapon.Teleport, fireHold.Held, scale);
        var pad = GamePad.Walker(layout.Pad, accent, theme);
        if (clicked >= 0)
        {
            Select((CraterWeapon)clicked);
        }

        Keyboard(raw, pad, shown);
        ShareMotion(shown);
        if (scene.Stranded)
        {
            Pass(shown);
            return;
        }

        FireButton(layout, shown);
        Pointer(body, layout, surfaceHovered, surfaceActivated, shown, scale);
        if (charging)
        {
            charge = MathF.Min(1f, charge + raw / ChargeSeconds);
        }
    }

    private string HintText() => weapon switch
    {
        CraterWeapon.Grenade => labels.FuseHint(fuse),
        CraterWeapon.Teleport => Loc.T(L.Crater.TeleportHint),
        CraterWeapon.Shield => Loc.T(L.Crater.ShieldHint),
        _ => firstShotTaken ? labels.WeaponName(weapon) : Loc.T(L.Crater.ControlsHint),
    };

    private void Select(CraterWeapon next)
    {
        if (charging || Ammo(next) == 0)
        {
            return;
        }

        weapon = next;
    }

    private void Keyboard(float raw, in ShooterPadInput pad, CraterRoomStateDto shown)
    {
        var left = pad.Left || GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);
        var right = pad.Right || GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow);
        var direction = left == right ? 0 : right ? 1 : -1;
        if (!charging)
        {
            scene.Walk(direction, raw);
        }

        var raise = GameInput.Held(ImGuiKey.UpArrow);
        var lower = GameInput.Held(ImGuiKey.DownArrow);
        if (raise != lower && scene.ActiveMoogle >= 0)
        {
            scene.SetAim(scene.Aim(scene.ActiveMoogle) + (raise ? AimKeySpeed : -AimKeySpeed) * raw, 0);
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

            Select(CraterWeapon.Grenade);
            if (weapon == CraterWeapon.Grenade)
            {
                fuse = key + 1;
            }
        }

        var space = GameInput.Held(ImGuiKey.Space);
        if (space && !spaceHeld)
        {
            BeginCharge(shown);
        }
        else if (!space && spaceHeld)
        {
            ReleaseCharge(shown);
        }

        spaceHeld = space;
    }

    private void Cycle(int direction)
    {
        var count = CraterRules.WeaponCount;
        var current = (int)weapon;
        for (var step = 1; step <= count; step++)
        {
            var candidate = ((current + direction * step) % count + count) % count;
            if (Ammo((CraterWeapon)candidate) == 0)
            {
                continue;
            }

            Select((CraterWeapon)candidate);
            return;
        }
    }

    private void FireButton(in CraterLayout layout, CraterRoomStateDto shown)
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

        var pressed = over && weapon != CraterWeapon.Teleport && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        switch (fireHold.Update(pressed, ImGui.IsMouseDown(ImGuiMouseButton.Left)))
        {
            case HoldEdge.Pressed:
                BeginCharge(shown);
                return;
            case HoldEdge.Released:
                ReleaseCharge(shown);
                return;
            default:
                return;
        }
    }

    private void ShareMotion(CraterRoomStateDto shown)
    {
        var active = scene.ActiveMoogle;
        if (active < 0)
        {
            return;
        }

        var facing = scene.Moogle(active).Facing;
        var aim = (int)MathF.Round(scene.Aim(active) * MilliradiansPerRadian);
        var ticks = scene.WalkTicks;
        var now = Environment.TickCount64;
        var unchanged = shown.TurnCount == sentTurn && ticks == sentTicks && facing == sentFacing && aim == sentAim;
        if (unchanged || now - motionSentAt < MotionIntervalMilliseconds)
        {
            return;
        }

        var steps = scene.Steps;
        var values = new int[GameRoomWire.CraterMotionHeader + steps.Length];
        values[0] = GameRoomWire.CraterMotionWalk;
        values[1] = shown.TurnCount;
        values[2] = active;
        values[3] = facing;
        values[4] = aim;
        steps.CopyTo(values.AsSpan(GameRoomWire.CraterMotionHeader));
        store.Room.SendMotion(values);
        motionSentAt = now;
        sentTurn = shown.TurnCount;
        sentTicks = ticks;
        sentFacing = facing;
        sentAim = aim;
    }

    private void FollowMotion(CraterRoomStateDto shown)
    {
        if (store.Room.Motion is not { } motion || motion.Serial == motionSeen)
        {
            return;
        }

        motionSeen = motion.Serial;
        var values = motion.Values;
        if (values.Length < GameRoomWire.CraterMotionHeader || values[0] != GameRoomWire.CraterMotionWalk
            || string.Equals(motion.From, myUserId, StringComparison.Ordinal) || !PlaysTurn(shown, motion.From))
        {
            return;
        }

        scene.Follow(values[1], values[2], values[3], values[4] / MilliradiansPerRadian,
            values.AsSpan(GameRoomWire.CraterMotionHeader));
    }

    private static bool PlaysTurn(CraterRoomStateDto shown, string userId)
    {
        var players = shown.Players ?? Array.Empty<CraterPlayerDto>();
        for (var index = 0; index < players.Length; index++)
        {
            if (players[index].Team == shown.TurnTeam
                && string.Equals(players[index].UserId, userId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void TrackPan(Rect body, in CraterLayout layout, bool allowed, bool leftPans, float scale)
    {
        var activated = false;
        if (leftPans)
        {
            PressSurface.Claim(SurfaceId, body, out activated);
        }

        var free = !PointerBlocked(body, layout, ImGui.GetMousePos(), scale);
        var right = free && UiInteract.Hover(body.Min, body.Max) && ImGui.IsMouseClicked(ImGuiMouseButton.Right);
        pan.Track(in camera, allowed, leftPans, activated && free, right);
    }

    private static bool PointerBlocked(Rect body, in CraterLayout layout, Vector2 mouse, float scale)
    {
        var backReach = BackRadius * ButtonReach * scale;
        return layout.Covers(mouse) || ResignRect(layout, scale).Contains(mouse) ||
               Vector2.DistanceSquared(mouse, BackCenter(body, scale)) <= backReach * backReach;
    }

    private void Pointer(Rect body, in CraterLayout layout, bool hovered, bool activated, CraterRoomStateDto shown,
        float scale)
    {
        var mouse = ImGui.GetMousePos();
        var blocked = fireHold.Held || PointerBlocked(body, layout, mouse, scale);
        var world = camera.ToWorld(mouse);
        showTeleport = weapon == CraterWeapon.Teleport && hovered && !blocked;
        if (showTeleport)
        {
            teleportValid = CraterFooting.TeleportSpot(scene.Terrain, scene.Water, world, out var spot);
            teleportSpot = teleportValid ? spot : world;
        }

        if (activated && !blocked)
        {
            if (weapon == CraterWeapon.Teleport)
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

        if (teleportPressed && showTeleport && teleportValid)
        {
            Send(shown, 0f, world);
        }

        aimDragging = false;
        teleportPressed = false;
    }

    private void AimAt(Vector2 world)
    {
        var active = scene.ActiveMoogle;
        if (active < 0)
        {
            return;
        }

        var offset = world - scene.Moogle(active).Position;
        if (offset.LengthSquared() < MinAimDistance * MinAimDistance)
        {
            return;
        }

        var facing = MathF.Abs(offset.X) > FacingDeadZone ? (offset.X > 0f ? 1 : -1) : 0;
        scene.SetAim(MathF.Atan2(-offset.Y, MathF.Abs(offset.X)), facing);
    }

    private void BeginCharge(CraterRoomStateDto shown)
    {
        if (charging || Ammo(weapon) == 0 || scene.Airborne)
        {
            return;
        }

        if (weapon == CraterWeapon.Shield)
        {
            if (scene.ActiveMoogle >= 0 && !scene.Moogle(scene.ActiveMoogle).Shielded)
            {
                Send(shown, 0f, Vector2.Zero);
            }

            return;
        }

        if (!CraterRules.Fires(weapon))
        {
            return;
        }

        charging = true;
        charge = 0f;
    }

    private void ReleaseCharge(CraterRoomStateDto shown)
    {
        if (!charging)
        {
            return;
        }

        var power = charge;
        charging = false;
        charge = 0f;
        lastPower = power;
        Send(shown, power, Vector2.Zero);
    }

    private void Send(CraterRoomStateDto shown, float power, Vector2 target)
    {
        var active = scene.ActiveMoogle;
        if (active < 0 || store.ActInFlight || scene.Airborne)
        {
            return;
        }

        ref readonly var moogle = ref scene.Moogle(active);
        var walkX = scene.Walked ? scene.WalkedX : -1f;
        var steps = scene.Walked ? scene.Steps.ToArray() : null;
        store.SendCraterShot((int)weapon, moogle.Facing, scene.Aim(active), power, fuse, walkX, target.X, target.Y,
            steps);
        Sent(shown);
        firstShotTaken = true;
    }

    private void Pass(CraterRoomStateDto shown)
    {
        if (store.ActInFlight)
        {
            return;
        }

        store.SendCraterPass(scene.Steps.ToArray());
        Sent(shown);
    }

    private void Sent(CraterRoomStateDto shown)
    {
        awaitingActionCount = shown.ActionCount;
        sentAtTick = Environment.TickCount64;
        ReleaseHolds();
    }
}
