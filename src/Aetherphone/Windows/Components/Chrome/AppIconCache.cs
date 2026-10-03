using System.Collections.Concurrent;
using Aetherphone.Core;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal static class AppIconCache
{
    public const string ForegroundSuffix = ".fg.png";
    public const string FinishedSuffix = ".png";
    public const long IdleDropMilliseconds = 120_000;
    public const long SweepIntervalMilliseconds = 5_000;
    private const long FailureRetryMilliseconds = 60_000;
    private const int AppearanceCount = 4;
    private const int SlotCount = AppearanceCount * TextureSizes.LevelCount;
    private const byte StateNone = 0;
    private const byte StateBuilding = 1;
    private const byte StateReady = 2;
    private const byte StateFailed = 3;

    private sealed class IconEntry
    {
        public readonly string FinishedPath;
        public readonly string ForegroundPath;
        public readonly IDalamudTextureWrap?[] Wraps = new IDalamudTextureWrap?[SlotCount];
        public readonly byte[] States = new byte[SlotCount];
        public readonly long[] LastDrawnTicks = new long[SlotCount];
        public readonly long[] FailedAtTicks = new long[SlotCount];
        public readonly int[] Generations = new int[SlotCount];
        public readonly int[] ResidentLevels = new int[AppearanceCount];
        public bool Painted;

        public IconEntry(string finishedPath, string foregroundPath, bool painted)
        {
            FinishedPath = finishedPath;
            ForegroundPath = foregroundPath;
            Painted = painted;
        }
    }

    private readonly struct Completion
    {
        public readonly IconEntry Entry;
        public readonly int Slot;
        public readonly int Generation;
        public readonly IDalamudTextureWrap? Wrap;

        public Completion(IconEntry entry, int slot, int generation, IDalamudTextureWrap? wrap)
        {
            Entry = entry;
            Slot = slot;
            Generation = generation;
            Wrap = wrap;
        }
    }

    private static readonly string IconDirectory =
        Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Icons");

    private static readonly Dictionary<string, IconEntry> Entries = new(StringComparer.Ordinal);
    private static readonly ConcurrentQueue<Completion> Completions = new();
    private static int preparedFrame = -1;
    private static long lastSweepTicks;
    private static uint tintStamp;
    private static Vector4 tintAccent;
    private static bool lightTheme;
    private static volatile bool disposed;

    public static float GlassBrightness { get; private set; }

    public static bool IsPainted(string appId)
    {
        if (disposed)
        {
            return false;
        }

        if (Entries.TryGetValue(appId, out var entry))
        {
            return entry.Painted;
        }

        var finished = Path.Combine(IconDirectory, appId + FinishedSuffix);
        var foreground = Path.Combine(IconDirectory, appId + ForegroundSuffix);
        var painted = File.Exists(foreground) && File.Exists(finished);
        Entries[appId] = new IconEntry(finished, foreground, painted);
        return painted;
    }

    public static void Prepare()
    {
        var frame = ImGui.GetFrameCount();
        if (frame == preparedFrame)
        {
            return;
        }

        preparedFrame = frame;
        DrainCompletions();
        var configuration = Plugin.Cfg;
        tintAccent = ThemeCatalog.ResolveAccent(configuration.AccentName);
        lightTheme = IsLightTheme(configuration);
        var stamp = PackStamp(tintAccent, lightTheme);
        if (stamp != tintStamp)
        {
            tintStamp = stamp;
            DropAppearance(IconAppearance.Tinted);
        }

        GlassBrightness = WallpaperLegibility.Normalize(
            Plugin.Wallpapers.HomeBrightness(configuration.LightWallpaperId, configuration.DarkWallpaperId));
        var now = Environment.TickCount64;
        if (now - lastSweepTicks >= SweepIntervalMilliseconds)
        {
            lastSweepTicks = now;
            SweepIdle(now);
        }
    }

    public static IDalamudTextureWrap? Resolve(string appId, IconAppearance appearance, int level, Vector4 accent)
    {
        if (!Entries.TryGetValue(appId, out var entry) || !entry.Painted)
        {
            return null;
        }

        var now = Environment.TickCount64;
        var slot = Slot(appearance, level);
        entry.LastDrawnTicks[slot] = now;
        var state = entry.States[slot];
        if (state == StateReady)
        {
            return entry.Wraps[slot];
        }

        if (state == StateNone
            || (state == StateFailed && now - entry.FailedAtTicks[slot] >= FailureRetryMilliseconds))
        {
            StartBuild(entry, appId, slot, appearance, level, accent);
        }

        var nearest = IconLadder.Nearest(entry.ResidentLevels[(int)appearance], level);
        if (nearest == TextureSizes.Native)
        {
            return null;
        }

        var nearestSlot = Slot(appearance, nearest);
        entry.LastDrawnTicks[nearestSlot] = now;
        return entry.Wraps[nearestSlot];
    }

    public static void Disable(string appId)
    {
        if (!Entries.TryGetValue(appId, out var entry))
        {
            return;
        }

        entry.Painted = false;
        for (var slot = 0; slot < SlotCount; slot++)
        {
            Drop(entry, slot, true);
        }
    }

    public static void Dispose()
    {
        disposed = true;
        foreach (var pair in Entries)
        {
            var entry = pair.Value;
            for (var slot = 0; slot < SlotCount; slot++)
            {
                entry.Generations[slot]++;
                entry.Wraps[slot]?.Dispose();
                entry.Wraps[slot] = null;
                entry.States[slot] = StateNone;
            }

            Array.Clear(entry.ResidentLevels);
        }

        Entries.Clear();
        while (Completions.TryDequeue(out var completion))
        {
            completion.Wrap?.Dispose();
        }
    }

    private static void StartBuild(IconEntry entry, string appId, int slot, IconAppearance appearance, int level,
        Vector4 accent)
    {
        if (disposed)
        {
            return;
        }

        entry.States[slot] = StateBuilding;
        var generation = entry.Generations[slot];
        var size = TextureSizes.SizeOf(level);
        var tint = tintAccent;
        var light = lightTheme;
        _ = Task.Run(() => BuildAsync(entry, appId, slot, generation, appearance, size, accent, tint, light));
    }

    private static async Task BuildAsync(IconEntry entry, string appId, int slot, int generation,
        IconAppearance appearance, int size, Vector4 accent, Vector4 tint, bool light)
    {
        IDalamudTextureWrap? wrap = null;
        try
        {
            var baked = Bake(entry, appearance, size, accent, tint, light);
            if (baked.IsEmpty)
            {
                throw new InvalidOperationException("The icon decoded to an empty image.");
            }

            wrap = await Plugin.TextureProvider.CreateFromRawAsync(
                    RawImageSpecification.Rgba32(baked.Width, baked.Height), baked.Pixels.AsMemory(0, baked.Length),
                    $"Aetherphone.Icon.{appId}.{appearance}.{size}", CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Icons] failed to build the {appearance} icon for {appId} at {size}px");
        }

        if (disposed)
        {
            wrap?.Dispose();
            return;
        }

        Completions.Enqueue(new Completion(entry, slot, generation, wrap));
    }

    private static PixelImage Bake(IconEntry entry, IconAppearance appearance, int size, Vector4 accent,
        Vector4 tint, bool light)
    {
        if (appearance == IconAppearance.Default)
        {
            return ImageProcessor.DecodeLocalRgba32(entry.FinishedPath, size);
        }

        var foreground = ImageProcessor.DecodeLocalRgba32(entry.ForegroundPath, size);
        switch (appearance)
        {
            case IconAppearance.Dark:
                return IconBake.BakeDark(foreground, accent);
            case IconAppearance.Tinted:
                return IconBake.BakeTinted(foreground, tint, light);
            default:
                return IconBake.BakeMask(foreground);
        }
    }

    private static void DrainCompletions()
    {
        while (Completions.TryDequeue(out var completion))
        {
            var entry = completion.Entry;
            var slot = completion.Slot;
            if (disposed || !entry.Painted || entry.Generations[slot] != completion.Generation)
            {
                completion.Wrap?.Dispose();
                continue;
            }

            if (completion.Wrap is null)
            {
                entry.States[slot] = StateFailed;
                entry.FailedAtTicks[slot] = Environment.TickCount64;
                continue;
            }

            entry.Wraps[slot] = completion.Wrap;
            entry.States[slot] = StateReady;
            entry.LastDrawnTicks[slot] = Environment.TickCount64;
            entry.ResidentLevels[slot / TextureSizes.LevelCount] |= IconLadder.Bit(slot % TextureSizes.LevelCount + 1);
        }
    }

    private static void SweepIdle(long now)
    {
        foreach (var pair in Entries)
        {
            var entry = pair.Value;
            for (var slot = 0; slot < SlotCount; slot++)
            {
                if (entry.States[slot] == StateReady && now - entry.LastDrawnTicks[slot] >= IdleDropMilliseconds)
                {
                    Drop(entry, slot, false);
                }
            }
        }
    }

    private static void DropAppearance(IconAppearance appearance)
    {
        var first = (int)appearance * TextureSizes.LevelCount;
        foreach (var pair in Entries)
        {
            for (var slot = first; slot < first + TextureSizes.LevelCount; slot++)
            {
                Drop(pair.Value, slot, true);
            }
        }
    }

    private static void Drop(IconEntry entry, int slot, bool deferred)
    {
        var state = entry.States[slot];
        if (state == StateNone)
        {
            return;
        }

        entry.Generations[slot]++;
        var wrap = entry.Wraps[slot];
        entry.Wraps[slot] = null;
        entry.States[slot] = StateNone;
        entry.ResidentLevels[slot / TextureSizes.LevelCount] &= ~IconLadder.Bit(slot % TextureSizes.LevelCount + 1);
        if (wrap is null)
        {
            return;
        }

        if (deferred)
        {
            DeferredDispose.Later(wrap);
        }
        else
        {
            wrap.Dispose();
        }
    }

    private static int Slot(IconAppearance appearance, int level) =>
        (int)appearance * TextureSizes.LevelCount + level - 1;

    private static bool IsLightTheme(Configuration configuration) =>
        configuration.ThemeMode switch
        {
            ThemeMode.Light => true,
            ThemeMode.Dark => false,
            _ => Plugin.Wallpapers.Darkness < 0.5f,
        };

    private static uint PackStamp(Vector4 accent, bool light)
    {
        var red = (uint)Math.Clamp((int)MathF.Round(accent.X * 255f), 0, 255);
        var green = (uint)Math.Clamp((int)MathF.Round(accent.Y * 255f), 0, 255);
        var blue = (uint)Math.Clamp((int)MathF.Round(accent.Z * 255f), 0, 255);
        return red | (green << 8) | (blue << 16) | (light ? 1u << 24 : 0u);
    }
}
