using System.Runtime.InteropServices;

namespace Aetherphone.Core.Platform;

internal static class NativeFileDialog
{
    private const int MaxPath = 4096;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnExplorer = 0x00080000;
    private const string ImageFilter = "Images\0*.png;*.jpg;*.jpeg;*.bmp\0All Files\0*.*\0";
    private const string AudioFilter = "Audio\0*.mp3;*.wav\0All Files\0*.*\0";

    public static Task<string?> OpenImageAsync(string title) => OpenAsync(title, ImageFilter, "[Wallpaper]");

    public static Task<string?> OpenAudioAsync(string title) => OpenAsync(title, AudioFilter, "[Sound]");

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
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(ShowDialog(title, filter));
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

    private static string? ShowDialog(string title, string filter)
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
                hwndOwner = IntPtr.Zero,
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

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OpenFileName dialog);

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
