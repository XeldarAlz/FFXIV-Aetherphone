using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal sealed class WinCelebration
{
    private const float ShowerRate = 70f;
    private const float FlashAlpha = 0.38f;
    private const float PunchAmount = 0.05f;
    private const float SlowMoFactor = 0.6f;
    private const float SlowMoSeconds = 0.5f;
    private const float VignetteStrength = 0.5f;
    private const float VignetteSeconds = 1.4f;
    private const float EdgeGlowStrength = 0.6f;
    private const float TailSeconds = 0.9f;
    private const float CardVeil = 0.55f;
    private const float AmountRise = 26f;
    private const float AmountGap = 22f;
    private const float SkipHintGap = 34f;
    private const int RingBursts = 3;
    private const int ShardCount = 40;
    private const ulong FlourishSeed = 0x6A3BA5EEDUL;

    private readonly ParticleSystem particles;
    private readonly ScreenFx fx;
    private readonly StageBackdrop backdrop;

    private GameRandom random = GameRandom.FromSeed(FlourishSeed);
    private RollingAmount count;
    private WinTierSpec spec = WinLadder.Spec(WinTier.None);
    private Vector2 origin;
    private long amount;
    private bool jackpot;
    private bool instant;
    private float elapsed;
    private float showerLeft;
    private float showerAccumulator;
    private float total;
    private bool skipped;

    public WinCelebration(ParticleSystem particles, ScreenFx fx, StageBackdrop backdrop)
    {
        this.particles = particles;
        this.fx = fx;
        this.backdrop = backdrop;
    }

    public bool Active => spec.Tier != WinTier.None && elapsed < total;

    public WinTier Tier => Active ? spec.Tier : WinTier.None;

    public bool Blocking => Active && spec.FullCard && !skipped;

    public long Shown => count.Display;

    public void Reseed(ulong seed)
    {
        random = GameRandom.FromSeed(seed);
        particles.Reseed(seed ^ FlourishSeed);
    }

    public WinTier Celebrate(long stake, long payout, Vector2 at, bool instantMode, bool isJackpot = false)
    {
        var tier = WinLadder.TierFor(stake, payout, isJackpot);
        if (tier == WinTier.None)
        {
            return tier;
        }

        Start(tier, payout, at, instantMode, isJackpot);
        return tier;
    }

    public void Start(WinTier tier, long payout, Vector2 at, bool instantMode, bool isJackpot)
    {
        spec = WinLadder.Spec(tier);
        amount = payout;
        origin = at;
        jackpot = isJackpot;
        instant = instantMode;
        elapsed = 0f;
        skipped = false;
        showerAccumulator = 0f;
        count.Snap(0);
        var scale = UiScale.Current;
        if (instant)
        {
            total = WinLadder.InstantPopSeconds;
            showerLeft = 0f;
            count.Snap(amount);
            CasinoSfx.Play(Core.Notifications.UiSound.WinSmall);
            return;
        }

        total = spec.CountUpSeconds + (spec.Banner ? WinLadder.BannerHoldSeconds : TailSeconds);
        showerLeft = spec.ShowerSeconds;
        CasinoSfx.Win(spec);
        Burst(scale);
        if (spec.Sweep > 0f)
        {
            CasinoLights.LightSweep(backdrop, spec.Sweep);
        }

        if (tier >= WinTier.Mega)
        {
            fx.Flash(CasinoColors.Money, FlashAlpha);
            fx.Punch(PunchAmount);
        }

        if (tier >= WinTier.Epic)
        {
            fx.Vignette(CasinoColors.Money, VignetteStrength, VignetteSeconds);
        }

        if (tier == WinTier.Legendary)
        {
            fx.SlowMo(SlowMoFactor, SlowMoSeconds);
        }
    }

    public void Skip()
    {
        if (!Active)
        {
            return;
        }

        skipped = true;
        showerLeft = 0f;
        count.Snap(amount);
        elapsed = MathF.Max(elapsed, total - TailSeconds * 0.5f);
    }

    public void Clear()
    {
        spec = WinLadder.Spec(WinTier.None);
        elapsed = 0f;
        total = 0f;
        showerLeft = 0f;
        amount = 0;
        count.Snap(0);
    }

    public void Update(float deltaSeconds, in CasinoStageLayout layout, bool snapToTruth)
    {
        if (!Active)
        {
            return;
        }

        if (snapToTruth)
        {
            Skip();
        }

        elapsed += deltaSeconds;
        if (instant)
        {
            return;
        }

        count.CountUp(amount, deltaSeconds, spec.CountUpSeconds);
        if (spec.BulbChase && !skipped && elapsed < spec.CountUpSeconds)
        {
            fx.EdgeGlow(EdgeGlowStrength);
        }

        if (showerLeft > 0f)
        {
            showerLeft -= deltaSeconds;
            Shower(deltaSeconds, layout.Full, UiScale.Current);
        }
    }

    public void HandleSkip(in CasinoStageLayout layout)
    {
        if (!Active || instant || skipped || elapsed < WinLadder.SkipAfterSeconds)
        {
            return;
        }

        var area = spec.FullCard ? layout.Full : layout.Safe;
        if (UiInteract.Click(area.Min, area.Max, UiInteract.Hover(area.Min, area.Max)))
        {
            Skip();
        }
    }

    public void Draw(ImDrawListPtr drawList, in CasinoStageLayout layout, float phase, float scale)
    {
        if (!Active)
        {
            return;
        }

        var fade = Math.Clamp((total - elapsed) / TailSeconds, 0f, 1f);
        if (instant)
        {
            DrawAmount(drawList, origin, 1f + 0.25f * MathF.Sin(MathF.PI * elapsed / total), 1f);
            return;
        }

        if (spec.FullCard)
        {
            drawList.AddRectFilled(layout.Full.Min, layout.Full.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, CardVeil * fade)));
        }

        if (spec.BulbChase)
        {
            var chase = spec.FullCard ? phase * 2f : phase;
            CasinoLights.BulbChase(drawList, layout.Safe, Metrics.Radius.Grouped * scale, scale, chase,
                CasinoLights.BulbPitch, CasinoColors.Money, CasinoColors.LightA, fade);
        }

        if (!spec.Banner)
        {
            DrawAmount(drawList, origin - new Vector2(0f, AmountRise * scale * MathF.Min(1f, elapsed * 3f)),
                count.PopScale, fade);
            return;
        }

        var bannerCenter = new Vector2(layout.Safe.Center.X, layout.Safe.Min.Y + layout.Safe.Height * 0.40f);
        var style = spec.Tier >= WinTier.Big ? TextStyles.Title1 : TextStyles.Title3;
        var progress = MathF.Min(0.99f, elapsed / total);
        var banner = Typography.FitText(Loc.T(WinLadder.Banner(spec.Tier, jackpot)), layout.Safe.Width * 0.8f, style);
        GameBanner.Draw(drawList, bannerCenter, banner, CasinoColors.Money, PhoneTheme.Default, progress, style);
        var amountCenter = bannerCenter + new Vector2(0f, GameBanner.HalfHeight(style) + AmountGap * scale);
        DrawAmount(drawList, amountCenter, count.PopScale, fade);
        if (spec.FullCard && elapsed >= WinLadder.SkipAfterSeconds && !skipped)
        {
            var hint = Typography.FitText(Loc.T(L.Strip.TapToSkip), layout.Safe.Width, TextStyles.Footnote);
            Typography.DrawCentered(drawList, amountCenter + new Vector2(0f, SkipHintGap * scale), hint,
                StageText.Strong with { W = fade }, TextStyles.Footnote);
        }
    }

    private void DrawAmount(ImDrawListPtr drawList, Vector2 center, float pop, float alpha)
    {
        var text = NumberText.Signed(count.Display);
        var style = TextStyles.Title2;
        var textScale = style.Scale * pop;
        var size = Typography.Measure(text, textScale, style.Weight);
        var lineHeight = Typography.LineHeight(style);
        var glyph = lineHeight * CurrencyGlyph.GlyphFraction;
        var width = size.X + CurrencyGlyph.Reserve(lineHeight);
        var left = center.X - width * 0.5f;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Chips, new Vector2(left + glyph * 0.5f, center.Y), glyph, alpha);
        Typography.Draw(drawList, new Vector2(left + CurrencyGlyph.Reserve(lineHeight), center.Y - size.Y * 0.5f), text,
            CasinoColors.Money with { W = alpha }, textScale, style.Weight);
    }

    private void Burst(float scale)
    {
        if (spec.Sparkles > 0)
        {
            particles.Emit(CasinoLights.Sparkle(scale), origin, spec.Sparkles);
        }

        if (spec.Confetti > 0)
        {
            particles.Confetti(origin, spec.Confetti, CasinoColors.Confetti, (220f + spec.Confetti) * scale, 4.5f * scale,
                1.4f + spec.ShowerSeconds * 0.2f);
        }

        if (spec.Tier >= WinTier.Epic)
        {
            particles.Emit(CasinoLights.Shard(scale), origin, ShardCount);
        }

        if (spec.Tier == WinTier.Legendary)
        {
            for (var ring = 0; ring < RingBursts; ring++)
            {
                particles.Emit(CasinoLights.Ring(scale), origin, 1);
            }
        }
    }

    private void Shower(float deltaSeconds, Rect full, float scale)
    {
        showerAccumulator += deltaSeconds * ShowerRate;
        var drops = (int)showerAccumulator;
        showerAccumulator -= drops;
        var coin = CasinoLights.CoinShower(scale);
        for (var drop = 0; drop < drops; drop++)
        {
            var x = full.Min.X + random.NextFloat() * full.Width;
            particles.Emit(coin, new Vector2(x, full.Min.Y - 6f * scale), 1);
        }
    }
}
