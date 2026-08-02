using Dalamud.Game.Gui.NamePlate;

namespace Aetherphone.Core.Video;

// Adapter over VideoEngine (the ported AlphaChannel engine, Voudi, GPL-3.0) that keeps the public
// contract the rest of AetherStream depends on. The old companion-scanning ScreenController this
// replaced tracked which summoned Carbuncle belonged to which player; there is no companion, VFX,
// or Penumbra dependency at all anymore (see port/alphachannel-engine Stage 4, and the later
// world-anchored ScreenPainter revamp), so Engine.IsActive is the only state signal left.
internal sealed class ScreenController : IDisposable
{
    private readonly Func<bool> hideNameplates;
    private const float NameplateHideRadius = 3.0f;

    internal VideoEngine Engine { get; }

    public ScreenController(Func<bool> hideNameplates)
    {
        this.hideNameplates = hideNameplates;
        Engine = new VideoEngine();
        Plugin.NamePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;
    }

    public void OnFrameworkUpdate() => Engine.OnFrameworkUpdate();

    // Ported from AlphaChannel's Core.OnNamePlateUpdate/HideNearbyNameplates (Voudi, GPL-3.0),
    // distance-gated against the screen's own world position now rather than a companion's or the
    // local player's - the screen renders wherever the user placed it, so that's where nameplate
    // clutter would overlap it. Reuses the same NamePlateStripper CameraApp uses.
    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!hideNameplates() || !Engine.IsActive)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            var gameObject = handler.GameObject;
            if (gameObject is null)
            {
                continue;
            }

            if (Vector3.Distance(gameObject.Position, Engine.ScreenPosition) <= NameplateHideRadius)
            {
                NamePlateStripper.Strip(handler);
                handler.VisibilityFlags = 0;
            }
        }
    }

    public void Dispose()
    {
        Plugin.NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
        Engine.Dispose();
    }
}
