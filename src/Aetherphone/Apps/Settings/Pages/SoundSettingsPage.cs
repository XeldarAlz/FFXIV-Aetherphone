using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class SoundSettingsPage : ISettingsPage
{
    private readonly SoundService sound;
    private readonly ConfirmService confirm;
    private readonly SoundKind kind;
    private readonly LocString title;
    private readonly FontAwesomeIcon icon;
    private readonly Vector4 tint;
    private readonly string sliderId;
    private readonly Func<string> getToken;
    private readonly Action<string> setToken;
    private readonly Func<float> getVolume;
    private readonly Action<float> setVolume;
    private readonly SoundImport import = new();
    private readonly Action<string?> select;
    private readonly Action<string> askRemove;

    public SoundSettingsPage(SoundService sound, ConfirmService confirm, SoundKind kind, LocString title,
        FontAwesomeIcon icon, Vector4 tint, string sliderId, Func<string> getToken, Action<string> setToken,
        Func<float> getVolume, Action<float> setVolume)
    {
        this.sound = sound;
        this.confirm = confirm;
        this.kind = kind;
        this.title = title;
        this.icon = icon;
        this.tint = tint;
        this.sliderId = sliderId;
        this.getToken = getToken;
        this.setToken = setToken;
        this.getVolume = getVolume;
        this.setVolume = setVolume;
        select = Select;
        askRemove = AskRemove;
    }

    public string Title => Loc.T(title);
    public string Summary => sound.Label(kind, getToken());
    public FontAwesomeIcon Icon => icon;
    public Vector4 Tint => tint;

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        if (import.TryTake(out var importedPath))
        {
            TryImport(importedPath);
        }

        using (var surface = AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Sound), theme, Loc.T(L.Settings.SoundImportHint));
            SoundOptionList.Draw(theme, sound, kind, getToken(), false, select, askRemove);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            SettingsSection.Header(Loc.T(L.Settings.Volume), theme);
            var volumeCard = GroupCard.Begin(theme, 1);
            var volume = VolumeSlider.Draw(sliderId, volumeCard.NextRow(), getVolume(), theme);
            volumeCard.End();
            if (volume.Dragging)
            {
                surface.CancelDrag();
            }

            if (volume.Released && MathF.Abs(volume.Value - getVolume()) > 0.001f)
            {
                setVolume(volume.Value);
                sound.Preview(kind, getToken(), volume.Value);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            var importCard = GroupCard.Begin(theme, 1);
            if (SettingsRow.Disclosure(importCard.NextRow(), Loc.T(L.Settings.ImportSound), string.Empty, theme))
            {
                import.Launch(Loc.T(title));
            }

            importCard.End();
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private void Select(string? token)
    {
        if (token is null)
        {
            return;
        }

        setToken(token);
        sound.Preview(kind, token, getVolume());
    }

    private void AskRemove(string token)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Settings.RemoveSound),
            Message = Loc.T(kind == SoundKind.Ringtone ? L.Settings.RemoveRingtoneMessage : L.Settings.RemoveSoundMessage,
                sound.Label(kind, token)),
            ConfirmLabel = Loc.T(L.Settings.RemoveSoundConfirm),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            Confirm = () => sound.RemoveUserFile(kind, token),
        });
    }

    private void TryImport(string path)
    {
        try
        {
            var token = sound.AddUserFile(kind, path);
            setToken(token);
            sound.Preview(kind, token, getVolume());
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Sound] import failed");
        }
    }
}
