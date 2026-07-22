using System.Runtime.InteropServices;

namespace Aetherphone.Core.Emulation.Libretro;

internal enum RetroPixelFormat
{
    Xrgb1555 = 0,
    Xrgb8888 = 1,
    Rgb565 = 2,
}

internal static class RetroEnvironmentCommand
{
    public const uint GetCanDupe = 3;
    public const uint Shutdown = 7;
    public const uint SetPerformanceLevel = 8;
    public const uint GetSystemDirectory = 9;
    public const uint SetPixelFormat = 10;
    public const uint SetInputDescriptors = 11;
    public const uint SetDiskControlInterface = 13;
    public const uint GetVariable = 15;
    public const uint SetVariables = 16;
    public const uint GetVariableUpdate = 17;
    public const uint SetSupportNoGame = 18;
    public const uint GetCoreAssetsDirectory = 30;
    public const uint GetSaveDirectory = 31;
    public const uint SetSystemAvInfo = 32;
    public const uint SetControllerInfo = 35;
    public const uint SetMemoryMaps = 36;
    public const uint SetGeometry = 37;
    public const uint GetLanguage = 39;
    public const uint SetSupportAchievements = 42;
    public const uint GetInputBitmasks = 51;
    public const uint GetCoreOptionsVersion = 52;
    public const uint SetCoreOptions = 53;
    public const uint SetCoreOptionsIntl = 54;
    public const uint SetCoreOptionsDisplay = 55;
    public const uint GetInputMaxUsers = 61;
    public const uint SetCoreOptionsV2 = 67;
    public const uint SetCoreOptionsV2Intl = 68;
    public const uint SetDiskControlExtInterface = 57;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RetroSystemInfo
{
    public IntPtr LibraryName;
    public IntPtr LibraryVersion;
    public IntPtr ValidExtensions;
    [MarshalAs(UnmanagedType.I1)] public bool NeedFullPath;
    [MarshalAs(UnmanagedType.I1)] public bool BlockExtract;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RetroGameGeometry
{
    public uint BaseWidth;
    public uint BaseHeight;
    public uint MaxWidth;
    public uint MaxHeight;
    public float AspectRatio;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RetroSystemTiming
{
    public double Fps;
    public double SampleRate;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RetroSystemAvInfo
{
    public RetroGameGeometry Geometry;
    public RetroSystemTiming Timing;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RetroGameInfo
{
    public IntPtr Path;
    public IntPtr Data;
    public nuint Size;
    public IntPtr Meta;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RetroVariable
{
    public IntPtr Key;
    public IntPtr Value;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RetroDiskControlCallback
{
    public IntPtr SetEjectState;
    public IntPtr GetEjectState;
    public IntPtr GetImageIndex;
    public IntPtr SetImageIndex;
    public IntPtr GetNumImages;
    public IntPtr ReplaceImageIndex;
    public IntPtr AddImageIndex;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
internal delegate bool RetroDiskSetEjectState([MarshalAs(UnmanagedType.I1)] bool ejected);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
internal delegate bool RetroDiskGetEjectState();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate uint RetroDiskGetImageIndex();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
internal delegate bool RetroDiskSetImageIndex(uint index);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate uint RetroDiskGetNumImages();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
internal delegate bool RetroEnvironmentCallback(uint command, IntPtr data);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroVideoRefreshCallback(IntPtr data, uint width, uint height, nuint pitch);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroAudioSampleCallback(short left, short right);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate nuint RetroAudioSampleBatchCallback(IntPtr data, nuint frames);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroInputPollCallback();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate short RetroInputStateCallback(uint port, uint device, uint index, uint id);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroSetControllerPortDevice(uint port, uint device);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate uint RetroApiVersion();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroSetEnvironment(RetroEnvironmentCallback callback);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroSetVideoRefresh(RetroVideoRefreshCallback callback);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroSetAudioSample(RetroAudioSampleCallback callback);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroSetAudioSampleBatch(RetroAudioSampleBatchCallback callback);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroSetInputPoll(RetroInputPollCallback callback);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroSetInputState(RetroInputStateCallback callback);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroVoid();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroGetSystemInfo(out RetroSystemInfo info);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RetroGetSystemAvInfo(out RetroSystemAvInfo info);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
internal delegate bool RetroLoadGame(ref RetroGameInfo info);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr RetroGetMemoryData(uint id);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate nuint RetroGetMemorySize(uint id);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate nuint RetroSerializeSize();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
internal delegate bool RetroSerialize(IntPtr data, nuint size);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
internal delegate bool RetroUnserialize(IntPtr data, nuint size);

internal sealed class LibretroApi : IDisposable
{
    private IntPtr library;

    public LibretroApi(string path)
    {
        library = NativeLibrary.Load(path);
        ApiVersion = Get<RetroApiVersion>("retro_api_version");
        SetEnvironment = Get<RetroSetEnvironment>("retro_set_environment");
        SetVideoRefresh = Get<RetroSetVideoRefresh>("retro_set_video_refresh");
        SetAudioSample = Get<RetroSetAudioSample>("retro_set_audio_sample");
        SetAudioSampleBatch = Get<RetroSetAudioSampleBatch>("retro_set_audio_sample_batch");
        SetInputPoll = Get<RetroSetInputPoll>("retro_set_input_poll");
        SetInputState = Get<RetroSetInputState>("retro_set_input_state");
        SetControllerPortDevice = Get<RetroSetControllerPortDevice>("retro_set_controller_port_device");
        Init = Get<RetroVoid>("retro_init");
        Deinit = Get<RetroVoid>("retro_deinit");
        GetSystemInfo = Get<RetroGetSystemInfo>("retro_get_system_info");
        GetSystemAvInfo = Get<RetroGetSystemAvInfo>("retro_get_system_av_info");
        LoadGame = Get<RetroLoadGame>("retro_load_game");
        UnloadGame = Get<RetroVoid>("retro_unload_game");
        Run = Get<RetroVoid>("retro_run");
        Reset = Get<RetroVoid>("retro_reset");
        GetMemoryData = Get<RetroGetMemoryData>("retro_get_memory_data");
        GetMemorySize = Get<RetroGetMemorySize>("retro_get_memory_size");
        SerializeSize = Get<RetroSerializeSize>("retro_serialize_size");
        Serialize = Get<RetroSerialize>("retro_serialize");
        Unserialize = Get<RetroUnserialize>("retro_unserialize");
    }

    public RetroApiVersion ApiVersion { get; }
    public RetroSetEnvironment SetEnvironment { get; }
    public RetroSetVideoRefresh SetVideoRefresh { get; }
    public RetroSetAudioSample SetAudioSample { get; }
    public RetroSetAudioSampleBatch SetAudioSampleBatch { get; }
    public RetroSetInputPoll SetInputPoll { get; }
    public RetroSetInputState SetInputState { get; }
    public RetroSetControllerPortDevice SetControllerPortDevice { get; }
    public RetroVoid Init { get; }
    public RetroVoid Deinit { get; }
    public RetroGetSystemInfo GetSystemInfo { get; }
    public RetroGetSystemAvInfo GetSystemAvInfo { get; }
    public RetroLoadGame LoadGame { get; }
    public RetroVoid UnloadGame { get; }
    public RetroVoid Run { get; }
    public RetroVoid Reset { get; }
    public RetroGetMemoryData GetMemoryData { get; }
    public RetroGetMemorySize GetMemorySize { get; }
    public RetroSerializeSize SerializeSize { get; }
    public RetroSerialize Serialize { get; }
    public RetroUnserialize Unserialize { get; }

    private T Get<T>(string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    public void Dispose()
    {
        if (library == IntPtr.Zero)
        {
            return;
        }

        NativeLibrary.Free(library);
        library = IntPtr.Zero;
    }
}
