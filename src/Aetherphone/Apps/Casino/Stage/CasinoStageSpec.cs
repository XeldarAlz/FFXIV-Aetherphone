using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Stage;

internal readonly record struct CasinoStageSpec(
    string GameId,
    LocString Title,
    Backdrop Preset,
    bool Room = false,
    float DeckHeight = 0f,
    bool Practice = false,
    bool BetsRail = false,
    bool InstantAvailable = false,
    int ReturnTenths = 0,
    LocString Extra = default,
    float Warmth = 0f,
    float LampPool = 0f)
{
    public bool Deck => DeckHeight > 0f;
}

internal enum CasinoStageAction : byte
{
    None,
    Back,
    Cashier,
}

internal enum CasinoInfoRequest : byte
{
    None,
    Rules,
    Extra,
    Fairness,
}

internal readonly struct CasinoStageFrame
{
    public readonly CasinoStageLayout Layout;
    public readonly float DeltaSeconds;
    public readonly bool SnapToTruth;
    public readonly bool Instant;
    public readonly bool Focused;
    public readonly bool Blocked;
    public readonly float Phase;

    public CasinoStageFrame(in CasinoStageLayout layout, float deltaSeconds, bool snapToTruth, bool instant,
        bool focused, bool blocked, float phase)
    {
        Layout = layout;
        DeltaSeconds = deltaSeconds;
        SnapToTruth = snapToTruth;
        Instant = instant;
        Focused = focused;
        Blocked = blocked;
        Phase = phase;
    }

    public Rect Safe => Layout.Safe;

    public Rect Deck => Layout.Deck;

    public Rect Body => Layout.Body;

    public Rect Full => Layout.Full;
}
