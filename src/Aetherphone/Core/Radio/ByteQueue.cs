namespace Aetherphone.Core.Radio;

internal sealed class ByteQueue
{
    private byte[] buffer;
    private int start;
    private int end;

    public ByteQueue(int capacity)
    {
        buffer = new byte[capacity];
    }

    public int Count => end - start;

    public void Append(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        if (end + data.Length > buffer.Length)
        {
            Compact(data.Length);
        }

        data.CopyTo(buffer.AsSpan(end));
        end += data.Length;
    }

    public int Read(byte[] target, int offset, int count)
    {
        var take = Math.Min(count, Count);
        Array.Copy(buffer, start, target, offset, take);
        start += take;
        if (start == end)
        {
            start = 0;
            end = 0;
        }

        return take;
    }

    public void Clear()
    {
        start = 0;
        end = 0;
    }

    private void Compact(int incoming)
    {
        var count = Count;
        var needed = count + incoming;
        if (needed > buffer.Length)
        {
            var grown = new byte[Math.Max(needed, buffer.Length * 2)];
            Array.Copy(buffer, start, grown, 0, count);
            buffer = grown;
        }
        else
        {
            Array.Copy(buffer, start, buffer, 0, count);
        }

        start = 0;
        end = count;
    }
}
