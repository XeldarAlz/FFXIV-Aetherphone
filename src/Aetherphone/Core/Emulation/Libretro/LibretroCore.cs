using System.Runtime.InteropServices;

namespace Aetherphone.Core.Emulation.Libretro;

internal sealed class LibretroCore : IEmulatorCore
{
    private const uint SaveRamMemoryId = 0;
    private const uint JoypadDevice = 1;
    private const uint AnalogDevice = 5;
    private const uint PointerDevice = 6;
    private const uint PointerXId = 0;
    private const uint PointerYId = 1;
    private const uint PointerPressedId = 2;
    private const uint PointerCountId = 3;
    private const uint PlayStationDualShockDevice = (2u << 8) | AnalogDevice;
    private const uint JoypadMaskId = 256;
    private const string PrintfConversions = "diuoxXfFeEgGaAcspn";
    private static readonly IntPtr HardwareFrameBuffer = new(-1);
    private readonly LibretroApi api;
    private readonly bool enableAudio;
    private readonly RetroEnvironmentCallback environmentCallback;
    private readonly RetroVideoRefreshCallback videoCallback;
    private readonly RetroAudioSampleCallback audioCallback;
    private readonly RetroAudioSampleBatchCallback audioBatchCallback;
    private readonly RetroInputPollCallback inputPollCallback;
    private readonly RetroInputStateCallback inputStateCallback;
    private readonly RetroLogCallback logCallback;
    private readonly Dictionary<string, IntPtr> optionValues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<string>> supportedCoreOptions = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, string> requestedOptions;
    private readonly uint controllerDevice;
    private readonly bool preserveSaveRamOnStateLoad;
    private IntPtr systemDirectoryPointer;
    private IntPtr coreAssetsDirectoryPointer;
    private IntPtr saveDirectoryPointer;
    private IntPtr contentPathPointer;
    private GCHandle romHandle;
    private byte[]? romBytes;
    private byte[] frame = Array.Empty<byte>();
    private byte[] source16 = Array.Empty<byte>();
    private LibretroAudioOutput? audio;
    private RetroPixelFormat pixelFormat = RetroPixelFormat.Xrgb1555;
    private string savePath = string.Empty;
    private bool initialized;
    private bool loaded;
    private bool shutdownRequested;
    private int audioPlaybackSpeed = 1;
    private float audioGain = 1f;
    private EmulatorInputState input;
    private DiskControl? diskControl;
    private bool warnedHardwareFrame;
    private bool variableUpdated;

    public LibretroCore(string corePath, string systemDirectory, string saveDirectory, bool enableAudio = true,
        IReadOnlyDictionary<string, string>? coreOptions = null, bool analogController = false,
        bool preserveSaveRamOnStateLoad = false)
    {
        this.enableAudio = enableAudio;
        controllerDevice = analogController ? PlayStationDualShockDevice : JoypadDevice;
        this.preserveSaveRamOnStateLoad = preserveSaveRamOnStateLoad;
        requestedOptions = coreOptions ?? new Dictionary<string, string>();
        Directory.CreateDirectory(systemDirectory);
        Directory.CreateDirectory(saveDirectory);
        api = new LibretroApi(corePath);
        try
        {
            systemDirectoryPointer = Marshal.StringToCoTaskMemUTF8(systemDirectory);
            coreAssetsDirectoryPointer = Marshal.StringToCoTaskMemUTF8(systemDirectory);
            saveDirectoryPointer = Marshal.StringToCoTaskMemUTF8(saveDirectory);
            if (api.ApiVersion() != 1)
            {
                throw new InvalidOperationException("Unsupported libretro API version.");
            }

            logCallback = OnCoreLog;
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
            FreePointer(ref systemDirectoryPointer);
            FreePointer(ref coreAssetsDirectoryPointer);
            FreePointer(ref saveDirectoryPointer);
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
    public float VideoAspectRatio { get; private set; }
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
    public float AudioGain
    {
        set
        {
            audioGain = Math.Clamp(value, 0f, 1f);
            if (audio is not null)
            {
                audio.Volume = audioGain;
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
        shutdownRequested = false;
        warnedHardwareFrame = false;
        FreePointer(ref contentPathPointer);
        contentPathPointer = Marshal.StringToCoTaskMemUTF8(romPath);
        var game = new RetroGameInfo { Path = contentPathPointer, Meta = IntPtr.Zero, };
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
        ApplyGeometry(avInfo.Geometry);
        if (enableAudio)
        {
            try
            {
                audio = new LibretroAudioOutput(avInfo.Timing.SampleRate)
                {
                    PlaybackSpeed = audioPlaybackSpeed,
                    Volume = audioGain,
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
        VideoAspectRatio = 0f;
        ReleaseRom();
    }

    private bool OnEnvironment(uint command, IntPtr data)
    {
        try
        {
            return HandleEnvironment(command, data);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Emulator] environment callback failed: {exception.Message}");
            return false;
        }
    }

    private bool HandleEnvironment(uint command, IntPtr data)
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
            case RetroEnvironmentCommand.GetLogInterface:
                return ProvideLogInterface(data);
            case RetroEnvironmentCommand.SetHwRender:
                AepLog.Error("[Emulator] The libretro core requested hardware rendering, but Aetherphone " +
                             "currently supports software video frames only. For N64, select Angrylion RDP.");
                return false;
            case RetroEnvironmentCommand.GetSystemDirectory:
                WriteStringPointer(data, systemDirectoryPointer);
                return true;
            case RetroEnvironmentCommand.GetCoreAssetsDirectory:
                WriteStringPointer(data, coreAssetsDirectoryPointer);
                return true;
            case RetroEnvironmentCommand.GetSaveDirectory:
                WriteStringPointer(data, saveDirectoryPointer);
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
                WriteBool(data, variableUpdated);
                variableUpdated = false;
                return true;
            case RetroEnvironmentCommand.SetVariable:
                return SetVariable(data);
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
                return CaptureGeometry(data);
            case RetroEnvironmentCommand.SetSystemAvInfo:
                return CaptureSystemAvInfo(data);
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

    private bool CaptureGeometry(IntPtr data)
    {
        if (data == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            ApplyGeometry(Marshal.PtrToStructure<RetroGameGeometry>(data));
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Emulator] rejected geometry update: {exception.Message}");
            return false;
        }
    }

    private bool CaptureSystemAvInfo(IntPtr data)
    {
        if (data == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var info = Marshal.PtrToStructure<RetroSystemAvInfo>(data);
            ApplyGeometry(info.Geometry);
            if (info.Timing.Fps > 1)
            {
                FramesPerSecond = info.Timing.Fps;
            }

            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Emulator] rejected AV-info update: {exception.Message}");
            return false;
        }
    }

    private void ApplyGeometry(RetroGameGeometry geometry)
    {
        var width = checked((int)geometry.BaseWidth);
        var height = checked((int)geometry.BaseHeight);
        if (width > 0)
        {
            VideoWidth = width;
        }

        if (height > 0)
        {
            VideoHeight = height;
        }

        var aspect = geometry.AspectRatio;
        if (aspect <= 0.01f || float.IsNaN(aspect) || float.IsInfinity(aspect))
        {
            aspect = width > 0 && height > 0 ? width / (float)height : 0f;
        }

        if (aspect > 0.01f)
        {
            VideoAspectRatio = aspect;
        }
    }

    private bool ProvideLogInterface(IntPtr data)
    {
        if (data == IntPtr.Zero)
        {
            return false;
        }

        var callback = new RetroLogInterface
        {
            Log = Marshal.GetFunctionPointerForDelegate(logCallback),
        };
        Marshal.StructureToPtr(callback, data, false);
        return true;
    }

    private static void OnCoreLog(uint level, IntPtr format)
    {
        try
        {
            var message = Marshal.PtrToStringUTF8(format)?.TrimEnd('\r', '\n');
            if (string.IsNullOrWhiteSpace(message) || ContainsPrintfConversion(message))
            {
                return;
            }

            var logLevel = (RetroLogLevel)Math.Min(level, (uint)RetroLogLevel.Error);
            var prefix = logLevel switch
            {
                RetroLogLevel.Debug => "DEBUG",
                RetroLogLevel.Info => "INFO",
                RetroLogLevel.Warn => "WARN",
                _ => "ERROR",
            };
            var text = $"[Libretro {prefix}] {message}";

            switch (logLevel)
            {
                case RetroLogLevel.Debug:
                    AepLog.Debug(text);
                    break;
                case RetroLogLevel.Info:
                    AepLog.Info(text);
                    break;
                case RetroLogLevel.Warn:
                    AepLog.Warning(text);
                    break;
                default:
                    AepLog.Error(text);
                    break;
            }
        }
        catch
        {
        }
    }

    private static bool ContainsPrintfConversion(string format)
    {
        for (var index = 0; index < format.Length; index++)
        {
            if (format[index] != '%')
            {
                continue;
            }

            index++;
            if (index >= format.Length)
            {
                return false;
            }

            if (format[index] == '%')
            {
                continue;
            }

            while (index < format.Length && "-+ #0'".Contains(format[index]))
            {
                index++;
            }

            if (index < format.Length && format[index] == '*')
            {
                index++;
            }
            else
            {
                while (index < format.Length && char.IsDigit(format[index]))
                {
                    index++;
                }
            }

            if (index < format.Length && format[index] == '.')
            {
                index++;
                if (index < format.Length && format[index] == '*')
                {
                    index++;
                }
                else
                {
                    while (index < format.Length && char.IsDigit(format[index]))
                    {
                        index++;
                    }
                }
            }

            while (index < format.Length && "hljztLI".Contains(format[index]))
            {
                index++;
            }

            if (index < format.Length && PrintfConversions.Contains(format[index]))
            {
                return true;
            }
        }

        return false;
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

            ReplaceOptionString(key, value);
        }
    }


    private bool SetVariable(IntPtr data)
    {
        if (data == IntPtr.Zero)
        {
            return true;
        }

        var variable = Marshal.PtrToStructure<RetroVariable>(data);
        var key = Marshal.PtrToStringUTF8(variable.Key);
        var value = Marshal.PtrToStringUTF8(variable.Value);
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value) ||
            !supportedCoreOptions.TryGetValue(key, out var supported) ||
            !supported.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        ReplaceOptionString(key, value);
        variableUpdated = true;
        return true;
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
        try
        {
            CopyVideo(data, width, height, pitch);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Emulator] video callback failed: {exception.Message}");
        }
    }

    private void CopyVideo(IntPtr data, uint width, uint height, nuint pitch)
    {
        if (data == IntPtr.Zero || width == 0 || height == 0)
        {
            return;
        }

        if (data == HardwareFrameBuffer)
        {
            if (!warnedHardwareFrame)
            {
                warnedHardwareFrame = true;
                AepLog.Error("[Emulator] A hardware-rendered libretro frame was rejected. " +
                             "Use the Angrylion software renderer for Nintendo 64.");
            }

            return;
        }

        VideoWidth = checked((int)width);
        VideoHeight = checked((int)height);
        if (VideoAspectRatio <= 0.01f)
        {
            VideoAspectRatio = VideoWidth / (float)VideoHeight;
        }
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
        var rowBytes = checked(VideoWidth * 2);
        if (source16.Length < rowBytes)
        {
            source16 = new byte[rowBytes];
        }

        for (var row = 0; row < VideoHeight; row++)
        {
            Marshal.Copy(data + row * pitch, source16, 0, rowBytes);
            var destination = row * VideoWidth * 4;
            for (var column = 0; column < VideoWidth; column++)
            {
                var value = (ushort)(source16[column * 2] | source16[column * 2 + 1] << 8);
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
        try
        {
            audio?.Push(left, right);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Emulator] audio callback failed: {exception.Message}");
        }
    }

    private nuint OnAudioBatch(IntPtr data, nuint frames)
    {
        try
        {
            var count = checked((int)frames);
            audio?.Push(data, count);
            return frames;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Emulator] audio batch callback failed: {exception.Message}");
            return 0;
        }
    }

    private static void OnInputPoll()
    {
        try
        {
        }
        catch
        {
        }
    }

    private short OnInputState(uint port, uint device, uint index, uint id)
    {
        try
        {
            return ReadInputState(port, device, index, id);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Emulator] input callback failed: {exception.Message}");
            return 0;
        }
    }

    private short ReadInputState(uint port, uint device, uint index, uint id)
    {
        if (port != 0)
        {
            return 0;
        }

        if (device == PointerDevice && index == 0)
        {
            return id switch
            {
                PointerXId => input.PointerX,
                PointerYId => input.PointerY,
                PointerPressedId => input.PointerPressed ? (short)1 : (short)0,
                PointerCountId => input.PointerPressed ? (short)1 : (short)0,
                _ => 0,
            };
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

    private void ReplaceOptionString(string key, string value)
    {
        if (optionValues.Remove(key, out var previous))
        {
            Marshal.FreeCoTaskMem(previous);
        }

        optionValues[key] = Marshal.StringToCoTaskMemUTF8(value);
    }

    private static void WriteStringPointer(IntPtr target, IntPtr value)
    {
        if (target != IntPtr.Zero)
        {
            Marshal.WriteIntPtr(target, value);
        }
    }

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
        FreePointer(ref contentPathPointer);
    }

    private static void FreePointer(ref IntPtr pointer)
    {
        if (pointer == IntPtr.Zero)
        {
            return;
        }

        Marshal.FreeCoTaskMem(pointer);
        pointer = IntPtr.Zero;
    }

    public void Dispose()
    {
        UnloadGame();
        if (initialized)
        {
            api.Deinit();
            initialized = false;
        }

        foreach (var pointer in optionValues.Values)
        {
            Marshal.FreeCoTaskMem(pointer);
        }

        optionValues.Clear();
        FreePointer(ref systemDirectoryPointer);
        FreePointer(ref coreAssetsDirectoryPointer);
        FreePointer(ref saveDirectoryPointer);
        FreePointer(ref contentPathPointer);
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
