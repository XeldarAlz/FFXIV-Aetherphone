using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Framework;

internal sealed class LegacyGameAdapter : IMiniGame
{
    private readonly ILegacyMiniGame game;
    private readonly GameSpec spec;

    public LegacyGameAdapter(ILegacyMiniGame game, LocString title, LocString hook)
    {
        this.game = game;
        spec = new GameSpec(game.Id, title, game.Genre, hook, Backdrop.Nebula, HudStyle.Standard, ScoreKind.Score,
            clocked: game.RunsOnAClock, landscape: game.WantsLandscape, legacy: true);
    }

    public GameSpec Spec => spec;

    public string Title => game.Title;

    public Vector4 Accent => game.Accent;

    public bool RunsOnAClock => game.RunsOnAClock;

    public ILegacyMiniGame Game => game;

    public void Start(in GameStart start)
    {
        game.Open();
    }

    public void Close()
    {
        game.Close();
    }

    public void Draw(in GameContext context)
    {
        var landscape = spec.Landscape && context.Full.IsLandscape();
        var body = landscape ? context.Full : StageLayout.LegacyBody(context.Full, UiScale.Current);
        game.Draw(context.WithBody(body, context.DeltaSeconds));
    }

    public void Dispose()
    {
        game.Dispose();
    }
}
