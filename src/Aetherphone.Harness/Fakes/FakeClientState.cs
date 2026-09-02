using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeClientState : IClientState
{
    private const uint NewGridaniaTerritory = 132;
    private const uint NewGridaniaMap = 2;

    public event Action<ZoneInitEventArgs>? ZoneInit { add { } remove { } }

    public event Action<uint>? TerritoryChanged { add { } remove { } }

    public event Action<uint>? MapIdChanged { add { } remove { } }

    public event Action<uint>? InstanceChanged { add { } remove { } }

    public event IClientState.ClassJobChangeDelegate? ClassJobChanged { add { } remove { } }

    public event IClientState.LevelChangeDelegate? LevelChanged { add { } remove { } }

    public event System.Action? Login;

    public event IClientState.LogoutDelegate? Logout;

    public event System.Action? EnterPvP { add { } remove { } }

    public event System.Action? LeavePvP { add { } remove { } }

    public event Action<ContentFinderCondition>? CfPop { add { } remove { } }

    public ClientLanguage ClientLanguage { get; set; } = ClientLanguage.English;

    public uint TerritoryType { get; set; } = NewGridaniaTerritory;

    public uint MapId { get; set; } = NewGridaniaMap;

    public uint Instance { get; set; }

    public bool IsLoggedIn { get; private set; }

    public bool IsPvP => false;

    public bool IsPvPExcludingDen => false;

    public bool IsGPosing => false;

    public bool IsClientIdle(out ConditionFlag blockingFlag)
    {
        blockingFlag = ConditionFlag.None;
        return true;
    }

    public bool IsClientIdle() => true;

    public void SimulateLogin()
    {
        IsLoggedIn = true;
        Login?.Invoke();
    }

    public void SimulateLogout()
    {
        IsLoggedIn = false;
        Logout?.Invoke(0, 0);
    }
}
