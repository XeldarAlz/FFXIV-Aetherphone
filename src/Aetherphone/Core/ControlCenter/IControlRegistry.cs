namespace Aetherphone.Core.ControlCenter;

internal interface IControlRegistry
{
    IReadOnlyList<IControlModule> Modules { get; }
    bool TryGet(string id, out IControlModule module);
}
