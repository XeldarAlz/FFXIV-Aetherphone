using Dalamud.Plugin;
using Lumina.Data.Files;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace Aetherphone.Core.Video;

internal sealed class TextureTranslate
{
    private readonly ConvertTextureData _convert;

    private static readonly int[] _sizes = [256, 512, 1024];

    public TextureTranslate(IDalamudPluginInterface pi)
    {
        _convert = new ConvertTextureData(pi);
    }

    public async Task EncodeToTexAsync(string? json, string outputPath)
    {
        if (json is null)
        {
            int size = _sizes[0];
            byte[] white = new byte[size * size * 4];
            Array.Fill(white, (byte)255);

            string temp = Path.Combine(Path.GetTempPath(), $"axc_{Guid.NewGuid():N}.tex");
            try
            {
                await _convert.Invoke(white, size, temp, TextureType.Bc7Tex, mipMaps: true).ConfigureAwait(false);
                File.Copy(temp, outputPath, overwrite: true);
            }
            finally { if (File.Exists(temp)) { try { File.Delete(temp); } catch { } } }
            return;
        }

        Exception? lastErr = null;

        foreach (int size in _sizes)
        {
            byte[] rgba;
            try
            {
                rgba = RenderQrToRgba(json, size);
            }
            catch (WriterException)
            {
                throw new InvalidOperationException("URL is too long to fit inside a QR code!");
            }

			string temp = Path.Combine(Path.GetTempPath(), $"axc_{Guid.NewGuid():N}.tex");
            try
            {
                await _convert.Invoke(rgba, size, temp, TextureType.Bc7Tex, mipMaps: true).ConfigureAwait(false);

				byte[] back = DecodeTexToRgba(temp, out int w, out int h);
                if (string.Equals(ScanQr(back, w, h), json, StringComparison.Ordinal))
                {
                    File.Copy(temp, outputPath, overwrite: true);
                    return;
                }
            }
            catch (Exception ex) { lastErr = ex; }
            finally { if (File.Exists(temp)) { try { File.Delete(temp); } catch { } } }
        }

        throw new InvalidOperationException(
            "URL is too long to fit inside a QR code - BC7 compression did not survive" +
            (lastErr != null ? $" Last Error: {lastErr.Message}" : ""));
    }

    public string? DecodeFromTex(string texPath)
    {
		byte[] rgba = DecodeTexToRgba(texPath, out int w, out int h);
        return ScanQr(rgba, w, h);
    }

    private static byte[] RenderQrToRgba(string content, int size)
    {
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.ERROR_CORRECTION] = ErrorCorrectionLevel.H,
            [EncodeHintType.MARGIN]           = 4,
            [EncodeHintType.CHARACTER_SET]    = "UTF-8",
        };

        BitMatrix m = new QRCodeWriter().encode(content, BarcodeFormat.QR_CODE, size, size, hints);

		byte[] rgba = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
            {
                byte v = m[x, y] ? (byte)0 : (byte)255;
                int i = (y * size + x) * 4;
                rgba[i] = v; rgba[i + 1] = v; rgba[i + 2] = v; rgba[i + 3] = 255;
            }
		}

		return rgba;
    }

    private static string? ScanQr(byte[] rgba, int width, int height)
    {
        try
        {
            var src = new RGBLuminanceSource(rgba, width, height, RGBLuminanceSource.BitmapFormat.RGBA32);
            var bin = new BinaryBitmap(new HybridBinarizer(src));
            var hints = new Dictionary<DecodeHintType, object>
            {
                [DecodeHintType.PURE_BARCODE] = true,   // reines QR-Bild, keine Szene
                [DecodeHintType.TRY_HARDER]   = true,
            };
            return new QRCodeReader().decode(bin, hints)?.Text;
        }
        catch { return null; } // ReaderException = nicht lesbar
    }

    private byte[] DecodeTexToRgba(string path, out int width, out int height)
    {
        var tex = Plugin.DataManager.GameData.GetFileFromDisk<TexFile>(path);
        width  = tex.Header.Width;
        height = tex.Header.Height;
        return tex.ImageData;
    }
}