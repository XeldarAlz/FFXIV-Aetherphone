using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal enum TableDoorAction : byte
{
    None,
    Knock,
    Leave,
    GilLedger,
}

internal readonly record struct TableDoorContent(LocString Title, LocString Hint, LocString Action,
    TableDoorAction Pressed);

internal static class TableDoorCard
{
    public static TableDoorAction Draw(ImDrawListPtr drawList, AppSkin ui, string reason, CasinoKnockStatus knock,
        LocString closedTitle, LocString closedHint, Rect safe, float scale, bool offerGilLedger = false)
    {
        var content = ContentFor(reason, knock, closedTitle, closedHint);
        var bottom = CasinoNotice.DrawWithAction(drawList, ui, CasinoNoticeKind.Card, Loc.T(content.Title),
            Loc.T(content.Hint), Loc.T(content.Action), safe.Min.X, safe.Min.Y + Metrics.Space.Lg * scale,
            safe.Width, scale, out var pressed);
        if (pressed)
        {
            return content.Pressed;
        }

        if (!offerGilLedger)
        {
            return TableDoorAction.None;
        }

        var label = Loc.T(L.Tables.MyLedgerTitle);
        var width = MathF.Min(Button.WidthFor(label, ButtonSize.Large), safe.Width);
        var top = bottom + Metrics.Space.Sm * scale;
        var left = safe.Min.X + (safe.Width - width) * 0.5f;
        var rect = new Rect(new Vector2(left, top), new Vector2(left + width, top + Button.LargeHeight * scale));
        return Button.Draw(drawList, rect, label, ui.Ink, ButtonStyle.Gray, id: "casino.door.gilLedger")
            ? TableDoorAction.GilLedger
            : TableDoorAction.None;
    }

    internal static TableDoorContent ContentFor(string reason, CasinoKnockStatus knock, LocString closedTitle,
        LocString closedHint)
    {
        switch (knock.Stage)
        {
            case KnockStage.Waiting or KnockStage.Admitted:
                return new TableDoorContent(L.Tables.KnockWaitingTitle, L.Casino.ReasonKnockPending,
                    L.Casino.WheelBackToFloor, TableDoorAction.Leave);
            case KnockStage.Denied:
                return new TableDoorContent(L.Tables.KnockDeniedTitle, L.Casino.ReasonDenied, L.Tables.KnockAgain,
                    TableDoorAction.Knock);
            case KnockStage.Refused:
                return new TableDoorContent(TitleFor(knock.Reason, closedTitle), HintFor(knock.Reason, closedHint),
                    L.Casino.WheelBackToFloor, TableDoorAction.Leave);
        }

        var asks = CasinoKnocks.AsksToJoin(reason);
        return new TableDoorContent(TitleFor(reason, closedTitle), HintFor(reason, closedHint),
            asks ? L.Casino.BlackjackAskToJoin : L.Casino.WheelBackToFloor,
            asks ? TableDoorAction.Knock : TableDoorAction.Leave);
    }

    internal static bool Removed(string reason)
    {
        return string.Equals(reason, CasinoReasons.Kicked, StringComparison.Ordinal)
            || string.Equals(reason, CasinoReasons.BannedFromTable, StringComparison.Ordinal);
    }

    internal static LocString TitleFor(string reason, LocString closedTitle)
    {
        if (Removed(reason))
        {
            return L.Tables.RemovedTitle;
        }

        return CasinoKnocks.AsksToJoin(reason) ? L.Casino.BlackjackDoorTitle : closedTitle;
    }

    private static LocString HintFor(string reason, LocString closedHint)
    {
        return CasinoReasons.TryMessage(reason, out var known) ? known : closedHint;
    }
}
