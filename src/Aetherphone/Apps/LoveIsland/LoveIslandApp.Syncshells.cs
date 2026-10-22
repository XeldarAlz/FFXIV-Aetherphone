using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal sealed partial class LoveIslandApp
{
    private void DrawSyncshells(Rect area)
    {
        var frame = BeginPushed(area, out var headerContext, out var body);
        var title = Texts.T(CampaignTextKeys.SyncshellsTitle);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = LeftOf(origin.X);
            var inner = InnerWidth(width);
            var cursorY = origin.Y + Metrics.Space.Sm * scale;
            cursorY = DrawMutedNote(left, cursorY, inner, Texts.T(CampaignTextKeys.SyncshellsBody))
                      + Metrics.Space.Lg * scale;
            var syncshells = frameCampaign?.Syncshells ?? Array.Empty<CampaignSyncshellDto>();
            if (syncshells.Length == 0 || labels.SyncshellServices.Length != syncshells.Length)
            {
                cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), Loc.T(L.LoveIsland.NothingListed),
                    ui.MutedInk, TextStyles.Body, inner);
            }
            else
            {
                for (var index = 0; index < syncshells.Length; index++)
                {
                    cursorY = DrawSyncshellCard(drawList, left, cursorY, inner, syncshells[index], index)
                              + LoveIslandKit.CardGap * scale;
                }
            }

            Reserve(origin, width, cursorY + Metrics.Space.Xl * scale);
        }

        EndPushed(in frame, in headerContext, "loveisland.nav.syncshells", title, Texts.T(CampaignTextKeys.TabTonight));
    }

    private float DrawSyncshellCard(ImDrawListPtr drawList, float left, float top, float width,
        CampaignSyncshellDto syncshell, int index)
    {
        var fieldCount = 0;
        var buttonCount = 0;
        var copyIdAction = CardIdle;
        if (syncshell.SyncshellId.Length > 0)
        {
            cardFields[fieldCount++] = new CardField(Loc.T(L.LoveIsland.SyncshellId), syncshell.SyncshellId);
            copyIdAction = buttonCount;
            cardButtons[buttonCount++] = new CardButton(Loc.T(L.LoveIsland.CopySyncshellId), true,
                labels.SyncshellCopyIdButtons[index]);
        }

        if (syncshell.Password.Length > 0)
        {
            cardFields[fieldCount++] = new CardField(Loc.T(L.LoveIsland.SyncshellPassword), syncshell.Password);
            var prominent = buttonCount == 0;
            cardButtons[buttonCount++] = new CardButton(Loc.T(L.LoveIsland.CopySyncshellPassword), prominent,
                labels.SyncshellCopyPasswordButtons[index]);
        }

        var bottom = DrawCard(drawList, left, top, width, new CardContent(syncshell.Id,
            labels.SyncshellServices[index], CardPill.None, string.Empty, syncshell.Name, syncshell.Blurb, 0, 0,
            false, FieldCount: fieldCount), buttonCount, out var action);
        if (action < 0)
        {
            return bottom;
        }

        CopyText(action == copyIdAction ? syncshell.SyncshellId : syncshell.Password);
        return bottom;
    }
}
