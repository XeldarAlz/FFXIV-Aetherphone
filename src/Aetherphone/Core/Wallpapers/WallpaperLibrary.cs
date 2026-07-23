using System.Collections.Concurrent;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Media;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Aetherphone.Core.Wallpapers;

internal sealed class WallpaperLibrary : IDisposable
{
    private const int DayStartHour = 7;
    private const int NightStartHour = 19;
    private const float DayNightSmoothTime = 0.6f;
    private const int BrightnessSampleSize = 24;
    private const float BrightPixelThreshold = 0.65f;
    private const float DefaultBrightness = 0.35f;
    private static readonly string[] BuiltInPatterns = { "*.png", "*.jpg", "*.jpeg", "*.bmp" };

    private static readonly WallpaperEntry Fallback = new()
    {
        Id = string.Empty, Kind = WallpaperKind.BuiltIn, FilePath = string.Empty, Crop = WallpaperCrop.Cover,
    };

    private readonly ITextureProvider textures;
    private readonly DirectoryInfo customDirectory;
    private readonly Configuration configuration;
    private readonly IReadOnlyList<WallpaperEntry> builtIns;
    private readonly ConcurrentDictionary<string, IDalamudTextureWrap> ready = new();
    private readonly ConcurrentDictionary<string, float> brightness = new();
    private readonly ConcurrentDictionary<string, byte> loading = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();
    private readonly CancellationTokenSource cancellation = new();
    private Spring darknessSpring;
    private bool dayNightInitialized;

    public WallpaperLibrary(ITextureProvider textures, DirectoryInfo builtInDirectory, DirectoryInfo customDirectory,
        Configuration configuration)
    {
        this.textures = textures;
        this.customDirectory = customDirectory;
        this.configuration = configuration;
        customDirectory.Create();
        builtIns = DiscoverBuiltIns(builtInDirectory);
        Entries = Rebuild();
    }

    public IReadOnlyList<WallpaperEntry> Entries { get; private set; }
    public float CurrentTargetAspect { get; set; } = 0.5f;
    public float Darkness { get; private set; }

    public WallpaperEntry Resolve(string id)
    {
        var entries = Entries;
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].Id == id)
            {
                return entries[index];
            }
        }

        return entries.Count > 0 ? entries[0] : Fallback;
    }

    public void StepDayNight(float deltaSeconds)
    {
        var target = IsNight() ? 1f : 0f;
        if (!dayNightInitialized)
        {
            darknessSpring.SnapTo(target);
            dayNightInitialized = true;
        }

        Darkness = Math.Clamp(darknessSpring.Step(target, DayNightSmoothTime, deltaSeconds), 0f, 1f);
    }

    public ImTextureID? HandlePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (ready.TryGetValue(path, out var wrap))
        {
            return wrap.Handle;
        }

        if (failed.ContainsKey(path) || !loading.TryAdd(path, 0))
        {
            return null;
        }

        _ = LoadAsync(path);
        return null;
    }

    public Vector2 SizeOfPath(string path) => ready.TryGetValue(path, out var wrap) ? wrap.Size : Vector2.Zero;

    public float HomeBrightness(string lightId, string darkId)
    {
        var light = BrightnessOfPath(Resolve(lightId).FilePath);
        if (Darkness <= 0.001f)
        {
            return light;
        }

        var dark = BrightnessOfPath(Resolve(darkId).FilePath);
        return light + (dark - light) * Darkness;
    }

    private float BrightnessOfPath(string path) =>
        brightness.TryGetValue(path, out var value) ? value : DefaultBrightness;

    public string AddCustom(string sourcePath, WallpaperCrop crop)
    {
        var id = "custom-" + Guid.NewGuid().ToString("N");
        var fileName = id + NormalizeExtension(Path.GetExtension(sourcePath));
        File.Copy(sourcePath, Path.Combine(customDirectory.FullName, fileName), true);
        configuration.CustomWallpapers.Add(new CustomWallpaper
        {
            Id = id,
            FileName = fileName,
            Zoom = crop.Zoom,
            CenterX = crop.CenterX,
            CenterY = crop.CenterY,
        });
        configuration.Save();
        Entries = Rebuild();
        return id;
    }

    public void UpdateCrop(string id, WallpaperCrop crop)
    {
        var record = FindCustom(id);
        if (record is null)
        {
            return;
        }

        record.Zoom = crop.Zoom;
        record.CenterX = crop.CenterX;
        record.CenterY = crop.CenterY;
        configuration.Save();
        Entries = Rebuild();
    }

    public void RemoveCustom(string id)
    {
        var record = FindCustom(id);
        if (record is null)
        {
            return;
        }

        configuration.CustomWallpapers.Remove(record);
        configuration.Save();
        var path = Path.Combine(customDirectory.FullName, record.FileName);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Wallpaper] failed to delete {record.FileName}: {exception.Message}");
        }

        if (ready.TryRemove(path, out var wrap))
        {
            wrap.Dispose();
        }

        brightness.TryRemove(path, out _);
        failed.TryRemove(path, out _);
        Entries = Rebuild();
    }

    public void Dispose()
    {
        cancellation.Cancel();
        foreach (var wrap in ready.Values)
        {
            wrap.Dispose();
        }

        ready.Clear();
        cancellation.Dispose();
    }

    private static bool IsNight()
    {
        var hour = DateTime.Now.Hour;
        return hour < DayStartHour || hour >= NightStartHour;
    }

    private CustomWallpaper? FindCustom(string id)
    {
        var customs = configuration.CustomWallpapers;
        for (var index = 0; index < customs.Count; index++)
        {
            if (customs[index].Id == id)
            {
                return customs[index];
            }
        }

        return null;
    }

    private IReadOnlyList<WallpaperEntry> Rebuild()
    {
        var customs = configuration.CustomWallpapers;
        var entries = new List<WallpaperEntry>(builtIns.Count + customs.Count);
        entries.AddRange(builtIns);
        for (var index = 0; index < customs.Count; index++)
        {
            var custom = customs[index];
            entries.Add(new WallpaperEntry
            {
                Id = custom.Id,
                Kind = WallpaperKind.Custom,
                FilePath = Path.Combine(customDirectory.FullName, custom.FileName),
                Crop = new WallpaperCrop(custom.Zoom, custom.CenterX, custom.CenterY),
            });
        }

        return entries;
    }

    private static IReadOnlyList<WallpaperEntry> DiscoverBuiltIns(DirectoryInfo directory)
    {
        if (!directory.Exists)
        {
            return Array.Empty<WallpaperEntry>();
        }

        var files = new List<string>();
        for (var index = 0; index < BuiltInPatterns.Length; index++)
        {
            files.AddRange(Directory.GetFiles(directory.FullName, BuiltInPatterns[index]));
        }

        files.Sort(static (left, right) => string.CompareOrdinal(Path.GetFileName(left), Path.GetFileName(right)));
        var entries = new List<WallpaperEntry>(files.Count);
        for (var index = 0; index < files.Count; index++)
        {
            entries.Add(new WallpaperEntry
            {
                Id = Path.GetFileNameWithoutExtension(files[index]),
                Kind = WallpaperKind.BuiltIn,
                FilePath = files[index],
                Crop = WallpaperCrop.Cover,
            });
        }

        return entries;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".png";
        }

        var normalized = extension.Trim().ToLowerInvariant();
        if (!normalized.StartsWith('.'))
        {
            normalized = "." + normalized;
        }

        return normalized switch
        {
            ".png" or ".jpg" or ".jpeg" or ".bmp" => normalized,
            _ => ".png",
        };
    }

    private async Task LoadAsync(string path)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            var wrap = await ImageProcessor.DecodeToTextureAsync(textures, bytes, $"Aetherphone.Wallpaper.{path}",
                token).ConfigureAwait(false);
            RecordBrightness(path, bytes);
            if (!ready.TryAdd(path, wrap))
            {
                wrap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            failed.TryAdd(path, 0);
            AepLog.Warning($"[Wallpaper] failed to load {path}: {exception.Message}");
        }
        finally
        {
            loading.TryRemove(path, out _);
        }
    }

    private void RecordBrightness(string path, byte[] bytes)
    {
        try
        {
            brightness[path] = MeasureBrightness(bytes);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Wallpaper] brightness analysis failed for {path}: {exception.Message}");
        }
    }

    private static float MeasureBrightness(byte[] bytes)
    {
        using var image = Image.Load<Rgba32>(bytes);
        image.Mutate(context => context.Resize(BrightnessSampleSize, BrightnessSampleSize));
        var lumaSum = 0f;
        var brightCount = 0;
        image.ProcessPixelRows(accessor =>
        {
            for (var rowIndex = 0; rowIndex < accessor.Height; rowIndex++)
            {
                var row = accessor.GetRowSpan(rowIndex);
                for (var columnIndex = 0; columnIndex < row.Length; columnIndex++)
                {
                    var pixel = row[columnIndex];
                    var luma = (0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B) / 255f;
                    lumaSum += luma;
                    if (luma >= BrightPixelThreshold)
                    {
                        brightCount++;
                    }
                }
            }
        });
        const float total = BrightnessSampleSize * BrightnessSampleSize;
        var mean = lumaSum / total;
        var brightFraction = brightCount / total;
        return Math.Clamp(0.5f * mean + 0.5f * brightFraction, 0f, 1f);
    }
}
