using System.Runtime.InteropServices;

namespace Aetherphone.Core.SystemMedia;

internal static unsafe class ShellAppNames
{
    private const int DefaultFolderFlags = 0;
    private const int NormalDisplay = 0;
    private const int GetDisplayNameSlot = 5;

    private static readonly Guid AppsFolderId = new("1e87508d-89c2-42f0-8a7e-645a0f50ca58");
    private static readonly Guid ShellItemId = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    public static string DisplayName(string appUserModelId)
    {
        if (appUserModelId.Length == 0)
        {
            return string.Empty;
        }

        var folderId = AppsFolderId;
        var itemId = ShellItemId;
        nint item = 0;
        try
        {
            if (!ComCall.Succeeded(SHCreateItemInKnownFolder(&folderId, DefaultFolderFlags, appUserModelId, &itemId,
                    &item)) || item == 0)
            {
                return string.Empty;
            }

            if (!ComCall.Succeeded(ComCall.GetPointerWithInt32(item, GetDisplayNameSlot, NormalDisplay,
                    out var name)) || name == 0)
            {
                return string.Empty;
            }

            try
            {
                return Marshal.PtrToStringUni(name) ?? string.Empty;
            }
            finally
            {
                Marshal.FreeCoTaskMem(name);
            }
        }
        finally
        {
            ComCall.Release(item);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemInKnownFolder(Guid* folderId, int flags, string itemName, Guid* interfaceId,
        nint* item);
}
