using System.Collections.Concurrent;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
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
    private const float ThemeSwitchSmoothTime = 0.35f;
    public const int BrightnessSampleSize = 24;
    private const float BrightPixelThreshold = 0.65f;
    private const float DefaultBrightness = 0.35f;
    private const int BlurBakeWidth = 320;
    private const float BlurSigma = 9f;
    private const float BlurSaturation = 1.3f;
    private static readonly int[] LevelExtents = { 640, 1280, 2560 };
    private static readonly int LevelCount = LevelExtents.Length + 1;
    private static readonly string[] BuiltInPatterns = { "*.png", "*.jpg", "*.jpeg", "*.bmp" };

    private sealed class LevelSet
    {
        public readonly IDalamudTextureWrap?[] Wraps = new IDalamudTextureWrap?[LevelCount];
        public readonly int[] Loading = new int[LevelCount];
    }

    private static readonly WallpaperEntry Fallback = new()
    {
        Id = string.Empty, Kind = WallpaperKind.BuiltIn, FilePath = string.Empty, Crop = WallpaperCrop.Cover,
    };

    private readonly ITextureProvider textures;
    private readonly DirectoryInfo customDirectory;
    private readonly Configuration configuration;
    private readonly IReadOnlyList<WallpaperEntry> builtIns;
    private readonly ConcurrentDictionary<string, LevelSet> ready = new();
    private readonly ConcurrentDictionary<string, IDalamudTextureWrap> blurred = new();
    private readonly ConcurrentDictionary<string, byte> blurring = new();
    private readonly ConcurrentDictionary<string, float> brightness = new();
    private readonly ConcurrentDictionary<string, float[]> lumaGrids = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();
    private readonly CancellationTokenSource cancellation = new();
    private Spring darknessSpring;
    private bool dayNightInitialized;
    private Spring themeDarknessSpring;
    private bool themeDarknessInitialized;
    private float themeDarkness;

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

    public float ThemeDarkness => themeDarkness;

    private float ThemeDarknessTarget =>
        configuration.ThemeMode switch
        {
            ThemeMode.Light => 0f,
            ThemeMode.Dark => 1f,
            _ => Darkness,
        };

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
        StepThemeDarkness(deltaSeconds);
    }

    private void StepThemeDarkness(float deltaSeconds)
    {
        var target = ThemeDarknessTarget;
        if (!themeDarknessInitialized)
        {
            themeDarknessSpring.SnapTo(target);
            themeDarknessInitialized = true;
        }

        themeDarkness = Math.Clamp(themeDarknessSpring.Step(target, ThemeSwitchSmoothTime, deltaSeconds), 0f, 1f);
    }

    public bool TryGetTexture(string path, float drawnExtent, out ImTextureID handle, out Vector2 size)
    {
        handle = default;
        size = Vector2.Zero;
        if (string.IsNullOrEmpty(path) || failed.ContainsKey(path))
        {
            return false;
        }

        var level = LevelFor(drawnExtent);
        var set = ready.GetOrAdd(path, static _ => new LevelSet());
        if (set.Wraps[level] is { } wrap)
        {
            handle = wrap.Handle;
            size = wrap.Size;
            return true;
        }

        if (Interlocked.CompareExchange(ref set.Loading[level], 1, 0) == 0)
        {
            _ = LoadAsync(path, level, set);
        }

        return TryNearest(set, level, out handle, out size);
    }

    private static int LevelFor(float drawnExtent)
    {
        for (var index = 0; index < LevelExtents.Length; index++)
        {
            if (drawnExtent <= LevelExtents[index])
            {
                return index;
            }
        }

        return LevelExtents.Length;
    }

    private static bool TryNearest(LevelSet set, int wanted, out ImTextureID handle, out Vector2 size)
    {
        handle = default;
        size = Vector2.Zero;
        var bestDistance = int.MaxValue;
        for (var index = 0; index < LevelCount; index++)
        {
            if (set.Wraps[index] is not { } wrap)
            {
                continue;
            }

            var distance = Math.Abs(index - wanted) * 2 - (index > wanted ? 1 : 0);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            handle = wrap.Handle;
            size = wrap.Size;
        }

        return bestDistance != int.MaxValue;
    }

    public bool TryGetBlurred(string path, out ImTextureID handle, out Vector2 size)
    {
        handle = default;
        size = Vector2.Zero;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (blurred.TryGetValue(path, out var wrap))
        {
            handle = wrap.Handle;
            size = wrap.Size;
            return true;
        }

        if (failed.ContainsKey(path) || !blurring.TryAdd(path, 0))
        {
            return false;
        }

        _ = LoadBlurredAsync(path);
        return false;
    }

    public float HomeBrightness(string lightId, string darkId)
    {
        var darkness = ThemeDarkness;
        var light = BrightnessOfPath(Resolve(lightId).FilePath);
        if (darkness <= 0.001f)
        {
            return light;
        }

        var dark = BrightnessOfPath(Resolve(darkId).FilePath);
        return light + (dark - light) * darkness;
    }

    private float BrightnessOfPath(string path) =>
        brightness.TryGetValue(path, out var value) ? value : DefaultBrightness;

    public float[]? LumaGrid(string path) => lumaGrids.TryGetValue(path, out var grid) ? grid : null;

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
            AepLog.Warning(exception, $"[Wallpaper] failed to delete {record.FileName}");
        }

        if (ready.TryRemove(path, out var set))
        {
            DisposeLevels(set);
        }

        if (blurred.TryRemove(path, out var blurredWrap))
        {
            blurredWrap.Dispose();
        }

        brightness.TryRemove(path, out _);
        lumaGrids.TryRemove(path, out _);
        failed.TryRemove(path, out _);
        Entries = Rebuild();
    }

    public void Dispose()
    {
        cancellation.Cancel();
        foreach (var set in ready.Values)
        {
            DisposeLevels(set);
        }

        ready.Clear();
        foreach (var wrap in blurred.Values)
        {
            wrap.Dispose();
        }

        blurred.Clear();
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

    private static void DisposeLevels(LevelSet set)
    {
        for (var index = 0; index < LevelCount; index++)
        {
            set.Wraps[index]?.Dispose();
            set.Wraps[index] = null;
        }
    }

    private async Task LoadAsync(string path, int level, LevelSet set)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            var maxDimension = level < LevelExtents.Length ? LevelExtents[level] : 0;
            var wrap = await ImageProcessor.DecodeToTextureAsync(textures, bytes,
                $"Aetherphone.Wallpaper.{level}.{path}", ImageProcessor.MaxLocalDecodePixels, maxDimension,
                token).ConfigureAwait(false);
            if (!brightness.ContainsKey(path))
            {
                RecordBrightness(path, bytes);
            }

            if (Interlocked.CompareExchange(ref set.Wraps[level], wrap, null) is not null)
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
            AepLog.Warning(exception, $"[Wallpaper] failed to load {path}");
        }
        finally
        {
            Interlocked.Exchange(ref set.Loading[level], 0);
        }
    }

    private async Task LoadBlurredAsync(string path)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            var (pixels, width, height) = await Task.Run(() => BakeBlurred(bytes), token).ConfigureAwait(false);
            var wrap = await textures.CreateFromRawAsync(RawImageSpecification.Rgba32(width, height), pixels,
                $"Aetherphone.Wallpaper.Blur.{path}", token).ConfigureAwait(false);
            if (!blurred.TryAdd(path, wrap))
            {
                wrap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Wallpaper] failed to bake the blurred copy of {path}");
        }
        finally
        {
            blurring.TryRemove(path, out _);
        }
    }

    private static (byte[] Pixels, int Width, int Height) BakeBlurred(byte[] bytes)
    {
        using var image = Image.Load<Rgba32>(ImageProcessor.SingleFrame, bytes);
        var width = Math.Max(1, Math.Min(BlurBakeWidth, image.Width));
        var height = Math.Max(1, (int)MathF.Round(image.Height * (width / (float)image.Width)));
        image.Mutate(context => context.Resize(width, height).GaussianBlur(BlurSigma).Saturate(BlurSaturation));
        var pixels = new byte[width * height * 4];
        image.CopyPixelDataTo(pixels);
        return (pixels, width, height);
    }

    private void RecordBrightness(string path, byte[] bytes)
    {
        try
        {
            var (score, grid) = MeasureBrightness(bytes);
            brightness[path] = score;
            lumaGrids[path] = grid;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Wallpaper] brightness analysis failed for {path}");
        }
    }

    private static (float Score, float[] Grid) MeasureBrightness(byte[] bytes)
    {
        using var image = Image.Load<Rgba32>(ImageProcessor.SingleFrame, bytes);
        image.Mutate(context => context.Resize(BrightnessSampleSize, BrightnessSampleSize));
        var grid = new float[BrightnessSampleSize * BrightnessSampleSize];
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
                    grid[rowIndex * BrightnessSampleSize + columnIndex] = luma;
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
        return (Math.Clamp(0.5f * mean + 0.5f * brightFraction, 0f, 1f), grid);
    }
}
