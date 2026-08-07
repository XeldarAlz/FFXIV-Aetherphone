using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class BehaviorPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Behavior);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.SlidersH;
    public Vector4 Tint => new(0.95f, 0.62f, 0.25f, 1f);
    private readonly Configuration configuration;

    public BehaviorPage(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Behavior), theme);
            var windowCard = GroupCard.Begin(theme, 1);
            var lockPosition = SettingsRow.Bool(windowCard.NextRow(), Loc.T(L.ControlCenter.LockPosition),
                configuration.LockPosition, theme);
            windowCard.End();
            if (lockPosition != configuration.LockPosition)
            {
                configuration.LockPosition = lockPosition;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            var screenshotCard = GroupCard.Begin(theme, 1);
            var importScreenshots = SettingsRow.Bool(screenshotCard.NextRow(),
                Loc.T(L.Settings.ImportScreenshots), configuration.ImportScreenshots, theme);
            screenshotCard.End();
            if (importScreenshots != configuration.ImportScreenshots)
            {
                configuration.ImportScreenshots = importScreenshots;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Settings.ImportScreenshotsHint), theme);

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            var usesNativeFileDialog = configuration.UseNativeFileDialog ?? NativeFileDialog.IsSupported;
            var filePickerCard = GroupCard.Begin(theme, 1);
            var nativeFileDialog = SettingsRow.Bool(filePickerCard.NextRow(),
                Loc.T(L.Settings.NativeFileDialog), usesNativeFileDialog, theme);
            filePickerCard.End();
            if (nativeFileDialog != usesNativeFileDialog)
            {
                configuration.UseNativeFileDialog = nativeFileDialog;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Settings.NativeFileDialogHint), theme);

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            var startupCard = GroupCard.Begin(theme, 2);
            var openStartup = SettingsRow.Bool(startupCard.NextRow(), Loc.T(L.Settings.OpenOnStartup),
                configuration.OpenOnStartup, theme);
            var openMinimized = SettingsRow.Bool(startupCard.NextRow(), Loc.T(L.Settings.OpenMinimized),
                configuration.OpenMinimizedOnStartup, theme);
            startupCard.End();
            if (openStartup != configuration.OpenOnStartup)
            {
                configuration.OpenOnStartup = openStartup;
                configuration.Save();
            }

            if (openMinimized != configuration.OpenMinimizedOnStartup)
            {
                configuration.OpenMinimizedOnStartup = openMinimized;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Settings.StartupHint), theme);
        }
    }
}
