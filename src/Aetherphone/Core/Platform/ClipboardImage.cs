using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Aetherphone.Core.Media;

namespace Aetherphone.Core.Platform;

internal static class ClipboardImage
{
    private const string PngFormatName = "PNG";
    private const uint DibFormat = 8;
    private const uint UnicodeTextFormat = 13;
    private const uint DropFilesFormat = 15;
    private const uint QueryFileCount = uint.MaxValue;
    private const int MaxPathChars = 32768;
    private const int OpenAttempts = 5;
    private const int OpenRetryMilliseconds = 20;
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;
    private const int BitFieldsCompression = 3;
    private const int AlphaBitFieldsCompression = 6;
    private const int BitFieldsMaskBytes = 12;
    private const int AlphaBitFieldsMaskBytes = 16;
    private const int MaxIndexedBitCount = 8;
    private const int PaletteEntryBytes = 4;
    private const int HeaderAllowanceBytes = 4096;
    private const long MaxReadBytes = ImageProcessor.MaxLocalDecodePixels * 4 + HeaderAllowanceBytes;

    private static readonly char[] ProbePath = new char[MaxPathChars];

    public static bool HasImage()
    {
        try
        {
            return IsClipboardFormatAvailable(RegisterClipboardFormatW(PngFormatName))
                || IsClipboardFormatAvailable(DibFormat)
                || (IsClipboardFormatAvailable(DropFilesFormat) && CopiedFilesIncludeImage());
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[Clipboard] could not check for an image");
            return false;
        }
    }

    public static bool HasText()
    {
        try
        {
            return IsClipboardFormatAvailable(UnicodeTextFormat);
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[Clipboard] could not check for text");
            return false;
        }
    }

    public static bool TryRead(out byte[]? encoded, out string[]? files)
    {
        encoded = null;
        files = null;
        if (!Open())
        {
            return false;
        }

        try
        {
            encoded = ReadFormat(RegisterClipboardFormatW(PngFormatName));
            if (encoded is not null)
            {
                return true;
            }

            files = DroppedFiles();
            if (files is not null)
            {
                return true;
            }

            var dib = ReadFormat(DibFormat);
            encoded = dib is null ? null : DibToBmp(dib);
            return encoded is not null;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static bool CopiedFilesIncludeImage()
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return true;
        }

        try
        {
            var handle = GetClipboardData(DropFilesFormat);
            var count = handle == IntPtr.Zero ? 0u : DragQueryFileW(handle, QueryFileCount, null, 0);
            for (var index = 0u; index < count; index++)
            {
                var length = DragQueryFileW(handle, index, ProbePath, (uint)ProbePath.Length);
                if (ImageFiles.IsImage(ProbePath.AsSpan(0, (int)length)))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            CloseClipboard();
        }
    }

    internal static byte[]? DibToBmp(ReadOnlySpan<byte> dib)
    {
        if (dib.Length < InfoHeaderSize)
        {
            return null;
        }

        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(dib);
        if (headerSize < InfoHeaderSize || headerSize > dib.Length)
        {
            return null;
        }

        var bitCount = BinaryPrimitives.ReadUInt16LittleEndian(dib[14..]);
        var compression = BinaryPrimitives.ReadInt32LittleEndian(dib[16..]);
        var colorsUsed = BinaryPrimitives.ReadInt32LittleEndian(dib[32..]);
        var maskBytes = headerSize != InfoHeaderSize ? 0
            : compression == BitFieldsCompression ? BitFieldsMaskBytes
            : compression == AlphaBitFieldsCompression ? AlphaBitFieldsMaskBytes
            : 0;
        var paletteEntries = colorsUsed > 0 ? colorsUsed : bitCount <= MaxIndexedBitCount ? 1 << bitCount : 0;
        var pixelOffset = (long)FileHeaderSize + headerSize + maskBytes + (long)paletteEntries * PaletteEntryBytes;
        var total = FileHeaderSize + dib.Length;
        if (colorsUsed < 0 || pixelOffset > total)
        {
            return null;
        }

        var bmp = new byte[total];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(2), total);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(10), (int)pixelOffset);
        dib.CopyTo(bmp.AsSpan(FileHeaderSize));
        return bmp;
    }

    private static bool Open()
    {
        for (var attempt = 0; attempt < OpenAttempts; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                return true;
            }

            Thread.Sleep(OpenRetryMilliseconds);
        }

        AepLog.Warning("[Clipboard] another app is holding the clipboard");
        return false;
    }

    private static byte[]? ReadFormat(uint format)
    {
        var handle = GetClipboardData(format);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var size = (long)GlobalSize(handle);
        if (size <= 0 || size > MaxReadBytes)
        {
            return null;
        }

        var source = GlobalLock(handle);
        if (source == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var bytes = new byte[size];
            Marshal.Copy(source, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private static string[]? DroppedFiles()
    {
        var handle = GetClipboardData(DropFilesFormat);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var count = DragQueryFileW(handle, QueryFileCount, null, 0);
        if (count == 0)
        {
            return null;
        }

        var buffer = new char[MaxPathChars];
        var files = new List<string>((int)count);
        for (var index = 0u; index < count; index++)
        {
            var length = DragQueryFileW(handle, index, buffer, (uint)buffer.Length);
            if (length > 0)
            {
                files.Add(new string(buffer, 0, (int)length));
            }
        }

        return files.Count > 0 ? files.ToArray() : null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormatW(string name);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll")]
    private static extern UIntPtr GlobalSize(IntPtr memory);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFileW(IntPtr drop, uint file, char[]? buffer, uint length);
}
