using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private static readonly TimeSpan PendingFlagTimeout = TimeSpan.FromSeconds(90d);
    private static readonly TimeSpan WorldHopRetryDelay = TimeSpan.FromSeconds(1d);
    private static readonly TimeSpan FlagRetryDelay = TimeSpan.FromSeconds(1d);
    private const int FlagRetryAttempts = 3;

    private uint pendingFlagWorldId;
    private uint pendingFlagTerritoryId;
    private uint pendingFlagMapId;
    private float pendingFlagMapX;
    private float pendingFlagMapY;

    private uint pendingWorldHopWorldId;
    private uint pendingWorldHopTerritoryId;
    private uint pendingWorldHopMapId;
    private (float X, float Y)? pendingWorldHopFlagCoordinate;

    private uint pendingInstanceWorldId;
    private uint pendingInstanceTerritoryId;
    private int pendingInstanceTarget;

    private void NavigateToCoordinate(uint territoryId, uint worldId, uint mapId, (float X, float Y)? targetCoordinate,
        (float X, float Y)? flagCoordinate, int zoneInstance)
    {
        ArmPendingInstanceSync(worldId, territoryId, zoneInstance);

        var destination = TravelPlanner.ResolveNearestAetheryteTo(territoryId, worldId, LocationShare.CurrentWorldId(),
            Plugin.ClientState.TerritoryType, targetCoordinate);
        if (destination.Kind == TravelKind.AlreadyThere)
        {
            if (flagCoordinate is { } here)
            {
                DropFlag(territoryId, mapId, here.X, here.Y);
            }

            return;
        }

        TravelToHuntZone(in destination, worldId, territoryId, mapId, flagCoordinate);
    }

    private void NavigateToAetheryte(uint territoryId, uint worldId, uint mapId, (float X, float Y)? targetCoordinate,
        int zoneInstance)
    {
        if (targetCoordinate is not { } coordinate)
        {
            return;
        }

        ArmPendingInstanceSync(worldId, territoryId, zoneInstance);

        var destination = TravelPlanner.ResolveAetheryteAt(territoryId, worldId, LocationShare.CurrentWorldId(),
            coordinate);
        TravelToHuntZone(in destination, worldId, territoryId, mapId, null);
    }

    private void TravelToHuntZone(in TravelDestination destination, uint worldId, uint territoryId, uint mapId,
        (float X, float Y)? flagCoordinate)
    {
        var outcome = TravelPlanner.Go(in destination);
        if (outcome == LifestreamOutcome.Started)
        {
            if (destination.Kind == TravelKind.World)
            {
                ArmPendingWorldHop(worldId, territoryId, mapId, flagCoordinate);
            }
            else if (flagCoordinate is { } coordinate)
            {
                ArmPendingFlag(worldId, territoryId, mapId, coordinate.X, coordinate.Y);
            }

            return;
        }

        HandleTravelFailure(outcome, in destination);
    }

    private void HandleTravelFailure(LifestreamOutcome outcome, in TravelDestination destination)
    {
        if (outcome == LifestreamOutcome.NotInstalled)
        {
            ImGui.SetClipboardText(TravelPlanner.Command(in destination));
            ShellToast.Show();
            return;
        }

        confirm.Alert(null, TravelPlanner.Notice(outcome, in destination), Loc.T(L.Common.Close));
    }

    private static void DropFlag(uint territoryId, uint mapId, float mapX, float mapY) =>
        LocationShare.OpenMap(new SharedLocation(territoryId, mapId, mapX, mapY, 0, 0, 0, 0));

    private static uint ResolveMapId(uint territoryId) =>
        territoryId != 0 && Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory)
            ? territory.Map.RowId
            : 0u;

    private void ArmPendingFlag(uint worldId, uint territoryId, uint mapId, float mapX, float mapY)
    {
        pendingFlagWorldId = worldId;
        pendingFlagTerritoryId = territoryId;
        pendingFlagMapId = mapId;
        pendingFlagMapX = mapX;
        pendingFlagMapY = mapY;
        pendingFlagAction.Arm();
    }

    private bool IsPendingFlagReady() =>
        Plugin.ClientState.TerritoryType == pendingFlagTerritoryId &&
        LocationShare.CurrentWorldId() == pendingFlagWorldId && !PlayerBusy.Now;

    private bool TryDropPendingFlag()
    {
        DropFlag(pendingFlagTerritoryId, pendingFlagMapId, pendingFlagMapX, pendingFlagMapY);
        return false;
    }

    private void ArmPendingWorldHop(uint worldId, uint territoryId, uint mapId, (float X, float Y)? flagCoordinate)
    {
        pendingWorldHopWorldId = worldId;
        pendingWorldHopTerritoryId = territoryId;
        pendingWorldHopMapId = mapId;
        pendingWorldHopFlagCoordinate = flagCoordinate;
        pendingWorldHopAction.Arm();
    }

    private bool IsPendingWorldHopReady() =>
        LocationShare.CurrentWorldId() == pendingWorldHopWorldId && !LifestreamBridge.IsBusy() && !PlayerBusy.Now;

    private bool TryContinuePendingWorldHop() =>
        TryContinueAfterWorldHop(pendingWorldHopTerritoryId, pendingWorldHopWorldId, pendingWorldHopMapId,
            pendingWorldHopFlagCoordinate);

    private bool TryContinueAfterWorldHop(uint territoryId, uint worldId, uint mapId,
        (float X, float Y)? flagCoordinate)
    {
        var destination = TravelPlanner.ResolveNearestAetheryteTo(territoryId, worldId, LocationShare.CurrentWorldId(),
            Plugin.ClientState.TerritoryType, flagCoordinate);
        if (destination.Kind == TravelKind.AlreadyThere)
        {
            if (flagCoordinate is { } coordinate)
            {
                DropFlag(territoryId, mapId, coordinate.X, coordinate.Y);
            }

            return true;
        }

        var outcome = TravelPlanner.Go(in destination);
        if (outcome == LifestreamOutcome.Started)
        {
            if (flagCoordinate is { } coordinate)
            {
                ArmPendingFlag(worldId, territoryId, mapId, coordinate.X, coordinate.Y);
            }

            return true;
        }

        if (outcome is LifestreamOutcome.Busy or LifestreamOutcome.CannotTeleportNow)
        {
            return false;
        }

        HandleTravelFailure(outcome, in destination);
        return true;
    }

    private void ArmPendingInstanceSync(uint worldId, uint territoryId, int zoneInstance)
    {
        if (zoneInstance <= 0)
        {
            return;
        }

        pendingInstanceWorldId = worldId;
        pendingInstanceTerritoryId = territoryId;
        pendingInstanceTarget = zoneInstance;
        pendingInstanceAction.Arm();
    }

    private bool IsPendingInstanceSyncReady() =>
        Plugin.ClientState.TerritoryType == pendingInstanceTerritoryId &&
        LocationShare.CurrentWorldId() == pendingInstanceWorldId && !PlayerBusy.Now && !LifestreamBridge.IsBusy();

    private bool TryAdvancePendingInstanceSync()
    {
        var currentInstance = LifestreamBridge.GetCurrentInstance();
        if (currentInstance <= 0 || currentInstance == pendingInstanceTarget)
        {
            return true;
        }

        if (!LifestreamBridge.CanChangeInstance())
        {
            return false;
        }

        LifestreamBridge.ChangeInstance(pendingInstanceTarget);
        return true;
    }
}
