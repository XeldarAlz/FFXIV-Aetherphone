using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed class CashierRain
{
    private const int Capacity = 64;
    private const int Drops = 22;
    private const float EmitSeconds = 0.35f;
    private const float DropBoost = 2f;
    private const float EdgeInset = 0.04f;

    private const ImGuiWindowFlags LayerFlags = ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground |
                                                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

    private static readonly Vector4 ChipFace = new(0.94f, 0.27f, 0.37f, 1f);

    private readonly ParticleSystem particles = new(Capacity);
    private GameRandom random = GameRandom.Fresh();
    private CurrencyKind kind;
    private float emitLeft;
    private float pending;

    public bool Active => emitLeft > 0f || particles.ActiveCount > 0;

    public void Start(CurrencyKind currency)
    {
        kind = currency;
        emitLeft = EmitSeconds;
        pending = 0f;
    }

    public void Clear()
    {
        emitLeft = 0f;
        pending = 0f;
        particles.Clear();
    }

    public void Draw(Rect screen, float deltaSeconds)
    {
        if (!Active)
        {
            return;
        }

        Emit(screen, deltaSeconds);
        particles.Update(deltaSeconds);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##cashierRain", screen.Size, false, LayerFlags))
        {
            particles.Draw(ImGui.GetWindowDrawList(), 1f);
        }
    }

    internal static ParticleSpec SpecFor(CurrencyKind currency, float scale)
    {
        var coin = CasinoLights.CoinShower(scale * DropBoost);
        if (currency == CurrencyKind.Coins)
        {
            return coin;
        }

        return new ParticleSpec(ChipFace, CasinoColors.InkTitle, coin.Size, coin.Speed, coin.Life, coin.Gravity,
            coin.Drag, coin.Spin, coin.Spread, coin.Direction, coin.Shape, coin.Curve, coin.Additive);
    }

    private void Emit(Rect screen, float deltaSeconds)
    {
        if (emitLeft <= 0f)
        {
            return;
        }

        var step = MathF.Min(emitLeft, deltaSeconds);
        emitLeft -= step;
        pending += step * Drops / EmitSeconds;
        var count = (int)pending;
        pending -= count;
        if (count == 0)
        {
            return;
        }

        var spec = SpecFor(kind, UiScale.Current);
        var span = screen.Width * (1f - EdgeInset * 2f);
        for (var drop = 0; drop < count; drop++)
        {
            var x = screen.Min.X + screen.Width * EdgeInset + random.NextFloat() * span;
            particles.Emit(spec, new Vector2(x, screen.Min.Y - spec.Size), 1);
        }
    }
}
