using System.Collections;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeObjectTable : IObjectTable
{
    public nint Address => 0;

    public int Length => 0;

    public IPlayerCharacter? LocalPlayer => null;

    public IEnumerable<IBattleChara> PlayerObjects => Array.Empty<IBattleChara>();

    public IEnumerable<IGameObject> CharacterManagerObjects => Array.Empty<IGameObject>();

    public IEnumerable<IGameObject> ClientObjects => Array.Empty<IGameObject>();

    public IEnumerable<IGameObject> EventObjects => Array.Empty<IGameObject>();

    public IEnumerable<IGameObject> StandObjects => Array.Empty<IGameObject>();

    public IEnumerable<IGameObject> ReactionEventObjects => Array.Empty<IGameObject>();

    public IGameObject? this[int index] => null;

    public IGameObject? SearchById(ulong gameObjectId) => null;

    public IGameObject? SearchByEntityId(uint entityId) => null;

    public nint GetObjectAddress(int index) => 0;

    public IGameObject? CreateObjectReference(nint address) => null;

    public IEnumerator<IGameObject> GetEnumerator() => ((IEnumerable<IGameObject>)Array.Empty<IGameObject>()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
