using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Aetherphone.Harness.Rendering;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeTextureProvider : ITextureProvider
{
    private const int FormatRgba32 = 28;
    private const int FormatBgra32 = 87;
    private const int PlaceholderIconSize = 64;
    private readonly TextureStore store;
    private readonly FakeDataManager data;
    private readonly Dictionary<string, FakeSharedTexture> shared = new(StringComparer.OrdinalIgnoreCase);
    private readonly FakeTextureWrap empty;

    public FakeTextureProvider(TextureStore store, FakeDataManager data)
    {
        this.store = store;
        this.data = data;
        empty = FakeTextureWrap.Upload(store, CpuTexture.Solid(4, 4, 0, 0, 0, 0), false);
    }

    public ISharedImmediateTexture GetFromFile(string path) => Shared("file:" + path, () => Upload(LoadImage(path), false));

    public ISharedImmediateTexture GetFromFile(FileInfo file) => GetFromFile(file.FullName);

    public ISharedImmediateTexture GetFromFileAbsolute(string fullPath) => GetFromFile(fullPath);

    public ISharedImmediateTexture GetFromGame(string path) => Shared("game:" + path, () => Upload(LoadGameTexture(path), false));

    public ISharedImmediateTexture GetFromGameIcon(in GameIconLookup lookup)
    {
        var iconId = lookup.IconId;
        var hiRes = lookup.HiRes;
        var itemHq = lookup.ItemHq;
        return Shared($"icon:{iconId}:{hiRes}:{itemHq}", () => Upload(LoadGameIcon(iconId, hiRes, itemHq), false));
    }

    public bool TryGetFromGameIcon(in GameIconLookup lookup, [NotNullWhen(true)] out ISharedImmediateTexture? texture)
    {
        texture = GetFromGameIcon(in lookup);
        return true;
    }

    public ISharedImmediateTexture GetFromManifestResource(Assembly assembly, string name) => throw new NotSupportedException();

    public string GetIconPath(in GameIconLookup lookup) => IconPath(lookup.IconId, lookup.HiRes, lookup.ItemHq);

    public bool TryGetIconPath(in GameIconLookup lookup, [NotNullWhen(true)] out string? path)
    {
        path = GetIconPath(in lookup);
        return true;
    }

    public IDalamudTextureWrap CreateFromRaw(RawImageSpecification specs, ReadOnlySpan<byte> bytes, string? debugName = null) =>
        Upload(FromRaw(specs, bytes), true);

    public Task<IDalamudTextureWrap> CreateFromRawAsync(RawImageSpecification specs, ReadOnlyMemory<byte> bytes,
        string? debugName = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateFromRaw(specs, bytes.Span, debugName));

    public Task<IDalamudTextureWrap> CreateFromRawAsync(RawImageSpecification specs, Stream stream, bool leaveOpen = false,
        string? debugName = null, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        if (!leaveOpen)
        {
            stream.Dispose();
        }

        return Task.FromResult(CreateFromRaw(specs, buffer.ToArray(), debugName));
    }

    public Task<IDalamudTextureWrap> CreateFromImageAsync(ReadOnlyMemory<byte> bytes, string? debugName = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IDalamudTextureWrap>(Upload(DecodeImage(bytes.Span), true));

    public Task<IDalamudTextureWrap> CreateFromImageAsync(Stream stream, bool leaveOpen = false, string? debugName = null,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        if (!leaveOpen)
        {
            stream.Dispose();
        }

        return Task.FromResult<IDalamudTextureWrap>(Upload(DecodeImage(buffer.ToArray()), true));
    }

    public IDalamudTextureWrap CreateFromTexFile(TexFile file) => Upload(FromTexFile(file), true);

    public Task<IDalamudTextureWrap> CreateFromTexFileAsync(TexFile file, string? debugName = null,
        CancellationToken cancellationToken = default) => Task.FromResult(CreateFromTexFile(file));

    public IDalamudTextureWrap CreateEmpty(RawImageSpecification specs, bool cpuRead, bool cpuWrite, string? debugName = null) =>
        Upload(CpuTexture.Solid(specs.Width, specs.Height, 0, 0, 0, 0), true);

    public IDrawListTextureWrap CreateDrawListTexture(string? debugName = null) => throw new NotSupportedException();

    public Task<IDalamudTextureWrap> CreateFromExistingTextureAsync(IDalamudTextureWrap wrap, TextureModificationArgs args = default,
        bool leaveWrapOpen = false, string? debugName = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IDalamudTextureWrap> CreateFromImGuiViewportAsync(ImGuiViewportTextureArgs args, string? debugName = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IDalamudTextureWrap> CreateFromClipboardAsync(string? debugName = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IDalamudTextureWrap CreateTextureFromSeString(ReadOnlySpan<byte> text, scoped in SeStringDrawParams drawParams = default,
        string? debugName = null) => throw new NotSupportedException();

    public IEnumerable<IBitmapCodecInfo> GetSupportedImageDecoderInfos() => Array.Empty<IBitmapCodecInfo>();

    public bool HasClipboardImage() => false;

    public bool IsDxgiFormatSupported(int dxgiFormat) => dxgiFormat is FormatRgba32 or FormatBgra32;

    public bool IsDxgiFormatSupportedForCreateFromExistingTextureAsync(int dxgiFormat) => false;

    public nint ConvertToKernelTexture(IDalamudTextureWrap wrap, bool leaveWrapOpen = false) => throw new NotSupportedException();

    private ISharedImmediateTexture Shared(string key, Func<FakeTextureWrap> loader)
    {
        lock (shared)
        {
            if (!shared.TryGetValue(key, out var texture))
            {
                texture = new FakeSharedTexture(loader, empty);
                shared[key] = texture;
            }

            return texture;
        }
    }

    private FakeTextureWrap Upload(CpuTexture texture, bool owned) => FakeTextureWrap.Upload(store, texture, owned);

    private static CpuTexture LoadImage(string path)
    {
        using var image = Image.Load<Rgba32>(path);
        return FromImage(image);
    }

    private static CpuTexture DecodeImage(ReadOnlySpan<byte> bytes)
    {
        using var image = Image.Load<Rgba32>(bytes);
        return FromImage(image);
    }

    private static CpuTexture FromImage(Image<Rgba32> image)
    {
        var pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);
        return new CpuTexture(image.Width, image.Height, pixels);
    }

    private static CpuTexture FromRaw(RawImageSpecification specs, ReadOnlySpan<byte> bytes)
    {
        if (specs.DxgiFormat != FormatRgba32 && specs.DxgiFormat != FormatBgra32)
        {
            throw new NotSupportedException($"DXGI format {specs.DxgiFormat} is not supported by the harness.");
        }

        var width = specs.Width;
        var height = specs.Height;
        var pitch = specs.Pitch > 0 ? specs.Pitch : width * 4;
        var pixels = new byte[width * height * 4];
        var swap = specs.DxgiFormat == FormatBgra32;
        for (var row = 0; row < height; row++)
        {
            var source = bytes.Slice(row * pitch, width * 4);
            var target = pixels.AsSpan(row * width * 4, width * 4);
            source.CopyTo(target);
            if (!swap)
            {
                continue;
            }

            for (var offset = 0; offset < target.Length; offset += 4)
            {
                (target[offset], target[offset + 2]) = (target[offset + 2], target[offset]);
            }
        }

        return new CpuTexture(width, height, pixels);
    }

    private static CpuTexture FromTexFile(TexFile file)
    {
        var bgra = file.ImageData;
        var rgba = new byte[bgra.Length];
        for (var offset = 0; offset + 3 < bgra.Length; offset += 4)
        {
            rgba[offset] = bgra[offset + 2];
            rgba[offset + 1] = bgra[offset + 1];
            rgba[offset + 2] = bgra[offset];
            rgba[offset + 3] = bgra[offset + 3];
        }

        return new CpuTexture(file.Header.Width, file.Header.Height, rgba);
    }

    private CpuTexture LoadGameTexture(string path)
    {
        var file = data.Store?.GetFile<TexFile>(path);
        return file is null ? Placeholder(path.GetHashCode()) : FromTexFile(file);
    }

    private CpuTexture LoadGameIcon(uint iconId, bool hiRes, bool itemHq)
    {
        var lumina = data.Store;
        if (lumina is not null)
        {
            var file = lumina.GetFile<TexFile>(IconPath(iconId, hiRes, itemHq)) ??
                       lumina.GetFile<TexFile>(IconPath(iconId, false, itemHq));
            if (file is not null)
            {
                return FromTexFile(file);
            }
        }

        return Placeholder((int)iconId);
    }

    private static string IconPath(uint iconId, bool hiRes, bool itemHq)
    {
        var folder = iconId / 1000;
        var quality = itemHq ? "hq/" : string.Empty;
        var suffix = hiRes ? "_hr1" : string.Empty;
        return $"ui/icon/{folder:D3}000/{quality}{iconId:D6}{suffix}.tex";
    }

    private static CpuTexture Placeholder(int seed)
    {
        var hue = (uint)((uint)seed * 2654435761UL % 360UL);
        var (red, green, blue) = HueToRgb(hue);
        var size = PlaceholderIconSize;
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var inset = x >= 8 && x < size - 8 && y >= 8 && y < size - 8;
                var offset = (y * size + x) * 4;
                pixels[offset] = (byte)(inset ? red : red / 2);
                pixels[offset + 1] = (byte)(inset ? green : green / 2);
                pixels[offset + 2] = (byte)(inset ? blue : blue / 2);
                pixels[offset + 3] = 255;
            }
        }

        return new CpuTexture(size, size, pixels);
    }

    private static (int Red, int Green, int Blue) HueToRgb(uint hue)
    {
        var sector = hue / 60f;
        var fraction = sector - MathF.Floor(sector);
        var rising = (int)(255 * fraction);
        var falling = 255 - rising;
        return (int)sector switch
        {
            0 => (255, rising, 40),
            1 => (falling, 255, 40),
            2 => (40, 255, rising),
            3 => (40, falling, 255),
            4 => (rising, 40, 255),
            _ => (255, 40, falling),
        };
    }
}
