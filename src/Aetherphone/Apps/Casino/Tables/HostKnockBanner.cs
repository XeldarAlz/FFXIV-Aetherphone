using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class HostKnockBanner
{
    public static float Reserve(CasinoTablesStore tables, string roomId, float width, float scale)
    {
        if (!Hosting(tables, roomId))
        {
            return 0f;
        }

        tables.EnsureDoorFresh(roomId);
        return FirstKnock(tables, roomId) is null ? 0f : KnockCard.Height(width, scale) + Metrics.Space.Sm * scale;
    }

    public static void Draw(ImDrawListPtr drawList, AppSkin ui, CasinoTablesStore tables, string roomId,
        Vector2 origin, float width, float scale)
    {
        if (!Hosting(tables, roomId))
        {
            return;
        }

        var knocker = FirstKnock(tables, roomId);
        if (knocker is null)
        {
            return;
        }

        var answer = KnockCard.Draw(drawList, ui, knocker, origin, width, !tables.IntentInFlight, scale);
        if (answer != KnockAnswer.None)
        {
            tables.AnswerKnock(roomId, knocker.UserId, answer == KnockAnswer.Approve);
        }
    }

    private static bool Hosting(CasinoTablesStore tables, string roomId)
    {
        var card = tables.CardFor(roomId);
        return card is not null && tables.Owns(card);
    }

    private static CasinoTableKnockDto? FirstKnock(CasinoTablesStore tables, string roomId)
    {
        var knocks = tables.DoorFor(roomId)?.Knocks;
        return knocks is null || knocks.Length == 0 ? null : knocks[0];
    }
}
