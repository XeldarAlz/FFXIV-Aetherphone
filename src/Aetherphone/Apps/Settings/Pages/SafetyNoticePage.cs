using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Moderation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class SafetyNoticePage : ISettingsPage
{
    private ModerationNoticeDto? notice;

    public string Title => notice is null ? Loc.T(L.Safety.Title) : ModerationNoticeText.Title(notice);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.ShieldAlt;
    public Vector4 Tint => new(0.86f, 0.42f, 0.32f, 1f);

    public void Show(ModerationNoticeDto value)
    {
        notice = value;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            if (notice is not { } current)
            {
                SettingsSection.Hint(Loc.T(L.Safety.Empty), theme);
                return;
            }

            SettingsSection.Header(ModerationNoticeText.Title(current), theme);
            SettingsSection.Hint(TimeText.Short(current.CreatedAtUnix), theme);

            ImGui.Dummy(new Vector2(0f, 10f * scale));
            Paragraph(ModerationNoticeText.Body(current), theme);

            if (current.ContentCreatedAtUnix is { } postedAt)
            {
                ImGui.Dummy(new Vector2(0f, 12f * scale));
                SettingsSection.Hint(Loc.T(L.Safety.PostedOn, TimeText.Short(postedAt)), theme);
            }
        }
    }

    private static void Paragraph(string text, PhoneTheme theme)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.PushTextWrapPos(0f);
            Typography.Wrapped(text);
            ImGui.PopTextWrapPos();
        }
    }
}
