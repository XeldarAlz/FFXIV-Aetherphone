using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal readonly struct SettingsGroup
{
    public readonly IReadOnlyList<ISettingsPage> Pages;
    public readonly LocString? Footer;

    public SettingsGroup(IReadOnlyList<ISettingsPage> pages, LocString? footer = null)
    {
        Pages = pages;
        Footer = footer;
    }
}

internal sealed class RootSettingsPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Title);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Cog;
    public Vector4 Tint => new(0.56f, 0.57f, 0.63f, 1f);
    private readonly ISettingsNavigator navigator;
    private readonly IReadOnlyList<SettingsGroup> groups;
    private readonly AethernetSession session;
    private readonly ISettingsPage accountPage;

    public RootSettingsPage(ISettingsNavigator navigator, IReadOnlyList<SettingsGroup> groups,
        AethernetSession session, ISettingsPage accountPage)
    {
        this.navigator = navigator;
        this.groups = groups;
        this.session = session;
        this.accountPage = accountPage;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            if (SettingsHero.Draw(theme, session))
            {
                navigator.Open(accountPage);
            }

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                ImGui.Dummy(new Vector2(0f, (groupIndex == 0 ? 20f : 14f) * scale));
                var group = groups[groupIndex];
                var pages = group.Pages;
                var card = GroupCard.Begin(theme, pages.Count);
                for (var index = 0; index < pages.Count; index++)
                {
                    var page = pages[index];
                    if (SettingsRow.Link(card.NextRow(), page.Icon, page.Tint, page.Title, page.Summary, theme,
                            page.ShowsBadge))
                    {
                        navigator.Open(page);
                    }
                }

                card.End();
                if (group.Footer is { } footer)
                {
                    DrawFooter(Loc.T(footer), theme, scale);
                }
            }

            ImGui.Dummy(new Vector2(0f, 22f * scale));
            DrawVersion(theme);
            ImGui.Dummy(new Vector2(0f, 14f * scale));
        }
    }

    private static void DrawFooter(string text, PhoneTheme theme, float scale)
    {
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        SettingsSection.Hint(text, theme);
    }

    private static void DrawVersion(PhoneTheme theme)
    {
        var label = $"{AepConstants.Name}  {AepConstants.Version}";
        var size = Typography.Measure(label, 0.78f);
        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail().X;
        Typography.Draw(new Vector2(origin.X + (avail - size.X) * 0.5f, origin.Y), label, theme.TextMuted, 0.78f);
        ImGui.Dummy(new Vector2(avail, size.Y));
    }
}
