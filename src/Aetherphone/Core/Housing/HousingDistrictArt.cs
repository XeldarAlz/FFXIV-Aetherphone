using System.Collections.Concurrent;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Aetherphone.Core.Housing;

internal sealed class HousingDistrictArt : IDisposable
{
    private const string LoadingImageFolder = "ui/loadingimage/";
    private const string HighResolutionSuffix = "_hr1.tex";
    private const string StandardSuffix = ".tex";
    private const int TileBakeWidth = 512;
    private const int BackdropBakeWidth = 192;
    private const float BackdropSigma = 7f;
    private const float BackdropSaturation = 1.15f;
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private readonly IDataManager data;
    private readonly ITextureProvider textures;
    private readonly ConcurrentDictionary<uint, Entry> entries = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly CancellationToken shutdown;
    private readonly SemaphoreSlim bakeGate = new(1, 1);

    public HousingDistrictArt(IDataManager data, ITextureProvider textures)
    {
        this.data = data;
        this.textures = textures;
        shutdown = cancellation.Token;
    }

    public IDalamudTextureWrap? Tile(uint districtId) => EntryFor(districtId).Tile;

    public IDalamudTextureWrap? Backdrop(uint districtId) => EntryFor(districtId).Backdrop;

    public void Dispose()
    {
        cancellation.Cancel();
        foreach (var entry in entries.Values)
        {
            entry.Release();
        }

        entries.Clear();
        bakeGate.Dispose();
        cancellation.Dispose();
    }

    private Entry EntryFor(uint districtId)
    {
        if (!entries.TryGetValue(districtId, out var entry))
        {
            entry = entries.GetOrAdd(districtId, static _ => new Entry());
        }

        if (Volatile.Read(ref entry.Started) == 0 && !shutdown.IsCancellationRequested &&
            entry.Attempts < MaxAttempts && DateTime.UtcNow >= entry.RetryAfterUtc &&
            Interlocked.CompareExchange(ref entry.Started, 1, 0) == 0)
        {
            entry.Attempts++;
            var token = shutdown;
            _ = Task.Run(() => BakeAsync(districtId, entry, token));
        }

        return entry;
    }

    private async Task BakeAsync(uint districtId, Entry entry, CancellationToken token)
    {
        try
        {
            await bakeGate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
            return;
        }

        try
        {
            await BakeOnceAsync(districtId, entry, token).ConfigureAwait(false);
        }
        catch (Exception exception) when (token.IsCancellationRequested &&
                                          exception is OperationCanceledException or ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            ScheduleRetry(districtId, entry, exception);
        }
        finally
        {
            ReleaseGate();
        }
    }

    private void ScheduleRetry(uint districtId, Entry entry, Exception exception)
    {
        if (entry.Attempts >= MaxAttempts)
        {
            AepLog.Warning(exception,
                $"Housing district art for {districtId} failed to bake after {MaxAttempts} attempts");
            return;
        }

        AepLog.Debug(exception, $"Housing district art for {districtId} failed to bake, retrying");
        entry.RetryAfterUtc = DateTime.UtcNow + RetryDelay;
        Volatile.Write(ref entry.Started, 0);
    }

    private void ReleaseGate()
    {
        try
        {
            bakeGate.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task BakeOnceAsync(uint districtId, Entry entry, CancellationToken token)
    {
        if (Resolve(districtId) is not { } path)
        {
            AepLog.Debug($"Housing found no loading image for district {districtId}");
            return;
        }

        var file = await data.GetFileAsync<TexFile>(path, token).ConfigureAwait(false);
        var width = file.Header.Width;
        var height = file.Header.Height;
        if (width == 0 || height == 0)
        {
            return;
        }

        var (tilePixels, tileWidth, tileHeight, backdropPixels, backdropWidth, backdropHeight) =
            Bake(file.ImageData, width, height);
        var tile = await textures.CreateFromRawAsync(RawImageSpecification.Bgra32(tileWidth, tileHeight),
            tilePixels, $"Aetherphone.Housing.District.{districtId}", token).ConfigureAwait(false);
        IDalamudTextureWrap backdrop;
        try
        {
            backdrop = await textures.CreateFromRawAsync(
                RawImageSpecification.Bgra32(backdropWidth, backdropHeight), backdropPixels,
                $"Aetherphone.Housing.Backdrop.{districtId}", token).ConfigureAwait(false);
        }
        catch
        {
            tile.Dispose();
            throw;
        }

        entry.Publish(tile, backdrop);
    }

    private static (byte[] TilePixels, int TileWidth, int TileHeight, byte[] BackdropPixels, int BackdropWidth,
        int BackdropHeight) Bake(byte[] source, int width, int height)
    {
        using var image = Image.LoadPixelData<Bgra32>(source, width, height);
        var tileWidth = Math.Min(TileBakeWidth, width);
        var tileHeight = Math.Max(1, (int)MathF.Round(height * (tileWidth / (float)width)));
        using var tile = image.Clone(context => context.Resize(tileWidth, tileHeight, KnownResamplers.Lanczos3));
        var backdropWidth = Math.Min(BackdropBakeWidth, width);
        var backdropHeight = Math.Max(1, (int)MathF.Round(height * (backdropWidth / (float)width)));
        using var backdrop = image.Clone(context => context.Resize(backdropWidth, backdropHeight)
            .GaussianBlur(BackdropSigma).Saturate(BackdropSaturation));
        var tilePixels = new byte[tileWidth * tileHeight * 4];
        tile.CopyPixelDataTo(tilePixels);
        var backdropPixels = new byte[backdropWidth * backdropHeight * 4];
        backdrop.CopyPixelDataTo(backdropPixels);
        return (tilePixels, tileWidth, tileHeight, backdropPixels, backdropWidth, backdropHeight);
    }

    private string? Resolve(uint districtId)
    {
        if (data.GetExcelSheet<TerritoryType>().GetRowOrDefault(districtId) is not { } territory ||
            territory.LoadingImage.ValueNullable is not { } loadingImage)
        {
            return null;
        }

        var name = loadingImage.FileName.ExtractText();
        if (name.Length == 0)
        {
            return null;
        }

        var highResolution = LoadingImageFolder + name + HighResolutionSuffix;
        if (data.FileExists(highResolution))
        {
            return highResolution;
        }

        var standard = LoadingImageFolder + name + StandardSuffix;
        return data.FileExists(standard) ? standard : null;
    }

    private sealed class Entry
    {
        public int Started;
        public int Attempts;
        public DateTime RetryAfterUtc;
        private readonly object sync = new();
        private IDalamudTextureWrap? tile;
        private IDalamudTextureWrap? backdrop;
        private bool released;

        public IDalamudTextureWrap? Tile => Volatile.Read(ref tile);

        public IDalamudTextureWrap? Backdrop => Volatile.Read(ref backdrop);

        public void Publish(IDalamudTextureWrap tileWrap, IDalamudTextureWrap backdropWrap)
        {
            lock (sync)
            {
                if (!released)
                {
                    Volatile.Write(ref tile, tileWrap);
                    Volatile.Write(ref backdrop, backdropWrap);
                    return;
                }
            }

            tileWrap.Dispose();
            backdropWrap.Dispose();
        }

        public void Release()
        {
            lock (sync)
            {
                released = true;
                tile?.Dispose();
                backdrop?.Dispose();
                Volatile.Write(ref tile, null);
                Volatile.Write(ref backdrop, null);
            }
        }
    }
}
