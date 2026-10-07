namespace Aetherphone.Apps.Games.Pinball;

internal enum PinballEventKind : byte
{
    Launch,
    Bumper,
    Sling,
    Target,
    BankDown,
    Spinner,
    TopLane,
    LanesComplete,
    Inlane,
    Outlane,
    RampMade,
    Jackpot,
    JackpotLit,
    JackpotRaised,
    Locked,
    Multiball,
    SaucerHold,
    SaucerKick,
    SkillShot,
    BallSaved,
    ExtraBall,
    Drain,
    Bonus,
    ShootAgain,
    NewBall,
    TiltWarning,
    Tilt,
    FlipperHit,
}

internal readonly struct PinballEvent
{
    public readonly PinballEventKind Kind;
    public readonly Vector2 Position;
    public readonly int Value;
    public readonly int Index;

    public PinballEvent(PinballEventKind kind, Vector2 position, int value, int index)
    {
        Kind = kind;
        Position = position;
        Value = value;
        Index = index;
    }
}
