using System.Runtime.InteropServices;
using Aetherphone.Core;
using Vortice.Direct3D11;
using Vortice.DXGI;
using KernelDevice = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device;

namespace Aetherphone.Apps.Camera;

internal sealed class PhotoCaptureService
{
    public unsafe bool TryCapture(Rect region, out byte[] pixels, out int width, out int height)
    {
        pixels = Array.Empty<byte>();
        width = 0;
        height = 0;

        var device = KernelDevice.Instance();
        if (device == null || device->SwapChain == null)
        {
            return false;
        }

        var swapChainPtr = (nint)device->SwapChain->DXGISwapChain;
        if (swapChainPtr == 0)
        {
            return false;
        }

        try
        {
            return Capture(swapChainPtr, region, out pixels, out width, out height);
        }
        catch (Exception exception)
        {
            Plugin.Log.Error(exception, "[Camera] capture failed");
            return false;
        }
    }

    private static bool Capture(nint swapChainPtr, Rect region, out byte[] pixels, out int width, out int height)
    {
        pixels = Array.Empty<byte>();
        width = 0;
        height = 0;

        using var swapChain = new IDXGISwapChain(swapChainPtr);
        swapChain.AddRef();

        using var backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
        var sourceDesc = backBuffer.Description;

        if (!IsSupported(sourceDesc.Format))
        {
            AepLog.Warning($"[Camera] unsupported back buffer format {sourceDesc.Format}");
            return false;
        }

        var left = Math.Clamp((int)MathF.Round(region.Min.X), 0, (int)sourceDesc.Width);
        var top = Math.Clamp((int)MathF.Round(region.Min.Y), 0, (int)sourceDesc.Height);
        var right = Math.Clamp((int)MathF.Round(region.Max.X), 0, (int)sourceDesc.Width);
        var bottom = Math.Clamp((int)MathF.Round(region.Max.Y), 0, (int)sourceDesc.Height);

        var regionWidth = right - left;
        var regionHeight = bottom - top;
        if (regionWidth <= 0 || regionHeight <= 0)
        {
            return false;
        }

        using var d3dDevice = backBuffer.Device;
        using var context = d3dDevice.ImmediateContext;

        var stagingDesc = new Texture2DDescription
        {
            Width = (uint)regionWidth,
            Height = (uint)regionHeight,
            MipLevels = 1,
            ArraySize = 1,
            Format = sourceDesc.Format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
        };

        using var staging = d3dDevice.CreateTexture2D(stagingDesc);

        var sourceBox = new Box(left, top, 0, right, bottom, 1);
        context.CopySubresourceRegion(staging, 0, 0, 0, 0, backBuffer, 0, sourceBox);

        var mapped = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            pixels = ReadPixels(mapped, regionWidth, regionHeight, IsBgra(sourceDesc.Format));
        }
        finally
        {
            context.Unmap(staging, 0);
        }

        width = regionWidth;
        height = regionHeight;
        return true;
    }

    private static byte[] ReadPixels(MappedSubresource mapped, int width, int height, bool swapRedBlue)
    {
        var result = new byte[width * height * 4];
        var rowBuffer = new byte[width * 4];

        for (var row = 0; row < height; row++)
        {
            Marshal.Copy(IntPtr.Add(mapped.DataPointer, row * mapped.RowPitch), rowBuffer, 0, rowBuffer.Length);
            var destinationOffset = row * width * 4;

            if (!swapRedBlue)
            {
                Array.Copy(rowBuffer, 0, result, destinationOffset, rowBuffer.Length);
                continue;
            }

            for (var column = 0; column < width; column++)
            {
                var index = column * 4;
                result[destinationOffset + index + 0] = rowBuffer[index + 2];
                result[destinationOffset + index + 1] = rowBuffer[index + 1];
                result[destinationOffset + index + 2] = rowBuffer[index + 0];
                result[destinationOffset + index + 3] = rowBuffer[index + 3];
            }
        }

        return result;
    }

    private static bool IsSupported(Format format) => IsBgra(format)
        || format == Format.R8G8B8A8_UNorm
        || format == Format.R8G8B8A8_UNorm_SRgb;

    private static bool IsBgra(Format format) => format == Format.B8G8R8A8_UNorm
        || format == Format.B8G8R8A8_UNorm_SRgb;
}
