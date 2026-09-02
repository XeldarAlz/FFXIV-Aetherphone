using Aetherphone.Core.Game;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Aetherphone.Core.GameChat;

internal static unsafe class GameLinkActions
{
    public static void TryOn(uint itemId)
    {
        if (!GameMemory.Attached)
        {
            return;
        }

        if (itemId == 0)
        {
            return;
        }

        AgentTryon.TryOn(0xFF, itemId, 0);
    }

    public static void CompareItem(uint itemId)
    {
        if (!GameMemory.Attached)
        {
            return;
        }

        var agent = AgentItemComp.Instance();
        if (itemId == 0 || agent is null)
        {
            return;
        }

        agent->CompareItem(0x4D, itemId, 0, 0);
    }

    public static void SearchRecipes(uint itemId)
    {
        if (!GameMemory.Attached)
        {
            return;
        }

        var agent = AgentRecipeProductList.Instance();
        if (itemId == 0 || agent is null)
        {
            return;
        }

        agent->SearchForRecipesUsingItem(itemId);
    }

    public static void FindItem(uint itemId)
    {
        if (!GameMemory.Attached)
        {
            return;
        }

        var module = ItemFinderModule.Instance();
        if (itemId == 0 || module is null)
        {
            return;
        }

        module->SearchForItem(itemId, true);
    }

    public static void LinkInChat(uint itemId)
    {
        if (!GameMemory.Attached)
        {
            return;
        }

        var agent = AgentChatLog.Instance();
        if (itemId == 0 || agent is null)
        {
            return;
        }

        agent->LinkItem(itemId);
    }

    public static void OpenMap(uint territoryId, uint mapId, int rawX, int rawY)
    {
        if (mapId == 0)
        {
            return;
        }

        try
        {
            Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(territoryId, mapId, rawX, rawY));
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[GameLinkActions] open map failed");
        }
    }
}
