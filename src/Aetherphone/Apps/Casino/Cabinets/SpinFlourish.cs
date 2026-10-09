using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class SpinFlourish
{
    private const float TailSeconds = 0.9f;
    private const float CardVeil = 0.55f;
    private const float AmountRise = 26f;
    private const float AmountGap = 22f;
    private const float ShowerRate = 40f;
    private const float FlashAlpha = 0.38f;
    private const float PunchAmount = 0.05f;
    private const float VignetteStrength = 0.5f;
    private const float VignetteSeconds = 1.4f;
    private const int ShardCount = 40;
    private const ulong ShowerSeed = 0x5B1D5EEDUL;

    private GameRandom random = GameRandom.FromSeed(ShowerSeed);
    private RollingAmount count;
    private WinTierSpec spec = WinLadder.Spec(WinTier.None);
    private Vector2 origin;
    private long amount;
    private float elapsed;
    private float total;
    private float showerLeft;
    private float showerAccumulator;
    private bool skipped;

    public bool Active => spec.Tier != WinTier.None && elapsed < total;

    public void Clear()
    {
        spec = WinLadder.Spec(WinTier.None);
        elapsed = 0f;
        total = 0f;
        showerLeft = 0f;
        count.Snap(0);
    }

    public void Start(CasinoStage stage, WinTier tier, long coins, Vector2 at)
    {
        if (tier == WinTier.None)
        {
            return;
        }

        var scale = UiScale.Current;
        spec = WinLadder.Spec(tier);
        amount = coins;
        origin = at;
        elapsed = 0f;
        skipped = false;
        showerAccumulator = 0f;
        showerLeft = spec.ShowerSeconds;
        total = spec.CountUpSeconds + (spec.Banner ? WinLadder.BannerHoldSeconds : TailSeconds);
        count.Snap(0);
        CasinoSfx.Win(spec);
        var particles = stage.Particles;
        if (spec.Sparkles > 0)
        {
            particles.Emit(CasinoLights.Sparkle(scale), origin, spec.Sparkles);
        }

        if (spec.Confetti > 0)
        {
            particles.Confetti(origin, spec.Confetti, CasinoColors.Confetti, (220f + spec.Confetti) * scale,
                4.5f * scale, 1.4f + spec.ShowerSeconds * 0.2f);
        }

        if (spec.Sweep > 0f)
        {
            CasinoLights.LightSweep(stage.Backdrop, spec.Sweep);
        }

        if (tier < WinTier.Mega)
        {
            return;
        }

        stage.Fx.Flash(CasinoColors.Money, FlashAlpha);
        stage.Fx.Punch(PunchAmount);
        if (tier < WinTier.Epic)
        {
            return;
        }

        stage.Fx.Vignette(CasinoColors.Money, VignetteStrength, VignetteSeconds);
        particles.Emit(CasinoLights.Shard(scale), origin, ShardCount);
    }

    public void Update(CasinoStage stage, float deltaSeconds, in CasinoStageLayout layout, bool snapToTruth)
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
        count.CountUp(amount, deltaSeconds, spec.CountUpSeconds);
        if (spec.BulbChase && !skipped && elapsed < spec.CountUpSeconds)
        {
            stage.Fx.EdgeGlow(0.6f);
        }

        if (showerLeft <= 0f)
        {
            return;
        }

        showerLeft -= deltaSeconds;
        var scale = UiScale.Current;
        showerAccumulator += deltaSeconds * ShowerRate;
        var drops = (int)showerAccumulator;
        showerAccumulator -= drops;
        var coin = CasinoLights.CoinShower(scale);
        var full = layout.Full;
        for (var drop = 0; drop < drops; drop++)
        {
            stage.Particles.Emit(coin, new Vector2(full.Min.X + random.NextFloat() * full.Width, full.Min.Y), 1);
        }
    }

    public void HandleSkip(in CasinoStageLayout layout)
    {
        if (!Active || skipped || elapsed < WinLadder.SkipAfterSeconds)
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
        if (spec.FullCard)
        {
            drawList.AddRectFilled(layout.Full.Min, layout.Full.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, CardVeil * fade)));
        }

        if (spec.BulbChase)
        {
            CasinoLights.BulbChase(drawList, layout.Safe, Metrics.Radius.Grouped * scale, scale,
                spec.FullCard ? phase * 2f : phase, CasinoLights.BulbPitch, CasinoColors.Money, CasinoColors.LightA,
                fade);
        }

        if (!spec.Banner)
        {
            DrawAmount(drawList, origin - new Vector2(0f, AmountRise * scale * MathF.Min(1f, elapsed * 3f)),
                count.PopScale, fade);
            return;
        }

        var bannerCenter = new Vector2(layout.Safe.Center.X, layout.Safe.Min.Y + layout.Safe.Height * 0.40f);
        var style = spec.Tier >= WinTier.Big ? TextStyles.Title1 : TextStyles.Title3;
        var banner = Typography.FitText(Loc.T(WinLadder.Banner(spec.Tier, false)), layout.Safe.Width * 0.8f, style);
        GameBanner.Draw(drawList, bannerCenter, banner, CasinoColors.Money, PhoneTheme.Default,
            MathF.Min(0.99f, elapsed / total), style);
        DrawAmount(drawList, bannerCenter + new Vector2(0f, GameBanner.HalfHeight(style) + AmountGap * scale),
            count.PopScale, fade);
    }

    private void Skip()
    {
        skipped = true;
        showerLeft = 0f;
        count.Snap(amount);
        elapsed = MathF.Max(elapsed, total - TailSeconds * 0.5f);
    }

    private void DrawAmount(ImDrawListPtr drawList, Vector2 center, float pop, float alpha)
    {
        var text = NumberText.Signed(count.Display);
        var style = TextStyles.Title2;
        var textScale = style.Scale * pop;
        var size = Typography.Measure(text, textScale, style.Weight);
        var lineHeight = Typography.LineHeight(style);
        var glyph = lineHeight * CurrencyGlyph.GlyphFraction;
        var reserve = CurrencyGlyph.Reserve(lineHeight);
        var left = center.X - (size.X + reserve) * 0.5f;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, new Vector2(left + glyph * 0.5f, center.Y), glyph, alpha);
        Typography.Draw(drawList, new Vector2(left + reserve, center.Y - size.Y * 0.5f), text,
            CasinoColors.Money with { W = alpha }, textScale, style.Weight);
    }
}
