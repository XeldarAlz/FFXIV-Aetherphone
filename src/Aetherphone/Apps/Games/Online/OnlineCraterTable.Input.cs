using Aetherphone.Apps.Games.Crater;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
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

    private CraterWeapon weapon = CraterWeapon.Shell;
    private int fuse = CraterRules.DefaultFuse;
    private float charge;
    private float lastPower = DefaultPower;
    private Vector2 teleportSpot;
    private int awaitingActionCount = -1;
    private long sentAtTick;
    private bool charging;
    private HoldLatch fireHold;
    private bool spaceHeld;
    private bool aimDragging;
    private bool teleportPressed;
    private bool showTeleport;
    private bool teleportValid;
    private bool firstShotTaken;

    private void ResetControls()
    {
        ReleaseHolds();
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
        _ => firstShotTaken ? labels.WeaponName(weapon) : Loc.T(L.Games.OnlineCraterControls),
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

    private void Pointer(Rect body, in CraterLayout layout, bool hovered, bool activated, CraterRoomStateDto shown,
        float scale)
    {
        var mouse = ImGui.GetMousePos();
        var backReach = BackRadius * ButtonReach * scale;
        var blocked = layout.Covers(mouse) || fireHold.Held || ResignRect(layout, scale).Contains(mouse) ||
                      Vector2.DistanceSquared(mouse, BackCenter(body, scale)) <= backReach * backReach;
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
        if (charging || Ammo(weapon) == 0)
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
        if (active < 0 || store.ActInFlight)
        {
            return;
        }

        ref readonly var moogle = ref scene.Moogle(active);
        var walkX = scene.Walked ? scene.WalkedX : -1f;
        store.SendCraterShot((int)weapon, moogle.Facing, scene.Aim(active), power, fuse, walkX, target.X, target.Y);
        awaitingActionCount = shown.ActionCount;
        sentAtTick = Environment.TickCount64;
        firstShotTaken = true;
        ReleaseHolds();
    }
}
