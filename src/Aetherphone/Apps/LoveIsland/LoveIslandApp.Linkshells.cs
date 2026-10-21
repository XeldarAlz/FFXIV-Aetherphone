using Aetherphone.Core;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal sealed partial class LoveIslandApp
{
    private const string LinkshellAskPrefix = "loveisland.linkshell.ask.";

    private readonly Dictionary<string, string> linkshellAskIds = new(StringComparer.Ordinal);

    private void DrawLinkshells(Rect area)
    {
        var frame = BeginPushed(area, out var headerContext, out var body);
        var title = Texts.T(CampaignTextKeys.LinkshellsTitle);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = LeftOf(origin.X);
            var inner = InnerWidth(width);
            var cursorY = origin.Y + Metrics.Space.Sm * scale;
            cursorY = DrawMutedNote(left, cursorY, inner, Texts.T(CampaignTextKeys.LinkshellsBody))
                      + Metrics.Space.Lg * scale;
            var campaign = frameCampaign;
            var linkshells = campaign?.Linkshells ?? Array.Empty<Core.Aethernet.Contracts.CampaignLinkshellDto>();
            if (linkshells.Length == 0 || labels.LinkshellKinds.Length != linkshells.Length)
            {
                cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), Loc.T(L.LoveIsland.NothingListed),
                    ui.MutedInk, TextStyles.Body, inner);
            }
            else
            {
                for (var index = 0; index < linkshells.Length; index++)
                {
                    var linkshell = linkshells[index];
                    var ask = labels.LinkshellAsks[index];
                    var buttonCount = 0;
                    if (ask.Length > 0)
                    {
                        cardButtons[buttonCount++] = new CardButton(ask, true, AskIdFor(linkshell.Id));
                    }

                    cursorY = DrawCard(drawList, left, cursorY, inner, new CardContent(linkshell.Id,
                        labels.LinkshellKinds[index], CardPill.None, string.Empty, linkshell.Name, linkshell.Blurb, 0,
                        0, false), buttonCount, out var action) + LoveIslandKit.CardGap * scale;
                    if (action == 0 && !string.IsNullOrEmpty(linkshell.ContactMemberId))
                    {
                        OpenMember(linkshell.ContactMemberId);
                    }
                }
            }

            Reserve(origin, width, cursorY + Metrics.Space.Xl * scale);
        }

        EndPushed(in frame, in headerContext, "loveisland.nav.linkshells", title, Texts.T(CampaignTextKeys.TabTonight));
    }

    private string AskIdFor(string linkshellId)
    {
        if (!linkshellAskIds.TryGetValue(linkshellId, out var id))
        {
            id = LinkshellAskPrefix + linkshellId;
            linkshellAskIds[linkshellId] = id;
        }

        return id;
    }
}
