using Aetherphone.Core.Telephony.Audio;

namespace Aetherphone.Core.Telephony;

internal sealed class CallSession : IDisposable
{
    private readonly Guid callId;
    private readonly RealtimeConnection connection;
    private readonly AudioCapture capture;
    private readonly VoiceMixer mixer;

    private byte localSlot;
    private ushort sequence;

    public CallSession(Guid callId, RealtimeConnection connection, int inputDevice, int outputDevice, float outputVolume)
    {
        this.callId = callId;
        this.connection = connection;
        capture = new AudioCapture();
        mixer = new VoiceMixer();

        capture.FrameEncoded = OnFrameEncoded;
        connection.MediaReceived += OnMediaReceived;

        mixer.Start(outputDevice, outputVolume);
        capture.Start(inputDevice);
    }

    public bool Muted
    {
        get => capture.Muted;
        set => capture.Muted = value;
    }

    public float MicLevel => capture.Level;

    public float Volume
    {
        get => mixer.Volume;
        set => mixer.Volume = value;
    }

    public void SetLocalSlot(int slot) => localSlot = (byte)slot;

    public void AddRemote(int slot) => mixer.AddParticipant(slot);

    public void RemoveRemote(int slot) => mixer.RemoveParticipant(slot);

    public float LevelOf(int slot) => mixer.LevelOf(slot);

    private void OnFrameEncoded(ReadOnlyMemory<byte> opus)
    {
        var frame = MediaFrame.Build(callId, localSlot, sequence++, opus.Span);
        _ = connection.SendMediaAsync(frame);
    }

    private void OnMediaReceived(byte[] frame)
    {
        if (!MediaFrame.TryParse(frame, out var id, out var slot, out _, out var offset, out var length))
        {
            return;
        }

        if (id != callId || slot == localSlot)
        {
            return;
        }

        mixer.Push(slot, new ReadOnlySpan<byte>(frame, offset, length));
    }

    public void Dispose()
    {
        connection.MediaReceived -= OnMediaReceived;
        capture.FrameEncoded = null;
        capture.Dispose();
        mixer.Dispose();
    }
}
