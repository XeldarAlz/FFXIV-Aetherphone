using System.Runtime.InteropServices;

namespace Aetherphone.Core.SystemMedia;

internal static unsafe class WinRtStreams
{
    private const string StreamReferenceClass = "Windows.Storage.Streams.RandomAccessStreamReference";
    private const int OpenReadAsyncSlot = 6;
    private const int RandomAccessStreamSizeSlot = 6;
    private const int CreateFromStreamSlot = 8;
    private const int StreamReadSlot = 3;
    private const int DefaultStreamOptions = 0;

    private static readonly Guid RandomAccessStreamId = new("905a0fe1-bc53-11df-8c49-001e4fc686da");
    private static readonly Guid StreamReferenceStaticsId = new("857309dc-3fbf-4e7d-986f-ef3b1a07a964");
    private static readonly Guid StreamId = new("0000000c-0000-0000-c000-000000000046");

    public static byte[]? ReadReference(nint streamReference, int timeoutMilliseconds, int maximumBytes)
    {
        nint operation = 0;
        nint contentStream = 0;
        nint randomAccessStream = 0;
        nint classicStream = 0;
        try
        {
            if (!ComCall.Succeeded(ComCall.GetPointer(streamReference, OpenReadAsyncSlot, out operation))
                || !WinRt.WaitForPointer(operation, timeoutMilliseconds, out contentStream)
                || contentStream == 0)
            {
                return null;
            }

            if (!ComCall.Succeeded(ComCall.QueryInterface(contentStream, RandomAccessStreamId, out randomAccessStream))
                || !ComCall.Succeeded(ComCall.GetUInt64(randomAccessStream, RandomAccessStreamSizeSlot, out var size))
                || size == 0
                || size > (ulong)maximumBytes)
            {
                return null;
            }

            var streamId = StreamId;
            if (!ComCall.Succeeded(CreateStreamOverRandomAccessStream(randomAccessStream, &streamId, &classicStream)))
            {
                return null;
            }

            return ReadExactly(classicStream, (int)size);
        }
        finally
        {
            ComCall.Release(classicStream);
            ComCall.Release(randomAccessStream);
            ComCall.Release(contentStream);
            ComCall.Release(operation);
        }
    }

    public static nint CreateReference(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return 0;
        }

        nint memoryStream = 0;
        nint randomAccessStream = 0;
        nint statics = 0;
        try
        {
            fixed (byte* source = bytes)
            {
                memoryStream = SHCreateMemStream(source, (uint)bytes.Length);
            }

            if (memoryStream == 0)
            {
                return 0;
            }

            var randomAccessStreamId = RandomAccessStreamId;
            if (!ComCall.Succeeded(CreateRandomAccessStreamOverStream(memoryStream, DefaultStreamOptions,
                    &randomAccessStreamId, &randomAccessStream)))
            {
                return 0;
            }

            if (!ComCall.Succeeded(WinRt.GetActivationFactory(StreamReferenceClass, StreamReferenceStaticsId,
                    out statics)))
            {
                return 0;
            }

            return ComCall.Succeeded(ComCall.GetPointerWithPointer(statics, CreateFromStreamSlot, randomAccessStream,
                out var reference))
                ? reference
                : 0;
        }
        finally
        {
            ComCall.Release(statics);
            ComCall.Release(randomAccessStream);
            ComCall.Release(memoryStream);
        }
    }

    private static byte[]? ReadExactly(nint stream, int size)
    {
        var bytes = new byte[size];
        var offset = 0;
        fixed (byte* destination = bytes)
        {
            while (offset < size)
            {
                var status = ComCall.ReadBuffer(stream, StreamReadSlot, destination + offset, (uint)(size - offset),
                    out var read);
                if (!ComCall.Succeeded(status) || read == 0)
                {
                    break;
                }

                offset += (int)read;
            }
        }

        return offset == size ? bytes : null;
    }

    [DllImport("shcore.dll")]
    private static extern int CreateStreamOverRandomAccessStream(nint randomAccessStream, Guid* interfaceId,
        nint* stream);

    [DllImport("shcore.dll")]
    private static extern int CreateRandomAccessStreamOverStream(nint stream, int options, Guid* interfaceId,
        nint* randomAccessStream);

    [DllImport("shlwapi.dll")]
    private static extern nint SHCreateMemStream(byte* source, uint length);
}
