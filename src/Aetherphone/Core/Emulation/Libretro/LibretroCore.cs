using System.Runtime.InteropServices;

namespace Aetherphone.Core.Emulation.Libretro;

internal sealed class LibretroCore : IEmulatorCore
{
    private const uint SaveRamMemoryId = 0;
    private const uint JoypadDevice = 1;
    private const uint AnalogDevice = 5;
    private const uint JoypadMaskId = 256;
    private readonly LibretroApi api;
    private readonly string systemDirectory;
    private readonly string saveDirectory;
    private readonly bool enableAudio;
    private readonly RetroEnvironmentCallback environmentCallback;
    private readonly RetroVideoRefreshCallback videoCallback;
    private readonly RetroAudioSampleCallback audioCallback;
    private readonly RetroAudioSampleBatchCallback audioBatchCallback;
    private readonly RetroInputPollCallback inputPollCallback;
    private readonly RetroInputStateCallback inputStateCallback;
    private readonly Dictionary<string, IntPtr> optionValues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<string>> supportedCoreOptions = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, string> requestedOptions;
    private readonly uint controllerDevice;
    private readonly bool preserveSaveRamOnStateLoad;
    private readonly List<IntPtr> nativeStrings = new();
    private GCHandle romHandle;
    private byte[]? romBytes;
    private byte[] frame = Array.Empty<byte>();
    private LibretroAudioOutput? audio;
    private RetroPixelFormat pixelFormat = RetroPixelFormat.Xrgb1555;
    private string savePath = string.Empty;
    private bool initialized;
    private bool loaded;
    private bool shutdownRequested;
    private int audioPlaybackSpeed = 1;
    private EmulatorInputState input;
    private DiskControl? diskControl;
    private string contentDirectory = string.Empty;

    public LibretroCore(string corePath, string systemDirectory, string saveDirectory, bool enableAudio = true,
        IReadOnlyDictionary<string, string>? coreOptions = null, bool analogController = false,
        bool preserveSaveRamOnStateLoad = false)
    {
        this.systemDirectory = systemDirectory;
        this.saveDirectory = saveDirectory;
        this.enableAudio = enableAudio;
        controllerDevice = analogController ? AnalogDevice : JoypadDevice;
        this.preserveSaveRamOnStateLoad = preserveSaveRamOnStateLoad;
        requestedOptions = coreOptions ?? new Dictionary<string, string>();
        Directory.CreateDirectory(systemDirectory);
        Directory.CreateDirectory(saveDirectory);
        api = new LibretroApi(corePath);
        try
        {
            if (api.ApiVersion() != 1)
            {
                throw new InvalidOperationException("Unsupported libretro API version.");
            }

            environmentCallback = OnEnvironment;
            videoCallback = OnVideo;
            audioCallback = OnAudio;
            audioBatchCallback = OnAudioBatch;
            inputPollCallback = OnInputPoll;
            inputStateCallback = OnInputState;
            api.SetEnvironment(environmentCallback);
            api.SetVideoRefresh(videoCallback);
            api.SetAudioSample(audioCallback);
            api.SetAudioSampleBatch(audioBatchCallback);
            api.SetInputPoll(inputPollCallback);
            api.SetInputState(inputStateCallback);
            api.Init();
            initialized = true;
            api.GetSystemInfo(out var info);
            Name = CombineName(info.LibraryName, info.LibraryVersion);
            NeedFullPath = info.NeedFullPath;
            ValidExtensions = Marshal.PtrToStringUTF8(info.ValidExtensions) ?? string.Empty;
        }
        catch
        {
            if (initialized)
            {
                api.Deinit();
                initialized = false;
            }

            api.Dispose();
            throw;
        }
    }

    public string Name { get; }
    public bool NeedFullPath { get; }
    public string ValidExtensions { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> SupportedCoreOptions => supportedCoreOptions;
    public double FramesPerSecond { get; private set; } = 59.7275;
    public int VideoWidth { get; private set; }
    public int VideoHeight { get; private set; }
    public ReadOnlyMemory<byte> VideoFrame => frame;
    public bool HasNewFrame { get; private set; }
    public int AudioPlaybackSpeed
    {
        set
        {
            audioPlaybackSpeed = Math.Clamp(value, 1, 8);
            if (audio is not null)
            {
                audio.PlaybackSpeed = audioPlaybackSpeed;
            }
        }
    }
    public EmulatorButtons Buttons { set => input = input with { Buttons = value }; }
    public EmulatorInputState Input { set => input = value; }
    public int DiskCount => diskControl?.Count ?? 0;
    public int DiskIndex => diskControl?.Index ?? 0;

    public void LoadGame(string romPath, string savePath)
    {
        if (loaded)
        {
            UnloadGame();
        }

        if (!File.Exists(romPath))
        {
            throw new FileNotFoundException("ROM file not found.", romPath);
        }

        this.savePath = savePath;
        contentDirectory = Path.GetDirectoryName(Path.GetFullPath(romPath)) ?? string.Empty;
        shutdownRequested = false;
        var pathPointer = KeepString(romPath);
        var game = new RetroGameInfo { Path = pathPointer, Meta = IntPtr.Zero, };
        if (!NeedFullPath)
        {
            romBytes = File.ReadAllBytes(romPath);
            romHandle = GCHandle.Alloc(romBytes, GCHandleType.Pinned);
            game.Data = romHandle.AddrOfPinnedObject();
            game.Size = (nuint)romBytes.Length;
        }

        if (!api.LoadGame(ref game))
        {
            ReleaseRom();
            throw new InvalidOperationException($"{Name} refused to load this ROM.");
        }

        loaded = true;
        api.SetControllerPortDevice(0, controllerDevice);
        api.GetSystemAvInfo(out var avInfo);
        FramesPerSecond = avInfo.Timing.Fps > 1 ? avInfo.Timing.Fps : 59.7275;
        VideoWidth = checked((int)avInfo.Geometry.BaseWidth);
        VideoHeight = checked((int)avInfo.Geometry.BaseHeight);
        if (enableAudio)
        {
            try
            {
                audio = new LibretroAudioOutput(avInfo.Timing.SampleRate)
                {
                    PlaybackSpeed = audioPlaybackSpeed,
                };
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Emulator] audio output unavailable; continuing muted: {exception.Message}");
            }
        }

        LoadPersistentMemory();
    }

    public void SetDiskIndex(int index)
    {
        if (diskControl is null)
        {
            throw new NotSupportedException("This core/content does not expose disc switching.");
        }

        diskControl.Change(index);
    }

    public void RunFrame()
    {
        if (!loaded || shutdownRequested)
        {
            return;
        }

        HasNewFrame = false;
        api.Run();
    }

    public void SavePersistentMemory()
    {
        if (!loaded || string.IsNullOrEmpty(savePath))
        {
            return;
        }

        var memory = api.GetMemoryData(SaveRamMemoryId);
        var size = checked((int)api.GetMemorySize(SaveRamMemoryId));
        if (memory == IntPtr.Zero || size <= 0)
        {
            return;
        }

        var data = new byte[size];
        Marshal.Copy(memory, data, 0, size);
        var directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(savePath, data);
    }

    public byte[] SaveState()
    {
        EnsureGameLoaded();
        var size = checked((int)api.SerializeSize());
        if (size <= 0)
        {
            throw new NotSupportedException("This libretro core does not support save states.");
        }

        var state = new byte[size];
        var handle = GCHandle.Alloc(state, GCHandleType.Pinned);
        try
        {
            if (!api.Serialize(handle.AddrOfPinnedObject(), (nuint)size))
            {
                throw new InvalidOperationException("The libretro core could not create a save state.");
            }
        }
        finally
        {
            handle.Free();
        }

        return state;
    }

    public void LoadState(ReadOnlySpan<byte> state)
    {
        EnsureGameLoaded();
        if (state.IsEmpty)
        {
            throw new InvalidDataException("The save state is empty.");
        }

        var expected = checked((int)api.SerializeSize());
        if (expected <= 0)
        {
            throw new NotSupportedException("This libretro core does not support save states.");
        }

        var persistentMemory = preserveSaveRamOnStateLoad ? ReadPersistentMemory() : null;
        var data = state.ToArray();
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var loadedState = api.Unserialize(handle.AddrOfPinnedObject(), (nuint)data.Length);
            if (persistentMemory is not null)
            {
                WritePersistentMemory(persistentMemory);
            }

            if (!loadedState)
            {
                throw new InvalidDataException("The save state is invalid or belongs to another game/core.");
            }
        }
        finally
        {
            handle.Free();
        }

        ResetOutputAfterStateLoad();
    }

    private void ResetOutputAfterStateLoad()
    {
        audio?.Clear();
        HasNewFrame = false;
    }

    private void EnsureGameLoaded()
    {
        if (!loaded)
        {
            throw new InvalidOperationException("No game is loaded.");
        }
    }

    private void LoadPersistentMemory()
    {
        if (!File.Exists(savePath))
        {
            return;
        }

        var memory = api.GetMemoryData(SaveRamMemoryId);
        var size = checked((int)api.GetMemorySize(SaveRamMemoryId));
        if (memory == IntPtr.Zero || size <= 0)
        {
            return;
        }

        var data = File.ReadAllBytes(savePath);
        Marshal.Copy(data, 0, memory, Math.Min(size, data.Length));
    }

    private byte[]? ReadPersistentMemory()
    {
        var memory = api.GetMemoryData(SaveRamMemoryId);
        var size = checked((int)api.GetMemorySize(SaveRamMemoryId));
        if (memory == IntPtr.Zero || size <= 0)
        {
            return null;
        }

        var data = new byte[size];
        Marshal.Copy(memory, data, 0, size);
        return data;
    }

    private void WritePersistentMemory(byte[] data)
    {
        var memory = api.GetMemoryData(SaveRamMemoryId);
        var size = checked((int)api.GetMemorySize(SaveRamMemoryId));
        if (memory != IntPtr.Zero && size > 0)
        {
            Marshal.Copy(data, 0, memory, Math.Min(size, data.Length));
        }
    }

    public void UnloadGame()
    {
        if (!loaded)
        {
            return;
        }

        try
        {
            SavePersistentMemory();
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Emulator] save failed: {exception.Message}");
        }

        audio?.Dispose();
        audio = null;
        api.UnloadGame();
        loaded = false;
        frame = Array.Empty<byte>();
        VideoWidth = 0;
        VideoHeight = 0;
        ReleaseRom();
    }

    private bool OnEnvironment(uint command, IntPtr data)
    {
        switch (command)
        {
            case RetroEnvironmentCommand.GetCanDupe:
            case RetroEnvironmentCommand.GetInputBitmasks:
                WriteBool(data, true);
                return true;
            case RetroEnvironmentCommand.SetSupportNoGame:
            case RetroEnvironmentCommand.SetPerformanceLevel:
            case RetroEnvironmentCommand.SetInputDescriptors:
            case RetroEnvironmentCommand.SetControllerInfo:
            case RetroEnvironmentCommand.SetMemoryMaps:
            case RetroEnvironmentCommand.SetSupportAchievements:
                return true;
            case RetroEnvironmentCommand.SetDiskControlInterface:
            case RetroEnvironmentCommand.SetDiskControlExtInterface:
                return CaptureDiskControl(data);
            case RetroEnvironmentCommand.Shutdown:
                shutdownRequested = true;
                return true;
            case RetroEnvironmentCommand.GetSystemDirectory:
                WriteStringPointer(data, systemDirectory);
                return true;
            case RetroEnvironmentCommand.GetCoreAssetsDirectory:
                WriteStringPointer(data, systemDirectory);
                return true;
            case RetroEnvironmentCommand.GetSaveDirectory:
                WriteStringPointer(data, saveDirectory);
                return true;
            case RetroEnvironmentCommand.SetPixelFormat:
                pixelFormat = (RetroPixelFormat)Marshal.ReadInt32(data);
                return pixelFormat is RetroPixelFormat.Xrgb1555 or RetroPixelFormat.Xrgb8888 or RetroPixelFormat.Rgb565;
            case RetroEnvironmentCommand.SetVariables:
                ReadLegacyVariables(data);
                return true;
            case RetroEnvironmentCommand.GetVariable:
                return GetVariable(data);
            case RetroEnvironmentCommand.GetVariableUpdate:
                WriteBool(data, false);
                return true;
            case RetroEnvironmentCommand.GetCoreOptionsVersion:
                Marshal.WriteInt32(data, 0);
                return true;
            case RetroEnvironmentCommand.GetLanguage:
                Marshal.WriteInt32(data, 0);
                return true;
            case RetroEnvironmentCommand.GetInputMaxUsers:
                Marshal.WriteInt32(data, 1);
                return true;
            case RetroEnvironmentCommand.SetGeometry:
            case RetroEnvironmentCommand.SetSystemAvInfo:
                return true;
            case RetroEnvironmentCommand.SetCoreOptions:
            case RetroEnvironmentCommand.SetCoreOptionsIntl:
            case RetroEnvironmentCommand.SetCoreOptionsDisplay:
            case RetroEnvironmentCommand.SetCoreOptionsV2:
            case RetroEnvironmentCommand.SetCoreOptionsV2Intl:
                return false;
            default:
                return false;
        }
    }

    private void ReadLegacyVariables(IntPtr data)
    {
        if (data == IntPtr.Zero)
        {
            return;
        }

        var stride = Marshal.SizeOf<RetroVariable>();
        for (var index = 0; ; index++)
        {
            var variable = Marshal.PtrToStructure<RetroVariable>(data + index * stride);
            if (variable.Key == IntPtr.Zero)
            {
                break;
            }

            var key = Marshal.PtrToStringUTF8(variable.Key);
            var definition = Marshal.PtrToStringUTF8(variable.Value);
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(definition))
            {
                continue;
            }

            var separator = definition.IndexOf(';');
            var choices = separator >= 0 ? definition[(separator + 1)..].Trim() : definition;
            var pipe = choices.IndexOf('|');
            var values = choices.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            supportedCoreOptions[key] = values;
            var value = values.FirstOrDefault() ?? string.Empty;
            if (requestedOptions.TryGetValue(key, out var requested))
            {
                value = values.FirstOrDefault(candidate => string.Equals(candidate, requested,
                    StringComparison.OrdinalIgnoreCase)) ?? value;
            }
            if (value.Length == 0)
            {
                continue;
            }

            optionValues[key] = KeepString(value);
        }
    }

    private bool GetVariable(IntPtr data)
    {
        if (data == IntPtr.Zero)
        {
            return false;
        }

        var variable = Marshal.PtrToStructure<RetroVariable>(data);
        var key = Marshal.PtrToStringUTF8(variable.Key);
        variable.Value = key is not null && optionValues.TryGetValue(key, out var value) ? value : IntPtr.Zero;
        Marshal.StructureToPtr(variable, data, false);
        return variable.Value != IntPtr.Zero;
    }

    private void OnVideo(IntPtr data, uint width, uint height, nuint pitch)
    {
        if (data == IntPtr.Zero || width == 0 || height == 0)
        {
            return;
        }

        VideoWidth = checked((int)width);
        VideoHeight = checked((int)height);
        var required = checked(VideoWidth * VideoHeight * 4);
        if (frame.Length != required)
        {
            frame = new byte[required];
        }

        switch (pixelFormat)
        {
            case RetroPixelFormat.Xrgb8888:
                CopyXrgb8888(data, checked((int)pitch));
                break;
            case RetroPixelFormat.Rgb565:
                Copy16Bit(data, checked((int)pitch), false);
                break;
            default:
                Copy16Bit(data, checked((int)pitch), true);
                break;
        }

        HasNewFrame = true;
    }

    private void CopyXrgb8888(IntPtr data, int pitch)
    {
        var rowBytes = VideoWidth * 4;
        for (var row = 0; row < VideoHeight; row++)
        {
            var destination = row * rowBytes;
            Marshal.Copy(data + row * pitch, frame, destination, rowBytes);
            for (var pixel = destination + 3; pixel < destination + rowBytes; pixel += 4)
            {
                frame[pixel] = 255;
            }
        }
    }

    private void Copy16Bit(IntPtr data, int pitch, bool xrgb1555)
    {
        var source = new byte[VideoWidth * 2];
        for (var row = 0; row < VideoHeight; row++)
        {
            Marshal.Copy(data + row * pitch, source, 0, source.Length);
            var destination = row * VideoWidth * 4;
            for (var column = 0; column < VideoWidth; column++)
            {
                var value = (ushort)(source[column * 2] | source[column * 2 + 1] << 8);
                int red;
                int green;
                int blue;
                if (xrgb1555)
                {
                    red = value >> 10 & 0x1f;
                    green = value >> 5 & 0x1f;
                    blue = value & 0x1f;
                    green = green * 255 / 31;
                }
                else
                {
                    red = value >> 11 & 0x1f;
                    green = value >> 5 & 0x3f;
                    blue = value & 0x1f;
                    green = green * 255 / 63;
                }

                frame[destination++] = (byte)(blue * 255 / 31);
                frame[destination++] = (byte)green;
                frame[destination++] = (byte)(red * 255 / 31);
                frame[destination++] = 255;
            }
        }
    }

    private void OnAudio(short left, short right)
    {
        audio?.Push(left, right);
    }

    private nuint OnAudioBatch(IntPtr data, nuint frames)
    {
        var count = checked((int)frames);
        audio?.Push(data, count);
        return frames;
    }

    private static void OnInputPoll()
    {
    }

    private short OnInputState(uint port, uint device, uint index, uint id)
    {
        if (port != 0)
        {
            return 0;
        }

        if (device == AnalogDevice && id <= 1)
        {
            return index switch
            {
                0 when id == 0 => input.LeftX,
                0 => input.LeftY,
                1 when id == 0 => input.RightX,
                1 => input.RightY,
                _ => 0,
            };
        }

        if (device != JoypadDevice || index != 0)
        {
            return 0;
        }

        if (id == JoypadMaskId)
        {
            return unchecked((short)input.Buttons);
        }

        return id < 16 && ((ushort)input.Buttons & 1 << checked((int)id)) != 0 ? (short)1 : (short)0;
    }

    private bool CaptureDiskControl(IntPtr data)
    {
        if (data == IntPtr.Zero)
        {
            return false;
        }

        var callbacks = Marshal.PtrToStructure<RetroDiskControlCallback>(data);
        if (callbacks.SetEjectState == IntPtr.Zero || callbacks.GetImageIndex == IntPtr.Zero ||
            callbacks.SetImageIndex == IntPtr.Zero || callbacks.GetNumImages == IntPtr.Zero)
        {
            return false;
        }

        diskControl = new DiskControl(callbacks);
        return true;
    }

    private IntPtr KeepString(string value)
    {
        var pointer = Marshal.StringToCoTaskMemUTF8(value);
        nativeStrings.Add(pointer);
        return pointer;
    }

    private void WriteStringPointer(IntPtr target, string value) => Marshal.WriteIntPtr(target, KeepString(value));

    private static void WriteBool(IntPtr target, bool value)
    {
        if (target != IntPtr.Zero)
        {
            Marshal.WriteByte(target, value ? (byte)1 : (byte)0);
        }
    }

    private static string CombineName(IntPtr name, IntPtr version)
    {
        var left = Marshal.PtrToStringUTF8(name) ?? "libretro";
        var right = Marshal.PtrToStringUTF8(version) ?? string.Empty;
        return right.Length == 0 ? left : $"{left} {right}";
    }

    private void ReleaseRom()
    {
        if (romHandle.IsAllocated)
        {
            romHandle.Free();
        }

        romBytes = null;
    }

    public void Dispose()
    {
        UnloadGame();
        if (initialized)
        {
            api.Deinit();
            initialized = false;
        }

        for (var index = 0; index < nativeStrings.Count; index++)
        {
            Marshal.FreeCoTaskMem(nativeStrings[index]);
        }

        nativeStrings.Clear();
        optionValues.Clear();
        supportedCoreOptions.Clear();
        diskControl = null;
        api.Dispose();
    }

    private sealed class DiskControl
    {
        private readonly RetroDiskSetEjectState setEject;
        private readonly RetroDiskGetImageIndex getIndex;
        private readonly RetroDiskSetImageIndex setIndex;
        private readonly RetroDiskGetNumImages getCount;

        public DiskControl(RetroDiskControlCallback callbacks)
        {
            setEject = Marshal.GetDelegateForFunctionPointer<RetroDiskSetEjectState>(callbacks.SetEjectState);
            getIndex = Marshal.GetDelegateForFunctionPointer<RetroDiskGetImageIndex>(callbacks.GetImageIndex);
            setIndex = Marshal.GetDelegateForFunctionPointer<RetroDiskSetImageIndex>(callbacks.SetImageIndex);
            getCount = Marshal.GetDelegateForFunctionPointer<RetroDiskGetNumImages>(callbacks.GetNumImages);
        }

        public int Count => checked((int)getCount());
        public int Index => checked((int)getIndex());

        public void Change(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (!setEject(true))
            {
                throw new InvalidOperationException("The core could not open the virtual disc tray.");
            }

            try
            {
                if (!setIndex(checked((uint)index)))
                {
                    throw new InvalidOperationException("The core could not select this disc.");
                }
            }
            finally
            {
                _ = setEject(false);
            }
        }
    }
}
