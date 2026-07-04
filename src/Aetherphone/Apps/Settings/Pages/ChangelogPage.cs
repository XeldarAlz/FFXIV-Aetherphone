using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Changelog;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class ChangelogPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Changelog);

    public string Summary => Loc.T(L.Settings.ChangelogSummary);

    public string Glyph => "C";

    public Vector4 Tint => new(0.62f, 0.42f, 0.90f, 1f);

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            for (var index = 0; index < ChangelogData.Entries.Count; index++)
            {
                var entry = ChangelogData.Entries[index];
                SettingsSection.Header(entry.Version, theme);

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16f * scale);
                using (Plugin.Fonts.Push(0.78f))
                using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
                {
                    ImGui.TextUnformatted(entry.Date);
                }

                ImGui.Dummy(new Vector2(0f, 6f * scale));

                for (var lineIndex = 0; lineIndex < entry.Highlights.Count; lineIndex++)
                {
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16f * scale);
                    using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
                    {
                        ImGui.Bullet();
                        ImGui.SameLine();
                        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - 16f * scale);
                        ImGui.TextWrapped(entry.Highlights[lineIndex]);
                        ImGui.PopTextWrapPos();
                    }
                }

                ImGui.Dummy(new Vector2(0f, 10f * scale));
            }
        }
    }
}
