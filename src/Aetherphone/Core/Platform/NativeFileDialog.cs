using System.Diagnostics;
using System.Runtime.InteropServices;
using Aetherphone.Core.Localization;
using KernelDevice = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device;

namespace Aetherphone.Core.Platform;

internal static class NativeFileDialog
{
    private const int MaxPath = 4096;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnExplorer = 0x00080000;
    private const string ImageExtensions = "*.png;*.jpg;*.jpeg;*.bmp";
    private const string AudioExtensions = "*.mp3;*.wav";

    private static readonly string[] OverlayMarkers = { "reshade", "gshade", };
    private static readonly string[] GraphicsModules =
    {
        "dxgi.dll", "d3d11.dll", "d3d12.dll", "d3d9.dll", "opengl32.dll",
    };

    public static readonly bool RunsUnderWine = DetectWine();
    public static readonly bool HasShaderOverlay = DetectShaderOverlay();

    public static bool IsSupported => !RunsUnderWine && !HasShaderOverlay;

    public static Task<string?> OpenImageAsync(string title) =>
        OpenAsync(title, Filter(L.Common.FileKindImages, ImageExtensions), "[Wallpaper]");

    public static Task<string?> OpenAudioAsync(string title) =>
        OpenAsync(title, Filter(L.Common.FileKindAudio, AudioExtensions), "[Sound]");

    private static string Filter(LocString kind, string extensions) =>
        $"{Loc.T(kind)}\0{extensions}\0{Loc.T(L.Common.FileKindAll)}\0*.*\0";

    public static void PickImage(string title, Action<string> onPicked) => Complete(OpenImageAsync(title), onPicked);

    public static void PickAudio(string title, Action<string> onPicked) => Complete(OpenAudioAsync(title), onPicked);

    private static void Complete(Task<string?> dialog, Action<string> onPicked)
    {
        _ = dialog.ContinueWith(task =>
        {
            if (task.Status == TaskStatus.RanToCompletion && !string.IsNullOrEmpty(task.Result))
            {
                onPicked(task.Result);
            }
        });
    }

    private static Task<string?> OpenAsync(string title, string filter, string logTag)
    {
        var owner = GameWindow();
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(ShowDialog(title, filter, owner));
            }
            catch (Exception exception)
            {
                AepLog.Warning($"{logTag} file dialog failed: {exception.Message}");
                completion.SetResult(null);
            }
        }) { IsBackground = true, };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static unsafe IntPtr GameWindow()
    {
        var device = KernelDevice.Instance();
        return device == null ? IntPtr.Zero : (IntPtr)device->hWnd;
    }

    private static string? ShowDialog(string title, string filter, IntPtr owner)
    {
        var fileBuffer = Marshal.AllocHGlobal(MaxPath * sizeof(char));
        var filterBuffer = Marshal.StringToHGlobalUni(filter);
        try
        {
            for (var offset = 0; offset < MaxPath; offset++)
            {
                Marshal.WriteInt16(fileBuffer, offset * sizeof(char), 0);
            }

            var dialog = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                // An unowned picker can open behind the game window and read as a freeze.
                hwndOwner = owner,
                lpstrFilter = filterBuffer,
                nFilterIndex = 1,
                lpstrFile = fileBuffer,
                nMaxFile = MaxPath,
                lpstrTitle = title,
                Flags = OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnExplorer,
            };
            if (!GetOpenFileNameW(ref dialog))
            {
                return null;
            }

            var path = Marshal.PtrToStringUni(fileBuffer);
            return string.IsNullOrEmpty(path) ? null : path;
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
            Marshal.FreeHGlobal(filterBuffer);
        }
    }

    private static bool DetectWine()
    {
        try
        {
            var ntdll = GetModuleHandleA("ntdll.dll");
            return ntdll != IntPtr.Zero && GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[FilePicker] Wine probe failed: {exception.Message}");
            return false;
        }
    }

    private static bool DetectShaderOverlay()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var modules = process.Modules;
            for (var moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
            {
                if (IsOverlayModule(modules[moduleIndex]))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[FilePicker] Overlay probe failed: {exception.Message}");
            return true;
        }
    }

    private static bool IsOverlayModule(ProcessModule module)
    {
        var name = module.ModuleName;
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (MentionsOverlay(name))
        {
            return true;
        }

        if (!IsGraphicsModule(name))
        {
            return false;
        }

        var info = module.FileVersionInfo;
        return MentionsOverlay(info.ProductName) || MentionsOverlay(info.FileDescription) ||
               MentionsOverlay(info.CompanyName);
    }

    private static bool IsGraphicsModule(string name)
    {
        for (var moduleIndex = 0; moduleIndex < GraphicsModules.Length; moduleIndex++)
        {
            if (string.Equals(name, GraphicsModules[moduleIndex], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MentionsOverlay(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (var markerIndex = 0; markerIndex < OverlayMarkers.Length; markerIndex++)
        {
            if (value.Contains(OverlayMarkers[markerIndex], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OpenFileName dialog);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetModuleHandleA(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrFileTitle;
        public int nMaxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrInitialDir;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }
}
