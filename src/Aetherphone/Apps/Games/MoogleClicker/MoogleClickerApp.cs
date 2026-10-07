using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.MoogleClicker;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.MoogleClicker;

internal sealed class MoogleClickerApp : IMiniGame
{
    private const string GameId = "moogleclicker";
    private const string MoogleSurfaceId = "moogleclicker.moogle";
    private const ulong IdleSeed = 11;
    private const float BannerSeconds = 1.9f;
    private const float HitWidth = 1.2f;
    private const float HitTop = 1.95f;
    private const float HitBottom = 1.5f;
    private const float MinionFraction = 0.045f;
    private const float MinionMinSize = 13f;
    private const float MinionReach = 1.9f;
    private const float MinionFade = 0.06f;
    private const float MinionTrailRate = 16f;
    private const float BankPopDecay = 3.5f;
    private const float BankPop = 0.12f;
    private const float NutRateScale = 1.1f;
    private const float NutMinRate = 0.4f;
    private const float NutMaxRate = 7f;
    private const float SmoothingSeconds = 1.5f;
    private const float UrgentBuffSeconds = 5f;
    private const int FrenzyBannerMultiplier = (int)KupoWorkshop.FrenzyMultiplier;
    private const int TapFrenzyBannerMultiplier = (int)KupoWorkshop.TapFrenzyMultiplier;

    private static readonly GameSpec StageSpec = new(GameId, L.MoogleClicker.Title, GameGenre.Arcade,
        L.MoogleClicker.Hook, Backdrop.Meadow, HudStyle.Standard, ScoreKind.Level);

    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Fluff = new(1f, 0.98f, 0.96f, 0.95f);
    private static readonly Vector4 PomPink = new(0.98f, 0.45f, 0.58f, 1f);
    private static readonly Vector4 NutBrown = new(0.66f, 0.44f, 0.24f, 0.9f);

    private static readonly Vector4[] Celebration =
    {
        new(1f, 0.84f, 0.36f, 1f), new(0.98f, 0.45f, 0.58f, 1f), new(0.62f, 0.48f, 0.92f, 1f),
        new(0.45f, 0.82f, 0.95f, 1f), new(1f, 1f, 1f, 1f),
    };

    private readonly MoogleClickerService service;
    private readonly MoogleClickerBoard board = new();
    private readonly MoogleClickerShop shop = new();
    private readonly MoogleClickerUpgrades upgrades = new();
    private readonly MoogleClickerSheets sheets = new();
    private readonly KupoLabels labels = new();
    private readonly ParticleSystem ambient = new(128);
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private GameRandom visuals = GameRandom.Fresh();
    private TextSlot rateLabel;
    private TextSlot lumpLabel;
    private TextSlot ledgerLabel;
    private LabelSlot frenzyLabel;
    private LabelSlot tapFrenzyLabel;
    private string bannerText = string.Empty;
    private Vector4 bannerColor = Gold;
    private float banner = 1f;
    private float idleTime;
    private float squashAge = 10f;
    private float bankPop;
    private float nutClock;
    private float trailClock;
    private bool recorded;

    public MoogleClickerApp(MoogleClickerService service)
    {
        this.service = service;
        board.Reset(GameRandom.FromSeed(IdleSeed));
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        particles.Clear();
        ambient.Clear();
        fx.Clear();
        shop.Reset();
        upgrades.Reset();
        sheets.Close();
        banner = 1f;
        squashAge = 10f;
        bankPop = 0f;
        recorded = false;
    }

    public void Close()
    {
        particles.Clear();
        ambient.Clear();
        fx.Clear();
        sheets.Close();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var now = MoogleClickerService.NowUnixMilliseconds;
        var workshop = service.Workshop;
        idleTime += context.RawDeltaSeconds;
        var layout = MoogleClickerLayout.From(context.Full, context.Safe, scale);
        MoogleClickerRenderer.DrawMoogle(drawList, layout, idleTime, squashAge, workshop.FrenzyActive(now),
            workshop.TapFrenzyActive(now), Accent, scale);
        upgrades.Draw(drawList, layout.Upgrades, workshop, Accent, false, context.RawDeltaSeconds, scale);
        shop.Draw(drawList, layout.Shop, workshop, Accent, context.Backdrop.Ink, context.Theme, false,
            context.RawDeltaSeconds, scale);
        DrawBank(drawList, context, workshop, now, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        var now = MoogleClickerService.NowUnixMilliseconds;
        var workshop = service.Workshop;
        idleTime += raw;
        squashAge += raw;
        bankPop = MathF.Max(0f, bankPop - raw * BankPopDecay);
        var frenzy = workshop.FrenzyActive(now);
        var tapFrenzy = workshop.TapFrenzyActive(now);
        var playing = context.Session.State == StageFlow.Playing;
        var blocking = sheets.Blocking(workshop);
        var interactive = playing && !blocking;
        particles.Update(raw);
        ambient.Update(raw);
        fx.Update(raw);
        board.Update(blocking ? 0f : context.DeltaSeconds);
        banner = GameBanner.Advance(banner, raw, BannerSeconds);
        var layout = MoogleClickerLayout.From(context.Full, context.Safe, scale);
        EmitNuts(layout, workshop.KupoPerSecond(now), raw, scale);
        ambient.Draw(drawList, scale);
        var moogle = layout.Punched(fx.ShakeOffset(scale), context.Fx.PlateScale);
        MoogleClickerRenderer.DrawMoogle(drawList, moogle, idleTime, squashAge, frenzy, tapFrenzy, Accent, scale);
        var caught = DrawMinion(drawList, layout, interactive, scale, context);
        HandleMoogle(moogle, interactive && !caught, scale, context);
        HandlePick(upgrades.Draw(drawList, layout.Upgrades, workshop, Accent, interactive, raw, scale), scale, context);
        HandleShop(shop.Draw(drawList, layout.Shop, workshop, Accent, context.Backdrop.Ink, context.Theme, interactive,
            raw, scale), scale, context);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, new Vector2(layout.Stage.Center.X, layout.Stage.Min.Y + layout.Stage.Height * 0.16f),
            bannerText, bannerColor, context.Theme, banner);
        DrawBank(drawList, context, workshop, now, scale);
        HandleSheet(sheets.Draw(drawList, context.Full, workshop, now, Accent, context.Theme, playing, raw, scale),
            layout, scale, context);
        if (frenzy)
        {
            context.Fx.Vignette(Gold, 0.12f + 0.06f * Pulse.Wave(Pulse.Medium), 0.3f);
        }

        if (!blocking)
        {
            FillHud(drawList, context, workshop, now, frenzy, scale);
        }
    }

    private void HandleMoogle(in MoogleClickerLayout moogle, bool interactive, float scale, in GameContext context)
    {
        if (!interactive)
        {
            return;
        }

        var center = MoogleClickerRenderer.MoogleCenter(moogle, idleTime);
        var radius = moogle.MoogleRadius;
        var area = new Rect(center - new Vector2(radius * HitWidth, radius * HitTop),
            center + new Vector2(radius * HitWidth, radius * HitBottom));
        PressSurface.Claim(MoogleSurfaceId, area, out var activated);
        var pointer = ImGui.GetMousePos();
        if (!activated || context.ChromeHit(pointer))
        {
            return;
        }

        var outcome = board.Tap();
        var gained = service.Tap(outcome.KupoMultiplier);
        squashAge = 0f;
        bankPop = 1f;
        UiFeedback.Play(UiSound.GameHitSoft);
        particles.Emit(new ParticleSpec(Fluff, Fluff with { W = 0f }, 3.2f, 150f * scale, 0.55f, 120f * scale,
            shape: ParticleShape.GlowCircle), pointer, 6);
        particles.Emit(new ParticleSpec(PomPink, PomPink with { W = 0f }, 2.4f, 110f * scale, 0.5f,
            shape: ParticleShape.Star, spin: 6f), pointer, 3);
        var label = labels.Plus(KupoFormat.Amount(gained));
        var textPoint = pointer - new Vector2(0f, 14f * scale);
        if (!outcome.Critical)
        {
            fx.AddText(label, textPoint, White, 1.05f);
            fx.AddTrauma(0.03f);
            if (outcome.TierUp && outcome.Multiplier >= 2)
            {
                GameSfx.ComboTierUp();
            }

            return;
        }

        fx.AddText(label, textPoint, Gold, 1.5f);
        fx.AddText(Loc.T(L.MoogleClicker.Critical), textPoint - new Vector2(0f, 26f * scale), Gold, 0.9f);
        particles.Emit(new ParticleSpec(Gold, White with { W = 0f }, 3.4f, 260f * scale, 0.7f, 200f * scale,
            shape: ParticleShape.Star, spin: 8f, additive: true), pointer, 14);
        fx.Shockwave(center, radius * 1.7f, Gold, 0.45f, 3f, radius * 0.6f);
        fx.AddTrauma(0.18f);
        context.Fx.Punch(0.05f);
        UiFeedback.Play(UiSound.GameCollect);
    }

    private bool DrawMinion(ImDrawListPtr drawList, in MoogleClickerLayout layout, bool interactive, float scale,
        in GameContext context)
    {
        if (!board.MinionActive)
        {
            trailClock = 0f;
            return false;
        }

        var point = layout.StagePoint(board.MinionPoint);
        var size = MathF.Max(MinionMinSize * scale, layout.Stage.Width * MinionFraction);
        var progress = board.MinionProgress;
        var alpha = Math.Clamp(MathF.Min(progress, 1f - progress) / MinionFade, 0f, 1f);
        trailClock += context.DeltaSeconds * MinionTrailRate;
        while (trailClock >= 1f)
        {
            trailClock -= 1f;
            particles.Emit(new ParticleSpec(Gold, White with { W = 0f }, 2.2f, 24f * scale, 0.7f, 20f * scale,
                shape: ParticleShape.Star, spin: 5f, additive: true), point, 1);
        }

        MoogleClickerRenderer.DrawMinion(drawList, point, size, board.MinionFromLeft, idleTime, alpha, scale);
        if (!interactive || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return false;
        }

        var reach = new Vector2(size * MinionReach, size * MinionReach);
        var pointer = ImGui.GetMousePos();
        if (!UiInteract.Hover(point - reach, point + reach) || context.ChromeHit(pointer))
        {
            return false;
        }

        var reward = board.Catch();
        var gained = service.Reward(reward);
        OnReward(reward, gained, point, scale, context);
        return true;
    }

    private void OnReward(KupoReward reward, double gained, Vector2 point, float scale, in GameContext context)
    {
        UiFeedback.Play(UiSound.GamePowerUp);
        particles.Confetti(point, 40, Celebration, 260f * scale, 4f, 1.1f, ParticleSystem.ConfettiGravity * scale);
        fx.Shockwave(point, 70f * scale, Gold, 0.5f, 3.2f);
        fx.AddTrauma(0.2f);
        context.Fx.Flash(Gold, 0.22f);
        context.Fx.Sweep();
        bannerColor = Gold;
        banner = 0f;
        switch (reward)
        {
            case KupoReward.Frenzy:
                bannerText = frenzyLabel.Get(L.MoogleClicker.FrenzyBanner, FrenzyBannerMultiplier);
                return;
            case KupoReward.TapFrenzy:
                bannerText = tapFrenzyLabel.Get(L.MoogleClicker.TapFrenzyBanner, TapFrenzyBannerMultiplier);
                return;
            default:
            {
                var amount = KupoFormat.Amount(gained);
                bannerText = lumpLabel.Get(L.MoogleClicker.LumpBanner, amount);
                fx.AddText(labels.Plus(amount), point, Gold, 1.4f);
                bankPop = 1f;
                return;
            }
        }
    }

    private void HandlePick(in UpgradePick pick, float scale, in GameContext context)
    {
        if (!pick.Picked)
        {
            return;
        }

        if (pick.Denied || !service.BuyUpgrade(pick.Upgrade))
        {
            UiFeedback.Play(UiSound.GameWrong);
            upgrades.Shake(pick.Upgrade);
            return;
        }

        var tint = MoogleClickerText.UpgradeTint(pick.Upgrade, Accent);
        UiFeedback.Play(UiSound.GamePowerUp);
        particles.Emit(new ParticleSpec(tint, White with { W = 0f }, 3.6f, 220f * scale, 0.7f, 120f * scale,
            shape: ParticleShape.Star, spin: 7f, additive: true), pick.Point, 18);
        particles.Emit(new ParticleSpec(Gold, Gold with { W = 0f }, 2.6f, 160f * scale, 0.6f,
            shape: ParticleShape.Shard, spin: 9f), pick.Point, 10);
        fx.Shockwave(pick.Point, 56f * scale, tint, 0.42f, 3f);
        fx.AddTrauma(0.1f);
        context.Fx.Sweep();
        RecordPlay(context);
    }

    private void HandleShop(in ShopAction action, float scale, in GameContext context)
    {
        switch (action.Command)
        {
            case ShopCommand.Stats:
                sheets.Open(SheetKind.Stats);
                return;
            case ShopCommand.Ledger:
                sheets.Open(SheetKind.Ledger);
                return;
            case ShopCommand.Denied:
                Deny(action.Building);
                return;
            case ShopCommand.Buy:
                Buy(action, scale, context);
                return;
            default:
                return;
        }
    }

    private void Buy(in ShopAction action, float scale, in GameContext context)
    {
        var bought = service.Buy(action.Building, action.Count);
        if (bought == 0)
        {
            Deny(action.Building);
            return;
        }

        var tint = MoogleClickerText.BuildingTints[action.Building];
        shop.Flash(action.Building);
        UiFeedback.Play(UiSound.GameCollect);
        particles.Burst(action.Point, 10 + Math.Min(bought, 20), tint, 170f * scale, 3f, 0.5f,
            ParticleSystem.BurstGravity * scale);
        particles.Emit(new ParticleSpec(Gold, Gold with { W = 0f }, 2.2f, 120f * scale, 0.5f,
            shape: ParticleShape.Star, spin: 6f, additive: true), action.Point, 6);
        fx.Shockwave(action.Point, 40f * scale, tint, 0.35f, 2.6f);
        fx.AddTrauma(0.05f);
        if (service.Workshop.Owned(action.Building) == bought)
        {
            context.Fx.Sweep();
            context.Fx.Punch(0.03f);
        }

        RecordPlay(context);
    }

    private void Deny(int building)
    {
        UiFeedback.Play(UiSound.GameWrong);
        if (building >= 0)
        {
            shop.Shake(building);
        }
    }

    private void HandleSheet(SheetAction action, in MoogleClickerLayout layout, float scale, in GameContext context)
    {
        switch (action)
        {
            case SheetAction.CollectAway:
                service.DismissAway();
                UiFeedback.Play(UiSound.GameCollect);
                particles.Confetti(StageLayout.PrimaryCenter(context.Full, scale), 36, Celebration, 220f * scale, 3.4f,
                    1f, ParticleSystem.ConfettiGravity * scale);
                particles.Emit(new ParticleSpec(Gold, Gold with { W = 0f }, 3f, 240f * scale, 0.8f, 180f * scale,
                    shape: ParticleShape.Star, spin: 7f, additive: true), layout.MoogleCenter, 20);
                bankPop = 1f;
                context.Fx.Sweep();
                return;
            case SheetAction.CloseLedger:
                CloseLedger(layout, scale, context);
                return;
            default:
                return;
        }
    }

    private void CloseLedger(in MoogleClickerLayout layout, float scale, in GameContext context)
    {
        var gained = service.CloseLedger();
        if (gained <= 0d)
        {
            Deny(-1);
            return;
        }

        context.Session.Record(GameId, service.Workshop.LedgerLevel, ScoreKind.Level);
        recorded = true;
        shop.Reset();
        upgrades.Reset();
        UiFeedback.Play(UiSound.GameWin);
        particles.Confetti(new Vector2(layout.Stage.Center.X, layout.Stage.Min.Y), 120, Celebration, 340f * scale, 4.4f,
            1.6f, ParticleSystem.ConfettiGravity * scale);
        particles.Emit(new ParticleSpec(Gold, White with { W = 0f }, 4f, 300f * scale, 0.9f, 160f * scale,
            shape: ParticleShape.Star, spin: 8f, additive: true), layout.MoogleCenter, 30);
        fx.Shockwave(layout.MoogleCenter, layout.MoogleRadius * 2.4f, Gold, 0.6f, 4f);
        fx.AddTrauma(0.35f);
        context.Fx.Flash(Gold, 0.32f);
        context.Fx.Sweep();
        context.Fx.Punch(0.07f);
        bannerText = ledgerLabel.Get(L.MoogleClicker.LedgerClosed, KupoFormat.Amount(gained));
        bannerColor = Gold;
        banner = 0f;
    }

    private void RecordPlay(in GameContext context)
    {
        if (recorded)
        {
            return;
        }

        recorded = true;
        context.Session.Record(GameId, service.Workshop.LedgerLevel, ScoreKind.Level);
    }

    private void EmitNuts(in MoogleClickerLayout layout, double kupoPerSecond, float raw, float scale)
    {
        if (!(kupoPerSecond > 0d))
        {
            nutClock = 0f;
            return;
        }

        var rate = Math.Clamp(MathF.Log10((float)Math.Min(kupoPerSecond, 1e30) + 1f) * NutRateScale, NutMinRate,
            NutMaxRate);
        nutClock += raw * rate;
        while (nutClock >= 1f)
        {
            nutClock -= 1f;
            var x = layout.Stage.Min.X + visuals.NextFloat() * layout.Stage.Width;
            ambient.Emit(new ParticleSpec(NutBrown, NutBrown with { W = 0f }, 3f, 24f * scale, 2.8f, 40f * scale,
                drag: 0.5f, spin: 4f, spread: 0.6f, direction: MathF.PI * 0.5f, shape: ParticleShape.Square),
                new Vector2(x, layout.Stage.Min.Y), 1);
        }
    }

    private void DrawBank(ImDrawListPtr drawList, in GameContext context, KupoWorkshop workshop, long now, float scale)
    {
        var since = Math.Clamp((now - workshop.LastTickUnixMilliseconds) / 1000d, 0d, SmoothingSeconds);
        var shown = workshop.Kupo + workshop.KupoPerSecond(now) * since;
        MoogleClickerRenderer.DrawKupoPill(drawList, StageLayout.PrimaryCenter(context.Full, scale),
            KupoFormat.Amount(shown), Loc.Upper(Loc.T(L.MoogleClicker.Kupo)), Accent, 1f + BankPop * bankPop,
            workshop.FrenzyActive(now), scale);
    }

    private void FillHud(ImDrawListPtr drawList, in GameContext context, KupoWorkshop workshop, long now, bool frenzy,
        float scale)
    {
        if (workshop.BuffActive(now))
        {
            var left = (float)workshop.BuffSecondsLeft(now);
            var total = (float)(frenzy ? KupoWorkshop.FrenzySeconds : KupoWorkshop.TapFrenzySeconds);
            context.Hud.Timer(left, total, left <= UrgentBuffSeconds);
        }

        context.Hud.Combo(board.Combo);
        var rate = rateLabel.Get(L.MoogleClicker.PerSecond, KupoFormat.Rate(workshop.KupoPerSecond(now)));
        context.Hud.Custom(StatCapsule.Width(rate, scale));
        if (!context.Hud.CustomPlaced(0))
        {
            return;
        }

        StatCapsule.Draw(drawList, context.Hud.CustomRect(0), FontAwesomeIcon.Hourglass, rate, frenzy ? Gold : Accent,
            scale);
    }
}
