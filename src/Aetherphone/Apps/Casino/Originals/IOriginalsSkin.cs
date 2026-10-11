using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Originals;

internal readonly record struct OriginalsOutcome(long Stake, long Payout, string RoundId, bool Capped = false);

internal readonly struct OriginalsFrame
{
    public readonly CasinoStage Stage;
    public readonly CasinoOriginalsStore Originals;
    public readonly Rect World;
    public readonly float DeltaSeconds;
    public readonly float Phase;
    public readonly bool Instant;
    public readonly bool Interactive;
    public readonly bool AutoTab;

    public OriginalsFrame(CasinoStage stage, CasinoOriginalsStore originals, Rect world, float deltaSeconds,
        float phase, bool instant, bool interactive, bool autoTab)
    {
        Stage = stage;
        Originals = originals;
        World = world;
        DeltaSeconds = deltaSeconds;
        Phase = phase;
        Instant = instant;
        Interactive = interactive;
        AutoTab = autoTab;
    }
}

internal interface IOriginalsSkin : ICabinetIdle
{
    string GameId { get; }

    LocString Title { get; }

    CasinoSign Sign { get; }

    LocString Action { get; }

    LocString Hint { get; }

    bool Knob { get; }

    bool AutoAvailable { get; }

    bool Live { get; }

    bool Busy { get; }

    bool CanCashOut { get; }

    long CashOutValue { get; }

    LocString LiveSecondary { get; }

    Vector2 Focus { get; }

    void Enter();

    void Reset();

    void Snap();

    void Resume(CasinoOriginalsOpenDto open);

    void Consume(CasinoOriginalsStore originals, bool instant);

    void Advance(float deltaSeconds);

    int FillLadder(Span<LadderStep> steps, out int focus);

    void DrawWorld(ImDrawListPtr drawList, in OriginalsFrame frame, AppSkin ui);

    void DrawKnob(ImDrawListPtr drawList, Rect rect, AppSkin ui, bool changeable);

    bool Play(CasinoOriginalsStore originals, long stake, bool auto);

    void CashOut(CasinoOriginalsStore originals);

    void Secondary(CasinoOriginalsStore originals);

    void Step(CasinoOriginalsStore originals, bool auto);

    bool TakeSettled(out OriginalsOutcome outcome);

    bool TakeNotice(out LocString notice);
}
