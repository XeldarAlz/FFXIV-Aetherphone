using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Vortice.Direct3D11;

namespace Aetherphone.Core.Emulation;

internal sealed class EmulatorVideoTexture : IDisposable
{
    private readonly ITextureProvider textures;
    private IDalamudTextureWrap? wrap;
    private ID3D11ShaderResourceView? view;
    private ID3D11Resource? resource;
    private ID3D11Device? device;
    private ID3D11DeviceContext? context;
    private byte[] nearestBuffer = Array.Empty<byte>();
    private byte[] balancedBuffer = Array.Empty<byte>();
    private int width;
    private int height;

    public EmulatorVideoTexture(ITextureProvider textures)
    {
        this.textures = textures;
    }

    public IDalamudTextureWrap? Wrap => wrap;

    public void Upload(ReadOnlyMemory<byte> pixels, int frameWidth, int frameHeight, EmulatorVideoFilter filter,
        int displayWidth, int displayHeight)
    {
        if (frameWidth <= 0 || frameHeight <= 0 || pixels.Length < frameWidth * frameHeight * 4)
        {
            return;
        }

        var nearest = filter is EmulatorVideoFilter.Pixel or EmulatorVideoFilter.Sharp;
        var balanced = filter == EmulatorVideoFilter.Balanced;
        var outputWidth = nearest || balanced ? Math.Max(1, displayWidth) : frameWidth;
        var outputHeight = nearest || balanced ? Math.Max(1, displayHeight) : frameHeight;
        if (wrap is null || outputWidth != width || outputHeight != height)
        {
            Create(outputWidth, outputHeight);
        }

        if (resource is null || context is null)
        {
            return;
        }

        byte[] source;
        var sourceOffset = 0;
        if (MemoryMarshal.TryGetArray(pixels, out var segment) && segment.Array is not null)
        {
            source = segment.Array;
            sourceOffset = segment.Offset;
        }
        else
        {
            source = pixels.ToArray();
        }

        if (nearest && (outputWidth != frameWidth || outputHeight != frameHeight))
        {
            source = ScaleNearest(source, sourceOffset, frameWidth, frameHeight, outputWidth, outputHeight);
            sourceOffset = 0;
        }
        else if (balanced && (outputWidth != frameWidth || outputHeight != frameHeight))
        {
            source = ScaleBalanced(source, sourceOffset, frameWidth, frameHeight, outputWidth, outputHeight);
            sourceOffset = 0;
        }

        var mapped = context.Map(resource, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            var rowBytes = outputWidth * 4;
            for (var row = 0; row < outputHeight; row++)
            {
                var destination = new IntPtr(mapped.DataPointer + (long)row * mapped.RowPitch);
                Marshal.Copy(source, sourceOffset + row * rowBytes, destination, rowBytes);
            }
        }
        finally
        {
            context.Unmap(resource, 0);
        }
    }

    private byte[] ScaleNearest(byte[] source, int sourceOffset, int sourceWidth, int sourceHeight,
        int outputWidth, int outputHeight)
    {
        var required = outputWidth * outputHeight * 4;
        if (nearestBuffer.Length != required)
        {
            nearestBuffer = new byte[required];
        }

        NearestNeighborScaler.ScaleBgra(
            source.AsSpan(sourceOffset, sourceWidth * sourceHeight * 4), sourceWidth, sourceHeight,
            nearestBuffer, outputWidth, outputHeight);

        return nearestBuffer;
    }

    private byte[] ScaleBalanced(byte[] source, int sourceOffset, int sourceWidth, int sourceHeight,
        int outputWidth, int outputHeight)
    {
        var required = outputWidth * outputHeight * 4;
        if (balancedBuffer.Length != required)
        {
            balancedBuffer = new byte[required];
        }

        NearestNeighborScaler.ScaleSharpBilinearBgra(
            source.AsSpan(sourceOffset, sourceWidth * sourceHeight * 4), sourceWidth, sourceHeight,
            balancedBuffer, outputWidth, outputHeight);

        return balancedBuffer;
    }

    private void Create(int frameWidth, int frameHeight)
    {
        Release();
        width = frameWidth;
        height = frameHeight;
        wrap = textures.CreateEmpty(RawImageSpecification.Bgra32(width, height), false, true,
            "Aetherphone.Emulator.Frame");
        var textureId = wrap.Handle;
        var nativeHandle = Unsafe.As<ImTextureID, nint>(ref textureId);
        Marshal.AddRef(nativeHandle);
        view = new ID3D11ShaderResourceView(nativeHandle);
        resource = view.Resource;
        device = resource.Device;
        context = device.ImmediateContext;
    }

    private void Release()
    {
        context?.Dispose();
        context = null;
        device?.Dispose();
        device = null;
        resource?.Dispose();
        resource = null;
        view?.Dispose();
        view = null;
        wrap?.Dispose();
        wrap = null;
        nearestBuffer = Array.Empty<byte>();
        balancedBuffer = Array.Empty<byte>();
        width = 0;
        height = 0;
    }

    public void Dispose() => Release();
}
