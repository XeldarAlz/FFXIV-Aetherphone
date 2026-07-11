using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AvatarPhotoPage : ISettingsPage
{
    public string Title => Loc.T(L.Account.ChangePhoto);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Camera;
    public Vector4 Tint => new(0.36f, 0.72f, 0.62f, 1f);
    public bool OwnsChrome => true;
    private readonly ISettingsNavigator navigator;
    private readonly Func<bool> busy;
    private readonly Action<string, WallpaperCrop, Action<bool>> upload;
    private readonly ImagePickCrop picker;
    private volatile int outcome;

    public AvatarPhotoPage(PhotoLibrary library, ISettingsNavigator navigator, Func<bool> busy,
        Action<string, WallpaperCrop, Action<bool>> upload)
    {
        this.navigator = navigator;
        this.busy = busy;
        this.upload = upload;
        picker = new ImagePickCrop(library);
        picker.Open();
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        if (outcome == 1)
        {
            outcome = 0;
            navigator.Back();
            return;
        }

        if (outcome == 2)
        {
            outcome = 0;
            Plugin.Confirm.Alert(null, Loc.T(L.Account.CannotReach), Loc.T(L.Account.FailDismiss));
        }

        var labels = new ImagePickCropLabels(Loc.T(L.Account.ChangePhoto), Loc.T(L.Account.ImportFromPc),
            Loc.T(L.Photos.NoPhotos), Loc.T(L.Account.MoveAndScale), Loc.T(L.Account.Use), Loc.T(L.Account.Saving),
            Loc.T(L.Account.GestureHint));
        var result = picker.Draw(body, context, labels, context.Theme.Accent, busy());
        if (result == ImagePickCropEvent.Cancelled)
        {
            navigator.Back();
            return;
        }

        if (result == ImagePickCropEvent.Committed && !busy() && picker.SourcePath.Length > 0)
        {
            upload(picker.SourcePath, picker.Crop, ok => outcome = ok ? 1 : 2);
        }
    }
}
