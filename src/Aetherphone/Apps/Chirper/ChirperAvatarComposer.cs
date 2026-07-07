using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Photos;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Chirper;

internal sealed class ChirperAvatarComposer
{
    private readonly ChirperStore store;
    private readonly ImagePickCrop picker;
    private volatile int outcome;

    public ChirperAvatarComposer(ChirperStore store, PhotoLibrary library)
    {
        this.store = store;
        picker = new ImagePickCrop(library);
    }

    public void Open()
    {
        outcome = 0;
        picker.Open();
    }

    public bool Draw(Rect area, in PhoneContext context, Vector4 accentColor)
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

        var labels = new ImagePickCropLabels(Loc.T(L.Chirper.ChangePhoto), Loc.T(L.Chirper.ImportFromPc),
            Loc.T(L.Photos.NoPhotos), Loc.T(L.Chirper.MoveAndScale), Loc.T(L.Chirper.Use), Loc.T(L.Chirper.Saving),
            Loc.T(L.Chirper.GestureHint));
        var result = picker.Draw(area, context, labels, accentColor, store.AvatarBusy);
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
