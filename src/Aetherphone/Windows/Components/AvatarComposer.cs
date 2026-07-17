using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Windows.Components;

internal readonly record struct AvatarComposerLabels(
    LocString ChangePhoto,
    LocString ImportFromPc,
    LocString NoPhotos,
    LocString MoveAndScale,
    LocString Use,
    LocString Saving,
    LocString GestureHint);

internal sealed class AvatarComposer
{
    private readonly Func<bool> busy;
    private readonly Action<string, WallpaperCrop, Action<bool>> update;
    private readonly AvatarComposerLabels labels;
    private readonly ImagePickCrop picker;
    private volatile int outcome;

    public AvatarComposer(Func<bool> busy, Action<string, WallpaperCrop, Action<bool>> update,
        AvatarComposerLabels labels, PhotoLibrary library, WallpaperImageCache images)
    {
        this.busy = busy;
        this.update = update;
        this.labels = labels;
        picker = new ImagePickCrop(library, images);
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

        var pickLabels = new ImagePickCropLabels(Loc.T(labels.ChangePhoto), Loc.T(labels.ImportFromPc),
            Loc.T(labels.NoPhotos), Loc.T(labels.MoveAndScale), Loc.T(labels.Use), Loc.T(labels.Saving),
            Loc.T(labels.GestureHint));
        var updating = busy();
        var result = picker.Draw(area, context, pickLabels, accentColor, updating);
        if (result == ImagePickCropEvent.Cancelled)
        {
            return true;
        }

        if (result == ImagePickCropEvent.Committed && !updating && picker.SourcePath.Length > 0)
        {
            update(picker.SourcePath, picker.Crop, ok => outcome = ok ? 1 : 2);
        }

        return false;
    }
}
