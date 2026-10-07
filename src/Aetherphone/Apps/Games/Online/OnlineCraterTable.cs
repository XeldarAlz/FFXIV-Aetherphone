using Aetherphone.Apps.Games.Crater;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.Online;

internal sealed partial class OnlineCraterTable : IDisposable
{
    private const string AccentId = "crater";
    private const float ViewWidth = 16f;
    private const float ViewHeight = 8f;
    private const float ChargeZoom = 1.15f;
    private const float FlightZoom = 1.08f;
    private const float ZoomSmoothSeconds = 0.4f;
    private const int HintCapacity = 192;
    private const int HintStride = 3;
    private const float Margin = 12f;
    private const float BackRadius = 15f;
    private const float RoundWidth = 78f;
    private const float RingRadius = 15f;
    private const float ResignWidth = 92f;
    private const float ResignHeight = 28f;
    private const float StatusLift = 30f;
    private const float HoldLift = 0.42f;
    private static readonly float[] SkyMoments = { 0.12f, 0.2f, 0.26f, 0.32f, 0.46f };
    private static readonly Vector4 HintInk = new(1f, 1f, 1f, 0.92f);
    private static readonly Vector4 HintShadow = new(0f, 0f, 0f, 0.6f);
    private static readonly Vector4 ResignTint = new(0.85f, 0.35f, 0.32f, 1f);

    private readonly GameRoomsStore store;
    private readonly ITextureProvider textures;
    private readonly OnlineCraterScene scene = new();
    private readonly CraterJuice juice = new();
    private readonly CraterLabels labels = new();
    private readonly StageBackdrop backdrop = new();
    private readonly ScreenFx screen;
    private readonly Vector2[] hintPath = new Vector2[HintCapacity];
    private readonly string[] teamNames = new string[CraterRules.MaxTeams];
    private readonly string[] turnLines = new string[CraterRules.MaxTeams];
    private readonly string[] winLines = new string[CraterRules.MaxTeams];
    private readonly int[] teamHealth = new int[CraterRules.MaxTeams];
    private readonly int[] ammo = new int[CraterRules.WeaponCount];
    private CraterTerrain? terrainTexture;
    private Camera2D camera = Camera2D.Create();
    private Spring zoom = new(1f);
    private CraterPlayerDto[]? namedPlayers;
    private LanguageInfo? namedLanguage;
    private string myUserId = string.Empty;
    private int terrainVersion = -1;
    private int teamCount;
    private int myTeam = CraterBoard.NoTeam;
    private int cachedSeconds = -1;
    private string cachedSecondsLabel = string.Empty;
    private float time;
    private float skyProgress;
    private bool cameraPlaced;

    public OnlineCraterTable(GameRoomsStore store, ITextureProvider textures)
    {
        this.store = store;
        this.textures = textures;
        screen = new ScreenFx(backdrop);
        backdrop.Set(Backdrop.Sky);
    }

    public void Reset()
    {
        scene.Reset();
        juice.Reset(0UL);
        screen.Clear();
        ResetControls();
        terrainTexture?.Dispose();
        terrainTexture = null;
        terrainVersion = -1;
        namedPlayers = null;
        cameraPlaced = false;
        camera = Camera2D.Create();
        zoom.SnapTo(1f);
    }

    public void Dispose()
    {
        terrainTexture?.Dispose();
        terrainTexture = null;
    }

    public void Draw(Rect body, PhoneTheme theme, float scale, GameRoomSnapshotDto snapshot, CraterRoomStateDto board,
        string notice, Action? back, OnlineFinishHold hold)
    {
        using var surface = AppSurface.Begin(body, true);
        ImGui.Dummy(new Vector2(MathF.Max(1f, body.Width - 32f * scale), body.Height - 16f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var raw = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var accent = AppAccents.For(AccentId);
        scene.Sync(board);
        var shown = scene.Shown ?? board;
        RefreshNames(shown);
        EnsureTerrain(shown);
        time += raw;
        juice.Advance(raw);
        screen.Update(raw);
        PlaceCamera(body, raw, scale);
        scene.Advance(juice.Fx.ScaleDelta(raw) * screen.TimeScale);
        Drain();
        var live = shown.EndKind.Length == 0 && shown.TurnTeam >= 0;
        var myTurn = live && !scene.Replaying && myTeam >= 0 && shown.TurnTeam == myTeam && !Locked(shown);
        if (!myTurn)
        {
            ReleaseHolds();
        }

        var pointer = ImGui.GetMousePos();
        backdrop.Update(raw, body, pointer, UiInteract.Hover(body.Min, body.Max));
        backdrop.Draw(drawList, body, accent, scale);
        DrawWorld(drawList, shown, myTurn, scale);
        screen.Draw(drawList, body, accent);

        var landscape = back is not null && body.IsLandscape();
        var safe = Safe(body, landscape, scale);
        var layout = CraterControls.Layout(body, safe, Math.Max(1, teamCount), scale);
        DrawRoster(drawList, layout, shown, scale);
        CraterControls.Wind(drawList, layout.Wind, shown.Wind, time, scale);
        var roundLeft = landscape
            ? BackCenter(body, scale).X + (BackRadius + 8f) * scale
            : layout.Wind.Max.X - (RoundWidth + 8f + RingRadius * 2f) * scale;
        var roundCenterY = landscape
            ? BackCenter(body, scale).Y
            : layout.Wind.Max.Y + (8f + StageLayout.SecondaryHeight * 0.5f) * scale;
        DrawRound(drawList, new Vector2(roundLeft, roundCenterY), snapshot, shown, theme, scale);
        if (myTurn)
        {
            pan.Release();
            DrawControls(drawList, body, layout, shown, accent, theme, raw, scale);
        }
        else
        {
            TrackPan(body, layout, live, scale);
            DrawStatus(drawList, layout, shown, live, notice, scale);
        }

        juice.DrawBanners(drawList, body, theme, MathF.Max(layout.Wind.Max.Y, roundCenterY + RingRadius * scale));
        if (hold.Holding)
        {
            var center = new Vector2(body.Center.X, body.Min.Y + body.Height * HoldLift);
            hold.Draw(drawList, center, MathF.Min(body.Width - 32f * scale, 360f * scale), theme, scale,
                !scene.Replaying);
        }
        else if (myTurn && notice.Length > 0)
        {
            DrawHint(drawList, layout.Weapons.Center.X, layout.HintY - StatusLift * scale, notice, scale);
        }

        if (landscape && GameHud.LandscapeBack(BackCenter(body, scale), BackRadius * scale, theme))
        {
            back!();
        }

        var resign = ResignRect(layout, scale);
        if (live && myTeam >= 0 && TeamStanding(myTeam)
            && GameHud.Button(resign.Center, resign.Size, Loc.T(L.Games.OnlineResign), ResignTint, theme)
            && !store.ActInFlight)
        {
            store.SendResign();
        }
    }

    private static Rect ResignRect(in CraterLayout layout, float scale)
    {
        var top = layout.Teams.Max.Y + 8f * scale;
        return new Rect(new Vector2(layout.Teams.Max.X - ResignWidth * scale, top),
            new Vector2(layout.Teams.Max.X, top + ResignHeight * scale));
    }

    private static Rect Safe(Rect body, bool landscape, float scale)
    {
        var side = StageLayout.SafeSide * scale;
        var top = (landscape ? StageLayout.SafeTopCompact : Margin) * scale;
        return new Rect(new Vector2(body.Min.X + side, body.Min.Y + top),
            new Vector2(body.Max.X - side, body.Max.Y - StageLayout.SafeBottom * scale));
    }

    private static Vector2 BackCenter(Rect body, float scale) =>
        new(body.Min.X + (Margin + BackRadius) * scale, body.Min.Y + (Margin + BackRadius) * scale);

    private void EnsureTerrain(CraterRoomStateDto shown)
    {
        terrainTexture ??= new CraterTerrain(textures, scene.Terrain);
        if (terrainVersion == scene.TerrainVersion)
        {
            return;
        }

        terrainVersion = scene.TerrainVersion;
        terrainTexture.Reset(CraterArt.Material(scene.Style));
        juice.Reset((ulong)(uint)shown.Seed);
        skyProgress = SkyMoments[(int)((uint)shown.Seed % (uint)SkyMoments.Length)];
        cameraPlaced = false;
        ResetControls();
    }

    private void RefreshNames(CraterRoomStateDto shown)
    {
        var players = shown.Players ?? Array.Empty<CraterPlayerDto>();
        if (ReferenceEquals(players, namedPlayers) && ReferenceEquals(namedLanguage, Loc.Current)
            && string.Equals(myUserId, store.AccountId, StringComparison.Ordinal))
        {
            return;
        }

        namedPlayers = players;
        namedLanguage = Loc.Current;
        myUserId = store.AccountId;
        teamCount = Math.Clamp(shown.TeamCount, 0, CraterRules.MaxTeams);
        myTeam = CraterBoard.NoTeam;
        for (var team = 0; team < CraterRules.MaxTeams; team++)
        {
            teamNames[team] = string.Empty;
        }

        for (var index = 0; index < players.Length; index++)
        {
            var player = players[index];
            if (player.Team < 0 || player.Team >= CraterRules.MaxTeams)
            {
                continue;
            }

            teamNames[player.Team] = player.DisplayName;
            if (string.Equals(player.UserId, myUserId, StringComparison.Ordinal))
            {
                myTeam = player.Team;
            }
        }

        for (var team = 0; team < CraterRules.MaxTeams; team++)
        {
            var mine = team == myTeam;
            turnLines[team] = mine ? Loc.T(L.Crater.YourTurn) : Loc.T(L.Crater.TeamTurn, teamNames[team]);
            winLines[team] = mine ? Loc.T(L.Crater.YourTeamWins) : Loc.T(L.Crater.TeamWins, teamNames[team]);
        }
    }

    private void PlaceCamera(Rect body, float raw, float scale)
    {
        var flyingNow = scene.Replaying && AnyProjectile();
        var wanted = charging ? ChargeZoom : flyingNow ? FlightZoom : 1f;
        zoom.Step(wanted, ZoomSmoothSeconds, raw);
        camera.Fit(body, ViewWidth * zoom.Value, ViewHeight * zoom.Value, FitMode.Contain);
        var active = scene.ActiveMoogle;
        var aim = active >= 0 ? scene.Aim(active) : 0f;
        var focus = CraterFocus.Goal(scene.Projectiles, scene.Moogles, scene.SinceBlast, scene.LastBlast, active,
            !scene.Replaying, aim, out var smoothSeconds, out var flying);
        var goal = CraterFocus.KeepInView(in camera, focus, flying, scene.Water);
        if (!cameraPlaced)
        {
            camera.Place(goal);
            cameraPlaced = true;
        }

        if (!pan.Steer(ref camera, scene.Water))
        {
            camera.Follow(goal, Vector2.Zero, smoothSeconds, raw);
        }

        screen.ApplyTo(ref camera);
        camera.Update(raw, scale);
        backdrop.SetSky(skyProgress);
        backdrop.SetCamera(in camera);
    }

    private bool AnyProjectile()
    {
        var projectiles = scene.Projectiles;
        for (var index = 0; index < projectiles.Length; index++)
        {
            if (projectiles[index].Alive)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawWorld(ImDrawListPtr drawList, CraterRoomStateDto shown, bool myTurn, float scale)
    {
        var material = CraterArt.Material(scene.Style);
        terrainTexture?.Draw(drawList, in camera, scene.Craters);
        juice.DrawEmbers(drawList, in camera);
        var active = scene.ActiveMoogle;
        var aim = active >= 0 ? scene.Aim(active) : 0f;
        var showLauncher = scene.Replaying || shown.EndKind.Length == 0;
        CraterRenderer.Bodies(drawList, in camera, scene.Moogles, active, aim, juice.Hurt, showLauncher, time);
        juice.DrawTrails(drawList, in camera, scene.Projectiles, material, scale);
        CraterRenderer.Projectiles(drawList, in camera, scene.Projectiles, time, scale);
        if (myTurn)
        {
            DrawAim(drawList, shown, scale);
        }

        CraterRenderer.Water(drawList, in camera, scene.Terrain, scene.Water, time, scale);
        juice.Particles.Draw(drawList, in camera);
        juice.Fx.DrawRings(drawList, scale);
        juice.Fx.DrawText();
        CraterRenderer.Labels(drawList, in camera, scene.Moogles, active, teamNames,
            !scene.Replaying && shown.EndKind.Length == 0, time, scale);
    }

    private void DrawAim(ImDrawListPtr drawList, CraterRoomStateDto shown, float scale)
    {
        var active = scene.ActiveMoogle;
        if (active < 0 || !scene.Moogle(active).Alive)
        {
            return;
        }

        ref readonly var moogle = ref scene.Moogle(active);
        var team = GameSeats.Color(moogle.Team);
        var direction = CraterRules.AimDirection(scene.Aim(active), moogle.Facing);
        if (charging)
        {
            CraterRenderer.Power(drawList, in camera, moogle.Position, direction, charge, scale);
        }

        if (weapon == CraterWeapon.Teleport)
        {
            if (showTeleport)
            {
                CraterRenderer.Ghost(drawList, in camera, teleportSpot, teleportValid, team, time, scale);
            }

            return;
        }

        if (!CraterRules.Fires(weapon))
        {
            return;
        }

        CraterRenderer.Reticle(drawList, in camera, moogle.Position, direction, team, time, scale);
        var shot = new CraterShot(weapon, moogle.Facing, scene.Aim(active), charging ? charge : lastPower, fuse);
        var projectile = CraterBallistics.Launch(moogle.Position, active, moogle.Team, shot);
        CraterBallistics.Predict(projectile, scene.Terrain, CraterRules.Wind(shown.Wind), scene.Water, scene.Moogles,
            hintPath, HintStride, out var count, out _, out _);
        var visible = Math.Min(count, Math.Max(2, (int)MathF.Ceiling(count * CraterRenderer.HintShare)));
        CraterRenderer.Hint(drawList, in camera, new ReadOnlySpan<Vector2>(hintPath, 0, visible), team, scale);
    }

    private void Drain()
    {
        var material = CraterArt.Material(scene.Style);
        while (scene.TryTakeEvent(out var entry))
        {
            switch (entry.Kind)
            {
                case CraterEventKind.TurnStarted:
                    OnTurnStarted(entry);
                    break;
                case CraterEventKind.MatchOver:
                    OnMatchOver(entry);
                    break;
                default:
                    juice.Play(entry, ref camera, screen, material, false);
                    break;
            }
        }
    }

    private void OnTurnStarted(in CraterEvent entry)
    {
        var team = entry.Team;
        if (team < 0 || team >= CraterRules.MaxTeams)
        {
            return;
        }

        juice.QueueBanner(turnLines[team], GameSeats.Color(team), CraterJuice.BannerSeconds);
        UiFeedback.Play(UiSound.GamePiece);
        if (team == myTeam)
        {
            ResetControls();
            scene.BeginAiming();
        }
    }

    private void OnMatchOver(in CraterEvent entry)
    {
        var winner = entry.Team;
        screen.SlowMo(0.35f, 0.7f);
        screen.Sweep();
        if (winner < 0 || winner >= CraterRules.MaxTeams)
        {
            juice.QueueBanner(Loc.T(L.Games.Draw), CraterJuice.Flash, CraterJuice.WinSeconds);
            UiFeedback.Play(UiSound.GameWrong);
            return;
        }

        var color = GameSeats.Color(winner);
        juice.QueueBanner(winLines[winner], color, CraterJuice.WinSeconds);
        juice.Celebrate(scene.Moogles, winner, color);
        UiFeedback.Play(winner == myTeam ? UiSound.GameClear : UiSound.GameWrong);
    }

    private void DrawRoster(ImDrawListPtr drawList, in CraterLayout layout, CraterRoomStateDto shown, float scale)
    {
        if (teamCount <= 0)
        {
            return;
        }

        Array.Clear(teamHealth);
        var moogles = scene.Moogles;
        var teamSize = 0;
        for (var index = 0; index < moogles.Length; index++)
        {
            ref readonly var moogle = ref moogles[index];
            if (moogle.Team < 0 || moogle.Team >= teamCount)
            {
                continue;
            }

            teamSize += moogle.Team == 0 ? 1 : 0;
            if (moogle.Alive)
            {
                teamHealth[moogle.Team] += moogle.Health;
            }
        }

        var active = scene.Replaying || shown.EndKind.Length > 0 ? CraterBoard.NoTeam : shown.TurnTeam;
        CraterControls.Teams(drawList, layout.Teams, new ReadOnlySpan<string>(teamNames, 0, teamCount),
            new ReadOnlySpan<int>(teamHealth, 0, teamCount), CraterRules.MaxHealth * Math.Max(1, teamSize), active,
            scale);
    }

    private void DrawRound(ImDrawListPtr drawList, Vector2 anchor, GameRoomSnapshotDto snapshot,
        CraterRoomStateDto shown, PhoneTheme theme, float scale)
    {
        var height = StageLayout.SecondaryHeight * scale;
        var capsule = new Rect(new Vector2(anchor.X, anchor.Y - height * 0.5f),
            new Vector2(anchor.X + RoundWidth * scale, anchor.Y + height * 0.5f));
        StageHud.Capsule(drawList, capsule, scale);
        var caption = Loc.T(L.Crater.Round);
        var value = GameNumber.Label(Math.Max(1, shown.Round));
        var valueWidth = Typography.Measure(value, TextStyles.FootnoteEmphasized).X;
        var padding = 10f * scale;
        Typography.DrawCentered(drawList, new Vector2(capsule.Max.X - padding - valueWidth * 0.5f, capsule.Center.Y),
            value, theme.TextStrong, TextStyles.FootnoteEmphasized);
        var captionWidth = MathF.Max(1f, capsule.Width - padding * 2f - valueWidth - 4f * scale);
        Typography.Draw(drawList,
            new Vector2(capsule.Min.X + padding, capsule.Center.Y - Typography.LineHeight(TextStyles.Caption2) * 0.5f),
            Typography.FitText(caption, captionWidth, TextStyles.Caption2), theme.TextMuted, TextStyles.Caption2);
        if (scene.Replaying || shown.EndKind.Length > 0 || shown.TurnTeam < 0)
        {
            return;
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var window = Math.Max(1, shown.TurnSeconds);
        var remaining = Math.Min(store.Room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, nowMs),
            window * 1000L);
        var ring = RingRadius * scale;
        var ringCenter = new Vector2(capsule.Max.X + 8f * scale + ring, capsule.Center.Y);
        var tint = GameSeats.Color(shown.TurnTeam);
        TurnTimerRing.Draw(drawList, ringCenter, ring, remaining, window, tint, scale);
        var urgent = TurnTimerRing.IsUrgent(remaining, window);
        Typography.DrawCentered(drawList, ringCenter, SecondsLabel(remaining),
            urgent ? theme.TextStrong : theme.TextMuted, 0.78f, FontWeight.SemiBold);
    }

    private void DrawStatus(ImDrawListPtr drawList, in CraterLayout layout, CraterRoomStateDto shown, bool live,
        string notice, float scale)
    {
        var text = notice;
        if (text.Length == 0 && !store.Room.Attached)
        {
            text = Loc.T(L.Games.OnlineReconnecting);
        }

        if (text.Length == 0 && live && !scene.Replaying && shown.TurnTeam >= 0)
        {
            text = turnLines[shown.TurnTeam];
        }

        if (text.Length == 0)
        {
            return;
        }

        DrawHint(drawList, layout.Weapons.Center.X, layout.HintY, text, scale);
    }

    private static void DrawHint(ImDrawListPtr drawList, float centerX, float bottom, string text, float scale)
    {
        var center = new Vector2(centerX, bottom - Typography.LineHeight(TextStyles.Footnote) * 0.5f);
        Typography.DrawCentered(drawList, center + new Vector2(1f, 1f) * scale, text, HintShadow, TextStyles.Footnote);
        Typography.DrawCentered(drawList, center, text, HintInk, TextStyles.Footnote);
    }

    private string SecondsLabel(long remainingMilliseconds)
    {
        var seconds = (int)((remainingMilliseconds + 999) / 1000);
        if (seconds < 0)
        {
            seconds = 0;
        }

        if (seconds != cachedSeconds)
        {
            cachedSeconds = seconds;
            cachedSecondsLabel = GameNumber.Label(seconds);
        }

        return cachedSecondsLabel;
    }

    private bool TeamStanding(int team)
    {
        var moogles = scene.Moogles;
        for (var index = 0; index < moogles.Length; index++)
        {
            if (moogles[index].Team == team && moogles[index].Alive)
            {
                return true;
            }
        }

        return false;
    }
}
