using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed class CashierBonusShelf
{
    private const float ShowerSeconds = 1.1f;
    private const float ShowerRate = 70f;
    private const int ShowerBurst = 22;

    private readonly CasinoStore store;
    private readonly ParticleSystem shower = new(240);
    private readonly Vector2[] claimCenters = new Vector2[CasinoBonusKinds.All.Length];
    private Emitter emitter;
    private float showerLeft;
    private Vector2 showerOrigin;
    private string refusal = string.Empty;

    public CashierBonusShelf(CasinoStore store)
    {
        this.store = store;
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
        shower.Emit(CasinoLights.CoinFountain(scale), showerOrigin, ShowerBurst);
        emitter = CasinoLights.CoinShowerEmitter(scale, ShowerRate);
        showerLeft = ShowerSeconds;
        UiFeedback.Play(UiSound.CoinShower);
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
        emitter.Reset();
        showerLeft = 0f;
    }

    public void Update(float delta, float scale)
    {
        ConsumeResults(scale);
        shower.Update(delta);
        if (showerLeft <= 0f)
        {
            return;
        }

        emitter.Advance(delta, showerOrigin, shower);
        showerLeft -= delta;
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
            refusal = Loc.T(CasinoReasons.MessageFor(CasinoReasons.Unreachable));
        }

        var result = store.TakeBonusResult();
        if (result is null)
        {
            return;
        }

        if (!result.Granted)
        {
            refusal = Loc.T(CasinoReasons.MessageFor(result.Reason.Length > 0 ? result.Reason : CasinoReasons.Unreachable));
            return;
        }

        var kind = CasinoBonusKinds.IndexOf(result.Kind);
        showerOrigin = kind >= 0 ? claimCenters[kind] : showerOrigin;
        Shower(showerOrigin, scale);
    }
}
