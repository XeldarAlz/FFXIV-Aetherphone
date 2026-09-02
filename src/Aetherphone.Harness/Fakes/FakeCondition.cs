using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeCondition : ICondition
{
    private const int EntryCount = 104;

    public event ICondition.ConditionChangeDelegate? ConditionChange { add { } remove { } }

    public int MaxEntries => EntryCount;

    public nint Address => 0;

    public bool this[int flag] => false;

    public bool this[ConditionFlag flag] => false;

    public IReadOnlySet<ConditionFlag> AsReadOnlySet() => new HashSet<ConditionFlag>();

    public bool Any() => false;

    public bool Any(params ConditionFlag[] flags) => false;

    public bool AnyExcept(params ConditionFlag[] except) => false;

    public bool OnlyAny(params ConditionFlag[] other) => false;

    public bool EqualTo(params ConditionFlag[] other) => other.Length == 0;
}
