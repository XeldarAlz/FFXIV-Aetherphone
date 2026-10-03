using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Media;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Aetherphone.Windows.Components;

internal static class BrandMark
{
    public const float CornerFraction = 0.2237f;
    public static readonly Vector4 Violet = new(0.56f, 0.40f, 1f, 1f);
    public static readonly Vector4 Lilac = new(0.80f, 0.72f, 1f, 1f);
    public static readonly Vector4 Vine = new(0.42f, 0.84f, 0.40f, 1f);
    public static readonly Vector4 Night = new(0.030f, 0.022f, 0.068f, 1f);

    private const int AmbientWidth = 160;
    private const float AmbientSigma = 14f;
    private const float AmbientSaturation = 1.15f;
    private const float StageZoom = 1.16f;
    private const double StageDriftXMs = 41000.0;
    private const double StageDriftYMs = 57000.0;
    private const int GlowLayers = 6;
    private const int SlotCount = TextureSizes.LevelCount + 1;
    private static readonly Vector4 ScrimTop = new(0.020f, 0.014f, 0.050f, 0.30f);
    private static readonly Vector4 ScrimBottom = new(0.016f, 0.010f, 0.040f, 0.58f);
    private static readonly Vector4 DayWash = new(0.97f, 0.96f, 1f, 0.78f);
    private static readonly Vector4 DayFlat = new(0.95f, 0.94f, 0.99f, 1f);
    private static readonly Vector4 DayScrimTop = new(1f, 1f, 1f, 0.45f);
    private static readonly Vector4 DayScrimBottom = new(0.98f, 0.97f, 1f, 0.70f);
    private static readonly Vector2 RimLight = new(-0.55f, -1f);
    private const int ShockwaveRings = 3;
    private const float ShockwaveReach = 1.35f;
    private const double SheenPeriodMs = 6400.0;
    private const float SheenSweepFraction = 0.16f;
    private const int SheenSpan = 3;
    private const int SheenHeight = 48;
    private const float SheenSlant = 0.55f;
    private const float SheenWidth = 0.16f;
    private const float SheenPeak = 0.38f;
    private const int MoteCount = 34;
    private const float MoteMinSeconds = 11f;
    private const float MoteSpreadSeconds = 12f;
    private const float MoteSwayUnits = 9f;
    private static readonly Vector4[] MoteSeeds = BuildMoteSeeds();

    private static readonly MarkSource Tile = new("Icon.png");
    private static readonly MarkSource Emblem = new("Emblem.png");
    private static IDalamudTextureWrap? ambient;
    private static int ambientLoading;
    private static IDalamudTextureWrap? sheenTexture;
    private static int sheenLoading;
    private static int generation;

    public static bool TryDraw(ImDrawListPtr drawList, Vector2 center, float size, float alpha, float scale) =>
        TryDraw(drawList, center, size, alpha, scale, AutoSheen());

    public static bool TryDraw(ImDrawListPtr drawList, Vector2 center, float size, float alpha, float scale,
        float sheen)
    {
        if (alpha <= 0.001f || size <= 1f)
        {
            return true;
        }

        if (!TryResolve(Tile, TextureSizes.LevelFor(size), out var texture))
        {
            return false;
        }

        var half = size * 0.5f;
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        var radius = size * CornerFraction;
        Glow(drawList, center, size, alpha);
        Elevation.Draw(drawList, min, max, radius, 1f, size * 0.10f, size * 0.07f, 0.42f, alpha);
        Squircle.FillImage(drawList, min, max, radius, texture, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
        Sheen(drawList, min, max, radius, sheen, alpha);
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f * alpha)),
            1f * scale);
        Squircle.StrokeDirectional(drawList, min, max, radius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.42f * alpha)), 1.3f * scale, RimLight, 2.2f);
        return true;
    }

    public static bool TryDrawEmblem(ImDrawListPtr drawList, Vector2 center, float size, float alpha)
    {
        if (alpha <= 0.001f || size <= 1f)
        {
            return true;
        }

        if (!TryResolve(Emblem, TextureSizes.LevelFor(size), out var texture))
        {
            return false;
        }

        var half = new Vector2(size * 0.5f, size * 0.5f);
        Glow(drawList, center, size * 0.8f, alpha);
        drawList.AddImage(texture, center - half, center + half, Vector2.Zero, Vector2.One,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
        return true;
    }

    public static void Glow(ImDrawListPtr drawList, Vector2 center, float size, float alpha)
    {
        var breath = 0.85f + 0.15f * Pulse.Wave(Pulse.Breath);
        var color = ImGui.GetColorU32(Violet with { W = 0.045f * alpha * breath });
        for (var layerIndex = 0; layerIndex < GlowLayers; layerIndex++)
        {
            var spread = size * (0.5f + 0.16f * (layerIndex + 1));
            var min = new Vector2(center.X - spread, center.Y - spread);
            var max = new Vector2(center.X + spread, center.Y + spread);
            Squircle.Fill(drawList, min, max, spread * 2f * CornerFraction * 1.4f, color);
        }
    }

    public static void DrawStage(ImDrawListPtr drawList, Rect screen, float rounding, float alpha, bool record,
        float darkness)
    {
        if (alpha <= 0f)
        {
            return;
        }

        Squircle.Fill(drawList, screen.Min, screen.Max, rounding, ImGui.GetColorU32(Night with { W = alpha }));
        if (TryResolveAmbient(out var texture))
        {
            var (uv0, uv1) = StageUv(screen);
            Squircle.FillImage(drawList, screen.Min, screen.Max, rounding, texture,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), uv0, uv1);
            if (record && darkness >= 0.5f)
            {
                WallpaperBackdrop.Record(screen, texture, uv0, uv1, null);
            }
        }
        else if (record && darkness >= 0.5f)
        {
            WallpaperBackdrop.Clear();
            WallpaperBackdrop.RecordFlat(Night);
        }

        if (record && darkness < 0.5f)
        {
            WallpaperBackdrop.Clear();
            WallpaperBackdrop.RecordFlat(DayFlat);
        }

        var light = 1f - darkness;
        if (light > 0.001f)
        {
            Squircle.Fill(drawList, screen.Min, screen.Max, rounding,
                ImGui.GetColorU32(DayWash with { W = DayWash.W * light * alpha }));
        }

        var top = Vector4.Lerp(DayScrimTop, ScrimTop, darkness);
        var bottom = Vector4.Lerp(DayScrimBottom, ScrimBottom, darkness);
        Squircle.FillVerticalGradient(drawList, screen.Min, screen.Max, rounding,
            ImGui.GetColorU32(top with { W = top.W * alpha }), ImGui.GetColorU32(top with { W = 0f }));
        Squircle.FillVerticalGradient(drawList, screen.Min, screen.Max, rounding,
            ImGui.GetColorU32(bottom with { W = 0f }), ImGui.GetColorU32(bottom with { W = bottom.W * alpha }));
        DrawMotes(drawList, screen, alpha, darkness);
    }

    public static void Shockwave(ImDrawListPtr drawList, Vector2 center, float size, float progress, float alpha,
        float scale)
    {
        if (progress <= 0f || progress >= 1f || alpha <= 0.001f)
        {
            return;
        }

        var fade = (1f - progress) * (1f - progress);
        for (var ringIndex = 0; ringIndex < ShockwaveRings; ringIndex++)
        {
            var lag = ringIndex * 0.12f;
            var local = Math.Clamp((progress - lag) / (1f - lag), 0f, 1f);
            if (local <= 0f)
            {
                continue;
            }

            var ringSize = size * (1f + ShockwaveReach * Spring.Settle(local, 0.35f));
            var half = new Vector2(ringSize * 0.5f, ringSize * 0.5f);
            var ink = ringIndex == 0 ? Lilac : Violet;
            Squircle.Stroke(drawList, center - half, center + half, ringSize * CornerFraction,
                ImGui.GetColorU32(ink with { W = 0.55f * fade * alpha / (ringIndex + 1) }),
                (2.2f - ringIndex * 0.6f) * scale);
        }
    }

    public static float AutoSheen()
    {
        var phase = Pulse.Phase(SheenPeriodMs);
        return phase < SheenSweepFraction ? phase / SheenSweepFraction : -1f;
    }

    public static void Sheen(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float progress,
        float alpha)
    {
        if (progress < 0f || progress > 1f || !TryResolveSheen(out var texture))
        {
            return;
        }

        var window = 1f / SheenSpan;
        var offset = (1f - progress) * (1f - window);
        Squircle.FillImage(drawList, min, max, radius, texture,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), new Vector2(offset, 0f),
            new Vector2(offset + window, 1f));
    }

    private static void DrawMotes(ImDrawListPtr drawList, Rect screen, float alpha, float darkness)
    {
        var scale = UiScale.Current;
        var seconds = (float)(Environment.TickCount64 % 3_600_000L / 1000.0);
        var height = screen.Height * 1.08f;
        drawList.PushClipRect(screen.Min, screen.Max, true);
        for (var moteIndex = 0; moteIndex < MoteCount; moteIndex++)
        {
            var seed = MoteSeeds[moteIndex];
            var cycle = seconds / (MoteMinSeconds + seed.Z * MoteSpreadSeconds) + seed.W;
            var phase = cycle - MathF.Floor(cycle);
            var sway = MathF.Sin(seconds * 0.35f + seed.Y * 6.283f) * MoteSwayUnits * scale;
            var position = new Vector2(screen.Min.X + seed.X * screen.Width + sway,
                screen.Max.Y + screen.Height * 0.04f - phase * height);
            var twinkle = 0.55f + 0.45f * MathF.Sin(seconds * (1.3f + seed.Z * 1.7f) + seed.X * 9f);
            var life = MathF.Sin(phase * MathF.PI);
            var strength = life * twinkle * alpha;
            if (strength <= 0.01f)
            {
                continue;
            }

            var radius = (0.7f + seed.Y * 1.5f) * scale;
            var ink = moteIndex % 7 == 0 ? Vine : moteIndex % 3 == 0 ? Vector4.Lerp(Violet, Vector4.One, darkness) :
                Vector4.Lerp(Violet, Lilac, darkness);
            drawList.AddCircleFilled(position, radius * 4f, ImGui.GetColorU32(ink with { W = 0.06f * strength }), 16);
            drawList.AddCircleFilled(position, radius * 2f, ImGui.GetColorU32(ink with { W = 0.14f * strength }), 12);
            drawList.AddCircleFilled(position, radius, ImGui.GetColorU32(ink with { W = 0.85f * strength }), 10);
        }

        drawList.PopClipRect();
    }

    public static void Dispose()
    {
        Interlocked.Increment(ref generation);
        Tile.Dispose();
        Emblem.Dispose();
        Interlocked.Exchange(ref ambient, null)?.Dispose();
        Interlocked.Exchange(ref ambientLoading, 0);
        Interlocked.Exchange(ref sheenTexture, null)?.Dispose();
        Interlocked.Exchange(ref sheenLoading, 0);
    }

    private static (Vector2 Min, Vector2 Max) StageUv(Rect screen)
    {
        var aspect = screen.Height > 0f ? screen.Width / screen.Height : 0.5f;
        var spanX = MathF.Min(1f, aspect) / StageZoom;
        var spanY = MathF.Min(1f, 1f / aspect) / StageZoom;
        var driftX = MathF.Sin(Pulse.Phase(StageDriftXMs) * MathF.PI * 2f) * (1f - spanX) * 0.5f;
        var driftY = MathF.Cos(Pulse.Phase(StageDriftYMs) * MathF.PI * 2f) * (1f - spanY) * 0.5f;
        var center = new Vector2(0.5f + driftX, 0.5f + driftY);
        var half = new Vector2(spanX, spanY) * 0.5f;
        return (center - half, center + half);
    }

    private static bool TryResolve(MarkSource source, int level, out ImTextureID texture)
    {
        texture = default;
        if (source.Failed)
        {
            return false;
        }

        if (source.Levels[level] is { } wrap)
        {
            texture = wrap.Handle;
            return true;
        }

        if (Interlocked.CompareExchange(ref source.Loading[level], 1, 0) == 0)
        {
            var stamp = generation;
            var size = TextureSizes.SizeOf(level);
            _ = Task.Run(() => BuildAsync(source, level, stamp, size, false));
        }

        for (var distance = 1; distance < SlotCount; distance++)
        {
            var above = level + distance;
            if (above < SlotCount && source.Levels[above] is { } larger)
            {
                texture = larger.Handle;
                return true;
            }

            var below = level - distance;
            if (below > TextureSizes.Native && source.Levels[below] is { } smaller)
            {
                texture = smaller.Handle;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveAmbient(out ImTextureID texture)
    {
        texture = default;
        if (ambient is { } wrap)
        {
            texture = wrap.Handle;
            return true;
        }

        if (!Tile.Failed && Interlocked.CompareExchange(ref ambientLoading, 1, 0) == 0)
        {
            var stamp = generation;
            _ = Task.Run(() => BuildAsync(Tile, 0, stamp, AmbientWidth, true));
        }

        return false;
    }

    private static bool TryResolveSheen(out ImTextureID texture)
    {
        texture = default;
        if (sheenTexture is { } wrap)
        {
            texture = wrap.Handle;
            return true;
        }

        if (Interlocked.CompareExchange(ref sheenLoading, 1, 0) == 0)
        {
            var stamp = generation;
            _ = Task.Run(() => BuildSheenAsync(stamp));
        }

        return false;
    }

    private static async Task BuildSheenAsync(int stamp)
    {
        try
        {
            var width = SheenHeight * SheenSpan;
            var pixels = new byte[width * SheenHeight * 4];
            for (var row = 0; row < SheenHeight; row++)
            {
                var v = (row + 0.5f) / SheenHeight;
                for (var column = 0; column < width; column++)
                {
                    var u = (column + 0.5f) / SheenHeight;
                    var line = SheenSpan * 0.5f + (0.5f - v) * SheenSlant;
                    var distance = (u - line) / SheenWidth;
                    var strength = MathF.Exp(-distance * distance) * SheenPeak;
                    var offset = (row * width + column) * 4;
                    pixels[offset] = 255;
                    pixels[offset + 1] = 255;
                    pixels[offset + 2] = 255;
                    pixels[offset + 3] = (byte)MathF.Round(strength * 255f);
                }
            }

            var wrap = await Plugin.TextureProvider.CreateFromRawAsync(
                RawImageSpecification.Rgba32(width, SheenHeight), pixels, "Aetherphone.Brand.sheen",
                CancellationToken.None).ConfigureAwait(false);
            if (stamp != generation || Interlocked.CompareExchange(ref sheenTexture, wrap, null) is not null)
            {
                wrap.Dispose();
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Brand] failed to build the sheen texture");
        }
    }

    private static Vector4[] BuildMoteSeeds()
    {
        var seeds = new Vector4[MoteCount];
        var random = new Random(7919);
        for (var moteIndex = 0; moteIndex < MoteCount; moteIndex++)
        {
            seeds[moteIndex] = new Vector4(random.NextSingle(), random.NextSingle(), random.NextSingle(),
                random.NextSingle());
        }

        return seeds;
    }

    private static async Task BuildAsync(MarkSource source, int level, int stamp, int size, bool blurred)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(source.Path).ConfigureAwait(false);
            var (pixels, width, height) = Bake(bytes, size, blurred);
            var wrap = await Plugin.TextureProvider.CreateFromRawAsync(RawImageSpecification.Rgba32(width, height),
                pixels, $"Aetherphone.Brand.{source.Name}.{(blurred ? "ambient" : size.ToString())}",
                CancellationToken.None).ConfigureAwait(false);
            if (stamp != generation)
            {
                wrap.Dispose();
                return;
            }

            var previous = blurred
                ? Interlocked.CompareExchange(ref ambient, wrap, null)
                : Interlocked.CompareExchange(ref source.Levels[level], wrap, null);
            if (previous is not null)
            {
                wrap.Dispose();
            }
        }
        catch (Exception exception)
        {
            source.Failed = true;
            AepLog.Warning(exception,
                $"[Brand] failed to bake the {(blurred ? "ambient" : $"{size}px")} {source.Name} brand mark");
        }
    }

    private static (byte[] Pixels, int Width, int Height) Bake(byte[] bytes, int size, bool blurred)
    {
        using var image = Image.Load<Rgba32>(ImageProcessor.SingleFrame, bytes);
        var width = Math.Max(1, Math.Min(size, image.Width));
        var height = Math.Max(1, (int)MathF.Round(image.Height * (width / (float)image.Width)));
        if (blurred)
        {
            image.Mutate(context => context.Resize(width, height).GaussianBlur(AmbientSigma)
                .Saturate(AmbientSaturation));
        }
        else if (width != image.Width)
        {
            image.Mutate(context => context.Resize(width, height, KnownResamplers.Lanczos3));
        }

        var pixels = new byte[width * height * 4];
        image.CopyPixelDataTo(pixels);
        return (pixels, width, height);
    }

    private sealed class MarkSource
    {
        public readonly string Name;
        public readonly string Path;
        public readonly IDalamudTextureWrap?[] Levels = new IDalamudTextureWrap?[SlotCount];
        public readonly int[] Loading = new int[SlotCount];
        public volatile bool Failed;

        public MarkSource(string fileName)
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(fileName);
            Path = System.IO.Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty,
                "Images", fileName);
        }

        public void Dispose()
        {
            for (var slotIndex = 0; slotIndex < SlotCount; slotIndex++)
            {
                Interlocked.Exchange(ref Levels[slotIndex], null)?.Dispose();
                Interlocked.Exchange(ref Loading[slotIndex], 0);
            }
        }
    }
}
