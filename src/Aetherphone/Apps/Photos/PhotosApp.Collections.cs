using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private static bool IsCustomKey(int key) => key <= -FirstCustomAlbumId;

    private void LoadFavorites()
    {
        favorites.Clear();
        favorites.UnionWith(configuration.PhotoFavorites);
    }

    private void SaveFavorites()
    {
        configuration.PhotoFavorites = new List<string>(favorites);
        configuration.Save();
    }

    private bool PruneFavorites(HashSet<string> validPaths)
    {
        var snapshot = new string[favorites.Count];
        favorites.CopyTo(snapshot);
        var removedAny = false;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (validPaths.Contains(snapshot[index]))
            {
                continue;
            }

            favorites.Remove(snapshot[index]);
            removedAny = true;
        }

        return removedAny;
    }

    private void BuildFavorites()
    {
        var built = new string[favorites.Count];
        favorites.CopyTo(built);
        Array.Sort(built, comparePaths);
        favoritePaths = built;
    }

    private bool IsFavorite(string path) => favorites.Contains(path);

    private void SetFavorite(string path, bool favorite)
    {
        var changed = favorite ? favorites.Add(path) : favorites.Remove(path);
        if (!changed)
        {
            return;
        }

        BuildFavorites();
        SaveFavorites();
    }

    private void ToggleFavorite(string path) => SetFavorite(path, !favorites.Contains(path));

    private void RefreshTrash()
    {
        var built = library.ListTrash();
        Array.Sort(built, comparePaths);
        trashPaths = built;
        trashExpiry.Clear();
        for (var index = 0; index < built.Length; index++)
        {
            trashExpiry[built[index]] = library.ExpiresAt(built[index]);
        }
    }

    private void AskDeletePhotos(string[] paths)
    {
        if (paths.Length == 0)
        {
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.Plural(L.Photos.DeleteToTrash, paths.Length),
            ConfirmLabel = Loc.T(L.Photos.DeleteConfirm),
            CancelLabel = Loc.T(L.Photos.DeleteCancel),
            Sheet = true,
            Confirm = () => DeletePhotos(paths),
        });
    }

    private void DeletePhotos(string[] paths)
    {
        for (var index = 0; index < paths.Length; index++)
        {
            library.Delete(paths[index]);
            EvictTextures(paths[index]);
        }

        RemoveFromViewer(paths);
        FinishRemoval();
    }

    private void RecoverPhotos(string[] trashed)
    {
        for (var index = 0; index < trashed.Length; index++)
        {
            library.Restore(trashed[index]);
            EvictTextures(trashed[index]);
        }

        RemoveFromViewer(trashed);
        FinishRemoval();
    }

    private void AskDeleteForever(string[] trashed)
    {
        if (trashed.Length == 0)
        {
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.Plural(L.Photos.DeleteForever, trashed.Length),
            ConfirmLabel = Loc.T(L.Photos.DeletePermanently),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = () => DeleteForever(trashed),
        });
    }

    private void DeleteForever(string[] trashed)
    {
        for (var index = 0; index < trashed.Length; index++)
        {
            library.DeletePermanently(trashed[index]);
            EvictTextures(trashed[index]);
        }

        RemoveFromViewer(trashed);
        FinishRemoval();
    }

    private void FinishRemoval()
    {
        EndSelect();
        Refresh();
        if (router.Current.Route == PhotoRoute.Viewer && viewerPaths.Length == 0)
        {
            router.Pop(false);
        }
    }

    private void EvictTextures(string path)
    {
        if (thumbnails.TryRemove(path, out var thumbWrap))
        {
            DeferredDispose.Later(thumbWrap);
        }

        if (fullImages.TryRemove(path, out var fullWrap))
        {
            DeferredDispose.Later(fullWrap);
        }
    }

    private void RemoveFromViewer(string[] removed)
    {
        if (viewerPaths.Length == 0)
        {
            return;
        }

        var kept = new List<string>(viewerPaths.Length);
        var keptBeforeCurrent = 0;
        for (var index = 0; index < viewerPaths.Length; index++)
        {
            var path = viewerPaths[index];
            if (ContainsPath(removed, path))
            {
                continue;
            }

            if (index < viewerIndex)
            {
                keptBeforeCurrent++;
            }

            kept.Add(path);
        }

        if (kept.Count == viewerPaths.Length)
        {
            return;
        }

        viewerPaths = kept.ToArray();
        viewerIndex = Math.Clamp(keptBeforeCurrent, 0, Math.Max(0, viewerPaths.Length - 1));
        zoomView.Reset();
    }

    private static bool ContainsPath(string[] paths, string path)
    {
        for (var index = 0; index < paths.Length; index++)
        {
            if (string.Equals(paths[index], path, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
