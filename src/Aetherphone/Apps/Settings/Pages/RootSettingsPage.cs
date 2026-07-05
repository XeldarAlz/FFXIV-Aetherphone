using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class RootSettingsPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Title);
    public string Summary => string.Empty;
    public string Glyph => "S";
    public Vector4 Tint => new(0.56f, 0.57f, 0.63f, 1f);
    private readonly ISettingsNavigator navigator;
    private readonly IReadOnlyList<IReadOnlyList<ISettingsPage>> groups;

    public RootSettingsPage(ISettingsNavigator navigator, IReadOnlyList<IReadOnlyList<ISettingsPage>> groups)
    {
        this.navigator = navigator;
        this.groups = groups;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                if (groupIndex > 0)
                {
                    ImGui.Dummy(new Vector2(0f, 16f * scale));
                }

                var group = groups[groupIndex];
                var card = GroupCard.Begin(theme, group.Count);
                for (var index = 0; index < group.Count; index++)
                {
                    var page = group[index];
                    if (SettingsRow.Link(card.NextRow(), page.Glyph, page.Tint, page.Title, page.Summary, theme))
                    {
                        navigator.Open(page);
                    }
                }

                card.End();
            }
        }
    }
}
