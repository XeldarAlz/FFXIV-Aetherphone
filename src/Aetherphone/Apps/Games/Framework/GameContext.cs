using Aetherphone.Core;
using Aetherphone.Core.Games;
using Aetherphone.Core.Theme;

namespace Aetherphone.Apps.Games.Framework;

internal readonly struct GameContext
{
    public readonly Rect Full;
    public readonly Rect Safe;
    public readonly Rect Body;
    public readonly PhoneTheme Theme;
    public readonly GameStatsStore Stats;
    public readonly float DeltaSeconds;
    public readonly float RawDeltaSeconds;
    public readonly GameSession Session;
    public readonly HudModel Hud;
    public readonly ScreenFx Fx;
    public readonly StageBackdrop Backdrop;

    public GameContext(Rect full, Rect safe, Rect body, PhoneTheme theme, GameStatsStore stats, float deltaSeconds,
        float rawDeltaSeconds, GameSession session, HudModel hud, ScreenFx fx, StageBackdrop backdrop)
    {
        Full = full;
        Safe = safe;
        Body = body;
        Theme = theme;
        Stats = stats;
        DeltaSeconds = deltaSeconds;
        RawDeltaSeconds = rawDeltaSeconds;
        Session = session;
        Hud = hud;
        Fx = fx;
        Backdrop = backdrop;
    }

    public GameContext WithBody(Rect body, float deltaSeconds) =>
        new(Full, Safe, body, Theme, Stats, deltaSeconds, RawDeltaSeconds, Session, Hud, Fx, Backdrop);
}
