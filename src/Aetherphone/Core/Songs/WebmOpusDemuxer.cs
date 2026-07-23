using NEbml.Core;

namespace Aetherphone.Core.Songs;

/// <summary>
/// Minimal WebM (Matroska subset) demuxer: walks the EBML tree far enough to pull raw Opus
/// packets out of Cluster/SimpleBlock elements for a single-audio-track stream. Not a general
/// Matroska parser - deliberately only reads what a YouTube audio-only webm/opus DASH stream
/// contains (one audio track, no video, no chapters/attachments).
/// </summary>
internal sealed class WebmOpusDemuxer
{
    // Matroska element IDs (top bit(s) kept, as read by NEbml's VInt).
    private const ulong EbmlHeaderId = 0x1A45DFA3;
    private const ulong SegmentId = 0x18538067;
    private const ulong InfoId = 0x1549A966;
    private const ulong TimestampScaleId = 0x2AD7B1;
    private const ulong DurationId = 0x4489;
    private const ulong TracksId = 0x1654AE6B;
    private const ulong TrackEntryId = 0xAE;
    private const ulong TrackNumberId = 0xD7;
    private const ulong CodecIdId = 0x86;
    private const ulong ClusterId = 0x1F43B675;
    private const ulong SimpleBlockId = 0xA3;
    private const ulong BlockGroupId = 0xA0;
    private const ulong BlockId = 0xA1;

    private readonly EbmlReader reader;
    private ulong timestampScale = 1_000_000; // Matroska default: 1ms per tick, in nanoseconds.
    private ulong? opusTrackNumber;
    private bool hasPendingFirstCluster;
    private bool insideCluster;

    public double? DurationSeconds { get; private set; }

    public WebmOpusDemuxer(Stream source)
    {
        reader = new EbmlReader(source);
    }

    /// <summary>
    /// Advances through the container until either a Tracks element has identified the Opus
    /// audio track, or a Cluster is reached (whichever comes first is fine - Tracks always
    /// precedes Cluster in a valid WebM stream).
    /// </summary>
    public void ReadHeader()
    {
        if (!reader.ReadNext())
        {
            throw new InvalidDataException("Empty stream.");
        }

        if (reader.ElementId.EncodedValue == EbmlHeaderId)
        {
            // Every real WebM/Matroska file leads with an EBML header element (version/doctype
            // info we don't need) before Segment. ReadNext() auto-skips its unread content when
            // advancing past it.
            if (!reader.ReadNext())
            {
                throw new InvalidDataException("Stream ended after EBML header.");
            }
        }

        if (reader.ElementId.EncodedValue != SegmentId)
        {
            throw new InvalidDataException(
                $"Expected top-level Segment element, got 0x{reader.ElementId.EncodedValue:X}.");
        }

        reader.EnterContainer();
        while (reader.ReadNext())
        {
            var id = reader.ElementId.EncodedValue;
            if (id == InfoId)
            {
                ReadInfo();
            }
            else if (id == TracksId)
            {
                ReadTracks();
            }
            else if (id == ClusterId)
            {
                // Header is done; the reader is left sitting on this Cluster's element header,
                // un-entered, ready for ReadNextPacket() to pick up.
                hasPendingFirstCluster = true;
                return;
            }
        }
    }

    private void ReadInfo()
    {
        reader.EnterContainer();
        while (reader.ReadNext())
        {
            var id = reader.ElementId.EncodedValue;
            if (id == TimestampScaleId)
            {
                timestampScale = reader.ReadUInt();
            }
            else if (id == DurationId)
            {
                var ticks = reader.ReadFloat();
                DurationSeconds = ticks * timestampScale / 1_000_000_000.0;
            }
        }

        reader.LeaveContainer();
    }

    private void ReadTracks()
    {
        reader.EnterContainer();
        while (reader.ReadNext())
        {
            if (reader.ElementId.EncodedValue != TrackEntryId)
            {
                continue;
            }

            ulong? trackNumber = null;
            var isOpus = false;
            reader.EnterContainer();
            while (reader.ReadNext())
            {
                var id = reader.ElementId.EncodedValue;
                if (id == TrackNumberId)
                {
                    trackNumber = reader.ReadUInt();
                }
                else if (id == CodecIdId)
                {
                    isOpus = string.Equals(reader.ReadAscii(), "A_OPUS", StringComparison.Ordinal);
                }
            }

            reader.LeaveContainer();

            if (isOpus && trackNumber is not null)
            {
                opusTrackNumber = trackNumber;
            }
        }

        reader.LeaveContainer();
    }

    /// <summary>
    /// Reads the next raw Opus packet belonging to the audio track, skipping SimpleBlocks/Blocks
    /// for any other track (there shouldn't be any in an audio-only stream, but don't assume).
    /// Returns null once the container is exhausted.
    /// </summary>
    public byte[]? ReadNextPacket()
    {
        if (opusTrackNumber is null)
        {
            throw new InvalidOperationException("ReadHeader() must locate the Opus track before reading packets.");
        }

        while (true)
        {
            if (insideCluster)
            {
                // Resume scanning the SAME already-entered Cluster from where the last call left
                // off - ReadPacketsWithinCluster() calls reader.ReadNext() on the current container
                // each time, so this must not re-enter or re-find the Cluster element itself.
                var packet = ReadPacketsWithinCluster();
                if (packet is not null)
                {
                    return packet;
                }

                // Cluster's children exhausted; ReadPacketsWithinCluster() already left the
                // container. Fall through to look for the next top-level Cluster.
                insideCluster = false;
                continue;
            }

            if (hasPendingFirstCluster)
            {
                // Already sitting on the first Cluster's element header from ReadHeader().
                hasPendingFirstCluster = false;
            }
            else if (!reader.ReadNext())
            {
                return null;
            }

            var id = reader.ElementId.EncodedValue;
            if (id == ClusterId)
            {
                reader.EnterContainer();
                insideCluster = true;
            }
        }
    }

    private byte[]? ReadPacketsWithinCluster()
    {
        while (reader.ReadNext())
        {
            var id = reader.ElementId.EncodedValue;
            if (id == SimpleBlockId)
            {
                var packet = ReadBlockPayload();
                if (packet is not null)
                {
                    return packet;
                }
            }
            else if (id == BlockGroupId)
            {
                reader.EnterContainer();
                byte[]? packet = null;
                while (reader.ReadNext())
                {
                    if (reader.ElementId.EncodedValue == BlockId)
                    {
                        packet = ReadBlockPayload();
                    }
                }

                reader.LeaveContainer();
                if (packet is not null)
                {
                    return packet;
                }
            }
        }

        reader.LeaveContainer();
        return null;
    }

    /// <summary>
    /// Parses a SimpleBlock/Block's binary payload: track number (Matroska vint), signed
    /// 16-bit timecode, flags byte, then frame data. Only "no lacing" is implemented - YouTube's
    /// audio-only Opus DASH streams do not lace frames. Laced blocks throw rather than silently
    /// producing corrupt audio, so this gap is visible if it turns out to matter in practice.
    /// </summary>
    private byte[]? ReadBlockPayload()
    {
        var size = (int)reader.ElementSize;
        var buffer = new byte[size];
        var read = 0;
        while (read < size)
        {
            var bytesRead = reader.ReadBinary(buffer, read, size - read);
            if (bytesRead < 0)
            {
                break;
            }

            read += bytesRead;
        }

        var offset = 0;
        var trackNumber = ReadMatroskaVint(buffer, ref offset);
        offset += 2; // signed 16-bit timecode, relative to the cluster - not needed for playback order.
        var flags = buffer[offset];
        offset += 1;

        var lacing = (flags >> 1) & 0x3;
        if (lacing != 0)
        {
            throw new NotSupportedException(
                $"Laced SimpleBlock encountered (lacing mode {lacing}); only unlaced frames are supported.");
        }

        if (trackNumber != opusTrackNumber)
        {
            return null;
        }

        var frame = new byte[size - offset];
        Array.Copy(buffer, offset, frame, 0, frame.Length);
        return frame;
    }

    /// <summary>
    /// Matroska's own vint encoding for track numbers/lace sizes: same leading-1-bit-marks-length
    /// scheme as EBML element IDs/sizes, but the marker bits are stripped from the value (unlike
    /// element IDs, which keep them).
    /// </summary>
    private static ulong ReadMatroskaVint(byte[] buffer, ref int offset)
    {
        var first = buffer[offset];
        var length = 1;
        var mask = (byte)0x80;
        while (mask != 0 && (first & mask) == 0)
        {
            mask >>= 1;
            length++;
        }

        if (mask == 0)
        {
            throw new InvalidDataException("Invalid Matroska vint in block header.");
        }

        ulong value = (ulong)(first & (mask - 1));
        for (var index = 1; index < length; index++)
        {
            value = (value << 8) | buffer[offset + index];
        }

        offset += length;
        return value;
    }
}
