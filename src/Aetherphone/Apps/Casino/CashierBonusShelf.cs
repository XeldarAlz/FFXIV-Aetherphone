using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed class CashierBonusShelf
{
    private const int ConfettiBurst = 28;
    private const int SparkleBurst = 10;
    private const float ConfettiSpeed = 170f;
    private const float ConfettiSize = 4f;
    private const float ConfettiLife = 1.1f;

    private static readonly Vector4[] ChipConfetti =
    {
        CasinoColors.LightA,
        CasinoColors.LightB,
        CasinoColors.InkTitle,
    };

    private readonly CasinoStore store;
    private readonly ParticleSystem shower = new(240);
    private readonly Vector2[] claimCenters = new Vector2[CasinoBonusKinds.All.Length];
    private Vector2 showerOrigin;
    private string refusal = string.Empty;
    private int seenRounds;
    private int autoClaimDay = -1;
    private bool autoClaiming;

    public CashierBonusShelf(CasinoStore store)
    {
        this.store = store;
    }

    public void ClaimStreakAfterRounds(int settledRounds)
    {
        if (settledRounds == seenRounds)
        {
            return;
        }

        seenRounds = settledRounds;
        var today = CoinDay.Index(DateTime.UtcNow);
        if (!AutoClaimsStreak(store.HasFeature(CasinoFeatures.Bonus) ? store.BonusFor(CasinoBonusKinds.Streak) : null,
                autoClaimDay, today) || store.ClaimingBonus.Length > 0)
        {
            return;
        }

        autoClaimDay = today;
        autoClaiming = true;
        refusal = string.Empty;
        store.ClaimBonus(CasinoBonusKinds.Streak);
    }

    internal static bool AutoClaimsStreak(CasinoBonusDto? streak, int claimedDay, int today)
    {
        return streak is { Ready: true, Eligible: true } && claimedDay != today;
    }

    public void Claim(string kind, Vector2 origin)
    {
        var index = CasinoBonusKinds.IndexOf(kind);
        if (index < 0 || store.ClaimingBonus.Length > 0)
        {
            return;
        }

        claimCenters[index] = origin;
        refusal = string.Empty;
        store.ClaimBonus(kind);
        UiFeedback.Play(UiSound.CasinoChips);
    }

    public void Shower(Vector2 origin, float scale)
    {
        showerOrigin = origin;
        shower.Confetti(showerOrigin, ConfettiBurst, ChipConfetti, ConfettiSpeed * scale, ConfettiSize * scale,
            ConfettiLife);
        shower.Emit(CasinoLights.ChipSparkle(scale), showerOrigin, SparkleBurst);
        UiFeedback.Play(UiSound.CasinoChips);
    }

    public string TakeRefusal()
    {
        var taken = refusal;
        refusal = string.Empty;
        return taken;
    }

    public void Reset()
    {
        refusal = string.Empty;
        shower.Clear();
    }

    public void Update(float delta, float scale)
    {
        ConsumeResults(scale);
        shower.Update(delta);
    }

    public void DrawShower(ImDrawListPtr drawList, float scale)
    {
        shower.Draw(drawList, scale);
    }

    internal static long SecondsUntil(CasinoBonusDto bonus, long nowUnix)
    {
        return bonus.Ready || bonus.NextAtUnix <= 0 ? 0 : Math.Max(0, bonus.NextAtUnix - nowUnix);
    }

    private void ConsumeResults(float scale)
    {
        if (store.TakeBonusFailure())
        {
            if (autoClaiming)
            {
                autoClaiming = false;
            }
            else
            {
                refusal = Loc.T(CasinoReasons.MessageFor(CasinoReasons.Unreachable));
            }
        }

        var result = store.TakeBonusResult();
        if (result is null)
        {
            return;
        }

        var automatic = autoClaiming && string.Equals(result.Kind, CasinoBonusKinds.Streak, StringComparison.Ordinal);
        if (automatic)
        {
            autoClaiming = false;
        }

        if (!result.Granted)
        {
            if (!automatic)
            {
                refusal = Loc.T(CasinoReasons.MessageFor(result.Reason.Length > 0 ? result.Reason
                    : CasinoReasons.Unreachable));
            }

            return;
        }

        if (automatic)
        {
            UiFeedback.Play(UiSound.CasinoChips);
            ShellToast.Show(Loc.T(L.Strip.StreakLanded, NumberText.Compact(result.Amount)));
            return;
        }

        var kind = CasinoBonusKinds.IndexOf(result.Kind);
        showerOrigin = kind >= 0 ? claimCenters[kind] : showerOrigin;
        Shower(showerOrigin, scale);
    }
}
