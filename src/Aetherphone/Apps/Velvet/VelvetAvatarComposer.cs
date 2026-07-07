using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Photos;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet;

internal sealed class VelvetAvatarComposer
{
    private readonly VelvetStore store;
    private readonly ImagePickCrop picker;
    private volatile int outcome;

    public VelvetAvatarComposer(VelvetStore store, PhotoLibrary library)
    {
        this.store = store;
        picker = new ImagePickCrop(library);
    }

    public void Open()
    {
        outcome = 0;
        picker.Open();
    }

    public bool Draw(Rect area, AppSkin ui, in PhoneContext context)
    {
        if (outcome == 1)
        {
            outcome = 0;
            return true;
        }

        if (outcome == 2)
        {
            outcome = 0;
        }

        var labels = new ImagePickCropLabels(Loc.T(L.Velvet.ChangePhoto), Loc.T(L.Velvet.ImportFromPc),
            Loc.T(L.Velvet.NoPhotos), Loc.T(L.Velvet.MoveAndScale), Loc.T(L.Velvet.Use), Loc.T(L.Velvet.Saving),
            Loc.T(L.Velvet.GestureHint));
        var result = picker.Draw(area, context, labels, ui.Accent, store.AvatarBusy);
        if (result == ImagePickCropEvent.Cancelled)
        {
            return true;
        }

        if (result == ImagePickCropEvent.Committed && !store.AvatarBusy && picker.SourcePath.Length > 0)
        {
            store.UpdateAvatar(picker.SourcePath, picker.Crop, ok => outcome = ok ? 1 : 2);
        }

        return false;
    }
}
