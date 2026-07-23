namespace Aetherphone.Core.Emulation;

internal interface IEmulatorCore : IDisposable
{
    string Name { get; }
    double FramesPerSecond { get; }
    int VideoWidth { get; }
    int VideoHeight { get; }
    ReadOnlyMemory<byte> VideoFrame { get; }
    bool HasNewFrame { get; }
    int AudioPlaybackSpeed { set; }
    EmulatorButtons Buttons { set; }
    EmulatorInputState Input { set; }
    int DiskCount { get; }
    int DiskIndex { get; }
    void SetDiskIndex(int index);
    void LoadGame(string romPath, string savePath);
    void RunFrame();
    byte[] SaveState();
    void LoadState(ReadOnlySpan<byte> state);
    void SavePersistentMemory();
    void UnloadGame();
}
