using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private readonly HashSet<string> favorites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> trashExpiry = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> placeNames = new(StringComparer.Ordinal);
    private readonly Dictionary<uint, PlaceAlbum> placeLookup = new();
    private string[] favoritePaths = Array.Empty<string>();
    private string[] trashPaths = Array.Empty<string>();
    private PlaceAlbum[] places = Array.Empty<PlaceAlbum>();

    private sealed class PlaceAlbum
    {
        public readonly uint Territory;
        public readonly string Name;
        public readonly string[] Paths;

        public PlaceAlbum(uint territory, string name, string[] paths)
        {
            Territory = territory;
            Name = name;
            Paths = paths;
        }
    }

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
        ApplyFilter();
    }

    private void ToggleFavorite(string path) => SetFavorite(path, !favorites.Contains(path));

    private uint PlaceOf(string path) => PhotoPlaces.TerritoryOf(configuration.PhotoPlaces, path);

    private void PrunePlaces(string[] libraryPaths)
    {
        if (configuration.PhotoPlaces.Count == 0)
        {
            return;
        }

        placeNames.Clear();
        for (var index = 0; index < libraryPaths.Length; index++)
        {
            placeNames.Add(Path.GetFileName(libraryPaths[index]));
        }

        for (var index = 0; index < trashPaths.Length; index++)
        {
            placeNames.Add(Path.GetFileName(trashPaths[index]));
        }

        library.AddPendingNames(placeNames);

        var pruned = PhotoPlaces.Prune(configuration.PhotoPlaces, placeNames);
        if (pruned)
        {
            configuration.Save();
        }
    }

    private void BuildPlaces()
    {
        placeLookup.Clear();
        if (configuration.PhotoPlaces.Count == 0)
        {
            places = Array.Empty<PlaceAlbum>();
            return;
        }

        var territories = new uint[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            territories[index] = PlaceOf(entries[index].Path);
        }

        var buckets = PhotoGrouping.Buckets(territories);
        var built = new List<PlaceAlbum>(buckets.Length);
        for (var bucketIndex = 0; bucketIndex < buckets.Length; bucketIndex++)
        {
            var bucket = buckets[bucketIndex];
            var name = PhotoPlaces.Name(bucket.Key);
            if (name.Length == 0)
            {
                continue;
            }

            var paths = new string[bucket.Indices.Length];
            for (var index = 0; index < paths.Length; index++)
            {
                paths[index] = entries[bucket.Indices[index]].Path;
            }

            var album = new PlaceAlbum(bucket.Key, name, paths);
            built.Add(album);
            placeLookup[bucket.Key] = album;
        }

        places = built.ToArray();
    }

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

    private int DaysLeft(string trashPath)
    {
        if (!trashExpiry.TryGetValue(trashPath, out var expiry))
        {
            return 0;
        }

        var days = (int)Math.Ceiling((expiry - DateTime.Now).TotalDays);
        return Math.Clamp(days, 0, PhotoLibrary.TrashRetentionDays);
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
            ConfirmLabel = Loc.T(L.Photos.Delete),
            CancelLabel = Loc.T(L.Common.Cancel),
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

        UiFeedback.Play(UiSound.Success);
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

        UiFeedback.Play(UiSound.Success);
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

        if (covers.TryRemove(path, out var coverWrap))
        {
            DeferredDispose.Later(coverWrap);
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
            if (IndexOf(removed, path) >= 0)
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
        ResetViewerMotion();
    }
}
