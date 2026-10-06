using Aetherphone.Core;
using Aetherphone.Core.Theme;

namespace Aetherphone.Apps.Games.Framework;

internal readonly struct GameContext
{
    public readonly Rect Full;
    public readonly Rect Safe;
    public readonly PhoneTheme Theme;
    public readonly float DeltaSeconds;
    public readonly float RawDeltaSeconds;
    public readonly GameSession Session;
    public readonly HudModel Hud;
    public readonly ScreenFx Fx;
    public readonly StageBackdrop Backdrop;

    public GameContext(Rect full, Rect safe, PhoneTheme theme, float deltaSeconds, float rawDeltaSeconds,
        GameSession session, HudModel hud, ScreenFx fx, StageBackdrop backdrop)
    {
        Full = full;
        Safe = safe;
        Theme = theme;
        DeltaSeconds = deltaSeconds;
        RawDeltaSeconds = rawDeltaSeconds;
        Session = session;
        Hud = hud;
        Fx = fx;
        Backdrop = backdrop;
    }
}
