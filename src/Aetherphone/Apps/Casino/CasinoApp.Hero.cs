using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float WordmarkHeight = 19f;
    private const float WordmarkFlickerPeriod = 6.5f;

    private RollingAmount jackpotRoll;

    private void DrawWordmark(ImDrawListPtr drawList, Rect body, float scale)
    {
        var rowCenter = body.Min.Y - (NavBarMetrics.InlineHeight * 0.5f + NavBarMetrics.UntitledTopGap) * scale;
        var height = WordmarkHeight * scale;
        var width = CasinoSigns.Measure(CasinoSign.Gamba, height);
        var left = body.Min.X + AppSurface.SidePadding * scale;
        CasinoSigns.Draw(drawList, CasinoSign.Gamba, new Vector2(left + width * 0.5f, rowCenter), height,
            CasinoColors.LightA, NeonFlicker.Level(floorLights.Phase, WordmarkFlickerPeriod));
    }

    private float DrawHeroBlock(ImDrawListPtr drawList, Vector2 origin, float width, float delta, float scale)
    {
        var state = casino.State;
        var progress = casino.HasFeature(CasinoFeatures.Levels) ? casino.Progress : null;
        var levelFraction = 0f;
        if (progress is not null)
        {
            var span = progress.NextLevelXp - progress.LevelStartXp;
            levelFraction = span <= 0 ? 1f : Math.Clamp((float)(progress.Xp - progress.LevelStartXp) / span, 0f, 1f);
        }

        var bonuses = casino.HasFeature(CasinoFeatures.Bonus);
        var spinReady = DailySpinStatus.OffersWheel(DailySpinStatus.Of(casinoSpin.Answer));
        var model = new StripHeroModel(FloorBalance(), state?.Sitting is not null, progress?.Level ?? 1,
            levelFraction, casino.Ceiling.LevelCap, progress is not null,
            bonuses ? casino.BonusFor(CasinoBonusKinds.Timed) : null,
            bonuses ? casino.BonusFor(CasinoBonusKinds.Streak) : null, spinReady, casino.ClaimingBonus.Length > 0,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), casino.Rate);
        var action = stripHero.Draw(drawList, ui, model, origin, width, delta, scale, out var bottom);
        switch (action)
        {
            case StripHeroAction.GetChips:
            case StripHeroAction.Cashier:
                cashier.Open();
                break;
            case StripHeroAction.ClaimTimed:
                bonusShelf.Claim(CasinoBonusKinds.Timed, stripHero.TimedCenter);
                break;
            case StripHeroAction.ClaimStreak:
                bonusShelf.Claim(CasinoBonusKinds.Streak, stripHero.StreakCenter);
                break;
            case StripHeroAction.FreeSpin:
                OpenGame(CasinoGames.DailySpin, stripHero.SpinSource);
                break;
        }

        return bottom;
    }

    private float DrawJackpot(ImDrawListPtr drawList, Vector2 origin, float width, float delta, float scale)
    {
        if (JackpotMarquee.Draw(drawList, origin, width, ref jackpotRoll, casino.Jackpot, floorLights.Phase, delta,
                scale, out var pressed))
        {
            OpenGame(CasinoGames.SlotsBird, pressed);
        }

        return origin.Y + JackpotMarquee.BlockHeight(scale);
    }
}
