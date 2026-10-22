using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Harness.Native;

internal static unsafe class NativeImGuiLoader
{
    private const string BindingLibraryFileName = "cimgui.dll";
    private const string ImportLibraryName = "cimgui";

    public static string? LastAssert { get; private set; }

    public static string LibraryPath => Path.Combine(AppContext.BaseDirectory, BindingLibraryFileName);

    public static void Configure()
    {
        var path = LibraryPath;
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Native ImGui library missing next to the harness.", path);
        }

        var handle = NativeLibrary.Load(path);
        NativeLibrary.SetDllImportResolver(typeof(ImGui).Assembly,
            (name, _, _) => string.Equals(name, ImportLibraryName, StringComparison.Ordinal) ? handle : 0);
        InstallAssertHook(handle);
    }

    public static void ClearAssert() => LastAssert = null;

    private static void InstallAssertHook(nint libraryHandle)
    {
        if (!NativeLibrary.TryGetExport(libraryHandle, "igCustom_SetAssertCallback", out var setter))
        {
            return;
        }

        var install = (delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<byte*, byte*, int, void>, void>)setter;
        install(&OnAssert);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnAssert(byte* expression, byte* file, int line)
    {
        var message = $"ImGui assert: {ReadUtf8(expression)} ({ReadUtf8(file)}:{line})";
        LastAssert = message;
        Console.Error.WriteLine(message);
    }

    private static string ReadUtf8(byte* text)
    {
        if (text == null)
        {
            return string.Empty;
        }

        var length = 0;
        while (text[length] != 0)
        {
            length += 1;
        }

        return Encoding.UTF8.GetString(text, length);
    }
}
